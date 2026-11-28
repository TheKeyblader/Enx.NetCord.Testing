using Enx.NetCord.Testing.State;

namespace Enx.NetCord.Testing;

/// <summary>Argument checks and failure mapping shared by the seed builders.</summary>
internal static class SeedGuard
{
    /// <summary>Rejects an explicit zero ID before anything is allocated or written.</summary>
    /// <param name="id">The optional explicit ID.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is zero.</exception>
    public static void ValidateId(ulong? id)
    {
        if (id == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), id, "An explicit snowflake must be nonzero.");
        }
    }

    /// <summary>Validates the arguments of a seeded reaction and adds it through the state, as the fixture caller.</summary>
    /// <param name="fake">The fake.</param>
    /// <param name="channelId">The channel of the message.</param>
    /// <param name="messageId">The message ID.</param>
    /// <param name="emoji">A unicode emoji or a custom one written <c>name:id</c>.</param>
    /// <param name="userId">The reacting user.</param>
    /// <param name="burst">Whether it is a super reaction.</param>
    /// <exception cref="ArgumentNullException"><paramref name="emoji"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="emoji"/> is neither a unicode emoji nor <c>name:id</c>.</exception>
    /// <exception cref="ObjectDisposedException">The fake has been disposed.</exception>
    /// <exception cref="InvalidOperationException">The message or the user does not exist in this fake, or the message already has 20 other emojis.</exception>
    public static void AddReaction(FakeDiscord fake, ulong channelId, ulong messageId, string emoji, ulong userId, bool burst)
    {
        ArgumentNullException.ThrowIfNull(emoji);
        if (!ReactionEmoji.TryParse(emoji, out var parsed))
        {
            throw new ArgumentException($"'{emoji}' is neither a unicode emoji nor a custom emoji written name:id.", nameof(emoji));
        }

        fake.ThrowIfDisposed();
        Unwrap(fake.State.AddReaction(StateCaller.Fixture, channelId, messageId, parsed, userId, burst));
    }

    /// <summary>Returns the value of a successful state result, or throws the seed-style error for its failure.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="result">The result.</param>
    /// <returns>The detached value.</returns>
    /// <exception cref="InvalidOperationException">The reference is unknown in this fake, or the membership already exists.</exception>
    public static T Unwrap<T>(StateResult<T> result)
        => result.TryGetValue(out var value) ? value : throw Describe(result.Failure);

    private static InvalidOperationException Describe(StateFailure failure)
    {
        var message = failure.Kind switch
        {
            StateFailureKind.MemberAlreadyExists => $"User {failure.ResourceId} is already a member of the guild.",
            StateFailureKind.UnknownUser => $"User {failure.ResourceId} does not exist in this fake.",
            StateFailureKind.UnknownGuild => $"Guild {failure.ResourceId} does not exist in this fake.",
            StateFailureKind.UnknownChannel => $"Channel {failure.ResourceId} does not exist in this fake.",
            StateFailureKind.UnknownRole => $"Role {failure.ResourceId} does not exist in this guild of this fake.",
            StateFailureKind.UnknownMember => $"Member {failure.ResourceId} does not exist in this guild of this fake.",
            StateFailureKind.UnknownMessage => $"Message {failure.ResourceId} does not exist in this fake.",
            StateFailureKind.InvalidChannelParent => $"Category {failure.ResourceId} does not exist in this guild of this fake (it may have been deleted).",
            StateFailureKind.ChannelParentNotCategory => $"Channel {failure.ResourceId} is not a category.",
            StateFailureKind.ChannelCannotHaveParent => $"A category cannot have a parent ({failure.ResourceId}).",
            StateFailureKind.ChannelParentFull => $"Category {failure.ResourceId} already holds 50 channels.",
            StateFailureKind.MaxGuildChannels => $"Guild {failure.ResourceId} already has 500 channels.",
            StateFailureKind.MissingAccess => $"The bot cannot access resource {failure.ResourceId}.",
            _ => failure.ToString(),
        };
        return new InvalidOperationException(message);
    }
}
