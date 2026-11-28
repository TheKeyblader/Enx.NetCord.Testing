using System.Collections.Immutable;

namespace Enx.NetCord.Testing.State;

// Detached, immutable values copied from the mutable entities under the state lock.
// `CreatedAt` is the instant encoded in the entity's snowflake; `Ordinal` is the internal creation
// order used to break ties between entities created at the same instant.

/// <summary>Discord channel type values used by the fake (<c>ChannelType</c> in the spec).</summary>
internal enum ChannelKind
{
    GuildText = 0,
    Dm = 1,
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
    DateTimeOffset CreatedAt,
    long Ordinal);

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
