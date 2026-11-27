using NetCord;
using NetCord.Rest;

namespace Enx.NetCord.Testing.Rest;

/// <summary>Creates real NetCord clients wired to the fake REST transport.</summary>
internal static class RestClientFactory
{
    /// <summary>
    /// Creates a caller-owned client with its own handler and rate limit manager. The hostname only lets the handler
    /// recognize requests: the in-memory handler, with no network fallback, is what guarantees nothing leaves the process.
    /// </summary>
    /// <param name="context">The shared pipeline configuration.</param>
    /// <param name="token">The token the client authenticates with.</param>
    /// <returns>A new <see cref="RestClient"/>.</returns>
    public static RestClient Create(RestPipelineContext context, IToken token)
        => new(token, CreateConfiguration(context));

    /// <summary>
    /// Builds the configuration every fake client uses, with a new handler and a new no-wait rate limit manager so that
    /// disposing one client affects no other. Gateway and Hosting reuse it for their own REST client.
    /// </summary>
    /// <param name="context">The shared pipeline configuration.</param>
    /// <returns>A new configuration.</returns>
    public static RestClientConfiguration CreateConfiguration(RestPipelineContext context)
        => new()
        {
            Hostname = context.Hostname,
            RequestHandler = new FakeRestRequestHandler(context),
            RateLimitManager = new NoWaitRateLimitManager(),
        };
}
