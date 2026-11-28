using Enx.NetCord.Testing.State;

namespace Enx.NetCord.Testing;

/// <summary>Identifies a seeded voice or stage channel and creates message fixtures in its chat.</summary>
public sealed class SeededVoiceChannel
{
    private readonly FakeDiscord _fake;

    internal SeededVoiceChannel(FakeDiscord fake, ulong id)
    {
        ArgumentNullException.ThrowIfNull(fake);
        _fake = fake;
        Id = id;
    }

    /// <summary>Gets the channel ID.</summary>
    public ulong Id { get; }

    /// <summary>Creates a message by the specified user in the chat of this channel and returns the message ID.</summary>
    /// <remarks>
    /// With an explicit <paramref name="id"/> the message timestamp is the millisecond encoded in that ID, so a test can
    /// compose a snowflake to seed an old message; fixtures need not be created in chronological order.
    /// </remarks>
    /// <param name="content">The message content.</param>
    /// <param name="authorId">The ID of a user of this fake.</param>
    /// <param name="id">An explicit nonzero ID, or <see langword="null"/> to allocate one.</param>
    /// <returns>The message ID.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="content"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is zero.</exception>
    /// <exception cref="ObjectDisposedException">The fake has been disposed.</exception>
    /// <exception cref="InvalidOperationException">The channel or the author does not exist in this fake, or the ID was already assigned.</exception>
    public ulong Message(string content, ulong authorId, ulong? id = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        SeedGuard.ValidateId(id);
        _fake.ThrowIfDisposed();

        return SeedGuard.Unwrap(_fake.State.CreateMessage(StateCaller.Fixture, new CreateMessageInput(Id, authorId, content, id))).Id;
    }
}
