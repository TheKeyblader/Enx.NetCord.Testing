using NetCord.Rest.RateLimits;

namespace Enx.NetCord.Testing.Rest;

/// <summary>
/// A NetCord <see cref="IRateLimitManager"/> that grants every acquisition immediately and arms no timer, so the
/// default limiter (50 calls/second with real-time delays) cannot sleep outside the fake's virtual clock.
/// NetCord still reads the rate limit headers of responses; it just never waits because of them on its own.
/// </summary>
internal sealed class NoWaitRateLimitManager : IRateLimitManager
{
    private static readonly NoWaitGlobalRateLimiter Global = new();
    private static readonly NoWaitRouteRateLimiter Route = new();

    /// <inheritdoc />
    public ValueTask<IGlobalRateLimiter> GetGlobalRateLimiterAsync(CancellationToken cancellationToken = default)
        => new(Global);

    /// <inheritdoc />
    public ValueTask<IRouteRateLimiter> GetRouteRateLimiterAsync(global::NetCord.Rest.RateLimits.Route route, CancellationToken cancellationToken = default)
        => new(Route);

    /// <inheritdoc />
    public ValueTask ExchangeRouteRateLimiterAsync(
        global::NetCord.Rest.RateLimits.Route route,
        RateLimitInfo? rateLimitInfo,
        BucketInfo? previousBucketInfo,
        CancellationToken cancellationToken = default)
        => default;

    /// <inheritdoc />
    public void Dispose()
    {
    }

    private sealed class NoWaitGlobalRateLimiter : IGlobalRateLimiter
    {
        public ValueTask<RateLimitAcquisitionResult> TryAcquireAsync(CancellationToken cancellationToken = default)
            => new(RateLimitAcquisitionResult.NoRateLimit);

        public ValueTask IndicateRateLimitAsync(long reset, CancellationToken cancellationToken = default) => default;
    }

    private sealed class NoWaitRouteRateLimiter : IRouteRateLimiter
    {
        public bool HasBucketInfo => false;

        public BucketInfo? BucketInfo => null;

        public ValueTask<RateLimitAcquisitionResult> TryAcquireAsync(CancellationToken cancellationToken = default)
            => new(RateLimitAcquisitionResult.NoRateLimit);

        public ValueTask CancelAcquireAsync(long acquisitionTimestamp, CancellationToken cancellationToken = default) => default;

        public ValueTask UpdateAsync(RateLimitInfo rateLimitInfo, CancellationToken cancellationToken = default) => default;

        public ValueTask IndicateExchangeAsync(long timestamp, CancellationToken cancellationToken = default) => default;
    }
}
