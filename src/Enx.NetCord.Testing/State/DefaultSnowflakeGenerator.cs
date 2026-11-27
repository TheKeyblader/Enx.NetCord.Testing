namespace Enx.NetCord.Testing.State;

/// <summary>
/// Deterministic per-fake generator: <c>(msSince2015 &lt;&lt; 22) | counter</c>. The 22-bit counter
/// (worker, process, increment) restarts at 0 on each new millisecond, or at 1 at the epoch so that
/// the zero ID is never produced.
/// </summary>
internal sealed class DefaultSnowflakeGenerator : ISnowflakeGenerator
{
    private long _lastMilliseconds = -1;
    private ulong _counter;

    public ulong Next(DateTimeOffset timestamp)
    {
        var requested = Snowflake.MillisecondsOf(timestamp)
            ?? throw new InvalidOperationException(
                "The requested instant is outside the 42-bit snowflake timestamp range.");

        ulong counter;
        if (requested < _lastMilliseconds)
        {
            throw new InvalidOperationException(
                "The requested instant is earlier than the last millisecond allocated by this generator.");
        }
        else if (requested == _lastMilliseconds)
        {
            if (_counter >= Snowflake.MaxCounter)
            {
                throw new InvalidOperationException(
                    "The 22-bit snowflake counter is exhausted for the requested millisecond.");
            }

            counter = _counter + 1;
        }
        else
        {
            counter = requested == 0 ? 1UL : 0UL;
        }

        _lastMilliseconds = requested;
        _counter = counter;
        return ((ulong)requested << Snowflake.CounterBits) | counter;
    }
}
