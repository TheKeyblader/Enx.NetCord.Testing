# Sources de parité épinglées

Mis à jour par `parity-auditor` via `/parity/sync`. Les matrices reflètent **ces** versions.

| Référence (alias opencode) | Dépôt | Branche | SHA | Date du commit | Fichiers suivis |
| --- | --- | --- | --- | --- | --- |
| `discord-api-spec` | discord/discord-api-spec | main | `654230017d4f5ee3e6a59a769ffc40cf61001e3f` | 2026-11-24 | `specs/openapi.json` |
| `discord-api-docs` | discord/discord-api-docs | main | `62ab2d5019782add4231a3fc6ee6c0fa2d8d62d1` | 2026-11-25 | `developers/events/**`, `developers/interactions/**`, `developers/components/**`, `developers/topics/opcodes-and-status-codes.mdx`, `developers/topics/rate-limits.mdx` |
| `netcord` | NetCordDev/NetCord | main | `bb5f4e137abea55b23b5b55015bf5b8f8f707d86` | 2026-11-07 | `NetCord/Rest/RestClient.*.cs`, `NetCord/Gateway/**`, `NetCord/JsonModels/**`, `NetCord/*Interaction.cs`, `NetCord/Components/**`, `NetCord.Services/**`, `Hosting/**` |

Version NuGet ciblée : **1.0.0-beta.30** pour `NetCord`, `NetCord.Services`, `NetCord.Hosting`,
`NetCord.Hosting.Services` et `NetCord.Hosting.AspNetCore` (dernière version des cinq paquets,
vérifiée sur NuGet le 2026-11-26).
La branche `main` de NetCord peut être en avance sur NuGet : en cas d'écart de seam entre
`main` et le package référencé, **le package fait foi** pour le code, `main` sert d'alerte
précoce (tâche `PAR-SEAM-*`).

## Inventaire REST embarqué

`src/Enx.NetCord.Testing/Rest/RouteInventory.g.cs` est dérivé hors ligne de `specs/openapi.json` au SHA `discord-api-spec` ci-dessus
(méthode, template, `operationId`, alternatives d'auth). Lors de `/parity/sync`, après avoir avancé ce SHA, le régénérer puis relire le diff :

```bash
python3 tools/generate-route-inventory.py <chemin>/discord-api-spec/specs/openapi.json <SHA-complet-du-commit>
```

Le même fichier embarque aussi `RouteInventory.SnowflakeParameters` : les noms de paramètres de chemin que la spec type `SnowflakeType`
(26 noms @ SHA épinglé), utilisés par les assertions `ShouldHaveRequested…` ([ADR 0012](../adr/0012-agnostic-assertions.md)) pour valider les
valeurs d'un motif de route. Cette liste est régénérée par le même script, donc à chaque `/parity/sync` ; le script échoue si un même nom
est Snowflake sur un chemin et autre chose sur un autre, car il ne pourrait plus le classer.

Le test `SourcesPinTests` échoue si `RouteInventory.SpecificationCommit` ne correspond plus au SHA épinglé ici. Les routes NetCord hors spec
(`Rest/NetCordOnlyRoutes.cs`) se contrôlent à part, contre `NetCord/Rest/RestClient.*.cs` et la section « Routes NetCord hors spec publique » de `rest.md`.

Inspirations (non suivies pour la parité) : `fauxcord` (tomacheese/fauxcord, master),
`shardix` (euandrelucas/shardix, main).

## Journal

| Date | Auteur | Changement |
| --- | --- | --- |
| 2026-11-25 | initialisation | Baselines générées : REST 250 ops (189 exposées par NetCord + 8 routes hors spec), Gateway 13 opcodes / 14 close codes / 8 send events / 82 receive events (71 gérés par NetCord), interactions & composants, erreurs. |
| 2026-11-26 | orchestrator | Version NuGet 1.0.0-beta.30 confirmée pour les cinq paquets ; SHA et matrices inchangés. |
| 2026-11-28 | test-engineer (M1-T-01) | Extrait de contrat OpenAPI commité (`tests/Enx.NetCord.Testing.Conformance/Contract/openapi-contract.json`, 250 opérations @ SHA `discord-api-spec` ci-dessus) : à **régénérer à chaque `/parity/sync`** avec `extract-openapi-contract.py` (procédure : `tests/Enx.NetCord.Testing.Conformance/README.md`), diff = dérive de contrat. Erratum **E-001** appliqué par le script (code des erreurs de champ `50035` : chaîne, pas entier, cf. `errors.md`) : vérifier à chaque sync s'il est encore nécessaire. |
