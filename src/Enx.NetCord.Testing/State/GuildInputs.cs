namespace Enx.NetCord.Testing.State;

/// <summary>
/// A partial guild update (<c>PATCH /guilds/{guild_id}</c>). A <see langword="null"/> scalar and an unset <see cref="Change{T}"/>
/// leave the field as it is; a set <see cref="Change{T}"/> with a <see langword="null"/> value clears it. Channel references are
/// checked by the state under its lock (they must name a channel of this guild of the right type).
/// </summary>
internal sealed record UpdateGuildInput(
    ulong GuildId,
    string? Name = null,
    Change<string> Description = default,
    Change<string> Icon = default,
    Change<string> Splash = default,
    Change<string> Banner = default,
    Change<string> DiscoverySplash = default,
    Change<string> HomeHeader = default,
    int? VerificationLevel = null,
    int? DefaultMessageNotifications = null,
    int? ExplicitContentFilter = null,
    string? PreferredLocale = null,
    int? AfkTimeout = null,
    Change<ulong?> AfkChannel = default,
    Change<ulong?> SystemChannel = default,
    int? SystemChannelFlags = null,
    Change<ulong?> RulesChannel = default,
    Change<ulong?> PublicUpdatesChannel = default,
    Change<ulong?> SafetyAlertsChannel = default,
    bool? PremiumProgressBarEnabled = null);

/// <summary>One entry of <c>PATCH /guilds/{guild_id}/channels</c>: a channel and what to change about its place.</summary>
/// <param name="ChannelId">The channel.</param>
/// <param name="Position">The new position, or <see langword="null"/> for unchanged.</param>
/// <param name="Parent">The new parent: unset is unchanged, set to <see langword="null"/> leaves the category.</param>
internal sealed record ChannelPlacement(ulong ChannelId, int? Position, Change<ulong?> Parent);
