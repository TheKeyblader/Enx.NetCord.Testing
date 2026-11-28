using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Enx.NetCord.Testing.Rest;

/// <summary>The request content, read exactly once by the pipeline before interception.</summary>
internal sealed class RestRequestBody(ReadOnlyMemory<byte> content, string? contentType)
{
    /// <summary>Gets the raw bytes (possibly empty).</summary>
    public ReadOnlyMemory<byte> Content { get; } = content;

    /// <summary>Gets the media type of the content, such as <c>application/json</c>, if any.</summary>
    public string? ContentType { get; } = contentType;

    /// <summary>Parses the content as one JSON document.</summary>
    /// <param name="document">The document when parsing succeeds; the caller disposes it.</param>
    /// <param name="error">The Discord 50109 error when the content is not valid JSON.</param>
    /// <returns><see langword="true"/> when the content is valid JSON.</returns>
    public bool TryParseJson([NotNullWhen(true)] out JsonDocument? document, [NotNullWhen(false)] out DiscordError? error)
    {
        try
        {
            document = JsonDocument.Parse(Content);
            error = null;
            return true;
        }
        catch (JsonException)
        {
            document = null;
            error = DiscordError.InvalidJson();
            return false;
        }
    }
}
