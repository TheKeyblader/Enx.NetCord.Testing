# Parité erreurs

<!-- Baseline 2026-11-25 : discord-api-docs@62ab2d50 (developers/topics/opcodes-and-status-codes.mdx §HTTP Response Codes, §JSON Error Codes ; developers/reference.mdx §Error Messages). Seuls les codes que le fake doit produire sont listés ; l'auditeur ajoute une ligne dès qu'un handler en produit un nouveau. -->

Forme d'erreur Discord (à reproduire exactement, via `DiscordError`) :

```json
{ "code": 50035, "message": "Invalid Form Body",
  "errors": { "content": { "_errors": [ { "code": "BASE_TYPE_MAX_LENGTH", "message": "Must be 2000 or fewer in length." } ] } } }
```

Rate limit (429) : corps `{ "message": "You are being rate limited.", "retry_after": 1.5, "global": false, "code"?: … }`
+ en-têtes `Retry-After`, `X-RateLimit-*`, `X-RateLimit-Scope`.

Progression `Fake` : ✅ **0** · 🟡 **0** · ⬜ **41** · ⛔ **0**

## Statuts HTTP

| Clé | Statut | Quand le fake le renvoie | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- |
| `http:200` | 200 OK | succès avec corps | ⬜ | ⬜ | |
| `http:201` | 201 Created | création quand Discord le fait (ex. PUT ban ? vérifier spec) | ⬜ | ⬜ | suivre les `responses` de la spec par opération |
| `http:204` | 204 No Content | DELETE, PUT réaction/pin, typing… | ⬜ | ⬜ | |
| `http:400` | 400 Bad Request | corps invalide (avec `50035`) | ⬜ | ⬜ | |
| `http:401` | 401 Unauthorized | token absent/inconnu | ⬜ | ⬜ | `{"message":"401: Unauthorized","code":0}` |
| `http:403` | 403 Forbidden | permission manquante (`50013`) / accès (`50001`) | ⬜ | ⬜ | |
| `http:404` | 404 Not Found | ressource inconnue (`10xxx`) ou route inconnue (`code 0`) | ⬜ | ⬜ | |
| `http:405` | 405 Method Not Allowed | méthode non supportée sur une route connue | ⬜ | ⬜ | |
| `http:429` | 429 Too Many Requests | simulation de rate limit (opt-in) ou `Intercept` | ⬜ | ⬜ | M6-02 |
| `http:5xx` | 500/502 | injecté par le test via `Intercept` | ⬜ | ⬜ | tester les retries NetCord |

## Codes JSON

| Code | Message Discord | Contexte typique | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- |
| `0` | General error / `404: Not Found` / `401: Unauthorized` | route inconnue, auth | ⬜ | ⬜ | |
| `10003` | Unknown Channel | `/channels/{id}` | ⬜ | ⬜ | |
| `10004` | Unknown Guild | `/guilds/{id}` | ⬜ | ⬜ | |
| `10007` | Unknown Member | `/guilds/{id}/members/{user_id}` | ⬜ | ⬜ | |
| `10008` | Unknown Message | `/channels/{id}/messages/{id}` | ⬜ | ⬜ | |
| `10011` | Unknown Role | rôles | ⬜ | ⬜ | |
| `10013` | Unknown User | `/users/{id}` | ⬜ | ⬜ | |
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
| `50013` | Missing Permissions | permission calculée insuffisante | ⬜ | ⬜ | dépend de M4-REST-05 |
| `50024` | Cannot execute action on this channel type | ex. thread sur DM | ⬜ | ⬜ | |
| `50027` | Invalid Webhook Token | token d'interaction expiré | ⬜ | ⬜ | |
| `50035` | Invalid Form Body | validation (arbre `errors`) | ⬜ | ⬜ | codes d'erreur de champ `BASE_TYPE_*` |
| `50083` | Operation on archived thread | threads archivés | ⬜ | ⬜ | |
| `160004` | Thread already created for this message | `POST /channels/{id}/messages/{id}/threads` | ⬜ | ⬜ | |
| `220001` | Forum webhook needs `thread_name`/`thread_id` | webhooks forum | ⬜ | ⬜ | |
