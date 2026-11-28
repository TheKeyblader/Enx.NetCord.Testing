---
description: Relecture read-only des changements courants (ou des fichiers/commit donnés) contre AGENTS.md, les ADR et la parité Discord
agent: reviewer
subagent: true
---

Relis les changements suivants : $ARGUMENTS
(si vide : tout le diff non commité, `git status` + `git diff`, et les fichiers non suivis).

Applique ta checklist complète et termine par `VERDICT: APPROVE`, `VERDICT: CHANGES_REQUESTED`
ou `VERDICT: BLOCKED` (vérifications non exécutées), avec les commandes lancées et les findings
classés.
