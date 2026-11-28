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

    internal DiscordRecording(FakeDiscord discord, RestJournal journal)
    {
        _discord = discord;
        _journal = journal;
    }

    /// <summary>Gets a detached snapshot of completed REST attempts in recording order.</summary>
    /// <remarks>
    /// Every attempt that reached the in-memory transport and ended is listed once, error responses included.
    /// Two concurrent calls have no predictable relative order. Each read returns a new list: a snapshot taken earlier never changes.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">The fake has been disposed.</exception>
    public IReadOnlyList<RecordedRestRequest> Requests
    {
        get
        {
            _discord.ThrowIfDisposed();
            return [.. _journal.Snapshot().Select(RecordedRestRequest.From)];
        }
    }
}
