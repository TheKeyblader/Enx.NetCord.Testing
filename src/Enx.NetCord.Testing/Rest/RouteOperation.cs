namespace Enx.NetCord.Testing.Rest;

/// <summary>One REST operation known to the fake, whether or not a handler implements it.</summary>
/// <param name="Method">The upper-case HTTP method.</param>
/// <param name="Template">The path template without the API prefix, such as <c>/channels/{channel_id}</c>.</param>
/// <param name="OperationId">The Discord <c>operationId</c>, or <see langword="null"/> for a NetCord-only route.</param>
/// <param name="Security">The authentication alternatives of the operation.</param>
/// <param name="InSpecification"><see langword="false"/> for routes NetCord calls that the pinned specification does not document.</param>
internal sealed record RouteOperation(
    string Method,
    string Template,
    string? OperationId,
    RouteSecurity Security,
    bool InSpecification = true)
{
    /// <summary>Gets the table key, <c>METHOD /template</c>, shared by routing, parity and interception.</summary>
    public string Key => $"{Method} {Template}";
}
