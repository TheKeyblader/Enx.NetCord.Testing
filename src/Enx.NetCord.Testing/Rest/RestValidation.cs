using System.Diagnostics.CodeAnalysis;

namespace Enx.NetCord.Testing.Rest;

/// <summary>
/// Validates the structure of a request (route parameters, query, body shape) before interception and before
/// the handler, without reading or changing any state (ADR 0005, step 3).
/// </summary>
/// <remarks>
/// A M1-REST family writes one validator per operation and binds it with <see cref="RouteBinding.Of(string, Func{FakeRestRequest, RestResult}, RestRequestValidator?)"/>:
/// parse <see cref="FakeRestRequest.Body"/> once (<see cref="RestRequestBody.TryParseJson"/> gives <c>50109</c>),
/// check types, lengths and required fields, collect field errors in a <see cref="FormErrors"/> (<c>50035</c>),
/// then return <see cref="RestValidation.Valid(object?)"/> with a <b>detached</b> input record. The handler reads it
/// with <see cref="FakeRestRequest.GetPayload{T}"/>. Dispose any <see cref="System.Text.Json.JsonDocument"/> inside the
/// validator and never keep a <see cref="System.Text.Json.JsonElement"/> in the payload. Checks that depend on state
/// (existence, access, permissions, limits) belong to the handler, under the state lock.
/// </remarks>
/// <param name="request">The resolved, authorized request with its body already read.</param>
/// <returns>A valid result carrying the parsed payload, or a <see cref="DiscordError"/>.</returns>
internal delegate RestValidation RestRequestValidator(FakeRestRequest request);

/// <summary>The outcome of a <see cref="RestRequestValidator"/>.</summary>
internal sealed class RestValidation
{
    private RestValidation(object? payload, DiscordError? error)
    {
        Payload = payload;
        Error = error;
    }

    /// <summary>Gets the parsed payload of a valid request.</summary>
    public object? Payload { get; }

    /// <summary>Gets the error of an invalid request.</summary>
    public DiscordError? Error { get; }

    /// <summary>Gets a value indicating whether validation failed.</summary>
    [MemberNotNullWhen(true, nameof(Error))]
    public bool IsInvalid => Error is not null;

    /// <summary>Creates a successful result.</summary>
    /// <param name="payload">A detached, parsed input for the handler, or <see langword="null"/>.</param>
    /// <returns>The result.</returns>
    public static RestValidation Valid(object? payload = null) => new(payload, null);

    /// <summary>Creates a failed result.</summary>
    /// <param name="error">The Discord error to return; no effect is consumed and no state is touched.</param>
    /// <returns>The result.</returns>
    public static RestValidation Invalid(DiscordError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new(null, error);
    }

    /// <summary>Converts an error to a failed result.</summary>
    /// <param name="error">The error.</param>
    public static implicit operator RestValidation(DiscordError error) => Invalid(error);
}
