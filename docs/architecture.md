# Architecture — Enx.NetCord.Testing

> Document vivant, tenu par l'agent `architect`. Les décisions sont tracées dans `docs/adr/`.
> Les noms de types ci-dessous sont **indicatifs** tant que l'ADR correspondante n'est pas
> « Acceptée ».

## Vue d'ensemble

```
             Code du bot (modules NetCord.Services, handlers Gateway, IHost)
                    │ utilise les vrais types NetCord
     ┌──────────────┼───────────────────────────┬────────────────────────┐
 RestClient     GatewayClient / Sharded      Interaction.CreateFromJson   ASP.NET Core
     │ IRestRequestHandler   │ IWebSocketConnectionProvider   │            │ POST signé
     ▼                       ▼                                ▼            ▼
┌────────────────────────────────────────────────────────────────────────────────┐
│ FakeDiscord (façade publique, IAsyncDisposable)                               │
│  ├─ Rest     : FakeRestRequestHandler → Router → Handlers/<Domaine>           │
│  │             (auth, validation, erreurs Discord, en-têtes rate limit,       │
│  │              enregistrement des requêtes)                                  │
│  ├─ Gateway  : FakeGatewayServer → sessions → FakeWebSocketConnection         │
│  │             (HELLO/IDENTIFY/READY/HEARTBEAT/RESUME, intents, shards)       │
│  ├─ Interactions : builders → JsonInteraction → exécution → InteractionResult │
│  ├─ Seed     : builders d'état (guildes, salons, membres, rôles, messages…)   │
│  ├─ Recorded : journal des requêtes REST, frames Gateway, callbacks           │
│  └─ State    : FakeDiscordState (source unique) ──StateChange──► Gateway      │
│                ISnowflakeGenerator · TimeProvider · options                    │
└────────────────────────────────────────────────────────────────────────────────┘
```

## Packages

| Package | Dépend de | Contenu |
| --- | --- | --- |
| `Enx.NetCord.Testing` | `NetCord` | État, REST, Gateway, interactions bas niveau, seed, enregistrements, assertions agnostiques |
| `Enx.NetCord.Testing.Services` | + `NetCord.Services` | Harnais modules : enregistrement des commandes contre le fake, exécution avec contextes NetCord, résultats |
| `Enx.NetCord.Testing.Hosting` | + `NetCord.Hosting*`, `Microsoft.AspNetCore.*` (FrameworkReference) | `UseFakeDiscord(...)` pour `IHostBuilder`/`IServiceCollection`, signature Ed25519, helpers `WebApplicationFactory` |

## Couches

### State

- `FakeDiscordState` : entités internes (users, applications, guilds, channels/threads,
  members, roles, emojis, stickers, messages, reactions, webhooks, invites, bans,
  application commands, interactions en cours, entitlements…).
- Thread-safe (verrou unique ou `ImmutableInterlocked`, à trancher : ADR 0003).
- Chaque mutation émet un `StateChange` typé (ex. `MessageCreated`) consommé par la Gateway.
- IDs via `ISnowflakeGenerator` (déterministe par défaut : séquence croissante à partir d'un
  epoch fixe) ; temps via `TimeProvider`.

### REST

- `FakeRestRequestHandler : IRestRequestHandler` : reçoit le `HttpRequestMessage` NetCord,
  extrait le chemin après `/api/v{N}`, le corps (JSON / multipart `payload_json` + `files[n]`),
  l'auth (`Bot <token>`, `Bearer`), puis route.
- `RouteTable` : templates **identiques à la spec** (`/channels/{channel_id}/messages/{message_id}`),
  littéraux avant paramètres. Une route inconnue ⇒ `404 {"message":"404: Not Found","code":0}`
  + entrée « non implémentée » dans `Recorded` (option : lever une exception pour détecter les trous).
- Pipeline : auth → résolution → validation → handler → sérialisation → en-têtes rate limit →
  enregistrement.
- Erreurs via `DiscordError` (catalogue = `docs/parity/errors.md`).
- Hooks de test : `discord.Rest.Intercept("POST /channels/{channel_id}/messages", ...)` pour
  forcer une erreur/latence/429 sur une route donnée.

### Gateway

- `FakeWebSocketConnectionProvider` → `FakeWebSocketConnection` (paires de `Channel<byte[]>`).
- `FakeGatewaySession` : machine à états (Hello → Identified → Ready ↔ Resuming → Closed),
  séquence, intents, shard, file d'envoi, rejouage pour RESUME.
- Traduction `StateChange` → dispatch(s), filtrée par intents et shard.
- Commandes de test : `DispatchAsync(...)`, `DisconnectAsync(closeCode)`, `RequestReconnectAsync()`,
  `InvalidateSessionAsync(resumable)`, `FlushAsync()`, `WaitForReadyAsync(client)`.
- Heartbeat & timeouts pilotés par `TimeProvider`.

### Interactions

- Builders → `JsonInteraction` cohérent avec l'état.
- Trois voies d'exécution :
  1. **Directe** : `Interaction.CreateFromJson(json, guild, delegate, rest)` ; le delegate
     enregistre le callback dans l'état (la plus rapide, pour tester un module isolé).
  2. **Gateway** : dispatch `INTERACTION_CREATE` vers un `GatewayClient` connecté ; la réponse
     revient par REST `POST /interactions/{id}/{token}/callback`.
  3. **HTTP** : POST signé vers l'endpoint ASP.NET Core du bot (`Hosting`).
- `InteractionResult` agrège callback, followups, edits, suppressions, modal, choix d'autocomplete.

### Services (package dédié)

- `FakeDiscordServicesBuilder` : ajoute modules / délégués minimal API, enregistre les commandes
  (`ApplicationCommandService.CreateCommandsAsync` contre le fake), exécute avec le bon contexte
  NetCord (Gateway ou Http), retourne `IExecutionResult` + `InteractionResult`.

### Hosting (package dédié)

- `services.AddFakeDiscord(discord)` / `builder.UseFakeDiscord(discord)` : `PostConfigure` des
  options NetCord (`GatewayClientOptions`, `ShardedGatewayClientOptions`, `RestClientOptions`)
  pour injecter `RequestHandler`, `WebSocketConnectionProvider`, `Compression`, `Hostname`, token
  et `PublicKey`.
- Signature Ed25519 des requêtes HTTP d'interaction (ADR à venir).

## Transverse

- **Enregistrements** (`discord.Recorded`) : requêtes REST (méthode, template, params, corps,
  réponse), frames Gateway envoyées/reçues, callbacks d'interaction.
- **Assertions** : méthodes d'extension sans dépendance à un framework (lèvent
  `FakeDiscordAssertionException`), ex. `discord.Recorded.ShouldHaveRequested("POST /channels/{channel_id}/messages")`.
- **Options** : `FakeDiscordOptions` (token, application, strictness : `Strict` lève sur route
  non implémentée / `Lenient` renvoie 404, simulation rate limit, latence virtuelle).
- **AOT** : sérialisation source-generated, pas de réflexion.

## Arborescence `src/Enx.NetCord.Testing/`

```
FakeDiscord.cs  FakeDiscordOptions.cs
State/        (entités, FakeDiscordState, StateChange, ISnowflakeGenerator)
Rest/         (FakeRestRequestHandler, RouteTable, FakeRestRequest/Response, DiscordError, RateLimitHeaders)
Rest/Routes/<Domaine>/   (Channels, Messages, Reactions, Threads, Guilds, Members, Roles, Bans,
                          Emojis, Stickers, Webhooks, Invites, Users, Applications, Commands,
                          Interactions, AutoModeration, ScheduledEvents, StageInstances, Polls, …)
Gateway/      (FakeWebSocketConnection*, FakeGatewayServer, FakeGatewaySession, EventIntents)
Gateway/Dispatch/<Domaine>Dispatches.cs
Interactions/ (Builders/, InteractionExecutor, InteractionResult)
Seed/         (builders d'état fluents)
Recording/    (journal)
Assertions/
Serialization/ (JsonSerializerContext du fake)
```
