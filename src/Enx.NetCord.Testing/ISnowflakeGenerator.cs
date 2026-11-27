namespace Enx.NetCord.Testing;

/// <summary>Allocates Discord snowflakes for one fake, in strictly increasing order.</summary>
/// <remarks>
/// Each fake calls Next synchronously under its own lock, one call at a time.
/// Do not block or reenter the fake. An instance shared by multiple fakes must be thread-safe.
/// </remarks>
public interface ISnowflakeGenerator
{
    /// <summary>Returns a unique, nonzero snowflake encoding the supplied UTC millisecond.</summary>
    /// <param name="timestamp">The UTC instant whose millisecond the snowflake must encode.</param>
    /// <returns>A snowflake whose 42 high bits are the milliseconds since 2015-01-01T00:00:00Z.</returns>
    ulong Next(DateTimeOffset timestamp);
}
