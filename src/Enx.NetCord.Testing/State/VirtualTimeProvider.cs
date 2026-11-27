namespace Enx.NetCord.Testing.State;

/// <summary>
/// Default clock of a fake: frozen at <c>2020-01-01T00:00:00Z</c>, never advances and never
/// consults the wall clock. Its timers never fire.
/// </summary>
internal sealed class VirtualTimeProvider : TimeProvider
{
    internal static readonly DateTimeOffset DefaultNow = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly DateTimeOffset _now;

    internal VirtualTimeProvider(DateTimeOffset? now = null)
    {
        _now = now ?? DefaultNow;
    }

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override DateTimeOffset GetUtcNow() => _now;

    public override long GetTimestamp() => 0;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        ArgumentNullException.ThrowIfNull(callback);
        return new InertTimer();
    }

    private sealed class InertTimer : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => true;

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
