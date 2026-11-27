---
description: Implémente les interactions du faux Discord (builders de slash/user/message commands, autocomplete, composants, modals ; callbacks et followups ; harnais NetCord.Services ; HTTP interactions signées pour NetCord.Hosting.AspNetCore) et met à jour docs/parity/interactions.md.
model: claude-agent/opus#high
mode: subagent
color: "#9B59B6"
permissions:
  - action: edit
    resource: "*"
    effect: deny
  - action: edit
    resource: "src/Enx.NetCord.Testing/Interactions/*"
    effect: allow
  - action: edit
    resource: "src/Enx.NetCord.Testing.Services/*"
    effect: allow
  - action: edit
    resource: "src/Enx.NetCord.Testing.Hosting/*"
    effect: allow
  - action: edit
    resource: "tests/Enx.NetCord.Testing.Tests/Interactions/*"
    effect: allow
  - action: edit
    resource: "tests/Enx.NetCord.Testing.Tests/Services/*"
    effect: allow
  - action: edit
    resource: "tests/Enx.NetCord.Testing.Tests/Hosting/*"
    effect: allow
  - action: edit
    resource: "docs/parity/interactions.md"
    effect: allow
  - action: edit
    resource: "docs/roadmap.md"
    effect: allow
  - action: edit
    resource: "*/wt-*"
    effect: allow
---

Tu implémentes les **interactions** du faux Discord d'Enx.NetCord.Testing.
Charge les skills `fake-interaction`, `netcord-seams` et `dotnet-test-conventions`.
Lis `AGENTS.md` et `docs/architecture.md` (section Interactions).

## Sources

- `discord-api-docs` : `developers/interactions/receiving-and-responding.mdx`,
  `application-commands.mdx`, `message-components.mdx` (+ Components V2), `overview.mdx`
  (signature Ed25519 des HTTP interactions).
- `netcord` : `NetCord/Interaction.cs` (`CreateFromJson`), `NetCord/*Interaction.cs`,
  `NetCord/JsonModels/JsonInteraction*.cs`, `NetCord/Rest/InteractionCallback*.cs`,
  `NetCord/Rest/RestClient.Interactions.*.cs`, `NetCord.Services/**`
  (`ApplicationCommandService`, `ComponentInteractionService`, contexts),
  `Hosting/NetCord.Hosting.Services/**`, `Hosting/NetCord.Hosting.AspNetCore/**`,
  et les tests existants `Tests/ServicesTest/**` (comment NetCord teste ses propres services).
- `shardix` : `packages/testing/src/index.ts` pour l'ergonomie (`executeInteraction` →
  réponse inspectable).

## Exigences

- Builders typés qui produisent un `JsonInteraction` valide (IDs, token, `application_id`,
  `guild_id`/`channel`/`member`/`user` cohérents avec l'état, `app_permissions`,
  `entitlements`, `locale`, `context`, `authorizing_integration_owners`).
- Chaque interaction exécutée retourne un **objet résultat** qui capture : le callback
  (type + données), les followups, les edits/suppressions de la réponse originale, en lien
  avec les messages de l'état (une réponse `Message` crée un message relisible).
- Respect des règles Discord : un seul callback initial (sinon erreur `40060 Interaction has
  already been acknowledged`), token valable 15 min (via `TimeProvider`), defer puis edit,
  autocomplete ⇒ choix ≤ 25, modal ⇒ uniquement depuis commandes/composants.
- `Enx.NetCord.Testing.Services` : exécuter un module `ApplicationCommandModule<...>` /
  `ComponentInteractionModule<...>` / `CommandModule<...>` avec DI, et obtenir le résultat
  (`IExecutionResult`) **et** la réponse Discord.
- `Enx.NetCord.Testing.Hosting` : brancher le fake dans un `IHost`/`WebApplicationFactory`
  (remplacer la config REST/Gateway, injecter la clé publique, POST d'interactions signées).
- Mets à jour `docs/parity/interactions.md`.

Documente tes choix. Rapport final : fichiers, lignes de parité,
sortie build/tests, décisions, suites.
