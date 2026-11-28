# Parité erreurs

<!-- Baseline 2026-11-25 : discord-api-docs@62ab2d50 (developers/topics/opcodes-and-status-codes.mdx §HTTP Response Codes, §JSON Error Codes ; developers/reference.mdx §Error Messages). Seuls les codes que le fake doit produire sont listés ; l'auditeur ajoute une ligne dès qu'un handler en produit un nouveau. -->

Forme d'erreur Discord (à reproduire exactement, via `DiscordError`) :

```json
{ "code": 50035, "message": "Invalid Form Body",
  "errors": { "content": { "_errors": [ { "code": "BASE_TYPE_MAX_LENGTH", "message": "Must be 2000 or fewer in length." } ] } } }
```

Rate limit (429) : corps `{ "message": "You are being rate limited.", "retry_after": 1.5, "global": false, "code"?: … }`
(Discord renvoie un flottant ; le 429 injecté par `Intercept` écrit un **entier**, ADR 0005 — les flottants relèvent de M6-02),
accompagné des en-têtes `Retry-After`, `X-RateLimit-*` et `X-RateLimit-Scope`.

Progression `Fake` : ✅ **6** · 🟡 **11** · ⬜ **26** · ⛔ **0**

## Statuts HTTP

| Clé | Statut | Quand le fake le renvoie | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- |
| `http:200` | 200 OK | succès avec corps | ⬜ | ⬜ | |
| `http:201` | 201 Created | création quand Discord le fait (ex. PUT ban ? vérifier spec) | ⬜ | ⬜ | suivre les `responses` de la spec par opération |
| `http:204` | 204 No Content | DELETE, PUT réaction/pin, typing… | ⬜ | ⬜ | |
| `http:400` | 400 Bad Request | corps invalide (avec `50035`) | 🟡 | ✅ | `DiscordError.InvalidFormBody`/`InvalidJson` testés via le vrai `RestClient` avec des handlers factices (M1-03) ; reste à les produire depuis les vrais handlers (M1-REST) |
| `http:401` | 401 Unauthorized | token absent/inconnu | ✅ | ✅ | `{"message":"401: Unauthorized","code":0}` ; absent, inconnu ou `Bearer <token bot>` ; une opération `{}` accepte l'anonyme **et** un token inconnu (le header est ignoré, `Caller.Kind == Unrecognized`). Source : fauxcord `src/middleware/auth.ts:109-118` (chemins exemptés, dont le callback d'interaction et les webhooks à token, sans contrôle de l'en-tête) ; le comportement réel de Discord pour un token invalide sur ces routes n'est **pas vérifié** (M1-03) |
| `http:403` | 403 Forbidden | permission manquante (`50013`) / accès (`50001`) | 🟡 | ✅ | produit via `Intercept`/handler factice, et par un vrai handler : `50025` sur `POST /users/@me/channels` (group DM, M1-REST-01) et `50001` sur `/channels/{channel_id}` (M1-REST-02) ; calcul de permissions : M4-REST-05 ; `50001` (accès à une guilde ou un salon que le bot ne voit pas) : fabrique `DiscordError.MissingAccess()` et helper d'état `GuildAccess`, testés avec un handler factice via le vrai `RestClient` (M1-05) |
| `http:404` | 404 Not Found | ressource inconnue (`10xxx`) ou route inconnue (`code 0`) | 🟡 | ✅ | route inconnue / non implémentée (Lenient) : produit par le pipeline, aussi testé via `FakeDiscord.CreateRestClient()` (M1-02) ; ressources `10xxx` : `10003` (M1-REST-02), `10013` (M1-REST-01) par de vrais handlers, les autres via handler factice (M1-REST) |
| `http:405` | 405 Method Not Allowed | méthode non supportée sur une route connue | ✅ | ✅ | en-tête `Allow` = méthodes inventoriées du chemin ; Lenient et Strict ; pas d'en-têtes de rate limit (aucune opération résolue) |
| `http:429` | 429 Too Many Requests | simulation de rate limit (opt-in) ou `Intercept` | 🟡 | ✅ | `Intercept(RestEffect.RateLimited(s))` : corps `{"message":"You are being rate limited.","retry_after":s,"global":false}`, scope `user`, sans `code` (exemple Discord) ; un 429 injecté ne prouve pas un quota : M6-02 |
| `http:5xx` | 500/502 | injecté par le test via `Intercept` | ✅ | ✅ | `RestEffect.Error` 500 ou 502 → corps `{"message":"500: Internal Server Error","code":0}` ; NetCord ne réessaie pas sur 5xx (`RestException`) |

## Codes JSON

| Code | Message Discord | Contexte typique | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- |
| `0` | General error / `404: Not Found` / `401: Unauthorized` | route inconnue, auth | ✅ | ✅ | 401, 404 (Lenient, aussi via la façade `FakeDiscord`) et 405 ; messages `NNN: Reason` comme Discord |
| `10003` | Unknown Channel | `/channels/{id}` | ✅ | ✅ | fabrique `DiscordError.UnknownChannel()` (404 même en Strict) produite par les vrais `GET`/`PATCH`/`DELETE /channels/{channel_id}` via le vrai `RestClient` (M1-REST-02) ; les sous-ressources d'un salon (messages…) la produisent à leur tour (M1-REST-03) |
| `10004` | Unknown Guild | `/guilds/{id}`, sous-ressources et commandes d'une guilde inexistante | 🟡 | ✅ | fabrique `DiscordError.UnknownGuild()` (404) produite par le helper d'état `GuildAccess` (M1-05, ADR 0004 amendement 04B) : guilde inexistante **ou** guilde du fake dont le bot n'est pas membre pour la guilde elle-même (profil « masquée », `GET /guilds/{id}` : discord-api-docs#5840) ; testée via le vrai `RestClient` avec un handler factice, et injectable par `RestEffect.Error(NotFound, 10004)` ; vrais handlers en M1-REST-06. Le choix `10004` plutôt que `50001` pour une guilde hors du bot est une hypothèse hors spec épinglée (PAR-ERR-02) |
| `10007` | Unknown Member | `/guilds/{id}/members/{user_id}` | ⬜ | ⬜ | |
| `10008` | Unknown Message | `/channels/{id}/messages/{id}` | 🟡 | ✅ | fabrique testée via un handler factice ; vrai handler en M1-REST-03 |
| `10011` | Unknown Role | rôles | ⬜ | ⬜ | |
| `10013` | Unknown User | `/users/{id}`, `recipient_id` d'un DM | ✅ | ✅ | `GET /users/{user_id}` et `POST /users/@me/channels` (M1-REST-01), via le vrai `RestClient` |
| `10014` | Unknown Emoji | réactions, emojis | ⬜ | ⬜ | |
| `10015` | Unknown Webhook | webhooks, followups avant callback | ⬜ | ⬜ | |
| `10026` | Unknown Ban | `GET/DELETE /guilds/{id}/bans/{user_id}` | ⬜ | ⬜ | |
| `10062` | Unknown interaction | callback hors délai / interaction inconnue | ⬜ | ⬜ | |
| `10065` | Unknown Voice State | voice states | ⬜ | ⬜ | |
| `20028` | Write rate limit on channel | simulation (opt-in) | ⬜ | ⬜ | |
| `30001` | Maximum number of guilds reached (100) | `POST /guilds`, rejoindre | ⬜ | ⬜ | |
| `30003` | Maximum number of pins reached (250) | pins | ⬜ | ⬜ | fauxcord utilise 50 (ancienne limite) — suivre la doc |
| `30005` | Maximum number of guild roles reached (250) | rôles | ⬜ | ⬜ | |
| `30007` | Maximum number of webhooks reached (15) | webhooks | ⬜ | ⬜ | |
| `30013` | Maximum number of guild channels reached (500) | salons | ⬜ | ⬜ | |
| `30033` | Maximum number of thread participants (1000) | threads | ⬜ | ⬜ | |
| `40060` | Interaction has already been acknowledged | 2e callback | ⬜ | ⬜ | |
| `50001` | Missing Access | guilde (sous-ressources, commandes) ou salon que le bot ne voit pas | 🟡 | ✅ | fabrique `DiscordError.MissingAccess()` (403) produite par `GuildAccess` quand la ressource existe mais que le bot n'est pas membre de la guilde (profil « refusée », M1-05, ADR 0004 amendement 04B) ; testée via le vrai `RestClient` avec un handler factice, injectable par `RestEffect.Error(Forbidden, 50001)` ; produite par les vrais `GET`/`PATCH`/`DELETE /channels/{channel_id}` pour un salon d'une guilde dont le bot n'est pas membre (M1-REST-02, rien n'est modifié ni supprimé) ; autres familles en M1-REST-03 à 09 et M2-07. Le choix de la famille de routes est **observé, non documenté** (PAR-ERR-02) ; le refus pour `VIEW_CHANNEL` viendra avec M4-REST-05 |
| `50006` | Cannot send an empty message | message sans contenu/embeds/fichiers/composants/sticker/poll | ⬜ | ⬜ | |
| `50007` | Cannot send messages to this user | DM refusé (option du seed) | ⬜ | ⬜ | |
| `50008` | Cannot send messages in a non-text channel | voix/catégorie | ⬜ | ⬜ | |
| `50013` | Missing Permissions | permission calculée insuffisante | 🟡 | ✅ | injecté via `Intercept(403, 50013)` : ne prouve pas un calcul de permission ; dépend de M4-REST-05 |
| `50024` | Cannot execute action on this channel type | ex. thread sur DM | ⬜ | ⬜ | |
| `50025` | Invalid OAuth2 access token provided | `POST /users/@me/channels` avec `access_tokens` (group DM) | 🟡 | ✅ | le fake n'a aucun grant OAuth2 : tout jeton est refusé. HTTP **403** : documenté pour `50025` dans `discord-social-sdk/development-guides/provisional-accounts/merging-accounts.mdx:347` (`external_auth_token` invalide), **non vérifié pour un group DM** (la page des codes JSON ne donne pas de statut) (M1-REST-01) |
| `50027` | Invalid Webhook Token | token d'interaction expiré | ⬜ | ⬜ | |
| `50035` | Invalid Form Body | validation (arbre `errors`) | 🟡 | ✅ | produit par les vrais validateurs des routes `/users` (M1-REST-01 : `BASE_TYPE_BAD_LENGTH`, `BASE_TYPE_BAD_TYPE`, `BASE_TYPE_REQUIRED`, `NUMBER_TYPE_COERCE`, `IMAGE_INVALID`, `MODEL_TYPE_CONVERT`) et `/channels/{channel_id}` (M1-REST-02 : `NUMBER_TYPE_MIN`/`NUMBER_TYPE_MAX` « int value should be greater than or equal to N. »/« …less than or equal to N. », `NUMBER_TYPE_COERCE` « Value "x" is not int. », `BOOLEAN_TYPE_COERCE` « Value "x" is not boolean. », `BASE_TYPE_CHOICES` « Value must be one of (0, 2, …). » : formes recoupées avec l'exemple de `reference.mdx` pour `BASE_TYPE_CHOICES`, les autres messages de mémoire du comportement de Discord, **non vérifiés** dans la doc épinglée) ; **hypothèses sans source** (aucune catégorie ni conversion de type dans l'état) : `CHANNEL_TYPE_INVALID` (`type` ≠ type courant) et `CHANNEL_PARENT_INVALID` (`parent_id` non nul) sur `PATCH /channels/{channel_id}`, décidés sous le verrou de l'état (`StateFailureKind.InvalidChannelType`/`InvalidChannelParent`/`InvalidChannelName`, traduits par `DiscordError.FromStateFailure`) ; ces refus d'état ne sont pas réunis avec les erreurs structurelles dans le même arbre `errors` : un seul est signalé (premier dans l'ordre type, parent, nom), alors que Discord réunit tout dans un seul `50035` ; ils passent après `Intercept` (ADR 0005) et sont journalisés comme réponse du handler ; un nom > 100 sur un DM reçoit le message de guilde « Must be between 1 and 100 in length. » ; arbre `errors` (objets par propriété/indice décimal, `_errors` à la feuille, pas d'`errors: null`) lu par NetCord ; testé aussi avec un validateur factice (un 50035 de validateur ne consomme pas de règle `Intercept`), autres familles en M1-REST ; codes de champ `BASE_TYPE_*` |
| `50083` | Operation on archived thread | threads archivés | ⬜ | ⬜ | |
| `50109` | The request body contains invalid JSON. | corps JSON syntaxiquement invalide | 🟡 | ✅ | produit par `RestRequestBody.TryParseJson` (aide partagée des handlers), testé via un validateur factice (n'arme ni ne consomme de règle `Intercept`) ; branché dans les validateurs `/users` (M1-REST-01) et `PATCH /channels/{channel_id}` (M1-REST-02), à brancher dans les autres familles |
| `160004` | Thread already created for this message | `POST /channels/{id}/messages/{id}/threads` | ⬜ | ⬜ | |
| `220001` | Forum webhook needs `thread_name`/`thread_id` | webhooks forum | ⬜ | ⬜ | |
