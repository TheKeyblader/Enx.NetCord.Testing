namespace Enx.NetCord.Testing.Rest;

internal static partial class RouteInventory
{
    private static readonly Lazy<RouteOperation[]> AllOperations = new(
        static () => [.. Specification, .. NetCordOnlyRoutes.All]);

    /// <summary>Gets the specification operations followed by the NetCord-only routes.</summary>
    internal static IReadOnlyList<RouteOperation> All => AllOperations.Value;
}
