---
description: Implémente une tâche (ID roadmap), un endpoint REST ("GET /channels/{channel_id}"), un event Gateway ("MESSAGE_CREATE") ou un type d'interaction, avec review et mise à jour de parité
agent: orchestrator
---

Cible : $ARGUMENTS

1. Identifie la cible : ID dans `docs/roadmap.md`, ou ligne(s) dans `docs/parity/rest.md`,
   `gateway.md`, `interactions.md`. Si elle n'existe pas encore, demande à `parity-auditor`
   d'ajouter la ligne et la tâche.
2. Si la cible implique une API publique nouvelle ou un changement de `State/`/table de
   routes, fais d'abord produire une ADR par `architect`.
3. Délègue à l'implementer du domaine (`rest-implementer`, `gateway-implementer`,
   `interactions-implementer`) avec : la ligne de parité exacte, les sources à lire
   (références `discord-api-spec`, `discord-api-docs`, `netcord`, `fauxcord`), les dossiers
   autorisés et la définition de « Fini » d'`AGENTS.md`.
4. Fais relire par `reviewer` ; boucle jusqu'à `APPROVE`.
5. Mets à jour `docs/roadmap.md` et résume (fichiers, statut de parité, build/tests).
