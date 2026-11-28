# Points à revoir par l'utilisateur

> Tenu par l'orchestrateur, sur `main` (règle « Autonomie et points utilisateur » d'AGENTS.md).
> Les agents ne s'arrêtent pas pour demander : ils tranchent, implémentent et consignent ici ce
> qui mérite un regard humain. Traite les points à ton rythme : remplis le champ **Réponse**
> (ou réponds en session) ; l'orchestrateur passe alors le point en « Traité » et crée les
> tâches qui en découlent.

## Types de points

| Type | Sens | Ce qu'on attend de toi |
| --- | --- | --- |
| `test-manuel` | Comportement du fake fondé sur une hypothèse que les sources hors ligne ne tranchent pas | Lancer la procédure contre le vrai Discord, noter le résultat observé |
| `décision` | Arbitrage fait à ta place (API publique, périmètre, compromis) | Valider ou demander un changement |
| `info` | À savoir, sans action requise (limite connue, dette assumée) | Rien, ou réagir si besoin |

## Format d'un point

```markdown
### UR-NNN — <titre court> (`<type>`)

- **Statut** : Ouvert | Traité (AAAA-MM-JJ)
- **Origine** : <ID de tâche>, <ADR / commit>
- **Concerne** : <lignes de parité (`METHOD /path`, `10003`…)>, <tests (`Class.Method`)>
- **Hypothèse / décision** : ce que le fake fait aujourd'hui, et pourquoi (sources consultées).
- **Procédure** (`test-manuel`) : étapes reproductibles contre le vrai Discord (bot de test,
  guilde de test, appel `curl` ou petit bot NetCord), permissions nécessaires.
- **Résultat attendu** : statut HTTP, `code` JSON, corps, events Gateway et leur ordre.
- **Si différent** : ce qu'il faudra changer (fake, test, matrice).
- **Réponse** : _(à remplir)_
```

Pour un `test-manuel`, la ligne de parité concernée reste 🟡 avec la note « à confirmer
(UR-NNN) » tant que le point est ouvert, sauf si les sources officielles suffisent déjà. Une
fois le résultat connu, le test du fake est aligné sur le comportement observé et cite UR-NNN.

## Prérequis communs aux tests manuels

- Une application Discord **de test** avec son bot (jeton dans `$TOKEN`, ID dans `$APP`).
- Une guilde de test **G1** où le bot est membre, avec un salon texte `$CH1` ; une guilde **G2**
  dont le bot n'est **pas** membre (le retirer après avoir noté `$G2` et un salon `$CH2`).
- Appels REST : `curl -sS -i -H "Authorization: Bot $TOKEN" -H "Content-Type: application/json"
  https://discord.com/api/v10/...` ; noter le statut HTTP, le `code` et l'arbre `errors`.
- Interactions et Gateway : un petit bot NetCord (ou `SampleBot`) connecté, qui journalise les
  dispatchs reçus et le résultat brut de ses réponses.

## Ouverts

### UR-001 — Visibilité d'une guilde sans le bot (`test-manuel`)

- **Statut** : Ouvert
- **Origine** : M1-05, M1-REST-06 ; [ADR 0004, amendement 04B § 2](adr/0004-fake-discord-public-api.md) ; roadmap `PAR-ERR-02`
- **Concerne** : `errors.md` `10004`, `50001` ; `rest.md` `/guilds/{guild_id}…`, `/channels/{channel_id}`, `/applications/{application_id}/guilds/{guild_id}/commands`, `DELETE /users/@me/guilds/{guild_id}`
- **Hypothèse** : guilde elle-même hors du bot ⇒ 404 `10004` (documenté pour `GET` seulement) ;
  sous-ressources, salons et commandes de guilde ⇒ 403 `50001` (observé, non documenté) ;
  départ d'une guilde non membre ⇒ 404 `10004` ; validation `50035` avant `50001`/`10004` ;
  réponse et followup d'interaction acceptés après le départ du bot tant que le jeton vit.
- **Procédure** :
  1. `GET`, `PATCH {"name":"x"}` et `GET …/preview` sur `/guilds/$G2`, puis `GET /guilds/1` (inexistante).
  2. `GET /guilds/$G2/channels`, `GET /guilds/$G2/members/$APP`, `GET /guilds/$G2/roles`.
  3. `GET /channels/$CH2`, `GET /channels/$CH2/messages`.
  4. `GET /applications/$APP/guilds/$G2/commands`, puis la même chose avec une guilde inexistante.
  5. `DELETE /users/@me/guilds/$G2`.
  6. `PATCH /channels/$CH2` avec un corps mal formé `{"rate_limit_per_user": -1}` : `50035` ou `50001` ?
  7. Interaction reçue dans G1, puis retirer le bot de G1 et, dans les 15 min, `POST` un followup.
  8. (ajout M1-REST-06) Si G2 est une guilde **publique** (découverte), `GET /guilds/$G2/preview` :
     Discord rend peut-être la preview à un bot non membre ; le fake rend 404 `10004`.
- **Résultat attendu** : 1 ⇒ 404 `10004` ; 2-4 ⇒ 403 `50001` (guilde inexistante en 4 ⇒ 404
  `10004`) ; 5 ⇒ 404 `10004` ; 6 ⇒ 400 `50035` ; 7 ⇒ followup accepté ; 8 ⇒ 404 `10004`
  (hypothèse du fake ; si 200, la preview d'une guilde découvrable échappe à `GuildAccess`).
- **Si différent** : changer le profil de la famille dans `GuildAccess` (aucun handler à toucher)
  ou l'ordre du pipeline (ADR 0005) ; corriger `errors.md` et les tests concernés.
- **Réponse** : _(à remplir)_

### UR-002 — Bot qui rejoint ou quitte une guilde, côté Gateway (`test-manuel`)

- **Statut** : Ouvert
- **Origine** : [ADR 0004, amendement 04B § 3](adr/0004-fake-discord-public-api.md) ; roadmap `PAR-GW-01` (implémentation en M3-02)
- **Concerne** : `gateway.md` `GUILD_CREATE`, `GUILD_DELETE`, `GUILD_MEMBER_ADD`
- **Hypothèse** : l'arrivée du bot produit un `GUILD_CREATE` complet **sans** `GUILD_MEMBER_ADD`
  pour le bot ; son départ produit un `GUILD_DELETE` avec le seul `id`, **sans** `unavailable`.
- **Procédure** : bot connecté avec les intents `GUILDS` et `GUILD_MEMBERS` (privilégié, à activer
  dans le portail) ; l'inviter dans une guilde, puis l'en expulser ; noter les dispatchs bruts.
- **Résultat attendu** : `GUILD_CREATE` seul à l'arrivée ; `GUILD_DELETE` `{ "id": … }` au départ.
- **Si différent** : ajuster la traduction du fait d'adhésion en M3-02 et l'amendement 04B.
- **Réponse** : _(à remplir)_

### UR-003 — Utilisateurs et DM (`test-manuel`)

- **Statut** : Ouvert
- **Origine** : M1-REST-01 ; roadmap `PAR-REST-01`
- **Concerne** : `rest.md` `POST /users/@me/channels`, `PATCH /users/@me`, `GET /users/@me` ; `errors.md` `10013`, `50007`, `50025`
- **Hypothèse** : `access_tokens` invalide (group DM) ⇒ 403 `50025` ; `recipient_id` inconnu ⇒
  `10013` (fauxcord) ; DM vers soi-même ou un autre bot ⇒ `50007` (vers soi-même : refus d'état
  `CannotMessageUser`, 403 `50007`, décidé par l'ADR 0014 et implémenté en M1-REST-02C1 ; vers
  un autre bot : manque) ; avatar/banner WebP accepté ;
  champs `verified`/`email`/`locale` de `GET /users/@me` pour un bot tels que le fake les rend.
- **Procédure** :
  1. `POST /users/@me/channels` `{"access_tokens":["x"],"nicks":{}}`.
  2. `POST /users/@me/channels` `{"recipient_id":"1"}`, puis avec `$APP`, puis avec l'ID d'un autre bot.
  3. `PATCH /users/@me` `{"avatar":"data:image/webp;base64,<petit webp>"}` (limite de
     changements d'avatar : le faire en dernier).
  4. `GET /users/@me` : copier le JSON complet.
- **Résultat attendu** : 1 ⇒ 403 `50025` ; 2 ⇒ `10013`, puis `50007` deux fois ; 3 ⇒ 200 ;
  4 ⇒ comparer au JSON du fake (`tests/Enx.NetCord.Testing.Tests/Rest/Users/CurrentUserRoutesTests.cs`).
- **Si différent** : corriger les handlers `Routes/Users`, `errors.md` et `rest.md`.
- **Réponse** : _(à remplir)_

### UR-004 — `PATCH /channels/{channel_id}` : messages et ordre des erreurs (`test-manuel`)

- **Statut** : Ouvert
- **Origine** : M1-REST-02 ; roadmap `PAR-ERR-03`
- **Concerne** : `rest.md` `PATCH /channels/{channel_id}` ; `errors.md` `50035`
- **Hypothèse** : messages `NUMBER_TYPE_MIN`/`MAX` « int value should be greater than or equal
  to N. », `NUMBER_TYPE_COERCE` « Value "x" is not int. », `BOOLEAN_TYPE_COERCE` « Value "x" is
  not boolean. » ; `type` différent ⇒ `CHANNEL_TYPE_INVALID`, `parent_id` inconnu ⇒
  `CHANNEL_PARENT_INVALID` ; un `50035` structurel passe avant `10003`/`50001` ; le PATCH d'un
  DM 1:1 renvoie le salon inchangé ; Discord réunit validation et refus métier dans un seul arbre.
- **Procédure** (sur `$CH1`, sauf mention) :
  1. `{"rate_limit_per_user": -1}`, `{"rate_limit_per_user": 21601}`, `{"rate_limit_per_user": "x"}`, `{"nsfw": "x"}`.
  2. `{"type": 2}` ; `{"parent_id": "1"}`.
  3. `{"rate_limit_per_user": -1}` sur `/channels/1` (inexistant) puis sur `$CH2`.
  4. `{"name": "x"}` sur un DM 1:1 du bot (créé par `POST /users/@me/channels`).
  5. `{"name": "", "rate_limit_per_user": -1, "type": 2}` : un seul `50035` avec tous les champs ?
- **Résultat attendu** : les messages et codes ci-dessus ; 3 ⇒ 400 `50035` ; 4 ⇒ 200 inchangé ;
  5 ⇒ un arbre `errors` qui réunit `name`, `rate_limit_per_user` et `type`.
- **Si différent** : corriger `DiscordError`, le validateur de `Routes/Channels` et la note de
  `rest.md` ; le point 5, s'il est confirmé, demande de faire remonter les refus d'état dans
  l'arbre de validation (tâche à créer).
- **Réponse** : _(à remplir)_

### UR-005 — Règles des réponses d'interaction (`test-manuel`)

- **Statut** : Ouvert
- **Origine** : M2-ADR-07 ; [ADR 0007 § règles](adr/0007-interaction-model.md) ; roadmap `PAR-ERR-01`
- **Concerne** : `interactions.md` `rule:*`, `callback:*` ; `errors.md` `40060`, `10062`, `10015`, `50027`, `10008`, `50035`
- **Hypothèse** (tableau de l'ADR 0007) : second callback ⇒ 400 `40060` ; callback après 3 s ou
  jeton d'une autre interaction ⇒ 404 `10062` ; jeton jamais émis ⇒ 401 `50027` (fauxcord :
  404 `10015`) ; jeton > 15 min ⇒ 401 `50027` ; followup avant réponse initiale ⇒ 404 `10015` ;
  `@original` absent (supprimé, ou callback 8/9/12) ⇒ 404 `10008` ; type de callback interdit
  (6/7 hors composant, 9 sur modal, 1 hors `PING`, autre que 8 sur autocomplete) ⇒ 400 `50035` ;
  type 12 accepté hors autocomplete ; `thread_id`/`username`/`avatar_url` d'un followup ignorés ;
  message éphémère inconnu (`10008`) des routes de salon et absent des dispatchs.
- **Procédure** : bot NetCord avec une commande slash `/probe` et un bouton ; pour chaque cas,
  une exécution de `/probe` dont le handler fait l'appel indiqué et journalise l'exception
  `RestException` (statut, `code`) : deux réponses de suite ; réponse après `Task.Delay(3500)` ;
  followup avant réponse ; `GET @original` après une réponse modal ; callback type 6 sur la
  commande ; followup avec `thread_id`/`username` ; réponse éphémère puis
  `GET /channels/{id}/messages/{id}` avec le jeton du bot ; followup après 15 min ; followup
  avec un jeton altéré.
- **Résultat attendu** : les statuts et codes de l'hypothèse.
- **Si différent** : corriger les règles de l'ADR 0007 (amendement), le traitement des
  callbacks et `errors.md`.
- **Réponse** : _(à remplir)_

### UR-008 — Lot A : parent et suppression de catégorie (`test-manuel`)

- **Statut** : Ouvert
- **Origine** : M1-ADR-13 ; ADR 0013 § 2, § 3, § 7 ; roadmap `PAR-REST-02`
- **Concerne** : `PATCH`/`DELETE /channels/{channel_id}`, `50035` ; `gateway.md` `CHANNEL_UPDATE`, `CHANNEL_DELETE`, `MESSAGE_DELETE`
- **Hypothèse** : parent inconnu ⇒ `CHANNEL_PARENT_INVALID` ; parent non catégorie ⇒
  `CHANNEL_PARENT_INVALID_TYPE` ; parent donné à une catégorie ⇒ `CHANNEL_PARENT_INVALID` ;
  51ᵉ enfant ⇒ `CHANNEL_PARENT_MAX_CHANNELS` ; à la suppression d'une catégorie, les
  `CHANNEL_UPDATE` des enfants (`parent_id: null`, position inchangée) précèdent le
  `CHANNEL_DELETE` ; supprimer un salon qui contient des messages n'émet aucun `MESSAGE_DELETE`.
- **Procédure** (G1, bot connecté avec l'intent `GUILDS`, qui journalise les dispatchs) :
  1. `PATCH /channels/$CH1` `{"parent_id":"1"}`, puis avec l'ID d'un autre salon texte.
  2. `PATCH` d'une catégorie avec `{"parent_id":"<autre catégorie>"}`.
  3. Créer 50 salons dans une catégorie, puis un 51ᵉ.
  4. Supprimer une catégorie de trois salons ; noter l'ordre des dispatchs et les `position`.
  5. Supprimer un salon contenant des messages ; noter les dispatchs.
- **Résultat attendu** : 1-3 ⇒ 400 `50035` avec les codes ci-dessus ; 4 ⇒ trois
  `CHANNEL_UPDATE` puis `CHANNEL_DELETE` ; 5 ⇒ `CHANNEL_DELETE` seul.
- **Si différent** : corriger `DiscordError.FromStateFailure`, l'ordre des faits de
  `DeleteChannel`, `errors.md` et le § 7 de l'ADR 0013.
- **Réponse** : _(à remplir)_

### UR-009 — Lot B : positions et listing (`test-manuel`)

- **Statut** : Ouvert
- **Origine** : M1-ADR-13, M1-REST-06 ; ADR 0013 § 4 ; roadmap `PAR-REST-02`
- **Concerne** : `GET`/`POST`/`PATCH /guilds/{guild_id}/channels`, `PATCH /channels/{channel_id}`, `40009`, `10003` ; tests `tests/Enx.NetCord.Testing.Tests/Rest/Guilds/*`
- **Hypothèse** : position par défaut = suivante du groupe (texte, annonces et forum ensemble ;
  voix et stage ; catégories) ; aucun voisin renuméroté ; un déplacement garde la position ;
  listing rendu par ID ; `40009` ne compte que les `parent_id` différents du parent courant ;
  ID inconnu ⇒ 404 `10003`.
- **Procédure** (guilde neuve) :
  1. Créer texte, voix, catégorie, puis un second texte, sans `position` ; noter chaque `position`.
  2. `GET /guilds/$G1/channels` : noter l'ordre du tableau.
  3. `PATCH /channels/<texte 2>` `{"position":0}` ; relire tous les salons et les dispatchs.
  4. `PATCH /channels/<texte 1>` `{"parent_id":"<catégorie>"}` ; noter sa `position`.
  5. `PATCH /guilds/$G1/channels` avec deux entrées dont une seule change réellement de parent,
     puis deux qui changent, puis `[{"id":"1","position":0}]`.
  6. (ajout M1-REST-06) `PATCH /guilds/$G1/channels` avec `[{"position":1}]` (sans `id`), puis
     le même `id` deux fois avec deux `parent_id` différents, puis `[]`.
- **Résultat attendu** : 1 ⇒ 0, 0, 0, 1 ; 2 ⇒ ordre des ID ; 3 ⇒ un seul `CHANNEL_UPDATE`,
  texte 1 inchangé ; 4 ⇒ position inchangée ; 5 ⇒ 204, puis 400 `40009`, puis 404 `10003` ;
  6 ⇒ 400 `50035` (`id` `BASE_TYPE_REQUIRED`), 400 `40009` (le salon cité deux fois compte une
  fois, mais deux parents), 204 sans `CHANNEL_UPDATE` (hypothèses du fake : lot validé en entier
  puis appliqué en un commit, un `CHANNEL_UPDATE` par salon réellement changé, dans l'ordre du
  tableau).
- **Si différent** : changer la règle de position par défaut ou le tri du listing dans l'état,
  et le § 4 de l'ADR 0013.
- **Réponse** : _(à remplir)_

### UR-010 — Lot C : conversion et bornes par type de salon (`test-manuel`)

- **Statut** : Ouvert
- **Origine** : M1-ADR-13 ; ADR 0013 § 1, § 5 ; roadmap `PAR-REST-02` (complète UR-004)
- **Concerne** : `PATCH /channels/{channel_id}`, `POST /guilds/{guild_id}/channels`, `POST /channels/{channel_id}/messages`, `30013`, `30052`, `40058`, `50035`
- **Hypothèse** : conversion 0 → 5 remet `rate_limit_per_user` à 0 et s'applique avant les
  autres champs ; sans `NEWS`, Discord refuse (le fake accepte) ; `topic` > 1024 hors forum ⇒
  `50035` `BASE_TYPE_BAD_LENGTH` ; `user_limit` 100 ⇒ `50035` `NUMBER_TYPE_MAX` ; bitrate trop
  haut ⇒ 400 `30052` ; message dans un salon media ⇒ `40058` ; bitrate par défaut 64000 ;
  `default_thread_rate_limit_per_user` présent et modifiable sur un salon d'annonces (docs
  contradictoires : `channel.mdx:442` vs `guild.mdx:928`).
- **Procédure** :
  1. Guilde Community : `PATCH` d'un salon texte en slowmode 30 avec `{"type":5}` ; copier le JSON. Même appel en guilde non Community.
  2. `{"topic":"<1025 caractères>"}` sur un salon texte, puis sur un forum.
  3. Salon vocal : `{"user_limit":100}`, `{"bitrate":500000}` ; créer un salon vocal sans `bitrate` et lire sa valeur.
  4. `POST` d'un message dans un salon media.
  5. `GET` d'un salon d'annonces : `default_thread_rate_limit_per_user` présent ? puis `PATCH` de ce champ.
  6. (ajout M1-REST-02B1) Champs sans objet pour le type : `PATCH` d'un salon d'annonces
     `{"rate_limit_per_user":5}`, d'un salon vocal `{"topic":"x"}`, d'une catégorie
     `{"nsfw":true}` ; noter statut, JSON et dispatchs.
  7. (ajout M1-REST-02B2) `null` et refus non couverts : sur un salon vocal `{"bitrate":null}`,
     `{"user_limit":null}`, `{"video_quality_mode":null}`, `{"rtc_region":null}` (après l'avoir
     fixé) ; sur un forum `{"default_sort_order":null}`, `{"default_forum_layout":null}`,
     `{"default_tag_setting":"x"}`, `{"available_tags":[{"name":"t"}]}`, `{"flags":2}` ; sur un
     salon texte `{"type":14}`, `{"default_thread_rate_limit_per_user":null}` ; sur un forum
     `{"type":16}`. Noter statut, JSON (le champ est-il remis à zéro, inchangé, effacé ?), code et
     message de champ, dispatchs.
- **Résultat attendu** : 1 ⇒ `type: 5`, slowmode 0 ou absent (noter le refus en guilde non
  Community) ; 2 ⇒ `50035` puis 200 ; 3 ⇒ `50035`, 400 `30052`, 64000 ; 4 ⇒ `40058` ; 5 ⇒ présent, 200 ;
  6 ⇒ 200, salon inchangé, aucun `CHANNEL_UPDATE` (tests
  `ModifyGuildChannelAsync_TopicAndNsfwOnACategoryOrAVoiceChannel_AreIgnoredWithoutAChange`,
  `UpdateChannel_FieldsTheTypeHasNoUseFor_AreIgnoredWithoutAChange`) ; 7 ⇒ hypothèses du fake :
  `null` laisse inchangés `bitrate`, `user_limit`, `video_quality_mode`, `default_forum_layout`,
  `default_thread_rate_limit_per_user` ; `null` efface `rtc_region` (automatique) et
  `default_sort_order` ; `default_tag_setting` invalide ⇒ `50035` `BASE_TYPE_CHOICES` ; tags et
  `flags` acceptés sans effet visible ; `{"type":14}` et `{"type":16}` ⇒ `50035` `type`
  `CHANNEL_TYPE_INVALID` (tests `Rest/Channels/ModifyChannelTypeTests.cs`,
  `State/ChannelUpdateStateTests.cs`).
- **Si différent** : corriger `ChannelTraits`, les refus d'état, `errors.md` ; le résultat de 1
  en guilde non Community fixe le contenu de M4-REST-10.
- **Réponse** : _(à remplir)_

### UR-011 — Messages : codes, statuts et pagination (`test-manuel`)

- **Statut** : Ouvert
- **Origine** : M1-REST-03 (`3aea9b6`…`18f71be`), M1-REST-03B (`3bec8aa`) ; roadmap `PAR-ERR-04`
- **Concerne** : `rest.md` `/channels/{channel_id}/messages…` (7 lignes) ; `errors.md` `50003`, `50005`, `50006`, `50008`, `50024`, `50034`, `50035`, `40033`, `40058` ; tests `tests/Enx.NetCord.Testing.Tests/Rest/Messages/*` (`SendRequest_InvalidField_*`, `SendRequest_InvalidEmbed_*`, `SendRequest_BulkDeleteWithInvalidMessages_*`, `MessageChannelTypeTests`)
- **Hypothèse** :
  - codes de champ `50035` : `MESSAGE_REFERENCE_UNKNOWN_MESSAGE` (réponse à un message
    inexistant), `MAX_EMBED_SIZE_EXCEEDED`, `MESSAGE_ALLOWED_MENTIONS_PARSE_EXCLUSIVE`,
    `BASE_TYPE_ARRAY_UNIQUE` (doublons du bulk-delete), `URL_TYPE_INVALID_URL`,
    `BASE_TYPE_CHOICES` (`allowed_mentions.parse.N`, `message_reference.type`) ;
  - statuts : `50003` ⇒ 403, `50005` ⇒ 403, `50024` ⇒ 400 ;
  - `50006` (message vide) et `50035` passent avant `10003`/`50001` ; `50006` aussi quand une
    édition viderait le message ;
  - en DM, supprimer le message de l'autre partie ⇒ 403 `50003` ;
  - `around` : `limit / 2` messages plus anciens, puis la cible et les suivants ; plusieurs
    curseurs ⇒ `around`, puis `before`, puis `after`, sans erreur ;
  - texte de `50034` repris du message observé de Discord (pas de la table des docs) ; un
    `nonce` numérique est renvoyé en chaîne ;
  - `attachments[].id` à l'édition : un `id` qui est un index `files[n]` désigne un fichier
    téléversé, sinon une pièce existante à conserver.
  - (ajout M1-REST-03B) statut 400 de `50008` (message dans une catégorie), `40058` (forum,
    et aussi salon media : les docs ne citent que le forum) et `40033` (second crosspost) ;
    `GET …/messages` et `bulk-delete` d'une catégorie, d'un forum ou d'un media ⇒ 400 `50024`
    (Discord pourrait rendre `[]` ou `10008`) ; `GET`/`PATCH`/`DELETE`/`crosspost` d'un message
    dans ces salons ⇒ 404 `10008`.
- **Procédure** (G1, `$CH1`, un DM du bot avec un utilisateur qui y a écrit) :
  1. `POST /channels/$CH1/messages` avec chaque corps invalide (réponse à `"message_id":"1"`,
     embed de plus de 6000 caractères, `allowed_mentions` `{"parse":["users"],"users":["1"]}`,
     `parse: ["x"]`, URL d'embed `"x"`) ; relever `errors.<chemin>._errors[0].code`.
  2. `POST …/messages/bulk-delete` avec deux ID identiques ; avec un message de plus de 14 jours
     (noter le texte) ; dans le DM (noter statut et code).
  3. `POST …/messages/{id}/crosspost` sur un message de `$CH1` (salon texte).
  4. `PATCH` du contenu d'un message d'un autre utilisateur ; `PATCH {"content":""}` sur un
     message sans embed ; `PATCH` avec `attachments: [{"id":"<id existant>"}]`.
  5. `POST {}` sur `/channels/1/messages` (inexistant).
  6. `DELETE` du message de l'utilisateur dans le DM.
  7. 7 messages, puis `GET …/messages?around=<4ᵉ>&limit=5` ; puis `?before=A&after=B`.
  8. `POST` avec `"nonce": 123`.
  9. (ajout M1-REST-03B, avec une catégorie `$CAT`, un forum `$FORUM`, un salon d'annonces
     `$NEWS`, un salon media si la guilde est Community) `POST /channels/$CAT/messages`
     `{"content":"x"}`, puis sur `$FORUM` et le media ; `GET /channels/$CAT/messages` puis
     `$FORUM` ; `POST /channels/$CAT/messages/bulk-delete` avec deux ID récents ; deux
     `POST …/crosspost` de suite sur un message de `$NEWS` ; `GET /channels/$CAT/messages/1`.
- **Résultat attendu** : 1-2 ⇒ les codes ci-dessus, 400 `50034` et 403 `50003` en DM ; 3 ⇒
  400 `50024` ; 4 ⇒ 403 `50005`, 400 `50006`, pièce conservée ; 5 ⇒ 400 `50006` ; 6 ⇒ 403
  `50003` ; 7 ⇒ 2 plus anciens + cible + 2 suivants, puis réponse 200 ; 8 ⇒ `"nonce":"123"` ;
  9 ⇒ 400 `50008`, 400 `40058` (×2), 400 `50024` (×2), 400 `50024`, 200 puis 400 `40033`,
  404 `10008`.
- **Si différent** : corriger `DiscordError`, `Routes/Messages/MessageValidators.cs`,
  `EmbedValidation.cs`, `FakeDiscordState.Messages.cs`, `errors.md`, `rest.md` et les tests.
- **Réponse** : _(à remplir)_

### UR-012 — Erratum E-001 du contrat OpenAPI (`décision`)

- **Statut** : Ouvert
- **Origine** : M1-T-01 (merge `01e6f35`) ; `tests/Enx.NetCord.Testing.Conformance/Contract/extract-openapi-contract.py` (`apply_errata`) ; roadmap `PAR-SPEC-01`
- **Concerne** : `errors.md` `50035` ; `ChannelsConformanceTests`, `ContractInfrastructureTests`
- **Décision** : la spec OpenAPI type `code` des erreurs de champ du `50035` en `integer`
  (schéma générique `Error`, `openapi.json:43688`) ; les docs officielles
  (`developers/reference.mdx:44-106`) et le vrai Discord renvoient une chaîne
  (`BASE_TYPE_REQUIRED`). L'extrait commité substitue un schéma `FieldError` à code chaîne ; le
  script échoue si la spec change de forme, pour retirer l'erratum le moment venu.
- **Action possible** : signaler l'écart en amont (issue `discord/discord-api-spec`) — action
  externe, laissée à ton initiative.
- **Réponse** : _(à remplir)_

### UR-014 — Réactions : codes et règles d'emoji (`test-manuel`)

- **Statut** : Ouvert
- **Origine** : M1-REST-04 (`912b344`) ; roadmap `PAR-ERR-05`
- **Concerne** : `rest.md` `PUT`/`DELETE …/reactions/{emoji_name}/@me`, `DELETE …/reactions/{emoji_name}/{user_id}`, `GET`/`DELETE …/reactions/{emoji_name}`, `DELETE …/reactions` ; `errors.md` `10014`, `30010`, `50035` ; tests `AddReactionTests`, `RemoveReactionTests`, `ClearReactionsTests`, `ListReactionsTests`
- **Hypothèse** :
  1. `DELETE …/@me`, `…/{user_id}` et `…/{emoji_name}` d'une réaction absente ⇒ 404 `10014` (fauxcord : 204) ; `DELETE …/reactions` sans réaction ⇒ 204.
  2. `GET …/{emoji_name}` d'un emoji que personne n'a utilisé ⇒ `[]`.
  3. Tout emoji personnalisé `name:id` bien formé est accepté, même sans guilde qui le porte (emojis de guilde en M4-REST-04) ; l'alternative serait `10014`.
  4. 21ᵉ emoji distinct ⇒ 400 `30010` ; un emoji déjà présent reste accepté.
  5. `type` hors `{0,1}` ⇒ `50035` `BASE_TYPE_CHOICES`.
  6. « Emoji unicode » = au moins un caractère non ASCII, sans lettre ASCII ni blanc : `é` ou `日本` sont acceptés ; `❤` et `❤️` (sélecteur de variante) comptent comme deux emojis.
  7. Ordre : `50035` d'ID, puis `10014` (emoji mal formé), puis `10003`/`50001`/`10008`.
- **Procédure** (G1, `$CH1`, un message `$M` du bot) :
  1. `DELETE /channels/$CH1/messages/$M/reactions/%F0%9F%91%8D/@me` sans avoir réagi ; idem `…/{user_id}` et `…/reactions/%F0%9F%91%8D` ; puis `DELETE …/reactions`.
  2. `GET …/reactions/%F0%9F%94%A5`.
  3. `PUT …/reactions/wave:1234/@me` (emoji inexistant).
  4. Poser 21 emojis distincts, puis reposer le 1ᵉʳ.
  5. `GET …/reactions/%F0%9F%91%8D?type=2`.
  6. `PUT …/reactions/%C3%A9/@me` (`é`), puis `❤` puis `❤️` ; `GET` du message : une ou deux réactions ?
  7. `PUT /channels/1/messages/$M/reactions/x/@me` (salon inexistant, emoji mal formé).
- **Résultat attendu** : 1 ⇒ 404 `10014` ×3, puis 204 ; 2 ⇒ `[]` ; 3 ⇒ 204 ; 4 ⇒ 400 `30010`, puis 204 ; 5 ⇒ 400 `50035` ; 6 ⇒ 204, deux réactions ; 7 ⇒ 400 `10014`.
- **Si différent** : corriger `State/Reactions.cs` (parseur), `FakeDiscordState.Reactions.cs`, `ReactionValidators.cs`, `errors.md`, `rest.md` et les tests.
- **Réponse** : _(à remplir)_

### UR-015 — Seed des réactions (`décision`)

- **Statut** : Ouvert
- **Origine** : M1-REST-04
- **Concerne** : API publique `SeededTextChannel.Reaction(messageId, emoji, userId, burst = false)` (et `SeededVoiceChannel.Reaction`)
- **Décision** : une méthode sur la poignée de salon, à côté de `Message`, pour semer la réaction d'un autre utilisateur (seul moyen de tester `DELETE …/{user_id}` et le listing) ; `burst: true` sème une super réaction, que le `PUT` du bot ne crée jamais (la spec n'a pas de `type` sur le `PUT`). Alternative écartée : une poignée de message (`channel.Message(...)` rendrait un `SeededMessage` portant `Reaction`), qui changerait le type de retour de `Message` (un ID aujourd'hui).
- **Réponse** : _(à remplir)_

### UR-016 — Réglages de guilde : salons désignés et champs synthétiques (`test-manuel`)

- **Statut** : Ouvert
- **Origine** : M1-REST-06 (`06be6cb`)
- **Concerne** : `rest.md` `GET`/`PATCH /guilds/{guild_id}`, `DELETE /channels/{channel_id}` ; `errors.md` `50035` ; `gateway.md` `GUILD_UPDATE`, `CHANNEL_DELETE` ; tests `tests/Enx.NetCord.Testing.Tests/Rest/Guilds/*`, `State/GuildStateTests.cs`
- **Hypothèse** :
  1. Un réglage qui désigne un salon (`afk_channel_id`, `system_channel_id`, `rules_channel_id`,
     `public_updates_channel_id`, `safety_alerts_channel_id`) exige un salon de la guilde du bon
     type, sinon 400 `50035` avec un code de champ **inventé** `GUILD_CHANNEL_INVALID` ; un seul
     refus est rendu.
  2. Supprimer le salon désigné efface le réglage dans le même commit : `CHANNEL_DELETE` puis
     `GUILD_UPDATE`.
  3. `owner_id` d'une guilde sans propriétaire dans l'état = ID du bot ; `region` = `"deprecated"` ;
     `approximate_presence_count` = 0 ; limites d'une guilde sans boost.
- **Procédure** (G1, bot `MANAGE_GUILD`, connecté avec l'intent `GUILDS`) :
  1. `PATCH /guilds/$G1` `{"afk_channel_id":"$CH1"}` (salon texte), puis
     `{"system_channel_id":"<salon vocal>"}`, puis les deux à la fois.
  2. `PATCH {"system_channel_id":"$CH1"}`, supprimer `$CH1`, relire la guilde ; noter les dispatchs et leur ordre.
  3. `GET /guilds/$G1?with_counts=true` : copier `owner_id`, `region`, `approximate_*`.
- **Résultat attendu** : 1 ⇒ 400 `50035`, noter le code et le message de champ (et si les deux
  champs sont signalés ensemble) ; 2 ⇒ `system_channel_id: null`, `CHANNEL_DELETE` puis
  `GUILD_UPDATE` ; 3 ⇒ comparer au JSON du fake.
- **Si différent** : corriger `GuildValidators.cs`, `FakeDiscordState.Guilds.cs`/`Channels.cs`,
  `GuildWire.cs`, `errors.md`, `rest.md` et les tests.
- **Réponse** : _(à remplir)_

### UR-017 — Pins et typing : idempotence, ordre des faits, codes (`test-manuel`)

- **Statut** : Ouvert
- **Origine** : M1-REST-05 (`6bf22af`, `81cafda`, merge `eac5253`)
- **Concerne** : `rest.md` `GET …/messages/pins`, `PUT`/`DELETE …/messages/pins/{message_id}`, `POST /channels/{channel_id}/typing` ; `errors.md` `30003`, `50021`, `50024`, `50035` ; `gateway.md` `CHANNEL_PINS_UPDATE`, `MESSAGE_UPDATE`, `MESSAGE_CREATE` ; tests `tests/Enx.NetCord.Testing.Tests/Rest/Pins/*`, `Rest/Typing/TriggerTypingTests.cs`
- **Hypothèse** :
  1. Épingler un message déjà épinglé, ou dépingler un message non épinglé ⇒ 204 sans fait (fauxcord).
  2. Ordre des faits au pin : `MESSAGE_UPDATE` (`pinned: true`), `CHANNEL_PINS_UPDATE`, puis
     `MESSAGE_CREATE` d'un message système type 6 (auteur : le bot, `message_reference` vers le
     message) ; au dépin : `MESSAGE_UPDATE` puis `CHANNEL_PINS_UPDATE`, le message système reste ;
     `last_pin_timestamp` recalculé, `null` quand le dernier pin est retiré. Supprimer un message
     épinglé le retire des pins sans `CHANNEL_PINS_UPDATE` (docs).
  3. `30003` (251ᵉ pin) ⇒ statut 400 ; `50021` (pin d'un message système, tout type autre que 0
     et 19) ⇒ statut 400.
  4. `GET …/messages/pins` et `POST …/typing` sur une catégorie ou un forum ⇒ 400 `50024`
     (cohérent avec UR-011) ; `PUT`/`DELETE pins/{id}` ⇒ 404 `10008`.
  5. `before` illisible ⇒ `50035`, code de champ `DATE_TYPE_PARSE`.
- **Procédure** (G1, `$CH1`, bot connecté avec `GUILDS` + `GUILD_MESSAGES`, qui journalise les dispatchs bruts) :
  1. `PUT /channels/$CH1/messages/pins/$M` deux fois ; `DELETE …/pins/<message jamais épinglé>`.
  2. Épingler `$M`, noter les dispatchs et leur ordre ; dépingler le seul pin, relire `GET /channels/$CH1` (`last_pin_timestamp`).
  3. Épingler 251 messages ; épingler le message « a épinglé un message » (type 6), puis un message de commande slash (type 20).
  4. `GET /channels/$CAT/messages/pins`, `POST /channels/$CAT/typing`, idem sur `$FORUM`.
  5. `GET /channels/$CH1/messages/pins?before=hier` : relever `errors.before._errors[0].code`.
- **Résultat attendu** : 1 ⇒ 204 ×3, un seul message système ; 2 ⇒ l'ordre ci-dessus,
  `last_pin_timestamp: null` ; 3 ⇒ 400 `30003`, 400 `50021`, et noter si le type 20 est accepté ;
  4 ⇒ 400 `50024` ×4 ; 5 ⇒ `DATE_TYPE_PARSE`.
- **Si différent** : corriger `FakeDiscordState.Pins.cs`, `PinValidators.cs`, `DiscordError`,
  `errors.md`, `rest.md` et les tests ; l'ordre des faits fixe la traduction de M3-03.
- **Réponse** : _(à remplir)_

### UR-018 — `pinned_at` à la seconde, pas de seed public de pin (`décision`)

- **Statut** : Ouvert
- **Origine** : M1-REST-05 ; roadmap `M1-REST-05C`
- **Concerne** : `rest.md` `GET …/messages/pins` ; `State/FakeDiscordState.Pins.cs`
- **Décision** :
  1. `pinned_at` est tronqué à la seconde et strictement croissant par salon (un pin prend
     `max(horloge, dernier pin + 1 s)`) : NetCord écrit le curseur `before` avec `ToString("s")`
     (`RestClient.Channel.cs:705`), et avec l'horloge virtuelle figée, deux pins de la même seconde
     seraient perdus à la page suivante. Conséquence : deux pins rapprochés ont des `pinned_at`
     qui ne reflètent pas l'horloge exacte.
  2. Pas d'API de seed publique pour épingler un message : les tests internes passent par l'état.
     Un `Pinned` sur le seed de message est proposé en M1-REST-05C (à rattacher à UR-015).
- **Réponse** : _(à remplir)_

### UR-019 — Membres de guilde : codes, statuts et ordre (`test-manuel`)

- **Statut** : Ouvert
- **Origine** : M1-REST-07 (`2790ada`, `762b99f`, `73b256b`)
- **Concerne** : `rest.md` `/guilds/{guild_id}/members…` (9 lignes) ; `errors.md` `10007`, `10011`, `10013`, `50013`, `50025`, `50028`, `50035` ; tests `tests/Enx.NetCord.Testing.Tests/Rest/Members/*`, `Conformance/Rest/MembersConformanceTests.cs`
- **Hypothèse** :
  1. `GET`/`PATCH`/`DELETE …/members/{user_id}` : utilisateur inexistant ⇒ 404 `10013`,
     utilisateur existant non membre ⇒ 404 `10007`.
  2. Ordre : guilde (`10004`/`50001`) → membre (`10007`) → `@everyone` (`50028`) → rôle (`10011`) ;
     rôle inconnu dans `roles` d'un `PATCH` ⇒ 404 `10011` (pas `50035`), rien n'est modifié.
  3. `PUT`/`DELETE …/roles/{guild_id}` (`@everyone`) ⇒ 400 `50028` ; `@everyone` dans `roles`
     d'un `PATCH` ignoré.
  4. Expulser le bot lui-même ou le propriétaire ⇒ 403 `50013` (souvenir de discord.py, non documenté).
  5. `PUT …/members/{user_id}` (OAuth2) ⇒ toujours 403 `50025` après `10004`/`50001` (pas de grants
     modélisés), sauf utilisateur déjà membre ⇒ 204 sans fait (`guild.mdx:1048`), vérifié
     **avant** le jeton (ordre supposé).
  6. Timeout au-delà de 28 jours ⇒ `50035` avec un code de champ **inventé**
     `GUILD_MEMBER_TIMEOUT_TOO_LONG` ; date illisible (non ISO 8601, ex. `12/25/2026`) ⇒
     `DATE_TYPE_PARSE` (code partagé avec le `before` des pins, UR-017).
  7. `mute`/`deaf`/`channel_id` acceptés hors vocal (Discord : 400, `40032` ?), faute de voice states.
  8. `search` trié par ID d'utilisateur croissant (Discord : pertinence, non documenté).
- **Procédure** (G1, bot avec `MANAGE_ROLES`, `KICK_MEMBERS`, `MODERATE_MEMBERS`, `MANAGE_NICKNAMES`) :
  1. `GET /guilds/$G1/members/1` (inexistant), puis `GET /guilds/$G1/members/<utilisateur réel non membre>`.
  2. `PATCH /guilds/$G1/members/$USER {"roles":["424242"]}` ; `PUT /guilds/$G1/members/<non membre>/roles/424242`.
  3. `PUT /guilds/$G1/members/$USER/roles/$G1` ; `PATCH {"roles":["$G1"]}` puis relire le membre.
  4. `DELETE /guilds/$G1/members/$APP`, puis sur le propriétaire.
  5. `PUT /guilds/$G1/members/$USER {"access_token":"x"}` (membre existant), puis sur un non-membre.
  6. `PATCH {"communication_disabled_until":"<now+29 j>"}`, puis `"tomorrow"` : relever l'arbre `errors`.
  7. `PATCH {"mute":true}` sur un membre hors vocal.
  8. `GET /guilds/$G1/members/search?query=a&limit=10` avec plusieurs membres en `a…`.
- **Résultat attendu** : 1 ⇒ `10013` puis `10007` ; 2 ⇒ 404 `10011`, puis 404 `10007` ; 3 ⇒
  400 `50028`, membre inchangé ; 4 ⇒ 403 `50013` ×2 ; 5 ⇒ 204, puis 403 `50025` ; 6 ⇒ noter codes
  et messages ; 7 ⇒ noter statut et code ; 8 ⇒ noter l'ordre.
- **Si différent** : corriger `FakeDiscordState.Members.cs`, `MemberValidators.cs`, `DiscordError`,
  `errors.md`, `rest.md` et les tests.
- **Réponse** : _(à remplir)_

### UR-020 — Seed des salons privés : `DMChannel`, `GroupDMChannel`, `SeededDMChannel` (`décision`)

- **Statut** : Ouvert
- **Origine** : M1-ADR-04C, [ADR 0014 § 3](adr/0014-private-channels-dm-and-group-dm.md) (`e2e9334`, `caac255`) ; implémentation M1-REST-02C1
- **Concerne** : API publique `DiscordSeed.DMChannel(userId, id)`, `DiscordSeed.GroupDMChannel(recipients, name, id)`, `SeededDMChannel` ; complète UR-015 (absorbe M1-REST-04B)
- **Décision** :
  1. Noms alignés sur les types NetCord (`DMChannel`, `GroupDMChannel`) plutôt que `Dm`/`GroupDm`.
  2. Une seule poignée `SeededDMChannel` (`Id`, `Message`, `Reaction`) pour le DM et le group DM,
     comme `SeededTextChannel` sert aussi aux annonces.
  3. `Seed.DMChannel` ouvre ou retrouve le DM par la même opération d'état que
     `POST /users/@me/channels` (refus du bot lui-même : `ArgumentException`).
  4. `recipients` en premier paramètre (`IEnumerable<ulong>`, donc `[alice, bob]`), `name`
     facultatif ; le bot est toujours propriétaire : ni `withBot: false`, ni icône, ni ajout de
     destinataire dans le seed.
  5. `Message` et `Reaction` d'un salon privé refusent un utilisateur non participant
     (`InvalidOperationException`), DM 1:1 compris ; l'état applique la même règle à tout appelant.
- **Réponse** : _(à remplir)_

### UR-021 — Group DM : périmètre sans OAuth2 et départ du bot (`décision`)

- **Statut** : Ouvert
- **Origine** : M1-ADR-04C, [ADR 0014 § 4](adr/0014-private-channels-dm-and-group-dm.md)
- **Concerne** : `rest.md` `POST /users/@me/channels`, `PUT /channels/{channel_id}/recipients/{user_id}`, `DELETE /channels/{channel_id}`
- **Décision** :
  1. Aucun grant OAuth2 `gdm.join` simulé : `CreateGroupDMChannelAsync` et
     `GroupDMChannelAddUserAsync` échouent toujours en 403 `50025` ; la ligne du `PUT` restera 🟡.
     Des jetons seedés sont écartés comme non-objectif v1 (`vision.md`).
  2. Quand le bot quitte un group DM (`DELETE /channels/{id}` ou `DELETE …/recipients/{bot}`), le
     salon sort de l'état avec ses messages : 404 `10003` ensuite, pas de retour possible.
     Alternative écartée : garder le salon avec un drapeau et rendre 403 `50001`.
- **Réponse** : _(à remplir)_

### UR-022 — Group DM : comportement réel de Discord, lot D (`test-manuel`)

- **Statut** : Ouvert
- **Origine** : M1-ADR-04C, [ADR 0014, lot D](adr/0014-private-channels-dm-and-group-dm.md) ; roadmap `PAR-REST-05`
- **Concerne** : `rest.md` `GET`/`PATCH`/`DELETE /channels/{channel_id}`, `PUT`/`DELETE /channels/{channel_id}/recipients/{user_id}` ; `errors.md` `10003`, `10013`, `50024`, `50025`, `50035`, `50109` ; `gateway.md` `CHANNEL_CREATE`, `CHANNEL_UPDATE`, `CHANNEL_DELETE`
- **Hypothèse** :
  - objet : `owner_id` = le bot, `managed: true`, `application_id` = l'application, `recipients` sans le bot ;
  - limites : 10 participants bot compris, 10 groupes actifs ;
  - `PATCH` : `name: ""` efface, `name: null` ne change rien, `icon: null` retire l'icône, icône
    invalide ⇒ `50035` `IMAGE_INVALID` sur un group DM, ignorée sur tout autre salon ;
  - `DELETE` du salon : le bot quitte, 200 avec le salon, puis 404 `10003` ;
  - destinataires : `PUT` sans corps ⇒ `50109` ; ordre structurel → accès → `50024` (hors group
    DM) → `10013` → `50025` ; `DELETE` d'un non-destinataire ⇒ 204 ; `DELETE` du bot = départ ;
  - Gateway : `CHANNEL_CREATE`/`CHANNEL_UPDATE`/`CHANNEL_DELETE` d'un group DM sous l'intent
    `GUILDS` (lecture des docs, `gateway.mdx:320-322` ; alternative : `DIRECT_MESSAGES`), rien au
    retrait d'un destinataire.
- **Procédure** (coûteuse : application de test avec URI de redirection, un ou deux comptes
  utilisateur qui accordent `gdm.join`, échange du code contre un jeton ; bot connecté qui
  journalise les dispatchs bruts) :
  1. `POST /users/@me/channels` `{"access_tokens":["<jeton>"],"nicks":{}}` : copier le JSON, noter les dispatchs.
  2. `PATCH /channels/$GDM` avec `{"name":"x"}`, `{"name":""}`, `{"name":null}`, `{"icon":null}`,
     `{"icon":"data:image/png;base64,xx"}`, `{"topic":"x"}` ; puis `{"icon":"data:image/png;base64,xx"}` sur `$CH1`.
  3. `PUT /channels/$GDM/recipients/$USER2` avec `{"access_token":"x"}`, puis sans corps, puis
     `{}`, puis avec un `user_id` inexistant ; même `PUT` sur `$CH1` et sur un DM 1:1.
  4. `DELETE /channels/$GDM/recipients/$USER`, deux fois ; noter les dispatchs.
  5. Dans un autre groupe : `DELETE /channels/$GDM/recipients/$APP`.
  6. `DELETE /channels/$GDM`, puis `GET /channels/$GDM` ; noter les dispatchs.
  7. Refaire 1 et 6 avec le seul intent `GUILDS`, puis le seul `DIRECT_MESSAGES`.
- **Résultat attendu** : 1 ⇒ `type: 3`, `owner_id` = `$APP`, `managed: true`, `CHANNEL_CREATE` ;
  2 ⇒ nom posé, effacé, inchangé, icône retirée, 400 `50035` `IMAGE_INVALID`, `topic` ignoré, un
  `CHANNEL_UPDATE` par changement, puis 200 sur `$CH1` sans changement ; 3 ⇒ 403 `50025`, 400
  `50109`, noter `{}`, 404 `10013`, 400 `50024` ×2 ; 4 ⇒ 204 ×2, aucun dispatch ; 5 ⇒ 204 et
  `CHANNEL_DELETE` ; 6 ⇒ 200, `CHANNEL_DELETE`, puis 404 `10003` ; 7 ⇒ dispatchs reçus avec
  `GUILDS`, pas avec `DIRECT_MESSAGES` seul.
- **Si différent** : corriger l'ADR 0014, `FakeDiscordState.Channels.cs`, les routes de
  M1-REST-02C2, `errors.md`, `rest.md` et les tests ; si 6 rend 403 `50001`, garder une liste des
  groupes quittés ; si l'intent diffère, ajuster M3-03.
- **Réponse** : _(à remplir ; le point peut rester ouvert, les lignes restant 🟡)_

## Traités

### UR-013 — Garde « un scénario de conformité par handler », sans exemption (`décision`)

- **Statut** : Traité (2026-11-28)
- **Origine** : M1-T-01 ; `ContractInfrastructureTests.ConformanceTests_EveryImplementedOperation_HasAScenarioTaggedWithItsKey` ; skill `dotnet-test-conventions` § Conformité
- **Concerne** : toutes les futures tâches REST/interactions
- **Décision** : tout handler REST enregistré doit avoir au moins un test non ignoré
  `[Parity("METHOD /path")]` dans le projet de conformité, terminé par `ContractAssert.Conforms` ;
  sinon `dotnet test` échoue. Pas de liste d'exemptions. L'extrait couvre toutes les opérations
  de la spec : ajouter un handler n'exige jamais de régénérer le contrat. Depuis M1-T-04 (merge
  `3b13a29`), le contrat est strict : les propriétés non déclarées par la spec sont rejetées.
- **Réponse** : solution proposée validée (en session, 2026-11-28) : garde stricte, sans liste
  d'exemptions, et texte du point mis à jour pour M1-T-04. Aucune tâche à créer.

### UR-006 — API de seed des salons (`décision`)

- **Statut** : Traité (2026-11-28)
- **Origine** : M1-ADR-13 ; [ADR 0013 § 6](adr/0013-channel-hierarchy-and-types.md)
- **Concerne** : API publique `SeededGuild`, `SeededCategory`, `SeededVoiceChannel`, `SeededForumChannel`, `SeededChannelOptions` (implémentation M1-REST-02B1)
- **Décision** : tout salon seedé rend une poignée, jamais un ID nu. Le parent se lit dans
  l'appel (`category.TextChannel(...)`). Une seule classe d'options pour tous les types, qui
  lève `ArgumentException` sur une option sans objet pour le type si sa valeur diffère du défaut
  (ex. `Topic` sur un salon vocal). Pas de réglages vocaux (`bitrate`, `user_limit`) dans le
  seed ; media et annuaire exclus. `Slowmode` y est (absorbe M1-REST-02D).
- **Réponse** : « ok pour la décision actuelle » (en session, 2026-11-28). ADR 0013 § 6 inchangé ;
  M1-REST-02B1 l'implémente tel quel.

### UR-007 — Périmètre des types de salons et des contrôles (`décision`)

- **Statut** : Traité (2026-11-28)
- **Origine** : M1-ADR-13 ; ADR 0013 § 1, § 4, § 5
- **Concerne** : `rest.md` `/channels/{channel_id}`, `/guilds/{guild_id}/channels` ; tâche M4-REST-10
- **Décision** :
  1. Le type 16 (media) est modélisé alors que la spec OpenAPI ne le liste pas (docs et NetCord oui).
  2. Tags de forum reportés à M4-REST-01 ; d'ici là validés puis ignorés.
  3. Aucune guilde n'exige `NEWS` ou Community pour un salon d'annonces ou stage (contrôles en M4-REST-10).
  4. Le listing d'une guilde est rendu par ID, pas par position.
  5. Les limites Discord (50 salons par catégorie, 500 par guilde) s'appliquent aussi au seed.
- **Réponse** : validé (en session, 2026-11-28) : un contrôle absent ou plus permissif que
  Discord parce qu'il dépend d'une fonctionnalité pas encore modélisée (`features`, tags de
  forum) est acceptable. Conséquences : les lignes concernées restent 🟡 avec la note du
  contrôle manquant ; M4-REST-10 et M4-REST-01 ajoutent les contrôles et adaptent les tests qui
  reposaient sur le comportement permissif.
