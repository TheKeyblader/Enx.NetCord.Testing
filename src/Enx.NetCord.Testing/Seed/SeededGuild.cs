using Enx.NetCord.Testing.State;

namespace Enx.NetCord.Testing;

/// <summary>Identifies a seeded guild and creates its child fixtures.</summary>
/// <remarks>
/// The handle keeps the fake and an ID, never mutable state. A numeric reference passed to a method is resolved in
/// this fake only: a number alone does not prove where it came from.
/// </remarks>
public sealed class SeededGuild
{
    private readonly FakeDiscord _fake;

    internal SeededGuild(FakeDiscord fake, ulong id)
    {
        _fake = fake;
        Id = id;
    }

    /// <summary>Gets the guild ID.</summary>
    public ulong Id { get; }

    /// <summary>Creates a guild text channel and returns its handle.</summary>
    /// <param name="name">The channel name.</param>
    /// <param name="id">An explicit nonzero ID, or <see langword="null"/> to allocate one.</param>
    /// <returns>A handle on the channel.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is zero.</exception>
    /// <exception cref="ObjectDisposedException">The fake has been disposed.</exception>
    /// <exception cref="InvalidOperationException">The guild no longer exists, or the ID was already assigned in this fake.</exception>
    public SeededTextChannel TextChannel(string name, ulong? id = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        SeedGuard.ValidateId(id);
        _fake.ThrowIfDisposed();

        var channel = SeedGuard.Unwrap(_fake.State.CreateTextChannel(StateCaller.Fixture, new CreateTextChannelInput(Id, name, id)));
        return new SeededTextChannel(_fake, channel.Id);
    }

    /// <summary>Creates a guild role and returns the role ID.</summary>
    /// <param name="name">The role name.</param>
    /// <param name="id">An explicit nonzero ID, or <see langword="null"/> to allocate one.</param>
    /// <returns>The role ID.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is zero.</exception>
    /// <exception cref="ObjectDisposedException">The fake has been disposed.</exception>
    /// <exception cref="InvalidOperationException">The guild no longer exists, or the ID was already assigned in this fake.</exception>
    public ulong Role(string name, ulong? id = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        SeedGuard.ValidateId(id);
        _fake.ThrowIfDisposed();

        return SeedGuard.Unwrap(_fake.State.CreateRole(StateCaller.Fixture, new CreateRoleInput(Id, name, id))).Id;
    }

    /// <summary>Creates a user and guild membership together and returns the user ID.</summary>
    /// <param name="username">The username.</param>
    /// <param name="id">An explicit nonzero user ID, or <see langword="null"/> to allocate one.</param>
    /// <returns>The user ID.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="username"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is zero.</exception>
    /// <exception cref="ObjectDisposedException">The fake has been disposed.</exception>
    /// <exception cref="InvalidOperationException">The guild no longer exists, or the ID was already assigned in this fake. Nothing is created.</exception>
    public ulong Member(string username, ulong? id = null)
    {
        ArgumentNullException.ThrowIfNull(username);
        SeedGuard.ValidateId(id);
        _fake.ThrowIfDisposed();

        return SeedGuard.Unwrap(_fake.State.CreateUserAndMember(StateCaller.Fixture, Id, new CreateUserInput(username, id))).UserId;
    }

    /// <summary>Adds an existing user to the guild and returns the same user ID.</summary>
    /// <remarks>
    /// The fake's bot is a user of the fake: <c>guild.Member(discord.ApplicationId)</c> makes it join a guild created with
    /// <c>withBot: false</c>. On a guild created with the bot this throws, as for any member already present.
    /// </remarks>
    /// <param name="userId">The ID of a user of this fake.</param>
    /// <returns><paramref name="userId"/>.</returns>
    /// <exception cref="ObjectDisposedException">The fake has been disposed.</exception>
    /// <exception cref="InvalidOperationException">The guild or the user does not exist in this fake, or the user is already a member.</exception>
    public ulong Member(ulong userId)
    {
        _fake.ThrowIfDisposed();

        return SeedGuard.Unwrap(_fake.State.AddMember(StateCaller.Fixture, new AddMemberInput(Id, userId))).UserId;
    }
}
