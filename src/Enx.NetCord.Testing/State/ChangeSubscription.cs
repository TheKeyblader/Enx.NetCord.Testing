using System.Threading.Channels;

namespace Enx.NetCord.Testing.State;

/// <summary>
/// The Gateway's single subscription to committed changes: an unbounded FIFO reader plus the projection
/// and last change number that were captured under the same lock as the subscription. Facts with a
/// number at or below <see cref="Initial"/>'s <c>LastChangeNumber</c> are already reflected in it.
/// Dispose to unsubscribe; the reader then completes after its buffered facts. Only the state writes.
/// </summary>
internal sealed class ChangeSubscription : IDisposable
{
    private readonly FakeDiscordState _owner;
    private readonly Channel<StateChange> _channel = Channel.CreateUnbounded<StateChange>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = true, // every write happens under the state lock
        AllowSynchronousContinuations = false,
    });

    internal ChangeSubscription(FakeDiscordState owner, StateProjection initial)
    {
        _owner = owner;
        Initial = initial;
    }

    internal ChannelReader<StateChange> Reader => _channel.Reader;

    internal StateProjection Initial { get; }

    /// <summary>Queues a change. Called by the state under its lock only.</summary>
    internal void Write(StateChange change) => _channel.Writer.TryWrite(change);

    /// <summary>Completes the writer. Called by the state under its lock only.</summary>
    internal void Complete() => _channel.Writer.TryComplete();

    public void Dispose() => _owner.Unsubscribe(this);
}
