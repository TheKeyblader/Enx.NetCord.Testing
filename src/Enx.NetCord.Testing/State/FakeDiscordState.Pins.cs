using System.Collections.Immutable;

namespace Enx.NetCord.Testing.State;

internal sealed partial class FakeDiscordState
{
    /// <summary>The number of messages Discord allows to pin in one channel (<c>30003</c> beyond it; the docs give 250).</summary>
    internal const int MaxPinsPerChannel = 250;

    private const int ChannelPinnedMessageType = 6;

    // ----- Pins -----------------------------------------------------------------------------

    /// <summary>
    /// Pins a message; only a default message (type 0) or a reply (type 19) can be pinned, any other type is refused with
    /// <see cref="StateFailureKind.CannotExecuteOnSystemMessage"/> (<c>50021</c>, hypothesis). Idempotent (hypothesis, <c>docs/parity/errors.md</c>): an already pinned message succeeds, commits nothing and
    /// creates no system message. A new pin commits, in this order, a <see cref="MessageEdited"/> (the <c>pinned</c> flag changed, as
    /// Discord fires <c>MESSAGE_UPDATE</c>), a <see cref="ChannelPinsUpdated"/> and a <see cref="MessageCreated"/> for the
    /// <c>CHANNEL_PINNED_MESSAGE</c> system message (type 6) that points at the pinned one and is authored by <paramref name="userId"/>.
    /// The pin counts in the channel's <c>last_message_id</c> like any message. The permission (<c>PIN_MESSAGES</c>) is not computed
    /// yet (M4-REST-05).
    /// </summary>
    /// <param name="caller">Who asks; <see cref="StateCaller.Bot"/> from <c>Rest/</c>.</param>
    /// <param name="channelId">The channel of the message.</param>
    /// <param name="messageId">The message to pin.</param>
    /// <param name="userId">The user who pins, which must exist.</param>
    /// <returns><see langword="true"/> when the message is newly pinned, <see langword="false"/> when it already was.</returns>
    internal StateResult<bool> PinMessage(StateCaller caller, ulong channelId, ulong messageId, ulong userId)
    {
        lock (_gate)
        {
            if (!TryGetMessage(caller, channelId, messageId, out var channel, out var message, out var failure))
            {
                return failure;
            }

            if (message.Type is not (0 or ReplyMessageType))
            {
                // Only a default message or a reply can be pinned: the system messages (join, pin, boost…) cannot (hypothesis, pending user review).
                return new StateFailure(StateFailureKind.CannotExecuteOnSystemMessage, message.Id);
            }

            if (!_users.TryGetValue(userId, out var pinner))
            {
                return new StateFailure(StateFailureKind.UnknownUser, userId);
            }

            if (message.PinnedAt is not null)
            {
                return false;
            }

            if (CountPins(channel.Id) >= MaxPinsPerChannel)
            {
                return new StateFailure(StateFailureKind.MaxPins, channel.Id);
            }

            var now = ReadClock();
            var pinnedAt = NextPinTime(channel, now);
            message.PinnedAt = pinnedAt;
            channel.LastPinTimestamp = pinnedAt;

            Publish(new MessageEdited(
                NextChangeNumber(),
                Snapshot(message, channel.GuildId),
                _users[message.AuthorId].ToSnapshot(),
                AuthorMemberOf(channel, message.AuthorId)));
            Publish(new ChannelPinsUpdated(NextChangeNumber(), channel.GuildId, channel.Id, pinnedAt));

            var systemId = ResolveNewId(null, now);
            _assigned.Add(systemId);
            var system = new MessageEntity(systemId, channel.Id, pinner.Id, NextOrdinal())
            {
                Type = ChannelPinnedMessageType,
                Reference = new MessageReferenceValue(message.Id, channel.Id, channel.GuildId),
            };
            _messages.Add(systemId, system);
            channel.MessageIds.Add(systemId);
            if (channel.LastMessageId is not { } lastId || systemId > lastId)
            {
                channel.LastMessageId = systemId;
            }

            Publish(new MessageCreated(NextChangeNumber(), Snapshot(system, channel.GuildId), pinner.ToSnapshot(), AuthorMemberOf(channel, pinner.Id)));
            return true;
        }
    }

    /// <summary>
    /// Unpins a message. Idempotent (hypothesis, <c>docs/parity/errors.md</c>): a message that is not pinned succeeds and commits nothing.
    /// An unpin commits a <see cref="MessageEdited"/> then a <see cref="ChannelPinsUpdated"/>; the system message of the pin stays.
    /// </summary>
    /// <param name="caller">Who asks; <see cref="StateCaller.Bot"/> from <c>Rest/</c>.</param>
    /// <param name="channelId">The channel of the message.</param>
    /// <param name="messageId">The message to unpin.</param>
    /// <returns><see langword="true"/> when the message was pinned, <see langword="false"/> when it was not.</returns>
    internal StateResult<bool> UnpinMessage(StateCaller caller, ulong channelId, ulong messageId)
    {
        lock (_gate)
        {
            if (!TryGetMessage(caller, channelId, messageId, out var channel, out var message, out var failure))
            {
                return failure;
            }

            if (message.PinnedAt is null)
            {
                return false;
            }

            message.PinnedAt = null;
            channel.LastPinTimestamp = channel.MessageIds
                .Select(id => _messages[id].PinnedAt)
                .Max();

            Publish(new MessageEdited(
                NextChangeNumber(),
                Snapshot(message, channel.GuildId),
                _users[message.AuthorId].ToSnapshot(),
                AuthorMemberOf(channel, message.AuthorId)));
            Publish(new ChannelPinsUpdated(NextChangeNumber(), channel.GuildId, channel.Id, channel.LastPinTimestamp));
            return true;
        }
    }

    /// <summary>
    /// Lists the pins of a channel, newest pin first (by pin time, then by message ID). <paramref name="before"/> is exclusive.
    /// A channel without chat (a category, a forum) refuses it with <see cref="StateFailureKind.CannotExecuteOnChannelType"/>
    /// (hypothesis UR-011, as for the list of messages). The <c>READ_MESSAGE_HISTORY</c> rule ("no pins will be returned") is not
    /// computed yet (M4-REST-05).
    /// </summary>
    /// <param name="caller">Who asks; <see cref="StateCaller.Bot"/> from <c>Rest/</c>.</param>
    /// <param name="channelId">The channel.</param>
    /// <param name="before">The exclusive upper bound on the pin time, or <see langword="null"/>.</param>
    /// <param name="limit">The maximum number of pins.</param>
    /// <returns>The page.</returns>
    internal StateResult<PinPage> ListPins(StateCaller caller, ulong channelId, DateTimeOffset? before, int limit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(limit);

        lock (_gate)
        {
            var access = _access.ResolveChannel(caller, channelId);
            if (!access.TryGetValue(out var channel))
            {
                return access.Failure;
            }

            if (!ChannelTraits.Of(channel.Kind).IsMessageable)
            {
                return new StateFailure(StateFailureKind.CannotExecuteOnChannelType, channel.Id);
            }

            var pins = channel.MessageIds
                .Select(id => _messages[id])
                .Where(message => message.PinnedAt is { } at && (before is not { } bound || at < bound))
                .OrderByDescending(message => message.PinnedAt)
                .ThenByDescending(message => message.Id)
                .ToList();

            var items = pins
                .Take(limit)
                .Select(message => new PinnedMessageSnapshot(message.PinnedAt!.Value, Snapshot(message, channel.GuildId)))
                .ToImmutableArray();
            return new PinPage(items, pins.Count > limit);
        }
    }

    private int CountPins(ulong channelId)
        => _channels.TryGetValue(channelId, out var channel)
            ? channel.MessageIds.Count(id => _messages[id].PinnedAt is not null)
            : 0;

    /// <summary>
    /// The time of a new pin: the clock cut to the second, and at least one second after the most recent pin of the channel. Whole
    /// seconds, strictly increasing, because NetCord pages <c>GET …/messages/pins</c> with <c>before</c> written to the second and
    /// Discord compares it exclusively: two pins in one second (always the case with a frozen virtual clock) would be skipped.
    /// </summary>
    private static DateTimeOffset NextPinTime(ChannelEntity channel, DateTimeOffset now)
    {
        var at = now.AddTicks(-(now.Ticks % TimeSpan.TicksPerSecond));
        return channel.LastPinTimestamp is { } last && at <= last ? last.AddSeconds(1) : at;
    }
}
