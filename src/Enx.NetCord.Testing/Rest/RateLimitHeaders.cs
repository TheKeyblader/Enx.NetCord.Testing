using System.Globalization;

namespace Enx.NetCord.Testing.Rest;

/// <summary>Adds the five Discord rate limit headers (ADR 0005). M1 models no quota: values are fixed.</summary>
internal static class RateLimitHeaders
{
    internal const string Bucket = "X-RateLimit-Bucket";
    internal const string Limit = "X-RateLimit-Limit";
    internal const string Remaining = "X-RateLimit-Remaining";
    internal const string ResetAfter = "X-RateLimit-Reset-After";
    internal const string Reset = "X-RateLimit-Reset";
    internal const string RetryAfter = "Retry-After";
    internal const string Scope = "X-RateLimit-Scope";

    /// <summary>Adds the headers of an ordinary response: limit 5, remaining 4, reset after 0.</summary>
    /// <param name="response">The response to complete.</param>
    /// <param name="entry">The resolved operation.</param>
    /// <param name="now">The virtual response time.</param>
    public static void AddOrdinary(FakeRestResponse response, RouteEntry entry, DateTimeOffset now)
        => Add(response, entry, now, remaining: 4, resetAfterSeconds: 0);

    /// <summary>Adds the headers of an injected per-user 429.</summary>
    /// <param name="response">The response to complete.</param>
    /// <param name="entry">The resolved operation.</param>
    /// <param name="now">The virtual response time.</param>
    /// <param name="retryAfterSeconds">The injected wait in seconds.</param>
    public static void AddRateLimited(FakeRestResponse response, RouteEntry entry, DateTimeOffset now, int retryAfterSeconds)
    {
        Add(response, entry, now, remaining: 0, resetAfterSeconds: retryAfterSeconds);
        response.Headers.Add(new(RetryAfter, retryAfterSeconds.ToString(CultureInfo.InvariantCulture)));
        response.Headers.Add(new(Scope, "user"));
    }

    private static void Add(FakeRestResponse response, RouteEntry entry, DateTimeOffset now, int remaining, int resetAfterSeconds)
    {
        var resetMilliseconds = now.ToUnixTimeMilliseconds() + (resetAfterSeconds * 1000L);
        response.Headers.Add(new(Bucket, entry.Bucket));
        response.Headers.Add(new(Limit, "5"));
        response.Headers.Add(new(Remaining, remaining.ToString(CultureInfo.InvariantCulture)));
        response.Headers.Add(new(ResetAfter, resetAfterSeconds.ToString(CultureInfo.InvariantCulture)));
        response.Headers.Add(new(Reset, FormatUnixSeconds(resetMilliseconds)));
    }

    private static string FormatUnixSeconds(long milliseconds)
        => string.Create(
            CultureInfo.InvariantCulture,
            $"{milliseconds / 1000}.{milliseconds % 1000:D3}");
}
