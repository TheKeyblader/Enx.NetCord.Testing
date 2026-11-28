---
name: Fake REST endpoint
description: Recette pas à pas pour ajouter ou compléter un endpoint REST du faux Discord (état, handler, validation, erreurs Discord, rate-limit headers, dispatch Gateway, tests via RestClient NetCord, mise à jour de parité).
---

# Ajouter un endpoint REST fake

Prérequis : avoir lu `docs/architecture.md` §REST et l'ADR sur la table de routes.

## 1. Comprendre la cible

- Spec : opération dans `discord-api-spec/specs/openapi.json` (`paths["/channels/{channel_id}/messages"].post`) :
  paramètres, `requestBody` (JSON et/ou multipart), réponses `200/201/204`, erreurs.
- Docs : page `discord-api-docs/developers/resources/<resource>.mdx` (limites, permissions, events émis).
- NetCord : méthode dans `NetCord/Rest/RestClient.<Domaine>.cs` (route, query, type de corps,
  type de retour attendu → quel `JsonModel` il désérialise).
- fauxcord : `src/routes/<x>.ts`, `src/services/<x>.ts`, `src/validators/<x>.ts` pour les
  cas limites (ex. `embeds: null`, `?wait=1`, pins nouvelle/ancienne API, bulk-delete 2–100).

## 2. État (`src/Enx.NetCord.Testing/State/`)

- Ajouter/compléter l'entité (classe interne mutable dans `State/`, ADR 0003 ; seules des valeurs détachées en sortent) et ses index.
- Toute mutation passe par une méthode de `FakeDiscordState` qui :
  1. valide les invariants (existence, appartenance guilde/canal) ;
  2. applique la mutation sous verrou ;
  3. publie un **événement de domaine** (`StateChange`) que la Gateway traduit en dispatch.
- IDs : `ISnowflakeGenerator` injecté ; dates : `TimeProvider`.
- **Salons** (ADR 0013, M1-REST-02B1) : une seule entité/`ChannelSnapshot` pour tous les types ; ce qui s'applique à un type
  se lit dans `ChannelTraits.Of(kind)` (famille, groupe de tri, parent permis, bornes, `HasNsfw`/`HasSlowmode`), jamais
  par un `switch` de plus dans un handler. Un salon de guilde se crée **uniquement** par `CreateGuildChannel` (seed et
  futur `POST /guilds/{id}/channels` : les invariants de parent, les 50/500 salons et la position par défaut y sont) ;
  un échec d'invariant sort **avant** d'allouer un ID (un refus ne consomme pas le générateur). Supprimer une catégorie
  détache ses enfants dans le même commit (`ChannelUpdated` par enfant, puis `ChannelDeleted`). Un champ que le type n'a
  pas est ignoré par l'état (et validé en amont), jamais stocké. Les listes de salons sont rendues par ID croissant.
- **`PATCH` d'un salon** (M1-REST-02B2) : `UpdateChannel` vérifie **tout avant la première écriture**, dans l'ordre `type`, `parent_id`,
  `name`, `topic`, `bitrate`, `user_limit` (un seul refus rendu), puis écrit ; la conversion texte ↔ annonces (`TryConvertKind`) s'applique
  d'abord et les autres champs sont jugés contre le **nouveau** type (`ChannelTraits.Of(kind)`). Le validateur structurel, qui ignore le type,
  prend la borne la plus large de la spec (topic 4096, bitrate ≥ 8000, user_limit ≥ 0) ; la borne propre au type (topic 1024, plafonds
  vocaux) est un `StateFailureKind` décidé sous le verrou, avec sa fabrique `DiscordError`. Un refus qui a besoin d'un nombre dans son message
  le porte dans `StateFailure.Limit` (`InvalidChannelUserLimit` : 99 voix, 10000 stage), sans multiplier les `StateFailureKind`.
- **Champ à trois états** (absent / `null` / valeur) : le validateur lit la clé avec `root.TryGetProperty` et rend un drapeau `ChangeX`
  à côté de la valeur (`ChangeTopic`, `ChangeParent`, `ChangeRtcRegion`, `ChangeDefaultSortOrder`) ; l'état n'applique la valeur nulle
  que si le drapeau est levé. Un champ où `null` veut dire « inchangé » (NetCord n'envoie jamais `null`) reste un `int?` lu par
  `ReadOptionalInteger`. Une valeur égale à l'état courant (le parent courant, `null` sans parent) ne se contrôle pas et ne publie rien.
  Un test de route qui envoie `null` construit la requête à la main (`SendRequestAsync` + `StringContent`) et note l'écart avec NetCord
  dans la ligne de parité ; l'opération d'état se teste aussi directement.
- Champ « validé puis ignoré » (tags, `default_reaction_emoji`, `flags`) : le validateur lit et borne la valeur (`Scope`), le payload ne la
  porte pas. Entier d'une énumération de la spec : `BASE_TYPE_CHOICES` « Value must be one of (a, b). » (`ChannelValidators.ReadChoice`).
- Tests d'état qui comptent les salons d'une guilde : `FakeDiscordState.MaxChannelsPerGuild` / `MaxChannelsPerCategory`.
  Un `[Theory]` public ne peut pas prendre un type `internal` (`ChannelKind`…) en paramètre : passer des `int`/`string`.

## 3. Handler (`src/Enx.NetCord.Testing/Rest/Routes/<Domaine>/`)

- Un handler par opération, enregistré dans la table de routes avec le template de la spec
  (`POST /channels/{channel_id}/messages`). Routes littérales avant paramétrées.
- Entrée : `FakeRestRequest` (méthode, template, paramètres, query, corps JSON ou multipart,
  `Authorization`, `X-Audit-Log-Reason`). Sortie : `FakeRestResponse` (statut, JSON, en-têtes).
- Validation structurelle (étape 3 du pipeline, sans lire l'état) : un validateur par opération,
  lié par `RouteBinding.Of(clé, handler, validateur)`. **Réutiliser** `Rest/FormValidation.cs`
  (ne pas recopier dans la famille) : `ValidateRouteSnowflake(request, "channel_id")`,
  `TryReadJsonObject(request, out doc, out error)` (`50109` / `50035` `MODEL_TYPE_CONVERT`),
  `ReadOptionalString`, `ReadNullableString` (distingue « absent » de `null` = effacer, via `out present`),
  `ReadOptionalInteger(root, name, min, max, errors)` (entier ou chaîne numérique ; `NUMBER_TYPE_COERCE`/`MIN`/`MAX` ;
  absent ou `null` ⇒ `null` = inchangé), `ReadOptionalBoolean` (`BOOLEAN_TYPE_COERCE`), `CheckLength`,
  `TryReadSnowflake` (chaîne ou nombre), `AddBadType`, `AddRequired`, `TryParseSnowflake` / `NotSnowflakeMessage`.
  Un validateur de `PATCH` valide aussi le `channel_id`/`…_id` de la route en premier (`ValidateRouteSnowflake`) et
  met l'ID dans son payload ; l'état détermine ensuite ce qui s'applique au type de ressource (ex. DM : renvoyé inchangé). Accumuler les erreurs dans un
  `FormErrors` puis `DiscordError.InvalidFormBody(errors)`, ou rendre
  `RestValidation.Valid(recordDétaché)`, lu par `request.GetPayload<T>()`. Ne garder dans
  `Routes/<Domaine>/` que les règles propres au domaine ; une règle qui sert à deux familles
  remonte dans `FormValidation`.
- **Tableaux typés, objets imbriqués, query** (`FormValidation`, M1-REST-03) : `TryReadArray` / `TryReadObject`
  (absent ou `null` ⇒ `false` sans erreur ; autre type ⇒ `BASE_TYPE_BAD_TYPE` / `MODEL_TYPE_CONVERT`),
  `ReadSnowflakeArray(root, name, minItems, maxItems, unique, errors)` (erreurs par indice `name.N`, `BASE_TYPE_BAD_LENGTH`
  si `minItems > 0`, sinon `BASE_TYPE_MAX_LENGTH`, `BASE_TYPE_ARRAY_UNIQUE`), `CheckMaxLength` / `CheckMaxCount`
  (`BASE_TYPE_MAX_LENGTH` « Must be N or fewer in length. »), `ReadQueryInteger` / `ReadQuerySnowflake` (la query a les mêmes
  erreurs que le corps). Pour lire un objet imbriqué avec ses propres noms de propriété, **`errors.Scope("embeds.0")`**
  rend un `FormErrors` qui préfixe ses chemins : y passer les mêmes helpers `Read*` (`Scope` se chaîne ; son `IsEmpty` ne
  reflète que ce qui a été ajouté par la vue). Le validateur d'une route peut rendre en payload **un record d'entrée de
  l'état** (`CreateMessageInput`, `EditMessageInput`) ; le handler complète ce que seul l'appelant connaît
  (`payload with { AuthorId = request.Caller.UserId }`).
- **Multipart** (`payload_json` + `files[n]`, ex. NetCord `IMessageProperties.Serialize`) : `RestMultipart.IsMultipart(body)` /
  `TryParse(body, out parts)` (`MultipartPart` : `Name`, `FileName`, `ContentType`, `Length`, `Content` en `ReadOnlyMemory<byte>` ;
  noms non ASCII décodés). **C'est le seul parseur multipart** : le journal (`RecordedRestRequest`) l'utilise aussi, via
  `TryParse(bytes, lenient: true, out parts)` (une partie sans `name` est alors ignorée au lieu d'invalider le corps). Il travaille
  sur les octets et ne copie rien : `Content` est une tranche du corps, donc ne lire que `Length` pour un fichier et `Text`
  seulement pour `payload_json`. Une frontière n'est reconnue qu'en début de ligne et suivie de CRLF ou `--` : des octets de
  fichier qui lui ressemblent restent dans leur partie. Ne pas écrire un second lecteur ni décoder le corps en `string`.
  Lire `payload_json` avec `FormValidation.TryReadJsonObject(new RestRequestBody(part.Content, "application/json"), …)` (un
  `payload_json` absent = objet vide ; `RestMultipart.PayloadJsonName`). Le fake ne conserve que le nom, la taille et le type des fichiers ; l'`url` d'une pièce
  jointe est déterministe (`MessageWire.CdnHost`). NetCord numérote ses fichiers à partir de 0 et ne peut donc que **remplacer**
  les pièces jointes d'un message à l'édition ; tester la conservation par un `SendRequestAsync` JSON brut.
- Ordre des contrôles d'un message : champs invalides (`50035`) puis message vide (`50006`) à l'étape structurelle ; salon
  (`10003`/`50001`) puis message (`10008`) puis règles d'auteur (`50005`) dans l'état. Un `50006` précède donc `10003` : les
  tests de `404`/`403` envoient un corps valide.
- Une projection qui nomme d'autres entités (auteur, mentions, message cité) reçoit `request.State.FindUser` (membre autorisé
  de la liste blanche de `RestIsolationTests`) ; ce que l'état renvoie est déjà détaché (`MessageSnapshot.ReferencedMessage`).
- Pour une réponse JSON, projeter le résultat dans un DTO interne conforme à la spec Discord et le sérialiser avec `FakeDiscordJsonContext` (source-gen STJ). Le vrai client désérialise la réponse ; réserver les `NetCord.JsonModels` aux API NetCord qui les exigent, comme `Interaction.CreateFromJson`.
- **Réponse qui dépend d'une famille** (salons : texte/annonces, voix/stage, catégorie, forum/media) : un DTO par famille dans
  `Serialization/`, tous inscrits dans `FakeDiscordJsonContext`, et une méthode de rendu unique (`ChannelWire.RenderGuildChannel`)
  qui choisit le DTO d'après les traits du type ; un champ « présent si défini » est un nullable
  `[JsonIgnore(Condition = WhenWritingNull)]` **sur ce champ seulement** (ADR 0006, règle 4). Les rendus de liste d'une route
  (M1-REST-06) auront besoin d'un `JsonConverter` fermé (règle 5), pas d'un repli sur `object`.
- Erreurs : `DiscordError.*` (404 `10003 Unknown Channel`, 400 `50035 Invalid Form Body`
  avec arbre `errors`, 403 `50013 Missing Permissions`, 401 `0 401: Unauthorized`…). Ajouter
  le code à `docs/parity/errors.md` s'il est nouveau.
- Visibilité (amendement 04B de l'ADR 0004) : toute opération d'état qui reçoit un ID de guilde
  ou de salon prend `StateCaller.Bot` depuis `Rest/` (jamais `StateCaller.Fixture`, réservé au
  seed et aux commandes de test). Résoudre la guilde ou le salon par `GetGuild(caller, id)` /
  `GetChannel(caller, id)` (contrôle `GuildAccess` : `10004` masquée pour `/guilds/{id}` et
  `preview`, `50001` refusée ailleurs) et traduire tout échec par
  `DiscordError.FromStateFailure(failure)`. Ne jamais appeler `FindGuild`, `FindChannel`,
  `FindMember` ni `FindMessage` depuis `Rest/` (lectures non contrôlées). Tester « guilde
  inconnue » **et** « bot non membre ». Les routes à jeton (interaction, webhook) ne passent pas
  par ce contrôle. Toute nouvelle opération (get/update/delete…) est un **membre de
  `FakeDiscordState`** (fichier partiel `FakeDiscordState.<Domaine>.cs`) qui prend un `StateCaller`
  et résout par `GuildAccess` : aucun helper d'état hors de `FakeDiscordState` n'est appelé depuis
  `Rest/`. Cette règle est vérifiée par `tests/Enx.NetCord.Testing.Tests/Architecture/RestIsolationTests.cs`
  (M1-T-02) : sous `Rest/**`, seul `StateCaller.Bot` est référencé et seuls les membres d'instance de
  `FakeDiscordState` qui prennent un `StateCaller` sont appelés. Toute opération d'état **sans**
  `StateCaller` appelée depuis `Rest/` (ex. futures opérations à jeton de M2) doit être ajoutée
  explicitement à la liste blanche `AllowedUnguardedMembers` de ce test (par le test-engineer), avec
  justification ADR.
- Une opération qui supprime ou ferme une ressource suit Discord, pas le nom de la route : fermer un
  DM (`DELETE /channels/{dm}`) ne détruit rien et ne committe aucun `StateChange` ; une mise à jour
  (`PATCH`) ne committe un `StateChange` que si une valeur change (comme `UpdateUser`).
- En-têtes rate limit (`X-RateLimit-Limit/Remaining/Reset/Reset-After/Bucket`) via le
  middleware commun ; ne pas les écrire dans le handler.
- `204 No Content` quand Discord le fait (DELETE, PUT réaction…).

## 4. Tests (`tests/Enx.NetCord.Testing.Tests/Rest/<Domaine>/`)

```csharp
[Fact, Parity("POST /channels/{channel_id}/messages")]
public async Task SendMessageAsync_ValidContent_CreatesMessageAndDispatchesMessageCreate()
{
    await using var discord = FakeDiscord.Create();
    var channel = discord.Seed.Guild("Test").TextChannel("general");
    var rest = discord.CreateRestClient();          // vrai NetCord.Rest.RestClient

    var message = await rest.SendMessageAsync(channel.Id, new() { Content = "hi" });

    Assert.Equal("hi", message.Content);
    Assert.Equal("hi", (await rest.GetMessageAsync(channel.Id, message.Id)).Content);
    discord.Gateway.Dispatched.Should().Contain("MESSAGE_CREATE"); // API d'assertion du fake
}
```

Pour affirmer qu'une requête a (ou n'a pas) été tentée, utiliser les assertions de l'ADR 0012 sur
le journal plutôt que du LINQ sur `Recorded.Requests` : `discord.Recorded.ShouldHaveRequestedOnce($"POST /channels/{channel.Id}/messages")`
(aussi `ShouldHaveRequested`, `ShouldNotHaveRequested`, et sur une suite : `Requests.Skip(n).Should…`).
Le motif est la clé `METHOD /template` où un paramètre peut être une valeur **décodée** ; interpoler
l'`Id` (jamais la poignée du seed). Un motif inconnu lève `ArgumentException`. Elles comptent des
tentatives, erreurs comprises. Pour émettre une requête de test avec une route qu'aucun
endpoint n'implémente encore, `RestClient.SendRequestAsync(method, content, $"/channels/{id}/…")` prend un
`FormattableString` (les arguments sont encodés par NetCord) : écrire un littéral interpolé, pas une variable `string`.
Si la spec change (`/parity/sync`), régénérer l'inventaire : la liste `RouteInventory.SnowflakeParameters`
(paramètres typés `SnowflakeType`) en fait partie.

Pièges de test : (1) `array.Reverse()` se lie à `MemoryExtensions.Reverse` (C# 14) et ne renvoie rien : utiliser
`OrderDescending()` / `AsEnumerable().Reverse()` ; (2) `ImmutableArray` dans `Assert.Equal` : `.AsEnumerable()` ; (3) l'horloge par
défaut est figée (`VirtualTimeProvider.DefaultNow`) : pour un message « ancien » (bulk-delete), semer avec un ID explicite
`IdAt(instant)` ; un ID **inventé** comme `424242` est déjà vieux de plus de 14 jours (snowflake de 2015), prendre `IdAt(now, n)` ;
(4) `NetCord.DeleteMessagesAsync` n'appelle `bulk-delete` que pour 2 IDs ou plus, un seul passe par `DELETE …/{message_id}` ;
(5) ne pas écrire de route avec `string` pour `SendRequestAsync` : un littéral interpolé, ou un paramètre `FormattableString`.

Minimum : nominal, relecture d'état, `Unknown *` 404, validation 50035, (401 si token
invalide modélisé), event Gateway émis (ou non). Les API exactes (`Seed`, `CreateRestClient`,
assertions) sont celles fixées par les ADR — adapter l'exemple.

## 5. Parité

Dans `docs/parity/rest.md`, ligne `POST` `/channels/{channel_id}/messages` : `Fake` ✅ ou 🟡
(+ Note : ce qui manque, ex. « attachments multipart non gérés »), `Tests` ✅. Recalculer
les compteurs d'en-tête.
