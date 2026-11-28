namespace Enx.NetCord.Testing;

/// <summary>Configures an isolated in-memory Discord instance.</summary>
public sealed class FakeDiscordOptions
{
    /// <summary>Gets the clock to use, or null to use the fake's fixed virtual clock.</summary>
    public TimeProvider? TimeProvider { get; init; }

    /// <summary>Gets the ID source, or null to create a deterministic generator for this fake.</summary>
    public ISnowflakeGenerator? SnowflakeGenerator { get; init; }

    /// <summary>Gets the raw test bot token, or null to synthesize one.</summary>
    /// <remarks>
    /// A token must be accepted by the <c>NetCord.BotToken</c> constructor; the nonzero ID it encodes is the ID of the
    /// bot user and of the application. When <see cref="ApplicationId"/> is also set, both must be equal.
    /// </remarks>
    public string? BotToken { get; init; }

    /// <summary>Gets the bot and application ID, or null to derive it from the token or allocate it.</summary>
    public ulong? ApplicationId { get; init; }

    /// <summary>Gets the policy for unimplemented REST routes. Defaults to <see cref="UnimplementedRouteMode.Lenient"/>.</summary>
    public UnimplementedRouteMode UnimplementedRoutes { get; init; }
}
