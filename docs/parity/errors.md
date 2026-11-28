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

Progression `Fake` : ✅ **5** · 🟡 **10** · ⬜ **28** · ⛔ **0**

## Statuts HTTP

| Clé | Statut | Quand le fake le renvoie | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- |
| `http:200` | 200 OK | succès avec corps | ⬜ | ⬜ | |
| `http:201` | 201 Created | création quand Discord le fait (ex. PUT ban ? vérifier spec) | ⬜ | ⬜ | suivre les `responses` de la spec par opération |
| `http:204` | 204 No Content | DELETE, PUT réaction/pin, typing… | ⬜ | ⬜ | |
| `http:400` | 400 Bad Request | corps invalide (avec `50035`) | 🟡 | ✅ | `DiscordError.InvalidFormBody`/`InvalidJson` testés via le vrai `RestClient` avec des handlers factices (M1-03) ; reste à les produire depuis les vrais handlers (M1-REST) |
| `http:401` | 401 Unauthorized | token absent/inconnu | ✅ | ✅ | `{"message":"401: Unauthorized","code":0}` ; absent, inconnu ou `Bearer <token bot>` ; une opération `{}` accepte l'anonyme **et** un token inconnu (le header est ignoré, `Caller.Kind == Unrecognized`). Source : fauxcord `src/middleware/auth.ts:109-118` (chemins exemptés, dont le callback d'interaction et les webhooks à token, sans contrôle de l'en-tête) ; le comportement réel de Discord pour un token invalide sur ces routes n'est **pas vérifié** (M1-03) |
| `http:403` | 403 Forbidden | permission manquante (`50013`) / accès (`50001`) | 🟡 | ✅ | produit via `Intercept`/handler factice seulement ; calcul de permissions : M4-REST-05 ; `50001` pas encore dans le catalogue |
| `http:404` | 404 Not Found | ressource inconnue (`10xxx`) ou route inconnue (`code 0`) | 🟡 | ✅ | route inconnue / non implémentée (Lenient) : produit par le pipeline, aussi testé via `FakeDiscord.CreateRestClient()` (M1-02) ; ressources `10xxx` : via handler factice, vrais handlers à venir (M1-REST) |
| `http:405` | 405 Method Not Allowed | méthode non supportée sur une route connue | ✅ | ✅ | en-tête `Allow` = méthodes inventoriées du chemin ; Lenient et Strict ; pas d'en-têtes de rate limit (aucune opération résolue) |
| `http:429` | 429 Too Many Requests | simulation de rate limit (opt-in) ou `Intercept` | 🟡 | ✅ | `Intercept(RestEffect.RateLimited(s))` : corps `{"message":"You are being rate limited.","retry_after":s,"global":false}`, scope `user`, sans `code` (exemple Discord) ; un 429 injecté ne prouve pas un quota : M6-02 |
| `http:5xx` | 500/502 | injecté par le test via `Intercept` | ✅ | ✅ | `RestEffect.Error` 500 ou 502 → corps `{"message":"500: Internal Server Error","code":0}` ; NetCord ne réessaie pas sur 5xx (`RestException`) |

## Codes JSON

| Code | Message Discord | Contexte typique | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- |
| `0` | General error / `404: Not Found` / `401: Unauthorized` | route inconnue, auth | ✅ | ✅ | 401, 404 (Lenient, aussi via la façade `FakeDiscord`) et 405 ; messages `NNN: Reason` comme Discord |
| `10003` | Unknown Channel | `/channels/{id}` | 🟡 | ✅ | fabrique `DiscordError.UnknownChannel()` testée via un handler factice (404 même en Strict) ; le vrai `GET /channels/{channel_id}` vient en M1-REST |
| `10004` | Unknown Guild | `/guilds/{id}` | ⬜ | ⬜ | |
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
| `50001` | Missing Access | salon non visible | ⬜ | ⬜ | |
| `50006` | Cannot send an empty message | message sans contenu/embeds/fichiers/composants/sticker/poll | ⬜ | ⬜ | |
| `50007` | Cannot send messages to this user | DM refusé (option du seed) | ⬜ | ⬜ | |
| `50008` | Cannot send messages in a non-text channel | voix/catégorie | ⬜ | ⬜ | |
| `50013` | Missing Permissions | permission calculée insuffisante | 🟡 | ✅ | injecté via `Intercept(403, 50013)` : ne prouve pas un calcul de permission ; dépend de M4-REST-05 |
| `50024` | Cannot execute action on this channel type | ex. thread sur DM | ⬜ | ⬜ | |
| `50025` | Invalid OAuth2 access token provided | `POST /users/@me/channels` avec `access_tokens` (group DM) | 🟡 | ✅ | le fake n'a aucun grant OAuth2 : tout jeton est refusé. HTTP **403** : documenté pour `50025` dans `discord-social-sdk/development-guides/provisional-accounts/merging-accounts.mdx:347` (`external_auth_token` invalide), **non vérifié pour un group DM** (la page des codes JSON ne donne pas de statut) (M1-REST-01) |
| `50027` | Invalid Webhook Token | token d'interaction expiré | ⬜ | ⬜ | |
| `50035` | Invalid Form Body | validation (arbre `errors`) | 🟡 | ✅ | produit par les vrais validateurs des routes `/users` (M1-REST-01 : `BASE_TYPE_BAD_LENGTH`, `BASE_TYPE_BAD_TYPE`, `BASE_TYPE_REQUIRED`, `NUMBER_TYPE_COERCE`, `IMAGE_INVALID`, `MODEL_TYPE_CONVERT`) ; arbre `errors` (objets par propriété/indice décimal, `_errors` à la feuille, pas d'`errors: null`) lu par NetCord ; testé aussi avec un validateur factice (un 50035 de validateur ne consomme pas de règle `Intercept`), autres familles en M1-REST ; codes de champ `BASE_TYPE_*` |
| `50083` | Operation on archived thread | threads archivés | ⬜ | ⬜ | |
| `50109` | The request body contains invalid JSON. | corps JSON syntaxiquement invalide | 🟡 | ✅ | produit par `RestRequestBody.TryParseJson` (aide partagée des handlers), testé via un validateur factice (n'arme ni ne consomme de règle `Intercept`) ; branché dans les validateurs `/users` (M1-REST-01), à brancher dans les autres familles |
| `160004` | Thread already created for this message | `POST /channels/{id}/messages/{id}/threads` | ⬜ | ⬜ | |
| `220001` | Forum webhook needs `thread_name`/`thread_id` | webhooks forum | ⬜ | ⬜ | |
