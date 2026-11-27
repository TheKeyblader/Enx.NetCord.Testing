# Roadmap — tableau de bord multi-agents

Légende : `[ ]` à faire · `[~] (agent)` en cours · `[x]` fait (+ référence) · `[!]` bloqué (raison).
Format : `- [ ] ID — description — agent — dépend de`.
L'orchestrateur choisit la prochaine tâche dont les dépendances sont `[x]` (`/next`).
Les ADR « à rédiger » se lancent avec `/adr <sujet>`.

## M0 — Fondations du dépôt

- [x] M0-01 — `git init`, [`.gitignore`](../.gitignore), [`.editorconfig`](../.editorconfig), [`global.json`](../global.json) (SDK 10) — build (`/bootstrap`) — —
- [x] M0-02 — [Solution `.slnx`](../Enx.NetCord.Testing.slnx), [`Directory.Build.props`](../Directory.Build.props), [`Directory.Packages.props`](../Directory.Packages.props) (NetCord dernière version NuGet) — build — M0-01
- [x] M0-03 — Sept projets compilables dans [`src/`](../src/), [`tests/`](../tests/) et [`samples/`](../samples/) — build — M0-02
- [x] M0-04 — [Attribut `[Parity]`](../tests/Shared/ParityAttribute.cs) + un test placeholder dans chaque projet de test — build — M0-03
- [x] M0-05 — CI [`ci.yml`](../.github/workflows/ci.yml) (restore/build/test/format/pack sans push) — build — M0-03
- [x] M0-06 — Workflow [`parity-drift.yml`](../.github/workflows/parity-drift.yml) hebdo (issue si SHA en retard) — build — M0-05

## M1 — Noyau : état, REST, façade

ADR à rédiger d'abord :

- [x] M1-ADR-03 — Modèle d'état & concurrence (`FakeDiscordState`, `StateChange`, Snowflakes, `TimeProvider`) — architect — M0-03 — [ADR 0003](adr/0003-state-model-and-concurrency.md), review APPROVE
- [x] M1-ADR-04 — API publique `FakeDiscord` / `FakeDiscordOptions` / `Seed` / `Recorded` / création des clients NetCord — architect — M1-ADR-03 — [ADR 0004](adr/0004-fake-discord-public-api.md), review APPROVE
- [x] M1-ADR-05 — Table de routes, pipeline REST, `DiscordError`, mode Strict/Lenient, interception — architect — M1-ADR-03 — [ADR 0005](adr/0005-rest-routing-and-pipeline.md), review APPROVE
- [x] M1-ADR-06 — Sérialisation (réutilisation `NetCord.JsonModels` vs DTO propres, contexte STJ source-gen, AOT) — architect — M1-ADR-03 — [ADR 0006](adr/0006-wire-serialization.md), review APPROVE + skills/AGENTS réalignés

Implémentation :

- [x] M1-01 — `FakeDiscordState` + entités de base (user, application, guild, channel, role, member, message) — rest-implementer — M1-ADR-03 — [`State/`](../src/Enx.NetCord.Testing/State/) (`27c6325`, `1327b98`), review APPROVE
- [ ] M1-02 — `FakeDiscord` façade + `Seed` (guild/channel/member/role) + `CreateRestClient()` (via `RestClientFactory`) + câblage `FakeDiscord.Rest`, `FakeDiscordOptions.UnimplementedRoutes`, `RestPipelineContext.Close()` au dispose — rest-implementer — M1-ADR-04, M1-01, M1-03
- [x] M1-03 — `FakeRestRequestHandler` + `RouteTable` + auth `Bot` + `DiscordError` + en-têtes rate limit — rest-implementer — M1-ADR-05 — [`Rest/`](../src/Enx.NetCord.Testing/Rest/), [`Serialization/`](../src/Enx.NetCord.Testing/Serialization/) (`cf488a4`, `2cd2c6c`), review APPROVE
- [ ] M1-DEBT-01 — NITs M1-03 : `FakeRestRequest.Payload` immuable après validation (`init`/ctor) ; `errors.md:12-14` ligne `+ en-têtes` (liste CommonMark involontaire) — rest-implementer — M1-03
- [ ] M1-04 — `Recorded.Requests` + assertions de base — rest-implementer — M1-03
- [ ] M1-REST-01 — Users : `GET/PATCH /users/@me`, `GET /users/{user_id}`, `POST /users/@me/channels` (DM) — rest-implementer — M1-03
- [ ] M1-REST-02 — Channels : `GET/PATCH/DELETE /channels/{channel_id}` — rest-implementer — M1-03
- [ ] M1-REST-03 — Messages : list/get/create/edit/delete/bulk-delete/crosspost (JSON + multipart) — rest-implementer — M1-REST-02
- [ ] M1-REST-04 — Réactions : put/delete @me, delete user, list, delete all, delete emoji — rest-implementer — M1-REST-03
- [ ] M1-REST-05 — Pins (nouvelle et ancienne API), typing — rest-implementer — M1-REST-03
- [ ] M1-REST-06 — Guilds : get/modify/preview, channels list/create/positions — rest-implementer — M1-03
- [ ] M1-REST-07 — Members : get/list/search/modify/@me/add role/remove role/kick — rest-implementer — M1-REST-06
- [ ] M1-REST-08 — Roles : list/get/create/modify/positions/delete (`@everyone` = guild id) — rest-implementer — M1-REST-06
- [ ] M1-REST-09 — Bans : list/get/create/delete/bulk-ban — rest-implementer — M1-REST-07
- [ ] M1-T-01 — Conformité M1 via `RestClient` NetCord + validation de contrat JSON (openapi) — test-engineer — M1-REST-03
- [ ] M1-R — Review de jalon + `/parity/audit rest` — reviewer, parity-auditor — M1-*

## M2 — Interactions & NetCord.Services

- [x] M2-ADR-07 — Modèle d'interaction (builders, `InteractionResult`, 3 voies d'exécution, règles 40060/15 min) — architect — M1-ADR-04 — [ADR 0007](adr/0007-interaction-model.md) (`884eb26`, `8c9eb78`, `5fbc9a0`), review APPROVE
- [ ] M2-ADR-08 — Harnais `Enx.NetCord.Testing.Services` (API, DI, enregistrement des commandes) — architect — M2-ADR-07
- [ ] M2-01 — Builders slash command (options, sous-commandes, `resolved`) + exécution directe — interactions-implementer — M2-ADR-07
- [ ] M2-02 — Callbacks : Pong, Message, DeferredMessage, DeferredModifyMessage, ModifyMessage, Autocomplete, Modal + route `POST /interactions/{interaction_id}/{interaction_token}/callback` (ADR 0007) — interactions-implementer — M2-01, M1-03, M1-04
- [ ] M2-03 — REST interactions à jeton : original response get/edit/delete, followups (`/webhooks/{application_id}/{token}…`), `WaitForAsync`, `GetResult` — interactions-implementer — M2-02, M1-REST-03
- [ ] M2-04 — User/Message commands, Autocomplete — interactions-implementer — M2-01
- [ ] M2-05 — Composants : bouton, string/user/role/mentionable/channel menus ; modals (text input, Label, FileUpload, Radio/Checkbox) — interactions-implementer — M2-02
- [ ] M2-06 — Components V2 (Section, TextDisplay, Thumbnail, MediaGallery, File, Separator, Container) dans les messages — interactions-implementer — M2-05
- [ ] M2-07 — Application commands REST (global/guild CRUD + bulk overwrite + permissions) — rest-implementer — M1-03
- [ ] M2-08 — Harnais Services : ApplicationCommandService (Gateway & Http contexts) — interactions-implementer — M2-ADR-08, M2-07
- [ ] M2-09 — Harnais Services : ComponentInteractionService, CommandService (text commands via MESSAGE_CREATE) — interactions-implementer — M2-08
- [ ] M2-10 — `Interaction.Guild` en voie directe (projection `GUILD_CREATE` minimale partagée avec M3-02) — interactions-implementer — M2-01, M1-REST-06
- [ ] M2-11 — Validation des requêtes d'interaction contre les commandes visibles (guilde du salon + globales) — interactions-implementer — M2-04, M2-07
- [ ] M2-T-01 — Sample bot : `/ping`, `/greet user`, bouton de confirmation, modal + tests — test-engineer — M2-08
- [ ] M2-R — Review de jalon + `/parity/audit interactions` — reviewer, parity-auditor — M2-*

## M3 — Gateway

- [ ] M3-ADR-09 — Gateway fake : connexion mémoire, barrières (`FlushAsync`), commandes de test, sharding — architect — M1-ADR-03
- [ ] M3-01 — `FakeWebSocketConnection(Provider)` + HELLO/IDENTIFY/READY/HEARTBEAT(ACK) — gateway-implementer — M3-ADR-09
- [ ] M3-02 — GUILD_CREATE initial complet (cache NetCord rempli) — gateway-implementer — M3-01
- [ ] M3-03 — Traduction `StateChange` → dispatch : messages, réactions, channels, threads, guild, members, roles, bans — gateway-implementer — M3-02
- [ ] M3-04 — Intents (dont privilégiés, MESSAGE_CONTENT) — gateway-implementer — M3-03
- [ ] M3-05 — RESUME/RESUMED, RECONNECT, INVALID_SESSION, close codes 4000–4014 — gateway-implementer — M3-01
- [ ] M3-06 — Sharding (`ShardedGatewayClient`), routage par shard — gateway-implementer — M3-03
- [ ] M3-07 — Send events : PRESENCE_UPDATE, VOICE_STATE_UPDATE (état seulement), REQUEST_GUILD_MEMBERS → GUILD_MEMBERS_CHUNK, REQUEST_SOUNDBOARD_SOUNDS — gateway-implementer — M3-03
- [ ] M3-08 — INTERACTION_CREATE via Gateway (voie 2) — interactions-implementer — M3-02, M2-03
- [ ] M3-R — Review + `/parity/audit gateway` — reviewer, parity-auditor — M3-*

## M4 — Couverture REST étendue

- [ ] M4-REST-01 — Threads (create from message/without, members, archived lists, `/guilds/{id}/threads/active`) — rest-implementer — M1-REST-03
- [ ] M4-REST-02 — Webhooks (CRUD, exécution `?wait`, messages de webhook, avec token) — rest-implementer — M1-REST-03
- [ ] M4-REST-03 — Invites (channel/guild/get/delete) — rest-implementer — M1-REST-06
- [ ] M4-REST-04 — Emojis (guild + application), stickers — rest-implementer — M1-REST-06
- [ ] M4-REST-05 — Permissions overwrites + calcul des permissions (`app_permissions`, 50013) — rest-implementer — M1-REST-08
- [ ] M4-REST-06 — Polls (create via message, voters, expire) + events vote — rest-implementer — M1-REST-03
- [ ] M4-REST-07 — Scheduled events, stage instances, auto-moderation rules — rest-implementer — M1-REST-06
- [ ] M4-REST-08 — Audit log (entrées générées par les mutations, `X-Audit-Log-Reason`) — rest-implementer — M1-REST-06
- [ ] M4-REST-09 — Entitlements/SKUs de test, soundboard, voice regions, gateway (`/gateway`, `/gateway/bot`) — rest-implementer — M1-03
- [ ] M4-R — Review + `/parity/audit rest` — reviewer, parity-auditor — M4-*

## M5 — Hosting

- [ ] M5-ADR-10 — Intégration Hosting (PostConfigure des options NetCord) + signature Ed25519 (libsodium P/Invoke vs managé) — architect — M2-ADR-07
- [ ] M5-01 — `UseFakeDiscord` pour `IHostBuilder`/`IServiceCollection` (Gateway, Sharded, Rest) — interactions-implementer — M5-ADR-10, M3-01
- [ ] M5-02 — HTTP interactions signées vers `NetCord.Hosting.AspNetCore` (+ `WebApplicationFactory`) — interactions-implementer — M5-01
- [ ] M5-03 — Sample : host complet testé (gateway + HTTP) — test-engineer — M5-02

## M6 — Qualité, docs, release

- [ ] M6-01 — Guides `docs/guides/01..08` + README — docs-writer — M2, M3
- [ ] M6-02 — Simulation de rate limits (429 + `retry_after`, bucket) opt-in — rest-implementer — M1-03
- [ ] M6-03 — Benchmarks + chasse aux tests flaky (50 runs) — test-engineer — M3
- [ ] M6-04 — Packaging NuGet (README, icône, SourceLink, symboles) — build — M6-01
- [ ] M6-05 — Release 1.0 (tag + publication : **action manuelle de l'utilisateur**) — utilisateur — M6-04

## M7 — Exploratoire

- [ ] M7-ADR-11 — Mode serveur hors processus (Kestrel/WebSocket) réutilisant état + handlers — architect — M4

## Backlog parité

<!-- Ajouté par parity-auditor : - [ ] PAR-<domaine>-NN — écart — agent — dépend de -->

- [ ] PAR-INT-01 — `interactions.md` selon ADR 0007 : ajouter `rule:callback-type`, `rule:followup-after-defer`, `rule:unacked-token`, `rule:rejected-callbacks`, `harness:direct-http-transport` ; corriger `rule:3s-deadline`, `rule:original-response`, `rule:followups`, note `interaction:2/primary_entry_point` ; lier `rule:token-15min`/`rule:ephemeral` à l'ADR — parity-auditor — M2-ADR-07
- [ ] PAR-ERR-01 — Confirmer contre Discord (hypothèses ADR 0007) : statuts/libellés `40060`, `10062`, `10015`, `50027`, `10008` ; jeton jamais émis (401/50027 vs 404/10015 fauxcord) ; followup avant réponse initiale ; type de callback interdit ; règles autocomplete ; type 12 ; paramètres de followup ignorés ; non-dispatch des éphémères — parity-auditor — M2-ADR-07
