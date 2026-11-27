namespace Enx.NetCord.Testing;

/// <summary>Configures an isolated in-memory Discord instance.</summary>
public sealed class FakeDiscordOptions
{
    /// <summary>Gets the clock to use, or null to use the fake's fixed virtual clock.</summary>
    public TimeProvider? TimeProvider { get; init; }

    /// <summary>Gets the ID source, or null to create a deterministic generator for this fake.</summary>
    public ISnowflakeGenerator? SnowflakeGenerator { get; init; }
}
