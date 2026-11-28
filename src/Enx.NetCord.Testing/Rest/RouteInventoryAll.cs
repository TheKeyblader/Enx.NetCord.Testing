namespace Enx.NetCord.Testing.Rest;

internal static partial class RouteInventory
{
    private static readonly Lazy<RouteOperation[]> AllOperations = new(
        static () => [.. Specification, .. NetCordOnlyRoutes.All]);

    /// <summary>Gets the specification operations followed by the NetCord-only routes.</summary>
    internal static IReadOnlyList<RouteOperation> All => AllOperations.Value;

    private static readonly Lazy<HashSet<string>> SnowflakeParameterSet = new(
        static () => new HashSet<string>(SnowflakeParameters, StringComparer.Ordinal));

    /// <summary>Returns whether the pinned specification types a path parameter as a Snowflake.</summary>
    /// <param name="name">The parameter name, such as <c>channel_id</c>.</param>
    /// <returns><see langword="true"/> for a Snowflake parameter; free strings and integers are <see langword="false"/>.</returns>
    internal static bool IsSnowflakeParameter(string name) => SnowflakeParameterSet.Value.Contains(name);
}
