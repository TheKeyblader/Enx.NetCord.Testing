---
description: Audit complet (ou d'un domaine : rest, gateway, interactions, errors) de la parité Discord ↔ NetCord ↔ fake ; met à jour les matrices et crée les tâches d'écart
agent: parity-auditor
subagent: true
---

Domaine à auditer : $ARGUMENTS
(vide = tous : `rest`, `gateway`, `interactions`, `errors`).

Suis la procédure « Audit » de la skill `discord-parity` :

1. Relis les sources épinglées dans `docs/parity/sources.md` et la tête actuelle des
   références `discord-api-spec`, `discord-api-docs`, `netcord` (`git -C <ref> log -1`).
2. Recense les opérations/events/types côté Discord, côté NetCord, côté fake (`src/**`) et
   côté tests (`tests/**`, attribut `[Parity("...")]`).
3. Mets à jour les matrices ligne par ligne et recalcule les compteurs d'en-tête.
4. Liste dans ton rapport (« Tâches à créer ») une tâche `PAR-<domaine>-NN` par écart
   actionnable, sans doublon avec `docs/roadmap.md` ; l'orchestrateur les reporte dans son
   « Backlog parité ».
5. Rapport : compteurs avant/après, lignes changées, dérive détectée, tâches à créer.

Ne modifie aucun code.
