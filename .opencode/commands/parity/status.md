---
description: Tableau de bord chiffré de la parité Discord (par matrice et par domaine), sans rien modifier
agent: parity-status
subagent: true
---

Produis un tableau de bord de parité **sans modifier aucun fichier**. Filtre éventuel : $ARGUMENTS

Lis `docs/parity/sources.md`, `docs/parity/rest.md`, `docs/parity/gateway.md`,
`docs/parity/interactions.md`, `docs/parity/errors.md`. Pour chaque matrice et chaque section
`##`, compte les statuts de la colonne `Fake` (⬜ 🟡 ✅ ➖ ⛔) et de la colonne `Tests`.

Sortie (français) :

1. Tableau global : matrice · total · ✅ · 🟡 · ⬜ · ⛔ · ➖ · % fait (✅ / (total − ➖)).
2. Top 10 des sections REST les moins couvertes parmi celles exposées par NetCord.
3. Lignes ⛔ et leurs raisons.
4. Âge des SHA épinglés (date dans `sources.md` vs aujourd'hui) ; si > 14 jours, recommander
   `/parity/sync`.
