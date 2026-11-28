# Enx.NetCord.Testing

Bibliothèque .NET qui permet d'**écrire des tests unitaires et d'intégration pour des bots
[NetCord](https://github.com/NetCordDev/NetCord)** sans contacter Discord : un « faux Discord »
**en mémoire, dans le processus de test**, branché sur les points d'extension officiels de NetCord
(REST, Gateway, interactions), avec un état cohérent (ce qu'on crée par POST se relit par GET et
déclenche les events Gateway correspondants) et des assertions orientées Discord.

Inspirations (références opencode `fauxcord` et `shardix`) :

- **fauxcord** : mock Discord *stateful* (REST v10 + Gateway), API de contrôle de test, codes
  d'erreur Discord, suivi de dérive de spec et matrice de couverture.
- **shardix `@shardix/testing`** : ergonomie côté développeur (`createTestingApplication`,
  `mockInteraction`, `executeInteraction` → réponse inspectable).

À lire avant toute tâche : `docs/vision.md`, `docs/architecture.md`, `docs/parity/README.md`,
`docs/roadmap.md`, et les ADR de `docs/adr/`.

## Langue

- Échanges avec l'utilisateur, docs `docs/**`, roadmap, ADR : **français**.
- Code, identifiants, commentaires, XML docs, messages d'exception/log, noms de tests,
  messages de commit (Conventional Commits) : **anglais**.

## Questions

Les agents peuvent poser des questions à l'utilisateur (outil `question`).

- Poser une question quand le choix est structurant ou dépend de ses préférences (périmètre,
  API publique, compromis coût/qualité), ou avant une action irréversible ou externe
  (publication NuGet, `git push`, suppression de données hors du dépôt).
- Pour les détails : choisir l'option la plus cohérente avec `docs/vision.md`, les ADR et le
  comportement réel de Discord, **la documenter** (ADR courte ou section « Décisions » du
  rapport) et continuer.

## Stack

- .NET **10** (`net10.0`), C# `latest`, `Nullable` + `ImplicitUsings` activés,
  `TreatWarningsAsErrors` sur les projets `src/`.
- Dépendances runtime autorisées : `NetCord`, `NetCord.Services`, `NetCord.Hosting*`,
  `Microsoft.Extensions.*`. Rien d'autre sans ADR.
- Compatible **Native AOT / trimming** comme NetCord (`IsAotCompatible=true`) : pas de
  réflexion non annotée, sérialisation `System.Text.Json` source-generated obligatoire. Pour
  ses réponses JSON, le fake sérialise des DTO internes conformes à la spec Discord avec
  `FakeDiscordJsonContext` dans `Serialization/` ; réserver les `NetCord.JsonModels` aux API
  NetCord qui les exigent ([ADR 0006](docs/adr/0006-wire-serialization.md)).
- Tests de la lib : **xUnit v3** (à confirmer dans l'ADR 0002) ; la lib publiée doit rester
  **agnostique du framework de test** (pas de dépendance xUnit/NUnit/MSTest dans `src/`).
- Gestion centralisée des versions : `Directory.Packages.props`, `Directory.Build.props`,
  solution `.slnx`.

## Arborescence cible

```
src/
  Enx.NetCord.Testing/                 # cœur : FakeDiscord (état), REST fake, Gateway fake, builders, assertions
  Enx.NetCord.Testing.Services/        # harnais pour NetCord.Services (slash/user/message commands, components, modals, text commands)
  Enx.NetCord.Testing.Hosting/         # intégration Generic Host / ASP.NET Core (HTTP interactions signées)
tests/
  Enx.NetCord.Testing.Tests/           # tests de la lib (un dossier par domaine : Rest/, Gateway/, Interactions/…)
  Enx.NetCord.Testing.Conformance/     # tests de conformité : NetCord réel ↔ fake, pilotés par docs/parity
samples/
  SampleBot/ + SampleBot.Tests/        # exemple de bot NetCord et de ses tests (documentation vivante)
docs/
  vision.md  architecture.md  roadmap.md  adr/  parity/  guides/
.opencode/  agents/  commands/  skills/
```

## Commandes

```bash
dotnet build -c Release                      # doit passer sans warning
dotnet test                                  # toute la solution
dotnet test --filter "FullyQualifiedName~Rest"   # un domaine
dotnet format --verify-no-changes            # style
dotnet pack -c Release                       # vérifie le packaging (ne jamais push)
```

## Principes d'architecture (résumé, détail dans `docs/architecture.md`)

1. **Brancher, ne pas forker** : on n'intercepte NetCord **que** via ses seams publics —
   `RestClientConfiguration.RequestHandler` (`IRestRequestHandler`),
   `GatewayClientConfiguration.WebSocketConnectionProvider` (`IWebSocketConnectionProvider`),
   `Hostname`, `Interaction.CreateFromJson(...)` + `InteractionResponseDelegate`,
   `NetCord.JsonModels.*` (publics). Pas de réflexion sur l'interne NetCord.
2. **Un seul état** (`FakeDiscordState`) partagé par REST, Gateway et interactions ; les
   mutations REST produisent les dispatchs Gateway que Discord enverrait.
3. **Fidélité Discord** : formes JSON, codes HTTP, `code` d'erreur JSON (`10003 Unknown Channel`,
   `50035 Invalid Form Body`…), en-têtes de rate limit, Snowflakes. La référence est
   `discord-api-spec` + `discord-api-docs`, pas ce que NetCord « accepte ».
4. **Déterminisme** : horloge (`TimeProvider`) et générateur de Snowflakes injectables,
   aucun `Task.Delay` réel, aucun réseau.
5. **API de test lisible** : façon shardix — `await using var discord = FakeDiscord.Create(...)`,
   builders fluents (`discord.Guild("g").TextChannel("general")`), `ExecuteSlashCommandAsync(...)`
   retournant une réponse inspectable, `discord.Recorded.Requests`, assertions dédiées.

## Conventions de code

- `public` minimal ; tout le reste `internal` (+ `InternalsVisibleTo` vers les tests).
- XML doc sur **toute** API publique (`GenerateDocumentationFile`, CS1591 en erreur dans `src/`).
- `async` partout, suffixe `Async`, `CancellationToken` en dernier paramètre, `ConfigureAwait(false)`
  dans `src/`.
- Pas de `Thread.Sleep`/`Task.Delay` réels ; utiliser `TimeProvider`.
- Un endpoint REST fake = un handler dans `src/Enx.NetCord.Testing/Rest/Routes/<Domaine>/`,
  enregistré dans la table de routes ; voir la skill `fake-rest-endpoint`.
- Un event Gateway = une méthode de dispatch dans `Gateway/Dispatch/` ; voir la skill
  `fake-gateway-event`.
- Erreurs Discord : passer par l'helper central (`DiscordError.UnknownChannel()`, etc.),
  jamais de JSON d'erreur écrit à la main.
- Tests : nom `Method_Scenario_Expected`, cas nominal **+** 404/`Unknown *`, 401/403
  (auth/permissions si modélisées) **+** validation (`50035`).

## Parité Discord (obligatoire)

La parité est suivie dans `docs/parity/` (méthodo : `docs/parity/README.md`).

- **Toute PR qui ajoute/modifie un comportement fake met à jour la ligne correspondante**
  des matrices (`rest.md`, `gateway.md`, `interactions.md`, `errors.md`) : statut `Fake`,
  `Tests`, et une note si l'implémentation est partielle.
- Statuts : ⬜ à faire · 🟡 partiel · ✅ fait (implémenté **et** testé) · ➖ hors périmètre
  (NetCord n'expose pas la route, ou justification écrite) · ⛔ bloqué (raison obligatoire).
- `docs/parity/sources.md` épingle les commits de NetCord / discord-api-spec / discord-api-docs
  de la dernière synchro. `/parity/sync` les met à jour et liste la dérive.
- Ne jamais marquer ✅ sans test ; ne jamais marquer ➖ « par flemme » (même politique que
  `spec/skip.ts` de fauxcord : justification vérifiable obligatoire).

## Travail multi-agents

Agents (`.opencode/agents/`) :

| Agent | Rôle | Écrit dans |
| --- | --- | --- |
| `orchestrator` (primaire, défaut) | planifie, découpe, délègue, intègre, tient la roadmap | `docs/roadmap.md` |
| `architect` | conçoit l'API publique et les seams, rédige les ADR | `docs/adr/**`, `docs/architecture.md` |
| `parity-auditor` | compare NetCord ↔ spec/docs Discord ↔ fake, met à jour les matrices | `docs/parity/**` |
| `rest-implementer` | endpoints REST fake + état, seed, journal, assertions | `src/**/{Rest,State,Seed,Recording,Serialization,Assertions}/**`, `FakeDiscord*.cs`, `tools/**`, tests associés, skill `fake-rest-endpoint` |
| `gateway-implementer` | faux WebSocket, handshake, dispatchs | `src/**/Gateway/**`, `State/**`, `FakeDiscord*.cs`, tests associés, skill `fake-gateway-event` |
| `interactions-implementer` | builders d'interactions, réponses, harnais Services/Hosting | `src/**/Interactions/**`, `Rest/Routes/{Interactions,Webhooks}/**`, `State/**`, `src/Enx.NetCord.Testing.Services/**`, `src/Enx.NetCord.Testing.Hosting/**`, skill `fake-interaction` |
| `test-engineer` | tests de conformité, samples, couverture | `tests/**`, `samples/**` |
| `reviewer` | relecture read-only, vérifie parité + conventions ; verdict `APPROVE` / `CHANGES_REQUESTED` / `BLOCKED` | (rien) |
| `parity-status` | tableau de bord de parité (`/parity/status`), lecture seule | (rien) |
| `docs-writer` | README, guides, XML docs manquantes | `README.md`, `docs/guides/**` |

Modèles affectés à chaque agent : voir `docs/models.md` (comparatif et sources dans ce fichier).
Ne jamais choisir un modèle du provider `opencode` (Zen, payé au token) : abonnements uniquement
(`claude-agent`, `opencode-go`). Le provider `openai` n'est plus disponible (2026-11-28).

Règles de coordination :

1. **La roadmap est le tableau de bord** : chaque tâche a un ID (`M1-REST-03`…).
   **Seul l'orchestrateur l'écrit**, sur `main` : `[~]` avec le nom de l'agent quand il
   délègue, `[x]` + lien commit/fichiers après fusion. Les sous-agents ne la modifient pas et
   listent les suites dans leur rapport (« Tâches à créer » : ID proposé, agent, dépendances).
2. **Pas deux implementers sur les mêmes fichiers** : l'orchestrateur découpe par domaine
   (une ligne de matrice ou un dossier `Routes/<Domaine>`) ; un changement transverse
   (`State/`, table de routes, `FakeDiscord` public) passe d'abord par l'`architect`.
3. **Un worktree OpenCode par tâche** qui écrit dans le dépôt, créé par l'orchestrateur
   (`tools.opencode.worktree_create`) ; l'orchestrateur reste sur `main`, fusionne et supprime
   le worktree. Voir « Worktrees » ci-dessous.
4. Chaque sous-agent termine par un **rapport** : fichiers touchés, lignes de parité modifiées,
   commandes lancées + résultat (`dotnet build`/`dotnet test`/`dotnet format`), décisions prises,
   skill mise à jour, tâches à créer.
5. « Fini » = build sans warning + tests verts + matrices à jour + XML docs + skill à jour si une
   convention nouvelle est apparue + `reviewer` `APPROVE` (un `BLOCKED` n'en est pas un).
6. **Permissions** : chaque agent n'édite que son périmètre (`permissions` de son fichier). On
   écrit avec `edit`/`write` ; un refus signale un fichier hors périmètre : ne jamais le
   contourner par le shell (`cat >`, `python3`, `sed -i`), mais s'arrêter et le signaler ou
   poser une question. Le shell est ouvert (`opencode.jsonc`, sauf publication et opérations
   destructives) : cette ouverture ne lève **pas** la règle précédente, une commande par appel
   reste préférée.

### Worktrees (sous-agents)

Quand ton prompt indique un worktree (`Worktree : <directory>`, branche `feat/<ID>` que
l'orchestrateur a créée : `worktree_create` part d'une HEAD détachée) :

1. **Avant toute autre action**, déplace ta session dans un appel `execute` **seul** :
   `await tools.opencode.session.move({ directory: "<directory>" })` (sans `sessionID` : la
   session courante). Le déplacement s'applique à la frontière suivante : ne lance rien qui
   dépende du dossier dans le même appel.
2. Vérifie ensuite avec `pwd` et `git branch --show-current` que tu es dans le worktree et sur
   la branche attendue. Sinon, arrête-toi et signale-le dans ton rapport, sans rien écrire.
3. Travaille, builde, teste et **commite** dans le worktree (Conventional Commits, ID de tâche).
   N'écris jamais dans le checkout principal, ne fusionne pas, ne supprime pas le worktree :
   c'est le rôle de l'orchestrateur.
4. Le rapport final indique le worktree, la branche et les commits créés.

Sans worktree indiqué alors que la tâche écrit dans le dépôt, demande-le à l'orchestrateur
dans ton rapport plutôt que d'écrire dans le checkout principal.

Commandes (`.opencode/commands/`) : `/next`, `/implement <ID|endpoint|event>`,
`/parity/audit [domaine]`, `/parity/sync`, `/parity/status`, `/adr <sujet>`, `/review`,
`/bootstrap`.

Skills (`.opencode/skills/`) : `netcord-seams`, `discord-parity`, `fake-rest-endpoint`,
`fake-gateway-event`, `fake-interaction`, `dotnet-test-conventions`.

## Gotchas connus (à compléter au fil de l'eau)

- NetCord appelle les routes **sans** préfixe `/api/v10` dans `SendRequestAsync` mais le
  `HttpRequestMessage` final contient `https://{Hostname}/api/v{N}/...` : router sur le chemin
  **après** `/api/v{N}`, en décodant `%40me` → `@me`.
- Snowflakes 64 bits : toujours `ulong`, sérialisés en **chaîne** JSON.
- Le rôle `@everyone` a l'ID de la guilde (comportement Discord, cf. fauxcord).
- Les réponses d'interaction passent par `POST /interactions/{id}/{token}/callback` (REST) ;
  les followups par `/webhooks/{application_id}/{token}`. Le fake doit relier les deux à
  l'interaction d'origine.
- Gateway : l'auth se fait dans `IDENTIFY`, pas via en-tête. NetCord active zstd/zlib par
  défaut : le fake force `Compression = new UncompressedGatewayCompression()` dans la
  configuration qu'il fournit, mais doit aussi tolérer le paramètre `compress=` dans l'URL.
- `RestClient` construit `https://{Hostname}/api/v{N}{route}{query}` (`RestClient.cs`) :
  `Hostname` sert seulement à reconnaître les requêtes ; aucune requête ne sort, tout passe
  par l'`IRestRequestHandler` fake.
- HTTP interactions (NetCord.Hosting.AspNetCore) vérifient une signature Ed25519
  (`X-Signature-Ed25519` + `X-Signature-Timestamp`, via `HttpEventValidator` → libsodium
  natif) : le fake signe avec sa propre paire de clés et injecte la clé publique dans
  `IDiscordOptions.PublicKey`. Le moyen de signer (libsodium P/Invoke vs implémentation
  managée) est à trancher par ADR (cf. `docs/roadmap.md` M5).
