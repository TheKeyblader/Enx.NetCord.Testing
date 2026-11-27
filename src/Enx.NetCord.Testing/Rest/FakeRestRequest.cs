namespace Enx.NetCord.Testing.Rest;

/// <summary>A resolved, authorized request handed to a route handler. Values are detached from the HTTP stack.</summary>
internal sealed class FakeRestRequest
{
    internal FakeRestRequest(
        RouteOperation operation,
        IReadOnlyDictionary<string, string> routeParameters,
        IReadOnlyList<KeyValuePair<string, string>> query,
        RestRequestBody? body,
        RestCaller caller,
        TimeProvider timeProvider)
    {
        Operation = operation;
        RouteParameters = routeParameters;
        Query = query;
        Body = body;
        Caller = caller;
        TimeProvider = timeProvider;
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

    /// <summary>Gets the fake's clock.</summary>
    public TimeProvider TimeProvider { get; }

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
