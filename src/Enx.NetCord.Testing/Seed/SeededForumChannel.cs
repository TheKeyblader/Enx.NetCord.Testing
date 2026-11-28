namespace Enx.NetCord.Testing;

/// <summary>Identifies a seeded forum channel.</summary>
/// <remarks>Forum posts arrive with M4-REST-01.</remarks>
public sealed class SeededForumChannel
{
    private readonly FakeDiscord _fake;

    internal SeededForumChannel(FakeDiscord fake, ulong id)
    {
        _fake = fake;
        Id = id;
    }

    /// <summary>Gets the channel ID.</summary>
    public ulong Id { get; }
}
