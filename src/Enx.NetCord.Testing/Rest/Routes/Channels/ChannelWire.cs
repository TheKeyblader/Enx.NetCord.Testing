using System.Globalization;
using Enx.NetCord.Testing.Serialization;
using Enx.NetCord.Testing.State;

namespace Enx.NetCord.Testing.Rest.Routes.Channels;

/// <summary>Projects detached channel snapshots into the wire DTOs of the channel routes.</summary>
internal static class ChannelWire
{
    public static GuildChannelResponse ToGuildChannel(ChannelSnapshot channel) => new()
    {
        Id = Id(channel.Id),
        Type = (int)channel.Kind,
        LastMessageId = channel.LastMessageId is { } last ? Id(last) : null,
        Flags = 0,
        GuildId = Id(channel.GuildId!.Value),
        Name = channel.Name ?? string.Empty,
        RateLimitPerUser = channel.RateLimitPerUser,
        Topic = channel.Topic,
        Position = channel.Position,
        PermissionOverwrites = [],
        Nsfw = channel.Nsfw,
        DefaultThreadRateLimitPerUser = 0,
    };

    private static string Id(ulong id) => id.ToString(CultureInfo.InvariantCulture);
}
