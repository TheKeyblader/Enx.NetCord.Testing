using System.Collections.Immutable;

namespace Enx.NetCord.Testing.State;

/// <summary>
/// The settings of a guild that <c>PATCH /guilds/{guild_id}</c> can change and the guild object carries (M1-REST-06). The
/// name, the owner, the icon and the description stay on the entity (they predate the route family); everything else a
/// <c>GuildPatchRequestPartial</c> accepts and the state keeps lives here, as one immutable value so a snapshot never
/// shares mutable state. <c>features</c> are not modeled (M4-REST-10): a guild always has none.
/// </summary>
/// <param name="VerificationLevel">The verification level (0-4).</param>
/// <param name="DefaultMessageNotifications">The default message notification level (0 all messages, 1 only mentions).</param>
/// <param name="ExplicitContentFilter">The explicit content filter (0-2).</param>
/// <param name="PreferredLocale">The preferred locale of a Community guild, such as <c>en-US</c>.</param>
/// <param name="AfkTimeout">The AFK timeout in seconds (60, 300, 900, 1800 or 3600).</param>
/// <param name="AfkChannelId">The AFK voice channel, or <see langword="null"/>.</param>
/// <param name="SystemChannelId">The system messages channel, or <see langword="null"/>.</param>
/// <param name="SystemChannelFlags">The system channel flags bitfield.</param>
/// <param name="RulesChannelId">The rules channel, or <see langword="null"/>.</param>
/// <param name="PublicUpdatesChannelId">The public updates channel, or <see langword="null"/>.</param>
/// <param name="SafetyAlertsChannelId">The safety alerts channel, or <see langword="null"/>.</param>
/// <param name="PremiumProgressBarEnabled">Whether the boost progress bar is shown.</param>
/// <param name="SplashHash">The invite splash hash, or <see langword="null"/>.</param>
/// <param name="BannerHash">The banner hash, or <see langword="null"/>.</param>
/// <param name="DiscoverySplashHash">The discovery splash hash, or <see langword="null"/>.</param>
/// <param name="HomeHeaderHash">The home header hash, or <see langword="null"/>.</param>
internal sealed record GuildSettings(
    int VerificationLevel = 0,
    int DefaultMessageNotifications = 0,
    int ExplicitContentFilter = 0,
    string PreferredLocale = GuildSettings.DefaultLocale,
    int AfkTimeout = GuildSettings.DefaultAfkTimeout,
    ulong? AfkChannelId = null,
    ulong? SystemChannelId = null,
    int SystemChannelFlags = 0,
    ulong? RulesChannelId = null,
    ulong? PublicUpdatesChannelId = null,
    ulong? SafetyAlertsChannelId = null,
    bool PremiumProgressBarEnabled = false,
    string? SplashHash = null,
    string? BannerHash = null,
    string? DiscoverySplashHash = null,
    string? HomeHeaderHash = null)
{
    /// <summary>The locale of a new guild.</summary>
    internal const string DefaultLocale = "en-US";

    /// <summary>The AFK timeout of a new guild, five minutes (Discord's default).</summary>
    internal const int DefaultAfkTimeout = 300;

    /// <summary>Gets the settings of a new guild.</summary>
    internal static GuildSettings Default { get; } = new();
}

/// <summary>A guild as the guild routes read it: the guild, its roles and its member count, taken under one lock.</summary>
/// <param name="Guild">The guild.</param>
/// <param name="Roles">The roles, in creation order (<c>@everyone</c> first).</param>
/// <param name="MemberCount">The number of members of the guild (the bot included).</param>
internal sealed record GuildDetails(GuildSnapshot Guild, ImmutableArray<RoleSnapshot> Roles, int MemberCount);

/// <summary>
/// A value of a three-state field: not sent (<see cref="IsSet"/> false) or sent, possibly as <see langword="null"/>
/// (which clears the field). Used by partial updates where the wire distinguishes an absent key from a <c>null</c> one.
/// </summary>
/// <typeparam name="T">The value type.</typeparam>
/// <param name="IsSet">Whether the field was sent.</param>
/// <param name="Value">The new value when <paramref name="IsSet"/>; <see langword="null"/> clears the field.</param>
internal readonly record struct Change<T>(bool IsSet, T? Value)
{
    /// <summary>Gets the change of a field that was not sent.</summary>
    internal static Change<T> Unchanged => default;

    /// <summary>Creates the change of a field that was sent.</summary>
    /// <param name="value">The new value, or <see langword="null"/> to clear it.</param>
    /// <returns>The change.</returns>
    internal static Change<T> To(T? value) => new(true, value);
}
