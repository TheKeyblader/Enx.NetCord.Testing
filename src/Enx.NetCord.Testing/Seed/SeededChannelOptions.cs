namespace Enx.NetCord.Testing;

/// <summary>Optional settings of a seeded guild channel.</summary>
/// <remarks>
/// A setting Discord has no field for on the requested channel type throws <see cref="ArgumentException"/> when it differs from its
/// default (a non-null <see cref="Topic"/>, a true <see cref="Nsfw"/>, a nonzero <see cref="Slowmode"/>); left at its default it is
/// not applied. A value outside Discord's bounds throws <see cref="ArgumentOutOfRangeException"/>.
/// </remarks>
public sealed class SeededChannelOptions
{
    /// <summary>Gets the topic (text, announcement and forum channels), or <see langword="null"/> for none.</summary>
    public string? Topic { get; init; }

    /// <summary>Gets whether the channel is age-restricted (every type but categories).</summary>
    public bool Nsfw { get; init; }

    /// <summary>Gets the slowmode in seconds, Discord's <c>rate_limit_per_user</c> (text, voice, stage and forum channels).</summary>
    public int Slowmode { get; init; }

    /// <summary>Gets the sorting position, or <see langword="null"/> to place the channel after the others of its sorting group.</summary>
    public int? Position { get; init; }
}
