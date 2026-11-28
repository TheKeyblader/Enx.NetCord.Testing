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

    /// <summary>Creates a category and returns its handle.</summary>
    /// <remarks>A category holds at most 50 channels and cannot hold another category. Only <see cref="SeededChannelOptions.Position"/> applies to it.</remarks>
    /// <param name="name">The category name.</param>
    /// <param name="id">An explicit nonzero ID, or <see langword="null"/> to allocate one.</param>
    /// <param name="options">The options, or <see langword="null"/> for the defaults.</param>
    /// <returns>A handle on the category.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is zero, or an option is out of Discord's bounds.</exception>
    /// <exception cref="ArgumentException">An option has no object for a category (a topic, nsfw or a slowmode).</exception>
    /// <exception cref="ObjectDisposedException">The fake has been disposed.</exception>
    /// <exception cref="InvalidOperationException">The guild no longer exists, it already has 500 channels, or the ID was already assigned in this fake.</exception>
    public SeededCategory Category(string name, ulong? id = null, SeededChannelOptions? options = null)
        => new(_fake, Id, Create(ChannelKind.GuildCategory, name, id, options).Id);

    /// <summary>Creates a guild text channel outside any category and returns its handle.</summary>
    /// <param name="name">The channel name.</param>
    /// <param name="id">An explicit nonzero ID, or <see langword="null"/> to allocate one.</param>
    /// <param name="options">The options, or <see langword="null"/> for the defaults.</param>
    /// <returns>A handle on the channel.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is zero, or an option is out of Discord's bounds.</exception>
    /// <exception cref="ObjectDisposedException">The fake has been disposed.</exception>
    /// <exception cref="InvalidOperationException">The guild no longer exists, it already has 500 channels, or the ID was already assigned in this fake.</exception>
    public SeededTextChannel TextChannel(string name, ulong? id = null, SeededChannelOptions? options = null)
        => new(_fake, Create(ChannelKind.GuildText, name, id, options).Id);

    /// <summary>Creates an announcement channel outside any category and returns its handle.</summary>
    /// <remarks>An announcement channel has no slowmode, so a nonzero <see cref="SeededChannelOptions.Slowmode"/> throws <see cref="ArgumentException"/>.</remarks>
    /// <param name="name">The channel name.</param>
    /// <param name="id">An explicit nonzero ID, or <see langword="null"/> to allocate one.</param>
    /// <param name="options">The options, or <see langword="null"/> for the defaults.</param>
    /// <returns>A handle on the channel.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is zero, or an option is out of Discord's bounds.</exception>
    /// <exception cref="ArgumentException">An option has no object for an announcement channel.</exception>
    /// <exception cref="ObjectDisposedException">The fake has been disposed.</exception>
    /// <exception cref="InvalidOperationException">The guild no longer exists, it already has 500 channels, or the ID was already assigned in this fake.</exception>
    public SeededTextChannel AnnouncementChannel(string name, ulong? id = null, SeededChannelOptions? options = null)
        => new(_fake, Create(ChannelKind.GuildAnnouncement, name, id, options).Id);

    /// <summary>Creates a voice channel outside any category and returns its handle.</summary>
    /// <remarks>A voice channel has no topic: a non-null <see cref="SeededChannelOptions.Topic"/> throws <see cref="ArgumentException"/>.</remarks>
    /// <param name="name">The channel name.</param>
    /// <param name="id">An explicit nonzero ID, or <see langword="null"/> to allocate one.</param>
    /// <param name="options">The options, or <see langword="null"/> for the defaults.</param>
    /// <returns>A handle on the channel.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is zero, or an option is out of Discord's bounds.</exception>
    /// <exception cref="ArgumentException">An option has no object for a voice channel.</exception>
    /// <exception cref="ObjectDisposedException">The fake has been disposed.</exception>
    /// <exception cref="InvalidOperationException">The guild no longer exists, it already has 500 channels, or the ID was already assigned in this fake.</exception>
    public SeededVoiceChannel VoiceChannel(string name, ulong? id = null, SeededChannelOptions? options = null)
        => new(_fake, Create(ChannelKind.GuildVoice, name, id, options).Id);

    /// <summary>Creates a stage channel outside any category and returns its handle.</summary>
    /// <remarks>The fake does not check the guild's features: any guild accepts a stage channel (ADR 0013, § 5).</remarks>
    /// <param name="name">The channel name.</param>
    /// <param name="id">An explicit nonzero ID, or <see langword="null"/> to allocate one.</param>
    /// <param name="options">The options, or <see langword="null"/> for the defaults.</param>
    /// <returns>A handle on the channel.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is zero, or an option is out of Discord's bounds.</exception>
    /// <exception cref="ArgumentException">An option has no object for a stage channel.</exception>
    /// <exception cref="ObjectDisposedException">The fake has been disposed.</exception>
    /// <exception cref="InvalidOperationException">The guild no longer exists, it already has 500 channels, or the ID was already assigned in this fake.</exception>
    public SeededVoiceChannel StageChannel(string name, ulong? id = null, SeededChannelOptions? options = null)
        => new(_fake, Create(ChannelKind.GuildStageVoice, name, id, options).Id);

    /// <summary>Creates a forum channel outside any category and returns its handle.</summary>
    /// <remarks>A forum topic (its guidelines) is at most 4096 characters long.</remarks>
    /// <param name="name">The channel name.</param>
    /// <param name="id">An explicit nonzero ID, or <see langword="null"/> to allocate one.</param>
    /// <param name="options">The options, or <see langword="null"/> for the defaults.</param>
    /// <returns>A handle on the channel.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is zero, or an option is out of Discord's bounds.</exception>
    /// <exception cref="ObjectDisposedException">The fake has been disposed.</exception>
    /// <exception cref="InvalidOperationException">The guild no longer exists, it already has 500 channels, or the ID was already assigned in this fake.</exception>
    public SeededForumChannel ForumChannel(string name, ulong? id = null, SeededChannelOptions? options = null)
        => new(_fake, Create(ChannelKind.GuildForum, name, id, options).Id);

    private ChannelSnapshot Create(ChannelKind kind, string name, ulong? id, SeededChannelOptions? options)
        => SeedChannels.Create(_fake, Id, null, kind, name, id, options);

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
