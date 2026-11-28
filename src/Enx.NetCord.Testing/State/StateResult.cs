using System.Diagnostics.CodeAnalysis;

namespace Enx.NetCord.Testing.State;

/// <summary>Business reasons an operation can be refused. Route families add the values they need.</summary>
internal enum StateFailureKind
{
    UnknownUser,
    UnknownGuild,
    UnknownChannel,
    UnknownRole,
    UnknownMember,
    UnknownMessage,
    MemberAlreadyExists,

    /// <summary>The resource exists but the bot cannot see it: it is not a member of the guild (403 <c>50001</c>).</summary>
    MissingAccess,

    /// <summary>A guild channel cannot become, or be created as, the requested type (ADR 0013, § 5): 400 <c>50035</c> on <c>type</c>.</summary>
    InvalidChannelType,

    /// <summary>The requested parent does not exist, or belongs to another guild (ADR 0013, § 2): 400 <c>50035</c> on <c>parent_id</c>.</summary>
    InvalidChannelParent,

    /// <summary>The requested parent exists but is not a category (ADR 0013, § 2): 400 <c>50035</c> on <c>parent_id</c> (hypothesis).</summary>
    ChannelParentNotCategory,

    /// <summary>The channel is a category, which cannot have a parent (ADR 0013, § 2): 400 <c>50035</c> on <c>parent_id</c> (hypothesis).</summary>
    ChannelCannotHaveParent,

    /// <summary>The requested parent category already holds 50 channels (ADR 0013, § 2): 400 <c>50035</c> on <c>parent_id</c> (hypothesis).</summary>
    ChannelParentFull,

    /// <summary>The guild already has 500 channels (ADR 0013, § 1): 400 <c>30013</c> (status is a hypothesis).</summary>
    MaxGuildChannels,

    /// <summary>A guild channel name must not be empty: 400 <c>50035</c> on <c>name</c> (a DM accepts an empty one).</summary>
    InvalidChannelName,

    /// <summary>The topic is longer than the channel type allows, 1024 outside forum and media channels (ADR 0013, § 1): 400 <c>50035</c> on <c>topic</c> (form is a hypothesis).</summary>
    InvalidChannelTopic,

    /// <summary>The bitrate exceeds what the voice or stage channel allows (ADR 0013, § 1): 400 <c>30052</c> (status is a hypothesis).</summary>
    ChannelBitrateTooHigh,

    /// <summary>The user limit exceeds what the voice or stage channel allows, carried by <see cref="StateFailure.Limit"/> (ADR 0013, § 1): 400 <c>50035</c> on <c>user_limit</c> (form is a hypothesis).</summary>
    InvalidChannelUserLimit,

    /// <summary>The bot edits the content, embeds or attachments of another user's message: 403 <c>50005</c>.</summary>
    CannotEditOtherAuthor,

    /// <summary>A message would have neither content, embed nor attachment: 400 <c>50006</c>.</summary>
    EmptyMessage,

    /// <summary>A bulk delete names a message older than two weeks: 400 <c>50034</c>.</summary>
    MessageTooOldForBulkDelete,

    /// <summary>The replied-to message does not exist and the reply must fail: 400 <c>50035</c> on <c>message_reference</c>.</summary>
    UnknownMessageReference,

    /// <summary>The action is for guild channels only and the channel is a DM: 403 <c>50003</c>.</summary>
    CannotExecuteInDm,

    /// <summary>The action does not apply to this type of channel: 400 <c>50024</c>.</summary>
    CannotExecuteOnChannelType,

    /// <summary>The user has no such reaction to remove, or nobody reacted with that emoji: 404 <c>10014</c> (hypothesis, docs/parity/errors.md).</summary>
    UnknownEmoji,

    /// <summary>The message already has 20 distinct reactions and the emoji is a new one: 400 <c>30010</c> (status is a hypothesis).</summary>
    MaxReactions,

    /// <summary>A message is sent to a channel that has no chat, a category: 400 <c>50008</c> (status is a hypothesis).</summary>
    CannotSendInNonTextChannel,

    /// <summary>A message is sent to a forum or media channel, where a post is a thread: 400 <c>40058</c> (status is a hypothesis).</summary>
    CannotSendInForumChannel,

    /// <summary>The message was already published by a crosspost: 400 <c>40033</c> (status is a hypothesis).</summary>
    MessageAlreadyCrossposted,
}

/// <summary>A refused operation: the reason and the ID of the resource it concerns. No HTTP here.</summary>
/// <param name="Kind">The reason.</param>
/// <param name="ResourceId">The ID of the resource the refusal concerns.</param>
/// <param name="Limit">The bound the request exceeded when the message needs it (a user limit), otherwise 0.</param>
internal readonly record struct StateFailure(StateFailureKind Kind, ulong ResourceId, int Limit = 0)
{
    public override string ToString() => Limit == 0 ? $"{Kind} ({ResourceId})" : $"{Kind} ({ResourceId}, limit {Limit})";
}

/// <summary>
/// Typed outcome of a state operation: a detached value or a business failure. A <c>default</c> instance is
/// neither: it is neither <see cref="IsSuccess"/> nor does it have a <see cref="Failure"/>, and reading its
/// value or failure throws, so it can never be mistaken for a real <c>UnknownUser(0)</c>.
/// </summary>
internal readonly struct StateResult<T>
{
    private enum Outcome : byte
    {
        Uninitialized = 0,
        Success,
        Failed,
    }

    private readonly T? _value;
    private readonly StateFailure _failure;
    private readonly Outcome _outcome;

    private StateResult(T value)
    {
        _value = value;
        _failure = default;
        _outcome = Outcome.Success;
    }

    private StateResult(StateFailure failure)
    {
        _value = default;
        _failure = failure;
        _outcome = Outcome.Failed;
    }

    internal bool IsSuccess => _outcome == Outcome.Success;

    /// <summary>Gets the value of a successful result.</summary>
    internal T Value => _outcome switch
    {
        Outcome.Success => _value!,
        Outcome.Failed => throw new InvalidOperationException($"The operation failed: {_failure}."),
        _ => throw new InvalidOperationException("The result was not initialized."),
    };

    /// <summary>Gets the failure of an unsuccessful result.</summary>
    internal StateFailure Failure => _outcome switch
    {
        Outcome.Failed => _failure,
        Outcome.Success => throw new InvalidOperationException("The operation succeeded."),
        _ => throw new InvalidOperationException("The result was not initialized."),
    };

    internal bool TryGetValue([MaybeNullWhen(false)] out T value)
    {
        value = _outcome == Outcome.Success ? _value : default;
        return IsSuccess;
    }

    /// <summary>Returns the value or throws <see cref="InvalidOperationException"/> (seed-style callers).</summary>
    internal T ValueOrThrow() => Value;

    public static implicit operator StateResult<T>(T value) => new(value);

    public static implicit operator StateResult<T>(StateFailure failure) => new(failure);
}
