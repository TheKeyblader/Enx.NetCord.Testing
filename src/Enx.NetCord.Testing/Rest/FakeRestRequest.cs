using Enx.NetCord.Testing.State;

namespace Enx.NetCord.Testing.Rest;

/// <summary>A resolved, authorized request handed to a route handler. Values are detached from the HTTP stack.</summary>
internal sealed class FakeRestRequest
{
    private readonly FakeDiscordState? _state;

    internal FakeRestRequest(
        RouteOperation operation,
        IReadOnlyDictionary<string, string> routeParameters,
        IReadOnlyList<KeyValuePair<string, string>> query,
        RestRequestBody? body,
        RestCaller caller,
        TimeProvider timeProvider,
        FakeDiscordState? state = null,
        object? payload = null)
    {
        Operation = operation;
        RouteParameters = routeParameters;
        Query = query;
        Body = body;
        Caller = caller;
        TimeProvider = timeProvider;
        _state = state;
        Payload = payload;
    }

    /// <summary>Gets the matched operation.</summary>
    public RouteOperation Operation { get; }

    /// <summary>Gets the decoded route parameters by template name (not redacted: handlers need webhook tokens).</summary>
    public IReadOnlyDictionary<string, string> RouteParameters { get; }

    /// <summary>Gets the decoded query pairs in wire order, preserving repeated names.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Query { get; }

    /// <summary>Gets the content, or <see langword="null"/> when the request had none.</summary>
    public RestRequestBody? Body { get; }

    /// <summary>Gets the identified caller.</summary>
    public RestCaller Caller { get; }

    /// <summary>Gets the detached input produced by the operation's validator, or <see langword="null"/> when it has none.</summary>
    /// <remarks>Immutable: the pipeline attaches the payload by creating a new request with <see cref="WithPayload"/> once validation succeeds.</remarks>
    public object? Payload { get; }

    /// <summary>Gets the fake's clock.</summary>
    public TimeProvider TimeProvider { get; }

    /// <summary>
    /// Gets the fake's state. A handler asks it for one coherent operation (ADR 0005) and never keeps what it returns:
    /// the state hands out detached snapshots only.
    /// </summary>
    /// <exception cref="InvalidOperationException">The request was built without a state (a test double, never a real attempt).</exception>
    public FakeDiscordState State
        => _state ?? throw new InvalidOperationException("This request was created without a state.");

    /// <summary>Returns a copy of this request that carries the payload produced by the operation's validator.</summary>
    /// <param name="payload">The validated payload.</param>
    /// <returns>A new request; this instance is left unchanged.</returns>
    internal FakeRestRequest WithPayload(object? payload)
        => new(Operation, RouteParameters, Query, Body, Caller, TimeProvider, _state, payload);

    /// <summary>Returns the payload the validator parsed, so the handler never re-reads the body.</summary>
    /// <typeparam name="T">The payload type the operation's validator returns.</typeparam>
    /// <returns>The payload.</returns>
    /// <exception cref="InvalidOperationException">The operation has no validator payload of type <typeparamref name="T"/> (a binding bug).</exception>
    public T GetPayload<T>()
        => Payload is T payload
            ? payload
            : throw new InvalidOperationException(
                $"Operation '{Operation.Key}' has no validated payload of type {typeof(T).Name}; check that its validator returns one.");

    /// <summary>Returns the first value of a query parameter, or <see langword="null"/>.</summary>
    /// <param name="name">The parameter name.</param>
    /// <returns>The value, if present.</returns>
    public string? GetQuery(string name)
    {
        foreach (var pair in Query)
        {
            if (string.Equals(pair.Key, name, StringComparison.Ordinal))
            {
                return pair.Value;
            }
        }

        return null;
    }
}
