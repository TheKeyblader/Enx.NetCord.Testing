# 0014 — Salons privés : DM et group DM dans l'état et le seed

- **Statut** : Proposée
- **Date** : 2026-11-28
- **Auteur** : agent `architect`
- **Tâches liées** : M1-ADR-04C, M1-REST-01, M1-REST-02, M1-REST-02C, M1-REST-04B, M1-REST-05,
  M2-01, M2-13, M3-ADR-09, M3-03 ; proposées : M1-REST-02C1, M1-REST-02C2, PAR-REST-05
- **Points utilisateur** (`docs/user-review.md`) : UR-020 (seed des salons privés), UR-021
  (périmètre sans OAuth2, départ du bot), UR-022 (test manuel du lot D) ; UR-003 pour `50007`

> **Une ADR, pas un amendement « 04C ».** La tâche garde son ID de roadmap, `M1-ADR-04C`. La
> décision touche à parts égales le seed de l'[ADR 0004](0004-fake-discord-public-api.md), le
> modèle de salon de l'[ADR 0013](0013-channel-hierarchy-and-types.md) et le fil de
> l'[ADR 0006](0006-wire-serialization.md), sans changer aucune signature existante : la loger
> dans l'ADR 0004, déjà longue de deux décisions, obligerait le lecteur du modèle de salon à la
> chercher dans l'ADR de la façade. Les ADR 0004 et 0013 reçoivent une note de renvoi.

## Contexte

Le fake connaît un seul salon privé, le DM 1:1 (`ChannelKind.Dm`), et seulement par REST :

- `POST /users/@me/channels` le crée ou le retrouve (`State/FakeDiscordState.cs:469-504`,
  `GetOrCreateDmChannel`). `RecipientIds` y contient **l'autre partie** ; le bot est implicite.
  Rien n'y refuse pourtant le bot lui-même : `{"recipient_id": "<bot>"}` crée aujourd'hui un DM
  dont `RecipientIds` vaut `[bot]` (`Rest/Routes/Users/UserValidators.cs:114-118`).
- Le même `POST` avec `access_tokens` rend 403 `50025` : le fake n'a aucun grant OAuth2
  (`Rest/Routes/Users/UserRoutes.cs:43-51`). Un group DM (type 3) ne peut donc pas exister, et
  l'[ADR 0013](0013-channel-hierarchy-and-types.md), § 1, renvoie ce type à M1-REST-02C.
- Le seed ne crée aucun salon privé. Pour obtenir l'ID d'un DM, un test appelle
  `GetDMChannelAsync`, qui entre dans `Recorded.Requests`. Il ne peut y semer ni le message de
  l'utilisateur, ni la réaction d'autrui (M1-REST-04B, UR-015), alors que l'[ADR 0007](0007-interaction-model.md)
  demande un ID de salon pour une interaction en DM.

Sources épinglées : `discord-api-docs` @ `62ab2d50`, `discord-api-spec` @ `65423001`, `netcord`
@ `bb5f4e13` (tag `1.0.0-beta.30`), `fauxcord` @ `70563d51`.

**Ce que Discord documente.**

- **Objet** (`developers/resources/channel.mdx:30-34,221-246`) : `recipients`, `name`, `icon`,
  `owner_id` (« id of the creator of the group DM »), `application_id` (« the application that
  created the group »), `managed` (« whether the channel is managed by an application via the
  `gdm.join` OAuth2 scope »), `last_message_id`. Schéma `PrivateGroupChannelResponse`
  (`specs/openapi.json:36291`) : `id`, `type`, `flags`, `recipients`, `name`, `icon` et `owner_id`
  requis ; `name` et `icon` nullables ; `managed`, `application_id`, `last_message_id` et
  `last_pin_timestamp` facultatifs.
- **Création** (`developers/resources/user.mdx:343-358`) : `POST /users/@me/channels` avec
  `access_tokens` (scope `gdm.join`, `developers/topics/oauth2.mdx:62`) et `nicks`. « Limited to
  10 active group DMs ». « Fires a Channel Create Gateway event ». L'endpoint était destiné au
  GameBridge SDK, « now-deprecated ».
- **Modification** (`channel.mdx:409-417`) : `name` (« 1-100 character channel name ») et `icon`
  (image en base64) ; « Fires a Channel Update Gateway event ». Schéma
  `UpdateGroupDMRequestPartial` (`openapi.json:41029`).
- **`DELETE /channels/{channel_id}`** (`channel.mdx:527-534`) : « Delete a channel, or close a
  private message […] Fires a Channel Delete Gateway event ». Rien sur le group DM.
- **Destinataires** (`channel.mdx:631-647`) : `PUT …/recipients/{user_id}` avec `access_token` et
  `nick` (spec : 201 avec le salon, ou 204, `openapi.json:4462`) ; `DELETE …/recipients/{user_id}`
  (204, l. 4558).
- **Codes** (`developers/topics/opcodes-and-status-codes.mdx:205,210,288`) : `30004` « Maximum
  number of recipients reached (10) », `30011` « Maximum number of group DMs reached (10) »,
  `50033` « Invalid Recipient(s) ». Aucune page ne dit quelle route rend lequel, ni avec quel
  statut.
- **Gateway** (`developers/events/gateway-events.mdx:572-584`, `gateway.mdx:313-335,390-394`) :
  `CHANNEL_CREATE` est « sent when a new **guild** channel is created, relevant to the current
  user » ; `CHANNEL_UPDATE`, « when a channel is updated », sans autre précision ;
  `CHANNEL_DELETE`, « when a channel relevant to the current user is deleted » (l. 584). Les
  trois ne figurent que sous l'intent `GUILDS` (`gateway.mdx:320-322`) ; `DIRECT_MESSAGES` porte
  les messages et `CHANNEL_PINS_UPDATE`. Aucun `CHANNEL_RECIPIENT_ADD` ni
  `CHANNEL_RECIPIENT_REMOVE` dans les docs épinglées, et `READY` ne liste aucun salon privé.

**Contradictions et silences.**

1. `name` : 1-100 dans les docs, 0-100 et nullable dans la spec.
2. `application_id` : `?snowflake` dans les docs, non nullable dans la spec.
3. `CHANNEL_CREATE` : annoncé par « Create Group DM », réservé aux salons de guilde par la page
   de l'event.
4. La roadmap cite `UpdateDMRequestPartial` pour M1-REST-02C. Ce schéma est celui du DM 1:1
   (`name` seul, `openapi.json:40994`) ; celui du group DM est `UpdateGroupDMRequestPartial`.
5. Non documentés : l'effet du `DELETE` sur un group DM, l'`owner_id` d'un groupe créé par une
   application, ce que compte la limite de 10, les erreurs des routes de destinataires.

**Ce que NetCord expose.**

- `GroupDMChannel : DMChannel : TextChannel` (`NetCord/Channels/TextChannels/GroupDMChannel.cs`) :
  `Name` (`_jsonModel.Name!`, donc `null` sans exception si le groupe n'a pas de nom),
  `IconHash`, `OwnerId`, `ApplicationId`, `Managed` ; `DMChannel.Users` lit `recipients`.
  `Channel.CreateFromJson` rend un `GroupDMChannel` pour le type 3 (`NetCord/Channels/Channel.cs:27`).
- Routes (`NetCord/Rest/RestClient.Channel.cs:30,75,749,763`, `RestClient.User.cs:57,63`) :
  `ModifyGroupDMChannelAsync(channelId, Action<GroupDMChannelOptions>)`, `DeleteChannelAsync`,
  `GroupDMChannelAddUserAsync(channelId, userId, GroupDMChannelUserAddProperties)`,
  `GroupDMChannelDeleteUserAsync(channelId, userId)`, `GetDMChannelAsync`,
  `CreateGroupDMChannelAsync(GroupDMChannelProperties)`. Les deux routes de destinataires sont
  donc dans le périmètre de parité (`docs/parity/rest.md`, section `/channels/{id}/recipients`).
- `GroupDMChannelOptions.Name` et `.Icon` sont `WhenWritingNull`
  (`NetCord/Rest/GroupDMChannelOptions.cs:12-18`) : NetCord n'envoie jamais `null`, donc ne peut
  pas retirer une icône.
- Gateway (`NetCord/Gateway/GatewayClient.cs:1184-1203`) : `CHANNEL_CREATE`, `CHANNEL_UPDATE` et
  `CHANNEL_DELETE` passent tous par `IGuildChannel.CreateFromJson`. Un group DM y devient un
  `UnknownGuildChannel` de guilde `0`, remis aux events `GuildChannel…`, sans toucher au cache :
  ni `CacheGuildChannel` (`ImmutableGatewayClientCache.cs:207-221`) pour la création et la
  modification, ni `RemoveGuildChannel` (l. 408-423) pour `CHANNEL_DELETE`, ne trouvent la
  guilde `0`. `CHANNEL_RECIPIENT_*` tomberait dans `UnknownEvent`.

**fauxcord** crée un group DM dès que `access_tokens` est présent, sans résoudre les jetons : le
bot en est le seul destinataire (`src/services/channels.ts:593-606`). Il rend `icon: null` et un
`owner_id` de repli (l. 310-313). Ses routes de destinataires rendent 400 `50024` hors d'un
group DM, 404 `10013` pour un utilisateur inconnu au `PUT`, et un `DELETE` idempotent en 204,
sans dispatch (`src/routes/channel-recipients.ts`). Rien n'y vérifie un jeton.

## Options

### Option A — Hors périmètre

Marquer ➖ les lignes de group DM et laisser `RestEffect` simuler les refus. Rien à modéliser.
Mais NetCord expose quatre méthodes dédiées : le ➖ n'aurait pas la justification exigée par
`docs/parity/README.md`. Et le DM 1:1 resterait sans poignée.

### Option B — Création par REST, jetons OAuth2 seedés

Le seed associe un jeton d'accès à un utilisateur (`Seed.AccessToken(alice)`), puis
`CreateGroupDMChannelAsync` réussit. Le parcours réel est testable de bout en bout. Mais il faut
modéliser des grants OAuth2, que `docs/vision.md` écarte de la v1, et la mise en place du test
entre dans `Recorded.Requests`. Le DM 1:1 n'y gagne rien.

### Option C — Seed public, entité de salon unique

`discord.Seed.GroupDMChannel([alice, bob])` crée le groupe dans l'état, comme tout autre salon
seedé ; `discord.Seed.DMChannel(alice)` fait de même pour le DM 1:1. Le type 3 est une ligne de
la table de traits de l'ADR 0013 et un bloc. La création par REST reste refusée.

## Décision

**Option C.** Elle suit la règle de l'ADR 0004 (le seed met en scène, sans faux POST), donne au
DM 1:1 et au group DM **une** API, et ne modélise que ce qu'un bot peut observer.

### 1. Modèle d'état

Le group DM entre dans l'entité de salon unique de l'ADR 0013 : `ChannelKind.GroupDm = 3`, même
famille `Private` que le DM, mêmes traits (pas de groupe de tri, pas de parent, salon à
messages), plus un bloc `Group` présent si et seulement si le type est 3.

| Champ d'état | DM (1) | Group DM (3) | À la création par le seed |
| --- | --- | --- | --- |
| `RecipientIds` | l'autre partie | les participants **autres que le bot**, dans l'ordre d'arrivée | ceux fournis |
| `Name` | toujours `null` | `null` ou 1-100 | `null` |
| `LastMessageId` | oui | oui | `null` |
| `Group.OwnerId` | — | le créateur | le bot |
| `Group.IconHash` | — | `null` ou hash | `null` |
| `Group.ApplicationId` | — | l'application qui a créé le groupe | celle du fake |
| `Group.Managed` | — | créé par `gdm.join` | `true` |

- **`RecipientIds` garde son sens actuel** : ce que le champ `recipients` montre au bot. Le bot
  n'y figure jamais : sa présence est l'invariant 2 du § 2, pas une donnée, et l'invariant 3
  ferme le seul chemin qui l'y mettait, le DM avec soi-même.
- **Propriétaire, application, `managed` sont stockés, pas déduits.** Le seul group DM qu'un bot
  peut voir est celui que son application a créé par `gdm.join` : ces trois valeurs sont donc
  fixées à la création. Les stocker garde la projection JSON sans lien avec l'identité du bot, et
  corriger l'hypothèse sur `owner_id` (lot D) revient à changer une ligne de la création.
- **Non modélisés** : `nicks` (absent de l'objet salon), `last_pin_timestamp` (M1-REST-05).
  `flags` vaut toujours `0`.

Squelette interne (illustratif ; signatures seulement) :

```csharp
internal enum ChannelKind { /* … */ Dm = 1, GroupDm = 3, /* … */ }

/// <summary>What only a group DM carries; null for every other kind.</summary>
internal sealed record GroupDmSettings(ulong OwnerId, string? IconHash, ulong? ApplicationId, bool Managed);

// ChannelSnapshot gains: GroupDmSettings? Group.
// ChannelTraits.Of(GroupDm) equals ChannelTraits.Of(Dm).

internal sealed record CreateGroupDmChannelInput(
    ImmutableArray<ulong> RecipientIds, string? Name = null, ulong? Id = null);

/// <summary>A user other than the bot left or was removed from a group DM; the channel is the one after the removal.</summary>
internal sealed record ChannelRecipientRemoved(long Number, ChannelSnapshot Channel, UserSnapshot User)
    : StateChange(Number);

internal sealed partial class FakeDiscordState
{
    // No StateCaller: it receives no guild or channel ID, and the seed is its only caller.
    internal StateResult<ChannelSnapshot> CreateGroupDmChannel(CreateGroupDmChannelInput input);
    internal StateResult<ChannelSnapshot> RemoveGroupDmRecipient(StateCaller caller, ulong channelId, ulong userId);
    // GetOrCreateDmChannel: refuses the bot as the recipient (§ 2, invariant 3).
    // UpdateChannel: UpdateChannelInput gains ChangeIcon, IconHash and IconInvalid (§ 4).
    // DeleteChannel: leaves a group DM (§ 4).
}
```

### 2. Invariants

Tenus par l'état pour tout appelant, seed compris, comme ceux de l'ADR 0013 : l'état ne prend pas
une forme que Discord ne peut pas avoir.

1. **Pas de guilde.** Un salon privé n'a ni `GuildId`, ni `ParentId`, ni position, ni `topic`.
   `CreateGuildChannel` refuse toute la famille `Private` (`InvalidChannelType`), et plus
   seulement `Dm`.
2. **Le bot en fait partie.** Aucun salon privé de l'état n'existe sans le bot : le DM le suppose
   déjà (« the other party is implicitly the bot »). Créer un group DM, dont le bot est
   propriétaire, demande une identité de bot (`InvalidOperationException` sinon, comme
   `CreateGuildInput.WithBot`). Quand le bot quitte un group DM, le salon **sort de l'état** (§ 4).
3. **Destinataires.** Des utilisateurs existants, distincts, dont **aucun n'est le bot**, dans un
   DM comme dans un group DM.
   - *DM.* L'état refuse le bot comme destinataire de son propre DM, pour tout appelant. C'est
     un changement : `GetOrCreateDmChannel` l'accepte aujourd'hui (Contexte). Le refus est un
     nouveau `StateFailureKind`, `CannotMessageUser`, rendu en 403 `50007` par
     `POST /users/@me/channels` (hypothèse d'UR-003 : « DM vers soi-même ou un bot ⇒ `50007` » ;
     le statut 403 est aussi une hypothèse). Dans l'opération, il vient après `UnknownUser`,
     dont il est exclusif : le bot est un utilisateur connu. Un état sans identité de bot n'a
     rien à refuser. Un **autre** bot n'est pas concerné : UR-003 dira si Discord le refuse aussi.
   - *Group DM.* Au moins un destinataire à la création, au plus **neuf** : dix participants
     avec le bot (`30004`). Des retraits peuvent ensuite laisser le bot seul.
4. **Dix group DM au plus** à la fois (`user.mdx:349`, `30011`). Un groupe quitté ne compte plus.
5. **Nom** : `null`, ou 1 à 100 points de code Unicode, l'unité de `IsValidChannelName` pour les
   salons de guilde. C'est l'unité de toute longueur de nom dans cette ADR.
6. **Seuls les participants agissent.** L'auteur d'un message et l'utilisateur d'une réaction,
   dans un salon privé, sont le bot ou l'un de ses destinataires actuels. Un salon de guilde ne
   l'exige pas, un membre parti y gardant ses messages ; un DM, lui, est défini par ses parties.

| Faute | Invariant | Dans l'état | Par REST | Au seed |
| --- | --- | --- | --- | --- |
| Le bot comme destinataire d'un DM | 3 | `CannotMessageUser` (nouveau) | 403 `50007` | `ArgumentException` |
| Liste de destinataires qui ne décrit pas un groupe : vide, plus de neuf, doublon, le bot | 3 | `ArgumentException`, levée avant toute écriture | inatteignable | `ArgumentException` |
| Destinataire inconnu | 3 | `UnknownUser` (existant) | 404 `10013` (DM) | `InvalidOperationException` |
| Onzième group DM | 4 | `MaxGroupDms` (nouveau) | inatteignable | `InvalidOperationException` |
| Auteur ou utilisateur hors du salon | 6 | `NotChannelRecipient` (nouveau) | inatteignable | `InvalidOperationException` |

- **Un refus d'état pour le DM, une exception pour le groupe.** Le DM avec soi-même est une
  requête qu'un bot peut envoyer : il lui faut un `StateFailure` et son erreur Discord. La liste
  d'un group DM ne vient que du test : une liste mal formée est une faute d'argument, que
  l'opération lève avant toute écriture, comme `ValidateExplicitId` lève une
  `ArgumentOutOfRangeException` pour un ID explicite nul (`State/FakeDiscordState.cs:126-132`).
- **Le seed lève `ArgumentException` sans passer par l'état** pour le bot destinataire d'un DM :
  la faute se lit dans l'argument, le fake connaissant son `ApplicationId`.
- `MaxGroupDms` et `NotChannelRecipient` ne sont pas atteignables par REST en v1 : la création
  et l'ajout d'un destinataire s'arrêtent à `50025` (§ 4), et le bot est toujours participant.
  Ils n'ont donc pas encore de fabrique `DiscordError`, et `30004` et `30011`, que le fake ne
  produit pas, n'ont pas de ligne à recevoir dans `errors.md`.

### 3. Seed

Extension de l'ADR 0004, avec ses règles : commit immédiat, pas de faux POST, `id` explicite
facultatif, échec avant commit.

- **`Seed.DMChannel(userId)`** ouvre le DM du bot avec cet utilisateur, par la même opération
  d'état que la route : un utilisateur n'a jamais deux DM. Un second appel rend une poignée sur
  le même salon, et `GetDMChannelAsync` le retrouve. Avec un `id` explicite qui diffère de celui
  d'un DM déjà ouvert, l'appel lève `InvalidOperationException` au lieu d'ignorer l'argument.
  Le bot lui-même n'est pas un destinataire (`ArgumentException`, invariant 3).
- **`Seed.GroupDMChannel(recipients, name)`** crée un group DM dont le bot est propriétaire. Les
  destinataires viennent en premier : ils définissent le groupe, alors que le nom est souvent
  absent.
- **Une poignée pour les deux** : `SeededDMChannel`, avec `Id`, `Message` et `Reaction`, aux
  signatures de `SeededTextChannel`. C'est la règle de l'ADR 0013, § 6 : une poignée par ce
  qu'on peut y créer, comme `SeededTextChannel` sert aussi aux annonces.
- **Noms** : `DMChannel` et `GroupDMChannel` sont les noms des types de NetCord, avec le sigle
  en capitales que demandent les conventions .NET pour deux lettres. Les autres méthodes de seed
  de salon se terminent déjà par `Channel`.
- **Après le départ du bot**, la poignée d'un group DM désigne un salon disparu : `Message` et
  `Reaction` lèvent `InvalidOperationException`, comme sur un salon de guilde supprimé.

**Hors du seed en v1** : icône (par `ModifyGroupDMChannelAsync`), `nicks`, ajout ou retrait d'un
destinataire après coup, group DM sans le bot, option « DM refusé » d'un utilisateur ordinaire
(l'autre cas de `50007`, UR-003).

Les choix d'ergonomie de cette section (noms, poignée unique, ordre des paramètres, pas de
`withBot`, contrôle des participants) sont soumis à l'utilisateur par **UR-020**.

**Rejeté :**

- *Deux types de poignée.* Ils auraient aujourd'hui les mêmes membres. Si une opération propre
  au groupe arrive, le type de retour de `GroupDMChannel` changera avant la première publication
  (M6-05), sans effet pour un test qui écrit `var`.
- *`Dm` et `GroupDm`.* Plus courts, mais sans équivalent dans NetCord.
- *`params ulong[] recipients`.* Un `params` doit être le dernier paramètre : `name` et `id`
  devraient le précéder, et `GroupDMChannel(null, null, alice, bob)` se lit mal.
- *Un propriétaire au choix.* Un group DM possédé par un utilisateur ne contient jamais de bot.
- *`withBot: false`,* par symétrie avec `Seed.Guild`. Une guilde sans le bot sert à tester
  `50001` et l'arrivée du bot ; un group DM que le bot ne peut ni voir ni rejoindre ne sert à rien.

### 4. Visibilité et routes

**Aucune règle d'accès nouvelle.** Par l'invariant 2, tout salon privé de l'état est visible du
bot : `GuildAccess.ResolveChannel` ne change pas. Un group DM quitté est un salon inconnu.

**Le group DM suit le DM 1:1** partout où l'état teste « salon sans guilde » : messages,
réactions, pins et typing (M1-REST-05) l'acceptent ; `bulk-delete`, `crosspost` et la suppression
du message d'autrui y rendent `50003`. Les routes propres aux guildes (threads, webhooks,
invitations, permissions) lui répondent comme à un DM. Le tableau ne liste que ce qui diffère.

| Route | Sur un group DM | Source |
| --- | --- | --- |
| `GET /channels/{channel_id}` | 200, `PrivateGroupChannelResponse` (§ 6) | spec |
| `PATCH /channels/{channel_id}` | `name` et `icon` ; `ChannelUpdated` si une valeur change | `channel.mdx:409-417` |
| `DELETE /channels/{channel_id}` | le bot quitte : 200 avec le salon tel qu'il était, salon retiré de l'état avec ses messages, `ChannelDeleted` ; ensuite 404 `10003` | hypothèse |
| `PUT /channels/{channel_id}/recipients/{user_id}` | toujours refusé : 403 `50025` | hypothèse (statut : UR-003) |
| `DELETE /channels/{channel_id}/recipients/{user_id}` | 204 ; destinataire retiré, `ChannelRecipientRemoved` | spec ; détails : hypothèse |
| `POST /users/@me/channels` avec `access_tokens` | inchangé : 403 `50025` | M1-REST-01 |
| `POST /users/@me/channels` avec `recipient_id` = le bot | **nouveau** : 403 `50007`, aucun DM créé (§ 2, invariant 3) | hypothèse (UR-003) |
| `GET /guilds/{guild_id}/channels` | n'en liste aucun | `guild.mdx:884-891` |

- **`POST /users/@me/channels`, ordre des refus.** Étape structurelle (`50109`, `50035`) ; group
  DM demandé ⇒ `50025` ; puis l'état : `10013` pour un destinataire inconnu, `50007` pour le bot
  lui-même ; sinon le DM est créé ou retrouvé.
- **`PATCH`.** `topic`, `position` et les autres champs de guilde sont validés par l'étape
  structurelle, puis ignorés pour un group DM (règle de l'ADR 0013).

  | Champ | Absent | `null` | Valeur |
  | --- | --- | --- | --- |
  | `name` | inchangé | inchangé | `""` efface le nom ; 1 à 100 points de code le remplacent ; plus de 100 ⇒ `50035` structurel |
  | `icon` | inchangé | retire l'icône | image en *data URI* ⇒ hash dérivé comme pour un avatar ; toute autre valeur ⇒ `50035` |

  `name: null` suit le choix de M1-REST-02B2 (« `null` = inchangé »). NetCord ne peut envoyer ni
  ce `null` ni `icon: null` : le test de route construit ces requêtes à la main.
  Le `PATCH` d'un DM 1:1 reste sans effet (UR-004).
- **`icon` est jugée par l'état, pas par l'étape structurelle.** Ce champ n'appartient qu'au
  schéma du group DM (`UpdateGroupDMRequestPartial`), et l'étape structurelle ne connaît pas le
  type du salon. Elle **lit sans refuser** : absente, `null`, image valide (hash dérivé par
  `ImageData`, comme pour `avatar`) ou **invalide**, c'est-à-dire toute valeur qui n'est ni
  `null` ni une image en *data URI*. L'état tranche sous le verrou, sans décoder d'image :
  - sur un group DM, une icône invalide est refusée avant toute écriture : `InvalidChannelIcon`
    (nouveau `StateFailureKind`), 400 `50035`, `icon` : `IMAGE_INVALID` « Invalid image data », le
    code de champ d'`avatar` ;
  - sur tout autre salon, DM 1:1 compris, la clé est ignorée, valide ou non.

  C'est le traitement de `name` vide, que l'étape structurelle laisse passer et que seul l'état
  refuse, pour un salon de guilde seulement.

  *Rejeté :* valider `icon` à l'étape structurelle, pour tout salon. Un `PATCH` de salon de
  guilde avec une `icon` mal formée rendrait alors `50035`, pour un champ que son schéma
  (`UpdateGuildChannelRequestPartial`) ne déclare pas. La règle de l'ADR 0013, « validé avec le
  schéma commun, puis ignoré », vaut pour les champs de ce schéma, pas pour un champ qui lui est
  étranger. Prix du choix retenu : un `StateFailureKind` et un indicateur de plus, et un refus
  qui arrive après `Intercept` sans se réunir avec les erreurs structurelles dans un même arbre
  `errors` (limite déjà connue des refus d'état, UR-004 point 5).
- **`DELETE` du salon.** Le DM 1:1 se « ferme » sans rien changer ; le group DM, lui, est quitté
  pour de bon, et le fake n'offre aucun moyen d'y revenir. Le salon part comme un salon de
  guilde supprimé : ses messages avec lui, sans fait par message, son ID jamais réutilisé.
- **`PUT` d'un destinataire.** Dans l'ordre du pipeline de l'ADR 0005 :
  1. **Étape structurelle**, avant tout accès à l'état. `channel_id` et `user_id` sont des
     Snowflakes (`50035`, `NUMBER_TYPE_COERCE`). Le corps est requis par la spec
     (`openapi.json:4487`) : **absent** ou non JSON ⇒ 400 `50109`, la réponse de
     `FormValidation.TryReadJsonObject` pour toute route à corps ; JSON qui n'est pas un objet ⇒
     `50035` `MODEL_TYPE_CONVERT`. `access_token` et `nick` sont chacun une chaîne d'au plus
     152 133 caractères, `null` ou absents (`50035`, `BASE_TYPE_BAD_TYPE` ou
     `BASE_TYPE_BAD_LENGTH`). Un objet vide `{}` est donc bien formé.
  2. Accès au salon par le helper : 404 `10003`, ou 403 `50001` pour un salon d'une guilde sans
     le bot.
  3. Salon qui n'est pas un group DM ⇒ 400 `50024`.
  4. Utilisateur inconnu ⇒ 404 `10013`.
  5. 403 `50025`, quel que soit `access_token`, `null` ou absent compris : aucun jeton ne peut
     être valide sans grant OAuth2.

  La ligne de parité restera 🟡 : le succès n'est pas simulable (UR-021).
- **`DELETE` d'un destinataire.** Pas de corps : l'étape structurelle ne contrôle que les deux
  Snowflakes de la route. Puis même accès, même `50024` hors d'un group DM. `user_id` qui n'est
  pas destinataire, inconnu compris ⇒ 204, rien n'est écrit. `user_id` = le bot ⇒ 204, et le bot
  quitte le groupe comme par `DELETE /channels/{channel_id}`. Les messages du destinataire
  retiré restent.
- **Interactions.** `Seed.DMChannel(alice).Id` est le salon qu'attend l'ADR 0007 pour une
  interaction en DM. Une requête qui vise un group DM lève `InvalidOperationException` : son
  `context` (`BOT_DM` ou `PRIVATE_CHANNEL`) dépend du modèle d'installation de `M2-13`.

**Rejeté : garder le salon dans l'état après le départ du bot**, avec un drapeau et un refus
403 `50001`, sur le modèle des guildes de l'amendement 04B. Une guilde sans le bot se seede, se
rejoint et se teste ; un group DM quitté n'a plus aucun usage. Le drapeau demanderait une règle
d'accès, un filtre de dispatch et un fait de plus, pour ne changer qu'un code d'erreur que les
sources ne donnent pas. Si Discord rend `50001` (lot D), une liste des groupes quittés suffira.

Le périmètre sans OAuth2 et le départ définitif du bot sont soumis à l'utilisateur par **UR-021**.

### 5. Faits et dispatchs attendus

Les faits sont décidés ici. Leur traduction est celle que M3-03 devra implémenter ; la barrière
de livraison relève de M3-ADR-09. Shard `0`, comme tout salon sans guilde.

| Mutation | Fait | Dispatch attendu | Intent |
| --- | --- | --- | --- |
| `Seed.GroupDMChannel` | `ChannelCreated` | `CHANNEL_CREATE` (`user.mdx:346`) | `GUILDS` |
| `PATCH` : `name`, `icon` | `ChannelUpdated` si une valeur change | `CHANNEL_UPDATE` (`channel.mdx:412`) | `GUILDS` |
| Le bot quitte le groupe | `ChannelDeleted`, salon tel qu'il était | `CHANNEL_DELETE` (hypothèse) | `GUILDS` |
| Retrait d'un autre destinataire | `ChannelRecipientRemoved` | **aucun** | — |
| `Seed.DMChannel`, premier appel | `ChannelCreated` | aucun (inchangé : M3-03 l'ignore pour un DM) | — |
| Message, réaction | faits existants, `GuildId` nul | ceux d'un DM | `DIRECT_MESSAGES`, `DIRECT_MESSAGE_REACTIONS` |

- **Un fait propre au retrait**, plutôt que `ChannelUpdated` : M3-03 traduit `ChannelUpdated` en
  `CHANNEL_UPDATE`, que Discord n'annonce pas pour un changement de destinataires. Le fait existe
  pour qu'un `CHANNEL_RECIPIENT_REMOVE` puisse en être tiré si Discord le documente un jour.
- **Pas de `ChannelRecipientAdded`** : aucune opération n'ajoute un destinataire après la création.
- **Intent : `GUILDS`, comme le disent les docs.** `CHANNEL_CREATE`, `CHANNEL_UPDATE` et
  `CHANNEL_DELETE` ne figurent que sous cet intent (`gateway.mdx:320-322`), sans exception pour
  un salon sans guilde : le fake les conditionne donc à `GUILDS`, group DM compris.
  *Alternative non retenue :* `DIRECT_MESSAGES`, par analogie avec `CHANNEL_PINS_UPDATE`, que
  les docs rangent sous les deux intents (`gateway.mdx:323,394`) selon que le salon a une guilde
  ou non. L'analogie est plausible, mais aucune source ne l'étend à ces trois events. La
  question est au lot D ; la changer revient à changer une ligne de la table d'intents de M3-03.
- **Côté NetCord**, ces trois dispatchs arrivent par `GuildChannelCreate`, `GuildChannelUpdate`
  et `GuildChannelDelete` avec un `UnknownGuildChannel` de guilde `0`. C'est ce que NetCord fait
  du payload de Discord ; le fake n'y adapte rien.

### 6. Forme JSON

Un DTO interne de plus dans `Serialization/`, `PrivateGroupChannelResponse`, enregistré dans
`FakeDiscordJsonContext` ([ADR 0006](0006-wire-serialization.md), règles 1, 2 et 4).

| Champ JSON | Présence | Valeur |
| --- | --- | --- |
| `id`, `type` | requis | `type` = `3` |
| `flags` | requis | `0` |
| `recipients` | requis | les utilisateurs de `RecipientIds`, dans l'ordre, sans le bot ; `[]` si le bot est seul |
| `name`, `icon` | requis, nullables | `null` explicite |
| `owner_id` | requis | `Group.OwnerId` |
| `last_message_id` | toujours écrit, nullable | comme `PrivateChannelResponse` |
| `last_pin_timestamp` | comme `PrivateChannelResponse` | M1-REST-05 |
| `managed` | toujours écrit | `Group.Managed` |
| `application_id` | écrit s'il est défini | la spec ne l'admet pas `null` |

**Union fermée des salons.** `GET`, `PATCH` et `DELETE /channels/{channel_id}` rendent le `oneOf`
`GuildChannelResponse | PrivateChannelResponse | PrivateGroupChannelResponse | ThreadResponse`.
Le rendu choisit la variante par `Kind`, discriminant `type` de Discord (règle 5), et sérialise
avec le `JsonTypeInfo` de cette variante : famille de guilde ⇒ DTO de l'ADR 0013, `Dm` ⇒
`PrivateChannelResponse`, `GroupDm` ⇒ `PrivateGroupChannelResponse`. Le test `GuildId is null`
de `ChannelRoutes.Render` ne suffit plus. Aucune route ne rend une liste de salons privés : pas
de convertisseur fermé à ajouter, et `GuildChannelUnion` ne change pas. Le dispatch `CHANNEL_*`
d'un group DM porte le même DTO, sans `guild_id`.

### Hypothèses à confirmer (lot D)

Les sources ne tranchent pas ces points. Le fake implémente la réponse indiquée ; les lignes de
parité restent 🟡 « à confirmer (UR-022) » tant que le test manuel **UR-022** n'a pas été fait
(tâche `PAR-REST-05`). Ce test demande un parcours OAuth2 `gdm.join` avec au moins un compte
utilisateur : il est coûteux, et l'utilisateur peut choisir de laisser ces lignes à 🟡.

- **Objet** : `owner_id` = le bot, `managed: true`, `application_id` = l'application ;
  `recipients` sans le bot ; `name: null` pour un groupe sans nom.
- **Limites** : dix participants **bot compris** ; dix group DM « actifs » = dix groupes dont le
  bot fait partie.
- **`PATCH`** : `name: ""` efface le nom, `name: null` ne change rien, `icon: null` retire
  l'icône ; une icône invalide ⇒ `50035` `IMAGE_INVALID` sur un group DM, ignorée sur un salon
  de guilde ; les champs de guilde sont ignorés.
- **`DELETE` du salon** : le bot quitte le groupe ; réponse 200 avec le salon ; accès ultérieur
  ⇒ 404 `10003` (et non 403 `50001`).
- **Destinataires** : corps absent du `PUT` ⇒ `50109` ; `50024` hors d'un group DM (fauxcord),
  `10013` au `PUT` d'un inconnu, `50025` pour un jeton invalide, `null` ou absent, et l'ordre de
  ces refus (structurel, accès, type, utilisateur, jeton) ; `DELETE` d'un non-destinataire en
  204 ; `DELETE` du bot lui-même = départ.
- **Gateway** : `CHANNEL_CREATE`, `CHANNEL_UPDATE` et `CHANNEL_DELETE` sont-ils envoyés pour un
  group DM ? Sous quel intent : `GUILDS` (défaut du fake, lecture des docs) ou `DIRECT_MESSAGES`
  (alternative du § 5) ? Aucun dispatch au retrait d'un destinataire.
- **Message d'autrui** : `50003` à sa suppression dans un group DM dont le bot est propriétaire
  (même hypothèse que pour le DM, UR-011).

Hors de ce lot : le `50007` du DM avec soi-même (§ 2, invariant 3) relève d'**UR-003**, qui le
teste déjà et ne demande pas OAuth2.

## API publique proposée

```csharp
namespace Enx.NetCord.Testing;

public sealed class DiscordSeed
{
    // Guild, User: unchanged (ADR 0004).

    /// <summary>Opens the DM channel between the bot and a user, or finds the one already open; returns its handle.</summary>
    /// <param name="userId">The ID of a user of this fake, other than the bot.</param>
    /// <param name="id">An explicit nonzero ID for a new channel, or <see langword="null"/> to allocate one.</param>
    public SeededDMChannel DMChannel(ulong userId, ulong? id = null);

    /// <summary>Creates a group DM owned by the bot and returns its handle.</summary>
    /// <param name="recipients">One to nine distinct users of this fake, other than the bot.</param>
    /// <param name="name">The group name (1 to 100 Unicode code points), or <see langword="null"/> for an unnamed group.</param>
    /// <param name="id">An explicit nonzero ID, or <see langword="null"/> to allocate one.</param>
    public SeededDMChannel GroupDMChannel(IEnumerable<ulong> recipients, string? name = null, ulong? id = null);
}

/// <summary>Identifies a seeded DM or group DM channel and creates message fixtures in it.</summary>
/// <remarks>Only the bot and the current recipients of the channel can author a message or react.</remarks>
public sealed class SeededDMChannel
{
    /// <summary>Gets the channel ID.</summary>
    public ulong Id { get; }
    /// <summary>Creates a message by the bot or a recipient and returns the message ID.</summary>
    public ulong Message(string content, ulong authorId, ulong? id = null);
    /// <summary>Makes the bot or a recipient react to a message of this channel.</summary>
    public void Reaction(ulong messageId, string emoji, ulong userId, bool burst = false);
}
```

Constructeurs non publics. Exceptions :

| Exception | `DMChannel` | `GroupDMChannel` | `Message`, `Reaction` |
| --- | --- | --- | --- |
| `ArgumentNullException` | — | `recipients` | `content`, `emoji` |
| `ArgumentException` | `userId` est le bot | aucun destinataire, plus de neuf, doublon, le bot | `emoji` mal formé |
| `ArgumentOutOfRangeException` | `id` nul | `id` nul ; `name` vide ou de plus de 100 points de code | `id` nul |
| `InvalidOperationException` | utilisateur inconnu ; ID déjà attribué ; `id` différent de celui du DM déjà ouvert | utilisateur inconnu ; ID déjà attribué ; dix group DM existent | salon disparu ; auteur ou utilisateur qui n'en est pas participant ; message inconnu ; ID déjà attribué |
| `ObjectDisposedException` | fake disposé | fake disposé | fake disposé |

Exemples d'usage dans des tests (pseudo-code) :

```csharp
async Task SeedDMChannel_UserMessageAndReaction_AreReadByTheBot()
{
    await using var discord = FakeDiscord.Create();
    var alice = discord.Seed.User("alice");
    var dm = discord.Seed.DMChannel(alice);
    var hello = dm.Message("hello", alice);
    dm.Reaction(hello, "👍", alice);                       // the reaction of someone else (M1-REST-04B)
    using var rest = discord.CreateRestClient();

    Assert.Empty(discord.Recorded.Requests);              // the fixture left no request behind
    Assert.Equal(dm.Id, (await rest.GetDMChannelAsync(alice)).Id);
    Assert.Equal("hello", (await rest.GetMessageAsync(dm.Id, hello)).Content);
    Assert.Contains(await rest.GetMessageReactionsAsync(dm.Id, hello, "👍").ToListAsync(), u => u.Id == alice);

    Assert.Equal(dm.Id, discord.Seed.DMChannel(alice).Id);                        // one DM per user
    var carol = discord.Seed.User("carol");
    Assert.Throws<InvalidOperationException>(() => dm.Message("intruder", carol)); // not a party of this DM
}

async Task DMChannel_WithTheBotItself_IsRefused()
{
    await using var discord = FakeDiscord.Create();
    using var rest = discord.CreateRestClient();

    Assert.Throws<ArgumentException>(() => discord.Seed.DMChannel(discord.ApplicationId));
    var refused = await Assert.ThrowsAsync<RestException>(() => rest.GetDMChannelAsync(discord.ApplicationId));
    Assert.Equal((HttpStatusCode.Forbidden, 50007), (refused.StatusCode, refused.Error!.Code));
}

async Task SeedGroupDMChannel_IsReadAndRenamed()
{
    await using var discord = FakeDiscord.Create();
    var alice = discord.Seed.User("alice");
    var bob = discord.Seed.User("bob");
    var group = discord.Seed.GroupDMChannel([alice, bob]);
    using var rest = discord.CreateRestClient();

    var read = Assert.IsType<GroupDMChannel>(await rest.GetChannelAsync(group.Id));
    Assert.Equal((discord.ApplicationId, true), (read.OwnerId, read.Managed));
    Assert.Equal(2, read.Users.Count);
    Assert.DoesNotContain(discord.ApplicationId, read.Users.Keys);   // the bot is not one of its own recipients
    Assert.Null(read.IconHash);

    var renamed = (GroupDMChannel)await rest.ModifyGroupDMChannelAsync(group.Id, o => o.Name = "Raid night");
    Assert.Equal("Raid night", renamed.Name);
    // With a connected client (M3): CHANNEL_UPDATE.
}

async Task GroupDMChannel_Recipients_AreRemovedButNeverAdded()
{
    await using var discord = FakeDiscord.Create();
    var alice = discord.Seed.User("alice");
    var bob = discord.Seed.User("bob");
    var carol = discord.Seed.User("carol");
    var group = discord.Seed.GroupDMChannel([alice, bob], name: "Raid");
    using var rest = discord.CreateRestClient();

    await rest.GroupDMChannelDeleteUserAsync(group.Id, bob);
    Assert.Equal(alice, Assert.Single(((GroupDMChannel)await rest.GetChannelAsync(group.Id)).Users.Keys));

    // The fake has no OAuth2 grant: no access token can add a recipient.
    var refused = await Assert.ThrowsAsync<RestException>(
        () => rest.GroupDMChannelAddUserAsync(group.Id, carol, new GroupDMChannelUserAddProperties("token")));
    Assert.Equal((HttpStatusCode.Forbidden, 50025), (refused.StatusCode, refused.Error!.Code));
}

async Task DeleteChannel_GroupDM_BotLeavesForGood()
{
    await using var discord = FakeDiscord.Create();
    var alice = discord.Seed.User("alice");
    var group = discord.Seed.GroupDMChannel([alice], name: "Raid");
    using var rest = discord.CreateRestClient();

    var left = Assert.IsType<GroupDMChannel>(await rest.DeleteChannelAsync(group.Id));
    Assert.Equal("Raid", left.Name);

    var gone = await Assert.ThrowsAsync<RestException>(() => rest.GetChannelAsync(group.Id));
    Assert.Equal((HttpStatusCode.NotFound, 10003), (gone.StatusCode, gone.Error!.Code));
    Assert.Throws<InvalidOperationException>(() => group.Message("late", alice));
    // With a connected client (M3): CHANNEL_DELETE.
}
```

## Conséquences

- **Positif.** Le DM 1:1 et le group DM ont une même API de seed. M1-REST-04B se réduit à
  `SeededDMChannel.Reaction`. Un test d'interaction en DM obtient son salon sans requête REST.
  Le group DM coûte une ligne de traits, un bloc et un DTO : ni règle d'accès, ni changement de
  `GuildAccess`, ni changement du sens de `RecipientIds`.
- **Négatif.** Le fake ne représente que le group DM vu par son bot propriétaire : pas de groupe
  sans le bot, pas de retour après un départ, pas de création ni d'ajout par REST. Presque tout
  le comportement repose sur des hypothèses (lot D), dont la vérification demande OAuth2.
- **Surface publique.** Un type de plus (`SeededDMChannel`) et deux méthodes sur `DiscordSeed` :
  des ajouts, compatibles source et binaire.
- **Changement de comportement : le DM avec soi-même.** `POST /users/@me/channels` avec l'ID du
  bot rendait un DM ; il rend 403 `50007` (§ 2, invariant 3). Aucun test du dépôt n'en dépend :
  le seul qui passe l'ID du bot à `GetDMChannelAsync`
  (`CurrentUserRoutesTests.UsersRoutes_WithoutCredentialsOrWithAnUnknownToken_Return401`)
  s'arrête au 401.
- **Refonte interne.** `ChannelKind.GroupDm`, `GroupDmSettings`, `ChannelSnapshot.Group`,
  `UpdateChannelInput.ChangeIcon`, `IconHash` et `IconInvalid`, leur pendant dans
  `ChannelPatch`, rendu par `Kind`. Quatre `StateFailureKind` : `CannotMessageUser`,
  `InvalidChannelIcon`, `MaxGroupDms`, `NotChannelRecipient` ; les deux premiers ont une
  fabrique `DiscordError`. Le commentaire du fait `ChannelDeleted` (« a guild channel was
  deleted ») s'élargit au group DM quitté. Le contrôle de l'invariant 6 touche `CreateMessage`
  et `AddReaction` : les tests d'état qui écrivent dans un DM au nom d'un tiers sont à ajuster.
- **Pas d'effet de bord sur les salons de guilde** : `icon` y reste ignorée, valide ou non
  (§ 4). Aucun point utilisateur n'est donc nécessaire à ce sujet.
- Ni dépendance, ni réflexion ; le DTO passe par `FakeDiscordJsonContext`.

**Découpage de M1-REST-02C** (à remplacer par les deux premières lignes) et tâches à créer :

- `M1-REST-02C1` — État, seed et lecture : § 1, § 2, § 3 et § 6 ; `GET /channels/{channel_id}`
  d'un group DM ; refus de la famille `Private` par `CreateGuildChannel` ; **refus du bot comme
  destinataire d'un DM** (`CannotMessageUser`, fabrique `DiscordError` 403 `50007`, branchée
  sur `POST /users/@me/channels` ; lignes `50007` de `errors.md` et `POST /users/@me/channels`
  de `rest.md`) — rest-implementer — M1-ADR-04C, M1-REST-04. **Absorbe M1-REST-04B.** Touche
  `State/` et `Seed/` : à lancer après la fusion de M1-REST-05 et de M1-REST-07, en cours sur
  ces dossiers.
- `M1-REST-02C2` — Routes du group DM : `PATCH` (`name`, `icon`, refus `InvalidChannelIcon`) et
  `DELETE` de `/channels/{channel_id}`, `PUT` et `DELETE …/recipients/{user_id}`, fait
  `ChannelRecipientRemoved`, lignes de `rest.md` et de `errors.md` : `50024`, `50025`, `10003`
  (group DM quitté), `10013` (`PUT` d'un utilisateur inconnu), `50035` (`IMAGE_INVALID` sur
  `icon` du `PATCH` ; champs du corps et de la route du `PUT`), `50109` (corps absent du `PUT`)
  — rest-implementer — M1-REST-02C1.
- `PAR-REST-05` — Confirmer contre Discord le lot D, puis aligner fake, tests et matrices —
  test manuel UR-022, puis parity-auditor — M1-REST-02C2.

**Tâches existantes impactées.**

- **M1-REST-02C** : remplacée par les deux lignes ci-dessus ; son libellé citait
  `UpdateDMRequestPartial` au lieu de `UpdateGroupDMRequestPartial`.
- **M1-REST-04B** : absorbée par M1-REST-02C1.
- **PAR-REST-01** (UR-003) : le `50007` du DM avec soi-même devient un comportement du fake à
  confirmer, et non plus un manque ; le cas d'un autre bot reste ouvert.
- **M1-REST-05** (en cours) : pins et typing traitent un group DM comme un DM. Rien à changer si
  la tâche teste « salon sans guilde ».
- **M2-01, M2-11** : `Seed.DMChannel` fournit le salon d'une interaction en DM ; un group DM est
  refusé localement. **M2-13** décide de son `context`.
- **M3-ADR-09, M3-03** : § 5. M3-03 traduit le `ChannelCreated` d'un group DM, continue
  d'ignorer celui d'un DM, ne dispatche rien pour `ChannelRecipientRemoved`, et conditionne les
  trois `CHANNEL_*` d'un group DM à l'intent `GUILDS`.

**Parité.** Prépare, sans les promouvoir :

- `rest.md` :
  - `GET`, `PATCH` et `DELETE /channels/{channel_id}` (le « Manque : group DM ») ;
  - `PUT` et `DELETE /channels/{channel_id}/recipients/{user_id}` (la première restera 🟡) ;
  - `POST /users/@me/channels` : group DM seedé, création toujours refusée ; le « Manque : DM
    vers soi-même » devient « 403 `50007`, à confirmer (UR-003) », celui d'un autre bot reste.
- `errors.md` :
  - `50007` : ⬜ → 🟡 à la livraison de M1-REST-02C1, contexte « DM avec soi-même », statut 403
    et code à confirmer (UR-003) ; l'option de seed « DM refusé » reste à faire ;
  - `50024` et `50025` : nouveaux contextes (routes de destinataires) ;
  - `10003` : group DM quitté ; `10013` : `PUT` d'un utilisateur inconnu ;
  - `50035` : `IMAGE_INVALID` sur `icon` du `PATCH` d'un group DM, et les champs du `PUT` ;
    `50109` : corps absent ou non JSON du `PUT` ;
  - `30004`, `30011` et `50033` n'y ont pas de ligne et n'en reçoivent pas : le fake ne les
    produit pas.
- `gateway.md` : `CHANNEL_CREATE`, `CHANNEL_UPDATE`, `CHANNEL_DELETE` (cas du group DM, intent
  à confirmer).

Aucune ligne promue ni exclue, aucune matrice modifiée par cette ADR.
