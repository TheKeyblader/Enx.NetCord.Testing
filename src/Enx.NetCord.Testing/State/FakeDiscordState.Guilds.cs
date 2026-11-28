using System.Collections.Immutable;

namespace Enx.NetCord.Testing.State;

// The guild routes of M1-REST-06: read the guild, update it, and reorder its channels. Creating a channel is CreateGuildChannel
// (FakeDiscordState.Channels.cs), listing them is ListGuildChannels (FakeDiscordState.cs).
internal sealed partial class FakeDiscordState
{
    /// <summary>
    /// Reads a guild with its roles and member count for a caller (<c>GET /guilds/{guild_id}</c> and <c>/preview</c>). Profile
    /// <see cref="GuildAccessProfile.Hidden"/>: a guild the bot is not a member of is indistinguishable from an unknown one.
    /// </summary>
    /// <param name="caller">The caller.</param>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The guild details or <see cref="StateFailureKind.UnknownGuild"/>.</returns>
    internal StateResult<GuildDetails> GetGuildDetails(StateCaller caller, ulong guildId)
    {
        lock (_gate)
        {
            var access = _access.ResolveGuild(caller, guildId, GuildAccessProfile.Hidden);
            return access.TryGetValue(out var guild) ? DetailsOf(guild) : access.Failure;
        }
    }

    private GuildDetails DetailsOf(GuildEntity guild)
        => new(
            guild.ToSnapshot(),
            [.. guild.RoleIds.Select(id => _roles[id].ToSnapshot())],
            guild.MemberUserIds.Count);

    /// <summary>
    /// Applies a partial update to a guild (<c>PATCH /guilds/{guild_id}</c>). Every refusal comes before the first write: a channel
    /// setting must name a channel of this guild of the right type (AFK: voice; system, rules, public updates and safety alerts: text
    /// or announcement), checked in the order of the spec's schema. A <see cref="GuildUpdated"/> is published only when a value
    /// actually changes. Profile <see cref="GuildAccessProfile.Hidden"/>, like the read.
    /// </summary>
    /// <param name="caller">The caller.</param>
    /// <param name="input">The update.</param>
    /// <returns>
    /// The guild after the update, <see cref="StateFailureKind.UnknownGuild"/> or <see cref="StateFailureKind.InvalidGuildChannelReference"/>.
    /// </returns>
    internal StateResult<GuildDetails> UpdateGuild(StateCaller caller, UpdateGuildInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        lock (_gate)
        {
            var access = _access.ResolveGuild(caller, input.GuildId, GuildAccessProfile.Hidden);
            if (!access.TryGetValue(out var guild))
            {
                return access.Failure;
            }

            var current = guild.Settings;
            var refusal = CheckGuildChannelReference(guild, input.AfkChannel, "afk_channel_id", current.AfkChannelId, voice: true)
                ?? CheckGuildChannelReference(guild, input.SystemChannel, "system_channel_id", current.SystemChannelId, voice: false)
                ?? CheckGuildChannelReference(guild, input.RulesChannel, "rules_channel_id", current.RulesChannelId, voice: false)
                ?? CheckGuildChannelReference(guild, input.PublicUpdatesChannel, "public_updates_channel_id", current.PublicUpdatesChannelId, voice: false)
                ?? CheckGuildChannelReference(guild, input.SafetyAlertsChannel, "safety_alerts_channel_id", current.SafetyAlertsChannelId, voice: false);
            if (refusal is { } failure)
            {
                return failure;
            }

            var changed = false;
            if (input.Name is { } name && !string.Equals(guild.Name, name, StringComparison.Ordinal))
            {
                guild.Name = name;
                changed = true;
            }

            if (input.Description.IsSet && !string.Equals(guild.Description, input.Description.Value, StringComparison.Ordinal))
            {
                guild.Description = input.Description.Value;
                changed = true;
            }

            if (input.Icon.IsSet && !string.Equals(guild.IconHash, input.Icon.Value, StringComparison.Ordinal))
            {
                guild.IconHash = input.Icon.Value;
                changed = true;
            }

            var updated = current with
            {
                VerificationLevel = input.VerificationLevel ?? current.VerificationLevel,
                DefaultMessageNotifications = input.DefaultMessageNotifications ?? current.DefaultMessageNotifications,
                ExplicitContentFilter = input.ExplicitContentFilter ?? current.ExplicitContentFilter,
                PreferredLocale = input.PreferredLocale ?? current.PreferredLocale,
                AfkTimeout = input.AfkTimeout ?? current.AfkTimeout,
                AfkChannelId = input.AfkChannel.IsSet ? input.AfkChannel.Value : current.AfkChannelId,
                SystemChannelId = input.SystemChannel.IsSet ? input.SystemChannel.Value : current.SystemChannelId,
                SystemChannelFlags = input.SystemChannelFlags ?? current.SystemChannelFlags,
                RulesChannelId = input.RulesChannel.IsSet ? input.RulesChannel.Value : current.RulesChannelId,
                PublicUpdatesChannelId = input.PublicUpdatesChannel.IsSet ? input.PublicUpdatesChannel.Value : current.PublicUpdatesChannelId,
                SafetyAlertsChannelId = input.SafetyAlertsChannel.IsSet ? input.SafetyAlertsChannel.Value : current.SafetyAlertsChannelId,
                PremiumProgressBarEnabled = input.PremiumProgressBarEnabled ?? current.PremiumProgressBarEnabled,
                SplashHash = input.Splash.IsSet ? input.Splash.Value : current.SplashHash,
                BannerHash = input.Banner.IsSet ? input.Banner.Value : current.BannerHash,
                DiscoverySplashHash = input.DiscoverySplash.IsSet ? input.DiscoverySplash.Value : current.DiscoverySplashHash,
                HomeHeaderHash = input.HomeHeader.IsSet ? input.HomeHeader.Value : current.HomeHeaderHash,
            };
            if (updated != current)
            {
                guild.Settings = updated;
                changed = true;
            }

            if (changed)
            {
                Publish(new GuildUpdated(NextChangeNumber(), guild.ToSnapshot()));
            }

            return DetailsOf(guild);
        }
    }

    /// <summary>Checks that a channel setting names a channel of the guild of the right type. A cleared, unchanged or unset setting is not checked. Call only under the lock.</summary>
    private StateFailure? CheckGuildChannelReference(GuildEntity guild, Change<ulong?> change, string field, ulong? current, bool voice)
    {
        if (!change.IsSet || change.Value is not { } id || id == current)
        {
            return null;
        }

        if (_channels.TryGetValue(id, out var channel)
            && channel.GuildId == guild.Id
            && (voice ? channel.Kind == ChannelKind.GuildVoice : channel.Kind is ChannelKind.GuildText or ChannelKind.GuildAnnouncement))
        {
            return null;
        }

        return new StateFailure(StateFailureKind.InvalidGuildChannelReference, id, Field: field);
    }

    /// <summary>Returns the settings without any reference to a channel, or <see langword="null"/> when none pointed to it.</summary>
    private static GuildSettings? ClearChannelReferences(GuildSettings settings, ulong channelId)
    {
        var cleared = settings with
        {
            AfkChannelId = settings.AfkChannelId == channelId ? null : settings.AfkChannelId,
            SystemChannelId = settings.SystemChannelId == channelId ? null : settings.SystemChannelId,
            RulesChannelId = settings.RulesChannelId == channelId ? null : settings.RulesChannelId,
            PublicUpdatesChannelId = settings.PublicUpdatesChannelId == channelId ? null : settings.PublicUpdatesChannelId,
            SafetyAlertsChannelId = settings.SafetyAlertsChannelId == channelId ? null : settings.SafetyAlertsChannelId,
        };
        return cleared == settings ? null : cleared;
    }

    /// <summary>
    /// Applies the batch of <c>PATCH /guilds/{guild_id}/channels</c> (ADR 0013, § 4): the whole batch is checked before the first
    /// write, then applied in the order of the array, in one commit. Checks, in order: every channel exists in this guild
    /// (<see cref="StateFailureKind.UnknownChannel"/>, nothing is written), at most one channel changes its parent
    /// (<see cref="StateFailureKind.TooManyParentChanges"/>; an entry that names the current parent changes nothing and does not count),
    /// then the parent invariants of § 2 for the channel that moves. One <see cref="ChannelUpdated"/> is published per channel whose
    /// position or parent really differs after the batch, by order of first appearance in the array; a channel named twice gets one.
    /// </summary>
    /// <param name="caller">The caller.</param>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="placements">The entries, in order.</param>
    /// <returns>
    /// The number of channels whose place changed, <see cref="StateFailureKind.UnknownGuild"/>/<see cref="StateFailureKind.MissingAccess"/>,
    /// <see cref="StateFailureKind.UnknownChannel"/>, <see cref="StateFailureKind.TooManyParentChanges"/>, or a parent refusal
    /// (<see cref="StateFailureKind.InvalidChannelParent"/>, <see cref="StateFailureKind.ChannelParentNotCategory"/>,
    /// <see cref="StateFailureKind.ChannelCannotHaveParent"/>, <see cref="StateFailureKind.ChannelParentFull"/>).
    /// </returns>
    internal StateResult<int> UpdateChannelPositions(StateCaller caller, ulong guildId, IReadOnlyList<ChannelPlacement> placements)
    {
        ArgumentNullException.ThrowIfNull(placements);
        var entries = placements.ToArray(); // copy: the caller's list may change under us

        lock (_gate)
        {
            var access = _access.ResolveGuild(caller, guildId, GuildAccessProfile.Denied);
            if (!access.TryGetValue(out var guild))
            {
                return access.Failure;
            }

            foreach (var entry in entries)
            {
                if (!_channels.TryGetValue(entry.ChannelId, out var channel) || channel.GuildId != guild.Id)
                {
                    return new StateFailure(StateFailureKind.UnknownChannel, entry.ChannelId);
                }
            }

            var moved = entries
                .Where(entry => entry.Parent.IsSet && entry.Parent.Value != _channels[entry.ChannelId].ParentId)
                .Select(entry => entry.ChannelId)
                .Distinct()
                .ToList();
            if (moved.Count > 1)
            {
                return new StateFailure(StateFailureKind.TooManyParentChanges, guild.Id);
            }

            foreach (var entry in entries)
            {
                var channel = _channels[entry.ChannelId];
                if (entry.Parent.IsSet
                    && entry.Parent.Value != channel.ParentId
                    && entry.Parent.Value is { } parentId
                    && CheckParent(guild, channel.Kind, parentId) is { } parentFailure)
                {
                    return parentFailure;
                }
            }

            var before = new Dictionary<ulong, (int Position, ulong? ParentId)>();
            var order = new List<ulong>();
            foreach (var entry in entries)
            {
                var channel = _channels[entry.ChannelId];
                if (before.TryAdd(channel.Id, (channel.Position, channel.ParentId)))
                {
                    order.Add(channel.Id);
                }

                if (entry.Position is { } position)
                {
                    channel.Position = position;
                }

                if (entry.Parent.IsSet)
                {
                    channel.ParentId = entry.Parent.Value;
                }
            }

            var changed = 0;
            foreach (var channelId in order)
            {
                var channel = _channels[channelId];
                if (before[channelId] != (channel.Position, channel.ParentId))
                {
                    Publish(new ChannelUpdated(NextChangeNumber(), channel.ToSnapshot()));
                    changed++;
                }
            }

            return changed;
        }
    }
}
