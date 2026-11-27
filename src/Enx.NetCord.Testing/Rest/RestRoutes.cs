namespace Enx.NetCord.Testing.Rest;

/// <summary>
/// The handler bindings of the fake, registered explicitly by key (no discovery by reflection).
/// The list is empty until the M1-REST families add their bindings here.
/// </summary>
internal static class RestRoutes
{
    /// <summary>Gets the bindings used by <see cref="RouteTable.Default"/>.</summary>
    internal static IReadOnlyList<RouteBinding> Bindings { get; } = [];
}
