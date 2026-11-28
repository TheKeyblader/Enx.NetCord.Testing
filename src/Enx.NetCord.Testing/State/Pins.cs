using System.Collections.Immutable;

namespace Enx.NetCord.Testing.State;

/// <summary>A pinned message with the time it was pinned (the Discord <c>PinnedMessageResponse</c>).</summary>
/// <param name="PinnedAt">When the message was pinned, with second precision.</param>
/// <param name="Message">The message as it is now.</param>
internal sealed record PinnedMessageSnapshot(DateTimeOffset PinnedAt, MessageSnapshot Message);

/// <summary>One page of the pins of a channel, newest pin first.</summary>
/// <param name="Items">The pins of the page.</param>
/// <param name="HasMore">Whether older pins remain after this page.</param>
internal sealed record PinPage(ImmutableArray<PinnedMessageSnapshot> Items, bool HasMore);

/// <summary>
/// A message was pinned or unpinned in a channel (the Gateway turns it into <c>CHANNEL_PINS_UPDATE</c>). It is not committed when a
/// pinned message is deleted (the docs: "This is not sent when a pinned message is deleted").
/// </summary>
/// <param name="Number">The change number.</param>
/// <param name="GuildId">The guild of the channel, or <see langword="null"/> in a DM.</param>
/// <param name="ChannelId">The channel ID.</param>
/// <param name="LastPinTimestamp">When the most recent remaining pin was made, or <see langword="null"/> when no message is pinned any more.</param>
internal sealed record ChannelPinsUpdated(long Number, ulong? GuildId, ulong ChannelId, DateTimeOffset? LastPinTimestamp) : StateChange(Number);

/// <summary>
/// A user started typing in a channel (the Gateway turns it into <c>TYPING_START</c>). It is a pure fact: the typing indicator is not
/// stored, so nothing reads who is typing back (Discord expires it after 10 seconds).
/// </summary>
/// <param name="Number">The change number.</param>
/// <param name="GuildId">The guild of the channel, or <see langword="null"/> in a DM.</param>
/// <param name="ChannelId">The channel ID.</param>
/// <param name="UserId">The user who started typing.</param>
/// <param name="Timestamp">When it happened (the event carries it in Unix seconds).</param>
/// <param name="Member">The membership of that user in the guild, or <see langword="null"/> (DM, or not a member).</param>
internal sealed record TypingStarted(
    long Number,
    ulong? GuildId,
    ulong ChannelId,
    ulong UserId,
    DateTimeOffset Timestamp,
    MemberSnapshot? Member) : StateChange(Number);
