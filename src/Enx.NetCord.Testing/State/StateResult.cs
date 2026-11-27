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
}

/// <summary>A refused operation: the reason and the ID of the resource it concerns. No HTTP here.</summary>
internal readonly record struct StateFailure(StateFailureKind Kind, ulong ResourceId)
{
    public override string ToString() => $"{Kind} ({ResourceId})";
}

/// <summary>Typed outcome of a state operation: a detached value or a business failure.</summary>
internal readonly struct StateResult<T>
{
    private readonly T? _value;
    private readonly StateFailure _failure;

    private StateResult(T value)
    {
        _value = value;
        _failure = default;
        IsSuccess = true;
    }

    private StateResult(StateFailure failure)
    {
        _value = default;
        _failure = failure;
        IsSuccess = false;
    }

    internal bool IsSuccess { get; }

    /// <summary>Gets the value of a successful result.</summary>
    internal T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"The operation failed: {_failure}.");

    /// <summary>Gets the failure of an unsuccessful result.</summary>
    internal StateFailure Failure => !IsSuccess
        ? _failure
        : throw new InvalidOperationException("The operation succeeded.");

    internal bool TryGetValue([System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out T value)
    {
        value = _value;
        return IsSuccess;
    }

    /// <summary>Returns the value or throws <see cref="InvalidOperationException"/> (seed-style callers).</summary>
    internal T ValueOrThrow() => Value;

    public static implicit operator StateResult<T>(T value) => new(value);

    public static implicit operator StateResult<T>(StateFailure failure) => new(failure);
}
