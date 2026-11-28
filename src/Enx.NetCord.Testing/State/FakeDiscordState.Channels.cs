namespace Enx.NetCord.Testing.State;

internal sealed partial class FakeDiscordState
{
    /// <summary>The maximum number of channels a guild holds (threads excluded; ADR 0013, § 1).</summary>
    internal const int MaxChannelsPerGuild = 500;

    /// <summary>The maximum number of channels a category holds (ADR 0013, § 2).</summary>
    internal const int MaxChannelsPerCategory = 50;

    /// <summary>
    /// Creates a guild channel of any modeled type (ADR 0013). Seed and <c>POST /guilds/{guild_id}/channels</c> both come here, so the
    /// invariants hold for every caller. Checks, in order, before anything is allocated or written: the guild (and, for the bot,
    /// membership), the 500-channel limit, the type, then the parent (it exists in the same guild, is a category, the channel is not
    /// itself a category, the category holds fewer than 50 channels). Settings that do not apply to the type are not stored. The
    /// position defaults to one more than the highest of the sorting group, or 0.
    /// </summary>
    /// <param name="caller">The caller.</param>
    /// <param name="input">The channel to create.</param>
    /// <returns>
    /// The channel, <see cref="StateFailureKind.UnknownGuild"/>, <see cref="StateFailureKind.MissingAccess"/>,
    /// <see cref="StateFailureKind.MaxGuildChannels"/>, <see cref="StateFailureKind.InvalidChannelType"/> (a DM, or a value that is
    /// not a channel type), <see cref="StateFailureKind.InvalidChannelParent"/>, <see cref="StateFailureKind.ChannelParentNotCategory"/>,
    /// <see cref="StateFailureKind.ChannelCannotHaveParent"/> or <see cref="StateFailureKind.ChannelParentFull"/>.
    /// </returns>
    internal StateResult<ChannelSnapshot> CreateGuildChannel(StateCaller caller, CreateGuildChannelInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Name);
        ValidateExplicitId(input.Id, nameof(input));

        lock (_gate)
        {
            var access = _access.ResolveGuild(caller, input.GuildId, GuildAccessProfile.Denied);
            if (!access.TryGetValue(out var guild))
            {
                return access.Failure;
            }

            if (guild.ChannelIds.Count >= MaxChannelsPerGuild)
            {
                return new StateFailure(StateFailureKind.MaxGuildChannels, guild.Id);
            }

            if (!Enum.IsDefined(input.Kind) || input.Kind == ChannelKind.Dm)
            {
                return new StateFailure(StateFailureKind.InvalidChannelType, guild.Id);
            }

            var traits = ChannelTraits.Of(input.Kind);
            if (input.ParentId is { } parentId)
            {
                var parentFailure = CheckParent(guild, input.Kind, parentId);
                if (parentFailure is { } failure)
                {
                    return failure;
                }
            }

            var now = ReadClock();
            var id = ResolveNewId(input.Id, now);
            _assigned.Add(id);

            var channel = new ChannelEntity(id, input.Kind, NextOrdinal())
            {
                GuildId = guild.Id,
                Name = input.Name,
                ParentId = input.ParentId,
                Position = input.Position ?? DefaultPosition(guild, traits.SortGroup),
                Topic = traits.TopicMaxLength > 0 ? input.Topic : null,
                Nsfw = traits.HasNsfw && input.Nsfw,
                RateLimitPerUser = traits.HasSlowmode ? input.RateLimitPerUser : 0,
            };
            channel.Voice = traits.Family == ChannelFamily.Voice ? input.Voice ?? VoiceSettings.Default : null;
            channel.Forum = traits.Family == ChannelFamily.Forum ? input.Forum ?? ForumSettings.Default : null;
            _channels.Add(id, channel);
            guild.ChannelIds.Add(id);

            var snapshot = channel.ToSnapshot();
            Publish(new ChannelCreated(NextChangeNumber(), snapshot));
            return snapshot;
        }
    }

    /// <summary>Checks the parent invariants of ADR 0013, § 2, in their order. Call only under the lock.</summary>
    private StateFailure? CheckParent(GuildEntity guild, ChannelKind kind, ulong parentId)
    {
        if (!_channels.TryGetValue(parentId, out var parent) || parent.GuildId != guild.Id)
        {
            return new StateFailure(StateFailureKind.InvalidChannelParent, parentId);
        }

        if (parent.Kind != ChannelKind.GuildCategory)
        {
            return new StateFailure(StateFailureKind.ChannelParentNotCategory, parentId);
        }

        if (!ChannelTraits.Of(kind).CanHaveParent)
        {
            return new StateFailure(StateFailureKind.ChannelCannotHaveParent, parentId);
        }

        var children = guild.ChannelIds.Count(childId => _channels[childId].ParentId == parentId);
        return children >= MaxChannelsPerCategory ? new StateFailure(StateFailureKind.ChannelParentFull, parentId) : null;
    }

    /// <summary>The default position: one more than the highest of the sorting group (all categories of the guild together), or 0.</summary>
    private int DefaultPosition(GuildEntity guild, ChannelSortGroup group)
    {
        var positions = guild.ChannelIds
            .Select(id => _channels[id])
            .Where(channel => ChannelTraits.Of(channel.Kind).SortGroup == group)
            .Select(channel => channel.Position)
            .ToList();
        return positions.Count == 0 ? 0 : positions.Max() + 1;
    }

    /// <summary>
    /// Applies a partial update to a guild channel (ADR 0013). A change is committed (and one <see cref="ChannelUpdated"/> published)
    /// only when at least one value actually differs. A DM channel has nothing to update: it is returned as it is, with no change.
    /// For a guild channel every refusal comes before the first write, in this order: the type (only text &lt;-&gt; announcement converts),
    /// the parent (the four invariants of § 2; the current parent, or no parent for a channel without one, changes nothing and is
    /// not checked), the name (not empty), the topic, the bitrate, the user limit. The conversion applies first, so the other fields
    /// are checked and applied against the new type; a field the type has no use for is ignored.
    /// </summary>
    /// <param name="caller">The caller; the bot is refused a channel of a guild it is not a member of.</param>
    /// <param name="input">The update.</param>
    /// <returns>
    /// The channel after the update, <see cref="StateFailureKind.UnknownChannel"/>, <see cref="StateFailureKind.MissingAccess"/>,
    /// <see cref="StateFailureKind.InvalidChannelType"/>, <see cref="StateFailureKind.InvalidChannelParent"/>,
    /// <see cref="StateFailureKind.ChannelParentNotCategory"/>, <see cref="StateFailureKind.ChannelCannotHaveParent"/>,
    /// <see cref="StateFailureKind.ChannelParentFull"/>, <see cref="StateFailureKind.InvalidChannelName"/>,
    /// <see cref="StateFailureKind.InvalidChannelTopic"/>, <see cref="StateFailureKind.ChannelBitrateTooHigh"/> or
    /// <see cref="StateFailureKind.InvalidChannelUserLimit"/>.
    /// </returns>
    internal StateResult<ChannelSnapshot> UpdateChannel(StateCaller caller, UpdateChannelInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        lock (_gate)
        {
            var access = _access.ResolveChannel(caller, input.ChannelId);
            if (!access.TryGetValue(out var channel))
            {
                return access.Failure;
            }

            var changed = false;
            if (channel.GuildId is { } guildId)
            {
                var guild = _guilds[guildId];

                // Every refusal comes before the first write: a refused update changes nothing. The kind the channel will have after
                // the conversion decides what the other fields mean.
                var kind = channel.Kind;
                if (input.Type is { } type && type != (int)kind)
                {
                    if (!TryConvertKind(kind, type, out kind))
                    {
                        return new StateFailure(StateFailureKind.InvalidChannelType, channel.Id);
                    }
                }

                var traits = ChannelTraits.Of(kind);
                var moveParent = input.ChangeParent && input.ParentId != channel.ParentId;
                if (moveParent && input.ParentId is { } parentId && CheckParent(guild, kind, parentId) is { } parentFailure)
                {
                    return parentFailure;
                }

                if (input.Name is { Length: 0 })
                {
                    return new StateFailure(StateFailureKind.InvalidChannelName, channel.Id);
                }

                if (traits.TopicMaxLength > 0
                    && input.ChangeTopic
                    && input.Topic is { } topic
                    && topic.EnumerateRunes().Count() > traits.TopicMaxLength)
                {
                    return new StateFailure(StateFailureKind.InvalidChannelTopic, channel.Id);
                }

                if (traits.Family == ChannelFamily.Voice)
                {
                    if (input.Bitrate > traits.BitrateMax)
                    {
                        return new StateFailure(StateFailureKind.ChannelBitrateTooHigh, channel.Id);
                    }

                    if (input.UserLimit > traits.UserLimitMax)
                    {
                        return new StateFailure(StateFailureKind.InvalidChannelUserLimit, channel.Id, traits.UserLimitMax);
                    }
                }

                // Writes. A field the type has no use for is validated upstream and ignored here (ADR 0013, § 1).
                if (kind != channel.Kind)
                {
                    channel.Kind = kind;
                    if (!traits.HasSlowmode)
                    {
                        channel.RateLimitPerUser = 0; // announcement channels have no slowmode (hypothesis, ADR 0013 lot C)
                    }

                    changed = true;
                }

                if (moveParent)
                {
                    channel.ParentId = input.ParentId;
                    changed = true;
                }

                if (input.Name is { } name && !string.Equals(channel.Name, name, StringComparison.Ordinal))
                {
                    channel.Name = name;
                    changed = true;
                }

                if (traits.TopicMaxLength > 0 && input.ChangeTopic && !string.Equals(channel.Topic, input.Topic, StringComparison.Ordinal))
                {
                    channel.Topic = input.Topic;
                    changed = true;
                }

                if (traits.HasNsfw && input.Nsfw is { } nsfw && channel.Nsfw != nsfw)
                {
                    channel.Nsfw = nsfw;
                    changed = true;
                }

                if (traits.HasSlowmode && input.RateLimitPerUser is { } slowmode && channel.RateLimitPerUser != slowmode)
                {
                    channel.RateLimitPerUser = slowmode;
                    changed = true;
                }

                if (input.Position is { } position && channel.Position != position)
                {
                    channel.Position = position;
                    changed = true;
                }

                if (traits.HasThreadDefaults)
                {
                    if (input.DefaultAutoArchiveDuration is { } archive && channel.DefaultAutoArchiveDuration != archive)
                    {
                        channel.DefaultAutoArchiveDuration = archive;
                        changed = true;
                    }

                    if (input.DefaultThreadRateLimitPerUser is { } threadSlowmode && channel.DefaultThreadRateLimitPerUser != threadSlowmode)
                    {
                        channel.DefaultThreadRateLimitPerUser = threadSlowmode;
                        changed = true;
                    }
                }

                if (traits.Family == ChannelFamily.Voice && channel.Voice is { } voice)
                {
                    var updated = voice with
                    {
                        Bitrate = input.Bitrate ?? voice.Bitrate,
                        UserLimit = input.UserLimit ?? voice.UserLimit,
                        RtcRegion = input.ChangeRtcRegion ? input.RtcRegion : voice.RtcRegion,
                        VideoQualityMode = input.VideoQualityMode ?? voice.VideoQualityMode,
                    };
                    if (updated != voice)
                    {
                        channel.Voice = updated;
                        changed = true;
                    }
                }

                if (traits.Family == ChannelFamily.Forum && channel.Forum is { } forum)
                {
                    var updated = forum with
                    {
                        DefaultSortOrder = input.ChangeDefaultSortOrder ? input.DefaultSortOrder : forum.DefaultSortOrder,
                        DefaultForumLayout = kind == ChannelKind.GuildForum ? input.DefaultForumLayout ?? forum.DefaultForumLayout : forum.DefaultForumLayout,
                    };
                    if (updated != forum)
                    {
                        channel.Forum = updated;
                        changed = true;
                    }
                }
            }

            var snapshot = channel.ToSnapshot();
            if (changed)
            {
                Publish(new ChannelUpdated(NextChangeNumber(), snapshot));
            }

            return snapshot;
        }
    }

    /// <summary>The only conversion Discord supports: text &lt;-&gt; announcement (ADR 0013, § 5). Any other target, type 14 included, is refused.</summary>
    private static bool TryConvertKind(ChannelKind from, int requested, out ChannelKind to)
    {
        to = from;
        switch (from, requested)
        {
            case (ChannelKind.GuildText, (int)ChannelKind.GuildAnnouncement):
                to = ChannelKind.GuildAnnouncement;
                return true;
            case (ChannelKind.GuildAnnouncement, (int)ChannelKind.GuildText):
                to = ChannelKind.GuildText;
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Deletes a guild channel and its messages, and returns what the channel was. Its ID is never reused in this fake. Like
    /// Discord, "deleting" a DM channel only closes it: the channel stays, nothing is committed, and it is returned as it is.
    /// Deleting a category does not delete its children (ADR 0013, § 3): in the same commit each of them, by ascending ID, loses
    /// its parent and publishes a <see cref="ChannelUpdated"/> carrying it after the change, then the category publishes its
    /// <see cref="ChannelDeleted"/>.
    /// </summary>
    /// <param name="caller">The caller; the bot is refused a channel of a guild it is not a member of.</param>
    /// <param name="channelId">The channel ID.</param>
    /// <returns>The channel, <see cref="StateFailureKind.UnknownChannel"/> or <see cref="StateFailureKind.MissingAccess"/>.</returns>
    internal StateResult<ChannelSnapshot> DeleteChannel(StateCaller caller, ulong channelId)
    {
        lock (_gate)
        {
            var access = _access.ResolveChannel(caller, channelId);
            if (!access.TryGetValue(out var channel))
            {
                return access.Failure;
            }

            var snapshot = channel.ToSnapshot();
            if (channel.GuildId is not { } guildId)
            {
                return snapshot;
            }

            var guild = _guilds[guildId];
            if (channel.Kind == ChannelKind.GuildCategory)
            {
                foreach (var childId in guild.ChannelIds.Order())
                {
                    var child = _channels[childId];
                    if (child.ParentId == channelId)
                    {
                        child.ParentId = null;
                        Publish(new ChannelUpdated(NextChangeNumber(), child.ToSnapshot()));
                    }
                }
            }

            foreach (var messageId in channel.MessageIds)
            {
                _messages.Remove(messageId);
            }

            _channels.Remove(channelId);
            guild.ChannelIds.Remove(channelId);
            Publish(new ChannelDeleted(NextChangeNumber(), snapshot));
            return snapshot;
        }
    }
}
