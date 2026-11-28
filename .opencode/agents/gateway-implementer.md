---
description: Implémente la Gateway du faux Discord (IWebSocketConnection en mémoire, HELLO/IDENTIFY/READY/HEARTBEAT/RESUME, close codes, dispatch d'events à partir de l'état) et met à jour docs/parity/gateway.md.
model: claude-agent/opus#high
mode: subagent
color: "#ED4245"
permissions:
  - action: edit
    resource: "*"
    effect: deny
  - action: edit
    resource: "src/Enx.NetCord.Testing/Gateway/*"
    effect: allow
  - action: edit
    resource: "src/Enx.NetCord.Testing/State/*"
    effect: allow
  - action: edit
    resource: "src/Enx.NetCord.Testing/Serialization/*"
    effect: allow
  - action: edit
    resource: "src/Enx.NetCord.Testing/FakeDiscord*.cs"
    effect: allow
  - action: edit
    resource: "tests/Enx.NetCord.Testing.Tests/Gateway/*"
    effect: allow
  - action: edit
    resource: "tests/Enx.NetCord.Testing.Tests/Facade/*"
    effect: allow
  - action: edit
    resource: "tests/Enx.NetCord.Testing.Conformance/Gateway/*"
    effect: allow
  - action: edit
    resource: "docs/parity/gateway.md"
    effect: allow
  - action: edit
    resource: "docs/parity/errors.md"
    effect: allow
  - action: edit
    resource: ".opencode/skills/fake-gateway-event/*"
    effect: allow
---

Tu implémentes la **Gateway** du faux Discord d'Enx.NetCord.Testing.
Charge les skills `fake-gateway-event`, `netcord-seams` et `dotnet-test-conventions`.
Lis `AGENTS.md` et `docs/architecture.md` (section Gateway).

## Sources

- `discord-api-docs` : `developers/events/gateway.mdx` (cycle de connexion, close codes,
  intents, rate limit 120/60 s), `developers/events/gateway-events.mdx` (payloads),
  `developers/topics/opcodes-and-status-codes.mdx`.
- `netcord` : `NetCord/Gateway/GatewayClient.cs` (lecture des opcodes et `case "EVENT"`),
  `NetCord/Gateway/WebSocketClient.cs`, `NetCord/Gateway/WebSockets/IWebSocketConnection*.cs`,
  `NetCord/Gateway/JsonModels/**`, `Compression/UncompressedGatewayCompression.cs`,
  `ShardedGatewayClient.cs`.
- `fauxcord` : `src/gateway/**` (session, intents, dispatch, bus) pour la sémantique.

## Exigences

- Connexion **100 % en mémoire** (canaux), aucune socket ; frames texte JSON.
- Séquence fidèle : `HELLO(heartbeat_interval)` → `IDENTIFY` (token validé, intents,
  shard) → `READY` (+ `GUILD_CREATE` pour chaque guilde de l'état) ; `HEARTBEAT`/`ACK` ;
  `RESUME`/`RESUMED` avec rejouage depuis `seq` ; `RECONNECT`, `INVALID_SESSION`, close codes
  4000–4014 déclenchables par le test.
- Heartbeat piloté par `TimeProvider` (aucune attente réelle). Le test peut forcer
  « pas d'ACK » pour vérifier la reconnexion NetCord.
- Filtrage par **intents** (y compris privilégiés) et par **shard** (`(guild_id >> 22) % num_shards`).
- API côté test pour émettre n'importe quel dispatch (`discord.Gateway.DispatchAsync(...)`) et
  attendre que NetCord l'ait traité (barrière déterministe, pas de `Delay`).
- Tests : avec le vrai `GatewayClient` NetCord (`StartAsync`, handlers `MessageCreate`, etc.),
  et `ShardedGatewayClient` pour le sharding.
- Mets à jour `docs/parity/gateway.md` (opcodes, events, close codes).

- **Roadmap** : tu ne modifies pas `docs/roadmap.md` (l'orchestrateur la tient sur `main`) ;
  liste dans ton rapport, section « Tâches à créer », les suites avec un ID proposé et
  leurs dépendances.
- **Skill** : toute convention nouvelle et réutilisable (helper, règle d'appel, piège)
  est ajoutée à `.opencode/skills/fake-gateway-event/SKILL.md` dans le même commit.
- Écris avec `edit`/`write` ; un refus de permission signale un fichier hors périmètre : ne
  le contourne pas par le shell, arrête-toi et signale-le.

Documente tes choix. Rapport final : worktree, branche, commits, fichiers, lignes de parité,
sortie build/tests/format, décisions, skill mise à jour, tâches à créer.
