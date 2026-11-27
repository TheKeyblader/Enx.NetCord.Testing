using System.Collections.Immutable;

namespace Enx.NetCord.Testing.State;

/// <summary>
/// The single source of truth of one fake (ADR 0003): mutable entities behind one synchronous lock.
/// Every read and write runs under that lock, nothing mutable escapes (callers get snapshots or IDs),
/// and each effective mutation commits one numbered <see cref="StateChange"/>. No await, I/O,
/// serialization, user callback or dispatch happens inside the critical section. Route families add
/// their operations as further partial files.
/// </summary>
internal sealed partial class FakeDiscordState
{
    /// <summary>Default <c>@everyone</c> permissions of a new guild (Discord's default bitfield).</summary>
    internal const ulong DefaultEveryonePermissions = 1071698660929UL;

    private readonly Lock _gate = new();
    private readonly TimeProvider _clock;
    private readonly ISnowflakeGenerator _ids;

    private readonly Dictionary<ulong, UserEntity> _users = [];
    private readonly Dictionary<ulong, ApplicationEntity> _applications = [];
    private readonly Dictionary<ulong, GuildEntity> _guilds = [];
    private readonly Dictionary<ulong, ChannelEntity> _channels = [];
    private readonly Dictionary<ulong, RoleEntity> _roles = [];
    private readonly Dictionary<(ulong GuildId, ulong UserId), MemberEntity> _members = [];
    private readonly Dictionary<ulong, MessageEntity> _messages = [];

    // Every ID ever assigned in this fake (explicit or generated), including deleted entities.
    private readonly HashSet<ulong> _assigned = [];

    private ulong? _botId;
    private ulong? _lastAllocated;
    private long _ordinal;
    private long _lastChangeNumber;
    private ChangeSubscription? _subscriber;

    internal FakeDiscordState(TimeProvider clock, ISnowflakeGenerator ids)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(ids);
        _clock = clock;
        _ids = ids;
    }

    /// <summary>Creates a state from public options, applying the fixed virtual clock and default generator.</summary>
    internal static FakeDiscordState Create(FakeDiscordOptions? options = null) =>
        new(
            options?.TimeProvider ?? new VirtualTimeProvider(),
            options?.SnowflakeGenerator ?? new DefaultSnowflakeGenerator());

    // ----- Change flow ---------------------------------------------------------------------

    /// <summary>
    /// Subscribes the Gateway: installs an unbounded FIFO channel under the lock and returns it with the
    /// projection taken under the same lock. Only one subscriber may exist at a time.
    /// </summary>
    internal ChangeSubscription SubscribeGateway()
    {
        lock (_gate)
        {
            if (_subscriber is not null)
            {
                throw new InvalidOperationException("The state already has a Gateway subscriber.");
            }

            var subscription = new ChangeSubscription(this, ProjectUnderLock());
            _subscriber = subscription;
            return subscription;
        }
    }

    internal void Unsubscribe(ChangeSubscription subscription)
    {
        lock (_gate)
        {
            if (!ReferenceEquals(_subscriber, subscription))
            {
                return;
            }

            _subscriber = null;
            subscription.Complete();
        }
    }

    /// <summary>Takes a detached projection of the state and its last change number (for a new session).</summary>
    internal StateProjection TakeProjection()
    {
        lock (_gate)
        {
            return ProjectUnderLock();
        }
    }

    private StateProjection ProjectUnderLock() => new(
        _lastChangeNumber,
        _botId is { } botId ? new BotIdentitySnapshot(_users[botId].ToSnapshot(), _applications[botId].ToSnapshot()) : null,
        [.. _users.Values.OrderBy(x => x.Ordinal).Select(x => x.ToSnapshot())],
        [.. _guilds.Values.OrderBy(x => x.Ordinal).Select(x => x.ToSnapshot())],
        [.. _channels.Values.OrderBy(x => x.Ordinal).Select(x => x.ToSnapshot())],
        [.. _roles.Values.OrderBy(x => x.Ordinal).Select(x => x.ToSnapshot())],
        [.. _members.Values.OrderBy(x => x.Ordinal).Select(x => x.ToSnapshot())]);

    /// <summary>Allocates the next change number. Call only under the lock, once the mutation is applied.</summary>
    private long NextChangeNumber() => ++_lastChangeNumber;

    /// <summary>Queues a committed change if the Gateway is subscribed; otherwise nothing is retained.</summary>
    private void Publish(StateChange change) => _subscriber?.Write(change);

    // ----- IDs, time, ordinals (call only under the lock) -----------------------------------

    private DateTimeOffset ReadClock() => _clock.GetUtcNow().ToUniversalTime();

    private long NextOrdinal() => ++_ordinal;

    private static void ValidateExplicitId(ulong? id, string parameterName)
    {
        if (id == 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, id, "An explicit snowflake must be nonzero.");
        }
    }

    /// <summary>
    /// Resolves the ID of a new entity without recording it: validates an explicit ID or allocates one under
    /// the generator contract, then checks that it was never assigned. Throws before any commit.
    /// </summary>
    private ulong ResolveNewId(ulong? explicitId, DateTimeOffset now)
    {
        ulong id;
        if (explicitId is { } given)
        {
            ValidateExplicitId(given, nameof(explicitId));
            id = given;
        }
        else
        {
            id = Allocate(now);
        }

        if (_assigned.Contains(id))
        {
            throw new InvalidOperationException($"The snowflake {id} was already assigned in this fake.");
        }

        return id;
    }

    private ulong Allocate(DateTimeOffset now)
    {
        var expectedMilliseconds = Snowflake.MillisecondsOf(now)
            ?? throw new InvalidOperationException(
                "The current instant is outside the 42-bit snowflake timestamp range.");

        var id = _ids.Next(now);
        if (id == 0)
        {
            throw new InvalidOperationException("The snowflake generator returned the zero ID.");
        }

        if (_lastAllocated is { } last && id <= last)
        {
            throw new InvalidOperationException(
                $"The snowflake generator returned {id}, which does not strictly follow {last}.");
        }

        if ((long)(id >> Snowflake.CounterBits) != expectedMilliseconds)
        {
            throw new InvalidOperationException(
                $"The snowflake generator returned {id}, which does not encode the requested millisecond.");
        }

        _lastAllocated = id;
        return id;
    }

    // ----- Bot identity, users --------------------------------------------------------------

    /// <summary>
    /// Creates the bot user and its application, which share one ID, in one commit. Throws
    /// <see cref="InvalidOperationException"/> if the identity already exists or the ID is unavailable.
    /// </summary>
    internal BotIdentitySnapshot CreateBotIdentity(CreateBotIdentityInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Username);
        ValidateExplicitId(input.Id, nameof(input));

        lock (_gate)
        {
            if (_botId is not null)
            {
                throw new InvalidOperationException("The bot identity already exists.");
            }

            var now = ReadClock();
            var id = ResolveNewId(input.Id, now);
            _assigned.Add(id); // user and application share this one assignment
            _botId = id;

            var user = new UserEntity(id, input.Username, NextOrdinal()) { IsBot = true };
            var application = new ApplicationEntity(id, input.ApplicationName ?? input.Username, NextOrdinal())
            {
                Description = input.ApplicationDescription ?? string.Empty,
            };
            _users.Add(id, user);
            _applications.Add(id, application);

            var identity = new BotIdentitySnapshot(user.ToSnapshot(), application.ToSnapshot());
            Publish(new BotIdentityCreated(NextChangeNumber(), identity.User, identity.Application));
            return identity;
        }
    }

    internal BotIdentitySnapshot? FindBotIdentity()
    {
        lock (_gate)
        {
            return _botId is { } id
                ? new BotIdentitySnapshot(_users[id].ToSnapshot(), _applications[id].ToSnapshot())
                : null;
        }
    }

    internal UserSnapshot CreateUser(CreateUserInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Username);
        ValidateExplicitId(input.Id, nameof(input));

        lock (_gate)
        {
            var now = ReadClock();
            var id = ResolveNewId(input.Id, now);
            _assigned.Add(id);

            var user = new UserEntity(id, input.Username, NextOrdinal())
            {
                GlobalName = input.GlobalName,
                AvatarHash = input.AvatarHash,
                IsBot = input.IsBot,
            };
            _users.Add(id, user);

            var snapshot = user.ToSnapshot();
            Publish(new UserCreated(NextChangeNumber(), snapshot));
            return snapshot;
        }
    }

    internal UserSnapshot? FindUser(ulong userId)
    {
        lock (_gate)
        {
            return _users.TryGetValue(userId, out var user) ? user.ToSnapshot() : null;
        }
    }

    internal ApplicationSnapshot? FindApplication(ulong applicationId)
    {
        lock (_gate)
        {
            return _applications.TryGetValue(applicationId, out var app) ? app.ToSnapshot() : null;
        }
    }

    // ----- Guilds, roles, channels, members -------------------------------------------------

    /// <summary>
    /// Creates a guild and its <c>@everyone</c> role (same ID as the guild) in one commit, so no reader or
    /// subscriber can see one without the other.
    /// </summary>
    internal StateResult<GuildCreation> CreateGuild(CreateGuildInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Name);
        ValidateExplicitId(input.Id, nameof(input));

        lock (_gate)
        {
            if (input.OwnerId is { } ownerId && !_users.ContainsKey(ownerId))
            {
                return new StateFailure(StateFailureKind.UnknownUser, ownerId);
            }

            var now = ReadClock();
            var id = ResolveNewId(input.Id, now);
            _assigned.Add(id);

            var guild = new GuildEntity(id, input.Name, NextOrdinal())
            {
                OwnerId = input.OwnerId,
                IconHash = input.IconHash,
                Description = input.Description,
            };
            var everyone = new RoleEntity(id, id, "@everyone", NextOrdinal())
            {
                Permissions = input.EveryonePermissions ?? DefaultEveryonePermissions,
                Position = 0,
            };
            _guilds.Add(id, guild);
            _roles.Add(id, everyone);
            guild.RoleIds.Add(id);

            var creation = new GuildCreation(guild.ToSnapshot(), everyone.ToSnapshot());
            Publish(new GuildCreated(NextChangeNumber(), creation.Guild, creation.Everyone));
            return creation;
        }
    }

    internal GuildSnapshot? FindGuild(ulong guildId)
    {
        lock (_gate)
        {
            return _guilds.TryGetValue(guildId, out var guild) ? guild.ToSnapshot() : null;
        }
    }

    internal StateResult<RoleSnapshot> CreateRole(CreateRoleInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Name);
        ValidateExplicitId(input.Id, nameof(input));

        lock (_gate)
        {
            if (!_guilds.TryGetValue(input.GuildId, out var guild))
            {
                return new StateFailure(StateFailureKind.UnknownGuild, input.GuildId);
            }

            var now = ReadClock();
            var id = ResolveNewId(input.Id, now);
            _assigned.Add(id);

            // Default: on top of the existing roles. Routes may reposition later.
            var position = input.Position ?? guild.RoleIds.Max(roleId => _roles[roleId].Position) + 1;
            var role = new RoleEntity(id, guild.Id, input.Name, NextOrdinal())
            {
                Permissions = input.Permissions,
                Color = input.Color,
                Hoist = input.Hoist,
                Mentionable = input.Mentionable,
                Position = position,
            };
            _roles.Add(id, role);
            guild.RoleIds.Add(id);

            var snapshot = role.ToSnapshot();
            Publish(new RoleCreated(NextChangeNumber(), snapshot));
            return snapshot;
        }
    }

    internal RoleSnapshot? FindRole(ulong roleId)
    {
        lock (_gate)
        {
            return _roles.TryGetValue(roleId, out var role) ? role.ToSnapshot() : null;
        }
    }

    internal StateResult<ImmutableArray<RoleSnapshot>> ListGuildRoles(ulong guildId)
    {
        lock (_gate)
        {
            if (!_guilds.TryGetValue(guildId, out var guild))
            {
                return new StateFailure(StateFailureKind.UnknownGuild, guildId);
            }

            return guild.RoleIds.Select(id => _roles[id].ToSnapshot()).ToImmutableArray();
        }
    }

    internal StateResult<ChannelSnapshot> CreateTextChannel(CreateTextChannelInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Name);
        ValidateExplicitId(input.Id, nameof(input));

        lock (_gate)
        {
            if (!_guilds.TryGetValue(input.GuildId, out var guild))
            {
                return new StateFailure(StateFailureKind.UnknownGuild, input.GuildId);
            }

            var now = ReadClock();
            var id = ResolveNewId(input.Id, now);
            _assigned.Add(id);

            var channel = new ChannelEntity(id, ChannelKind.GuildText, NextOrdinal())
            {
                GuildId = guild.Id,
                Name = input.Name,
                Topic = input.Topic,
                Position = input.Position ?? guild.ChannelIds.Count,
                Nsfw = input.Nsfw,
            };
            _channels.Add(id, channel);
            guild.ChannelIds.Add(id);

            var snapshot = channel.ToSnapshot();
            Publish(new ChannelCreated(NextChangeNumber(), snapshot));
            return snapshot;
        }
    }

    /// <summary>
    /// Returns the DM channel of a recipient, creating it if none exists, under one lock: a recipient can never
    /// end up with two DM channels. When the channel already exists nothing is allocated, committed or published,
    /// and an explicit <see cref="CreateDmChannelInput.Id"/> is ignored. The other party is implicitly the bot.
    /// </summary>
    internal StateResult<(ChannelSnapshot Channel, bool Created)> GetOrCreateDmChannel(CreateDmChannelInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ValidateExplicitId(input.Id, nameof(input));

        lock (_gate)
        {
            if (!_users.ContainsKey(input.RecipientId))
            {
                return new StateFailure(StateFailureKind.UnknownUser, input.RecipientId);
            }

            var existing = FindDmChannelUnderLock(input.RecipientId);
            if (existing is not null)
            {
                return (existing.ToSnapshot(), false);
            }

            var now = ReadClock();
            var id = ResolveNewId(input.Id, now);
            _assigned.Add(id);

            var channel = new ChannelEntity(id, ChannelKind.Dm, NextOrdinal());
            channel.RecipientIds.Add(input.RecipientId);
            _channels.Add(id, channel);

            var snapshot = channel.ToSnapshot();
            Publish(new ChannelCreated(NextChangeNumber(), snapshot));
            return (snapshot, true);
        }
    }

    private ChannelEntity? FindDmChannelUnderLock(ulong recipientId) =>
        _channels.Values
            .Where(c => c.Kind == ChannelKind.Dm && c.RecipientIds.Contains(recipientId))
            .OrderBy(c => c.Ordinal)
            .FirstOrDefault();

    internal ChannelSnapshot? FindChannel(ulong channelId)
    {
        lock (_gate)
        {
            return _channels.TryGetValue(channelId, out var channel) ? channel.ToSnapshot() : null;
        }
    }

    /// <summary>Finds the DM channel whose only recipient is the given user, if any (oldest first).</summary>
    internal ChannelSnapshot? FindDmChannel(ulong recipientId)
    {
        lock (_gate)
        {
            return FindDmChannelUnderLock(recipientId)?.ToSnapshot();
        }
    }

    internal StateResult<ImmutableArray<ChannelSnapshot>> ListGuildChannels(ulong guildId)
    {
        lock (_gate)
        {
            if (!_guilds.TryGetValue(guildId, out var guild))
            {
                return new StateFailure(StateFailureKind.UnknownGuild, guildId);
            }

            return guild.ChannelIds.Select(id => _channels[id].ToSnapshot()).ToImmutableArray();
        }
    }

    internal StateResult<MemberSnapshot> AddMember(AddMemberInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var requestedRoleIds = input.RoleIds?.ToArray() ?? []; // copy: the caller's list may change under us

        lock (_gate)
        {
            if (!_guilds.TryGetValue(input.GuildId, out var guild))
            {
                return new StateFailure(StateFailureKind.UnknownGuild, input.GuildId);
            }

            if (!_users.TryGetValue(input.UserId, out var user))
            {
                return new StateFailure(StateFailureKind.UnknownUser, input.UserId);
            }

            if (_members.ContainsKey((guild.Id, user.Id)))
            {
                return new StateFailure(StateFailureKind.MemberAlreadyExists, user.Id);
            }

            // Validate the whole role list before writing anything; @everyone is implicit, never listed.
            var roleIds = new List<ulong>();
            foreach (var roleId in requestedRoleIds)
            {
                if (!_roles.TryGetValue(roleId, out var role) || role.GuildId != guild.Id)
                {
                    return new StateFailure(StateFailureKind.UnknownRole, roleId);
                }

                if (roleId != guild.Id && !roleIds.Contains(roleId))
                {
                    roleIds.Add(roleId);
                }
            }

            var now = ReadClock();
            var member = new MemberEntity(guild.Id, user.Id, input.JoinedAt ?? now, NextOrdinal())
            {
                Nick = input.Nick,
            };
            member.RoleIds.AddRange(roleIds);
            _members.Add((guild.Id, user.Id), member);
            guild.MemberUserIds.Add(user.Id);

            var snapshot = member.ToSnapshot();
            Publish(new MemberAdded(NextChangeNumber(), snapshot, user.ToSnapshot()));
            return snapshot;
        }
    }

    internal MemberSnapshot? FindMember(ulong guildId, ulong userId)
    {
        lock (_gate)
        {
            return _members.TryGetValue((guildId, userId), out var member) ? member.ToSnapshot() : null;
        }
    }

    internal StateResult<ImmutableArray<MemberSnapshot>> ListGuildMembers(ulong guildId)
    {
        lock (_gate)
        {
            if (!_guilds.TryGetValue(guildId, out var guild))
            {
                return new StateFailure(StateFailureKind.UnknownGuild, guildId);
            }

            return guild.MemberUserIds.Select(id => _members[(guildId, id)].ToSnapshot()).ToImmutableArray();
        }
    }

    // ----- Messages -------------------------------------------------------------------------

    internal StateResult<MessageSnapshot> CreateMessage(CreateMessageInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Content);
        ValidateExplicitId(input.Id, nameof(input));

        lock (_gate)
        {
            if (!_channels.TryGetValue(input.ChannelId, out var channel))
            {
                return new StateFailure(StateFailureKind.UnknownChannel, input.ChannelId);
            }

            if (!_users.TryGetValue(input.AuthorId, out var author))
            {
                return new StateFailure(StateFailureKind.UnknownUser, input.AuthorId);
            }

            var now = ReadClock();
            var id = ResolveNewId(input.Id, now);
            _assigned.Add(id);

            var message = new MessageEntity(id, channel.Id, author.Id, NextOrdinal())
            {
                Content = input.Content,
                Tts = input.Tts,
                Pinned = input.Pinned,
                Type = input.Type,
                Flags = input.Flags,
            };
            _messages.Add(id, message);
            channel.MessageIds.Add(id);
            if (channel.LastMessageId is not { } lastId || id > lastId)
            {
                channel.LastMessageId = id;
            }

            var snapshot = message.ToSnapshot(channel.GuildId);
            Publish(new MessageCreated(NextChangeNumber(), snapshot, author.ToSnapshot(), AuthorMemberOf(channel, author.Id)));
            return snapshot;
        }
    }

    internal MessageSnapshot? FindMessage(ulong channelId, ulong messageId)
    {
        lock (_gate)
        {
            return TryGetMessage(channelId, messageId, out var channel, out var message)
                ? message.ToSnapshot(channel.GuildId)
                : null;
        }
    }

    /// <summary>
    /// Lists messages of a channel, newest first (ordered by ID, as Discord does). With <paramref name="after"/>
    /// the <paramref name="limit"/> messages immediately following it are returned (still newest first).
    /// <paramref name="before"/> and <paramref name="after"/> are exclusive; both set is rejected.
    /// </summary>
    internal StateResult<ImmutableArray<MessageSnapshot>> ListMessages(
        ulong channelId,
        int limit,
        ulong? before = null,
        ulong? after = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(limit);
        if (before is not null && after is not null)
        {
            throw new ArgumentException("Specify at most one of before and after.");
        }

        lock (_gate)
        {
            if (!_channels.TryGetValue(channelId, out var channel))
            {
                return new StateFailure(StateFailureKind.UnknownChannel, channelId);
            }

            IEnumerable<ulong> ids;
            if (after is { } afterId)
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

            return ids.Select(id => _messages[id].ToSnapshot(channel.GuildId)).ToImmutableArray();
        }
    }

    /// <summary>
    /// Replaces the content of a message. Like Discord, any edit counts: <c>edited_timestamp</c> is set to the
    /// clock and a change is committed even when the content is identical.
    /// </summary>
    internal StateResult<MessageSnapshot> EditMessageContent(ulong channelId, ulong messageId, string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        lock (_gate)
        {
            if (!TryGetMessage(channelId, messageId, out var channel, out var message, out var failure))
            {
                return failure;
            }

            message.Content = content;
            message.EditedTimestamp = ReadClock();
            var snapshot = message.ToSnapshot(channel.GuildId);
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
    internal StateResult<MessageSnapshot> DeleteMessage(ulong channelId, ulong messageId)
    {
        lock (_gate)
        {
            if (!TryGetMessage(channelId, messageId, out var channel, out var message, out var failure))
            {
                return failure;
            }

            var snapshot = message.ToSnapshot(channel.GuildId);
            _messages.Remove(messageId);
            channel.MessageIds.Remove(messageId);
            Publish(new MessageDeleted(NextChangeNumber(), channel.GuildId, channelId, messageId));
            return snapshot;
        }
    }

    private bool TryGetMessage(ulong channelId, ulong messageId, out ChannelEntity channel, out MessageEntity message) =>
        TryGetMessage(channelId, messageId, out channel, out message, out _);

    private bool TryGetMessage(
        ulong channelId,
        ulong messageId,
        out ChannelEntity channel,
        out MessageEntity message,
        out StateFailure failure)
    {
        message = null!;
        if (!_channels.TryGetValue(channelId, out channel!))
        {
            failure = new StateFailure(StateFailureKind.UnknownChannel, channelId);
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
