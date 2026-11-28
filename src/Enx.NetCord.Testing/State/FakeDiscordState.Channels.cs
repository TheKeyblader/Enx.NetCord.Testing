namespace Enx.NetCord.Testing.State;

internal sealed partial class FakeDiscordState
{
    /// <summary>
    /// Applies a partial update to a guild channel. A change is committed (and <see cref="ChannelUpdated"/> published) only when at
    /// least one value actually differs. A DM channel has nothing to update: it is returned as it is, with no change.
    /// </summary>
    /// <param name="caller">The caller; the bot is refused a channel of a guild it is not a member of.</param>
    /// <param name="input">The update.</param>
    /// <returns>The channel after the update, <see cref="StateFailureKind.UnknownChannel"/> or <see cref="StateFailureKind.MissingAccess"/>.</returns>
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
            if (channel.GuildId is not null)
            {
                if (input.Name is { } name && !string.Equals(channel.Name, name, StringComparison.Ordinal))
                {
                    channel.Name = name;
                    changed = true;
                }

                if (input.ChangeTopic && !string.Equals(channel.Topic, input.Topic, StringComparison.Ordinal))
                {
                    channel.Topic = input.Topic;
                    changed = true;
                }

                if (input.Nsfw is { } nsfw && channel.Nsfw != nsfw)
                {
                    channel.Nsfw = nsfw;
                    changed = true;
                }

                if (input.RateLimitPerUser is { } slowmode && channel.RateLimitPerUser != slowmode)
                {
                    channel.RateLimitPerUser = slowmode;
                    changed = true;
                }

                if (input.Position is { } position && channel.Position != position)
                {
                    channel.Position = position;
                    changed = true;
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

    /// <summary>
    /// Deletes a guild channel and its messages, and returns what the channel was. Its ID is never reused in this fake. Like
    /// Discord, "deleting" a DM channel only closes it: the channel stays, nothing is committed, and it is returned as it is.
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

            foreach (var messageId in channel.MessageIds)
            {
                _messages.Remove(messageId);
            }

            _channels.Remove(channelId);
            _guilds[guildId].ChannelIds.Remove(channelId);
            Publish(new ChannelDeleted(NextChangeNumber(), snapshot));
            return snapshot;
        }
    }
}
