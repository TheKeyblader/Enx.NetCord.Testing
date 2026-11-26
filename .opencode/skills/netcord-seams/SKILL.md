---
name: NetCord seams
description: Points d'extension publics de NetCord utilisés par le faux Discord (IRestRequestHandler, IWebSocketConnectionProvider, Interaction.CreateFromJson, JsonModels, contexts NetCord.Services, Hosting). À charger avant de concevoir ou d'implémenter quoi que ce soit qui se branche sur NetCord.
---

# Seams NetCord

Chemins relatifs à la référence opencode `netcord` (dépôt `NetCordDev/NetCord`).
**Toujours revérifier la signature dans la référence** avant de l'utiliser : NetCord évolue
(MINOR peut casser). Si une signature a changé, signale-le (tâche `PAR-SEAM-NN`).

## REST

| Seam | Fichier | Usage par le fake |
| --- | --- | --- |
| `RestClientConfiguration.RequestHandler : IRestRequestHandler` | `NetCord/Rest/RestClientConfiguration.cs`, `NetCord/Rest/IRestRequestHandler.cs` | `FakeRestRequestHandler.SendAsync(HttpRequestMessage, CancellationToken)` route vers les handlers et renvoie un `HttpResponseMessage` Discord-fidèle. `AddDefaultHeader` : mémoriser (User-Agent, Authorization). |
| `RestClientConfiguration.Hostname` | idem | Valeur sentinelle (`fake.discord.test`) pour reconnaître les URI. |
| `RestClientConfiguration.RateLimitManager : IRateLimitManager` | `NetCord/Rest/RateLimits/` | Toujours injecter le gestionnaire interne sans attente du fake (ADR 0005) : le limiteur par défaut de NetCord peut dormir en temps réel avant le handler, hors de l'horloge virtuelle. NetCord continue de lire les en-têtes `X-RateLimit-*`. |
| URI construite | `NetCord/Rest/RestClient.cs` : `_baseUri = $"https://{Hostname}/api/v{N}"`, `uri = $"{_baseUri}{route}{query}"` | Retirer `/api/v{N}`, décoder (`%40me`), séparer la query. |
| Routes | `NetCord/Rest/RestClient.*.cs` : `SendRequestAsync(HttpMethod.X, [content,] $"/route", query, new(TopLevelId), ...)` | Source de la colonne `NetCord` de `docs/parity/rest.md`. |
| Corps | `JsonContent<T>` (JSON) ou `MultipartFormDataContent` (`payload_json` + `files[n]`) | Le fake doit parser les deux. |

## Gateway

| Seam | Fichier | Usage |
| --- | --- | --- |
| `GatewayClientConfiguration.WebSocketConnectionProvider : IWebSocketConnectionProvider` | `NetCord/Gateway/GatewayClientConfiguration.cs`, `NetCord/Gateway/WebSockets/IWebSocketConnection*.cs` | `CreateConnection()` → `FakeWebSocketConnection` en mémoire (`OpenAsync`, `SendAsync`, `ReceiveAsync` → `WebSocketConnectionReceiveResult`, `CloseAsync`, `Abort`, `CloseStatus`). |
| `GatewayClientConfiguration.Compression` | `NetCord/Gateway/Compression/UncompressedGatewayCompression.cs` | Forcer `new UncompressedGatewayCompression()`. |
| `GatewayClientConfiguration.Hostname`, `Version`, `Intents`, `Shard` | idem | Hostname sentinelle ; intents/shard lus dans IDENTIFY côté fake. |
| `GatewayClientConfiguration.RestClientConfiguration` | idem (ligne ~81) | Y injecter le `RequestHandler` **et** le `RateLimitManager` sans attente du fake : `GatewayClient.Rest` pointe alors sur le fake sans attente réelle. |
| `ReconnectStrategy`, `LatencyTimer`, `RateLimiterProvider` | `NetCord/Gateway/ReconnectStrategies/`, `LatencyTimers/`, `IRateLimiterProvider.cs` | Stratégie de reconnexion sans délai pour les tests ; `NullRateLimiterProvider` dispo. |
| Sharding | `NetCord/Gateway/ShardedGatewayClient*.cs` | Même `RestClientConfiguration` + `WebSocketConnectionProvider` via la configuration factory. |
| Dispatch | `NetCord/Gateway/GatewayClient.cs` : `case "EVENT_NAME":` | Source de la colonne `NetCord` de `docs/parity/gateway.md`. |
| Cache | `IGatewayClientCacheProvider`, `ImmutableGatewayClientCache` | Le cache est rempli par READY/GUILD_CREATE : le fake doit les envoyer complets. |

## Interactions

| Seam | Fichier | Usage |
| --- | --- | --- |
| `Interaction.CreateFromJson(JsonInteraction, Guild?, InteractionResponseDelegate, RestClient)` | `NetCord/Interaction.cs` | Créer une interaction NetCord sans Gateway, avec un delegate de réponse qui enregistre le callback dans le fake. |
| `Interaction.CreateFromJson(JsonInteraction, IGatewayClientCache, RestClient)` | idem | Variante « comme via Gateway » : la réponse part en REST `POST /interactions/{id}/{token}/callback` → passe par le handler fake. |
| `NetCord.JsonModels.*` (public) | `NetCord/JsonModels/**` | Fournir un `JsonInteraction` à `Interaction.CreateFromJson`. Pour les réponses JSON, le fake sérialise ses DTO internes avec `FakeDiscordJsonContext` ; le vrai client les désérialise. |
| Callbacks | `NetCord/Rest/InteractionCallback*.cs`, `InteractionCallbackType.cs` | Désérialiser ce que NetCord envoie pour remplir `InteractionResult`. |

## NetCord.Services

- `ApplicationCommandService<TContext>.ExecuteAsync(TContext, IServiceProvider?)`,
  `ComponentInteractionService<TContext>.ExecuteAsync(...)`,
  `CommandService<TContext>.ExecuteAsync(ReadOnlyMemory<char>|int prefixLength, TContext, IServiceProvider?)`.
- Contextes : `ApplicationCommandContext(interaction, GatewayClient)`,
  `HttpApplicationCommandContext(interaction, RestClient)`, `SlashCommandContext`, `ButtonInteractionContext`,
  `ModalInteractionContext`, `CommandContext(Message, GatewayClient)`… (`NetCord.Services/**/…Contexts.cs`).
- Inspiration : `Tests/ServicesTest/**` (comment NetCord teste ses services).

## Hosting

- `Hosting/NetCord.Hosting/Gateway/GatewayClientOptions.cs`, `Rest/RestClientOptions.cs`
  (`IDiscordOptions` : `Token`, `PublicKey`, configurations) → le fake fournit un
  `IConfigureOptions`/`PostConfigure` qui injecte ses seams.
- `Hosting/NetCord.Hosting.AspNetCore/HttpEventParser.cs` : en-têtes
  `X-Signature-Ed25519` / `X-Signature-Timestamp`, validation via `NetCord/Rest/HttpEventValidator.cs`
  (libsodium).

## Interdits

- Réflexion / `UnsafeAccessor` sur des membres non publics de NetCord.
- Copier du code NetCord dans `src/` (licence MIT OK, mais dette de synchro) — sauf ADR.
