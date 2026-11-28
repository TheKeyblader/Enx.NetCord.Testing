# Parité REST

<!-- Baseline générée le 2026-11-25 depuis discord-api-spec@65423001 et NetCord@bb5f4e13 (voir sources.md). Les agents maintiennent ensuite les colonnes Fake/Tests/Notes à la main (skill discord-parity). -->

Opérations de la spec : **250** · exposées par `RestClient` NetCord : **189** · routes NetCord hors spec : **8**

Progression `Fake` (hors ➖) : ✅ **2** · 🟡 **12** · ⬜ **183** · ⛔ **0** — ➖ **61**

Légende — `NetCord` : méthode(s) de `RestClient` (vide ⇒ non exposé ⇒ ➖ sauf besoin justifié). `Fake` : ⬜ à faire · 🟡 partiel · ✅ fait · ➖ hors périmètre · ⛔ bloqué. `Tests` : ⬜ / ✅ (test `[Parity("METHOD /path")]`). `Notes` : ce qui manque, liens ADR/tâches.

## /applications

| Method | Path | operationId | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/applications/@me` | get_my_application | `GetCurrentApplicationAsync` | ⬜ | ⬜ | |
| PATCH | `/applications/@me` | update_my_application | `ModifyCurrentApplicationAsync` | ⬜ | ⬜ | |
| GET | `/applications/{application_id}` | get_application |  | ➖ | ➖ | Non exposé par NetCord @bb5f4e13 |
| PATCH | `/applications/{application_id}` | update_application |  | ➖ | ➖ | Non exposé par NetCord @bb5f4e13 |
| GET | `/applications/{application_id}/activity-instances/{instance_id}` | applications_get_activity_instance |  | ➖ | ➖ | Non exposé par NetCord @bb5f4e13 |
| POST | `/applications/{application_id}/attachment` | upload_application_attachment |  | ➖ | ➖ | Non exposé par NetCord @bb5f4e13 |
| GET | `/applications/{application_id}/commands` | list_application_commands | `GetGlobalApplicationCommandsAsync` | ⬜ | ⬜ | |
| POST | `/applications/{application_id}/commands` | create_application_command | `CreateGlobalApplicationCommandAsync` | ⬜ | ⬜ | |
| PUT | `/applications/{application_id}/commands` | bulk_set_application_commands | `BulkOverwriteGlobalApplicationCommandsAsync` | ⬜ | ⬜ | |
| DELETE | `/applications/{application_id}/commands/{command_id}` | delete_application_command | `DeleteGlobalApplicationCommandAsync` | ⬜ | ⬜ | |
| GET | `/applications/{application_id}/commands/{command_id}` | get_application_command | `GetGlobalApplicationCommandAsync` | ⬜ | ⬜ | |
| PATCH | `/applications/{application_id}/commands/{command_id}` | update_application_command | `ModifyGlobalApplicationCommandAsync` | ⬜ | ⬜ | |
| GET | `/applications/{application_id}/emojis` | list_application_emojis | `GetApplicationEmojisAsync` | ⬜ | ⬜ | |
| POST | `/applications/{application_id}/emojis` | create_application_emoji | `CreateApplicationEmojiAsync` | ⬜ | ⬜ | |
| DELETE | `/applications/{application_id}/emojis/{emoji_id}` | delete_application_emoji | `DeleteApplicationEmojiAsync` | ⬜ | ⬜ | |
| GET | `/applications/{application_id}/emojis/{emoji_id}` | get_application_emoji | `GetApplicationEmojiAsync` | ⬜ | ⬜ | |
| PATCH | `/applications/{application_id}/emojis/{emoji_id}` | update_application_emoji | `ModifyApplicationEmojiAsync` | ⬜ | ⬜ | |
| GET | `/applications/{application_id}/entitlements` | get_entitlements | `GetEntitlementsAsync` | ⬜ | ⬜ | |
| POST | `/applications/{application_id}/entitlements` | create_entitlement | `CreateTestEntitlementAsync` | ⬜ | ⬜ | |
| DELETE | `/applications/{application_id}/entitlements/{entitlement_id}` | delete_entitlement | `DeleteTestEntitlementAsync` | ⬜ | ⬜ | |
| GET | `/applications/{application_id}/entitlements/{entitlement_id}` | get_entitlement | `GetEntitlementAsync` | ⬜ | ⬜ | |
| POST | `/applications/{application_id}/entitlements/{entitlement_id}/consume` | consume_entitlement | `ConsumeEntitlementAsync` | ⬜ | ⬜ | |
| GET | `/applications/{application_id}/guilds/{guild_id}/commands` | list_guild_application_commands | `GetGuildApplicationCommandsAsync` | ⬜ | ⬜ | |
| POST | `/applications/{application_id}/guilds/{guild_id}/commands` | create_guild_application_command | `CreateGuildApplicationCommandAsync` | ⬜ | ⬜ | |
| PUT | `/applications/{application_id}/guilds/{guild_id}/commands` | bulk_set_guild_application_commands | `BulkOverwriteGuildApplicationCommandsAsync` | ⬜ | ⬜ | |
| GET | `/applications/{application_id}/guilds/{guild_id}/commands/permissions` | list_guild_application_command_permissions | `GetApplicationCommandsGuildPermissionsAsync` | ⬜ | ⬜ | |
| DELETE | `/applications/{application_id}/guilds/{guild_id}/commands/{command_id}` | delete_guild_application_command | `DeleteGuildApplicationCommandAsync` | ⬜ | ⬜ | |
| GET | `/applications/{application_id}/guilds/{guild_id}/commands/{command_id}` | get_guild_application_command | `GetGuildApplicationCommandAsync` | ⬜ | ⬜ | |
| PATCH | `/applications/{application_id}/guilds/{guild_id}/commands/{command_id}` | update_guild_application_command | `ModifyGuildApplicationCommandAsync` | ⬜ | ⬜ | |
| GET | `/applications/{application_id}/guilds/{guild_id}/commands/{command_id}/permissions` | get_guild_application_command_permissions | `GetApplicationCommandGuildPermissionsAsync` | ⬜ | ⬜ | |
| PUT | `/applications/{application_id}/guilds/{guild_id}/commands/{command_id}/permissions` | set_guild_application_command_permissions | `OverwriteApplicationCommandGuildPermissionsAsync` | ⬜ | ⬜ | |
| GET | `/applications/{application_id}/role-connections/metadata` | get_application_role_connections_metadata | `GetApplicationRoleConnectionMetadataRecordsAsync` | ⬜ | ⬜ | |
| PUT | `/applications/{application_id}/role-connections/metadata` | update_application_role_connections_metadata | `UpdateApplicationRoleConnectionMetadataRecordsAsync` | ⬜ | ⬜ | |

## /channels

| Method | Path | operationId | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| DELETE | `/channels/{channel_id}` | delete_channel | `DeleteChannelAsync` | 🟡 | ✅ | Salon de guilde de tout type modélisé (texte, annonces, voix, stage, catégorie, forum, media) : supprimé avec ses messages (ID jamais réutilisé), renvoie le salon tel qu'il était (DTO de sa famille), `ChannelDeleted` publié (le dispatch `CHANNEL_DELETE` vient en M3). **Catégorie** (ADR 0013, § 3) : ses enfants ne sont pas supprimés, ils perdent leur `parent_id` (position et messages conservés) ; dans un même commit, un `ChannelUpdated` par enfant (ID croissant, portant l'enfant après la mutation) puis le `ChannelDeleted` de la catégorie. DM : « fermer » ne détruit rien (doc : on peut le rouvrir) — salon renvoyé tel quel, aucun changement committé, `GET`/`POST /users/@me/channels` retrouvent le même salon. `10003` inconnu, `50001` guilde sans le bot, `50035` `NUMBER_TYPE_COERCE` sur `channel_id`. **À confirmer (UR-008)** : ordre « enfants d'abord » des `CHANNEL_UPDATE` avant le `CHANNEL_DELETE`, enfants qui gardent leur position, aucun `MESSAGE_DELETE` pour les messages du salon supprimé. Manque : permission `MANAGE_CHANNELS` (M4-REST-05), threads (M4-REST-01), salons Rules/Community Updates non supprimables (`50074`, M4-REST-10), `X-Audit-Log-Reason` (M1-REST-02) |
| GET | `/channels/{channel_id}` | get_channel | `GetChannelAsync` | 🟡 | ✅ | Types 0, 2, 4, 5, 13, 15 et 16 (ADR 0013, M1-REST-02B1) : un DTO par famille, choisi par le type (`GuildChannelResponse` de la spec). Texte et annonces : `topic`, `rate_limit_per_user` (toujours `0` pour le type 5), `default_thread_rate_limit_per_user`, `parent_id`. Voix et stage : `bitrate` (64000 par défaut), `user_limit`, `rtc_region` (`null`), `video_quality_mode` (absent tant que non défini), pas de `topic`. Catégorie : ni `topic`, ni `last_message_id`, ni `rate_limit_per_user`, `parent_id: null`, `nsfw: false`. Forum et media : `topic` (jusqu'à 4096), `available_tags: []`, `default_reaction_emoji: null`, `default_sort_order`, `default_forum_layout` (type 15 seulement). `position` par groupe de tri (texte/annonces/forum/media ensemble, voix/stage ensemble, catégories), `parent_id` d'un salon dans une catégorie. `permission_overwrites` toujours `[]`. Et `PrivateChannelResponse` (DM 1:1). `10003` inconnu, `50001` salon d'une guilde dont le bot n'est pas membre (profil « refusée », ADR 0004 amendement 04B), `50035` `NUMBER_TYPE_COERCE`. **À confirmer (UR-009, UR-010)** : groupes de tri et position par défaut (`1 + max` du groupe), bitrate par défaut 64000, `rate_limit_per_user` 0 d'une annonce, `default_thread_rate_limit_per_user` d'une annonce, champs par famille d'après les exemples de `channel.mdx` ; type 16 absent de la spec (retenu d'après les docs et NetCord). Manque : threads (M4-REST-01), group DM (M1-REST-02C), tags et `default_reaction_emoji` réels (M4-REST-01), `last_pin_timestamp`, `permission_overwrites` et `permissions` (M4-REST-05) |
| PATCH | `/channels/{channel_id}` | update_channel | `ModifyGroupDMChannelAsync`, `ModifyGuildChannelAsync` | 🟡 | ✅ | Salon de guilde de tout type modélisé (texte, annonces, voix, stage, catégorie, forum, media ; ADR 0013, M1-REST-02B2), schéma `UpdateGuildChannelRequestPartial` : `name` (1–100 ; > 100 → `BASE_TYPE_BAD_LENGTH` à l'étape structurelle, vide → par l'état), `topic` (`null` l'efface ; structurel ≤ 4096 comme la spec, puis par l'état **1024 hors forum/media**, 4096 en forum/media : `BASE_TYPE_BAD_LENGTH` « Must be between 0 and 1024 in length. »), `nsfw`, `rate_limit_per_user` (0–21600, chaîne numérique coercée), `position` (≥ 0, valeur stockée, voisins jamais renumérotés), `type` (enum de la spec `0,2,4,5,13,14,15`, plus `16` (media, ADR 0013 § 1), sinon `BASE_TYPE_CHOICES`). **`parent_id` à trois états** (clé absente = inchangé, `null` = sortir de la catégorie, ID = déplacer) avec les quatre invariants de l'ADR 0013 § 2, dans l'ordre (parent inexistant ou d'une autre guilde, parent non catégorie, catégorie sans parent, catégorie pleine à 50) → `50035` sur `parent_id` ; envoyer le parent courant, ou `null` à un salon sans parent, ne change rien, ne publie rien et n'est pas contrôlé ; un déplacement ne change que `ParentId` (ni position ni autre champ). **Conversion de type** (§ 5) : 0 ↔ 5 seulement, avant les autres champs du même `PATCH` (validés contre le nouveau type), un seul `ChannelUpdated` ; 0 → 5 remet `rate_limit_per_user` à 0 ; tout autre couple, type 14 compris → `50035` `CHANNEL_TYPE_INVALID`. **Champs par type** (§ 1) : voix/stage `bitrate` (≥ 8000 structurel ; plafond 96000 voix / 64000 stage → `30052`), `user_limit` (≥ 0 structurel ; plafond 99 voix / 10000 stage → `50035` `NUMBER_TYPE_MAX`), `rtc_region` (`null` = automatique), `video_quality_mode` (1 ou 2) ; texte/annonces/forum/media `default_auto_archive_duration` (60, 1440, 4320, 10080) et `default_thread_rate_limit_per_user` (0–21600, annonces comprises) ; forum/media `default_sort_order` (0–1, `null` l'efface), forum seul `default_forum_layout` (0–2) ; `available_tags`, `default_reaction_emoji`, `default_tag_setting` et `flags` validés (types, tailles) puis **ignorés** pour tout type (M4-REST-01, UR-007) ; `permission_overwrites` non validé ni appliqué (M4-REST-05). Un champ sans objet pour le type (un `bitrate` sur un texte, un `topic` sur une voix) est validé puis ignoré sans changement. `null` sur `rate_limit_per_user`/`position`/`nsfw`/`bitrate`/`user_limit`/`video_quality_mode`/`default_*` = inchangé (hypothèse). `ChannelUpdated` publié (dispatch `CHANNEL_UPDATE` en M3) seulement si une valeur change. Contrôles métier et commit sous le même verrou (une seule opération d'état, rien n'est écrit si l'un est refusé). **À confirmer (UR-004, UR-008, UR-010)** : codes de champ et messages des quatre refus de parent (`CHANNEL_PARENT_INVALID` « Category does not exist » / « Categories cannot have subcategories », `CHANNEL_PARENT_INVALID_TYPE` « Not a category », `CHANNEL_PARENT_MAX_CHANNELS` « Maximum number of channels in category reached (50) » ; seul le message « Category does not exist » est rapporté par un tiers) ; `CHANNEL_TYPE_INVALID` pour toute conversion hors 0 ↔ 5 ; `rate_limit_per_user` remis à 0 par la conversion 0 → 5 ; conversion appliquée avant les autres champs ; statut HTTP 400 de `30052` (code et message documentés, statut non) ; forme du refus de `topic` (> 1024 hors forum), de `user_limit` (> plafond) ; `default_thread_rate_limit_per_user` sur une annonce (docs contradictoires) ; `null` = inchangé pour les champs vocaux, `default_*` et `video_quality_mode` (`rtc_region` et `default_sort_order` : `null` efface/automatique) ; pas de contrôle de la fonctionnalité `NEWS` ni Community pour le type 13 (M4-REST-10) ; `parent_id: null` n'est **pas émis par NetCord** (propriétés `WhenWritingNull`) : testé par une requête construite à la main et par l'opération d'état directe ; plafonds de bitrate d'une guilde sans boost uniquement (paliers non modélisés). **Autres hypothèses non vérifiées** : `name` vide refusé sur un salon de guilde (`BASE_TYPE_BAD_LENGTH`, 1–100), `name: null` = inchangé ; ordre de l'ADR 0005 (`50035` structurel avant `10003`/`50001`, y compris pour un salon inconnu ou d'une guilde sans le bot), non vérifié contre Discord ; les refus décidés par l'état (type, parent, nom vide, `topic`, `bitrate`, `user_limit`) ne sont pas réunis avec les erreurs structurelles dans le même arbre `errors` : un seul refus d'état est signalé (premier dans l'ordre type, parent, nom, topic, bitrate, user_limit), alors que Discord réunit tout dans un seul `50035` (point 5 d'UR-004) ; ces refus passent après `Intercept` (ADR 0005) et sont journalisés comme réponse du handler ; un nom > 100 sur un DM reçoit le message de guilde « Must be between 1 and 100 in length. » ; PATCH sur un DM 1:1 : la spec prévoit `UpdateDMRequestPartial` (`name` 0–100, nullable), le fake l'accepte (nom vide ou `null` compris) et renvoie le salon **inchangé** (aucun nom de DM modélisé), comportement réel non vérifié ; le schéma DM n'ayant que `name`, les autres champs d'un DM sont validés comme ceux d'un salon de guilde (le type du salon est inconnu à l'étape structurelle). Group DM (`ModifyGroupDMChannelAsync`) : non modélisé. Un champ que le type n'a pas (`topic` sur une voix ou une catégorie, `nsfw`/`rate_limit_per_user` sur une catégorie, `rate_limit_per_user` sur une annonce, `bitrate` sur un texte) est validé puis ignoré sans changement (à confirmer, UR-010). Manque : permission `MANAGE_CHANNELS`/`MANAGE_ROLES` (M4-REST-05), threads (`archived`, `locked`…), `permission_overwrites`, `flags` et tags/`default_reaction_emoji` réels (M4-REST-01), `X-Audit-Log-Reason` (M1-REST-02), contrôles par `features` de guilde (M4-REST-10), group DM (M1-REST-02C) |

## /channels/{id}/followers

| Method | Path | operationId | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| POST | `/channels/{channel_id}/followers` | follow_channel | `FollowAnnouncementGuildChannelAsync` | ⬜ | ⬜ | |

## /channels/{id}/invites

| Method | Path | operationId | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/channels/{channel_id}/invites` | list_channel_invites | `GetGuildChannelInvitesAsync` | ⬜ | ⬜ | |
| POST | `/channels/{channel_id}/invites` | create_channel_invite | `CreateGuildChannelInviteAsync` | ⬜ | ⬜ | |

## /channels/{id}/messages

| Method | Path | operationId | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/channels/{channel_id}/messages` | list_messages | `GetMessagesAroundAsync`, `GetMessagesAsync` | 🟡 | ✅ | Liste triée par ID décroissant (ordre de Discord), `limit` 1–100 (défaut 50 ; `NUMBER_TYPE_MIN`/`MAX`/`COERCE`), `before`/`after` exclusifs, `around` (`limit / 2` messages plus anciens, puis la cible et les suivants jusqu'à `limit`) ; `10003`, `50001` (salon d'une guilde sans le bot), `50035` (`channel_id`, `limit`, curseurs). DM, voix et stage acceptés ; catégorie, forum et media → `400` `50024` (**hypothèse à confirmer (UR-011)** : Discord refuse de les lister plutôt que de répondre `[]`). **Hypothèses non vérifiées** : partage exact de `around` (moitié avant / cible + reste après) ; plusieurs curseurs à la fois (documentés exclusifs, comportement réel inconnu) : le fake applique `around`, puis `before`, puis `after` sans erreur. Manque : permissions `VIEW_CHANNEL`/`READ_MESSAGE_HISTORY` (M4-REST-05), intent `MESSAGE_CONTENT` (contenu vidé pour une application sans l'intent : M3), `reactions`, `pinned` jamais vrai (M1-REST-04/05), threads (M1-REST-03) |
| POST | `/channels/{channel_id}/messages` | create_message | `SendMessageAsync` | 🟡 | ✅ | JSON **et** `multipart/form-data` (`payload_json` + `files[n]`, `attachments` en métadonnées ; fichiers sans métadonnée nommés d'après la part). `content` ≤ 2000 (`BASE_TYPE_MAX_LENGTH`), `embeds` ≤ 10 avec limites de champs, 25 champs, 6000 caractères au total, couleurs, URL ; `message_reference` (réponse : type `19`, `referenced_message`, ping de l'auteur selon `allowed_mentions.replied_user`, `fail_if_not_exists` ; cible hors du salon = inexistante) ; `allowed_mentions` (`parse`/`users`/`roles`/`replied_user`) appliqué aux mentions `<@id>`, `<@&id>`, `@everyone`/`@here` ; `tts`, `flags` (seuls `SUPPRESS_EMBEDS`, `SUPPRESS_NOTIFICATIONS`, `IS_VOICE_MESSAGE`, `IS_COMPONENTS_V2` sont conservés) ; `nonce` renvoyé, `enforce_nonce` (même auteur et nonce dans les 5 dernières minutes → message existant renvoyé, rien committé) ; pièces jointes : `id` fictif, `size`, `url` déterministe `https://cdn.discordapp.com/attachments/{channel_id}/{id}/{filename}` (le fake ne sert aucun fichier). `50006` message vide, `10003`, `50001`, `50035` (arbre `errors`), `50109`. DM acceptés. Selon le type de salon (ADR 0013, M1-REST-03B) : texte, annonces, voix et stage (chat intégré) acceptent les messages ; catégorie → `400` `50008` (Cannot send messages in a non-text channel), forum et media → `400` `40058` (Cannot send a message in a forum channel), après `10003`/`50001`, avant tout commit (rien committé, aucun fait publié) ; **codes documentés, statut 400 et refus du media (non décrit par la doc) = hypothèses à confirmer (UR-011)**. `MessageCreated` publié (dispatch `MESSAGE_CREATE` en M3). **Hypothèses non vérifiées** (UR) : codes de champ `MESSAGE_REFERENCE_UNKNOWN_MESSAGE`, `MAX_EMBED_SIZE_EXCEEDED`, `MESSAGE_ALLOWED_MENTIONS_PARSE_EXCLUSIVE`, `URL_TYPE_INVALID_URL`, `BASE_TYPE_CHOICES` d'`allowed_mentions.parse` ; `50006` renvoyé avant `10003` (validation structurelle d'abord, ADR 0005) ; un `nonce` numérique est renvoyé sous forme de chaîne. Manque : URL `attachment://fichier` d'un embed (image, thumbnail, icônes) acceptées mais **non réécrites** vers l'URL CDN de la pièce jointe, `components`, `sticker_ids`, `poll`, `shared_client_theme` acceptés puis ignorés (ils comptent seulement comme « non vide »), forward (`message_reference.type` 1 refusé), limite de 25 MiB (`40005`), `Content-Type` `application/x-www-form-urlencoded`, permissions `SEND_MESSAGES`/`SEND_TTS_MESSAGES`/`MENTION_EVERYONE`, rôle non mentionnable (M4-REST-05), `50007`/`50008`, intents, `member`/`guild_id` (absents des réponses REST, présents dans les events : M3), limite 4000 pour Nitro (le fake applique 2000) (M1-REST-03) |
| POST | `/channels/{channel_id}/messages/bulk-delete` | bulk_delete_messages | `DeleteMessagesAsync` | 🟡 | ✅ | `messages` requis, 2 à 100 IDs distincts (`BASE_TYPE_BAD_LENGTH`, `BASE_TYPE_ARRAY_UNIQUE`, `NUMBER_TYPE_COERCE` par indice, `BASE_TYPE_REQUIRED`) ; les IDs inconnus ou d'un autre salon comptent mais sont ignorés (doc) ; un ID de plus de 14 jours (lu dans le snowflake, que le message existe ou non) refuse toute la requête avec `50034` sans rien supprimer ; DM → `50003` ; catégorie, forum et media → `50024` (hypothèse à confirmer (UR-011)) ; `204`, `MessagesBulkDeleted` publié (dispatch `MESSAGE_DELETE_BULK` en M3) avec les seuls IDs supprimés, rien si aucun. NetCord n'appelle la route que pour 2 IDs ou plus (un seul ID passe par `DELETE …/{message_id}`). **Hypothèses non vérifiées** : codes `BASE_TYPE_BAD_LENGTH`/`BASE_TYPE_ARRAY_UNIQUE` (doc : « 400 BAD REQUEST » sans code) ; limite de 100 sans le code `50016` (la doc du code mentionne « fewer than 100 »). Manque : permission `MANAGE_MESSAGES` (M4-REST-05), `X-Audit-Log-Reason` (M1-REST-03) |
| GET | `/channels/{channel_id}/messages/pins` | list_pins | `GetChannelPinsAsync` | ⬜ | ⬜ | |
| DELETE | `/channels/{channel_id}/messages/pins/{message_id}` | delete_pin | `UnpinMessageAsync` | ⬜ | ⬜ | |
| PUT | `/channels/{channel_id}/messages/pins/{message_id}` | create_pin | `PinMessageAsync` | ⬜ | ⬜ | |
| DELETE | `/channels/{channel_id}/messages/{message_id}` | delete_message | `DeleteMessageAsync` | 🟡 | ✅ | `204`, message retiré (l'ID n'est jamais réutilisé), `last_message_id` du salon inchangé (Discord ne le recule pas), `MessageDeleted` publié (dispatch `MESSAGE_DELETE` en M3). `10008` inconnu, ou d'un autre salon ; `10003`, `50001` (contrôlés avant `10008`), `50035` sur `channel_id`/`message_id`. Dans un DM, supprimer le message de l'autre partie → `403` `50003` (**hypothèse non vérifiée, à confirmer**, UR) ; message d'autrui dans une guilde : accepté. Manque : permission `MANAGE_MESSAGES` pour le message d'un autre (le fake l'autorise en guilde : M4-REST-05), `X-Audit-Log-Reason`, messages système non supprimables, réactions/pins/thread associés (M1-REST-03) |
| GET | `/channels/{channel_id}/messages/{message_id}` | get_message | `GetMessageAsync` | 🟡 | ✅ | Forme `MessageResponse` (`author`, `mentions`, `mention_roles`, `attachments`, `embeds`, `components: []`, `edited_timestamp`, `message_reference`/`referenced_message` d'une réponse — `referenced_message` absent pour un message qui n'en est pas une, `null` explicite quand la cible a été supprimée (doc : absent = inconnu, `null` = supprimé) ; pas de `guild_id` ni de `member`, comme la réponse REST de Discord). `10008`, y compris un message d'un autre salon ; `10003`, `50001` contrôlés avant `10008` ; `50035`. DM acceptés. Manque : permissions `VIEW_CHANNEL`/`READ_MESSAGE_HISTORY` (M4-REST-05), intent `MESSAGE_CONTENT` (M3), `reactions`, `thread`, `poll`, `resolved`, `interaction_metadata`, `application_id`/`webhook_id` (messages de webhook et de réponse d'interaction : M2/M4), `position` (M1-REST-03) |
| PATCH | `/channels/{channel_id}/messages/{message_id}` | update_message | `ModifyMessageAsync` | 🟡 | ✅ | `content` (≤ 2000, `null` l'efface), `embeds` (liste complète, `null`/`[]` efface), `flags` (seul `SUPPRESS_EMBEDS` est modifiable, posé ou retiré ; autres bits ignorés sans erreur comme le documente Discord), `allowed_mentions` (mentions reconstruites depuis le nouveau contenu), `attachments` (API v10 : liste de ce qui reste ; un `id` égal à l'index d'un fichier téléversé `files[n]` (indices non contigus acceptés) décrit ce fichier, tout autre `id` désigne une pièce existante conservée ; absent = tout conserver ; les fichiers sont ajoutés) ; `edited_timestamp` pris sur l'horloge, posé à chaque édition même identique (comme Discord), `MessageEdited` publié (dispatch `MESSAGE_UPDATE` en M3). `50005` : édition du contenu, des embeds ou des pièces jointes d'un message d'un autre auteur ; changer seulement `flags` d'un message d'autrui est accepté (Discord l'exige sous `MANAGE_MESSAGES`, non calculé). `50006` si le message n'aurait plus ni contenu, ni embed, ni pièce jointe. `10008`, `10003`, `50001`, `50035`, `50109`. **Hypothèses non vérifiées** : classification des `attachments[].id` ci-dessus (NetCord numérote ses fichiers de 0 et ne sait donc que remplacer les pièces jointes) ; `50006` à l'édition (le fake réutilise le code de l'envoi). Une réponse conserve à l'édition la mention de l'auteur cité (sauf `allowed_mentions.replied_user` faux, ou cible supprimée). Manque : `components`/`sticker_ids` acceptés et ignorés — non stockés, donc un message qui n'en avait que ces champs-là et dont on édite `embeds: []` reçoit à tort `50006` —, `IS_COMPONENTS_V2`, `poll`, permission `MANAGE_MESSAGES` (M4-REST-05) (M1-REST-03) |
| POST | `/channels/{channel_id}/messages/{message_id}/crosspost` | crosspost_message | `CrosspostMessageAsync` | 🟡 | ✅ | Salon d'annonces (type 5, seedé par `AnnouncementChannel`, dans une catégorie ou non) : pose `CROSSPOSTED`, publie `MessageEdited` (dispatch `MESSAGE_UPDATE` en M3) ; testé via le vrai `RestClient` (M1-REST-02B1). Sur un autre salon de guilde (texte…) → `400` `50024` (Cannot execute action on this channel type), sur un DM → `403` `50003`, après `10003`/`50001`/`10008`, sans rien committer ; testé. Second crosspost du même message → `400` `40033` (This message has already been crossposted), contrôlé après le type du salon, sans rien committer (pas de second `MessageEdited`) ; testé. Sur une catégorie, un forum ou un media : aucun message n'y existe, donc `10008` (hypothèse à confirmer (UR-011)) ; le statut 400 de `40033` est une hypothèse (UR-011). Manque : suivi de salon (`channel-follow`), `SEND_MESSAGES`/`MANAGE_MESSAGES` (M4-REST-05), message système non publiable (`50021`) (M1-REST-03) |
| DELETE | `/channels/{channel_id}/messages/{message_id}/reactions` | delete_all_message_reactions | `DeleteAllMessageReactionsAsync` | ⬜ | ⬜ | |
| DELETE | `/channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}` | delete_all_message_reactions_by_emoji | `DeleteAllMessageReactionsForEmojiAsync` | ⬜ | ⬜ | |
| GET | `/channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}` | list_message_reactions_by_emoji | `GetMessageReactionsAsync` | ⬜ | ⬜ | |
| DELETE | `/channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}/@me` | delete_my_message_reaction | `DeleteCurrentUserMessageReactionAsync` | ⬜ | ⬜ | |
| PUT | `/channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}/@me` | add_my_message_reaction | `AddMessageReactionAsync` | ⬜ | ⬜ | |
| DELETE | `/channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}/{user_id}` | delete_user_message_reaction | `DeleteUserMessageReactionAsync` | ⬜ | ⬜ | |
| POST | `/channels/{channel_id}/messages/{message_id}/threads` | create_thread_from_message | `CreateGuildThreadAsync` | ⬜ | ⬜ | |

## /channels/{id}/permissions

| Method | Path | operationId | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/channels/{channel_id}/permissions/member/{user_id}` | get_channel_member_permissions |  | ➖ | ➖ | Non exposé par NetCord @bb5f4e13 |
| GET | `/channels/{channel_id}/permissions/role/{role_id}` | get_channel_role_permissions |  | ➖ | ➖ | Non exposé par NetCord @bb5f4e13 |
| DELETE | `/channels/{channel_id}/permissions/{overwrite_id}` | delete_channel_permission_overwrite | `DeleteGuildChannelPermissionAsync` | ⬜ | ⬜ | |
| PUT | `/channels/{channel_id}/permissions/{overwrite_id}` | set_channel_permission_overwrite | `ModifyGuildChannelPermissionsAsync` | ⬜ | ⬜ | |

## /channels/{id}/pins

| Method | Path | operationId | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/channels/{channel_id}/pins` | deprecated_list_pins |  | ➖ | ➖ | Non exposé par NetCord @bb5f4e13 |
| DELETE | `/channels/{channel_id}/pins/{message_id}` | deprecated_delete_pin |  | ➖ | ➖ | Non exposé par NetCord @bb5f4e13 |
| PUT | `/channels/{channel_id}/pins/{message_id}` | deprecated_create_pin |  | ➖ | ➖ | Non exposé par NetCord @bb5f4e13 |

## /channels/{id}/polls

| Method | Path | operationId | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/channels/{channel_id}/polls/{message_id}/answers/{answer_id}` | get_answer_voters | `GetMessagePollAnswerVotersAsync` | ⬜ | ⬜ | |
| POST | `/channels/{channel_id}/polls/{message_id}/expire` | poll_expire | `EndMessagePollAsync` | ⬜ | ⬜ | |

## /channels/{id}/recipients

| Method | Path | operationId | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| DELETE | `/channels/{channel_id}/recipients/{user_id}` | delete_group_dm_user | `GroupDMChannelDeleteUserAsync` | ⬜ | ⬜ | |
| PUT | `/channels/{channel_id}/recipients/{user_id}` | add_group_dm_user | `GroupDMChannelAddUserAsync` | ⬜ | ⬜ | |

## /channels/{id}/send-soundboard-sound

| Method | Path | operationId | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| POST | `/channels/{channel_id}/send-soundboard-sound` | send_soundboard_sound |  | ➖ | ➖ | Non exposé par NetCord @bb5f4e13 ; soundboard (rouvrir si NetCord l’ajoute) |

## /channels/{id}/thread-members

| Method | Path | operationId | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/channels/{channel_id}/thread-members` | list_thread_members | `GetGuildThreadUsersAsync` | ⬜ | ⬜ | |
| DELETE | `/channels/{channel_id}/thread-members/@me` | leave_thread | `LeaveGuildThreadAsync` | ⬜ | ⬜ | |
| PUT | `/channels/{channel_id}/thread-members/@me` | join_thread | `JoinGuildThreadAsync` | ⬜ | ⬜ | |
| DELETE | `/channels/{channel_id}/thread-members/{user_id}` | delete_thread_member | `DeleteGuildThreadUserAsync` | ⬜ | ⬜ | |
| GET | `/channels/{channel_id}/thread-members/{user_id}` | get_thread_member | `GetGuildThreadUserAsync` | ⬜ | ⬜ | |
| PUT | `/channels/{channel_id}/thread-members/{user_id}` | add_thread_member | `AddGuildThreadUserAsync` | ⬜ | ⬜ | |

## /channels/{id}/threads

| Method | Path | operationId | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| POST | `/channels/{channel_id}/threads` | create_thread | `CreateForumGuildThreadAsync`, `CreateGuildThreadAsync` | ⬜ | ⬜ | |
| GET | `/channels/{channel_id}/threads/archived/private` | list_private_archived_threads | `GetJoinedPrivateArchivedGuildThreadsAsync`, `GetPrivateArchivedGuildThreadsAsync` | ⬜ | ⬜ | |
| GET | `/channels/{channel_id}/threads/archived/public` | list_public_archived_threads | `GetPublicArchivedGuildThreadsAsync` | ⬜ | ⬜ | |
| GET | `/channels/{channel_id}/threads/search` | thread_search |  | ➖ | ➖ | Non exposé par NetCord @bb5f4e13 |

## /channels/{id}/typing

| Method | Path | operationId | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| POST | `/channels/{channel_id}/typing` | trigger_typing_indicator | `TriggerTypingAsync` | ⬜ | ⬜ | |

## /channels/{id}/users

| Method | Path | operationId | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/channels/{channel_id}/users/@me/threads/archived/private` | list_my_private_archived_threads |  | ➖ | ➖ | Non exposé par NetCord @bb5f4e13 |

## /channels/{id}/voice-status

| Method | Path | operationId | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| PUT | `/channels/{channel_id}/voice-status` | update_voice_channel_status | `SetVoiceGuildChannelStatusAsync` | ⬜ | ⬜ | |

## /channels/{id}/webhooks

| Method | Path | operationId | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/channels/{channel_id}/webhooks` | list_channel_webhooks | `GetChannelWebhooksAsync` | ⬜ | ⬜ | |
| POST | `/channels/{channel_id}/webhooks` | create_webhook | `CreateWebhookAsync` | ⬜ | ⬜ | |

## /gateway

| Method | Path | operationId | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/gateway` | get_gateway | `GetGatewayAsync` | ⬜ | ⬜ | |
| GET | `/gateway/bot` | get_bot_gateway | `GetGatewayBotAsync` | ⬜ | ⬜ | |

## /guilds

| Method | Path | operationId | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/guilds/templates/{code}` | get_guild_template | `GetGuildTemplateAsync` | ⬜ | ⬜ | |
| GET | `/guilds/{guild_id}` | get_guild | `GetGuildAsync` | ⬜ | ⬜ | |
| PATCH | `/guilds/{guild_id}` | update_guild | `ModifyGuildAsync` | ⬜ | ⬜ | |

## /guilds/{id}/audit-logs

| Method | Path | operationId | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/guilds/{guild_id}/audit-logs` | list_guild_audit_log_entries | `GetGuildAuditLogAsync` | ⬜ | ⬜ | |

## /guilds/{id}/auto-moderation

| Method | Path | operationId | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/guilds/{guild_id}/auto-moderation/rules` | list_auto_moderation_rules | `GetAutoModerationRulesAsync` | ⬜ | ⬜ | |
| POST | `/guilds/{guild_id}/auto-moderation/rules` | create_auto_moderation_rule | `CreateAutoModerationRuleAsync` | ⬜ | ⬜ | |
| DELETE | `/guilds/{guild_id}/auto-moderation/rules/{rule_id}` | delete_auto_moderation_rule | `DeleteAutoModerationRuleAsync` | ⬜ | ⬜ | |
| GET | `/guilds/{guild_id}/auto-moderation/rules/{rule_id}` | get_auto_moderation_rule | `GetAutoModerationRuleAsync` | ⬜ | ⬜ | |
| PATCH | `/guilds/{guild_id}/auto-moderation/rules/{rule_id}` | update_auto_moderation_rule | `ModifyAutoModerationRuleAsync` | ⬜ | ⬜ | |

## /guilds/{id}/bans

| Method | Path | operationId | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/guilds/{guild_id}/bans` | list_guild_bans | `GetGuildBansAsync` | ⬜ | ⬜ | |
| DELETE | `/guilds/{guild_id}/bans/{user_id}` | unban_user_from_guild | `UnbanGuildUserAsync` | ⬜ | ⬜ | |
| GET | `/guilds/{guild_id}/bans/{user_id}` | get_guild_ban | `GetGuildBanAsync` | ⬜ | ⬜ | |
| PUT | `/guilds/{guild_id}/bans/{user_id}` | ban_user_from_guild | `BanGuildUserAsync` | ⬜ | ⬜ | |

## /guilds/{id}/bulk-ban

| Method | Path | operationId | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| POST | `/guilds/{guild_id}/bulk-ban` | bulk_ban_users_from_guild | `BanGuildUsersAsync` | ⬜ | ⬜ | |

## /guilds/{id}/channels

| Method | Path | operationId | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/guilds/{guild_id}/channels` | list_guild_channels | `GetGuildChannelsAsync` | ⬜ | ⬜ | |
| PATCH | `/guilds/{guild_id}/channels` | bulk_update_guild_channels | `ModifyGuildChannelPositionsAsync` | ⬜ | ⬜ | |
| POST | `/guilds/{guild_id}/channels` | create_guild_channel | `CreateGuildChannelAsync` | ⬜ | ⬜ | |

## /guilds/{id}/emojis

| Method | Path | operationId | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/guilds/{guild_id}/emojis` | list_guild_emojis | `GetGuildEmojisAsync` | ⬜ | ⬜ | |
| POST | `/guilds/{guild_id}/emojis` | create_guild_emoji | `CreateGuildEmojiAsync` | ⬜ | ⬜ | |
| DELETE | `/guilds/{guild_id}/emojis/{emoji_id}` | delete_guild_emoji | `DeleteGuildEmojiAsync` | ⬜ | ⬜ | |
| GET | `/guilds/{guild_id}/emojis/{emoji_id}` | get_guild_emoji | `GetGuildEmojiAsync` | ⬜ | ⬜ | |
| PATCH | `/guilds/{guild_id}/emojis/{emoji_id}` | update_guild_emoji | `ModifyGuildEmojiAsync` | ⬜ | ⬜ | |

## /guilds/{id}/incident-actions

| Method | Path | operationId | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| PUT | `/guilds/{guild_id}/incident-actions` | update_guild_incident_actions |  | ➖ | ➖ | Non exposé par NetCord @bb5f4e13 |

## /guilds/{id}/integrations

| Method | Path | operationId | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/guilds/{guild_id}/integrations` | list_guild_integrations | `GetGuildIntegrationsAsync` | ⬜ | ⬜ | |
| DELETE | `/guilds/{guild_id}/integrations/{integration_id}` | delete_guild_integration | `DeleteGuildIntegrationAsync` | ⬜ | ⬜ | |

## /guilds/{id}/invites

| Method | Path | operationId | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/guilds/{guild_id}/invites` | list_guild_invites | `GetGuildInvitesAsync` | ⬜ | ⬜ | |

## /guilds/{id}/members

| Method | Path | operationId | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/guilds/{guild_id}/members` | list_guild_members | `GetGuildUsersAsync` | ⬜ | ⬜ | |
| PATCH | `/guilds/{guild_id}/members/@me` | update_my_guild_member | `ModifyCurrentGuildUserAsync` | ⬜ | ⬜ | |
| GET | `/guilds/{guild_id}/members/search` | search_guild_members | `FindGuildUserAsync` | ⬜ | ⬜ | |
| DELETE | `/guilds/{guild_id}/members/{user_id}` | delete_guild_member | `KickGuildUserAsync` | ⬜ | ⬜ | |
| GET | `/guilds/{guild_id}/members/{user_id}` | get_guild_member | `GetGuildUserAsync` | ⬜ | ⬜ | |
| PATCH | `/guilds/{guild_id}/members/{user_id}` | update_guild_member | `ModifyGuildUserAsync` | ⬜ | ⬜ | |
| PUT | `/guilds/{guild_id}/members/{user_id}` | add_guild_member | `AddGuildUserAsync` | ⬜ | ⬜ | |
| DELETE | `/guilds/{guild_id}/members/{user_id}/roles/{role_id}` | delete_guild_member_role | `RemoveGuildUserRoleAsync` | ⬜ | ⬜ | |
| PUT | `/guilds/{guild_id}/members/{user_id}/roles/{role_id}` | add_guild_member_role | `AddGuildUserRoleAsync` | ⬜ | ⬜ | |

## /guilds/{id}/messages

| Method | Path | operationId | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/guilds/{guild_id}/messages/search` | guild_search | `SearchGuildMessagesAsync` | ⬜ | ⬜ | |

## /guilds/{id}/new-member-welcome

| Method | Path | operationId | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/guilds/{guild_id}/new-member-welcome` | get_guild_new_member_welcome |  | ➖ | ➖ | Non exposé par NetCord @bb5f4e13 |

## /guilds/{id}/onboarding

| Method | Path | operationId | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/guilds/{guild_id}/onboarding` | get_guilds_onboarding | `GetGuildOnboardingAsync` | ⬜ | ⬜ | |
| PUT | `/guilds/{guild_id}/onboarding` | put_guilds_onboarding | `ModifyGuildOnboardingAsync` | ⬜ | ⬜ | |

## /guilds/{id}/permissions

| Method | Path | operationId | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/guilds/{guild_id}/permissions/member/{user_id}` | get_guild_member_permissions |  | ➖ | ➖ | Non exposé par NetCord @bb5f4e13 |
| GET | `/guilds/{guild_id}/permissions/role/{role_id}` | get_guild_role_permissions |  | ➖ | ➖ | Non exposé par NetCord @bb5f4e13 |

## /guilds/{id}/preview

| Method | Path | operationId | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/guilds/{guild_id}/preview` | get_guild_preview | `GetGuildPreviewAsync` | ⬜ | ⬜ | |

## /guilds/{id}/prune

| Method | Path | operationId | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/guilds/{guild_id}/prune` | preview_prune_guild | `GetGuildPruneCountAsync` | ⬜ | ⬜ | |
| POST | `/guilds/{guild_id}/prune` | prune_guild | `GuildPruneAsync` | ⬜ | ⬜ | |

## /guilds/{id}/regions

| Method | Path | operationId | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/guilds/{guild_id}/regions` | list_guild_voice_regions | `GetGuildVoiceRegionsAsync` | ⬜ | ⬜ | |

## /guilds/{id}/requests

| Method | Path | operationId | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/guilds/{guild_id}/requests` | get_guild_join_requests |  | ➖ | ➖ | Non exposé par NetCord @bb5f4e13 |
| PATCH | `/guilds/{guild_id}/requests/{request_id}` | action_guild_join_request |  | ➖ | ➖ | Non exposé par NetCord @bb5f4e13 |

## /guilds/{id}/roles

| Method | Path | operationId | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/guilds/{guild_id}/roles` | list_guild_roles | `GetGuildRolesAsync` | ⬜ | ⬜ | |
| PATCH | `/guilds/{guild_id}/roles` | bulk_update_guild_roles | `ModifyGuildRolePositionsAsync` | ⬜ | ⬜ | |
| POST | `/guilds/{guild_id}/roles` | create_guild_role | `CreateGuildRoleAsync` | ⬜ | ⬜ | |
| GET | `/guilds/{guild_id}/roles/member-counts` | guild_role_member_counts |  | ➖ | ➖ | Non exposé par NetCord @bb5f4e13 |
| DELETE | `/guilds/{guild_id}/roles/{role_id}` | delete_guild_role | `DeleteGuildRoleAsync` | ⬜ | ⬜ | |
| GET | `/guilds/{guild_id}/roles/{role_id}` | get_guild_role | `GetGuildRoleAsync` | ⬜ | ⬜ | |
| PATCH | `/guilds/{guild_id}/roles/{role_id}` | update_guild_role | `ModifyGuildRoleAsync` | ⬜ | ⬜ | |

## /guilds/{id}/scheduled-events

| Method | Path | operationId | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/guilds/{guild_id}/scheduled-events` | list_guild_scheduled_events | `GetGuildScheduledEventsAsync` | ⬜ | ⬜ | |
| POST | `/guilds/{guild_id}/scheduled-events` | create_guild_scheduled_event | `CreateGuildScheduledEventAsync` | ⬜ | ⬜ | |
| DELETE | `/guilds/{guild_id}/scheduled-events/{guild_scheduled_event_id}` | delete_guild_scheduled_event | `DeleteGuildScheduledEventAsync` | ⬜ | ⬜ | |
| GET | `/guilds/{guild_id}/scheduled-events/{guild_scheduled_event_id}` | get_guild_scheduled_event | `GetGuildScheduledEventAsync` | ⬜ | ⬜ | |
| PATCH | `/guilds/{guild_id}/scheduled-events/{guild_scheduled_event_id}` | update_guild_scheduled_event | `ModifyGuildScheduledEventAsync` | ⬜ | ⬜ | |
| POST | `/guilds/{guild_id}/scheduled-events/{guild_scheduled_event_id}/exceptions` | create_guild_scheduled_event_exception |  | ➖ | ➖ | Non exposé par NetCord @bb5f4e13 |
| DELETE | `/guilds/{guild_id}/scheduled-events/{guild_scheduled_event_id}/exceptions/{exception_id}` | delete_guild_scheduled_event_exception |  | ➖ | ➖ | Non exposé par NetCord @bb5f4e13 |
| PATCH | `/guilds/{guild_id}/scheduled-events/{guild_scheduled_event_id}/exceptions/{exception_id}` | update_guild_scheduled_event_exception |  | ➖ | ➖ | Non exposé par NetCord @bb5f4e13 |
| GET | `/guilds/{guild_id}/scheduled-events/{guild_scheduled_event_id}/users` | list_guild_scheduled_event_users | `GetGuildScheduledEventUsersAsync` | ⬜ | ⬜ | |
| GET | `/guilds/{guild_id}/scheduled-events/{guild_scheduled_event_id}/users/counts` | count_guild_scheduled_event_users |  | ➖ | ➖ | Non exposé par NetCord @bb5f4e13 |
| GET | `/guilds/{guild_id}/scheduled-events/{guild_scheduled_event_id}/{guild_scheduled_event_exception_id}/users` | list_guild_scheduled_event_exception_users |  | ➖ | ➖ | Non exposé par NetCord @bb5f4e13 |

## /guilds/{id}/soundboard-sounds

| Method | Path | operationId | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/guilds/{guild_id}/soundboard-sounds` | list_guild_soundboard_sounds |  | ➖ | ➖ | Non exposé par NetCord @bb5f4e13 ; soundboard (rouvrir si NetCord l’ajoute) |
| POST | `/guilds/{guild_id}/soundboard-sounds` | create_guild_soundboard_sound |  | ➖ | ➖ | Non exposé par NetCord @bb5f4e13 ; soundboard (rouvrir si NetCord l’ajoute) |
| DELETE | `/guilds/{guild_id}/soundboard-sounds/{sound_id}` | delete_guild_soundboard_sound |  | ➖ | ➖ | Non exposé par NetCord @bb5f4e13 ; soundboard (rouvrir si NetCord l’ajoute) |
| GET | `/guilds/{guild_id}/soundboard-sounds/{sound_id}` | get_guild_soundboard_sound |  | ➖ | ➖ | Non exposé par NetCord @bb5f4e13 ; soundboard (rouvrir si NetCord l’ajoute) |
| PATCH | `/guilds/{guild_id}/soundboard-sounds/{sound_id}` | update_guild_soundboard_sound |  | ➖ | ➖ | Non exposé par NetCord @bb5f4e13 ; soundboard (rouvrir si NetCord l’ajoute) |

## /guilds/{id}/stickers

| Method | Path | operationId | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/guilds/{guild_id}/stickers` | list_guild_stickers | `GetGuildStickersAsync` | ⬜ | ⬜ | |
| POST | `/guilds/{guild_id}/stickers` | create_guild_sticker | `CreateGuildStickerAsync` | ⬜ | ⬜ | |
| DELETE | `/guilds/{guild_id}/stickers/{sticker_id}` | delete_guild_sticker | `DeleteGuildStickerAsync` | ⬜ | ⬜ | |
| GET | `/guilds/{guild_id}/stickers/{sticker_id}` | get_guild_sticker | `GetGuildStickerAsync` | ⬜ | ⬜ | |
| PATCH | `/guilds/{guild_id}/stickers/{sticker_id}` | update_guild_sticker | `ModifyGuildStickerAsync` | ⬜ | ⬜ | |

## /guilds/{id}/templates

| Method | Path | operationId | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/guilds/{guild_id}/templates` | list_guild_templates | `GetGuildTemplatesAsync` | ⬜ | ⬜ | |
| POST | `/guilds/{guild_id}/templates` | create_guild_template | `CreateGuildTemplateAsync` | ⬜ | ⬜ | |
| DELETE | `/guilds/{guild_id}/templates/{code}` | delete_guild_template | `DeleteGuildTemplateAsync` | ⬜ | ⬜ | |
| PATCH | `/guilds/{guild_id}/templates/{code}` | update_guild_template | `ModifyGuildTemplateAsync` | ⬜ | ⬜ | |
| PUT | `/guilds/{guild_id}/templates/{code}` | sync_guild_template | `SyncGuildTemplateAsync` | ⬜ | ⬜ | |

## /guilds/{id}/threads

| Method | Path | operationId | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/guilds/{guild_id}/threads/active` | get_active_guild_threads | `GetActiveGuildThreadsAsync` | ⬜ | ⬜ | |

## /guilds/{id}/vanity-url

| Method | Path | operationId | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/guilds/{guild_id}/vanity-url` | get_guild_vanity_url | `GetGuildVanityInviteAsync` | ⬜ | ⬜ | |

## /guilds/{id}/voice-states

| Method | Path | operationId | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/guilds/{guild_id}/voice-states/@me` | get_self_voice_state | `GetCurrentGuildUserVoiceStateAsync` | ⬜ | ⬜ | |
| PATCH | `/guilds/{guild_id}/voice-states/@me` | update_self_voice_state | `ModifyCurrentGuildUserVoiceStateAsync` | ⬜ | ⬜ | |
| GET | `/guilds/{guild_id}/voice-states/{user_id}` | get_voice_state | `GetGuildUserVoiceStateAsync` | ⬜ | ⬜ | |
| PATCH | `/guilds/{guild_id}/voice-states/{user_id}` | update_voice_state | `ModifyGuildUserVoiceStateAsync` | ⬜ | ⬜ | |

## /guilds/{id}/webhooks

| Method | Path | operationId | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/guilds/{guild_id}/webhooks` | get_guild_webhooks | `GetGuildWebhooksAsync` | ⬜ | ⬜ | |

## /guilds/{id}/welcome-screen

| Method | Path | operationId | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/guilds/{guild_id}/welcome-screen` | get_guild_welcome_screen | `GetGuildWelcomeScreenAsync` | ⬜ | ⬜ | |
| PATCH | `/guilds/{guild_id}/welcome-screen` | update_guild_welcome_screen | `ModifyGuildWelcomeScreenAsync` | ⬜ | ⬜ | |

## /guilds/{id}/widget

| Method | Path | operationId | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/guilds/{guild_id}/widget` | get_guild_widget_settings | `GetGuildWidgetSettingsAsync` | ⬜ | ⬜ | |
| PATCH | `/guilds/{guild_id}/widget` | update_guild_widget_settings | `ModifyGuildWidgetSettingsAsync` | ⬜ | ⬜ | |

## /guilds/{id}/widget.json

| Method | Path | operationId | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/guilds/{guild_id}/widget.json` | get_guild_widget | `GetGuildWidgetAsync` | ⬜ | ⬜ | |

## /guilds/{id}/widget.png

| Method | Path | operationId | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/guilds/{guild_id}/widget.png` | get_guild_widget_png |  | ➖ | ➖ | Non exposé par NetCord @bb5f4e13 |

## /interactions

| Method | Path | operationId | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| POST | `/interactions/{interaction_id}/{interaction_token}/callback` | create_interaction_response | `SendInteractionResponseAsync` | ⬜ | ⬜ | |

## /invites

| Method | Path | operationId | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| DELETE | `/invites/{code}` | invite_revoke | `DeleteInviteAsync` | ⬜ | ⬜ | |
| GET | `/invites/{code}` | invite_resolve | `GetInviteAsync` | ⬜ | ⬜ | |
| GET | `/invites/{code}/target-users` | get_invite_target_users | `GetInviteTargetUsersAsync` | ⬜ | ⬜ | |
| PUT | `/invites/{code}/target-users` | update_invite_target_users | `UpdateInviteTargetUsersAsync` | ⬜ | ⬜ | |
| POST | `/invites/{code}/target-users/bulk-add` | bulk_add_invite_target_users |  | ➖ | ➖ | Non exposé par NetCord @bb5f4e13 |
| POST | `/invites/{code}/target-users/bulk-delete` | bulk_remove_invite_target_users |  | ➖ | ➖ | Non exposé par NetCord @bb5f4e13 |
| GET | `/invites/{code}/target-users/job-status` | get_invite_target_users_job_status | `GetInviteTargetUsersJobStatusAsync` | ⬜ | ⬜ | |
| DELETE | `/invites/{code}/target-users/{user_id}` | remove_invite_target_user |  | ➖ | ➖ | Non exposé par NetCord @bb5f4e13 |
| PUT | `/invites/{code}/target-users/{user_id}` | add_invite_target_user |  | ➖ | ➖ | Non exposé par NetCord @bb5f4e13 |

## /lobbies

| Method | Path | operationId | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| POST | `/lobbies` | create_lobby |  | ➖ | ➖ | Non exposé par NetCord @bb5f4e13 ; Social SDK hors vision |
| PUT | `/lobbies` | create_or_join_lobby |  | ➖ | ➖ | Non exposé par NetCord @bb5f4e13 ; Social SDK hors vision |
| DELETE | `/lobbies/{lobby_id}` | delete_lobby |  | ➖ | ➖ | Non exposé par NetCord @bb5f4e13 ; Social SDK hors vision |
| GET | `/lobbies/{lobby_id}` | get_lobby |  | ➖ | ➖ | Non exposé par NetCord @bb5f4e13 ; Social SDK hors vision |
| PATCH | `/lobbies/{lobby_id}` | edit_lobby |  | ➖ | ➖ | Non exposé par NetCord @bb5f4e13 ; Social SDK hors vision |
| PATCH | `/lobbies/{lobby_id}/channel-linking` | edit_lobby_channel_link |  | ➖ | ➖ | Non exposé par NetCord @bb5f4e13 ; Social SDK hors vision |
| DELETE | `/lobbies/{lobby_id}/members/@me` | leave_lobby |  | ➖ | ➖ | Non exposé par NetCord @bb5f4e13 ; Social SDK hors vision |
| POST | `/lobbies/{lobby_id}/members/@me/invites` | create_linked_lobby_guild_invite_for_self |  | ➖ | ➖ | Non exposé par NetCord @bb5f4e13 ; Social SDK hors vision |
| POST | `/lobbies/{lobby_id}/members/bulk` | bulk_update_lobby_members |  | ➖ | ➖ | Non exposé par NetCord @bb5f4e13 ; Social SDK hors vision |
| DELETE | `/lobbies/{lobby_id}/members/{user_id}` | delete_lobby_member |  | ➖ | ➖ | Non exposé par NetCord @bb5f4e13 ; Social SDK hors vision |
| PUT | `/lobbies/{lobby_id}/members/{user_id}` | add_lobby_member |  | ➖ | ➖ | Non exposé par NetCord @bb5f4e13 ; Social SDK hors vision |
| POST | `/lobbies/{lobby_id}/members/{user_id}/invites` | create_linked_lobby_guild_invite_for_user |  | ➖ | ➖ | Non exposé par NetCord @bb5f4e13 ; Social SDK hors vision |
| GET | `/lobbies/{lobby_id}/messages` | get_lobby_messages |  | ➖ | ➖ | Non exposé par NetCord @bb5f4e13 ; Social SDK hors vision |
| POST | `/lobbies/{lobby_id}/messages` | create_lobby_message |  | ➖ | ➖ | Non exposé par NetCord @bb5f4e13 ; Social SDK hors vision |
| PUT | `/lobbies/{lobby_id}/messages/{message_id}/moderation-metadata` | update_lobby_message_external_moderation_metadata |  | ➖ | ➖ | Non exposé par NetCord @bb5f4e13 ; Social SDK hors vision |

## /oauth2

| Method | Path | operationId | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/oauth2/@me` | get_my_oauth2_authorization | `GetCurrentAuthorizationInformationAsync` | ⬜ | ⬜ | |
| GET | `/oauth2/applications/@me` | get_my_oauth2_application | `GetCurrentBotApplicationInformationAsync` | ⬜ | ⬜ | |
| GET | `/oauth2/keys` | get_public_keys |  | ➖ | ➖ | Non exposé par NetCord @bb5f4e13 |
| GET | `/oauth2/userinfo` | get_openid_connect_userinfo |  | ➖ | ➖ | Non exposé par NetCord @bb5f4e13 |

## /partner-sdk

| Method | Path | operationId | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| PUT | `/partner-sdk/dms/{user_id_1}/{user_id_2}/messages/{message_id}/moderation-metadata` | update_user_message_external_moderation_metadata |  | ➖ | ➖ | Non exposé par NetCord @bb5f4e13 ; Social SDK hors vision |
| POST | `/partner-sdk/provisional-accounts/unmerge` | partner_sdk_unmerge_provisional_account |  | ➖ | ➖ | Non exposé par NetCord @bb5f4e13 ; Social SDK hors vision |
| POST | `/partner-sdk/provisional-accounts/unmerge/bot` | bot_partner_sdk_unmerge_provisional_account |  | ➖ | ➖ | Non exposé par NetCord @bb5f4e13 ; Social SDK hors vision |
| POST | `/partner-sdk/token` | partner_sdk_token |  | ➖ | ➖ | Non exposé par NetCord @bb5f4e13 ; Social SDK hors vision |
| POST | `/partner-sdk/token/bot` | bot_partner_sdk_token |  | ➖ | ➖ | Non exposé par NetCord @bb5f4e13 ; Social SDK hors vision |

## /skus

| Method | Path | operationId | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/skus/{sku_id}/subscriptions` | get_sku_subscriptions | `GetSkuSubscriptionsAsync` | ⬜ | ⬜ | |
| GET | `/skus/{sku_id}/subscriptions/{subscription_id}` | get_sku_subscription | `GetSkuSubscriptionAsync` | ⬜ | ⬜ | |

## /soundboard-default-sounds

| Method | Path | operationId | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/soundboard-default-sounds` | get_soundboard_default_sounds |  | ➖ | ➖ | Non exposé par NetCord @bb5f4e13 ; soundboard (rouvrir si NetCord l’ajoute) |

## /stage-instances

| Method | Path | operationId | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| POST | `/stage-instances` | create_stage_instance | `CreateStageInstanceAsync` | ⬜ | ⬜ | |
| DELETE | `/stage-instances/{channel_id}` | delete_stage_instance | `DeleteStageInstanceAsync` | ⬜ | ⬜ | |
| GET | `/stage-instances/{channel_id}` | get_stage_instance | `GetStageInstanceAsync` | ⬜ | ⬜ | |
| PATCH | `/stage-instances/{channel_id}` | update_stage_instance | `ModifyStageInstanceAsync` | ⬜ | ⬜ | |

## /sticker-packs

| Method | Path | operationId | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/sticker-packs` | list_sticker_packs | `GetStickerPacksAsync` | ⬜ | ⬜ | |
| GET | `/sticker-packs/{pack_id}` | get_sticker_pack | `GetStickerPackAsync` | ⬜ | ⬜ | |

## /stickers

| Method | Path | operationId | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/stickers/{sticker_id}` | get_sticker | `GetStickerAsync` | ⬜ | ⬜ | |

## /users

| Method | Path | operationId | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/users/@me` | get_my_user | `GetCurrentUserAsync` | ✅ | ✅ | Forme `UserPIIResponse` (`discriminator` `"0"`, `mfa_enabled` false, `locale` `en-US`, `verified` true, `email` null). Pas d'identité OAuth2/Bearer (la sécurité `OAuth2` de l'opération ne correspond jamais) ; `flags`, `public_flags`, `accent_color`, `avatar_decoration_data`, `collectibles`, `primary_guild` toujours vides (M1-REST-01) |
| PATCH | `/users/@me` | update_my_user | `ModifyCurrentUserAsync` | 🟡 | ✅ | `username` (2–32 caractères, `BASE_TYPE_BAD_LENGTH`), `avatar`/`banner` (data URI base64 png/jpeg/gif et, écart toléré, webp : la doc `reference.mdx` (Image Data) ne liste que JPG, GIF et PNG mais aucune erreur Discord documentée ne permettrait de refuser webp, `null` retire ; hash déterministe dérivé des octets, `a_` pour un GIF) ; fait `UserUpdated` publié (aucun dispatch `USER_UPDATE` avant M3), seulement si une valeur change. Tous les champs optionnels (docs), alors que la spec marque `username` requis (écart documenté, NetCord n'envoie que les champs renseignés). Manque : contenu de l'image non décodé (signature PNG/JPEG… non vérifiée), mots interdits du username (`discord`, `everyone`…), limite de changements de username, discriminator (M1-REST-01) |
| GET | `/users/@me/applications/{application_id}/entitlements` | get_current_user_application_entitlements |  | ➖ | ➖ | Non exposé par NetCord @bb5f4e13 |
| DELETE | `/users/@me/applications/{application_id}/role-connection` | delete_application_user_role_connection |  | ➖ | ➖ | Non exposé par NetCord @bb5f4e13 |
| GET | `/users/@me/applications/{application_id}/role-connection` | get_application_user_role_connection | `GetCurrentUserApplicationRoleConnectionAsync` | ⬜ | ⬜ | |
| PUT | `/users/@me/applications/{application_id}/role-connection` | update_application_user_role_connection | `UpdateCurrentUserApplicationRoleConnectionAsync` | ⬜ | ⬜ | |
| POST | `/users/@me/channels` | create_dm | `CreateGroupDMChannelAsync`, `GetDMChannelAsync` | 🟡 | ✅ | DM 1:1 : get-or-create (même salon au 2e appel, fait `ChannelCreated` publié une seule fois — aucun dispatch avant M3 ; Discord n'envoie pas `CHANNEL_CREATE` pour un DM, `events/gateway-events.mdx` « Sent when a new guild channel is created » : M3 devra ignorer le `ChannelCreated` d'un DM), `recipient_id` accepté en nombre (NetCord) ou en chaîne, `10013` si `recipient_id` inconnu (comportement fauxcord, non vérifié sur Discord), `50035` (`BASE_TYPE_REQUIRED`/`NUMBER_TYPE_COERCE`). Group DM non simulé : le fake n'a aucun grant OAuth2 (`gdm.join`), donc tout `access_tokens` non vide est refusé avec `50025` (HTTP 403 : documenté pour `50025` dans `merging-accounts.mdx:347`, non vérifié pour un group DM) ; `access_tokens: []` est traité comme absent. Manque : DM vers soi-même/un autre bot (`50007`, erreur exacte non vérifiée), option de seed « DM refusé » (M1-REST-01) |
| GET | `/users/@me/connections` | list_my_connections | `GetCurrentUserConnectionsAsync` | ⬜ | ⬜ | |
| GET | `/users/@me/guilds` | list_my_guilds | `GetCurrentUserGuildsAsync` | ⬜ | ⬜ | |
| DELETE | `/users/@me/guilds/{guild_id}` | leave_guild | `LeaveGuildAsync` | ⬜ | ⬜ | |
| GET | `/users/@me/guilds/{guild_id}/member` | get_my_guild_member | `GetCurrentUserGuildUserAsync` | ⬜ | ⬜ | |
| GET | `/users/{user_id}` | get_user | `GetUserAsync` | ✅ | ✅ | Forme `UserResponse` (`bot` omis pour un humain, pas de champs de compte) ; `10013` si l'utilisateur n'existe pas dans l'état (seed ou identité du bot), `50035` `NUMBER_TYPE_COERCE` (`Value "abc" is not snowflake.`) si `user_id` n'est pas un snowflake. Champs de profil riches toujours vides (M1-REST-01) |

## /voice

| Method | Path | operationId | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/voice/regions` | list_voice_regions | `GetVoiceRegionsAsync` | ⬜ | ⬜ | |

## /webhooks

| Method | Path | operationId | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| DELETE | `/webhooks/{webhook_id}` | delete_webhook | `DeleteWebhookAsync` | ⬜ | ⬜ | |
| GET | `/webhooks/{webhook_id}` | get_webhook | `GetWebhookAsync` | ⬜ | ⬜ | |
| PATCH | `/webhooks/{webhook_id}` | update_webhook | `ModifyWebhookAsync` | ⬜ | ⬜ | |
| DELETE | `/webhooks/{webhook_id}/{webhook_token}` | delete_webhook_by_token | `DeleteWebhookWithTokenAsync` | ⬜ | ⬜ | |
| GET | `/webhooks/{webhook_id}/{webhook_token}` | get_webhook_by_token | `GetWebhookWithTokenAsync` | ⬜ | ⬜ | |
| PATCH | `/webhooks/{webhook_id}/{webhook_token}` | update_webhook_by_token | `ModifyWebhookWithTokenAsync` | ⬜ | ⬜ | |
| POST | `/webhooks/{webhook_id}/{webhook_token}` | execute_webhook | `ExecuteWebhookAsync`, `SendInteractionFollowupMessageAsync` | ⬜ | ⬜ | |
| POST | `/webhooks/{webhook_id}/{webhook_token}/github` | execute_github_compatible_webhook |  | ➖ | ➖ | Non exposé par NetCord @bb5f4e13 |
| DELETE | `/webhooks/{webhook_id}/{webhook_token}/messages/@original` | delete_original_webhook_message | `DeleteInteractionResponseAsync` | ⬜ | ⬜ | |
| GET | `/webhooks/{webhook_id}/{webhook_token}/messages/@original` | get_original_webhook_message | `GetInteractionResponseAsync` | ⬜ | ⬜ | |
| PATCH | `/webhooks/{webhook_id}/{webhook_token}/messages/@original` | update_original_webhook_message | `ModifyInteractionResponseAsync` | ⬜ | ⬜ | |
| DELETE | `/webhooks/{webhook_id}/{webhook_token}/messages/{message_id}` | delete_webhook_message | `DeleteInteractionFollowupMessageAsync`, `DeleteWebhookMessageAsync` | ⬜ | ⬜ | |
| GET | `/webhooks/{webhook_id}/{webhook_token}/messages/{message_id}` | get_webhook_message | `GetInteractionFollowupMessageAsync`, `GetWebhookMessageAsync` | ⬜ | ⬜ | |
| PATCH | `/webhooks/{webhook_id}/{webhook_token}/messages/{message_id}` | update_webhook_message | `ModifyInteractionFollowupMessageAsync`, `ModifyWebhookMessageAsync` | ⬜ | ⬜ | |
| POST | `/webhooks/{webhook_id}/{webhook_token}/slack` | execute_slack_compatible_webhook |  | ➖ | ➖ | Non exposé par NetCord @bb5f4e13 |

## Routes NetCord hors spec publique

| Method | Route | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- |
| GET | `/applications/{application_id}/rpc` | `GetApplicationAsync` | ⬜ | ⬜ | Dans l'inventaire du fake (`NetCordOnlyRoutes`, auth Bot) ; handler à venir |
| GET | `/applications/{application_id}/skus` | `GetSkusAsync` | ⬜ | ⬜ | Dans l'inventaire du fake (`NetCordOnlyRoutes`, auth Bot) ; handler à venir |
| POST | `/channels/{channel_id}/attachments` | `CreateGoogleCloudPlatformStorageBucketsAsync` | ⬜ | ⬜ | Dans l'inventaire du fake (`NetCordOnlyRoutes`, auth Bot) ; handler à venir |
| POST | `/guilds` | `CreateGuildAsync` | ⬜ | ⬜ | Dans l'inventaire du fake (`NetCordOnlyRoutes`, auth Bot) ; handler à venir |
| POST | `/guilds/templates/{code}` | `CreateGuildFromGuildTemplateAsync` | ⬜ | ⬜ | Dans l'inventaire du fake (`NetCordOnlyRoutes`, auth Bot) ; nom de paramètre `code` repris de la spec (`GET`) ; handler à venir |
| DELETE | `/guilds/{guild_id}` | `DeleteGuildAsync` | ⬜ | ⬜ | Dans l'inventaire du fake (`NetCordOnlyRoutes`, auth Bot) ; `guild_id` repris de la spec (`GET`/`PATCH`) ; handler à venir |
| POST | `/guilds/{guild_id}/members-search` | `SearchGuildUsersAsync` | ⬜ | ⬜ | Dans l'inventaire du fake (`NetCordOnlyRoutes`, auth Bot) ; handler à venir |
| POST | `/guilds/{guild_id}/mfa` | `ModifyGuildMfaLevelAsync` | ⬜ | ⬜ | Dans l'inventaire du fake (`NetCordOnlyRoutes`, auth Bot) ; handler à venir |
