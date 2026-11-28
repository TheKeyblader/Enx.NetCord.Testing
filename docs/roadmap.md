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
- [x] M1-02 — `FakeDiscord` façade + `Seed` (guild/channel/member/role) + `CreateRestClient()` + `FakeDiscord.Rest`, `FakeDiscordOptions.{BotToken,ApplicationId,UnimplementedRoutes}`, `Close()` au dispose — rest-implementer — M1-ADR-04, M1-01, M1-03 — [`FakeDiscord.cs`](../src/Enx.NetCord.Testing/FakeDiscord.cs), [`Seed/`](../src/Enx.NetCord.Testing/Seed/) (`cfc6ff0`), review APPROVE
- [x] M1-03 — `FakeRestRequestHandler` + `RouteTable` + auth `Bot` + `DiscordError` + en-têtes rate limit — rest-implementer — M1-ADR-05 — [`Rest/`](../src/Enx.NetCord.Testing/Rest/), [`Serialization/`](../src/Enx.NetCord.Testing/Serialization/) (`cf488a4`, `2cd2c6c`), review APPROVE
- [x] M1-DEBT-01 — NITs M1-03 : `FakeRestRequest.Payload` immuable (ctor + `WithPayload`) ; `errors.md` ligne `+ en-têtes` reformulée — rest-implementer — M1-03 — (`5398767`), review APPROVE
- [x] M1-DEBT-02 — NITs M1-02 : paramètre `state` obligatoire dans `FakeRestRequest` ; test `CreateRestClient_DefaultOptions_IsLenient` qui teste vraiment le défaut ; supprimer/reformuler `Create_DefaultTable_IsTheTableOfRestRoutes` ; une seule source des valeurs par défaut (`FakeDiscord.cs:76-78` vs `FakeDiscordState.Create`) — rest-implementer — M1-02 — horloge unique `State.Clock` (`3450dd7`, `2688d07`), review APPROVE
- [x] M1-ADR-04B — Adhésion du bot aux guildes seedées (implicite vs `Seed.Guild(..., withBot)`), visibilité REST (`10004`/`50001`) et Gateway (`READY.guilds`) ; amendement ADR 0004 — architect — M1-02 — [amendement 04B](adr/0004-fake-discord-public-api.md#amendement-04b--adhésion-du-bot-aux-guildes-seedées) (`324be21`), review APPROVE
- [x] M1-04 — `Recorded.Requests` (`DiscordRecording`, `RecordedRestRequest`, `FakeDiscord.Recorded`, ADR 0004/0005) ; les assertions sont déplacées en M1-04B — rest-implementer — M1-03 — [`Recording/`](../src/Enx.NetCord.Testing/Recording/) (`e3ffa9b`, `56e67e8`), review APPROVE
- [x] M1-05 — Adhésion du bot (amendement 04B) : `Seed.Guild(…, withBot)`, adhésion dans le commit de création et `GuildCreated`, `CreateGuildInput.WithBot`, `FakeDiscord.ApplicationId`, helper `GuildAccess`, appelant `StateCaller` (`Fixture`, `Bot`), `StateFailureKind.MissingAccess`, `DiscordError.UnknownGuild()`/`MissingAccess()` + `errors.md`, couples `RestEffect.Error`, ajustement de `DiscordSeedTests.cs:202,214,364` — rest-implementer — M1-ADR-04B, M1-04 — [`State/GuildAccess.cs`](../src/Enx.NetCord.Testing/State/GuildAccess.cs), [`StateCaller.cs`](../src/Enx.NetCord.Testing/State/StateCaller.cs) (`3338d6a`, `a4f69c8`, merge `afd17f0`), review APPROVE ; `errors.md` `10004`/`50001` Fake 🟡 Tests ✅ ; skill `fake-rest-endpoint` (visibilité)
- [x] M1-ADR-12 — Assertions agnostiques : `FakeDiscordAssertionException`, `ShouldHaveRequested(...)` et premières assertions sur `Recorded` (ADR 0002 n'en fixe que l'exception, ADR 0004 les exclut) — architect — M1-04 — [ADR 0012](adr/0012-agnostic-assertions.md) (`5b72b9d`, `cd39440`, merge `96e070a`), review APPROVE ; 3 choix d'ergonomie confirmés par l'utilisateur
- [x] M1-04B — [`Assertions/`](../src/Enx.NetCord.Testing/Assertions/) (`e637a94`, `f2c906f`, `5ab851d`, `a791792`, `92eb887`), review APPROVE ; inventaire régénéré à l'identique, skills `fake-rest-endpoint` § 4 et `dotnet-test-conventions` à jour — Assertions de base (ADR 0012) : `FakeDiscordAssertionException` (+ interface interne `IAssertionException`), `RecordedRequestAssertions` (`ShouldHaveRequested`, `ShouldHaveRequestedOnce`, `ShouldNotHaveRequested` sur `DiscordRecording` et `IEnumerable<RecordedRestRequest>`), motif de route résolu par `RouteTable` avec ses gardes de valeur (segment `%XX`, Snowflake = `ulong` canonique, jeton via `RestRedaction.IsTokenName`, `[redacted]`), liste générée des paramètres `SnowflakeType` (`tools/generate-route-inventory.py` → `RouteInventory.g.cs`), message d'échec du § 5, accès interne de `DiscordRecording` à la table ; tests des tableaux § 3 et § 4, suite paresseuse non énumérée, interface nommée `IAssertionException` — rest-implementer — M1-ADR-12, M1-04
- [x] M1-REST-01 — Users : `GET/PATCH /users/@me`, `GET /users/{user_id}`, `POST /users/@me/channels` (DM) — rest-implementer — M1-03 — [`Routes/Users/`](../src/Enx.NetCord.Testing/Rest/Routes/Users/) (`e4a2df8`, `0bd0dba`, `f669901`), review APPROVE ; PATCH et DM 🟡 (group DM, `50007`)
- [x] M1-DEBT-03 — Factoriser les aides de validation locales à `Routes/Users/UserValidators.cs` (snowflake de route/corps, corps objet, `MODEL_TYPE_CONVERT`) dans `Rest/` pour toutes les familles — rest-implementer — M1-REST-01 — [`Rest/FormValidation.cs`](../src/Enx.NetCord.Testing/Rest/FormValidation.cs) (`2b84902`, `b75b511`, `edc847f`, merge `8cf44d8`), review APPROVE ; skill `fake-rest-endpoint` § 3 à jour
- [ ] M1-DEBT-04 — Compléter `FormValidation` au besoin des familles M1-REST-02+ : entiers et bornes numériques, booléens, tableaux typés (à faire dans la première tâche qui en a besoin, pas en lot) — rest-implementer — M1-DEBT-03 — partiel via M1-REST-02 : `ReadNullableString`, `ReadOptionalInteger`, `ReadOptionalBoolean` ; reste : tableaux typés
- [x] M1-REST-02 — Channels : `GET/PATCH/DELETE /channels/{channel_id}` — rest-implementer — M1-03, M1-05 — [`Routes/Channels/`](../src/Enx.NetCord.Testing/Rest/Routes/Channels/), [`FakeDiscordState.Channels.cs`](../src/Enx.NetCord.Testing/State/FakeDiscordState.Channels.cs) (`a57af74`, `ff54c75`, `62799b7`, `f2659be`), review APPROVE ; 3 lignes 🟡/✅ (salons texte et DM seulement), `10003` ✅ ; refus de type/parent/nom sous le verrou ; skill `fake-rest-endpoint` à jour
- [ ] M1-REST-02B — Catégories et autres types de salons : `parent_id`, suppression d'une catégorie et effet sur ses enfants, `position` et réordonnancement, voix/annonces/forum (`topic` 4096) — architect (état), puis rest-implementer — M1-REST-02
- [ ] M1-REST-02C — Group DM : `PATCH /channels/{channel_id}` (`ModifyGroupDMChannelAsync`, `UpdateDMRequestPartial` complet) — rest-implementer — M1-REST-01, M1-REST-02
- [ ] M1-REST-02D — Seed : `rate_limit_per_user` (slowmode) sur `CreateTextChannelInput` / `TextChannel(...)` (faible priorité) — rest-implementer — M1-REST-02
- [ ] M1-REST-03 — Messages : list/get/create/edit/delete/bulk-delete/crosspost (JSON + multipart) — rest-implementer — M1-REST-02
- [ ] M1-REST-04 — Réactions : put/delete @me, delete user, list, delete all, delete emoji — rest-implementer — M1-REST-03
- [ ] M1-REST-05 — Pins (nouvelle et ancienne API), typing — rest-implementer — M1-REST-03
- [ ] M1-REST-06 — Guilds : get/modify/preview, channels list/create/positions — rest-implementer — M1-03, M1-05
- [ ] M1-REST-07 — Members : get/list/search/modify/@me/add role/remove role/kick ; ajouter `UnknownMember` (`10007`) au catalogue `DiscordError.FromStateFailure` + `errors.md` — rest-implementer — M1-REST-06
- [ ] M1-REST-08 — Roles : list/get/create/modify/positions/delete (`@everyone` = guild id) ; ajouter `UnknownRole` (`10011`) au catalogue `DiscordError.FromStateFailure` + `errors.md` — rest-implementer — M1-REST-06
- [ ] M1-REST-09 — Bans : list/get/create/delete/bulk-ban — rest-implementer — M1-REST-07
- [ ] M1-REST-10 — `GET /users/@me/guilds`, `DELETE /users/@me/guilds/{guild_id}` (le départ produit le fait que M3-03 traduira en `GUILD_DELETE`) — rest-implementer — M1-05, M1-REST-06
- [x] M1-T-02 — Test d'architecture : aucun `StateCaller.Fixture` ni `Find(Guild|Channel|Member|Message)` appelé depuis `src/**/Rest/**` (suite review M1-05) — test-engineer — M1-05 — [`Architecture/RestIsolationTests.cs`](../tests/Enx.NetCord.Testing.Tests/Architecture/RestIsolationTests.cs) (`d9546f8`, `b80c420`, `a117b04`), review APPROVE ; analyse du texte source, membres de `FakeDiscordState` sans `StateCaller` interdits sauf liste blanche `AllowedUnguardedMembers`
- [ ] M1-T-01 — Conformité M1 via `RestClient` NetCord + validation de contrat JSON (openapi) — test-engineer — M1-REST-03
- [ ] M1-R — Review de jalon + `/parity/audit rest` — reviewer, parity-auditor — M1-*

## M2 — Interactions & NetCord.Services

- [x] M2-ADR-07 — Modèle d'interaction (builders, `InteractionResult`, 3 voies d'exécution, règles 40060/15 min) — architect — M1-ADR-04 — [ADR 0007](adr/0007-interaction-model.md) (`884eb26`, `8c9eb78`, `5fbc9a0`), review APPROVE
- [x] M2-ADR-08 — Harnais `Enx.NetCord.Testing.Services` (API, DI, enregistrement des commandes) — architect — M2-ADR-07 — [ADR 0008](adr/0008-services-harness.md) (`c557acb`, `b7ea229`, `b3d983a`), review APPROVE
- [ ] M2-01 — Builders slash command (options, sous-commandes, `resolved`) + exécution directe (refus local d'une guilde sans le bot, amendement 04B § 4) — interactions-implementer — M2-ADR-07, M1-05
- [ ] M2-02 — Callbacks : Pong, Message, DeferredMessage, DeferredModifyMessage, ModifyMessage, Autocomplete, Modal + route `POST /interactions/{interaction_id}/{interaction_token}/callback` (ADR 0007) — interactions-implementer — M2-01, M1-03, M1-04
- [ ] M2-03 — REST interactions à jeton : original response get/edit/delete, followups (`/webhooks/{application_id}/{token}…`), `WaitForAsync`, `GetResult` ; `InteractionResult.Requests` rend les instances du journal (ADR 0012) — interactions-implementer — M2-02, M1-REST-03
- [ ] M2-04 — User/Message commands, Autocomplete — interactions-implementer — M2-01
- [ ] M2-05 — Composants : bouton, string/user/role/mentionable/channel menus ; modals (text input, Label, FileUpload, Radio/Checkbox) — interactions-implementer — M2-02
- [ ] M2-06 — Components V2 (Section, TextDisplay, Thumbnail, MediaGallery, File, Separator, Container) dans les messages — interactions-implementer — M2-05
- [ ] M2-07 — Application commands REST (global/guild CRUD + bulk overwrite + permissions) ; `PUT` rend les commandes dans l'ordre reçu, tous types (4 compris) ; `PUT` de guilde selon M1-ADR-04B (`50001`) — rest-implementer — M1-03, M1-05
- [ ] M2-08 — Harnais Services : `discord.Services()`, builder, `ApplicationCommandServiceManager`/`RegisterCommandsAsync`, `ExecuteAsync` → `ServiceExecutionResult`, scopes (ADR 0008) — interactions-implementer — M2-ADR-08, M2-07, M2-02, M2-04, M2-12
- [ ] M2-09 — Harnais Services : `ComponentInteractionService` (composants, modals) ; commandes texte → M3-09 — interactions-implementer — M2-05, M2-08
- [ ] M2-10 — `Interaction.Guild` en voie directe (projection `GUILD_CREATE` minimale partagée avec M3-02) — interactions-implementer — M2-01, M1-REST-06
- [ ] M2-11 — Validation des requêtes d'interaction contre les commandes visibles (guilde du salon + globales) — interactions-implementer — M2-04, M2-07, M1-05
- [ ] M2-12 — `FakeDiscord.CreateGatewayClient()` minimal : REST branché, compression désactivée, provider WebSocket dans `Gateway/` qui refuse la connexion (remplacé par M3-01) — gateway-implementer — M1-02
- [ ] M2-13 — Interactions hors installation de guilde : installation par l'utilisateur (`authorizing_integration_owners` `{"1": user_id}`, `context`, `app_permissions`, limite de 5 followups) et application installée sans utilisateur bot ; amendement ADR 0007 puis implémentation — architect, puis interactions-implementer — M2-07, M2-11
- [ ] M2-14 — Assertions sur `InteractionResult` (« a répondu », modal, réponse refusée) : amendement ADR 0012 puis implémentation — architect, puis interactions-implementer — M2-03, M1-04B
- [ ] M2-T-01 — Sample bot : `/ping`, `/greet user`, bouton de confirmation, modal + tests (utilise les assertions de l'ADR 0012) — test-engineer — M2-08, M1-04B
- [ ] M2-R — Review de jalon + `/parity/audit interactions` — reviewer, parity-auditor — M2-*

## M3 — Gateway

- [ ] M3-ADR-09 — Gateway fake : connexion mémoire, barrières (`FlushAsync`), commandes de test, sharding — architect — M1-ADR-03
- [ ] M3-01 — `FakeWebSocketConnection(Provider)` + HELLO/IDENTIFY/READY/HEARTBEAT(ACK) — gateway-implementer — M3-ADR-09
- [ ] M3-02 — GUILD_CREATE initial complet (cache NetCord rempli) — gateway-implementer — M3-01
- [ ] M3-03 — Traduction `StateChange` → dispatch : messages, réactions, channels, threads, guild, members, roles, bans, `UserUpdated` → `USER_UPDATE`, `ChannelUpdated`/`ChannelDeleted` → `CHANNEL_UPDATE`/`CHANNEL_DELETE` (rien pour la fermeture d'un DM, qui ne publie pas de fait) ; ignorer `ChannelCreated` d'un DM (Discord n'envoie `CHANNEL_CREATE` que pour un salon de guilde) — gateway-implementer — M3-02
- [ ] M3-04 — Intents (dont privilégiés, MESSAGE_CONTENT) — gateway-implementer — M3-03
- [ ] M3-05 — RESUME/RESUMED, RECONNECT, INVALID_SESSION, close codes 4000–4014 — gateway-implementer — M3-01
- [ ] M3-06 — Sharding (`ShardedGatewayClient`), routage par shard — gateway-implementer — M3-03
- [ ] M3-07 — Send events : PRESENCE_UPDATE, VOICE_STATE_UPDATE (état seulement), REQUEST_GUILD_MEMBERS → GUILD_MEMBERS_CHUNK, REQUEST_SOUNDBOARD_SOUNDS — gateway-implementer — M3-03
- [ ] M3-08 — INTERACTION_CREATE via Gateway (voie 2) — interactions-implementer — M3-02, M2-03
- [ ] M3-09 — Harnais Services : `CommandService` par `MESSAGE_CREATE` sur un client connecté (préfixe, résultat par ID de message) — interactions-implementer — M3-ADR-09, M3-03, M3-04, M2-08
- [ ] M3-10 — Assertions Gateway (journal des frames et dispatchs) : amendement ADR 0012 puis implémentation — architect, puis gateway-implementer — M3-ADR-09, M3-03, M1-04B
- [ ] M3-R — Review + `/parity/audit gateway` — reviewer, parity-auditor — M3-*

## M4 — Couverture REST étendue

- [ ] M4-REST-01 — Threads (create from message/without, members, archived lists, `/guilds/{id}/threads/active`) — rest-implementer — M1-REST-03
- [ ] M4-REST-02 — Webhooks (CRUD, exécution `?wait`, messages de webhook, avec token) — rest-implementer — M1-REST-03
- [ ] M4-REST-03 — Invites (channel/guild/get/delete) — rest-implementer — M1-REST-06
- [ ] M4-REST-04 — Emojis (guild + application), stickers — rest-implementer — M1-REST-06
- [ ] M4-REST-05 — Permissions overwrites + calcul des permissions (`app_permissions`, 50013), dont `MANAGE_CHANNELS` sur `PATCH`/`DELETE /channels/{channel_id}` et `permission_overwrites` réels dans les réponses de salon — rest-implementer — M1-REST-08
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

- [ ] M6-01 — Guides `docs/guides/01..08` + README (dont composition des assertions ADR 0012 avec xUnit/NUnit/MSTest/Shouldly/FluentAssertions) — docs-writer — M2, M3
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
- [ ] PAR-INT-02 — `interactions.md` selon ADR 0008 : `services:register-commands` (`RegisterCommandsAsync`, deux `PUT`) ; `services:minimal-api` (builders NetCord.Services vs `host.AddSlashCommand` M5) ; `services:text-commands` dépend de M3 ; ajouter `services:execution-result`, `services:scoped-services`, `services:custom-context` — parity-auditor — M2-ADR-08
- [ ] PAR-REST-01 — Confirmer contre Discord (hypothèses M1-REST-01) : statut 403 de `50025` pour un group DM ; `10013` pour un `recipient_id` inconnu (fauxcord) ; `50007` pour un DM vers soi-même ou un bot ; refus éventuel de WebP pour avatar/banner ; `verified`/`email`/`locale` de `GET /users/@me` pour un bot — parity-auditor — M1-REST-01
- [ ] PAR-ERR-02 — Confirmer contre Discord les lignes « hypothèse »/« observé » du tableau § 2 de l'amendement 04B : sous-ressources et salons (`50001` vs `10004`), `PATCH`/`preview` de guilde, commandes d'une guilde inexistante, départ d'une guilde non membre, ordre `50035` avant `50001`/`10004`, validité du jeton d'interaction/webhook après départ du bot — parity-auditor — M1-REST-06
- [ ] PAR-GW-01 — Confirmer : pas de `GUILD_MEMBER_ADD` pour le bot qui rejoint ; pas de champ `unavailable` dans le `GUILD_DELETE` d'un départ (amendement 04B § 3) — parity-auditor — M3-02
- [ ] PAR-ERR-03 — Confirmer contre Discord (hypothèses M1-REST-02) : messages `NUMBER_TYPE_MIN`/`MAX`/`COERCE` et `BOOLEAN_TYPE_COERCE` ; codes `CHANNEL_TYPE_INVALID`/`CHANNEL_PARENT_INVALID` ; `PATCH` d'un DM 1:1 (renvoi inchangé ou erreur) ; ordre `50035` avant `10003`/`50001` ; regroupement de l'arbre `errors` entre validation et refus d'état — parity-auditor — M1-REST-02
- [ ] PAR-SEAM-01 — Réaligner skill `fake-interaction` (l. 44 `CreateCommandsAsync` → `RegisterCommandsAsync`), `AGENTS.md:94` (`ExecuteSlashCommandAsync` → `bot.ExecuteAsync(request)`) ; corriger `AddModule&lt;T&gt;` échappé dans `docs/architecture.md` § Services — orchestrator — M2-ADR-08
