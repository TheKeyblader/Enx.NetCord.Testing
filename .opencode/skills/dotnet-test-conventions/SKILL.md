---
name: Dotnet test conventions
description: Conventions de tests du dépôt Enx.NetCord.Testing (xUnit v3, nommage, attribut Parity, déterminisme TimeProvider, isolation, tests via les vrais clients NetCord, commandes dotnet test).
---

# Conventions de tests

- Framework des tests **du dépôt** : xUnit v3 (`xunit.v3`), `Microsoft.NET.Test.Sdk`,
  `Microsoft.Extensions.TimeProvider.Testing` (`FakeTimeProvider`). La lib `src/` ne dépend
  d'aucun framework de test (ses assertions lèvent `FakeDiscordAssertionException`).
- Requêtes REST du bot : préférer `discord.Recorded.ShouldHaveRequested…` / `ShouldNotHaveRequested`
  (ADR 0012) au LINQ sur `Recorded.Requests` ; elles valident le motif de route et listent le
  journal en cas d'échec.
- Nommage : classe `<Sujet>Tests`, méthode `Method_Scenario_Expected`.
- Un dossier par domaine (`Rest/Channels`, `Rest/Guilds`, `Gateway`, `Interactions`, `Services`, `Hosting`).
- Attribut `[Parity("<clé de ligne>")]` (défini dans le projet de tests) sur chaque test qui
  couvre une ligne de `docs/parity/*.md` — l'auditeur s'en sert.
- **Toujours passer par l'API NetCord réelle** (`RestClient`, `GatewayClient`,
  `ApplicationCommandService`…) configurée par le fake ; tester un handler interne
  directement n'est autorisé qu'en complément.
- Déterminisme : `FakeTimeProvider` pour heartbeats, expirations de token, rate limits ;
  aucune attente réelle (`Task.Delay`, `Thread.Sleep`, `SpinWait` interdits) ; barrières
  explicites du fake (`FlushAsync`, `WaitForReadyAsync`) avec timeout de garde (5 s) qui échoue
  le test au lieu de bloquer.
- Isolation : une instance `FakeDiscord` par test (`await using`), aucun état statique,
  tests parallélisables.
- Données : préférer les builders `discord.Seed.*` ; IDs déterministes via le générateur de
  Snowflakes du fake.
- **Conformité (contrat OpenAPI)** — règle obligatoire pour tout handler REST :
  - Tout handler enregistré dans `RestRoutes.Bindings` exige un scénario dans
    `tests/Enx.NetCord.Testing.Conformance/Rest/` (ou `Interactions/` pour les routes d'interactions et de
    webhooks) : test `[Parity("METHOD /path")]` (clé exacte) qui appelle l'opération **par le vrai `RestClient`**,
    vérifie la forme de l'objet NetCord et **se termine par `ContractAssert.Conforms(discord, "METHOD /path")`**
    (valide toutes les tentatives du journal contre le schéma de la spec épinglée et exige que la route ait été
    atteinte). Ajouter le cas d'erreur principal (`Unknown *`, `[Parity("<code>")]`).
  - La garde `ConformanceTests_EveryImplementedOperation_HasAScenarioTaggedWithItsKey` **échoue** sans ce scénario ;
    un test `Skip`/`SkipUnless`/`SkipWhen` ne compte pas, et un scénario de conformité ne doit **pas** appeler
    `Assert.Skip()` à l'exécution (non détectable par la garde : le test passerait pour une couverture sans en être une). L'extrait couvre toutes les opérations de la spec : aucun fichier de `Contract/` à toucher.
  - Un écart de schéma est un bug du fake (corriger `src/`, ou `Skip` avec raison + ID de tâche et ligne de parité en 🟡).
    Une route hors spec (`InSpecification: false`) n'a pas de contrat.
  - Régénération après `/parity/sync` (tâche `parity-auditor`/`test-engineer`) :
    `python3 -I tests/Enx.NetCord.Testing.Conformance/Contract/extract-openapi-contract.py <chemin>/discord-api-spec/specs/openapi.json <sha-complet>`,
    relire le diff (= dérive de contrat), puis `dotnet test` ; voir `tests/Enx.NetCord.Testing.Conformance/README.md`.
- Commandes :
  - `dotnet test` (tout), `dotnet test --filter "FullyQualifiedName~Gateway"`,
  - `dotnet test --filter "Category=Conformance"`,
  - `dotnet test --collect:"XPlat Code Coverage"`.
