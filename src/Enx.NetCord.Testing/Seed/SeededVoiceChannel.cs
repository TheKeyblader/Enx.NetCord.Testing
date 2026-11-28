namespace Enx.NetCord.Testing;

/// <summary>Identifies a seeded voice or stage channel.</summary>
/// <remarks>Creating messages in its chat arrives with M1-REST-03B.</remarks>
public sealed class SeededVoiceChannel
{
    private readonly FakeDiscord _fake;

    internal SeededVoiceChannel(FakeDiscord fake, ulong id)
    {
        _fake = fake;
        Id = id;
    }

    /// <summary>Gets the channel ID.</summary>
    public ulong Id { get; }
}
