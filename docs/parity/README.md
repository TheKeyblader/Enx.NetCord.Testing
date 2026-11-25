# Suivi de parité Discord

Objectif : savoir à tout moment **quelle part de Discord le faux Discord reproduit**, pour les
fonctionnalités que **NetCord** expose, et détecter la **dérive** quand Discord ou NetCord évoluent.
Méthode inspirée de fauxcord (`spec/manifest.ts`, `spec-drift.yml`, `compat/coverage-matrix.md`),
adaptée à une lib en processus et pilotée par des agents.

## Trois sources, trois colonnes

```
discord-api-spec / discord-api-docs  ──►  ligne de matrice (ce que Discord fait)
NetCord (référence `netcord`)        ──►  colonne NetCord   (ce que le bot peut appeler/recevoir)
src/ + tests/ de ce dépôt            ──►  colonnes Fake / Tests (ce que l'on simule et prouve)
```

## Matrices

| Fichier | Périmètre | Clé |
| --- | --- | --- |
| [`rest.md`](rest.md) | opérations REST v10 | `METHOD /path/{param}` |
| [`gateway.md`](gateway.md) | opcodes, close codes, send/receive events, sémantique | `op N`, `4004`, `MESSAGE_CREATE`, `intents-filter`… |
| [`interactions.md`](interactions.md) | types, champs, options, callbacks, règles, composants, harnais | `interaction:2/chat_input`, `callback:4`, `component:2`… |
| [`errors.md`](errors.md) | statuts HTTP et codes JSON produits | `http:404`, `10003`… |
| [`sources.md`](sources.md) | SHA épinglés + journal | — |

## Statuts

| Symbole | Sens | Exigence |
| --- | --- | --- |
| ⬜ | à faire | — |
| 🟡 | partiel | `Notes` = ce qui manque |
| ✅ | fait | implémenté **et** test `[Parity("<clé>")]` passant via l'API NetCord |
| ➖ | hors périmètre | justification vérifiable (non exposé par NetCord @sha, non-objectif de `vision.md`) |
| ⛔ | bloqué | raison + lien (bug NetCord/spec) |

Progression d'une matrice = ✅ / (total − ➖).

## Cycle de vie

1. **Implémentation** : l'implementer met à jour sa/ses ligne(s) dans la même tâche
   (vérifié par `reviewer`).
2. **Audit** (`/parity/audit [domaine]`) : l'auditeur revérifie toutes les lignes contre le code
   et les tests, recalcule les compteurs, crée les tâches `PAR-*`.
3. **Sync** (`/parity/sync`) : avance les SHA de `sources.md`, détecte ajouts/retraits/changements
   de schéma, rétrograde ✅ → 🟡 si besoin, crée les tâches.
4. **Statut** (`/parity/status`) : tableau de bord en lecture seule.
5. **CI** (`parity-drift.yml`, M0-06) : alerte hebdomadaire dès qu'une tête de branche diffère
   du SHA épinglé. Le workflow ne filtre pas les changements par fichier : c'est `/parity/sync`
   qui détermine lesquels affectent les matrices.

## Tests et traçabilité

Chaque test qui prouve une ligne porte `[Parity("<clé>")]` (plusieurs autorisés). L'auditeur
fait `rg '\[Parity\("' tests/` pour relier tests et lignes ; une ligne ✅ sans test trouvé
est rétrogradée.

## Règles d'or

- La référence de comportement est **Discord** (spec + docs), pas ce que NetCord tolère.
- On ne supprime pas de ligne ; on barre celles retirées en amont.
- ➖ et ⛔ ne sont jamais des raccourcis : justification obligatoire.
