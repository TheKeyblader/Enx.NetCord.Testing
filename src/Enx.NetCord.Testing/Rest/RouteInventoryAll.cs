namespace Enx.NetCord.Testing.Rest;

internal static partial class RouteInventory
{
    private static RouteOperation[]? _all;

    /// <summary>Gets the specification operations followed by the NetCord-only routes.</summary>
    internal static IReadOnlyList<RouteOperation> All => _all ??= [.. Specification, .. NetCordOnlyRoutes.All];
}
