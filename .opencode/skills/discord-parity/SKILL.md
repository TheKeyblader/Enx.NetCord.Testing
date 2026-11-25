---
name: Discord parity
description: Procédures d'audit et de synchronisation de la parité Discord (spec OpenAPI + docs officielles) ↔ NetCord ↔ faux Discord, format et règles des matrices docs/parity/*.md. À charger pour /parity/*, après toute implémentation, ou pour évaluer une dérive.
---

# Parité Discord

## Fichiers

| Fichier | Contenu | Clé de ligne |
| --- | --- | --- |
| `docs/parity/rest.md` | une ligne par opération de `specs/openapi.json` + routes NetCord hors spec | `METHOD /path/{param}` (chemin de la spec) |
| `docs/parity/gateway.md` | opcodes, close codes, events dispatch, intents | nom (`MESSAGE_CREATE`, `op 2 IDENTIFY`, `4004`) |
| `docs/parity/interactions.md` | types d'interaction, types de commande/option, composants, callbacks, règles | `type:valeur` |
| `docs/parity/errors.md` | codes d'erreur JSON et statuts HTTP produits par le fake | code numérique |
| `docs/parity/sources.md` | SHA + date des références lors de la dernière synchro, journal | — |

Colonnes communes : `NetCord` (ce que NetCord expose), `Fake` (statut), `Tests` (⬜/✅),
`Notes`. Statuts `Fake` : ⬜ à faire · 🟡 partiel · ✅ fait (implémenté + testé) · ➖ hors
périmètre · ⛔ bloqué.

## Règles

1. ✅ seulement si un test du dépôt exerce la ligne via l'API NetCord (attribut
   `[Parity("<clé>")]`) **et** vérifie la forme de la réponse.
2. 🟡 si une partie des paramètres/champs/erreurs n'est pas modélisée → la `Note` dit quoi.
3. ➖ si NetCord n'expose pas la fonctionnalité (colonne `NetCord` vide) ou si elle est hors
   vision (`docs/vision.md` §Non-objectifs) ; justification dans `Notes`.
4. ⛔ uniquement avec une raison vérifiable (bug NetCord/spec, lien ou SHA).
5. Ne jamais supprimer une ligne : si retirée en amont, la barrer `~~…~~` + « removed upstream @sha ».
6. Les compteurs en tête de matrice sont recalculés à chaque modification.

## Procédure « Audit »

1. **Discord** :
   - REST : parcourir `paths` de `discord-api-spec/specs/openapi.json` (méthodes get/post/put/patch/delete,
     `operationId`). Grouper par ressource (`/channels/{id}/messages`, `/guilds/{id}/roles`…).
   - Gateway : `discord-api-docs/developers/events/gateway-events.mdx` (tables « Send Events » et
     « Receive Events »), `developers/events/gateway.mdx` (intents, close codes),
     `developers/topics/opcodes-and-status-codes.mdx` (opcodes, close codes, codes JSON).
   - Interactions/composants : `developers/interactions/*.mdx`, `developers/components/*.mdx`.
2. **NetCord** :
   - REST : `rg -n 'HttpMethod\.\w+' NetCord/Rest/RestClient.*.cs` puis le littéral `$"/..."`
     qui suit ; associer la méthode publique englobante (`public async Task<...> XxxAsync(`).
   - Gateway : `rg -o 'case "[A-Z_]+"' NetCord/Gateway/GatewayClient.cs`, `GatewayOpcode.cs`.
   - Interactions : `InteractionType.cs`, `ApplicationCommandType.cs`,
     `ApplicationCommandOptionType.cs`, `Rest/InteractionCallbackType.cs`,
     `Components/ComponentType.cs`, fichiers `*Interaction.cs`.
3. **Fake** : handlers enregistrés dans `src/**/Rest/Routes/**`, dispatchs dans
   `src/**/Gateway/Dispatch/**`, builders dans `src/**/Interactions/**`.
4. **Tests** : `rg -n '\[Parity\("' tests/`.
5. Mettre à jour les matrices ; créer les tâches `PAR-<domaine>-NN` dans `docs/roadmap.md`.

## Procédure « Sync / dérive »

1. Lire les SHA dans `sources.md` ; `git -C <ref> log -1 --format='%H %cs'` pour la tête.
2. Si l'historique local est superficiel : `git -C <ref> fetch --deepen=500` puis
   `git -C <ref> diff <old>..HEAD --stat -- <chemins>`. À défaut, comparer le contenu
   actuel aux lignes des matrices (méthode de l'audit) — c'est la vérité terrain.
3. Classer la dérive :
   - **ajout amont** → nouvelle ligne ⬜ (ou ➖ si NetCord ne l'expose pas) ;
   - **retrait amont** → ligne barrée ;
   - **changement de schéma** sur ✅/🟡 → rétrograder en 🟡, Note « schema drift @sha » ;
   - **NetCord expose une nouvelle route/event** → colonne `NetCord` remplie, ➖ → ⬜ ;
   - **seam NetCord modifié** → `PAR-SEAM-NN` haute priorité.
4. Écrire les nouveaux SHA + date + résumé dans le journal de `sources.md`.

## Rapport type

```
Parité — <date>
REST         : 12/189 ✅ (+3)  4 🟡  173 ⬜  61 ➖  0 ⛔
Gateway      : …
Interactions : …
Dérive       : spec +2 ops (POST /…), NetCord +1 méthode (…)
Tâches       : PAR-REST-07, PAR-GW-02
```
