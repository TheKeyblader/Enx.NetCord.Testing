using Enx.NetCord.Testing.Rest.Routes.Channels;
using Enx.NetCord.Testing.Rest.Routes.Guilds;
using Enx.NetCord.Testing.Rest.Routes.Messages;
using Enx.NetCord.Testing.Rest.Routes.Users;

namespace Enx.NetCord.Testing.Rest;

/// <summary>
/// The handler bindings of the fake, registered explicitly by key (no discovery by reflection).
/// Each M1-REST family adds its bindings here.
/// </summary>
internal static class RestRoutes
{
    /// <summary>Gets the bindings used by <see cref="RouteTable.Default"/>.</summary>
    internal static IReadOnlyList<RouteBinding> Bindings { get; } =
    [
        .. UserRoutes.Bindings,
        .. ChannelRoutes.Bindings,
        .. GuildRoutes.Bindings,
        .. MessageRoutes.Bindings,
    ];
}
