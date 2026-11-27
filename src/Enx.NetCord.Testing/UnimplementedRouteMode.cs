namespace Enx.NetCord.Testing;

/// <summary>Specifies how the fake reports a missing REST operation.</summary>
public enum UnimplementedRouteMode
{
    /// <summary>Returns a Discord 404 with error code zero.</summary>
    Lenient = 0,

    /// <summary>Records the attempt and throws a local <see cref="NotSupportedException"/>.</summary>
    Strict = 1,
}
