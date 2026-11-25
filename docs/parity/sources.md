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

Inspirations (non suivies pour la parité) : `fauxcord` (tomacheese/fauxcord, master),
`shardix` (euandrelucas/shardix, main).

## Journal

| Date | Auteur | Changement |
| --- | --- | --- |
| 2026-11-25 | initialisation | Baselines générées : REST 250 ops (189 exposées par NetCord + 8 routes hors spec), Gateway 13 opcodes / 14 close codes / 8 send events / 82 receive events (71 gérés par NetCord), interactions & composants, erreurs. |
| 2026-11-26 | orchestrator | Version NuGet 1.0.0-beta.30 confirmée pour les cinq paquets ; SHA et matrices inchangés. |
