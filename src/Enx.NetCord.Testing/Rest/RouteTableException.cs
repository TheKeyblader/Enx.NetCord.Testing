namespace Enx.NetCord.Testing.Rest;

/// <summary>Raised when the route table check fails (duplicate, ambiguity or binding without an inventory entry).</summary>
internal sealed class RouteTableException : InvalidOperationException
{
    /// <summary>Initializes a new instance of the <see cref="RouteTableException"/> class.</summary>
    /// <param name="problems">The problems found by the check.</param>
    public RouteTableException(IReadOnlyList<string> problems)
        : base("The REST route table is invalid: " + string.Join("; ", problems))
    {
        Problems = problems;
    }

    /// <summary>Gets the individual problems.</summary>
    public IReadOnlyList<string> Problems { get; }
}
