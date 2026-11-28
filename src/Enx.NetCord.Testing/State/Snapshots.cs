using System.Collections.Immutable;

namespace Enx.NetCord.Testing.State;

// Detached, immutable values copied from the mutable entities under the state lock.
// `CreatedAt` is the instant encoded in the entity's snowflake; `Ordinal` is the internal creation
// order used to break ties between entities created at the same instant.

/// <summary>Discord channel type values used by the fake (<c>ChannelType</c> in the spec; ADR 0013, § 1).</summary>
internal enum ChannelKind
{
    GuildText = 0,
    Dm = 1,
    GuildVoice = 2,
    GuildCategory = 4,
    GuildAnnouncement = 5,
    GuildStageVoice = 13,
    GuildForum = 15,

    /// <summary>Type 16: described by the docs and typed by NetCord, absent from the spec's enumeration (ADR 0013, § 1).</summary>
    GuildMedia = 16,
}

/// <summary>The family of a channel kind: it decides which block of fields the channel carries.</summary>
internal enum ChannelFamily
{
    Private,
    Text,
    Voice,
    Category,
    Forum,
}

/// <summary>The sorting group of a channel kind: <c>position</c> counts within a group, not across guild channels.</summary>
internal enum ChannelSortGroup
{
    None,
    Textual,
    Voice,
    Category,
}

/// <summary>What applies to a channel kind: the single source for the state, the wire and the seed (ADR 0013, § 1).</summary>
/// <param name="Family">The family of fields.</param>
/// <param name="SortGroup">The group <c>position</c> is counted in.</param>
/// <param name="CanHaveParent">Whether the channel can sit in a category.</param>
/// <param name="IsMessageable">Whether messages can be sent to the channel (enforced by the message operations of the state).</param>
/// <param name="TopicMaxLength">The maximum topic length, or 0 when the kind has no topic.</param>
/// <param name="HasNsfw">Whether the kind has the <c>nsfw</c> flag.</param>
/// <param name="HasSlowmode">Whether the kind has <c>rate_limit_per_user</c>.</param>
/// <param name="BitrateMax">The maximum bitrate, or 0 when the kind has none.</param>
/// <param name="UserLimitMax">The maximum user limit, or 0 when the kind has none.</param>
internal readonly record struct ChannelTraits(
    ChannelFamily Family,
    ChannelSortGroup SortGroup,
    bool CanHaveParent,
    bool IsMessageable,
    int TopicMaxLength,
    bool HasNsfw,
    bool HasSlowmode,
    int BitrateMax,
    int UserLimitMax)
{
    /// <summary>Default bitrate of a new voice or stage channel (hypothesis of ADR 0013, lot C).</summary>
    internal const int DefaultBitrate = 64000;

    /// <summary>Gets whether the kind has default thread settings (<c>default_auto_archive_duration</c>, <c>default_thread_rate_limit_per_user</c>): text, announcement, forum and media.</summary>
    internal bool HasThreadDefaults => Family is ChannelFamily.Text or ChannelFamily.Forum;

    /// <summary>Gets the traits of a channel kind.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The traits.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The kind is not modeled.</exception>
    internal static ChannelTraits Of(ChannelKind kind) => kind switch
    {
        ChannelKind.Dm => new(ChannelFamily.Private, ChannelSortGroup.None, false, true, 0, false, false, 0, 0),
        ChannelKind.GuildText => new(ChannelFamily.Text, ChannelSortGroup.Textual, true, true, 1024, true, true, 0, 0),
        ChannelKind.GuildAnnouncement => new(ChannelFamily.Text, ChannelSortGroup.Textual, true, true, 1024, true, false, 0, 0),
        ChannelKind.GuildVoice => new(ChannelFamily.Voice, ChannelSortGroup.Voice, true, true, 0, true, true, 96000, 99),
        ChannelKind.GuildStageVoice => new(ChannelFamily.Voice, ChannelSortGroup.Voice, true, true, 0, true, true, 64000, 10000),
        ChannelKind.GuildCategory => new(ChannelFamily.Category, ChannelSortGroup.Category, false, false, 0, false, false, 0, 0),
        ChannelKind.GuildForum or ChannelKind.GuildMedia =>
            new(ChannelFamily.Forum, ChannelSortGroup.Textual, true, false, 4096, true, true, 0, 0),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "The channel kind is not modeled."),
    };
}

/// <summary>The settings of a voice or stage channel.</summary>
/// <param name="Bitrate">The bitrate in bits per second.</param>
/// <param name="UserLimit">The user limit, 0 for none.</param>
/// <param name="RtcRegion">The voice region override, or <see langword="null"/> for automatic.</param>
/// <param name="VideoQualityMode">The video quality mode (1 or 2), or <see langword="null"/> when not defined.</param>
internal sealed record VoiceSettings(int Bitrate, int UserLimit, string? RtcRegion, int? VideoQualityMode)
{
    /// <summary>Gets the settings of a new voice channel.</summary>
    internal static VoiceSettings Default { get; } = new(ChannelTraits.DefaultBitrate, 0, null, null);
}

/// <summary>The settings of a forum or media channel (tags and reaction emoji wait for M4-REST-01).</summary>
/// <param name="DefaultSortOrder">The default sort order of posts, or <see langword="null"/> for none.</param>
/// <param name="DefaultForumLayout">The default layout (0-2).</param>
internal sealed record ForumSettings(int? DefaultSortOrder, int DefaultForumLayout)
{
    /// <summary>Gets the settings of a new forum channel.</summary>
    internal static ForumSettings Default { get; } = new(null, 0);
}

internal sealed record UserSnapshot(
    ulong Id,
    string Username,
    string? GlobalName,
    string? AvatarHash,
    string? BannerHash,
    bool IsBot,
    DateTimeOffset CreatedAt,
    long Ordinal);

internal sealed record ApplicationSnapshot(
    ulong Id,
    string Name,
    string Description,
    DateTimeOffset CreatedAt,
    long Ordinal)
{
    /// <summary>Gets the ID of the bot user; it equals <see cref="Id"/>.</summary>
    internal ulong BotUserId => Id;
}

internal sealed record BotIdentitySnapshot(UserSnapshot User, ApplicationSnapshot Application);

internal sealed record GuildSnapshot(
    ulong Id,
    string Name,
    ulong? OwnerId,
    string? IconHash,
    string? Description,
    DateTimeOffset CreatedAt,
    long Ordinal);

internal sealed record ChannelSnapshot(
    ulong Id,
    ChannelKind Kind,
    ulong? GuildId,
    string? Name,
    string? Topic,
    int Position,
    bool Nsfw,
    int RateLimitPerUser,
    ulong? LastMessageId,
    ImmutableArray<ulong> RecipientIds,
    ulong? ParentId,
    int Flags,
    int? DefaultAutoArchiveDuration,
    int DefaultThreadRateLimitPerUser,
    VoiceSettings? Voice,
    ForumSettings? Forum,
    DateTimeOffset CreatedAt,
    long Ordinal)
{
    /// <summary>Gets what applies to this channel's kind.</summary>
    internal ChannelTraits Traits => ChannelTraits.Of(Kind);
}

internal sealed record RoleSnapshot(
    ulong Id,
    ulong GuildId,
    string Name,
    ulong Permissions,
    int Color,
    bool Hoist,
    bool Mentionable,
    bool Managed,
    int Position,
    DateTimeOffset CreatedAt,
    long Ordinal)
{
    /// <summary>Gets whether this is the guild's <c>@everyone</c> role (same ID as the guild).</summary>
    internal bool IsEveryone => Id == GuildId;
}

internal sealed record MemberSnapshot(
    ulong GuildId,
    ulong UserId,
    string? Nick,
    ImmutableArray<ulong> RoleIds,
    DateTimeOffset JoinedAt,
    bool Deaf,
    bool Mute,
    long Ordinal);

internal sealed record MessageSnapshot(
    ulong Id,
    ulong ChannelId,
    ulong? GuildId,
    ulong AuthorId,
    string Content,
    DateTimeOffset Timestamp,
    DateTimeOffset? EditedTimestamp,
    bool Tts,
    bool Pinned,
    int Type,
    int Flags,
    long Ordinal)
{
    /// <summary>Gets the embeds, in order.</summary>
    internal ImmutableArray<EmbedValue> Embeds { get; init; } = [];

    /// <summary>Gets the attachments, in order.</summary>
    internal ImmutableArray<AttachmentValue> Attachments { get; init; } = [];

    /// <summary>Gets the IDs of the mentioned users, in order of appearance.</summary>
    internal ImmutableArray<ulong> MentionUserIds { get; init; } = [];

    /// <summary>Gets the IDs of the mentioned roles, in order of appearance.</summary>
    internal ImmutableArray<ulong> MentionRoleIds { get; init; } = [];

    /// <summary>Gets whether <c>@everyone</c> or <c>@here</c> was mentioned (and allowed).</summary>
    internal bool MentionEveryone { get; init; }

    /// <summary>Gets the reply target, or <see langword="null"/> for a message that replies to nothing.</summary>
    internal MessageReferenceValue? Reference { get; init; }

    /// <summary>Gets the replied-to message as it is now (one level only), or <see langword="null"/> when none or since deleted.</summary>
    internal MessageSnapshot? ReferencedMessage { get; init; }

    /// <summary>Gets the nonce sent by the client, if any.</summary>
    internal string? Nonce { get; init; }

    /// <summary>Gets the reactions, in the order of the first reaction with each emoji.</summary>
    internal ImmutableArray<ReactionSnapshot> Reactions { get; init; } = [];
}

/// <summary>
/// A guild with its <c>@everyone</c> role and, if requested, the bot's membership, as produced by the single
/// guild-creating commit.
/// </summary>
internal sealed record GuildCreation(GuildSnapshot Guild, RoleSnapshot Everyone, MemberSnapshot? BotMember);

/// <summary>
/// A detached picture of the whole state (messages excluded) taken under the lock, together with the
/// number of the last committed change it already reflects.
/// </summary>
internal sealed record StateProjection(
    long LastChangeNumber,
    BotIdentitySnapshot? Bot,
    ImmutableArray<UserSnapshot> Users,
    ImmutableArray<GuildSnapshot> Guilds,
    ImmutableArray<ChannelSnapshot> Channels,
    ImmutableArray<RoleSnapshot> Roles,
    ImmutableArray<MemberSnapshot> Members);
