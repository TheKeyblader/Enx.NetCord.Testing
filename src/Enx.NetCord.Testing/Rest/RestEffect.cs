using System.Net;
using Enx.NetCord.Testing.Rest;

namespace Enx.NetCord.Testing;

/// <summary>Describes a one-shot REST test effect.</summary>
public sealed class RestEffect
{
    /// <summary>The largest <c>Retry-After</c>, in seconds, that NetCord can represent in milliseconds as an <see cref="int"/>.</summary>
    internal const int MaxRetryAfterSeconds = 2_147_483;

    private static readonly TimeSpan MaxDelay = TimeSpan.FromMilliseconds(uint.MaxValue - 1d);

    private RestEffect(RestEffectKind kind, DiscordError? error, int retryAfterSeconds, TimeSpan delay)
    {
        Kind = kind;
        ErrorValue = error;
        RetryAfterSeconds = retryAfterSeconds;
        DelayDuration = delay;
    }

    internal RestEffectKind Kind { get; }

    internal DiscordError? ErrorValue { get; }

    internal int RetryAfterSeconds { get; }

    internal TimeSpan DelayDuration { get; }

    /// <summary>Returns a terminal error from the fake's Discord error catalog.</summary>
    /// <param name="status">The HTTP status.</param>
    /// <param name="code">The Discord JSON error code, zero for a general error.</param>
    /// <returns>The effect.</returns>
    /// <exception cref="ArgumentException">The status and code do not identify a supported simple error.</exception>
    public static RestEffect Error(HttpStatusCode status, int code = 0)
    {
        if (status == HttpStatusCode.TooManyRequests)
        {
            throw new ArgumentException("A 429 has its own format: use RestEffect.RateLimited.", nameof(status));
        }

        if (code == 50035)
        {
            throw new ArgumentException("Invalid Form Body (50035) needs field details and cannot be injected as a simple error.", nameof(code));
        }

        if (!DiscordError.TryCreateSimple(status, code, out var error))
        {
            throw new ArgumentException($"The status {(int)status} with code {code} is not a supported simple Discord error.", nameof(code));
        }

        return new RestEffect(RestEffectKind.Error, error, 0, TimeSpan.Zero);
    }

    /// <summary>Returns a per-user HTTP 429 with the specified Retry-After in seconds.</summary>
    /// <param name="retryAfterSeconds">The number of seconds, from 0 to 2147483.</param>
    /// <returns>The effect.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="retryAfterSeconds"/> is outside the supported range.</exception>
    public static RestEffect RateLimited(int retryAfterSeconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(retryAfterSeconds);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(retryAfterSeconds, MaxRetryAfterSeconds);
        return new RestEffect(RestEffectKind.RateLimited, null, retryAfterSeconds, TimeSpan.Zero);
    }

    /// <summary>Delays normal handling using the fake's clock.</summary>
    /// <param name="duration">A finite, non-negative delay supported by the clock's timers.</param>
    /// <returns>The effect.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="duration"/> is negative, infinite or too large.</exception>
    public static RestEffect Delay(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero || duration > MaxDelay)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "The delay must be finite, non-negative and supported by a timer.");
        }

        return new RestEffect(RestEffectKind.Delay, null, 0, duration);
    }
}

/// <summary>The kind of a <see cref="RestEffect"/>.</summary>
internal enum RestEffectKind
{
    /// <summary>A terminal catalog error.</summary>
    Error,

    /// <summary>A per-user 429.</summary>
    RateLimited,

    /// <summary>A delay on the fake's clock before normal handling.</summary>
    Delay,
}
