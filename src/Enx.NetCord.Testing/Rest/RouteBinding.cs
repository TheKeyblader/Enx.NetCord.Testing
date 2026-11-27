namespace Enx.NetCord.Testing.Rest;

/// <summary>Handles one implemented REST operation after authentication, structural reading and interception.</summary>
/// <param name="request">The detached request, including decoded route parameters.</param>
/// <param name="cancellationToken">The token of the transport attempt.</param>
/// <returns>A business success or a <see cref="DiscordError"/>; handlers never build raw HTTP responses.</returns>
internal delegate ValueTask<RestResult> RestRouteHandler(FakeRestRequest request, CancellationToken cancellationToken);

/// <summary>Binds a handler, and its optional structural validator, to the exact <c>METHOD /template</c> key of one inventoried operation.</summary>
/// <param name="Key">The operation key, as in <see cref="RouteOperation.Key"/>.</param>
/// <param name="Handler">The handler.</param>
/// <param name="Validator">
/// The structural validator, run after the body is read and before an interception rule is taken
/// (see <see cref="RestRequestValidator"/>); <see langword="null"/> when the operation has nothing to validate.
/// </param>
internal sealed record RouteBinding(string Key, RestRouteHandler Handler, RestRequestValidator? Validator = null)
{
    /// <summary>Creates a binding from a synchronous handler.</summary>
    /// <param name="key">The operation key.</param>
    /// <param name="handler">The handler.</param>
    /// <param name="validator">The optional structural validator whose payload the handler reads with <see cref="FakeRestRequest.GetPayload{T}"/>.</param>
    /// <returns>The binding.</returns>
    public static RouteBinding Of(string key, Func<FakeRestRequest, RestResult> handler, RestRequestValidator? validator = null)
        => new(key, (request, _) => ValueTask.FromResult(handler(request)), validator);
}
