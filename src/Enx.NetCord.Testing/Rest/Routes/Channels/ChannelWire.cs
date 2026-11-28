using System.Globalization;
using Enx.NetCord.Testing.Serialization;
using Enx.NetCord.Testing.State;

namespace Enx.NetCord.Testing.Rest.Routes.Channels;

/// <summary>
/// Projects detached channel snapshots into the wire DTOs of the channel routes: one DTO per channel family, chosen by the kind
/// (ADR 0013, § 1).
/// </summary>
internal static class ChannelWire
{
    /// <summary>Renders a guild channel as a 200 response with the DTO of its family.</summary>
    /// <param name="channel">A guild channel (not a DM).</param>
    /// <returns>The result.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The channel is not a guild channel.</exception>
    public static RestResult RenderGuildChannel(ChannelSnapshot channel) => channel.Traits.Family switch
    {
        ChannelFamily.Text => RestResult.Ok(ToText(channel), FakeDiscordJsonContext.Default.TextChannelResponse),
        ChannelFamily.Voice => RestResult.Ok(ToVoice(channel), FakeDiscordJsonContext.Default.VoiceChannelResponse),
        ChannelFamily.Category => RestResult.Ok(ToCategory(channel), FakeDiscordJsonContext.Default.CategoryChannelResponse),
        ChannelFamily.Forum => RestResult.Ok(ToForum(channel), FakeDiscordJsonContext.Default.ForumChannelResponse),
        _ => throw new ArgumentOutOfRangeException(nameof(channel), channel.Kind, "Not a guild channel."),
    };

    public static TextChannelResponse ToText(ChannelSnapshot channel) => new()
    {
        Id = Id(channel.Id),
        Type = (int)channel.Kind,
        LastMessageId = OptionalId(channel.LastMessageId),
        Flags = channel.Flags,
        GuildId = Id(channel.GuildId!.Value),
        Name = channel.Name ?? string.Empty,
        ParentId = OptionalId(channel.ParentId),
        RateLimitPerUser = channel.RateLimitPerUser,
        Topic = channel.Topic,
        Position = channel.Position,
        PermissionOverwrites = [],
        Nsfw = channel.Nsfw,
        DefaultAutoArchiveDuration = channel.DefaultAutoArchiveDuration,
        DefaultThreadRateLimitPerUser = channel.DefaultThreadRateLimitPerUser,
    };

    public static VoiceChannelResponse ToVoice(ChannelSnapshot channel)
    {
        var voice = channel.Voice ?? VoiceSettings.Default;
        return new()
        {
            Id = Id(channel.Id),
            Type = (int)channel.Kind,
            LastMessageId = OptionalId(channel.LastMessageId),
            Flags = channel.Flags,
            GuildId = Id(channel.GuildId!.Value),
            Name = channel.Name ?? string.Empty,
            ParentId = OptionalId(channel.ParentId),
            RateLimitPerUser = channel.RateLimitPerUser,
            Bitrate = voice.Bitrate,
            UserLimit = voice.UserLimit,
            RtcRegion = voice.RtcRegion,
            Position = channel.Position,
            PermissionOverwrites = [],
            Nsfw = channel.Nsfw,
            VideoQualityMode = voice.VideoQualityMode,
        };
    }

    public static CategoryChannelResponse ToCategory(ChannelSnapshot channel) => new()
    {
        Id = Id(channel.Id),
        Type = (int)channel.Kind,
        Flags = channel.Flags,
        GuildId = Id(channel.GuildId!.Value),
        Name = channel.Name ?? string.Empty,
        ParentId = null,
        Position = channel.Position,
        PermissionOverwrites = [],
        Nsfw = false,
    };

    public static ForumChannelResponse ToForum(ChannelSnapshot channel)
    {
        var forum = channel.Forum ?? ForumSettings.Default;
        return new()
        {
            Id = Id(channel.Id),
            Type = (int)channel.Kind,
            LastMessageId = OptionalId(channel.LastMessageId),
            Flags = channel.Flags,
            GuildId = Id(channel.GuildId!.Value),
            Name = channel.Name ?? string.Empty,
            ParentId = OptionalId(channel.ParentId),
            RateLimitPerUser = channel.RateLimitPerUser,
            Topic = channel.Topic,
            Position = channel.Position,
            PermissionOverwrites = [],
            Nsfw = channel.Nsfw,
            DefaultAutoArchiveDuration = channel.DefaultAutoArchiveDuration,
            DefaultThreadRateLimitPerUser = channel.DefaultThreadRateLimitPerUser,
            AvailableTags = [],
            DefaultReactionEmoji = null,
            DefaultSortOrder = forum.DefaultSortOrder,
            DefaultForumLayout = channel.Kind == ChannelKind.GuildForum ? forum.DefaultForumLayout : null,
        };
    }

    private static string Id(ulong id) => id.ToString(CultureInfo.InvariantCulture);

    private static string? OptionalId(ulong? id) => id is { } value ? Id(value) : null;
}
