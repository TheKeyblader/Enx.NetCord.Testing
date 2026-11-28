namespace Enx.NetCord.Testing;

/// <summary>Identifies a seeded forum channel.</summary>
/// <remarks>Forum posts arrive with M4-REST-01.</remarks>
public sealed class SeededForumChannel
{
    // The fake comes back with the post fixtures (M4-REST-01); the constructor already takes it so the callers do not change then.
    internal SeededForumChannel(FakeDiscord fake, ulong id)
    {
        ArgumentNullException.ThrowIfNull(fake);
        Id = id;
    }

    /// <summary>Gets the channel ID.</summary>
    public ulong Id { get; }
}
