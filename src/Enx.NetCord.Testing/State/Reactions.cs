using System.Globalization;

namespace Enx.NetCord.Testing.State;

/// <summary>
/// An emoji a message can be reacted with: a unicode emoji (<see cref="Id"/> is <see langword="null"/>, <see cref="Name"/> is
/// the emoji itself) or a custom one (<see cref="Id"/> is its snowflake, <see cref="Name"/> the name it was first used with).
/// A custom emoji is identified by its ID alone, as Discord does; a unicode emoji by its characters.
/// </summary>
/// <param name="Name">The unicode emoji, or the name of the custom emoji.</param>
/// <param name="Id">The custom emoji ID, or <see langword="null"/> for a unicode emoji.</param>
internal readonly record struct ReactionEmoji(string Name, ulong? Id)
{
    private const int MaxCustomNameLength = 32;
    private const char ReplacementCharacter = '�';

    /// <summary>Gets whether two emojis are the same one: same ID for a custom emoji, same characters for a unicode one.</summary>
    /// <param name="other">The other emoji.</param>
    /// <returns><see langword="true"/> when they designate the same emoji.</returns>
    public bool Equals(ReactionEmoji other)
        => Id is not null || other.Id is not null
            ? Id == other.Id
            : string.Equals(Name, other.Name, StringComparison.Ordinal);

    /// <summary>Gets a hash code consistent with <see cref="Equals(ReactionEmoji)"/>.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => Id is { } id ? id.GetHashCode() : StringComparer.Ordinal.GetHashCode(Name);

    /// <summary>
    /// Reads the <c>emoji_name</c> segment of a reaction route (already percent-decoded by the router): <c>name:id</c> for a custom
    /// emoji, otherwise a unicode emoji.
    /// </summary>
    /// <remarks>
    /// The fake has no table of unicode emojis. A segment is taken for one when it has a non-ASCII character and no ASCII letter,
    /// whitespace or control character (so a keycap like <c>1&#xFE0F;&#x20E3;</c> passes and <c>abc</c> or <c>1</c> does not), nor a replacement
    /// character (what a malformed percent-encoding decodes to). A custom emoji needs a name of letters, digits and underscores and a snowflake ID;
    /// its existence is not checked until the emojis of a guild are modeled (M4-REST-04).
    /// </remarks>
    /// <param name="segment">The decoded path segment.</param>
    /// <param name="emoji">The emoji on success.</param>
    /// <returns><see langword="true"/> when the segment is an emoji; Discord answers <c>10014</c> otherwise.</returns>
    public static bool TryParse(string? segment, out ReactionEmoji emoji)
    {
        emoji = default;
        if (string.IsNullOrEmpty(segment))
        {
            return false;
        }

        var colon = segment.IndexOf(':', StringComparison.Ordinal);
        if (colon >= 0)
        {
            var name = segment[..colon];
            if (name.Length is 0 or > MaxCustomNameLength
                || !name.All(static c => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_')
                || !ulong.TryParse(segment.AsSpan(colon + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var id)
                || id == 0)
            {
                return false;
            }

            emoji = new ReactionEmoji(name, id);
            return true;
        }

        var hasNonAscii = false;
        foreach (var c in segment)
        {
            if (c == ReplacementCharacter || char.IsWhiteSpace(c) || char.IsControl(c) || char.IsAsciiLetter(c))
            {
                return false;
            }

            hasNonAscii |= c > '\u007F';
        }

        if (!hasNonAscii)
        {
            return false;
        }

        emoji = new ReactionEmoji(segment, null);
        return true;
    }
}

/// <summary>The users who reacted to a message with one emoji; the entity never leaves the state.</summary>
internal sealed class ReactionEntity(ReactionEmoji emoji)
{
    internal ReactionEmoji Emoji { get; } = emoji;

    /// <summary>Gets the users with a normal reaction, ordered by ID (the order of the listing route).</summary>
    internal SortedSet<ulong> NormalUserIds { get; } = [];

    /// <summary>Gets the users with a super reaction, ordered by ID.</summary>
    internal SortedSet<ulong> BurstUserIds { get; } = [];

    internal bool IsEmpty => NormalUserIds.Count == 0 && BurstUserIds.Count == 0;

    /// <summary>Projects the reaction as one user (the bot, the only identity of the REST routes) sees it.</summary>
    internal ReactionSnapshot ToSnapshot(ulong? viewerId) => new(
        Emoji,
        NormalUserIds.Count,
        BurstUserIds.Count,
        viewerId is { } viewer && NormalUserIds.Contains(viewer),
        viewerId is { } burstViewer && BurstUserIds.Contains(burstViewer));
}

/// <summary>A detached reaction of a message: the counts and whether the bot is one of the reactors.</summary>
/// <param name="Emoji">The emoji.</param>
/// <param name="NormalCount">The number of normal reactions.</param>
/// <param name="BurstCount">The number of super reactions.</param>
/// <param name="Me">Whether the bot reacted normally.</param>
/// <param name="MeBurst">Whether the bot super-reacted.</param>
internal sealed record ReactionSnapshot(ReactionEmoji Emoji, int NormalCount, int BurstCount, bool Me, bool MeBurst);

/// <summary>A user reacted to a message (the Gateway turns it into <c>MESSAGE_REACTION_ADD</c>).</summary>
/// <param name="Number">The change number.</param>
/// <param name="GuildId">The guild of the channel, or <see langword="null"/> in a DM.</param>
/// <param name="ChannelId">The channel ID.</param>
/// <param name="MessageId">The message ID.</param>
/// <param name="MessageAuthorId">The author of the message.</param>
/// <param name="UserId">The user who reacted.</param>
/// <param name="Member">The membership of that user in the guild, or <see langword="null"/> (DM, or not a member).</param>
/// <param name="Emoji">The emoji.</param>
/// <param name="Burst">Whether it is a super reaction.</param>
internal sealed record ReactionAdded(
    long Number,
    ulong? GuildId,
    ulong ChannelId,
    ulong MessageId,
    ulong MessageAuthorId,
    ulong UserId,
    MemberSnapshot? Member,
    ReactionEmoji Emoji,
    bool Burst) : StateChange(Number);

/// <summary>A user's reaction was removed (the Gateway turns it into <c>MESSAGE_REACTION_REMOVE</c>).</summary>
/// <param name="Number">The change number.</param>
/// <param name="GuildId">The guild of the channel, or <see langword="null"/> in a DM.</param>
/// <param name="ChannelId">The channel ID.</param>
/// <param name="MessageId">The message ID.</param>
/// <param name="UserId">The user whose reaction was removed.</param>
/// <param name="Emoji">The emoji.</param>
/// <param name="Burst">Whether it was a super reaction.</param>
internal sealed record ReactionRemoved(
    long Number,
    ulong? GuildId,
    ulong ChannelId,
    ulong MessageId,
    ulong UserId,
    ReactionEmoji Emoji,
    bool Burst) : StateChange(Number);

/// <summary>Every reaction of a message was removed (the Gateway turns it into <c>MESSAGE_REACTION_REMOVE_ALL</c>).</summary>
/// <param name="Number">The change number.</param>
/// <param name="GuildId">The guild of the channel, or <see langword="null"/> in a DM.</param>
/// <param name="ChannelId">The channel ID.</param>
/// <param name="MessageId">The message ID.</param>
internal sealed record ReactionsCleared(long Number, ulong? GuildId, ulong ChannelId, ulong MessageId) : StateChange(Number);

/// <summary>Every reaction of one emoji was removed from a message (the Gateway turns it into <c>MESSAGE_REACTION_REMOVE_EMOJI</c>).</summary>
/// <param name="Number">The change number.</param>
/// <param name="GuildId">The guild of the channel, or <see langword="null"/> in a DM.</param>
/// <param name="ChannelId">The channel ID.</param>
/// <param name="MessageId">The message ID.</param>
/// <param name="Emoji">The emoji.</param>
internal sealed record ReactionEmojiCleared(long Number, ulong? GuildId, ulong ChannelId, ulong MessageId, ReactionEmoji Emoji) : StateChange(Number);
