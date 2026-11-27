namespace Enx.NetCord.Testing.Rest;

/// <summary>The outcome category of resolving a request against the route table.</summary>
internal enum RouteResolutionKind
{
    /// <summary>No template matches the path.</summary>
    NotFound,

    /// <summary>The path is known but the method is absent from the inventory.</summary>
    MethodNotAllowed,

    /// <summary>An inventoried operation matches.</summary>
    Found,
}

/// <summary>The result of <see cref="RouteTable.Resolve"/>.</summary>
internal sealed class RouteResolution
{
    private static readonly IReadOnlyDictionary<string, string> NoParameters = new Dictionary<string, string>();

    private RouteResolution(
        RouteResolutionKind kind,
        RouteEntry? entry,
        string? template,
        IReadOnlyDictionary<string, string> parameters,
        IReadOnlyList<string> allowedMethods)
    {
        Kind = kind;
        Entry = entry;
        Template = template;
        Parameters = parameters;
        AllowedMethods = allowedMethods;
    }

    /// <summary>Gets the outcome category.</summary>
    public RouteResolutionKind Kind { get; }

    /// <summary>Gets the matched operation, or <see langword="null"/> unless <see cref="Kind"/> is <see cref="RouteResolutionKind.Found"/>.</summary>
    public RouteEntry? Entry { get; }

    /// <summary>Gets the winning template when the path is known, even without a matching method.</summary>
    public string? Template { get; }

    /// <summary>Gets the decoded route parameters of the winning template.</summary>
    public IReadOnlyDictionary<string, string> Parameters { get; }

    /// <summary>Gets the inventoried methods of the winning template, sorted ordinally.</summary>
    public IReadOnlyList<string> AllowedMethods { get; }

    internal static RouteResolution NotFound() => new(RouteResolutionKind.NotFound, null, null, NoParameters, []);

    internal static RouteResolution MethodNotAllowed(string template, IReadOnlyDictionary<string, string> parameters, IReadOnlyList<string> allowed)
        => new(RouteResolutionKind.MethodNotAllowed, null, template, parameters, allowed);

    internal static RouteResolution Found(RouteEntry entry, IReadOnlyDictionary<string, string> parameters, IReadOnlyList<string> allowed)
        => new(RouteResolutionKind.Found, entry, entry.Operation.Template, parameters, allowed);
}
