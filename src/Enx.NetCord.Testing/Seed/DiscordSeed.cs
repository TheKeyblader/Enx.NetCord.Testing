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

    /// <summary>Creates a guild, its everyone role and, by default, the bot's membership; returns the guild handle.</summary>
    /// <remarks>
    /// The bot's membership is that of an ordinary member (no roles, joined at the fake's clock) and allocates no ID. It
    /// is committed with the guild, so no reader or Gateway subscriber ever sees the guild without its bot. The seed
    /// itself is not subject to access control: children of a guild without the bot are created freely.
    /// </remarks>
    /// <param name="name">The guild name.</param>
    /// <param name="id">An explicit nonzero ID, or <see langword="null"/> to allocate one.</param>
    /// <param name="withBot">
    /// <see langword="true"/> to make the fake's bot a member of the guild in the same commit;
    /// <see langword="false"/> to create a guild the bot cannot see until it is added with
    /// <see cref="SeededGuild.Member(ulong)"/> (<see cref="FakeDiscord.ApplicationId"/>).
    /// </param>
    /// <returns>A handle on the guild.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is zero.</exception>
    /// <exception cref="ObjectDisposedException">The fake has been disposed.</exception>
    /// <exception cref="InvalidOperationException">The ID was already assigned in this fake, or the generator violates its contract.</exception>
    public SeededGuild Guild(string name, ulong? id = null, bool withBot = true)
    {
        ArgumentNullException.ThrowIfNull(name);
        SeedGuard.ValidateId(id);
        _fake.ThrowIfDisposed();

        var creation = SeedGuard.Unwrap(_fake.State.CreateGuild(new CreateGuildInput(name, id, WithBot: withBot)));
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
