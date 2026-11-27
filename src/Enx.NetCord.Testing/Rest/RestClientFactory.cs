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
        => new(token, new RestClientConfiguration
        {
            Hostname = context.Hostname,
            RequestHandler = new FakeRestRequestHandler(context),
            RateLimitManager = new NoWaitRateLimitManager(),
        });
}
