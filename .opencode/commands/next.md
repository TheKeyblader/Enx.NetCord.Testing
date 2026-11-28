---
description: Prend la (ou les) prochaine(s) tâche(s) de la roadmap et les fait exécuter de bout en bout par les sous-agents
agent: orchestrator
---

Lis `docs/roadmap.md`. Sélectionne la prochaine tâche non commencée (`[ ]`) dont toutes les
dépendances sont `[x]`, dans l'ordre des jalons. Si plusieurs tâches indépendantes touchent
des fichiers disjoints, prends-en jusqu'à 3 et parallélise-les (un worktree OpenCode par tâche, cf. section
« Worktrees » de l'orchestrateur : `worktree_create`, déplacement de session du sous-agent,
fusion puis `worktree_remove`).

Contrainte ou précision de l'utilisateur (peut être vide) : $ARGUMENTS

Pour chaque tâche : délègue au bon sous-agent avec un prompt autonome, puis fais passer
`reviewer`, renvoie en correction tant que le verdict n'est pas `APPROVE`, puis coche la
tâche. Termine par `/parity/status` (résumé chiffré) et la liste des 3 prochaines tâches.
