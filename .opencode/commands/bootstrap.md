---
description: Jalon M0 — crée le squelette de solution .NET (slnx, Directory.*.props, projets src/tests/samples, CI) conformément à AGENTS.md et aux ADR
agent: build
model: claude-agent/sonnet#medium
---

Réalise les tâches `M0-*` de `docs/roadmap.md` qui ne sont pas encore cochées.

Contraintes (lis d'abord `AGENTS.md`, `docs/architecture.md`, `docs/adr/*.md`) :

- `git init` si le dépôt n'existe pas, `.gitignore` .NET, `.editorconfig` (style C#),
  `global.json` (SDK .NET 10, `rollForward: latestFeature`).
- `Enx.NetCord.Testing.slnx`, `Directory.Build.props` (net10.0, Nullable, ImplicitUsings,
  LangVersion latest, TreatWarningsAsErrors + GenerateDocumentationFile + IsAotCompatible pour
  `src/`, métadonnées NuGet, MinVer), `Directory.Packages.props` (versions centralisées :
  NetCord, NetCord.Services, NetCord.Hosting, NetCord.Hosting.Services,
  NetCord.Hosting.AspNetCore — version de `docs/parity/sources.md` (1.0.0-beta.30 au 2026-11-25,
  vérifier s'il y a plus récent sur NuGet et mettre `sources.md` à jour) ; xUnit v3 ; Microsoft.NET.Test.Sdk ;
  Microsoft.Extensions.TimeProvider.Testing).
- Projets vides mais compilables listés dans l'« Arborescence cible » d'`AGENTS.md`, avec
  `InternalsVisibleTo` vers les tests, et un test placeholder par projet de test.
- `.github/workflows/ci.yml` : restore, build Release, test, format check, pack (sans push).
- `.github/workflows/parity-drift.yml` : hebdomadaire + `workflow_dispatch`, qui ouvre une
  issue « parity drift » si les SHA de `docs/parity/sources.md` sont en retard sur
  `discord/discord-api-spec`, `discord/discord-api-docs` et `NetCordDev/NetCord` (pas de push de code).
- Vérifie : `dotnet build -c Release` et `dotnet test` passent.

Coche les tâches M0 dans `docs/roadmap.md`. Ne fais ni `git push`
ni publication.
