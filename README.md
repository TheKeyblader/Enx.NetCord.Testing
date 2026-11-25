# Enx.NetCord.Testing

> Kit de tests pour bots [NetCord](https://github.com/NetCordDev/NetCord) : un faux Discord en
> mémoire, stateful et déterministe, branché sur les seams publics de NetCord.

**Statut : fondations.** La solution .NET 10 compile et les projets de test sont en place.
Le faux Discord n'est pas encore implémenté.

- Vision : [`docs/vision.md`](docs/vision.md)
- Architecture : [`docs/architecture.md`](docs/architecture.md) · ADR : [`docs/adr/`](docs/adr/)
- Roadmap / tableau de bord : [`docs/roadmap.md`](docs/roadmap.md)
- Parité Discord : [`docs/parity/`](docs/parity/README.md)

## Parité (baseline 2026-11-25)

| Matrice | À faire | Hors périmètre | Fait |
| --- | --- | --- | --- |
| REST | 197 | 61 | 0 |
| Gateway | 113 | 12 | 0 |
| Interactions | 81 | 2 | 0 |
| Erreurs | 41 | 0 | 0 |

## Travailler avec opencode

L'agent par défaut est `orchestrator` : il planifie et délègue aux sous-agents
(`architect`, `rest-implementer`, `gateway-implementer`, `interactions-implementer`,
`test-engineer`, `parity-auditor`, `reviewer`, `docs-writer`).

| Commande | Effet |
| --- | --- |
| `/bootstrap` | crée le squelette .NET (jalon M0) |
| `/next [contrainte]` | exécute la ou les prochaines tâches de la roadmap |
| `/implement <ID \| "GET /channels/{channel_id}" \| MESSAGE_CREATE>` | implémente une cible précise |
| `/adr <sujet>` | fait rédiger une ADR |
| `/review [fichiers]` | relecture read-only |
| `/parity/status` | tableau de bord de parité |
| `/parity/audit [rest\|gateway\|interactions\|errors]` | réaudit et met à jour les matrices |
| `/parity/sync` | synchronise sur les dernières versions Discord/NetCord, détecte la dérive |

Dépôts de référence (clonés par opencode, lecture seule) : `netcord`, `discord-api-spec`,
`discord-api-docs`, `fauxcord`, `shardix` — voir `opencode.jsonc`.
