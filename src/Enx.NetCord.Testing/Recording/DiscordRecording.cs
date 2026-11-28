using Enx.NetCord.Testing.Rest;

namespace Enx.NetCord.Testing;

/// <summary>Provides read-only observations from one fake.</summary>
/// <remarks>
/// The journal is neither a source of state nor a Gateway barrier: it only reports what already happened.
/// It is never reset, so a test may keep a snapshot taken before an action and compare it afterwards.
/// </remarks>
public sealed class DiscordRecording
{
    private readonly FakeDiscord _discord;
    private readonly RestJournal _journal;
    private readonly Lock _gate = new();

    // An attempt never changes once recorded, so its projection is computed once and shared by every snapshot.
    private readonly List<RecordedRestRequest> _projected = [];

    internal DiscordRecording(FakeDiscord discord, RestJournal journal)
    {
        _discord = discord;
        _journal = journal;
    }

    /// <summary>Gets a detached snapshot of completed REST attempts in recording order.</summary>
    /// <remarks>
    /// Every attempt that reached the in-memory transport and ended is listed once, error responses included.
    /// Two concurrent calls have no predictable relative order. Each read returns a new list, so a snapshot taken earlier never changes; the records themselves are immutable and shared between snapshots.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">The fake has been disposed.</exception>
    public IReadOnlyList<RecordedRestRequest> Requests
    {
        get
        {
            _discord.ThrowIfDisposed();

            lock (_gate)
            {
                foreach (var attempt in _journal.SnapshotFrom(_projected.Count))
                {
                    _projected.Add(RecordedRestRequest.From(attempt));
                }

                return [.. _projected];
            }
        }
    }
}
