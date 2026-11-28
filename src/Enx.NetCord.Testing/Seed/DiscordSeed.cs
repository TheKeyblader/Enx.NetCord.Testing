using Enx.NetCord.Testing.State;

namespace Enx.NetCord.Testing;

/// <summary>Creates top-level fixtures in one fake.</summary>
/// <remarks>
/// A successful call is committed immediately, with the same notifications as any state mutation and without a
/// fictitious REST request; it proves nothing about the REST endpoints themselves. An explicit <c>id</c> must be nonzero
/// and never assigned in this fake (even after deletion); its encoded millisecond is the creation date of the entity.
/// </remarks>
public sealed class DiscordSeed
{
    private readonly FakeDiscord _fake;

    internal DiscordSeed(FakeDiscord fake)
    {
        _fake = fake;
    }

    /// <summary>Creates a guild and its everyone role; returns the guild handle.</summary>
    /// <param name="name">The guild name.</param>
    /// <param name="id">An explicit nonzero ID, or <see langword="null"/> to allocate one.</param>
    /// <returns>A handle on the guild.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is zero.</exception>
    /// <exception cref="ObjectDisposedException">The fake has been disposed.</exception>
    /// <exception cref="InvalidOperationException">The ID was already assigned in this fake, or the generator violates its contract.</exception>
    public SeededGuild Guild(string name, ulong? id = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        SeedGuard.ValidateId(id);
        _fake.ThrowIfDisposed();

        var creation = SeedGuard.Unwrap(_fake.State.CreateGuild(new CreateGuildInput(name, id)));
        return new SeededGuild(_fake, creation.Guild.Id);
    }

    /// <summary>Creates a user and returns the user ID.</summary>
    /// <param name="username">The username.</param>
    /// <param name="id">An explicit nonzero ID, or <see langword="null"/> to allocate one.</param>
    /// <returns>The user ID.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="username"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is zero.</exception>
    /// <exception cref="ObjectDisposedException">The fake has been disposed.</exception>
    /// <exception cref="InvalidOperationException">The ID was already assigned in this fake, or the generator violates its contract.</exception>
    public ulong User(string username, ulong? id = null)
    {
        ArgumentNullException.ThrowIfNull(username);
        SeedGuard.ValidateId(id);
        _fake.ThrowIfDisposed();

        return _fake.State.CreateUser(new CreateUserInput(username, id)).Id;
    }
}
