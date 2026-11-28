using System.Globalization;
using System.Net;
using Enx.NetCord.Testing.Rest.Routes.Channels;
using Enx.NetCord.Testing.Serialization;
using Enx.NetCord.Testing.State;

namespace Enx.NetCord.Testing.Rest.Routes.Guilds;

/// <summary>Projects detached guild values into the wire DTOs of the guild routes.</summary>
internal static class GuildWire
{
    // The deprecated `region` field: Discord stopped using voice regions per guild and keeps the property as this constant.
    private const string DeprecatedRegion = "deprecated";

    // Limits of a guild without boosts (the fake models no premium tier).
    private const int MaxMembers = 500000;
    private const int MaxStageVideoChannelUsers = 50;
    private const int MaxVideoChannelUsers = 25;

    /// <summary>Projects a guild to <c>GuildResponse</c>, or to <c>GuildWithCountsResponse</c> when <paramref name="withCounts"/> is set.</summary>
    /// <param name="details">The guild with its roles and member count.</param>
    /// <param name="fallbackOwnerId">The owner reported when the guild has none in the state: the bot that reads it (a guild always has an owner).</param>
    /// <param name="withCounts">Whether the approximate counts are present.</param>
    /// <returns>The DTO.</returns>
    public static GuildResponse ToGuild(GuildDetails details, ulong fallbackOwnerId, bool withCounts)
    {
        var guild = details.Guild;
        var settings = guild.Settings;
        return new GuildResponse
        {
            Id = Id(guild.Id),
            Name = guild.Name,
            Icon = guild.IconHash,
            Description = guild.Description,
            HomeHeader = settings.HomeHeaderHash,
            Splash = settings.SplashHash,
            DiscoverySplash = settings.DiscoverySplashHash,
            Features = [],
            Banner = settings.BannerHash,
            OwnerId = Id(guild.OwnerId ?? fallbackOwnerId),
            ApplicationId = null,
            Region = DeprecatedRegion,
            AfkChannelId = OptionalId(settings.AfkChannelId),
            AfkTimeout = settings.AfkTimeout,
            SystemChannelId = OptionalId(settings.SystemChannelId),
            SystemChannelFlags = settings.SystemChannelFlags,
            WidgetEnabled = false,
            WidgetChannelId = null,
            VerificationLevel = settings.VerificationLevel,
            Roles = [.. details.Roles.Select(ToRole)],
            DefaultMessageNotifications = settings.DefaultMessageNotifications,
            MfaLevel = 0,
            ExplicitContentFilter = settings.ExplicitContentFilter,
            MaxPresences = null,
            MaxMembers = MaxMembers,
            MaxStageVideoChannelUsers = MaxStageVideoChannelUsers,
            MaxVideoChannelUsers = MaxVideoChannelUsers,
            VanityUrlCode = null,
            PremiumTier = 0,
            PremiumSubscriptionCount = 0,
            PreferredLocale = settings.PreferredLocale,
            RulesChannelId = OptionalId(settings.RulesChannelId),
            SafetyAlertsChannelId = OptionalId(settings.SafetyAlertsChannelId),
            PublicUpdatesChannelId = OptionalId(settings.PublicUpdatesChannelId),
            PremiumProgressBarEnabled = settings.PremiumProgressBarEnabled,
            Nsfw = false,
            NsfwLevel = 0,
            Emojis = [],
            Stickers = [],
            IncidentsData = null,
            ApproximateMemberCount = withCounts ? details.MemberCount : null,

            // The fake models no presence: nobody is online.
            ApproximatePresenceCount = withCounts ? 0 : null,
        };
    }

    /// <summary>Projects a guild to <c>GuildPreviewResponse</c>.</summary>
    /// <param name="details">The guild with its member count.</param>
    /// <returns>The DTO.</returns>
    public static GuildPreviewResponse ToPreview(GuildDetails details)
    {
        var guild = details.Guild;
        var settings = guild.Settings;
        return new GuildPreviewResponse
        {
            Id = Id(guild.Id),
            Name = guild.Name,
            Icon = guild.IconHash,
            Description = guild.Description,
            HomeHeader = settings.HomeHeaderHash,
            Splash = settings.SplashHash,
            DiscoverySplash = settings.DiscoverySplashHash,
            Features = [],
            ApproximateMemberCount = details.MemberCount,
            ApproximatePresenceCount = 0,
            Emojis = [],
            Stickers = [],
        };
    }

    /// <summary>Projects a guild channel to the variant of its family, for a list that mixes the families.</summary>
    /// <param name="channel">A guild channel (not a DM).</param>
    /// <returns>The union holding the one DTO.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The channel is not a guild channel.</exception>
    public static GuildChannelUnion ToChannelUnion(ChannelSnapshot channel) => channel.Traits.Family switch
    {
        ChannelFamily.Text => new GuildChannelUnion { Text = ChannelWire.ToText(channel) },
        ChannelFamily.Voice => new GuildChannelUnion { Voice = ChannelWire.ToVoice(channel) },
        ChannelFamily.Category => new GuildChannelUnion { Category = ChannelWire.ToCategory(channel) },
        ChannelFamily.Forum => new GuildChannelUnion { Forum = ChannelWire.ToForum(channel) },
        _ => throw new ArgumentOutOfRangeException(nameof(channel), channel.Kind, "Not a guild channel."),
    };

    /// <summary>Renders a created guild channel as a <c>201</c> response with the DTO of its family.</summary>
    /// <param name="channel">A guild channel (not a DM).</param>
    /// <returns>The result.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The channel is not a guild channel.</exception>
    public static RestResult RenderCreatedChannel(ChannelSnapshot channel) => channel.Traits.Family switch
    {
        ChannelFamily.Text => RestResult.Json(HttpStatusCode.Created, ChannelWire.ToText(channel), FakeDiscordJsonContext.Default.TextChannelResponse),
        ChannelFamily.Voice => RestResult.Json(HttpStatusCode.Created, ChannelWire.ToVoice(channel), FakeDiscordJsonContext.Default.VoiceChannelResponse),
        ChannelFamily.Category => RestResult.Json(HttpStatusCode.Created, ChannelWire.ToCategory(channel), FakeDiscordJsonContext.Default.CategoryChannelResponse),
        ChannelFamily.Forum => RestResult.Json(HttpStatusCode.Created, ChannelWire.ToForum(channel), FakeDiscordJsonContext.Default.ForumChannelResponse),
        _ => throw new ArgumentOutOfRangeException(nameof(channel), channel.Kind, "Not a guild channel."),
    };

    private static GuildRoleResponse ToRole(RoleSnapshot role) => new()
    {
        Id = Id(role.Id),
        Name = role.Name,
        Permissions = role.Permissions.ToString(CultureInfo.InvariantCulture),
        Position = role.Position,
        Color = role.Color,
        Colors = new GuildRoleColorsResponse { PrimaryColor = role.Color },
        Hoist = role.Hoist,
        Managed = role.Managed,
        Mentionable = role.Mentionable,
        Flags = 0,
    };

    private static string Id(ulong id) => id.ToString(CultureInfo.InvariantCulture);

    private static string? OptionalId(ulong? id) => id is { } value ? Id(value) : null;
}
