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
- **Résultat attendu** : 1 ⇒ 404 `10004` ; 2-4 ⇒ 403 `50001` (guilde inexistante en 4 ⇒ 404
  `10004`) ; 5 ⇒ 404 `10004` ; 6 ⇒ 400 `50035` ; 7 ⇒ followup accepté.
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
  `10013` (fauxcord) ; DM vers soi-même ou un autre bot ⇒ `50007` ; avatar/banner WebP accepté ;
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

## Traités

_(aucun)_
