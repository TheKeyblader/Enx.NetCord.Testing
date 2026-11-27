namespace Enx.NetCord.Testing.State;

/// <summary>Constants and helpers for Discord snowflakes.</summary>
internal static class Snowflake
{
    /// <summary>Unix milliseconds of the Discord epoch, 2015-01-01T00:00:00Z.</summary>
    internal const long EpochUnixMilliseconds = 1420070400000L;

    /// <summary>Number of low bits holding worker, process and increment.</summary>
    internal const int CounterBits = 22;

    /// <summary>Largest value of the 22-bit counter.</summary>
    internal const ulong MaxCounter = (1UL << CounterBits) - 1;

    /// <summary>Largest value of the 42-bit millisecond field.</summary>
    internal const long MaxMilliseconds = (1L << 42) - 1;

    /// <summary>Gets the milliseconds since the Discord epoch for an instant, or null if outside 42 bits.</summary>
    internal static long? MillisecondsOf(DateTimeOffset timestamp)
    {
        var ms = timestamp.ToUnixTimeMilliseconds() - EpochUnixMilliseconds;
        return ms is < 0 or > MaxMilliseconds ? null : ms;
    }

    /// <summary>Gets the UTC creation instant encoded in a snowflake.</summary>
    internal static DateTimeOffset CreatedAt(ulong id) =>
        DateTimeOffset.FromUnixTimeMilliseconds((long)(id >> CounterBits) + EpochUnixMilliseconds);
}
