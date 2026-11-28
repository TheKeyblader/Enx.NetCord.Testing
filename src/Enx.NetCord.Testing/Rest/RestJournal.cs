namespace Enx.NetCord.Testing.Rest;

/// <summary>
/// The fake's journal of terminal REST attempts, in the order they were added. The public <c>Recorded.Requests</c>
/// projection (<see cref="DiscordRecording"/>) is built from <see cref="SnapshotFrom"/>.
/// </summary>
internal sealed class RestJournal : IRestAttemptSink
{
    private readonly Lock _gate = new();
    private readonly List<RestAttempt> _attempts = [];

    /// <inheritdoc />
    public void Record(RestAttempt attempt)
    {
        ArgumentNullException.ThrowIfNull(attempt);

        lock (_gate)
        {
            _attempts.Add(attempt);
        }
    }

    /// <summary>Returns the attempts recorded so far, in recording order.</summary>
    /// <returns>A copy of the list; the attempts themselves are detached records.</returns>
    public IReadOnlyList<RestAttempt> Snapshot()
    {
        lock (_gate)
        {
            return [.. _attempts];
        }
    }

    /// <summary>Returns the attempts recorded from a position on, in recording order.</summary>
    /// <param name="start">The number of attempts already seen.</param>
    /// <returns>A copy of the tail of the list (empty when nothing is new).</returns>
    public IReadOnlyList<RestAttempt> SnapshotFrom(int start)
    {
        lock (_gate)
        {
            return start >= _attempts.Count ? [] : [.. _attempts.Skip(start)];
        }
    }
}
