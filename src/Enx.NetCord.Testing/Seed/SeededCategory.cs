using Enx.NetCord.Testing.State;

namespace Enx.NetCord.Testing;

/// <summary>Identifies a seeded category and creates channels in it.</summary>
/// <remarks>
/// A category holds at most 50 channels and cannot hold another category. Each method throws
/// <see cref="InvalidOperationException"/> when the category has been deleted or is full, besides the exceptions of the same
/// method of <see cref="SeededGuild"/>.
/// </remarks>
public sealed class SeededCategory
{
    private readonly FakeDiscord _fake;
    private readonly ulong _guildId;

    internal SeededCategory(FakeDiscord fake, ulong guildId, ulong id)
    {
        _fake = fake;
        _guildId = guildId;
        Id = id;
    }

    /// <summary>Gets the category ID.</summary>
    public ulong Id { get; }

    /// <summary>Creates a guild text channel in this category and returns its handle.</summary>
    /// <param name="name">The channel name.</param>
    /// <param name="id">An explicit nonzero ID, or <see langword="null"/> to allocate one.</param>
    /// <param name="options">The options, or <see langword="null"/> for the defaults.</param>
    /// <returns>A handle on the channel.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is zero, or an option is out of Discord's bounds.</exception>
    /// <exception cref="ObjectDisposedException">The fake has been disposed.</exception>
    /// <exception cref="InvalidOperationException">The category or the guild no longer exists, the category or the guild is full, or the ID was already assigned.</exception>
    public SeededTextChannel TextChannel(string name, ulong? id = null, SeededChannelOptions? options = null)
        => new(_fake, Create(ChannelKind.GuildText, name, id, options).Id);

    /// <summary>Creates an announcement channel in this category and returns its handle.</summary>
    /// <param name="name">The channel name.</param>
    /// <param name="id">An explicit nonzero ID, or <see langword="null"/> to allocate one.</param>
    /// <param name="options">The options, or <see langword="null"/> for the defaults.</param>
    /// <returns>A handle on the channel.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is zero, or an option is out of Discord's bounds.</exception>
    /// <exception cref="ArgumentException">An option has no object for an announcement channel (a nonzero slowmode).</exception>
    /// <exception cref="ObjectDisposedException">The fake has been disposed.</exception>
    /// <exception cref="InvalidOperationException">The category or the guild no longer exists, the category or the guild is full, or the ID was already assigned.</exception>
    public SeededTextChannel AnnouncementChannel(string name, ulong? id = null, SeededChannelOptions? options = null)
        => new(_fake, Create(ChannelKind.GuildAnnouncement, name, id, options).Id);

    /// <summary>Creates a voice channel in this category and returns its handle.</summary>
    /// <param name="name">The channel name.</param>
    /// <param name="id">An explicit nonzero ID, or <see langword="null"/> to allocate one.</param>
    /// <param name="options">The options, or <see langword="null"/> for the defaults.</param>
    /// <returns>A handle on the channel.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is zero, or an option is out of Discord's bounds.</exception>
    /// <exception cref="ArgumentException">An option has no object for a voice channel (a topic).</exception>
    /// <exception cref="ObjectDisposedException">The fake has been disposed.</exception>
    /// <exception cref="InvalidOperationException">The category or the guild no longer exists, the category or the guild is full, or the ID was already assigned.</exception>
    public SeededVoiceChannel VoiceChannel(string name, ulong? id = null, SeededChannelOptions? options = null)
        => new(_fake, Create(ChannelKind.GuildVoice, name, id, options).Id);

    /// <summary>Creates a stage channel in this category and returns its handle.</summary>
    /// <param name="name">The channel name.</param>
    /// <param name="id">An explicit nonzero ID, or <see langword="null"/> to allocate one.</param>
    /// <param name="options">The options, or <see langword="null"/> for the defaults.</param>
    /// <returns>A handle on the channel.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is zero, or an option is out of Discord's bounds.</exception>
    /// <exception cref="ArgumentException">An option has no object for a stage channel (a topic).</exception>
    /// <exception cref="ObjectDisposedException">The fake has been disposed.</exception>
    /// <exception cref="InvalidOperationException">The category or the guild no longer exists, the category or the guild is full, or the ID was already assigned.</exception>
    public SeededVoiceChannel StageChannel(string name, ulong? id = null, SeededChannelOptions? options = null)
        => new(_fake, Create(ChannelKind.GuildStageVoice, name, id, options).Id);

    /// <summary>Creates a forum channel in this category and returns its handle.</summary>
    /// <param name="name">The channel name.</param>
    /// <param name="id">An explicit nonzero ID, or <see langword="null"/> to allocate one.</param>
    /// <param name="options">The options, or <see langword="null"/> for the defaults.</param>
    /// <returns>A handle on the channel.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is zero, or an option is out of Discord's bounds.</exception>
    /// <exception cref="ObjectDisposedException">The fake has been disposed.</exception>
    /// <exception cref="InvalidOperationException">The category or the guild no longer exists, the category or the guild is full, or the ID was already assigned.</exception>
    public SeededForumChannel ForumChannel(string name, ulong? id = null, SeededChannelOptions? options = null)
        => new(_fake, Create(ChannelKind.GuildForum, name, id, options).Id);

    private ChannelSnapshot Create(ChannelKind kind, string name, ulong? id, SeededChannelOptions? options)
        => SeedChannels.Create(_fake, _guildId, Id, kind, name, id, options);
}
