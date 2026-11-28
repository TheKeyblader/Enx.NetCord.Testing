# 0013 — Hiérarchie et types de salons dans l'état

- **Statut** : Acceptée
- **Date** : 2026-11-28
- **Auteur** : agent `architect`
- **Tâches liées** : M1-ADR-13, M1-REST-02, M1-REST-02B, M1-REST-02D, M1-REST-03, M1-REST-06,
  M3-02, M3-03, M4-REST-01, M4-REST-05 ; proposées : M1-REST-02B1, M1-REST-02B2, M1-REST-03B,
  M4-REST-10, PAR-REST-02

## Contexte

M1-REST-02 a livré `GET`/`PATCH`/`DELETE /channels/{channel_id}` pour deux sortes de salons :
texte de guilde et DM. L'état sur `main` le reflète : `ChannelKind { GuildText = 0, Dm = 1 }`,
une `ChannelEntity` à plat, `CreateTextChannelInput`, une position par défaut égale au nombre de
salons de la guilde, un `UpdateChannel` qui refuse **tout** parent et tout changement de type,
un listing dans l'ordre de création (`State/{Snapshots,Entities,Inputs}.cs`,
`State/FakeDiscordState.cs:467-500,563-575`, `State/FakeDiscordState.Channels.cs`). Le seed ne
connaît que `SeededGuild.TextChannel`. Catégories, salons vocaux, annonces, forums, positions
(M1-REST-06) et threads (M4-REST-01) ont besoin d'un modèle commun avant d'être codés.

Sources épinglées : `discord-api-docs` @ `62ab2d50`, `discord-api-spec` @ `65423001`,
`netcord` @ `bb5f4e13` (tag `1.0.0-beta.30`), `fauxcord` @ `70563d51`.

**Ce que Discord documente.**

- **Types** (`developers/resources/channel.mdx:66-80`) : 0 texte, 1 DM, 2 voix, 3 group DM,
  4 catégorie, 5 annonces, 10/11/12 threads, 13 stage, 14 annuaire, 15 forum, 16 media
  (« still in active development », l. 82).
- **Un seul schéma** pour tous les salons de guilde : `GuildChannelResponse`
  (`specs/openapi.json:27326`), dont seuls `id`, `type`, `flags`, `guild_id`, `name` et
  `position` sont requis. Les champs propres à un type sont donnés par la colonne « Channel
  Type » des tableaux de `Modify Channel` (`channel.mdx:424-444`) et de `Create Guild Channel`
  (`developers/resources/guild.mdx:909-929`).
- **`topic`** : 0-1024, ou 0-4096 pour forum et media (`channel.mdx:24,429`). La spec ne porte
  que la borne haute, 4096, pour tous les types (`UpdateGuildChannelRequestPartial`,
  `openapi.json:41049` ; `CreateGuildChannelRequest`, l. 24230).
- **`parent_id`** : « id of the parent category », au plus 50 salons par catégorie
  (`channel.mdx:35,72`) ; applicable à texte, voix, annonces, stage, forum, media (l. 435), donc
  pas à une catégorie, dont l'exemple porte `"parent_id": null` (l. 251-262).
- **Suppression d'une catégorie** : « Deleting a category does not delete its child channels;
  they will have their `parent_id` removed and a Channel Update Gateway event will fire for
  each of them » (`channel.mdx:530`).
- **`position`** : « sorting position of the channel (channels with the same position are sorted
  by id) » (`channel.mdx:21,428`). `PATCH /guilds/{guild_id}/channels` prend un tableau
  `{id, position?, lock_permissions?, parent_id?}`, rend 204 et « fires multiple Channel Update
  Gateway events » ; au plus une entrée peut changer `parent_id`, sinon 400 `40009`
  (`guild.mdx:935-972`).
- **Changement de type** : « only conversion between text and announcement is supported and only
  in guilds with the "NEWS" feature » (`channel.mdx:427`, `guild.mdx:163`).
- **Listing** : `GET /guilds/{guild_id}/channels` « does not include threads » et omet les salons
  que le bot ne peut pas voir (`guild.mdx:884-891`). Aucun ordre n'est documenté.
- **Gateway** : `CHANNEL_CREATE`, `CHANNEL_UPDATE`, `CHANNEL_DELETE` portent un objet salon ;
  `CHANNEL_UPDATE` « is not sent when the field `last_message_id` is altered »
  (`developers/events/gateway-events.mdx:572-584`).
- **Codes JSON documentés** (`developers/topics/opcodes-and-status-codes.mdx`) : `30013` (500
  salons, l. 211), `30052` (bitrate, l. 231), `40009` (l. 244), `40033` (l. 249), `40058`
  (l. 252), `50008` (l. 270), `50024` (l. 283), `50074` (l. 303). **Aucun** ne décrit un parent
  invalide ni une catégorie pleine : ces refus sont des `50035`, dont les codes de champ ne sont
  pas documentés.

**Contradiction spec ↔ docs.** Le type 16 figure dans les docs et dans NetCord
(`NetCord/ChannelType.cs:71`, `MediaForumGuildChannel`), mais ni dans `ChannelTypes`
(`openapi.json:23502`) ni dans l'énumération `[0, 2, 4, 5, 13, 14, 15]` des schémas de salon.

**Ce que NetCord expose.**

- Un `JsonChannel` unique, tout en nullable (`NetCord/JsonModels/JsonChannel.cs`), typé par
  `IGuildChannel.CreateFromJson` (`NetCord/Channels/IGuildChannel.cs:29-47`). `VoiceGuildChannel`
  et `StageGuildChannel` dérivent de `TextGuildChannel` : ce sont des salons à messages.
  `ForumGuildChannel` dérive de `Channel`, `MediaForumGuildChannel` de `ForumGuildChannel`,
  `AnnouncementGuildChannel` de `TextGuildChannel`. `IGuildChannel.Position` est un `int?`.
- Routes : `GetChannelAsync`, `ModifyGuildChannelAsync(channelId, Action<GuildChannelOptions>)`,
  `DeleteChannelAsync` (`NetCord/Rest/RestClient.Channel.cs:19,46,75`) ; `GetGuildChannelsAsync`,
  `CreateGuildChannelAsync(guildId, GuildChannelProperties)`,
  `ModifyGuildChannelPositionsAsync(guildId, IEnumerable<GuildChannelPositionProperties>)`
  (`NetCord/Rest/RestClient.Guild.cs:37-51`).
- **NetCord n'envoie jamais `null`** : toutes les propriétés de `GuildChannelOptions` et de
  `GuildChannelPositionProperties` sont `WhenWritingNull` (`NetCord/Rest/GuildChannelOptions.cs:12-50`,
  `NetCord/Rest/ChannelPositionProperties.cs:21-37`). Un bot NetCord ne peut donc ni sortir un
  salon de sa catégorie, ni envoyer `topic: null`. Le fake accepte quand même ces deux requêtes :
  la référence est Discord.
- Gateway : `CHANNEL_CREATE` et `CHANNEL_UPDATE` remplacent l'entrée du cache, `CHANNEL_DELETE`
  la retire, en lisant `guild_id` dans le payload (`NetCord/Gateway/GatewayClient.cs:1184-1203`).

**fauxcord n'est pas une référence ici.** `parent_id` y est une colonne sans règle
(`src/db.ts:113-133`), la suppression ne détache pas les enfants
(`src/services/channels.ts:430-449`), la position par défaut est le nombre de salons
(`src/routes/guilds.ts:140-148`), le listing est trié par `position, id`
(`src/services/channels.ts:457-476`), et le `PATCH` de positions ne peut pas remettre un parent à
`null` ni ne dispatche (`src/routes/guild-advanced.ts:414-439`).

**Sources non épinglées**, consultées le 2026-11-28 :

- discord.js (`packages/discord.js/src/util/Util.js`, branche `main`) trie par **groupes** :
  texte, annonces, forum et media ensemble (« always at the top »), voix et stage ensemble
  (« always at the bottom »), catégories entre elles (`getSortableGroupTypes`). Son `setPosition`
  envoie lui-même `{id, position}` pour **tous** les salons du groupe, ce qui suggère que le
  serveur ne renumérote pas les voisins. Son `discordSort` (« Sorts by Discord's position and
  id ») re-trie les salons par position puis par ID, sans se fier à l'ordre reçu.
- Une question publique (Stack Overflow 68512655) cite la réponse de Discord à un parent
  inexistant : `Invalid Form Body — parent_id: Category does not exist`.

## Options

### Option A — Entité à plat, tous les champs facultatifs

Prolonger `ChannelEntity` : `ParentId`, `Bitrate`, `UserLimit`, `RtcRegion`, tags… en nullable,
comme `JsonChannel`. Rien à restructurer. Mais rien ne dit quels champs valent pour quel type :
chaque handler, chaque projection JSON et le seed refont le tri à la main, et un `bitrate` peut
se retrouver sur un salon texte.

### Option B — Une classe d'entité par type

`TextChannelEntity`, `VoiceChannelEntity`, `CategoryEntity`…, comme la hiérarchie publique de
NetCord. Le compilateur garantit les champs. En contrepartie, une conversion texte ↔ annonces
remplace l'objet sous le même ID ; `GuildAccess.ResolveChannel`, `StateProjection.Channels` et
les faits `ChannelCreated`/`ChannelUpdated`/`ChannelDeleted` deviennent polymorphes, ce que
l'[ADR 0003](0003-state-model-and-concurrency.md) écarte pour les faits (« pas de cast
dynamique ») ; et le fil n'a qu'un schéma.

### Option C — Entité unique, blocs par famille, table de traits

Une `ChannelEntity`, un `ChannelSnapshot`. Les champs communs restent à plat ; les champs d'une
**famille** vivent dans un bloc présent si et seulement si le type en relève (`Voice`, `Forum`,
plus tard `Thread`). Une table statique dit, par type, ce qui s'applique.

## Décision

**Option C.** Elle garde un seul type à résoudre, à projeter et à transporter dans les faits,
suit la forme du schéma Discord, et concentre le savoir « quel champ pour quel type » en un
endroit que l'état, le fil et le seed consultent.

### 1. Types et modèle d'état

| Type | Nom Discord | Dans l'état | Famille | Groupe de tri | Parent | Messages |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | `GUILD_TEXT` | fait (M1-REST-02) | texte | textuel | oui | oui |
| 5 | `GUILD_ANNOUNCEMENT` | M1 | texte | textuel | oui | oui |
| 2 | `GUILD_VOICE` | M1 | voix | vocal | oui | oui |
| 13 | `GUILD_STAGE_VOICE` | M1 | voix | vocal | oui | oui |
| 4 | `GUILD_CATEGORY` | M1 | catégorie | catégorie | non | non (`50008`) |
| 15 | `GUILD_FORUM` | M1, sans tags | forum | textuel | oui | non (`40058`) |
| 16 | `GUILD_MEDIA` | M1, sans tags | forum | textuel | oui | non (`40058`, hypothèse) |
| 10, 11, 12 | threads | M4-REST-01 | — | — | salon d'origine | oui |
| 3 | `GROUP_DM` | [ADR 0014](0014-private-channels-dm-and-group-dm.md) (proposée) | privée, comme le DM | — | — | oui |
| 14 | `GUILD_DIRECTORY` | non modélisé | — | — | — | — |

- **Type 16 retenu** malgré son absence de la spec : les docs le décrivent, NetCord le type, et
  il a la forme d'un forum. Le validateur structurel de `type` accepte donc `16` en plus de
  l'énumération de la spec.
- **Type 14 écarté** : il n'existe que dans un hub étudiant, qu'un bot ne crée pas. Le demander,
  à la création comme par conversion, est refusé comme un type invalide (§ 5). Le type 21 de
  NetCord (`ApplicationGuildChannel`, `ChannelType.cs:76`) n'est ni dans les docs ni dans la
  spec : ignoré.
- **Les groupes de tri** viennent du comportement des clients (discord.js), pas des docs :
  **hypothèse** (lot B des [hypothèses à confirmer](#hypothèses-à-confirmer)).

**Champs.** Défauts à la création quand rien n'est fourni.

| Champ d'état | Familles | Défaut | Bornes |
| --- | --- | --- | --- |
| `Name`, `Position`, `ParentId`, `Flags` | toutes (`ParentId` toujours nul pour une catégorie ; `Flags` toujours `0` en stage et en catégorie) | —, § 4, `null`, `0` | nom 1-100 |
| `Topic` | texte, forum | `null` | 1024 ; 4096 en forum |
| `Nsfw` | texte, voix, forum | `false` | — |
| `RateLimitPerUser` | texte (type 0 seulement), voix, forum | `0` | 0-21600 |
| `LastMessageId` | texte, voix, forum (dernier thread, M4) | `null` | — |
| `DefaultAutoArchiveDuration` | texte, forum | non défini | 60, 1440, 4320, 10080 |
| `DefaultThreadRateLimitPerUser` | texte (annonces comprises, voir ci-dessous), forum | `0` | 0-21600 |
| `Voice.Bitrate` | voix | `64000` | 8000-96000 ; 64000 en stage |
| `Voice.UserLimit` | voix | `0` | 0-99 ; 0-10000 en stage |
| `Voice.RtcRegion`, `Voice.VideoQualityMode` | voix | `null`, non défini | — ; 1 ou 2 |
| `Forum.DefaultSortOrder`, `Forum.DefaultForumLayout` | forum (layout : type 15) | `null`, `0` | 0-1 ; 0-2 |

Sources : `channel.mdx:424-446`. Le défaut `64000` est celui de l'exemple (l. 191) : hypothèse.
Les paliers de boost ne sont pas modélisés : le plafond vocal est celui d'une guilde sans boost.

- **`default_thread_rate_limit_per_user` sur un salon d'annonces : les docs se contredisent.**
  `Modify Channel` le réserve à « Text, Forum, Media » (`channel.mdx:442`), `Create Guild
  Channel` y ajoute « Announcement » (`guild.mdx:928`). Le fake suit la création : le champ
  existe pour toute la famille texte, annonces comprises, se modifie par `PATCH` et figure dans
  le JSON. Hypothèse (lot C).
- **`flags`** : `PATCH` ne l'accepte que pour « Text, Voice, Announcement, Forum, Media »
  (`channel.mdx:439`), donc ni pour un salon stage ni pour une catégorie, où il est validé puis
  ignoré comme tout champ hors de sa famille. Ailleurs il est reporté (point suivant).

- **Reportés** : `available_tags`, `default_reaction_emoji` et le drapeau `REQUIRE_TAG` n'ont de
  sens qu'avec les posts de forum, donc avec M4-REST-01. D'ici là un forum rend
  `available_tags: []` et `default_reaction_emoji: null` ; ces champs et `flags` sont validés
  dans un `PATCH`, puis ignorés (note 🟡 de parité). `permission_overwrites` et
  `lock_permissions` restent à M4-REST-05, `last_pin_timestamp` à M1-REST-05.
- **Champ hors de sa famille** (un `bitrate` sur un salon texte) : validé à l'étape structurelle
  avec le schéma commun, puis ignoré par l'état. C'est le choix de M1-REST-02, toujours une
  hypothèse.

**Squelette interne** (illustratif ; signatures seulement) :

```csharp
internal enum ChannelKind
{
    GuildText = 0, Dm = 1, GuildVoice = 2, GuildCategory = 4, GuildAnnouncement = 5,
    GuildStageVoice = 13, GuildForum = 15, GuildMedia = 16,
}

internal enum ChannelFamily { Private, Text, Voice, Category, Forum }
internal enum ChannelSortGroup { None, Textual, Voice, Category }

/// <summary>What applies to a channel kind: the single source for the state, the wire and the seed.</summary>
internal readonly record struct ChannelTraits(
    ChannelFamily Family, ChannelSortGroup SortGroup, bool CanHaveParent, bool IsMessageable,
    int TopicMaxLength, bool HasNsfw, bool HasSlowmode, int BitrateMax, int UserLimitMax)
{
    internal static ChannelTraits Of(ChannelKind kind);
}

internal sealed record VoiceSettings(int Bitrate, int UserLimit, string? RtcRegion, int? VideoQualityMode);
internal sealed record ForumSettings(int? DefaultSortOrder, int DefaultForumLayout);

// ChannelSnapshot gains: ulong? ParentId, int Flags, int? DefaultAutoArchiveDuration,
// int DefaultThreadRateLimitPerUser, VoiceSettings? Voice, ForumSettings? Forum.
// Invariant: Voice is not null exactly for the voice family, Forum exactly for the forum family.

internal sealed record CreateGuildChannelInput(
    ulong GuildId, ChannelKind Kind, string Name, ulong? Id = null, ulong? ParentId = null,
    int? Position = null, string? Topic = null, bool Nsfw = false, int RateLimitPerUser = 0,
    VoiceSettings? Voice = null, ForumSettings? Forum = null);

internal sealed partial class FakeDiscordState
{
    internal StateResult<ChannelSnapshot> CreateGuildChannel(StateCaller caller, CreateGuildChannelInput input);
}
```

- `CreateGuildChannel` **remplace** `CreateTextChannel` : une seule opération crée un salon de
  guilde, donc un seul endroit où tiennent les invariants des § 2 et 4. Seed et
  `POST /guilds/{guild_id}/channels` (M1-REST-06) l'appellent tous deux.
- `ChannelEntity.Kind` devient modifiable, par la seule conversion du § 5. Elle reste dans la
  famille texte : aucun bloc n'apparaît ni ne disparaît.
- Les faits ne changent pas : `ChannelCreated`, `ChannelUpdated`, `ChannelDeleted` portent le
  `ChannelSnapshot` enrichi. Pas de nouveau type de fait.
- **Fil** ([ADR 0006](0006-wire-serialization.md), règles 4 et 5) : une variante de DTO par
  famille, choisie par `Kind`, car la présence d'un champ dépend de la famille et non de sa
  valeur. `topic` est présent et nullable en texte, absent en voix ; `rtc_region` l'inverse. Une
  liste de salons mêle les variantes : convertisseur fermé de la règle 5.

| Champ JSON | Texte (0, 5) | Voix (2, 13) | Catégorie (4) | Forum (15, 16) |
| --- | --- | --- | --- | --- |
| `id`, `type`, `guild_id`, `name`, `position`, `flags`, `permission_overwrites`, `parent_id`, `nsfw` | présents | présents | présents (`parent_id: null`, `nsfw: false`) | présents |
| `topic` | nullable | absent | absent | nullable |
| `last_message_id` | nullable | nullable | absent | nullable |
| `rate_limit_per_user` | présent (`0` pour le type 5) | présent | absent | présent |
| `bitrate`, `user_limit`, `rtc_region` | absents | présents (`rtc_region` nullable) | absents | absents |
| `video_quality_mode` | absent | présent si défini | absent | absent |
| `default_auto_archive_duration` | présent si défini | absent | absent | présent si défini |
| `default_thread_rate_limit_per_user` | présent (type 5 compris : hypothèse, lot C) | absent | absent | présent |
| `available_tags`, `default_reaction_emoji`, `default_sort_order` | absents | absents | absents | présents (les deux derniers nullables) |
| `default_forum_layout` | absent | absent | absent | présent (type 15) |

Source : exemples de `channel.mdx:142-262`. « Si défini » suit « 1 when not present » (l. 38).
L'exemple d'un salon d'annonces n'a pas de `rate_limit_per_user` (l. 164-177) ; le fake l'émet à
`0`, ce que NetCord lit de la même façon (lot C).

**Bornes qui dépendent du type.** L'étape structurelle de l'[ADR 0005](0005-rest-routing-and-pipeline.md)
ne connaît pas le type du salon : elle valide avec le schéma commun de la spec (`topic` ≤ 4096,
`bitrate` ≥ 8000, `user_limit` ≥ 0). Les bornes par type sont vérifiées par l'opération d'état,
sous le verrou, avant toute écriture, comme `InvalidChannelName` aujourd'hui. Un seul refus est
rendu, le premier dans l'ordre `type`, `parent_id`, `name`, `topic`, `bitrate`, `user_limit` ; à
la création, la limite de 500 salons passe avant.
`StateFailure(Kind, ResourceId)` garde sa forme : la réunion de tous les refus dans un seul arbre
`errors` dépend de la réponse au point 5 d'UR-004 et reste hors de cette ADR.

> **Amendement (M1-REST-02B2, 2026-11-28).** `StateFailure` gagne un troisième champ facultatif,
> `Limit` (`int`, `0` par défaut) : la borne dépassée, quand le message de la réponse la cite.
> Seul `InvalidChannelUserLimit` le renseigne (99 en voix, 10000 en stage), ce qui évite un
> `StateFailureKind` par type de salon. `Limit` ne sert qu'au message : ni le statut ni le code
> n'en dépendent. Le refus reste unique ; la réunion de plusieurs refus dans un seul arbre
> `errors` reste hors de cette ADR (UR-004, point 5).

| Refus (`StateFailureKind`) | Réponse | Source |
| --- | --- | --- |
| `InvalidChannelTopic` (nouveau) : `topic` > 1024 hors forum | 400 `50035`, `topic` : `BASE_TYPE_BAD_LENGTH` « Must be between 0 and 1024 in length. » | borne documentée, forme hypothèse |
| `ChannelBitrateTooHigh` (nouveau) | 400 `30052` | code documenté, statut hypothèse |
| `InvalidChannelUserLimit` (nouveau), avec `Limit` | 400 `50035`, `user_limit` : `NUMBER_TYPE_MAX` « int value should be less than or equal to {Limit}. » | borne documentée, forme hypothèse |
| `MaxGuildChannels` (nouveau) : 500 salons hors threads, à la création | 400 `30013` | code documenté (`channel.mdx:273` pour les threads), statut hypothèse |

### 2. `parent_id`

Quatre invariants, tenus par l'état **pour tout appelant**, seed compris : l'état ne doit pas
prendre une forme que Discord ne peut pas avoir. Ils sont vérifiés dans cet ordre, et le premier
qui échoue donne le refus.

1. Le parent existe et appartient à la même guilde.
2. Le parent est une catégorie (type 4).
3. Une catégorie n'a pas de parent.
4. Une catégorie a au plus 50 enfants.

| Refus (`StateFailureKind`) | Réponse (400 `50035`, champ `parent_id`) | Source |
| --- | --- | --- |
| `InvalidChannelParent` (existant) : parent inconnu ou d'une autre guilde | `CHANNEL_PARENT_INVALID` « Category does not exist » | message rapporté par un tiers ; code de champ hypothèse |
| `ChannelParentNotCategory` (nouveau) | `CHANNEL_PARENT_INVALID_TYPE` « Not a category » | hypothèse |
| `ChannelCannotHaveParent` (nouveau) : le salon est une catégorie | `CHANNEL_PARENT_INVALID` « Categories cannot have subcategories » | hypothèse |
| `ChannelParentFull` (nouveau) | `CHANNEL_PARENT_MAX_CHANNELS` « Maximum number of channels in category reached (50) » | limite documentée ; code et message hypothèses |

Le message actuel de `CHANNEL_PARENT_INVALID`, « Invalid parent channel. » (`Rest/DiscordError.cs:123`),
est remplacé par celui du tableau. Les quatre lignes sont à confirmer (lot A).

- **Trois états sur le fil** : clé absente = inchangé ; `null` = sortir de la catégorie ; ID =
  déplacer. `ChannelPatch` et `UpdateChannelInput` confondent aujourd'hui absent et `null` : ils
  gagnent `ChangeParent`, sur le modèle de `ChangeTopic`. Envoyer le parent courant, ou `null` à
  un salon sans parent, ne change rien et ne publie rien.
- **Un déplacement ne change que `ParentId`** : ni la position (hypothèse, lot B), ni les
  permissions avant M4-REST-05.
- **`parent_id: null` n'est pas atteignable par NetCord** (Contexte). L'opération d'état est
  testée directement ; le test de route construit la requête à la main, NetCord ne sachant pas
  l'émettre, et la ligne de parité le note.
- Un parent d'une guilde dont le bot n'est pas membre ne demande aucune règle de visibilité de
  plus : l'invariant 1 le refuse déjà.

### 3. Suppression d'une catégorie

`DELETE /channels/{channel_id}` sur une catégorie fait **un** commit :

1. chaque enfant, par ID croissant, perd son parent et produit un `ChannelUpdated` qui porte
   l'enfant après la mutation ;
2. la catégorie est retirée et produit un `ChannelDeleted` qui la porte telle qu'elle était.

Les numéros de changement se suivent ; aucun lecteur ne voit un enfant pointer vers une catégorie
absente. Les enfants gardent position, messages et (M4-REST-05) permissions. La réponse est la
catégorie telle qu'elle était, comme pour tout salon.

**Hypothèse.** La doc garantit les `CHANNEL_UPDATE` des enfants et le `CHANNEL_DELETE`, pas leur
ordre. Les enfants d'abord : c'est le seul ordre où le cache d'un client ne contient jamais un
salon dont le `parent_id` désigne une catégorie disparue (lot A).

Rien ne change pour les autres types : le salon part avec ses messages, sans fait par message.
Les threads d'un salon supprimé relèvent de M4-REST-01. Les salons qu'une guilde Community
interdit de supprimer (`50074`) ne sont pas modélisés.

### 4. `position`

- **Une valeur stockée, pas un rang.** Entier ≥ 0, propre au salon, non unique. Les ex æquo se
  départagent par ID (documenté). Le fake **ne renumérote jamais un autre salon** : ni à la
  création, ni au déplacement, ni à la suppression. C'est une hypothèse (lot B), appuyée sur
  discord.js, qui renvoie lui-même toutes les positions du groupe.
- **Défaut à la création** : `1 + max(position)` parmi les salons de la guilde du **même groupe
  de tri**, toutes catégories confondues ; `0` s'il n'y en a pas. Le premier salon texte, le
  premier salon vocal et la première catégorie d'une guilde ont donc chacun la position `0`.
  Une guilde qui n'a que des salons texte garde les valeurs d'aujourd'hui (`0`, `1`, `2`…) tant
  qu'aucun salon n'y a été supprimé ni créé avec une position explicite : après la suppression
  du premier de trois salons, l'ancien défaut donnait `2` au suivant (ex æquo), le nouveau
  donne `3`. Hypothèse, non documentée (lot B).
  *Rejeté :* le nombre de salons de la guilde (valeur actuelle et de fauxcord), qui numérote
  ensemble des groupes que Discord numérote à part et crée des ex æquo après une suppression.
- **`PATCH /channels/{channel_id}`** : `position` écrit la valeur de ce salon ; `null` ou absent
  = inchangé.
- **`PATCH /guilds/{guild_id}/channels`** (M1-REST-06) : le tableau est validé en entier, puis
  appliqué en un commit ([ADR 0003](0003-state-model-and-concurrency.md), « un lot est validé
  avant toute écriture »).
  - `position` : absent ou `null` = inchangé. `parent_id` : absent = inchangé, `null` = sortir,
    ID = déplacer, avec les invariants du § 2.
  - Plus d'une entrée dont le `parent_id` **diffère du parent courant** : 400 `40009`
    (documenté ; la lecture de « changes » est une hypothèse).
  - ID inconnu ou d'une autre guilde : 404 `10003`, rien n'est écrit (hypothèse).
  - Un `ChannelUpdated` par salon **réellement changé**, dans l'ordre du tableau ; un salon cité
    deux fois reçoit ses entrées dans l'ordre et un seul fait. Réponse 204.
  - `lock_permissions` : validé (booléen ou `null`), sans effet avant M4-REST-05, qui portera
    aussi les permissions par entrée de `guild.mdx:966-967`.
- **Ordre de `GET /guilds/{guild_id}/channels`** : ID croissant, sans les threads. Le fake ne
  trie **pas** par position. Aucun ordre n'est documenté ; l'ID est le seul départage que la doc
  nomme, et il ne dépend d'aucun `PATCH` ; les bibliothèques clientes ne se fient pas à l'ordre
  reçu et re-trient (`discordSort` de discord.js, par position puis ID). Hypothèse (lot B).
  `GUILD_CREATE.channels` suit le même ordre (M3-02). Avec un ID explicite au seed, cet ordre
  n'est plus celui de création, que rend le listing d'aujourd'hui.

### 5. Changement de type

| `type` demandé | Résultat |
| --- | --- |
| absent, `null`, ou le type courant | inchangé |
| 0 → 5, 5 → 0 | accepté |
| tout autre couple, et tout type 14 | `InvalidChannelType` : 400 `50035`, `type` : `CHANNEL_TYPE_INVALID` (hypothèse déjà suivie par UR-004) |

- La conversion garde ID, nom, parent, position, `topic`, `nsfw` et messages. 0 → 5 remet
  `RateLimitPerUser` à `0`, les annonces n'ayant pas de slowmode (`channel.mdx:431`) : hypothèse
  (lot C).
- Elle s'applique **avant** les autres champs du même `PATCH`, validés contre le nouveau type ;
  l'ensemble produit un seul `ChannelUpdated`. Hypothèse (lot C).
- **La fonctionnalité `NEWS` n'est pas exigée.** L'état ne modélise pas `features` : en M1 toute
  guilde accepte un salon d'annonces, à la création comme par conversion ; de même pour un salon
  stage, que Discord réserve aux guildes Community (`guild.mdx:149`). Le contrôle viendra avec
  `features` (tâche M4-REST-10).

### 6. Seed

Extension de l'[ADR 0004](0004-fake-discord-public-api.md), avec ses règles : commit immédiat,
pas de faux POST, `id` explicite facultatif, échec avant commit par `InvalidOperationException`.

- **Un salon est toujours une poignée.** L'ADR 0004 réserve les IDs nus aux feuilles. Un salon
  n'en est pas une : il porte des messages, demain des posts (M4-REST-01), des permissions
  (M4-REST-05). Rendre un `ulong` aujourd'hui obligerait à casser la signature ce jour-là.
- **Trois poignées de plus**, par ce qu'on peut y créer : `SeededCategory` (des salons),
  `SeededVoiceChannel` (des messages : voix et stage ont un salon de discussion),
  `SeededForumChannel` (rien en M1, des posts en M4). `SeededTextChannel` sert aussi aux annonces.
- **Le parent se lit dans l'appel** : `category.TextChannel("general")`, pas un paramètre.
- **Options** : une classe, `SeededChannelOptions`, en dernier paramètre facultatif. Elle porte
  `Topic`, `Nsfw`, `Slowmode` et `Position`. **`rate_limit_per_user` est donc inclus dans le
  seed** (`Slowmode`, le nom de NetCord), ce qui absorbe M1-REST-02D. Une option qui ne vaut pas
  pour le type demandé lève `ArgumentException` au lieu d'être ignorée, **si sa valeur diffère
  du défaut** : `Topic` non nul, `Nsfw` vrai, `Slowmode` différent de `0` (`Position` vaut pour
  tous les types). `Nsfw` et `Slowmode` n'étant pas nullables, leur valeur par défaut ne se
  distingue pas d'une option non renseignée et ne lève donc jamais. Une valeur hors bornes lève
  `ArgumentOutOfRangeException`. Les réglages vocaux (`bitrate`, `user_limit`…) ne sont pas
  dans le seed : valeurs par défaut, modifiables par REST. Ajouter une propriété plus tard ne
  casse aucune signature.

| Option | Texte | Annonces | Voix, stage | Forum | Catégorie |
| --- | --- | --- | --- | --- | --- |
| `Topic` | oui (1024) | oui (1024) | non | oui (4096) | non |
| `Nsfw` | oui | oui | oui | oui | non |
| `Slowmode` | oui | non | oui | oui | non |
| `Position` | oui | oui | oui | oui | oui |

- **Hors du seed** : media (type en développement selon la doc, absent de la spec) et annuaire.
  Un salon media se crée par `CreateGuildChannelAsync`, une fois M1-REST-06 livré.
- **Les invariants valent pour le seed** : un 51ᵉ salon dans une catégorie, un 501ᵉ salon dans
  une guilde, ou un salon demandé à une catégorie supprimée, lèvent `InvalidOperationException`
  sans rien créer.

**Rejeté :** une poignée unique `SeededChannel` dont `Message` lèverait sur un forum (l'erreur
passerait de la compilation à l'exécution, et `SeededTextChannel` serait à renommer) ; une classe
d'options par famille (trois types publics de plus pour quatre propriétés) ; des paramètres
nommés par réglage (chaque ajout change la signature) ; un paramètre `category:` (deux façons
d'écrire la même relation) ; aucun réglage dans le seed, à faire par REST (la mise en place du
test entrerait dans `Recorded.Requests` et, en M3, dans les dispatchs).

### 7. Dispatchs Gateway attendus (M3-03)

Intent `GUILDS` ; aucun dispatch pour une guilde dont le bot n'est pas membre (amendement 04B).
Chaque payload est l'objet salon du § 1, **avec** `guild_id`, que NetCord lit pour son cache.

| Mutation | Faits, dans l'ordre | Dispatchs |
| --- | --- | --- |
| Création (seed ou `POST`), catégorie comprise | `ChannelCreated` | `CHANNEL_CREATE` |
| `PATCH` d'un salon : champs, parent, position, type | `ChannelUpdated` si une valeur change | `CHANNEL_UPDATE` (le nouveau `type` après conversion) |
| `PATCH` de positions | un `ChannelUpdated` par salon changé, ordre du tableau | autant de `CHANNEL_UPDATE` |
| Suppression d'un salon | `ChannelDeleted` | `CHANNEL_DELETE`, sans `MESSAGE_DELETE` pour ses messages (hypothèse, lot A) |
| Suppression d'une catégorie | `ChannelUpdated` par enfant (ID croissant), puis `ChannelDeleted` | `CHANNEL_UPDATE` × n, puis `CHANNEL_DELETE` |
| Message créé dans le salon | `MessageCreated` seul | pas de `CHANNEL_UPDATE` pour `last_message_id` (documenté) |

- « If modifying a category, individual Channel Update events will fire for each child channel
  that also changes » (`channel.mdx:422`) : en M1 rien ne change chez les enfants quand on
  modifie une catégorie. La synchronisation des permissions (`permissions.mdx:211-213`) relève de
  M4-REST-05.
- Les salons masqués par `VIEW_CHANNEL` (`CHANNEL_OBFUSCATED`, `channel.mdx:100-118`) relèvent de
  M4-REST-05 : en M1 le bot voit tous les salons de ses guildes.

### Hypothèses à confirmer

Les sources officielles ne tranchent pas les points suivants. Le fake implémente la réponse
indiquée dans les sections citées ; les lignes de parité concernées restent 🟡 « à confirmer »
tant que le test manuel correspondant de `docs/user-review.md` n'a pas été fait (tâche
`PAR-REST-02`). Corriger l'un de ces points ne change pas le modèle du § 1.

- **Lot A — hiérarchie** (§ 2, § 3) : codes de champ et messages des quatre refus de parent ;
  ordre des `CHANNEL_UPDATE` des enfants et du `CHANNEL_DELETE` d'une catégorie supprimée ; les
  enfants gardent leur position ; aucun `MESSAGE_DELETE` pour les messages d'un salon supprimé.
- **Lot B — positions** (§ 1, § 4) : groupes de tri ; position par défaut d'un nouveau salon ;
  absence de renumérotation des voisins ; position inchangée par un déplacement ; ordre de
  `GET /guilds/{guild_id}/channels` ; sens de « changes `parent_id` » pour `40009` ; réponse à
  un ID inconnu et ordre des `CHANNEL_UPDATE` du `PATCH` de positions.
- **Lot C — types** (§ 1, § 5) : slowmode remis à `0` par la conversion 0 → 5 ;
  `rate_limit_per_user` et `default_thread_rate_limit_per_user` d'un salon d'annonces (docs
  contradictoires pour le second) ; conversion appliquée avant les autres champs ;
  refus d'un `topic` de plus de 1024 caractères hors forum, d'un `user_limit` trop grand ; statut
  HTTP de `30052` et de `30013` ; `40058` pour un salon media ; bitrate par défaut ; guilde sans
  `NEWS`.

## API publique proposée

```csharp
namespace Enx.NetCord.Testing;

/// <summary>Optional settings of a seeded guild channel.</summary>
/// <remarks>
/// A setting Discord has no field for on the requested channel type throws
/// <see cref="ArgumentException"/> when it differs from its default (a non-null
/// <see cref="Topic"/>, a true <see cref="Nsfw"/>, a nonzero <see cref="Slowmode"/>); left at
/// its default it is not applied. A value outside Discord's bounds throws
/// <see cref="ArgumentOutOfRangeException"/>.
/// </remarks>
public sealed class SeededChannelOptions
{
    /// <summary>Gets the topic (text, announcement and forum channels), or null for none.</summary>
    public string? Topic { get; init; }
    /// <summary>Gets whether the channel is age-restricted (every type but categories).</summary>
    public bool Nsfw { get; init; }
    /// <summary>Gets the slowmode in seconds, Discord's rate_limit_per_user (text, voice, stage and forum channels).</summary>
    public int Slowmode { get; init; }
    /// <summary>Gets the sorting position, or null to place the channel after the others of its sorting group.</summary>
    public int? Position { get; init; }
}

public sealed class SeededGuild
{
    /// <summary>Creates a category and returns its handle.</summary>
    public SeededCategory Category(string name, ulong? id = null, SeededChannelOptions? options = null);
    /// <summary>Creates a guild text channel outside any category and returns its handle.</summary>
    public SeededTextChannel TextChannel(string name, ulong? id = null, SeededChannelOptions? options = null);
    /// <summary>Creates an announcement channel outside any category and returns its handle.</summary>
    public SeededTextChannel AnnouncementChannel(string name, ulong? id = null, SeededChannelOptions? options = null);
    /// <summary>Creates a voice channel outside any category and returns its handle.</summary>
    public SeededVoiceChannel VoiceChannel(string name, ulong? id = null, SeededChannelOptions? options = null);
    /// <summary>Creates a stage channel outside any category and returns its handle.</summary>
    public SeededVoiceChannel StageChannel(string name, ulong? id = null, SeededChannelOptions? options = null);
    /// <summary>Creates a forum channel outside any category and returns its handle.</summary>
    public SeededForumChannel ForumChannel(string name, ulong? id = null, SeededChannelOptions? options = null);
    // Id, Role, Member: unchanged (ADR 0004).
}

/// <summary>Identifies a seeded category and creates channels in it.</summary>
/// <remarks>A category holds at most 50 channels and cannot hold another category.</remarks>
public sealed class SeededCategory
{
    /// <summary>Gets the category ID.</summary>
    public ulong Id { get; }
    /// <summary>Creates a guild text channel in this category and returns its handle.</summary>
    public SeededTextChannel TextChannel(string name, ulong? id = null, SeededChannelOptions? options = null);
    /// <summary>Creates an announcement channel in this category and returns its handle.</summary>
    public SeededTextChannel AnnouncementChannel(string name, ulong? id = null, SeededChannelOptions? options = null);
    /// <summary>Creates a voice channel in this category and returns its handle.</summary>
    public SeededVoiceChannel VoiceChannel(string name, ulong? id = null, SeededChannelOptions? options = null);
    /// <summary>Creates a stage channel in this category and returns its handle.</summary>
    public SeededVoiceChannel StageChannel(string name, ulong? id = null, SeededChannelOptions? options = null);
    /// <summary>Creates a forum channel in this category and returns its handle.</summary>
    public SeededForumChannel ForumChannel(string name, ulong? id = null, SeededChannelOptions? options = null);
}

/// <summary>Identifies a seeded guild text or announcement channel and creates message fixtures.</summary>
public sealed class SeededTextChannel
{
    // Id, Message: unchanged (ADR 0004).
}

/// <summary>Identifies a seeded voice or stage channel and creates message fixtures in its chat.</summary>
public sealed class SeededVoiceChannel
{
    /// <summary>Gets the channel ID.</summary>
    public ulong Id { get; }
    /// <summary>Creates a message by the specified user and returns the message ID.</summary>
    public ulong Message(string content, ulong authorId, ulong? id = null);   // delivered by M1-REST-03B
}

/// <summary>Identifies a seeded forum channel.</summary>
public sealed class SeededForumChannel
{
    /// <summary>Gets the channel ID.</summary>
    public ulong Id { get; }
}
```

Constructeurs non publics, sauf celui de `SeededChannelOptions`. `TextChannel(name, id)` reste
compatible source ; la rupture binaire est sans effet avant la première publication (M6-05).
Exceptions de toutes ces méthodes : `ArgumentNullException` (`name`),
`ArgumentOutOfRangeException` (`id` nul, option hors bornes), `ArgumentException` (option sans
objet pour ce type), `ObjectDisposedException`, `InvalidOperationException` (guilde ou catégorie
disparue, catégorie ou guilde pleine, ID déjà attribué).

Exemples d'usage dans des tests (pseudo-code) :

```csharp
async Task SeedCategory_Children_AreReadWithTheirParent()
{
    await using var discord = FakeDiscord.Create();
    var guild = discord.Seed.Guild("Test");
    var lobby = guild.Category("Lobby");
    var general = lobby.TextChannel("general");
    var news = lobby.AnnouncementChannel("news");
    var voice = lobby.VoiceChannel("Voice");
    var rules = guild.TextChannel("rules");                  // outside any category
    using var rest = discord.CreateRestClient();

    Assert.IsType<CategoryGuildChannel>(await rest.GetChannelAsync(lobby.Id));
    Assert.Equal(lobby.Id, ((TextGuildChannel)await rest.GetChannelAsync(general.Id)).ParentId);
    Assert.IsType<AnnouncementGuildChannel>(await rest.GetChannelAsync(news.Id));
    var read = Assert.IsType<VoiceGuildChannel>(await rest.GetChannelAsync(voice.Id));
    Assert.Equal((lobby.Id, 64000, 0), (read.ParentId, read.Bitrate, read.Position));
    Assert.Null(((TextGuildChannel)await rest.GetChannelAsync(rules.Id)).ParentId);
    // Positions count per sorting group: general 0, news 1, rules 2; voice 0; lobby 0.
}

async Task DeleteChannel_Category_DetachesItsChildren()
{
    await using var discord = FakeDiscord.Create();
    var lobby = discord.Seed.Guild("Test").Category("Lobby");
    var general = lobby.TextChannel("general");
    var alice = discord.Seed.User("alice");
    var message = general.Message("hello", alice);
    using var rest = discord.CreateRestClient();

    var deleted = await rest.DeleteChannelAsync(lobby.Id);

    Assert.Equal(lobby.Id, deleted.Id);
    Assert.Null(((TextGuildChannel)await rest.GetChannelAsync(general.Id)).ParentId);
    Assert.Equal("hello", (await rest.GetMessageAsync(general.Id, message)).Content);
    Assert.Throws<InvalidOperationException>(() => lobby.TextChannel("late"));
    // With a connected client (M3): CHANNEL_UPDATE for general, then CHANNEL_DELETE for the category.
}

async Task ModifyChannel_Parent_MovesOrIsRefused()
{
    await using var discord = FakeDiscord.Create();
    var guild = discord.Seed.Guild("Test");
    var archive = guild.Category("Archive");
    var general = guild.TextChannel("general");
    var rules = guild.TextChannel("rules");
    using var rest = discord.CreateRestClient();

    var moved = (TextGuildChannel)await rest.ModifyGuildChannelAsync(general.Id, o => o.ParentId = archive.Id);
    Assert.Equal(archive.Id, moved.ParentId);

    // Only a category can be a parent.
    var refused = await Assert.ThrowsAsync<RestException>(
        () => rest.ModifyGuildChannelAsync(general.Id, o => o.ParentId = rules.Id));
    Assert.Equal((HttpStatusCode.BadRequest, 50035), (refused.StatusCode, refused.Error!.Code));
    var field = discord.Recorded.Requests.Last().ResponseBody!.Value
        .GetProperty("errors").GetProperty("parent_id").GetProperty("_errors")[0];
    Assert.Equal("CHANNEL_PARENT_INVALID_TYPE", field.GetProperty("code").GetString());
    Assert.Equal(archive.Id, ((TextGuildChannel)await rest.GetChannelAsync(general.Id)).ParentId);
}

async Task ModifyChannel_TextToAnnouncement_KeepsMessages()
{
    await using var discord = FakeDiscord.Create();
    var general = discord.Seed.Guild("Test").TextChannel("general", options: new() { Slowmode = 30 });
    var message = general.Message("hello", discord.Seed.User("alice"));
    using var rest = discord.CreateRestClient();

    var news = await rest.ModifyGuildChannelAsync(
        general.Id, o => o.ChannelType = ChannelType.AnnouncementGuildChannel);

    Assert.Equal(0, Assert.IsType<AnnouncementGuildChannel>(news).Slowmode);
    Assert.Equal("hello", (await rest.GetMessageAsync(general.Id, message)).Content);
    await Assert.ThrowsAsync<RestException>(                // only text <-> announcement converts
        () => rest.ModifyGuildChannelAsync(general.Id, o => o.ChannelType = ChannelType.VoiceGuildChannel));
}

async Task SeedChannel_Options_ApplyOrThrow()
{
    await using var discord = FakeDiscord.Create();
    var guild = discord.Seed.Guild("Test");
    using var rest = discord.CreateRestClient();

    var memes = guild.TextChannel("memes", options: new() { Nsfw = true, Topic = "18+", Position = 7 });
    var read = (TextGuildChannel)await rest.GetChannelAsync(memes.Id);
    Assert.Equal((true, "18+", 7), (read.Nsfw, read.Topic, read.Position));

    Assert.Throws<ArgumentException>(() => guild.VoiceChannel("Voice", options: new() { Topic = "no topic here" }));
    Assert.Throws<ArgumentException>(() => guild.AnnouncementChannel("news", options: new() { Slowmode = 5 }));
}
```

## Conséquences

- **Positif.** Une seule table dit ce qui vaut pour chaque type : ajouter les threads ou le
  group DM, c'est une ligne et, au besoin, un bloc. Les faits et `GuildAccess` ne bougent pas.
  Les fixtures existantes restent valables : `TextChannel(name, id)` compile, et une guilde qui
  n'a que des salons texte, **créés sans ID explicite et dont aucun n'a été supprimé**, garde
  ses positions et son ordre de listing. Hors de ce cas les deux changent (§ 4) : un ID
  explicite plus petit passe devant dans le listing, et la position par défaut après une
  suppression n'est plus le nombre de salons. Le test
  `FakeDiscordStateTests.CreateTextChannel_Several_AreListedInCreationOrderWithPositions`
  (`tests/Enx.NetCord.Testing.Tests/State/FakeDiscordStateTests.cs:599`) appelle
  `CreateTextChannel`, qui disparaît, et son nom décrit l'ancienne règle. Ses valeurs attendues
  (positions `0` et `1`, listing `a` puis `b`) restent justes sous les nouvelles règles :
  M1-REST-02B1 le migre vers `CreateGuildChannel`, le renomme (ordre par ID, position par
  groupe) et y ajoute un cas à ID explicite et un cas après suppression.
- **Négatif.** Quatre types publics de plus et onze méthodes de seed. Les bornes par type sont
  jugées sous le verrou, donc après `Intercept` et sans se réunir avec les erreurs structurelles
  dans un même arbre `errors` (limite déjà notée par M1-REST-02). Une bonne part des codes de
  champ et des ordres repose sur des hypothèses : lots A, B et C ci-dessus.
- **Refonte interne à prévoir** : `CreateTextChannelInput` et `CreateTextChannel` disparaissent
  au profit de `CreateGuildChannel` ; `ChannelSnapshot` gagne des champs ; `ChannelPatch` et
  `UpdateChannelInput` gagnent `ChangeParent` et les champs par type ; `GuildChannelResponse`
  devient la variante texte. Ces fichiers de `State/` sont aussi touchés par M1-REST-03, en
  cours : lancer M1-REST-02B1 **après** sa fusion.
- **Interactions.** Une requête de l'[ADR 0007](0007-interaction-model.md) peut viser un salon
  vocal. Le sort d'une requête dans une catégorie ou un forum est à trancher par M2-01.
- Ni dépendance, ni réflexion ; les DTO de plus passent par `FakeDiscordJsonContext`.

**Découpage de M1-REST-02B** (à remplacer par les deux premières lignes) et tâches à créer :

- `M1-REST-02B1` — État, seed et lecture : `ChannelKind` étendu, `ChannelTraits`, blocs `Voice`
  et `Forum`, `ParentId`, `CreateGuildChannel` et ses invariants (§ 2), position par défaut par
  groupe et listing par ID (§ 4), suppression d'une catégorie (§ 3), seed du § 6
  (`SeededChannelOptions` compris), variantes de DTO par famille, `GET` et `DELETE
  /channels/{channel_id}` pour tous les types du § 1 — rest-implementer — M1-ADR-13, M1-REST-03.
  **Absorbe M1-REST-02D.**
  - Porte les `StateFailureKind` dont la création a besoin : `ChannelParentNotCategory`,
    `ChannelCannotHaveParent`, `ChannelParentFull` (§ 2) et `MaxGuildChannels` (§ 1). Dans cette
    tâche leur seul appelant est le seed, qui les rend en `InvalidOperationException` ; aucune
    fabrique `DiscordError` n'est encore nécessaire.
  - Livre `SeededVoiceChannel` avec **`Id` seulement**. Sa méthode `Message` (§ 6) attend le
    contrôle des salons à messages, donc M1-REST-03B.
- `M1-REST-02B2` — `PATCH /channels/{channel_id}` complet : `parent_id` à trois états et
  déplacement (§ 2), conversion 0 ↔ 5 (§ 5), champs et bornes par type (§ 1) —
  rest-implementer — M1-REST-02B1.
  - Ajoute les `StateFailureKind` propres au `PATCH` (`InvalidChannelTopic`,
    `ChannelBitrateTooHigh`, `InvalidChannelUserLimit`) et les fabriques `DiscordError` des
    refus qu'un `PATCH` peut rendre : ceux-là et les trois refus de parent de M1-REST-02B1, plus
    le nouveau message de `CHANNEL_PARENT_INVALID` ; lignes `30052` et `50035` de `errors.md`.
  - La fabrique de `MaxGuildChannels` (`30013`) revient à M1-REST-06, seule route qui crée un
    salon ; elle y réutilise les fabriques de parent pour `POST` et pour le `PATCH` de positions.
- `M1-REST-03B` — Messages selon le type de salon : `50008` dans une catégorie, `40058` dans un
  forum ou un media, `crosspost` réservé au type 5 (`40033` au second appel), méthode `Message`
  de `SeededVoiceChannel` — rest-implementer — M1-REST-03, M1-REST-02B1.
- `M4-REST-10` — Contrôles par `features` de guilde : `NEWS` pour le type 5 et la conversion,
  Community pour le type 13, salons qu'une guilde Community interdit de supprimer (`50074`) —
  rest-implementer — M1-REST-06.
- `PAR-REST-02` — Confirmer contre Discord les lots A, B et C des hypothèses de cette ADR (tests
  manuels de `docs/user-review.md`), puis aligner fake, tests et matrices — parity-auditor —
  M1-REST-02B2, M1-REST-06.

**Tâches existantes impactées.**

- **M1-REST-06** dépend de M1-REST-02B1 et de M1-REST-02B2 (fabriques `DiscordError` des refus
  de parent) : listing par ID sans threads ; `POST` par `CreateGuildChannel` pour les types du
  § 1 (fabrique `30013`, invariants de parent, position par défaut) ; `PATCH` de positions selon
  le § 4 (`40009`).
- **M1-REST-03** (en cours) ne change pas : il ne connaît que le salon texte. Le salon
  d'annonces et les refus par type arrivent avec M1-REST-03B.
- **M1-REST-02D** : absorbée par M1-REST-02B1 (`SeededChannelOptions.Slowmode`).
- **M3-02** : `GUILD_CREATE.channels` dans l'ordre du § 4. **M3-03** : tableau du § 7.
- **M4-REST-01** : types 10, 11, 12 par une ligne de `ChannelTraits` et un bloc `Thread` ;
  `parent_id` y désigne le salon d'origine, sans les invariants du § 2 ; hors du listing et de
  la limite de 500 ; sort des threads d'un salon supprimé ; `available_tags`,
  `default_reaction_emoji`, `applied_tags`, `REQUIRE_TAG` ; posts sur `SeededForumChannel`.
- **M4-REST-05** : `permission_overwrites` réels, `lock_permissions`, reprise des permissions de
  la catégorie à la création, synchronisation catégorie → enfants, filtrage du listing par
  `VIEW_CHANNEL`, salons masqués sur la Gateway.

**Parité.** Prépare, sans les promouvoir :

- `rest.md` : `GET`/`PATCH`/`DELETE /channels/{channel_id}` (les « Manque » sur les types, les
  catégories et le `topic` de 4096), `GET`/`POST`/`PATCH /guilds/{guild_id}/channels`,
  `POST /channels/{channel_id}/messages` et `…/crosspost` (refus par type).
- `errors.md` : `50035` (codes de champ `CHANNEL_PARENT_*`, `CHANNEL_TYPE_INVALID`), et les
  lignes `30013`, `30052`, `40009`, `40033`, `40058`, `50008`, à créer pour celles qui manquent.
- `gateway.md` : `CHANNEL_CREATE`, `CHANNEL_UPDATE`, `CHANNEL_DELETE`.
- `sources.md` ne suit pas `developers/resources/**`, que cette ADR cite : à ajouter aux fichiers
  suivis lors du prochain `/parity/sync`.

Aucune ligne promue ni exclue, aucune matrice modifiée par cette ADR.
