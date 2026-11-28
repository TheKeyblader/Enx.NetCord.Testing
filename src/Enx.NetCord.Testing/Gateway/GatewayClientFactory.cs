using Enx.NetCord.Testing.Rest;
using NetCord;
using NetCord.Gateway;
using NetCord.Gateway.Compression;

namespace Enx.NetCord.Testing.Gateway;

/// <summary>Creates real NetCord gateway clients wired to the fake (ADR 0008, § 2).</summary>
internal static class GatewayClientFactory
{
    /// <summary>
    /// Creates a caller-owned gateway client whose <see cref="GatewayClient.Rest"/> has its own handler and no-wait rate
    /// limit manager, as <see cref="RestClientFactory.Create"/> gives a REST client. Disposing the gateway client disposes
    /// that REST client too (NetCord owns it, since it builds it from the configuration).
    /// </summary>
    /// <param name="context">The shared pipeline configuration.</param>
    /// <param name="token">The token the client authenticates with.</param>
    /// <returns>A new <see cref="GatewayClient"/> that cannot be started yet.</returns>
    public static GatewayClient Create(RestPipelineContext context, IEntityToken token)
        => new(token, CreateConfiguration(context));

    /// <summary>
    /// Builds the configuration of one gateway client: REST through <see cref="RestClientFactory.CreateConfiguration"/>,
    /// the sentinel hostname, no compression (the fake will send plain JSON text frames) and a WebSocket provider that
    /// refuses to connect until the in-memory Gateway exists. Every call returns new instances, so no object is shared
    /// between two clients.
    /// </summary>
    /// <param name="context">The shared pipeline configuration.</param>
    /// <returns>A new configuration.</returns>
    /// <remarks>Intents, shards and the reconnect strategy keep NetCord's defaults: M3-ADR-09 decides them.</remarks>
    public static GatewayClientConfiguration CreateConfiguration(RestPipelineContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new()
        {
            Hostname = context.Hostname,
            Compression = new UncompressedGatewayCompression(),
            WebSocketConnectionProvider = new UnavailableWebSocketConnectionProvider(context),
            RestClientConfiguration = RestClientFactory.CreateConfiguration(context),
        };
    }
}
