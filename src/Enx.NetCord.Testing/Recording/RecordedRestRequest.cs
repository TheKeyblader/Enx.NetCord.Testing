using System.Collections.ObjectModel;
using System.Net;
using System.Text.Json;
using Enx.NetCord.Testing.Rest;

namespace Enx.NetCord.Testing;

/// <summary>Describes one completed attempt at the in-memory REST transport.</summary>
/// <remarks>
/// An instance is a detached copy: it holds no HTTP stream, no disposable <see cref="JsonDocument"/> and no reference to the
/// fake's state, and it never contains a credential. Request headers are not recorded, and the token segments of the path
/// and query are masked. The journal of a test is not a redacted export of its application content: bodies are kept as sent.
/// </remarks>
public sealed class RecordedRestRequest
{
    private RecordedRestRequest(
        string method,
        string path,
        string? routeTemplate,
        IReadOnlyDictionary<string, string> routeParameters,
        IReadOnlyList<KeyValuePair<string, string>> query,
        JsonElement? requestBody,
        HttpStatusCode? statusCode,
        JsonElement? responseBody)
    {
        Method = method;
        Path = path;
        RouteTemplate = routeTemplate;
        RouteParameters = routeParameters;
        Query = query;
        RequestBody = requestBody;
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }

    /// <summary>Gets the HTTP method, such as POST.</summary>
    public string Method { get; }

    /// <summary>Gets the request path without the API prefix or query.</summary>
    /// <remarks>Segments are decoded once (<c>%40me</c> is <c>@me</c>); token segments are masked.</remarks>
    public string Path { get; }

    /// <summary>Gets the matched Discord path template, or <see langword="null"/> if none was resolved.</summary>
    public string? RouteTemplate { get; }

    /// <summary>Gets decoded route parameter values by template parameter name; empty when no template was resolved.</summary>
    /// <remarks>The values of token parameters, such as <c>webhook_token</c>, are masked.</remarks>
    public IReadOnlyDictionary<string, string> RouteParameters { get; }

    /// <summary>Gets decoded query pairs in wire order, preserving repeated names.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Query { get; }

    /// <summary>Gets the detached JSON request or multipart payload, if available.</summary>
    /// <remarks>
    /// For <c>multipart/form-data</c> this is the <c>payload_json</c> part; file bytes are not kept. It is <see langword="null"/>
    /// when the request had no content, a non-JSON content, content that was unreadable, or when the request ended before its
    /// content was read (unknown or unimplemented route, method not allowed, rejected authentication). The JSON literal <c>null</c>
    /// is a <see cref="JsonElement"/> whose kind is <see cref="JsonValueKind.Null"/>.
    /// </remarks>
    public JsonElement? RequestBody { get; }

    /// <summary>Gets the HTTP response status, or <see langword="null"/> if no response was produced.</summary>
    /// <remarks>No response is produced for a local failure, a cancellation, or an unimplemented route in strict mode.</remarks>
    public HttpStatusCode? StatusCode { get; }

    /// <summary>Gets the detached JSON response, if available.</summary>
    public JsonElement? ResponseBody { get; }

    /// <summary>Projects a terminal attempt into a detached public record.</summary>
    /// <param name="attempt">The internal record.</param>
    /// <returns>A new record sharing nothing mutable with <paramref name="attempt"/>.</returns>
    internal static RecordedRestRequest From(RestAttempt attempt)
    {
        ArgumentNullException.ThrowIfNull(attempt);

        return new RecordedRestRequest(
            attempt.Method,
            attempt.Path,
            attempt.RouteTemplate,
            new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(attempt.RouteParameters, StringComparer.Ordinal)),
            Array.AsReadOnly(attempt.Query.ToArray()),
            ReadRequestBody(attempt),
            attempt.StatusCode,
            ParseDetached(attempt.ResponseBody));
    }

    private static JsonElement? ReadRequestBody(RestAttempt attempt)
    {
        if (attempt.RequestBody is not { Length: > 0 } bytes)
        {
            return null;
        }

        var mediaType = attempt.RequestContentType;
        if (string.Equals(mediaType, "multipart/form-data", StringComparison.OrdinalIgnoreCase))
        {
            return ParseDetached(ExtractPayloadJson(bytes));
        }

        return IsJsonMediaType(mediaType) ? ParseDetached(bytes) : null;
    }

    private static bool IsJsonMediaType(string? mediaType)
        => mediaType is null
            || mediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase)
            || mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase);

    private static JsonElement? ParseDetached(ReadOnlyMemory<byte> json)
    {
        if (json.IsEmpty)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);

            // Clone copies the element out of the pooled document, which is disposed right after.
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Finds the first <c>payload_json</c> part of a multipart body; the other parts are not kept, nor is any byte copied.</summary>
    /// <remarks>Reads with the shared <see cref="RestMultipart"/> parser, in its lenient mode: a malformed sibling part is ignored.</remarks>
    private static ReadOnlyMemory<byte> ExtractPayloadJson(byte[] body)
    {
        if (!RestMultipart.TryParse(body, lenient: true, out var parts))
        {
            return default;
        }

        foreach (var part in parts)
        {
            if (string.Equals(part.Name, RestMultipart.PayloadJsonName, StringComparison.Ordinal))
            {
                return part.Content;
            }
        }

        return default;
    }
}
