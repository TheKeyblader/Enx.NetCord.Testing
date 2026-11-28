using System.Collections.Immutable;

namespace Enx.NetCord.Testing.State;

internal sealed partial class FakeDiscordState
{
    /// <summary>The number of distinct emojis Discord allows on one message (<c>30010</c> beyond it).</summary>
    internal const int MaxDistinctReactions = 20;

    // ----- Reactions ------------------------------------------------------------------------

    /// <summary>
    /// Adds a user's reaction to a message. Idempotent like Discord's <c>PUT</c>: a user who already reacted with this emoji (of the same
    /// kind) changes nothing, commits nothing and still succeeds. A new emoji on a message that already has
    /// <see cref="MaxDistinctReactions"/> is refused with <see cref="StateFailureKind.MaxReactions"/>. The permissions
    /// (<c>READ_MESSAGE_HISTORY</c>, <c>ADD_REACTIONS</c>) are not computed yet (M4-REST-05).
    /// </summary>
    /// <param name="caller">Who asks; <see cref="StateCaller.Bot"/> from <c>Rest/</c>.</param>
    /// <param name="channelId">The channel of the message.</param>
    /// <param name="messageId">The message.</param>
    /// <param name="emoji">The emoji.</param>
    /// <param name="userId">The reacting user, which must exist.</param>
    /// <param name="burst">Whether it is a super reaction (the REST route only creates normal ones; the seed may create both).</param>
    /// <returns><see langword="true"/> when the reaction is new, <see langword="false"/> when it already existed.</returns>
    internal StateResult<bool> AddReaction(StateCaller caller, ulong channelId, ulong messageId, ReactionEmoji emoji, ulong userId, bool burst = false)
    {
        lock (_gate)
        {
            if (!TryGetMessage(caller, channelId, messageId, out var channel, out var message, out var failure))
            {
                return failure;
            }

            if (!_users.ContainsKey(userId))
            {
                return new StateFailure(StateFailureKind.UnknownUser, userId);
            }

            var reaction = FindReaction(message, emoji);
            if (reaction is null)
            {
                if (message.Reactions.Count >= MaxDistinctReactions)
                {
                    return new StateFailure(StateFailureKind.MaxReactions, message.Id);
                }

                reaction = new ReactionEntity(emoji);
                message.Reactions.Add(reaction);
            }

            if (!(burst ? reaction.BurstUserIds : reaction.NormalUserIds).Add(userId))
            {
                return false;
            }

            Publish(new ReactionAdded(
                NextChangeNumber(),
                channel.GuildId,
                channel.Id,
                message.Id,
                message.AuthorId,
                userId,
                AuthorMemberOf(channel, userId),
                reaction.Emoji,
                burst));
            return true;
        }
    }

    /// <summary>
    /// Removes a user's normal reaction. Unlike the <c>PUT</c> it is not idempotent: when the user has no such reaction Discord answers
    /// <c>10014</c> (hypothesis, <c>docs/parity/errors.md</c>), so the failure is <see cref="StateFailureKind.UnknownEmoji"/>.
    /// </summary>
    /// <param name="caller">Who asks; <see cref="StateCaller.Bot"/> from <c>Rest/</c>.</param>
    /// <param name="channelId">The channel of the message.</param>
    /// <param name="messageId">The message.</param>
    /// <param name="emoji">The emoji.</param>
    /// <param name="userId">The user whose reaction is removed (the bot for <c>…/@me</c>).</param>
    /// <returns><see langword="true"/> on success.</returns>
    internal StateResult<bool> RemoveReaction(StateCaller caller, ulong channelId, ulong messageId, ReactionEmoji emoji, ulong userId)
    {
        lock (_gate)
        {
            if (!TryGetMessage(caller, channelId, messageId, out var channel, out var message, out var failure))
            {
                return failure;
            }

            var reaction = FindReaction(message, emoji);
            if (reaction is null || !reaction.NormalUserIds.Remove(userId))
            {
                return new StateFailure(StateFailureKind.UnknownEmoji, message.Id);
            }

            if (reaction.IsEmpty)
            {
                message.Reactions.Remove(reaction);
            }

            Publish(new ReactionRemoved(NextChangeNumber(), channel.GuildId, channel.Id, message.Id, userId, reaction.Emoji, Burst: false));
            return true;
        }
    }

    /// <summary>
    /// Lists the users who reacted with an emoji, by ascending user ID. A message nobody reacted to with that emoji gives an empty list,
    /// not an error (hypothesis, <c>docs/parity/errors.md</c>).
    /// </summary>
    /// <param name="caller">Who asks; <see cref="StateCaller.Bot"/> from <c>Rest/</c>.</param>
    /// <param name="channelId">The channel of the message.</param>
    /// <param name="messageId">The message.</param>
    /// <param name="emoji">The emoji.</param>
    /// <param name="burst">Whether to list the super reactions instead of the normal ones.</param>
    /// <param name="after">The exclusive lower bound on the user ID, or <see langword="null"/>.</param>
    /// <param name="limit">The maximum number of users.</param>
    /// <returns>The users.</returns>
    internal StateResult<ImmutableArray<UserSnapshot>> ListReactionUsers(
        StateCaller caller,
        ulong channelId,
        ulong messageId,
        ReactionEmoji emoji,
        bool burst,
        ulong? after,
        int limit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(limit);

        lock (_gate)
        {
            if (!TryGetMessage(caller, channelId, messageId, out _, out var message, out var failure))
            {
                return failure;
            }

            if (FindReaction(message, emoji) is not { } reaction)
            {
                return ImmutableArray<UserSnapshot>.Empty;
            }

            var ids = burst ? reaction.BurstUserIds : reaction.NormalUserIds;
            IEnumerable<ulong> page = after is { } lower
                ? lower == ulong.MaxValue ? [] : ids.GetViewBetween(lower + 1, ulong.MaxValue)
                : ids;
            return page.Take(limit).Select(id => _users[id].ToSnapshot()).ToImmutableArray();
        }
    }

    /// <summary>
    /// Removes every reaction of a message. A message without reactions succeeds and commits nothing (no emoji is named, so
    /// there is nothing to be unknown).
    /// </summary>
    /// <param name="caller">Who asks; <see cref="StateCaller.Bot"/> from <c>Rest/</c>.</param>
    /// <param name="channelId">The channel of the message.</param>
    /// <param name="messageId">The message.</param>
    /// <returns><see langword="true"/> when reactions were removed, <see langword="false"/> when there were none.</returns>
    internal StateResult<bool> ClearReactions(StateCaller caller, ulong channelId, ulong messageId)
    {
        lock (_gate)
        {
            if (!TryGetMessage(caller, channelId, messageId, out var channel, out var message, out var failure))
            {
                return failure;
            }

            if (message.Reactions.Count == 0)
            {
                return false;
            }

            message.Reactions.Clear();
            Publish(new ReactionsCleared(NextChangeNumber(), channel.GuildId, channel.Id, message.Id));
            return true;
        }
    }

    /// <summary>
    /// Removes every reaction (normal and super) of one emoji from a message; <see cref="StateFailureKind.UnknownEmoji"/> when nobody
    /// reacted with it (hypothesis, <c>docs/parity/errors.md</c>).
    /// </summary>
    /// <param name="caller">Who asks; <see cref="StateCaller.Bot"/> from <c>Rest/</c>.</param>
    /// <param name="channelId">The channel of the message.</param>
    /// <param name="messageId">The message.</param>
    /// <param name="emoji">The emoji.</param>
    /// <returns><see langword="true"/> on success.</returns>
    internal StateResult<bool> ClearEmojiReactions(StateCaller caller, ulong channelId, ulong messageId, ReactionEmoji emoji)
    {
        lock (_gate)
        {
            if (!TryGetMessage(caller, channelId, messageId, out var channel, out var message, out var failure))
            {
                return failure;
            }

            if (FindReaction(message, emoji) is not { } reaction)
            {
                return new StateFailure(StateFailureKind.UnknownEmoji, message.Id);
            }

            message.Reactions.Remove(reaction);
            Publish(new ReactionEmojiCleared(NextChangeNumber(), channel.GuildId, channel.Id, message.Id, reaction.Emoji));
            return true;
        }
    }

    private static ReactionEntity? FindReaction(MessageEntity message, ReactionEmoji emoji)
        => message.Reactions.Find(reaction => reaction.Emoji.Equals(emoji));
}
