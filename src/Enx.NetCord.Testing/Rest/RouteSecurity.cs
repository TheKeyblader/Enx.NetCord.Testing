namespace Enx.NetCord.Testing.Rest;

/// <summary>The authentication alternatives an operation accepts, as documented by Discord.</summary>
[Flags]
internal enum RouteSecurity
{
    /// <summary>No alternative is accepted (never produced by the inventory).</summary>
    None = 0,

    /// <summary>The <c>BotToken</c> scheme: <c>Authorization: Bot {token}</c>.</summary>
    Bot = 1,

    /// <summary>The <c>OAuth2</c> scheme (Bearer). The fake does not model Bearer identities yet, so it never matches.</summary>
    OAuth2 = 2,

    /// <summary>The empty security requirement <c>{}</c>: the operation accepts a request without credentials.</summary>
    Anonymous = 4,
}
