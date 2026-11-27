using System.Threading.Channels;

namespace Enx.NetCord.Testing.State;

/// <summary>
/// The Gateway's single subscription to committed changes: an unbounded FIFO reader plus the projection
/// and last change number that were captured under the same lock as the subscription. Facts with a
/// number at or below <see cref="Initial"/>'s <c>LastChangeNumber</c> are already reflected in it.
/// Dispose to unsubscribe; the reader then completes after its buffered facts.
/// </summary>
internal sealed class ChangeSubscription : IDisposable
{
    private readonly FakeDiscordState _owner;

    internal ChangeSubscription(
        FakeDiscordState owner,
        Channel<StateChange> channel,
        StateProjection initial)
    {
        _owner = owner;
        Channel = channel;
        Initial = initial;
    }

    internal Channel<StateChange> Channel { get; }

    internal ChannelReader<StateChange> Reader => Channel.Reader;

    internal StateProjection Initial { get; }

    public void Dispose() => _owner.Unsubscribe(this);
}
