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
  le porte dans `StateFailure.Limit` (amendement ADR 0013 ; `InvalidChannelUserLimit` : 99 voix, 10000 stage), sans multiplier les `StateFailureKind`.
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
- **Famille Guilds** (M1-REST-06) : conventions qui se réutilisent pour les autres routes de guilde (rôles, membres…).
  - **Réglages d'une ressource en un bloc immuable** : les champs qu'un `PATCH` change et que l'objet porte vivent dans un `record` (`GuildSettings`)
    tenu par l'entité et rendu par le snapshot (propriété `init`, valeur par défaut : aucun constructeur existant ne change) ; la mise à jour
    construit le nouveau bloc par `with`, compare à l'ancien (`!=`) et ne publie (`GuildUpdated`) que si quelque chose a changé.
  - **Champ à trois états dans une entrée d'état** : `Change<T>` (`IsSet`, `Value`) : `default` = non envoyé, `Change<T>.To(null)` = effacer. Pour un `ulong?`
    écrire `Change<ulong?>` (un `T?` non contraint ne devient pas `Nullable<T>`).
  - **Réglage qui désigne un autre objet** (un salon d'une guilde) : l'état vérifie l'appartenance et le type **avant toute écriture**
    (`InvalidGuildChannelReference`, le champ fautif voyage dans `StateFailure.Field`), et la suppression de l'objet visé efface le réglage dans le même commit.
  - **Lot validé en entier puis appliqué** (`PATCH /guilds/{id}/channels`) : contrôler tous les éléments (existence), puis la règle de lot (`40009`),
    puis les invariants par élément, **puis** écrire dans l'ordre du tableau ; publier un fait par objet **réellement** différent après le lot
    (comparer l'état avant/après, premier passage dans l'ordre du tableau), pas un fait par entrée.
  - **Refus d'un lot indexé** : un refus d'état qui vise l'entrée N d'un corps tableau porte l'indice (`StateFailure.Index`, posé par l'état : `failure with { Index = i }`) et la
    fabrique `DiscordError` correspondante prend `int? index` pour écrire `N.parent_id` (même chemin que les erreurs structurelles `N.id`).
  - **Valeur que tout lecteur doit rendre pareil** (REST, futur `GUILD_CREATE`) : la résoudre **dans l'état**, pas dans le `*Wire` (ex. `GuildSnapshot.EffectiveOwnerId` =
    propriétaire stocké, sinon le bot) ; toute projection d'une guilde passe par `SnapshotOf(guild)` sous le verrou, jamais `guild.ToSnapshot()` directement.
  - **Corps racine tableau** : `request.Body.TryParseJson` (`50109`), racine non tableau ⇒ `BASE_TYPE_BAD_TYPE` à la racine (`AddBadType(errors, "", "array")`),
    puis une `errors.Scope("<indice>")` par élément pour que les chemins soient `0.id`, `1.position`.
  - **Liste de DTO de familles différentes** : une classe-union (`GuildChannelUnion`, une propriété par variante, exactement une renseignée) avec
    `[JsonConverter]` fermé qui écrit la variante avec `FakeDiscordJsonContext.Default.<Variante>` ; ne pas passer par `object`.
  - **Réponse 201** : `RestResult.Json(HttpStatusCode.Created, dto, typeInfo)` (`RestResult.Ok` est 200) ; `ChannelWire.ToText/ToVoice/ToCategory/ToForum` sont publics, réutilisables.
  - **Pièges NetCord** : `GetGuildAsync` écrit la query `?with_counts=True`/`False` (texte de `bool`), pas `true` : lire sans tenir compte de la casse ; un JPEG part en
    `data:image/jpg;base64,` (pas `jpeg`) : `ImageData.TryGetHash` accepte les deux, ne rien normaliser dans un validateur ; `GuildOptions`/`GuildChannelProperties` n'envoient jamais `null` (tester les
    `null` par `SendRequestAsync` + corps à la main) ; `ForumLayoutType` NetCord : `NotSet`=0, `ListView`=1, `GalleryView`=2.
  - **Conformité stricte** : une propriété requise nullable de la spec (`icon`, `description`, `incidents_data`…) s'écrit `null` (pas `WhenWritingNull`) ; seules les propriétés
    facultatives (`approximate_member_count`) s'omettent. Le type de salon 16 (media) n'est pas dans l'énumération de la spec : un scénario de conformité ne peut pas le
    créer, le couvrir dans les tests de route.
  - **Un test qui veut une route non implémentée** ne doit plus prendre une route de la famille Guilds (`GET /guilds/{guild_id}` est implémentée) :
    `RestTestHarness` a son propre jeu de bindings, mais choisir une route lointaine (`/guilds/{guild_id}/vanity-url`…) pour un test sur `RestRoutes.Bindings`.
- **Famille Members** (M1-REST-07) : conventions pour une sous-ressource de guilde indexée par utilisateur (rôles, bans, futurs `members/@me`).
  - **Une seule résolution** : `ResolveMember(caller, guildId, userId)` (`FakeDiscordState.Members.cs`) enchaîne `GuildAccess` (profil `Denied`) puis le membre
    (`UnknownUser` `10013` si le fake ne connaît pas l'utilisateur, puis `UnknownMember` `10007` s'il existe sans être membre ; ordre à confirmer UR-019) ; toutes les opérations de la famille l'appellent sous le verrou, jamais `_members` directement.
    Un handler reçoit des `MemberDetails` (membre **et** utilisateur, détachés) car le JSON d'un membre porte `user`.
  - **Faits** : `MemberUpdated` (nick, rôles, timeout, profil) et `MemberRemoved` (le membre tel qu'il était + l'utilisateur) à côté de `MemberAdded` ; comme pour une guilde,
    un `PATCH` ne publie que si une valeur **change** (comparer avant d'écrire ; liste de rôles comparée comme **ensemble**) ; un `PUT`/`DELETE` de rôle déjà dans l'état voulu
    renvoie 204 sans fait (`StateResult<bool>` : `true` = changé).
  - **Validation avant écriture** d'une mise à jour multi-champs : membre → rôles (le premier inconnu, `@everyone` ignoré comme à `AddMember`) → règles de timeout → puis écrire ; un refus ne laisse rien.
  - **Une même entrée d'état pour deux routes** : `UpdateMemberInput` sert `PATCH …/{user_id}` et `PATCH …/@me` (le validateur de `@me` y met `request.Caller.UserId`). Les routes littérales
    (`@me`, `search`) sont inventoriées à côté de `{user_id}` : le littéral prime, rien à faire. Une réponse commune à deux schémas de la spec (`GuildMemberResponse`,
    `PrivateGuildMemberResponse`) = un DTO sans les propriétés facultatives en trop (le mode strict ne les exige pas).
  - **Défaut de pagination documenté mais que NetCord n'utilise pas** : la doc donne `limit` = 1 par défaut (`list`, `search`) alors que NetCord envoie toujours `limit` ; implémenter le défaut de la doc et le tester par `SendRequestAsync` brut.
  - **Date-time ISO 8601** (corps ou query) : `FormValidation.TryReadDateTime(raw, path, errors, out value)` (formats ISO stricts, sans offset = UTC, `DATE_TYPE_PARSE` sinon ; refuse `12/25/2026`), jamais `DateTimeOffset.Parse`.
  - **Route dont le seul succès possible est « déjà fait »** (`PUT …/members/{user_id}` sans OAuth2) : répondre le 204 documenté pour l'état déjà atteint, puis l'erreur du catalogue ; le dire dans la ligne de parité.
  - **Image de guilde** (`avatar`/`banner` d'un membre) : réutiliser `GuildValidators.ReadImage` (interne, `Change<string>` avec hash dérivé) plutôt que d'en écrire un second.
  - **Protéger le bot et le propriétaire** (`DELETE …/members/{user_id}`, timeout) : `StateFailureKind.MissingPermissions` (`50013`) **sans calcul de permissions** ; le test d'état attendu « un kind sans entrée du catalogue
    lève `NotSupportedException` » (`DiscordErrorTests`) prend un kind qui n'a toujours pas d'entrée (`MemberAlreadyExists`) : le changer quand on le catalogue.
  - **Tests** : semer un membre avec pseudo/rôles par `discord.State.AddMember(StateCaller.Fixture, new AddMemberInput(guild.Id, user, "Nick", [role]))` après `discord.Seed.User(...)`
    (le seed n'expose pas le pseudo) ; un rôle administrateur par `discord.State.CreateRole(StateCaller.Fixture, new CreateRoleInput(guild.Id, "Admins", Permissions: 1UL << 3))`.
    NetCord : `GetGuildUsersAsync` n'a pas de `cancellationToken` (`.WithCancellation(Ct)`), pagine par `after` jusqu'à une page **plus courte** que `BatchSize` (une dernière requête vide quand
    le total est un multiple) ; `TimeOutUntil = default(DateTimeOffset)` écrit `null` (efface le timeout) et `ImageProperties.Empty` écrit `null` (efface l'image) ; `Nickname = ""` efface le pseudo, `null` n'est jamais envoyé
    (tester `{"nick":null}` par `SendRequestAsync`). Le bot est le membre d'ID le plus petit : il ouvre tout listing trié par ID.

## 3. Handler (`src/Enx.NetCord.Testing/Rest/Routes/<Domaine>/`)

- Un handler par opération, enregistré dans la table de routes avec le template de la spec
  (`POST /channels/{channel_id}/messages`). Routes littérales avant paramétrées.
- Entrée : `FakeRestRequest` (méthode, template, paramètres, query, corps JSON ou multipart,
  `Authorization`, `X-Audit-Log-Reason`). Sortie : `FakeRestResponse` (statut, JSON, en-têtes).
- Validation structurelle (étape 3 du pipeline, sans lire l'état) : un validateur par opération,
  lié par `RouteBinding.Of(clé, handler, validateur)`. **Réutiliser** `Rest/FormValidation.cs`
  (ne pas recopier dans la famille) : `ValidateRouteSnowflake(request, "channel_id")`,
  `ReadRouteSnowflake(request, "message_id", errors)` (une route à plusieurs IDs : tous les `50035` d'ID dans un seul `FormErrors`),
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
- **Messages selon le type de salon** (M1-REST-03B) : l'état lit `ChannelTraits.Of(channel.Kind).IsMessageable` (jamais un `switch` sur le
  type dans un handler) après `ResolveChannel` et avant tout commit, pour **tous** les appelants (le seed obtient une `InvalidOperationException`).
  Création : catégorie ⇒ `CannotSendInNonTextChannel` (`50008`), forum/media ⇒ `CannotSendInForumChannel` (`40058`) ; liste et bulk-delete ⇒
  `CannotExecuteOnChannelType` (`50024`, hypothèse UR-011) ; `GET`/`PATCH`/`DELETE` d'un message ⇒ `10008` (rien n'y existe, pas de contrôle de plus) ;
  `crosspost` : `10008` → DM `50003` → type ≠ 5 `50024` → déjà `CROSSPOSTED` `40033` (sans `StateChange`). Un salon media n'est pas seedable :
  `discord.State.CreateGuildChannel(StateCaller.Fixture, new CreateGuildChannelInput(guild.Id, ChannelKind.GuildMedia, "gallery"))` dans les tests.
  Une fabrique `DiscordError` ajoute aussi sa paire `(status, code)` à `TryCreateSimple` et à `DiscordErrorTests`.
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
  (M1-REST-06) passent par un `JsonConverter` fermé (règle 5, `GuildChannelUnion`), pas par un repli sur `object` : voir « Famille Guilds » plus bas.
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
- **Sous-ressource d'un message** (réactions, M1-REST-04) : l'état de la sous-ressource vit **sur `MessageEntity`** (liste d'entités
  `ReactionEntity`, dans l'ordre de première apparition) et ses opérations sont des membres de `FakeDiscordState.<Domaine>.cs` qui
  résolvent par `TryGetMessage(caller, …)` (salon `10003`/`50001` puis message `10008`). Ce que la réponse d'un message en lit est un
  champ d'init de `MessageSnapshot` ; un champ relatif à l'appelant (`me`) est calculé par `MessageEntity.ToSnapshot(…, viewerId)`
  avec le bot (`_botId`), seule identité REST, jamais dans la couche `Rest/`. Une propriété que Discord omet sans valeur (`reactions`)
  est un DTO nullable `WhenWritingNull` rendu `null` quand la liste est vide (`ReactionWire.ToResponses`).
- **Opération idempotente ou non** : un `PUT` qui crée un fait déjà vrai renvoie succès **sans** `StateChange` (`StateResult<bool>` :
  `true` = nouveau) ; un `DELETE` d'un fait absent n'est pas silencieux quand Discord répond une erreur (réactions : `10014`, hypothèse
  consignée), alors que vider « tout » (aucun emoji nommé) réussit toujours. Un `StateFailureKind` sans corps `50035` propre
  (`UnknownEmoji`, `MaxReactions`) a sa fabrique `DiscordError`, son entrée `FromStateFailure` et son entrée `TryCreateSimple`.
- **Segment de route qui désigne une ressource sans ID** (`emoji_name` : unicode encodé dans l'URL ou `name:id`) : le validateur
  l'analyse avec une aide de domaine (`ReactionEmoji.TryParse`, dans `State/`) **après** les `50035` d'ID et rend l'erreur du catalogue
  (`10014`) à l'étape structurelle ; une table d'emojis unicode n'existe pas, la règle est « un caractère non ASCII, pas de lettre ASCII ni
  de blanc ». Un test qui envoie un segment brut par `SendRequestAsync` passe un littéral interpolé (NetCord l'encode ; `%` et `:` sont
  encodés, donc on ne peut pas simuler un pourcentage mal formé par ce chemin).
- **Pins et typing** (M1-REST-05) : conventions qui se réutilisent pour une sous-ressource à horodatage ou un fait sans état.
  - **Horodatage que NetCord repagine** : `GET …/messages/pins` est paginé par NetCord avec `before` écrit à la **seconde** (`ToString("s")`, sans offset ⇒
    lu en UTC par `DateTimeStyles.AssumeUniversal`). `pinned_at` est donc coupé à la seconde et strictement croissant par salon (`NextPinTime` : une seconde de plus
    que `ChannelEntity.LastPinTimestamp` si l'horloge n'a pas avancé) ; sinon deux pins de la même seconde (toujours le cas avec l'horloge virtuelle figée) seraient perdus à la
    page suivante. Tout autre curseur horodaté NetCord doit être vérifié dans `RestClient.*.cs` (format de `AppendQueryParameter`) avant de choisir la précision.
  - **Message système créé par une opération** (pin ⇒ type 6, `message_reference` vers le message épinglé, contenu vide, auteur = l'appelant) : construit **dans** l'opération d'état, sous le
    même verrou, sans passer par `CreateMessage` (qui ajouterait mentions et type 19 de réponse) ; il compte dans `last_message_id`. Faits dans l'ordre : `MessageEdited`, `ChannelPinsUpdated`, `MessageCreated`.
  - **Opération sans mutation qui publie un fait** (`TriggerTyping` ⇒ `TypingStarted`) : permise sans changement structurel de l'état (`NextChangeNumber()` + `Publish`) ; ne rien stocker si Discord n'expose rien à relire.
  - **Idempotence** : `PUT` d'un fait déjà vrai et `DELETE` d'un fait déjà faux rendent `204` sans fait publié (`StateResult<bool>` `false`) ; la limite (`30003`) se contrôle **après** l'idempotence.
  - Un salon sans chat (catégorie, forum, media) : les routes qui **listent** ou **déclenchent** (`GET …/pins`, `POST …/typing`) rendent `50024` ; celles qui visent **un message** (`PUT`/`DELETE …/pins/{message_id}`) rendent `10008`.
  - **Nom NetCord** : `RestMessage.IsPinned` (pas `Pinned`) ; `GetChannelPinsAsync` prend `paginationProperties` (`BatchSize`) et, comme les autres lecteurs paginés, `cancellationToken: Ct` doit être nommé (xUnit1051). Pour semer un message déjà épinglé :
    `discord.State.CreateMessage(StateCaller.Fixture, new CreateMessageInput(channel, author, "x", Pinned: true))` (aucune API de seed publique, UR-015) ; un DM : `discord.State.GetOrCreateDmChannel(new CreateDmChannelInput(userId))`.
  - **Un champ de salon alimenté par une autre famille** (`last_pin_timestamp`) : `ChannelEntity` → `ChannelSnapshot` (propriété `init`) → `ChannelWire.ToText/ToVoice` et `UserWire.ToDmChannel` ; NetCord ne l'expose pas sur ses types de salon REST, le lire par `GetRawAsync`.
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
(5) ne pas écrire de route avec `string` pour `SendRequestAsync` : un littéral interpolé, ou un paramètre `FormattableString` ;
(6) le bot est créé avec le fake : son ID est le plus petit, donc il passe **en premier** dans un listing trié par ID (réactions) ;
(7) une réaction posée par un autre utilisateur se sème avec `channel.Reaction(messageId, "👍", userId, burst: false)`
(`SeededTextChannel`), pas par un faux `PUT`.

Minimum : nominal, relecture d'état, `Unknown *` 404, validation 50035, (401 si token
invalide modélisé), event Gateway émis (ou non). Les API exactes (`Seed`, `CreateRestClient`,
assertions) sont celles fixées par les ADR — adapter l'exemple.

### Scénario de conformité (obligatoire, en plus des tests ci-dessus)

Tout handler enregistré exige **au moins un** test non ignoré `[Parity("METHOD /path")]`
(clé exacte) dans `tests/Enx.NetCord.Testing.Conformance/Rest/` (`Conformance/Interactions/`
pour les routes d'interactions et de webhooks) : il appelle la route par le vrai `RestClient`
et **se termine par `ContractAssert.Conforms(discord, "METHOD /path")`**. Sans lui, la garde
`ConformanceTests_EveryImplementedOperation_HasAScenarioTaggedWithItsKey` fait échouer
`dotnet test` ; pas de liste d'exemptions. L'extrait OpenAPI couvre toutes les opérations :
ajouter un handler ne demande jamais de régénérer le contrat. Style de
`Rest/ChannelsConformanceTests.cs` (classe `[Trait("Category", "Conformance")]`,
`Ct => TestContext.Current.CancellationToken`) :

```csharp
[Fact]
[Parity("GET /channels/{channel_id}")]
public async Task GetChannelAsync_SeededTextChannel_ReturnsItAndConformsToGuildChannelResponse()
{
    await using var discord = FakeDiscord.Create();
    var general = discord.Seed.Guild("Test").TextChannel("general");
    using var rest = discord.CreateRestClient();

    var channel = await rest.GetChannelAsync(general.Id, cancellationToken: Ct);

    Assert.Equal("general", Assert.IsType<TextGuildChannel>(channel).Name);
    // add the set-up routes the scenario reached, if any
    ContractAssert.Conforms(discord, "GET /channels/{channel_id}");
}
```

`Conforms` valide **toutes** les tentatives du journal (erreurs comprises) et exige que la
route ait été atteinte : un cas d'erreur principal (`Unknown *`, `[Parity("10003")]`) se
termine aussi par `Conforms`. Un test de conformité n'appelle jamais `Assert.Skip()`. Un écart
de schéma est un bug du fake (corriger `src/` ; un test `Skip` ne compte pas pour la garde).
Détail et régénération du contrat : `dotnet-test-conventions` § Conformité.

## 5. Parité

Dans `docs/parity/rest.md`, ligne `POST` `/channels/{channel_id}/messages` : `Fake` ✅ ou 🟡
(+ Note : ce qui manque, ex. « attachments multipart non gérés »), `Tests` ✅. Recalculer
les compteurs d'en-tête.

Check-list de fin : build sans warning, tests verts, **scénario de conformité `[Parity]` +
`ContractAssert.Conforms` ajouté** (§4), parité à jour, skill mise à jour si convention nouvelle.
