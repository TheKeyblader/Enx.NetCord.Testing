using Enx.NetCord.Testing.Rest;
using NetCord.Gateway.WebSockets;

namespace Enx.NetCord.Testing.Gateway;

/// <summary>
/// The WebSocket provider of the fake's gateway clients until the in-memory Gateway exists: it refuses every connection,
/// so <c>GatewayClient.StartAsync</c> fails before any socket is created and nothing reaches the network.
/// </summary>
/// <remarks>
/// NetCord calls <see cref="CreateConnection"/> first thing in its connection routine and lets the exception escape
/// <c>StartAsync</c> without retrying (the reconnect strategy only applies to a connection that was established).
/// The in-memory provider of M3-01 replaces this type.
/// </remarks>
internal sealed class UnavailableWebSocketConnectionProvider : IWebSocketConnectionProvider
{
    /// <summary>The message of the <see cref="NotSupportedException"/> thrown by <see cref="CreateConnection"/>.</summary>
    public const string NotAvailableMessage =
        "The fake Discord does not provide an in-memory Gateway yet (planned for M3): a GatewayClient created by "
        + "FakeDiscord.CreateGatewayClient() cannot be started. Its Rest client targets the fake and can be used.";

    private readonly RestPipelineContext _context;

    /// <summary>Initializes a new instance of the <see cref="UnavailableWebSocketConnectionProvider"/> class.</summary>
    /// <param name="context">The fake's shared configuration, used to report a disposed fake.</param>
    public UnavailableWebSocketConnectionProvider(RestPipelineContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <summary>Refuses the connection.</summary>
    /// <returns>Never returns.</returns>
    /// <exception cref="ObjectDisposedException">The fake has been disposed.</exception>
    /// <exception cref="NotSupportedException">The fake is alive but has no in-memory Gateway yet.</exception>
    public IWebSocketConnection CreateConnection()
    {
        _context.ThrowIfClosed();
        throw new NotSupportedException(NotAvailableMessage);
    }
}
