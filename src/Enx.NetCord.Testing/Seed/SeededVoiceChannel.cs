namespace Enx.NetCord.Testing;

/// <summary>Identifies a seeded voice or stage channel.</summary>
/// <remarks>Creating messages in its chat arrives with M1-REST-03B.</remarks>
public sealed class SeededVoiceChannel
{
    // The fake comes back with Message (M1-REST-03B); the constructor already takes it so the callers do not change then.
    internal SeededVoiceChannel(FakeDiscord fake, ulong id)
    {
        ArgumentNullException.ThrowIfNull(fake);
        Id = id;
    }

    /// <summary>Gets the channel ID.</summary>
    public ulong Id { get; }
}
