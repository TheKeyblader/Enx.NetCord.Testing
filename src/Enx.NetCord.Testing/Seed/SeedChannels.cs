using Enx.NetCord.Testing.State;

namespace Enx.NetCord.Testing;

/// <summary>Creates seeded guild channels for every handle that can (guild and category), enforcing the options table of ADR 0013, § 6.</summary>
internal static class SeedChannels
{
    private const int MaxSlowmode = 21600;

    /// <summary>Validates the arguments, then creates the channel through the state (so the parent and size invariants hold) and returns it.</summary>
    /// <param name="fake">The fake.</param>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="parentId">The category ID, or <see langword="null"/> for a channel outside any category.</param>
    /// <param name="kind">The channel type to create.</param>
    /// <param name="name">The channel name.</param>
    /// <param name="id">An explicit nonzero ID, or <see langword="null"/>.</param>
    /// <param name="options">The options, or <see langword="null"/>.</param>
    /// <returns>The detached channel.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is zero, or an option is out of bounds.</exception>
    /// <exception cref="ArgumentException">An option has no object for this channel type.</exception>
    /// <exception cref="ObjectDisposedException">The fake has been disposed.</exception>
    /// <exception cref="InvalidOperationException">The guild or the category no longer exists, the category or the guild is full, or the ID was already assigned.</exception>
    public static ChannelSnapshot Create(
        FakeDiscord fake,
        ulong guildId,
        ulong? parentId,
        ChannelKind kind,
        string name,
        ulong? id,
        SeededChannelOptions? options)
    {
        ArgumentNullException.ThrowIfNull(name);
        ValidateName(name);
        SeedGuard.ValidateId(id);
        var traits = ChannelTraits.Of(kind);
        Validate(options, traits, kind);
        fake.ThrowIfDisposed();

        var input = new CreateGuildChannelInput(
            guildId,
            kind,
            name,
            id,
            parentId,
            options?.Position,
            options?.Topic,
            options?.Nsfw ?? false,
            options?.Slowmode ?? 0);
        return SeedGuard.Unwrap(fake.State.CreateGuildChannel(StateCaller.Fixture, input));
    }

    // M1-DEBT-06: a channel name has 1 to 100 code points, as Discord requires (the state checks it again for any caller).
    private static void ValidateName(string name)
    {
        var length = name.EnumerateRunes().Count();
        if (length is < 1 or > FakeDiscordState.MaxChannelNameLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(name), length, $"A channel name is between 1 and {FakeDiscordState.MaxChannelNameLength} characters long.");
        }
    }

    private static void Validate(SeededChannelOptions? options, ChannelTraits traits, ChannelKind kind)
    {
        if (options is null)
        {
            return;
        }

        if (options.Topic is { } topic)
        {
            if (traits.TopicMaxLength == 0)
            {
                throw new ArgumentException($"A {Describe(kind)} has no topic.", nameof(options));
            }

            if (topic.Length > traits.TopicMaxLength)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options), topic.Length, $"The topic of a {Describe(kind)} is at most {traits.TopicMaxLength} characters long.");
            }
        }

        if (options.Nsfw && !traits.HasNsfw)
        {
            throw new ArgumentException($"A {Describe(kind)} cannot be age-restricted.", nameof(options));
        }

        if (options.Slowmode != 0 && !traits.HasSlowmode)
        {
            throw new ArgumentException($"A {Describe(kind)} has no slowmode.", nameof(options));
        }

        if (options.Slowmode is < 0 or > MaxSlowmode)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options), options.Slowmode, $"The slowmode is between 0 and {MaxSlowmode} seconds.");
        }

        if (options.Position is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.Position, "The position must not be negative.");
        }
    }

    private static string Describe(ChannelKind kind) => kind switch
    {
        ChannelKind.GuildText => "text channel",
        ChannelKind.GuildAnnouncement => "announcement channel",
        ChannelKind.GuildVoice => "voice channel",
        ChannelKind.GuildStageVoice => "stage channel",
        ChannelKind.GuildCategory => "category",
        ChannelKind.GuildForum => "forum channel",
        ChannelKind.GuildMedia => "media channel",
        _ => "channel",
    };
}
