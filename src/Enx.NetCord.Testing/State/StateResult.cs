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

    /// <summary>A guild channel cannot become the requested type (the fake models only guild text channels): 400 <c>50035</c> on <c>type</c>.</summary>
    InvalidChannelType,

    /// <summary>The requested parent category does not exist (the fake models no category): 400 <c>50035</c> on <c>parent_id</c>.</summary>
    InvalidChannelParent,

    /// <summary>A guild channel name must not be empty: 400 <c>50035</c> on <c>name</c> (a DM accepts an empty one).</summary>
    InvalidChannelName,
}

/// <summary>A refused operation: the reason and the ID of the resource it concerns. No HTTP here.</summary>
internal readonly record struct StateFailure(StateFailureKind Kind, ulong ResourceId)
{
    public override string ToString() => $"{Kind} ({ResourceId})";
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
