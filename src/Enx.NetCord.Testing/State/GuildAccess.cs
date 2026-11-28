namespace Enx.NetCord.Testing.State;

/// <summary>How a guild is seen by the bot when it exists but the bot is not a member (ADR 0004, amendment 04B, § 2).</summary>
internal enum GuildAccessProfile
{
    /// <summary>
    /// The guild itself (<c>/guilds/{guild_id}</c>): the bot cannot tell it from a guild that does not exist
    /// (<see cref="StateFailureKind.UnknownGuild"/>, 404 <c>10004</c>).
    /// </summary>
    Hidden,

    /// <summary>
    /// Anything under the guild (channels, members, roles, commands…): the resource exists but access is missing
    /// (<see cref="StateFailureKind.MissingAccess"/>, 403 <c>50001</c>).
    /// </summary>
    Denied,
}

/// <summary>
/// The single place that decides whether a caller may see a guild or a channel. It resolves the guild, or the channel
/// then its guild, and applies the visibility profile of the amendment. It must be called under the state lock, with
/// the state's own dictionaries, and returns mutable entities that never leave the lock.
/// </summary>
/// <remarks>
/// Correcting a row of the visibility table means changing the profile a family passes here, not its handlers.
/// A direct message channel belongs to no guild and is never access-controlled.
/// </remarks>
internal sealed class GuildAccess(
    Dictionary<ulong, GuildEntity> guilds,
    Dictionary<ulong, ChannelEntity> channels,
    Dictionary<(ulong GuildId, ulong UserId), MemberEntity> members,
    Func<ulong?> botId)
{
    /// <summary>Resolves a guild for a caller.</summary>
    /// <param name="caller">The caller.</param>
    /// <param name="guildId">The guild ID given by the caller.</param>
    /// <param name="profile">What to answer when the guild exists but the bot is not a member.</param>
    /// <returns>The guild, <see cref="StateFailureKind.UnknownGuild"/> or <see cref="StateFailureKind.MissingAccess"/>.</returns>
    internal StateResult<GuildEntity> ResolveGuild(StateCaller caller, ulong guildId, GuildAccessProfile profile)
    {
        Validate(caller);
        if (!guilds.TryGetValue(guildId, out var guild))
        {
            return new StateFailure(StateFailureKind.UnknownGuild, guildId);
        }

        if (caller == StateCaller.Bot && !BotIsMember(guildId))
        {
            return profile == GuildAccessProfile.Hidden
                ? new StateFailure(StateFailureKind.UnknownGuild, guildId)
                : new StateFailure(StateFailureKind.MissingAccess, guildId);
        }

        return guild;
    }

    /// <summary>Resolves a channel for a caller.</summary>
    /// <param name="caller">The caller.</param>
    /// <param name="channelId">The channel ID given by the caller.</param>
    /// <returns>
    /// The channel, <see cref="StateFailureKind.UnknownChannel"/>, or <see cref="StateFailureKind.MissingAccess"/> when it
    /// belongs to a guild the bot is not a member of.
    /// </returns>
    internal StateResult<ChannelEntity> ResolveChannel(StateCaller caller, ulong channelId)
    {
        Validate(caller);
        if (!channels.TryGetValue(channelId, out var channel))
        {
            return new StateFailure(StateFailureKind.UnknownChannel, channelId);
        }

        if (caller == StateCaller.Bot && channel.GuildId is { } guildId && !BotIsMember(guildId))
        {
            return new StateFailure(StateFailureKind.MissingAccess, channelId);
        }

        return channel;
    }

    private bool BotIsMember(ulong guildId) => botId() is { } id && members.ContainsKey((guildId, id));

    private static void Validate(StateCaller caller)
    {
        if (caller is not (StateCaller.Fixture or StateCaller.Bot))
        {
            throw new ArgumentOutOfRangeException(nameof(caller), caller, "The state caller must be Fixture or Bot.");
        }
    }
}
