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
│  ├─ Interactions : requêtes → payload unique → 3 voies → InteractionResult    │
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

### Façade et seed

- [ADR 0004 — Façade, seed et journal](adr/0004-fake-discord-public-api.md) :
  `await using var discord = FakeDiscord.Create(options)` crée un fake isolé et une identité
  bot/application. `discord.CreateRestClient()` fournit un vrai client dont l'appelant est
  propriétaire. Chaque client a son handler en mémoire ; tous ceux du même fake voient le même
  état et alimentent le même journal. La fabrique Gateway est reportée à M3-ADR-09.
- `var channel = discord.Seed.Guild("Test").TextChannel("general")` crée les deux ressources
  immédiatement. La guilde et le salon sont des poignées avec `Id` ; les feuilles renvoient
  des IDs (`Member` renvoie celui de l'utilisateur). Pas de `out`, de `Build()` ni de faux POST.
- Le seed accepte un ID explicite non nul, jamais attribué dans ce fake. Sa milliseconde
  encodée fixe la date de création sans faire reculer l'horloge ni avancer le générateur.

### State

- [ADR 0003 — Modèle d'état et concurrence](adr/0003-state-model-and-concurrency.md) :
  un `FakeDiscordState` interne par fake, des dictionnaires mutables pour users, applications,
  guilds, channels, roles, members (clé guilde/utilisateur) et messages en M1.
- Lectures et commits sous un verrou synchrone unique. Seules des valeurs détachées en sortent ;
  la guilde et son rôle `@everyone`, par exemple, sont créés en une seule mutation.
- Au commit, les `StateChange` typés sont ordonnés et mis en file si la Gateway est abonnée.
  Elle les traduit hors verrou ; RESUME rejoue les dispatchs numérotés par session, pas l'état.
- `TimeProvider` et `ISnowflakeGenerator` injectables ; par défaut une horloge virtuelle fixe
  et des Snowflakes Discord strictement croissants, alloués avec un compteur de 22 bits
  par milliseconde.

### REST

- [ADR 0005 — Table de routes, pipeline REST et interception](adr/0005-rest-routing-and-pipeline.md) :
  `RouteTable` sépare l'inventaire statique dérivé hors ligne de la spec épinglée (méthode,
  template, `operationId`, politique d'authentification) des handlers enregistrés explicitement
  par clé `METHOD /template`. Une opération inventoriée peut ne pas encore avoir de handler.
- `FakeRestRequestHandler` retire un seul préfixe `/api/v{N}`, sépare la query et découpe le
  chemin **avant** de décoder chaque segment une fois (`%40me` → `@me`, `%2F` ne sépare pas
  deux segments). Le littéral prime sur le paramètre ; si le chemin est connu mais pas la
  méthode, le fake renvoie 405 avec les méthodes autorisées dans `Allow`.
- Le pipeline identifie l'appelant, résout l'opération et applique sa politique d'authentification
  (401, code `0` si l'authentification requise échoue). Suivent la validation structurelle,
  l'interception, puis le handler : contrôles métier et commit sous le verrou de l'ADR 0003.
  Hors verrou : sérialisation, ajout des en-têtes de quota, puis enregistrement de la tentative.
  Un échec local est journalisé sans réponse HTTP fictive.
- `DiscordError` est le catalogue interne de fabriques d'erreurs (statut HTTP et corps JSON), avec un arbre
  `errors` pour `50035` ([catalogue](parity/errors.md)). Pour une route non implémentée,
  `FakeDiscordOptions.UnimplementedRoutes` choisit `Lenient` (défaut : 404, code `0`) ou
  `Strict` (journalise puis lève `NotSupportedException`). Ce choix ne modifie aucune réponse
  d'un handler : un salon inconnu reste un 404, code `10003`.
- `discord.Rest.Intercept(route, effect)` applique un `RestEffect` à la prochaine tentative
  éligible, avant toute mutation : erreur du catalogue, délai virtuel via `TimeProvider` avant
  le traitement normal, ou 429 par utilisateur.
- En M1, NetCord lit les en-têtes de quota, mais les clients du fake utilisent un
  `IRateLimitManager` interne sans attente, car le gestionnaire par défaut peut retarder l'appel
  **avant** le handler, hors de l'horloge virtuelle. La simulation des quotas par bucket et
  des 429 qui en découlent relève de M6-02.

### Gateway

- `FakeWebSocketConnectionProvider` → `FakeWebSocketConnection` (paires de `Channel<byte[]>`).
- `FakeGatewaySession` : machine à états (Hello → Identified → Ready ↔ Resuming → Closed),
  séquence, intents, shard, file d'envoi, rejouage pour RESUME.
- Traduction `StateChange` → dispatch(s), filtrée par intents et shard.
- Commandes de test : `DispatchAsync(...)`, `DisconnectAsync(closeCode)`, `RequestReconnectAsync()`,
  `InvalidateSessionAsync(resumable)`, `FlushAsync()`, `WaitForReadyAsync(client)`.
- Heartbeat & timeouts pilotés par `TimeProvider`.

### Interactions

- [ADR 0007 — Modèle d'interaction](adr/0007-interaction-model.md) :
  `discord.Interactions.SlashCommand("greet", alice, general.Id).WithUser("user", bob)` décrit le
  geste d'un utilisateur à partir d'IDs du seed. À l'exécution, l'interaction devient une
  ressource de l'état (ID, jeton, instant de création) et le fake sérialise **un** payload,
  identique pour les trois voies.
- Trois voies de livraison :
  1. **Directe** (M2) : `request.ExecuteAsync(handler)` relit les octets avec NetCord, appelle
     `Interaction.CreateFromJson` puis le handler. Transport `Gateway` : la réponse initiale part
     en REST. Transport `Http` : pas de guilde, réponse inline comme `NetCord.Hosting.AspNetCore`.
  2. **Gateway** (M3-08) : le fait `InteractionCreated` devient un dispatch `INTERACTION_CREATE`
     pour un `GatewayClient` connecté ; la réponse revient par REST.
  3. **HTTP** (M5-02) : POST signé des mêmes octets vers l'endpoint ASP.NET Core du bot ; la
     réponse initiale est le corps de la réponse HTTP.
- Une seule capture, côté serveur : la route `POST /interactions/{interaction_id}/{interaction_token}/callback`
  et l'entrée inline mènent à la même mutation d'état. Réponse originale et followups passent
  par les routes `/webhooks/{webhook_id}/{webhook_token}…`, partagées avec les webhooks et
  distinguées par `webhook_id` = ID de l'application.
- Réponse et followups sont de vrais messages de l'état. Un message éphémère est lisible par
  les routes à jeton, inconnu des routes de salon et absent des dispatchs Gateway.
- Règles appliquées sous le verrou avec le `TimeProvider`, sans attente : réponse initiale
  unique (`40060`), délai de 3 s (`10062`), jeton de 15 min (`50027`), suite avant réponse
  initiale (`10015`), types de callback permis par type d'interaction.
- `InteractionResult` est une photographie détachée : `Callback`, `Response` et `Followups`
  (des `RestMessage` NetCord dans leur état courant), `Modal`, `AutocompleteChoices`, et les
  `Requests` du journal rattachées à l'interaction. `GetResult` en reprend une ;
  `WaitForAsync(id, condition)` réévalue une condition à chaque commit qui touche l'interaction.

### Services (package dédié)

- `FakeDiscordServicesBuilder` : ajoute modules / délégués minimal API, enregistre les commandes
  (`ApplicationCommandService.CreateCommandsAsync` contre le fake), exécute avec le bon contexte
  NetCord (Gateway ou Http), retourne `IExecutionResult` + `InteractionResult`.

### Hosting (package dédié)

- `services.AddFakeDiscord(discord)` / `builder.UseFakeDiscord(discord)` : `PostConfigure` des
  options NetCord (`GatewayClientOptions`, `ShardedGatewayClientOptions`, `RestClientOptions`)
  pour injecter `RequestHandler`, le `RateLimitManager` REST sans attente, `WebSocketConnectionProvider`,
  `Compression`, `Hostname`, le token et `PublicKey`.
- Signature Ed25519 des requêtes HTTP d'interaction (ADR à venir).

## Transverse

- **Enregistrements** : `discord.Recorded.Requests` renvoie un instantané détaché des
  tentatives REST terminées (méthode, chemin, template, paramètres, query, corps JSON,
  statut et réponse JSON). Le seed n'y écrit pas. Les réponses d'interaction se lisent dans
  `InteractionResult`, qui renvoie aussi aux requêtes du journal (ADR 0007) ; les frames Gateway
  relèvent de M3.
- **Assertions** : méthodes d'extension sans dépendance à un framework (lèvent
  `FakeDiscordAssertionException`), ex. `discord.Recorded.ShouldHaveRequested("POST /channels/{channel_id}/messages")`.
- **Options** : `FakeDiscordOptions` fixe l'identité (`BotToken`, `ApplicationId`), l'horloge
  et le générateur d'IDs. M1-ADR-05 précise l'option de traitement des routes non implémentées.
- **AOT** : sérialisation source-generated, sans réflexion ([ADR 0006 — Sérialisation](adr/0006-wire-serialization.md)).

## Arborescence `src/Enx.NetCord.Testing/`

```
FakeDiscord.cs  FakeDiscordOptions.cs
State/        (entités dont l'interaction et son jeton, FakeDiscordState, StateChange, ISnowflakeGenerator)
Rest/         (FakeDiscordRest, RestEffect, FakeRestRequestHandler, RouteTable, FakeRestRequest/Response,
               DiscordError, RateLimitHeaders, gestionnaire de rate limit sans attente)
Rest/Routes/<Domaine>/   (Channels, Messages, Reactions, Threads, Guilds, Members, Roles, Bans,
                          Emojis, Stickers, Webhooks, Invites, Users, Applications, Commands,
                          Interactions, AutoModeration, ScheduledEvents, StageInstances, Polls, …)
Gateway/      (FakeWebSocketConnection*, FakeGatewayServer, FakeGatewaySession, EventIntents)
Gateway/Dispatch/<Domaine>Dispatches.cs
Interactions/ (FakeDiscordInteractions, Requests/, exécution directe, traitement des callbacks,
               InteractionResult)
Seed/         (builders d'état fluents)
Recording/    (journal)
Assertions/
Serialization/ (FakeDiscordJsonContext, DTO internes du protocole Discord)
```
