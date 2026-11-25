# 0001 — Faux Discord en processus, branché sur les seams publics de NetCord

- **Statut** : Acceptée
- **Date** : 2026-11-25
- **Auteur** : initialisation du projet
- **Tâches liées** : M0, M1

## Contexte

On veut tester des bots NetCord avec les vrais clients NetCord, sans réseau. Deux références :

- **fauxcord** (TypeScript) : serveur HTTP + WebSocket autonome, stateful, ciblé
  « intégration multi-librairies » (dont Discord.Net) via changement d'URL de base.
- **shardix `@shardix/testing`** : adapter mock en mémoire + `executeInteraction`, ciblé
  « unit test » du framework, mais peu fidèle (payloads minimaux, pas d'état).

NetCord (référence `netcord`, `main` @ bb5f4e13) expose des seams publics :

- `RestClientConfiguration.RequestHandler : IRestRequestHandler`
  (`SendAsync(HttpRequestMessage, CancellationToken)`, `AddDefaultHeader`) ;
- `GatewayClientConfiguration.WebSocketConnectionProvider : IWebSocketConnectionProvider`
  (`IWebSocketConnection` : `OpenAsync/SendAsync/ReceiveAsync/CloseAsync/Abort`) ;
- `GatewayClientConfiguration.RestClientConfiguration`, `Compression`
  (`UncompressedGatewayCompression`), `Hostname`, `ReconnectStrategy`, `LatencyTimer` ;
- `Interaction.CreateFromJson(JsonInteraction, Guild?, InteractionResponseDelegate, RestClient)` ;
- `NetCord.JsonModels.*` publics.

## Options

### A — Serveur hors processus (façon fauxcord)

Kestrel + WebSocket, `Hostname` pointé dessus. Fidèle au transport, mais lent, ports,
TLS (`https://` codé en dur dans `RestClient`), parallélisme de tests compliqué.

### B — Mocks des types NetCord (façon Moq/NSubstitute)

Peu de types NetCord sont mockables (classes concrètes, constructeurs à `JsonModel`), aucune
cohérence d'état, cassant à chaque version.

### C — Faux Discord en processus sur les seams publics (retenue)

`IRestRequestHandler` + `IWebSocketConnection` en mémoire + `CreateFromJson`, un état unique.

## Décision

Option **C**. Le code du bot exécute le vrai NetCord (sérialisation, cache, rate limiter,
services, hosting) ; seul le transport est remplacé. Rapide (aucune socket), parallélisable,
déterministe, compatible AOT. Un mode serveur (A) reste envisageable plus tard en réutilisant
le même état et les mêmes handlers (M7, ADR séparée).

## Conséquences

- La compatibilité dépend des seams : toute évolution est surveillée par `/parity/sync`
  (tâches `PAR-SEAM-*`).
- Le fake doit produire des JSON exactement au format Discord (NetCord les désérialise
  strictement via ses `JsonModels`).
- La voix (UDP) est hors périmètre (pas de seam équivalent simple) → ➖.
