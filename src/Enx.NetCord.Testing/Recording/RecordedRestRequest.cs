using System.Collections.ObjectModel;
using System.Net;
using System.Text;
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
    private const string PayloadJsonName = "payload_json";

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
    /// when the request had no content, a non-JSON content, or content that was unreadable. The JSON literal <c>null</c>
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

    private static JsonElement? ParseDetached(byte[]? json)
    {
        if (json is not { Length: > 0 })
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

    /// <summary>Finds the <c>payload_json</c> part of a multipart body without keeping the other parts.</summary>
    /// <remarks>
    /// Latin-1 maps every byte to one char, so the body can be split as text and the part converted back
    /// to its exact original bytes. The delimiter is the first line of the body (<c>--boundary</c>).
    /// </remarks>
    private static byte[]? ExtractPayloadJson(byte[] body)
    {
        var text = Encoding.Latin1.GetString(body);
        var lineEnd = text.IndexOf("\r\n", StringComparison.Ordinal);
        if (!text.StartsWith("--", StringComparison.Ordinal) || lineEnd < 0)
        {
            return null;
        }

        var delimiter = text[..lineEnd];
        foreach (var rawPart in text.Split(delimiter))
        {
            var part = rawPart.StartsWith("\r\n", StringComparison.Ordinal) ? rawPart[2..] : rawPart;
            var headerEnd = part.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            if (headerEnd < 0 || !IsPayloadJsonPart(part[..headerEnd]))
            {
                continue;
            }

            var content = part[(headerEnd + 4)..];
            if (content.EndsWith("\r\n", StringComparison.Ordinal))
            {
                content = content[..^2];
            }

            return Encoding.Latin1.GetBytes(content);
        }

        return null;
    }

    private static bool IsPayloadJsonPart(string headers)
        => headers.Contains($"; name=\"{PayloadJsonName}\"", StringComparison.OrdinalIgnoreCase)
            || headers.Contains($"; name={PayloadJsonName}", StringComparison.OrdinalIgnoreCase);
}
