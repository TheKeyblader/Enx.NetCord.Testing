---
description: Synchronise la parité sur les dernières versions de discord-api-spec, discord-api-docs et NetCord ; détecte la dérive depuis les SHA épinglés et met à jour docs/parity/sources.md
agent: parity-auditor
subagent: true
---

Synchronisation de parité. Précision éventuelle : $ARGUMENTS

Suis la procédure « Sync / dérive » de la skill `discord-parity` :

1. Lis les SHA épinglés dans `docs/parity/sources.md`.
2. Pour chaque référence (`discord-api-spec`, `discord-api-docs`, `netcord`), compare l'ancien
   SHA à la tête (`git -C <ref> log --oneline <old>..HEAD -- <chemins pertinents>` ; si
   l'historique est superficiel, `git -C <ref> fetch --deepen=200` ou compare le contenu actuel
   aux matrices).
   - spec : `specs/openapi.json` (chemins ajoutés/retirés, schémas modifiés sur les lignes ✅/🟡) ;
   - docs : `developers/events/**`, `developers/interactions/**`,
     `developers/components/**`, `developers/topics/opcodes-and-status-codes.mdx` ;
   - NetCord : `NetCord/Rest/RestClient.*.cs`, `NetCord/Gateway/GatewayClient.cs`,
     `NetCord/JsonModels/**`, `NetCord/*Interaction.cs`, `NetCord/Components/**`,
     et tout seam utilisé (`IRestRequestHandler`, `IWebSocketConnection*`, `Interaction.CreateFromJson`).
3. Applique l'audit complet sur les zones touchées, rétrograde en 🟡 les lignes ✅ dont le
   schéma a changé.
4. **Changement de seam NetCord** ⇒ tâche `PAR-SEAM-NN` priorité haute + note pour `architect`.
5. Écris les nouveaux SHA/dates dans `docs/parity/sources.md` avec un journal de la synchro.

Ne modifie aucun code. Rapport : dérive par source, lignes
rétrogradées, tâches créées.
