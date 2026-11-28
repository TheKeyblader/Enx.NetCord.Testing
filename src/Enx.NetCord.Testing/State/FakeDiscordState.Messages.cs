using System.Collections.Immutable;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Enx.NetCord.Testing.State;

internal sealed partial class FakeDiscordState
{
    /// <summary>The oldest a message may be to be bulk deleted: Discord refuses messages older than two weeks.</summary>
    internal static readonly TimeSpan BulkDeleteMaxAge = TimeSpan.FromDays(14);

    private const int CrosspostedFlag = 1 << 0;
    private const int SuppressEmbedsFlag = 1 << 2;

    private const int ReplyMessageType = 19;

    // ChannelKind has no announcement value until the channel types of ADR 0013; crosspost only works on type 5.
    private const int AnnouncementChannelType = 5;

    private static readonly TimeSpan NonceWindow = TimeSpan.FromMinutes(5);

    // enforce_nonce: the message created by an author with a nonce, for the "past few minutes" window.
    private readonly Dictionary<(ulong AuthorId, string Nonce), (ulong MessageId, DateTimeOffset At)> _nonces = [];

    [GeneratedRegex(@"<@!?(?<user>\d+)>|<@&(?<role>\d+)>|(?<everyone>@everyone|@here)", RegexOptions.CultureInvariant)]
    private static partial Regex MentionPattern();

    // ----- Messages -------------------------------------------------------------------------

    /// <summary>
    /// Creates a message. The mentions are built from the content and <see cref="CreateMessageInput.AllowedMentions"/>; a
    /// <see cref="CreateMessageInput.Reference"/> makes it a reply (type <c>19</c>) when the target is in the same channel, and fails with
    /// <see cref="StateFailureKind.UnknownMessageReference"/> when it is not and the reply must fail. With
    /// <see cref="CreateMessageInput.EnforceNonce"/>, a message the same author created with the same nonce in the last five minutes
    /// is returned and nothing is committed.
    /// </summary>
    internal StateResult<MessageSnapshot> CreateMessage(StateCaller caller, CreateMessageInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Content);
        ValidateExplicitId(input.Id, nameof(input));

        lock (_gate)
        {
            var access = _access.ResolveChannel(caller, input.ChannelId);
            if (!access.TryGetValue(out var channel))
            {
                return access.Failure;
            }

            if (!_users.TryGetValue(input.AuthorId, out var author))
            {
                return new StateFailure(StateFailureKind.UnknownUser, input.AuthorId);
            }

            var now = ReadClock();
            if (input is { EnforceNonce: true, Nonce: { } nonce }
                && _nonces.TryGetValue((author.Id, nonce), out var prior)
                && now - prior.At <= NonceWindow
                && _messages.TryGetValue(prior.MessageId, out var duplicate))
            {
                return Snapshot(duplicate, channel.GuildId);
            }

            MessageEntity? referenced = null;
            MessageReferenceValue? reference = null;
            if (input.Reference is { } requested)
            {
                var sameChannel = requested.ChannelId is null || requested.ChannelId == channel.Id;
                if (sameChannel && _messages.TryGetValue(requested.MessageId, out referenced) && referenced.ChannelId == channel.Id)
                {
                    reference = new MessageReferenceValue(referenced.Id, channel.Id, channel.GuildId);
                }
                else if (requested.FailIfNotExists)
                {
                    return new StateFailure(StateFailureKind.UnknownMessageReference, requested.MessageId);
                }
                else
                {
                    referenced = null;
                }
            }

            var id = ResolveNewId(input.Id, now);
            _assigned.Add(id);
            var attachments = AllocateAttachments(input.Attachments, now);

            var message = new MessageEntity(id, channel.Id, author.Id, NextOrdinal())
            {
                Content = input.Content,
                Tts = input.Tts,
                Pinned = input.Pinned,
                Type = reference is not null && input.Type == 0 ? ReplyMessageType : input.Type,
                Flags = input.Flags,
                Reference = reference,
                Nonce = input.Nonce,
            };
            if (input.Embeds is not null)
            {
                message.Embeds.AddRange(input.Embeds);
            }

            message.Attachments.AddRange(attachments);
            ApplyMentions(message, input.Content, channel.GuildId, input.AllowedMentions, referenced?.AuthorId);

            _messages.Add(id, message);
            channel.MessageIds.Add(id);
            if (channel.LastMessageId is not { } lastId || id > lastId)
            {
                channel.LastMessageId = id;
            }

            if (input is { EnforceNonce: true, Nonce: { } enforcedNonce })
            {
                _nonces[(author.Id, enforcedNonce)] = (id, now);
            }

            var snapshot = Snapshot(message, channel.GuildId);
            Publish(new MessageCreated(NextChangeNumber(), snapshot, author.ToSnapshot(), AuthorMemberOf(channel, author.Id)));
            return snapshot;
        }
    }

    /// <summary>Looks a message up without any access control: for tests, seed and inspection, never for a handler of <c>Rest/</c>.</summary>
    internal MessageSnapshot? FindMessage(ulong channelId, ulong messageId)
    {
        lock (_gate)
        {
            return TryGetMessage(channelId, messageId, out var channel, out var message)
                ? Snapshot(message, channel.GuildId)
                : null;
        }
    }

    /// <summary>Reads a message for a caller: channel access first, then <see cref="StateFailureKind.UnknownMessage"/>.</summary>
    internal StateResult<MessageSnapshot> GetMessage(StateCaller caller, ulong channelId, ulong messageId)
    {
        lock (_gate)
        {
            if (!TryGetMessage(caller, channelId, messageId, out var channel, out var message, out var failure))
            {
                return failure;
            }

            return Snapshot(message, channel.GuildId);
        }
    }

    /// <summary>
    /// Lists messages of a channel, newest first (ordered by ID, as Discord does). With <paramref name="after"/>
    /// the <paramref name="limit"/> messages immediately following it are returned (still newest first).
    /// <paramref name="before"/> and <paramref name="after"/> are exclusive. With <paramref name="around"/> the result is centered
    /// on that ID: the <c>limit / 2</c> messages before it, then it and the following ones up to <paramref name="limit"/>. At most one cursor may be set.
    /// </summary>
    internal StateResult<ImmutableArray<MessageSnapshot>> ListMessages(
        StateCaller caller,
        ulong channelId,
        int limit,
        ulong? before = null,
        ulong? after = null,
        ulong? around = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(limit);
        if ((before is not null ? 1 : 0) + (after is not null ? 1 : 0) + (around is not null ? 1 : 0) > 1)
        {
            throw new ArgumentException("Specify at most one of before, after and around.");
        }

        lock (_gate)
        {
            var access = _access.ResolveChannel(caller, channelId);
            if (!access.TryGetValue(out var channel))
            {
                return access.Failure;
            }

            IEnumerable<ulong> ids;
            if (around is { } aroundId)
            {
                List<ulong> older = aroundId == 0
                    ? []
                    : channel.MessageIds.GetViewBetween(0, aroundId - 1).Reverse().Take(limit / 2).ToList();
                var newer = channel.MessageIds.GetViewBetween(aroundId, ulong.MaxValue).Take(limit - older.Count);
                ids = newer.Reverse().Concat(older);
            }
            else if (after is { } afterId)
            {
                ids = afterId == ulong.MaxValue
                    ? []
                    : channel.MessageIds.GetViewBetween(afterId + 1, ulong.MaxValue).Take(limit).Reverse();
            }
            else if (before is { } beforeId)
            {
                ids = beforeId == 0
                    ? []
                    : channel.MessageIds.GetViewBetween(0, beforeId - 1).Reverse().Take(limit);
            }
            else
            {
                ids = channel.MessageIds.Reverse().Take(limit);
            }

            return ids.Select(id => Snapshot(_messages[id], channel.GuildId)).ToImmutableArray();
        }
    }

    /// <summary>
    /// Replaces the content of a message. Like Discord, any edit counts: <c>edited_timestamp</c> is set to the
    /// clock and a change is committed even when the content is identical.
    /// </summary>
    internal StateResult<MessageSnapshot> EditMessageContent(StateCaller caller, ulong channelId, ulong messageId, string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        lock (_gate)
        {
            if (!TryGetMessage(caller, channelId, messageId, out var channel, out var message, out var failure))
            {
                return failure;
            }

            message.Content = content;
            message.EditedTimestamp = ReadClock();
            var snapshot = Snapshot(message, channel.GuildId);
            Publish(new MessageEdited(
                NextChangeNumber(),
                snapshot,
                _users[message.AuthorId].ToSnapshot(),
                AuthorMemberOf(channel, message.AuthorId)));
            return snapshot;
        }
    }

    /// <summary>
    /// Applies a partial edit. Like Discord, any edit counts: <c>edited_timestamp</c> is set and a change is committed even when
    /// nothing differs. Only the author edits the content, embeds and attachments (<see cref="StateFailureKind.CannotEditOtherAuthor"/>);
    /// another user may only change the flags (the <c>MANAGE_MESSAGES</c> permission is not computed yet). An edit that would leave the
    /// message without content, embed or attachment fails with <see cref="StateFailureKind.EmptyMessage"/>, before anything is written.
    /// </summary>
    internal StateResult<MessageSnapshot> EditMessage(StateCaller caller, EditMessageInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        lock (_gate)
        {
            if (!TryGetMessage(caller, input.ChannelId, input.MessageId, out var channel, out var message, out var failure))
            {
                return failure;
            }

            var editsContent = input.ChangeContent
                || input.Embeds is not null
                || input.RetainedAttachmentIds is not null
                || input.NewAttachments is { Count: > 0 };
            if (editsContent && caller == StateCaller.Bot && message.AuthorId != _botId)
            {
                return new StateFailure(StateFailureKind.CannotEditOtherAuthor, message.Id);
            }

            if (editsContent)
            {
                var content = input.ChangeContent ? input.Content ?? string.Empty : message.Content;
                var embedCount = input.Embeds?.Count ?? message.Embeds.Count;
                var keptCount = input.RetainedAttachmentIds is null
                    ? message.Attachments.Count
                    : message.Attachments.Count(a => input.RetainedAttachmentIds.Contains(a.Id));
                if (content.Length == 0 && embedCount == 0 && keptCount + (input.NewAttachments?.Count ?? 0) == 0)
                {
                    return new StateFailure(StateFailureKind.EmptyMessage, message.Id);
                }
            }

            var now = ReadClock();
            if (input.ChangeContent)
            {
                message.Content = input.Content ?? string.Empty;
                ApplyMentions(message, message.Content, channel.GuildId, input.AllowedMentions, null);
            }

            if (input.Embeds is not null)
            {
                message.Embeds.Clear();
                message.Embeds.AddRange(input.Embeds);
            }

            if (input.RetainedAttachmentIds is { } retained)
            {
                message.Attachments.RemoveAll(a => !retained.Contains(a.Id));
            }

            if (input.NewAttachments is { Count: > 0 } added)
            {
                message.Attachments.AddRange(AllocateAttachments(added, now));
            }

            if (input.Flags is { } flags)
            {
                message.Flags = (message.Flags & ~SuppressEmbedsFlag) | (flags & SuppressEmbedsFlag);
            }

            message.EditedTimestamp = now;
            var snapshot = Snapshot(message, channel.GuildId);
            Publish(new MessageEdited(
                NextChangeNumber(),
                snapshot,
                _users[message.AuthorId].ToSnapshot(),
                AuthorMemberOf(channel, message.AuthorId)));
            return snapshot;
        }
    }

    /// <summary>The author's membership in the channel's guild as of now, or null (DM, or not a member).</summary>
    private MemberSnapshot? AuthorMemberOf(ChannelEntity channel, ulong authorId) =>
        channel.GuildId is { } guildId && _members.TryGetValue((guildId, authorId), out var member)
            ? member.ToSnapshot()
            : null;

    /// <summary>Deletes a message and returns what it was. Its ID is never reused in this fake.</summary>
    internal StateResult<MessageSnapshot> DeleteMessage(StateCaller caller, ulong channelId, ulong messageId)
    {
        lock (_gate)
        {
            if (!TryGetMessage(caller, channelId, messageId, out var channel, out var message, out var failure))
            {
                return failure;
            }

            var snapshot = Snapshot(message, channel.GuildId);
            _messages.Remove(messageId);
            channel.MessageIds.Remove(messageId);
            Publish(new MessageDeleted(NextChangeNumber(), channel.GuildId, channelId, messageId));
            return snapshot;
        }
    }

    /// <summary>
    /// Deletes several messages of a guild channel in one commit. The age of each requested ID is read from its snowflake, whether or not
    /// the message exists, and one ID older than <see cref="BulkDeleteMaxAge"/> refuses the whole request before anything is deleted.
    /// IDs that name no message of the channel are ignored, as Discord does.
    /// </summary>
    /// <returns>The IDs actually deleted, in request order; a DM is refused with <see cref="StateFailureKind.CannotExecuteInDm"/>.</returns>
    internal StateResult<ImmutableArray<ulong>> BulkDeleteMessages(StateCaller caller, ulong channelId, IReadOnlyList<ulong> messageIds)
    {
        ArgumentNullException.ThrowIfNull(messageIds);

        lock (_gate)
        {
            var access = _access.ResolveChannel(caller, channelId);
            if (!access.TryGetValue(out var channel))
            {
                return access.Failure;
            }

            if (channel.GuildId is null)
            {
                return new StateFailure(StateFailureKind.CannotExecuteInDm, channel.Id);
            }

            var now = ReadClock();
            foreach (var id in messageIds)
            {
                if (now - Snowflake.CreatedAt(id) > BulkDeleteMaxAge)
                {
                    return new StateFailure(StateFailureKind.MessageTooOldForBulkDelete, id);
                }
            }

            var deleted = messageIds
                .Distinct()
                .Where(id => _messages.TryGetValue(id, out var message) && message.ChannelId == channel.Id)
                .ToImmutableArray();
            foreach (var id in deleted)
            {
                _messages.Remove(id);
                channel.MessageIds.Remove(id);
            }

            if (!deleted.IsEmpty)
            {
                Publish(new MessagesBulkDeleted(NextChangeNumber(), channel.GuildId, channel.Id, deleted));
            }

            return deleted;
        }
    }

    /// <summary>
    /// Publishes a message of an announcement channel: sets <c>CROSSPOSTED</c> and commits a <see cref="MessageEdited"/> (Discord fires
    /// <c>MESSAGE_UPDATE</c>). The fake has no channel to follow, so nothing else happens. Any other channel type is refused.
    /// </summary>
    internal StateResult<MessageSnapshot> CrosspostMessage(StateCaller caller, ulong channelId, ulong messageId)
    {
        lock (_gate)
        {
            if (!TryGetMessage(caller, channelId, messageId, out var channel, out var message, out var failure))
            {
                return failure;
            }

            if (channel.GuildId is null)
            {
                return new StateFailure(StateFailureKind.CannotExecuteInDm, channel.Id);
            }

            if ((int)channel.Kind != AnnouncementChannelType)
            {
                return new StateFailure(StateFailureKind.CannotExecuteOnChannelType, channel.Id);
            }

            message.Flags |= CrosspostedFlag;
            var snapshot = Snapshot(message, channel.GuildId);
            Publish(new MessageEdited(
                NextChangeNumber(),
                snapshot,
                _users[message.AuthorId].ToSnapshot(),
                AuthorMemberOf(channel, message.AuthorId)));
            return snapshot;
        }
    }

    /// <summary>Projects a message with the message it replies to (one level), if that one still exists. Call only under the lock.</summary>
    private MessageSnapshot Snapshot(MessageEntity message, ulong? guildId) =>
        message.ToSnapshot(
            guildId,
            message.Reference is { } reference && _messages.TryGetValue(reference.MessageId, out var referenced)
                ? referenced.ToSnapshot(guildId)
                : null);

    private List<AttachmentValue> AllocateAttachments(IReadOnlyList<NewAttachment>? files, DateTimeOffset now)
    {
        var result = new List<AttachmentValue>();
        foreach (var file in files ?? [])
        {
            var id = ResolveNewId(null, now);
            _assigned.Add(id);
            result.Add(new AttachmentValue(id, file.FileName, file.Size, file.ContentType, file.Title, file.Description));
        }

        return result;
    }

    /// <summary>
    /// Rebuilds the mentions of a message from its content: users that exist, roles of the channel's guild, and <c>@everyone</c>/<c>@here</c>,
    /// each only if <paramref name="allowed"/> lets it through (null allows everything). A reply also mentions the replied-to author
    /// unless <c>replied_user</c> is false. The <c>MENTION_EVERYONE</c> permission and the role <c>mentionable</c> flag are not computed yet.
    /// </summary>
    private void ApplyMentions(MessageEntity message, string content, ulong? guildId, AllowedMentionsValue? allowed, ulong? repliedAuthorId)
    {
        message.MentionUserIds.Clear();
        message.MentionRoleIds.Clear();
        message.MentionEveryone = false;

        foreach (Match match in MentionPattern().Matches(content))
        {
            if (match.Groups["user"].Success)
            {
                if (ulong.TryParse(match.Groups["user"].ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture, out var userId)
                    && _users.ContainsKey(userId)
                    && (allowed is null || allowed.ParseUsers || allowed.Users.Contains(userId))
                    && !message.MentionUserIds.Contains(userId))
                {
                    message.MentionUserIds.Add(userId);
                }
            }
            else if (match.Groups["role"].Success)
            {
                if (ulong.TryParse(match.Groups["role"].ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture, out var roleId)
                    && _roles.TryGetValue(roleId, out var role)
                    && role.GuildId == guildId
                    && (allowed is null || allowed.ParseRoles || allowed.Roles.Contains(roleId))
                    && !message.MentionRoleIds.Contains(roleId))
                {
                    message.MentionRoleIds.Add(roleId);
                }
            }
            else if (allowed is null || allowed.ParseEveryone)
            {
                message.MentionEveryone = true;
            }
        }

        if (repliedAuthorId is { } replied && (allowed is null || allowed.RepliedUser) && !message.MentionUserIds.Contains(replied))
        {
            message.MentionUserIds.Add(replied);
        }
    }

    /// <summary>Unchecked lookup, for <see cref="FindMessage"/> only.</summary>
    private bool TryGetMessage(ulong channelId, ulong messageId, out ChannelEntity channel, out MessageEntity message) =>
        TryGetMessage(StateCaller.Fixture, channelId, messageId, out channel, out message, out _);

    /// <summary>Channel access first, then the existence of the message (ADR 0004, amendment 04B: access before <c>10008</c>).</summary>
    private bool TryGetMessage(
        StateCaller caller,
        ulong channelId,
        ulong messageId,
        out ChannelEntity channel,
        out MessageEntity message,
        out StateFailure failure)
    {
        message = null!;
        var access = _access.ResolveChannel(caller, channelId);
        if (!access.TryGetValue(out channel!))
        {
            failure = access.Failure;
            return false;
        }

        if (!_messages.TryGetValue(messageId, out message!) || message.ChannelId != channelId)
        {
            failure = new StateFailure(StateFailureKind.UnknownMessage, messageId);
            return false;
        }

        failure = default;
        return true;
    }
}
