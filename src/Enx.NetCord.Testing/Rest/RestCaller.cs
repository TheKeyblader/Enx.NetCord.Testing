namespace Enx.NetCord.Testing.Rest;

/// <summary>How the caller of a REST attempt was identified.</summary>
internal enum RestCallerKind
{
    /// <summary>No <c>Authorization</c> header.</summary>
    Anonymous,

    /// <summary>A header that is not a known credential of this fake (unknown token, Bearer, other scheme).</summary>
    Unrecognized,

    /// <summary>A known bot token.</summary>
    Bot,
}

/// <summary>The identified caller of a REST attempt.</summary>
/// <param name="Kind">The kind of identity.</param>
/// <param name="UserId">The bot user ID for <see cref="RestCallerKind.Bot"/>, otherwise zero.</param>
internal readonly record struct RestCaller(RestCallerKind Kind, ulong UserId = 0)
{
    /// <summary>Gets the caller without credentials.</summary>
    public static RestCaller Anonymous { get; } = new(RestCallerKind.Anonymous);

    /// <summary>Gets the caller with credentials the fake does not know.</summary>
    public static RestCaller Unrecognized { get; } = new(RestCallerKind.Unrecognized);

    /// <summary>Returns whether the caller satisfies at least one authentication alternative.</summary>
    /// <param name="security">The operation's alternatives.</param>
    /// <returns><see langword="true"/> when the request may proceed.</returns>
    public bool Satisfies(RouteSecurity security)
        => security.HasFlag(RouteSecurity.Anonymous)
           || (Kind == RestCallerKind.Bot && security.HasFlag(RouteSecurity.Bot));
}

/// <summary>Maps an <c>Authorization</c> header value to a caller identity.</summary>
internal interface IRestIdentityResolver
{
    /// <summary>Identifies the caller.</summary>
    /// <param name="authorization">The effective header value, or <see langword="null"/> when absent.</param>
    /// <returns>The caller.</returns>
    RestCaller Resolve(string? authorization);
}

/// <summary>Knows exactly one bot token, the one of the fake's identity.</summary>
internal sealed class BotIdentityResolver(string rawToken, ulong botUserId) : IRestIdentityResolver
{
    private const string Scheme = "Bot ";

    /// <inheritdoc />
    public RestCaller Resolve(string? authorization)
    {
        if (string.IsNullOrWhiteSpace(authorization))
        {
            return RestCaller.Anonymous;
        }

        return authorization.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase)
               && string.Equals(authorization[Scheme.Length..], rawToken, StringComparison.Ordinal)
            ? new RestCaller(RestCallerKind.Bot, botUserId)
            : RestCaller.Unrecognized;
    }
}
