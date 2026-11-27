using System.Net;

namespace Enx.NetCord.Testing.Rest;

/// <summary>How an attempt at the transport ended.</summary>
internal enum RestAttemptOutcome
{
    /// <summary>A local fault: bad configuration, handler bug or unreadable content. No HTTP response was produced.</summary>
    Faulted,

    /// <summary>An HTTP response was produced.</summary>
    Responded,

    /// <summary>No handler implements the operation: a lenient 404 was returned, or strict mode threw.</summary>
    Unimplemented,

    /// <summary>The caller's <see cref="CancellationToken"/> fired before a response was produced.</summary>
    Canceled,
}

/// <summary>
/// The internal, detached terminal record of one <c>SendAsync</c> (retries included). It is the producer contract
/// the public <c>Recorded.Requests</c> projection (M1-04) is built from. It never holds an <c>HttpContent</c>,
/// a live <c>JsonDocument</c> or a reference to the state, and it never contains credentials.
/// </summary>
internal sealed class RestAttempt
{
    /// <summary>Gets the upper-case HTTP method.</summary>
    public string Method { get; internal set; } = string.Empty;

    /// <summary>Gets the decoded path without prefix or query, with token segments masked.</summary>
    public string Path { get; internal set; } = "/";

    /// <summary>Gets the matched template, or <see langword="null"/> when none was resolved.</summary>
    public string? RouteTemplate { get; internal set; }

    /// <summary>Gets the decoded route parameters, with token values masked; empty when no template was resolved.</summary>
    public IReadOnlyDictionary<string, string> RouteParameters { get; internal set; } = new Dictionary<string, string>();

    /// <summary>Gets the decoded query pairs in wire order, with token-like values masked.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Query { get; internal set; } = [];

    /// <summary>Gets a value indicating whether the pipeline read the request content (distinguishes "not read" from "empty").</summary>
    public bool RequestBodyRead { get; internal set; }

    /// <summary>Gets the request content bytes when read; empty when the request had none.</summary>
    public byte[]? RequestBody { get; internal set; }

    /// <summary>Gets the request media type, if any.</summary>
    public string? RequestContentType { get; internal set; }

    /// <summary>Gets the terminal outcome.</summary>
    public RestAttemptOutcome Outcome { get; internal set; } = RestAttemptOutcome.Faulted;

    /// <summary>Gets the effect applied to this attempt, if any (internal information, not public API).</summary>
    public RestEffectKind? Effect { get; internal set; }

    /// <summary>Gets the HTTP status, or <see langword="null"/> when no response was produced.</summary>
    public HttpStatusCode? StatusCode { get; internal set; }

    /// <summary>Gets the response headers (internal information, not public API).</summary>
    public IReadOnlyList<KeyValuePair<string, string>> ResponseHeaders { get; internal set; } = [];

    /// <summary>Gets the response JSON bytes, if any.</summary>
    public byte[]? ResponseBody { get; internal set; }

    /// <summary>Gets the message of the local fault, if any.</summary>
    public string? FaultMessage { get; internal set; }
}

/// <summary>Receives one terminal <see cref="RestAttempt"/> per transport call, before the result is returned or thrown.</summary>
internal interface IRestAttemptSink
{
    /// <summary>Records a finished attempt.</summary>
    /// <param name="attempt">The detached record.</param>
    void Record(RestAttempt attempt);
}

/// <summary>A sink that discards attempts.</summary>
internal sealed class NullRestAttemptSink : IRestAttemptSink
{
    /// <summary>Gets the shared instance.</summary>
    public static NullRestAttemptSink Instance { get; } = new();

    /// <inheritdoc />
    public void Record(RestAttempt attempt)
    {
    }
}
