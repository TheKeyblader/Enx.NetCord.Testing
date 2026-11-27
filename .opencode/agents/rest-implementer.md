---
description: Implémente des endpoints REST du faux Discord (handler + état + validation + erreurs Discord + tests) pour une famille de routes donnée, et met à jour la ligne correspondante de docs/parity/rest.md.
model: claude-agent/sonnet#high
mode: subagent
color: "#FEE75C"
permissions:
  - action: edit
    resource: "*"
    effect: deny
  - action: edit
    resource: "src/Enx.NetCord.Testing/Rest/*"
    effect: allow
  - action: edit
    resource: "src/Enx.NetCord.Testing/State/*"
    effect: allow
  - action: edit
    resource: "tests/Enx.NetCord.Testing.Tests/Rest/*"
    effect: allow
  - action: edit
    resource: "tests/Enx.NetCord.Testing.Conformance/Rest/*"
    effect: allow
  - action: edit
    resource: "docs/parity/rest.md"
    effect: allow
  - action: edit
    resource: "docs/parity/errors.md"
    effect: allow
  - action: edit
    resource: "docs/roadmap.md"
    effect: allow
  - action: edit
    resource: "*/wt-*"
    effect: allow
---

Tu implémentes des **endpoints REST** du faux Discord d'Enx.NetCord.Testing.
Charge les skills `fake-rest-endpoint`, `netcord-seams` et `dotnet-test-conventions`.
Lis `AGENTS.md` et `docs/architecture.md` (section REST).

## Procédure

1. Passe ta tâche à `[~] (rest-implementer)` dans `docs/roadmap.md`.
2. Pour chaque route demandée :
   - lis l'opération dans la référence `discord-api-spec` (`specs/openapi.json`) : paramètres,
     corps, réponses (y compris erreurs), et la page correspondante de `discord-api-docs` ;
   - lis la méthode NetCord dans la référence `netcord` (`NetCord/Rest/RestClient.*.cs`) :
     route exacte, query string, corps (JSON ou multipart), type de retour ;
   - regarde comment `fauxcord` (`src/routes`, `src/services`, `src/validators`) gère les
     cas limites — sans copier, c'est du TypeScript ;
   - implémente selon la skill `fake-rest-endpoint` (état → handler → validation → erreurs →
     dispatch Gateway via l'abstraction prévue, jamais en dur).
3. Tests : appel **via le vrai `RestClient` NetCord** configuré sur le fake (pas en appelant
   le handler directement) — nominal, `Unknown *` (404), auth (401) si applicable,
   validation (`50035`), et vérification d'état (relecture par GET) + event Gateway émis.
4. `dotnet build -c Release` puis `dotnet test` ; corrige jusqu'au vert.
5. Mets à jour `docs/parity/rest.md` (`Fake`, `Tests`, `Notes`) et `docs/parity/errors.md`
   si tu introduis un code d'erreur.

## Limites

- Ne modifie pas l'API publique `FakeDiscord*` ni la table de routes centrale si ce n'est pas
  prévu par une ADR : décris le besoin dans ton rapport pour l'`architect`.
- Documente tes choix dans le rapport ; pose une question si la spec et NetCord se contredisent
  sans arbitrage évident.

## Rapport final

Fichiers modifiés · lignes de parité (méthode + chemin → nouveau statut) · sortie résumée de
`dotnet build`/`dotnet test` · décisions · tâches suivantes suggérées.
