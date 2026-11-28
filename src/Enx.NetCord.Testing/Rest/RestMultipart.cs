using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Enx.NetCord.Testing.Rest;

/// <summary>One part of a <c>multipart/form-data</c> request body.</summary>
/// <param name="Name">The form field name, such as <c>payload_json</c> or <c>files[0]</c>.</param>
/// <param name="FileName">The file name of a file part, or <see langword="null"/> for a plain field.</param>
/// <param name="ContentType">The <c>Content-Type</c> of the part, if any.</param>
/// <param name="Content">The exact bytes of the part: a slice of the parsed body, not a copy.</param>
internal sealed record MultipartPart(string Name, string? FileName, string? ContentType, ReadOnlyMemory<byte> Content)
{
    /// <summary>Gets the size in bytes of the part content.</summary>
    public int Length => Content.Length;

    /// <summary>Gets the content decoded as UTF-8 (the encoding of <c>payload_json</c>); do not use it on a file part.</summary>
    public string Text => Encoding.UTF8.GetString(Content.Span);
}

/// <summary>
/// The single reader for the <c>multipart/form-data</c> bodies NetCord builds (<c>payload_json</c> and <c>files[n]</c>, see
/// <c>IMessageProperties.Serialize</c>), shared by the routes and the journal. It is not a general MIME parser: parts are
/// delimited by the first line of the body (<c>--boundary</c>, whatever the <c>boundary</c> parameter of the content type, quoted
/// or not), and every part needs a <c>Content-Disposition</c> with a <c>name</c>.
/// </summary>
/// <remarks>
/// The body is scanned as bytes and never copied: a part's <see cref="MultipartPart.Content"/> is a slice of the body, so a file
/// costs nothing but its size. A delimiter is only recognized at the start of a line and followed by CRLF or <c>--</c>, so file
/// bytes that merely resemble the boundary stay in their part.
/// </remarks>
internal static class RestMultipart
{
    /// <summary>The form field of a message request that holds its JSON.</summary>
    public const string PayloadJsonName = "payload_json";

    private const string MediaType = "multipart/form-data";

    /// <summary>Returns whether the body declares a <c>multipart/form-data</c> content type.</summary>
    /// <param name="body">The body.</param>
    /// <returns><see langword="true"/> for a multipart body.</returns>
    public static bool IsMultipart(RestRequestBody body)
    {
        ArgumentNullException.ThrowIfNull(body);
        return string.Equals(body.ContentType, MediaType, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Splits a multipart body into its parts.</summary>
    /// <param name="body">The body.</param>
    /// <param name="parts">The parts, in order, when the body is well formed.</param>
    /// <returns><see langword="false"/> when the body is not a multipart body this reader understands.</returns>
    public static bool TryParse(RestRequestBody body, [NotNullWhen(true)] out IReadOnlyList<MultipartPart>? parts)
    {
        ArgumentNullException.ThrowIfNull(body);
        parts = null;
        return IsMultipart(body) && TryParse(body.Content, lenient: false, out parts);
    }

    /// <summary>Splits the bytes of a multipart body into its parts, whatever its declared content type.</summary>
    /// <param name="content">The body bytes; the parts keep slices of them.</param>
    /// <param name="lenient">
    /// When <see langword="true"/>, a part without a header block or without a <c>name</c> is skipped instead of making the whole
    /// body unreadable (the journal looks for one part and ignores the rest).
    /// </param>
    /// <param name="parts">The parts, in order, when the body starts with a delimiter line.</param>
    /// <returns><see langword="false"/> when the body is not a multipart body this reader understands.</returns>
    public static bool TryParse(ReadOnlyMemory<byte> content, bool lenient, [NotNullWhen(true)] out IReadOnlyList<MultipartPart>? parts)
    {
        parts = null;
        var span = content.Span;
        var lineEnd = span.IndexOf("\r\n"u8);
        if (lineEnd < 0 || !span.StartsWith("--"u8))
        {
            return false;
        }

        // CRLF + the first line: the only form in which a delimiter, not a part of the content, can appear after the first one.
        var marker = new byte[lineEnd + 2];
        "\r\n"u8.CopyTo(marker);
        span[..lineEnd].CopyTo(marker.AsSpan(2));

        var result = new List<MultipartPart>();
        var position = lineEnd + 2;
        while (position <= span.Length)
        {
            var end = FindDelimiter(span, position, marker, out var closing);
            if (end < 0)
            {
                end = span.Length;
            }

            var part = span[position..end];
            int headerEnd;
            var contentStart = 0;
            if (part.StartsWith("\r\n"u8))
            {
                headerEnd = 0; // no header at all
                contentStart = 2;
            }
            else
            {
                headerEnd = part.IndexOf("\r\n\r\n"u8);
                contentStart = headerEnd + 4;
            }

            if (headerEnd < 0)
            {
                if (!lenient)
                {
                    return false;
                }
            }
            else
            {
                var contentEnd = part.Length;
                if (end == span.Length && part[contentStart..].EndsWith("\r\n"u8))
                {
                    contentEnd -= 2; // no closing delimiter: the body's own final line break
                }

                if (TryReadHeaders(Encoding.Latin1.GetString(part[..headerEnd]), out var name, out var fileName, out var contentType))
                {
                    result.Add(new MultipartPart(name, fileName, contentType, content.Slice(position + contentStart, contentEnd - contentStart)));
                }
                else if (!lenient)
                {
                    return false;
                }
            }

            if (closing || end == span.Length)
            {
                break;
            }

            position = end + marker.Length + 2; // the delimiter line and its CRLF
        }

        parts = result;
        return true;
    }

    // Returns the index of the CRLF that precedes the next delimiter line at or after `from`, or -1. A candidate only counts when
    // followed by CRLF (next part) or "--" (closing delimiter) or the end of the body.
    private static int FindDelimiter(ReadOnlySpan<byte> span, int from, ReadOnlySpan<byte> marker, out bool closing)
    {
        closing = false;
        var offset = from;
        while (offset <= span.Length - marker.Length)
        {
            var found = span[offset..].IndexOf(marker);
            if (found < 0)
            {
                return -1;
            }

            var index = offset + found;
            var after = span[(index + marker.Length)..];
            if (after.StartsWith("\r\n"u8))
            {
                return index;
            }

            if (after.IsEmpty || after.StartsWith("--"u8))
            {
                closing = true;
                return index;
            }

            offset = index + 1;
        }

        return -1;
    }

    private static bool TryReadHeaders(string headers, [NotNullWhen(true)] out string? name, out string? fileName, out string? contentType)
    {
        name = null;
        fileName = null;
        contentType = null;
        string? extendedFileName = null;

        foreach (var line in headers.Split("\r\n"))
        {
            var colon = line.IndexOf(':', StringComparison.Ordinal);
            if (colon < 0)
            {
                continue;
            }

            var header = line[..colon].Trim();
            var value = line[(colon + 1)..].Trim();
            if (header.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
            {
                contentType = value;
            }
            else if (header.Equals("Content-Disposition", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var (key, parameter) in ReadParameters(value))
                {
                    if (key.Equals("name", StringComparison.OrdinalIgnoreCase))
                    {
                        name = ReadUtf8(parameter);
                    }
                    else if (key.Equals("filename", StringComparison.OrdinalIgnoreCase))
                    {
                        fileName = DecodeEncodedWord(ReadUtf8(parameter));
                    }
                    else if (key.Equals("filename*", StringComparison.OrdinalIgnoreCase))
                    {
                        extendedFileName = DecodeExtended(parameter);
                    }
                }
            }
        }

        fileName = extendedFileName ?? fileName;
        return name is not null;
    }

    /// <summary>Reads <c>; key=value</c> parameters, honoring quotes (a quoted value may hold a <c>;</c>).</summary>
    private static IEnumerable<(string Key, string Value)> ReadParameters(string header)
    {
        var parameters = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        foreach (var c in header)
        {
            if (c == '"')
            {
                quoted = !quoted;
            }

            if (c == ';' && !quoted)
            {
                parameters.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        parameters.Add(current.ToString());

        // The first element is the disposition type (form-data).
        foreach (var parameter in parameters.Skip(1))
        {
            var equals = parameter.IndexOf('=', StringComparison.Ordinal);
            if (equals < 0)
            {
                continue;
            }

            var value = parameter[(equals + 1)..].Trim();
            if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
            {
                value = value[1..^1].Replace("\\\"", "\"", StringComparison.Ordinal);
            }

            yield return (parameter[..equals].Trim(), value);
        }
    }

    // Header text read as Latin-1 holds the UTF-8 bytes of a raw (unencoded) value one char per byte.
    private static string ReadUtf8(string latin1) => Encoding.UTF8.GetString(Encoding.Latin1.GetBytes(latin1));

    // RFC 2047 encoded word, as .NET writes a non-ASCII filename: =?utf-8?B?...?=
    private static string DecodeEncodedWord(string value)
    {
        const string prefix = "=?utf-8?B?";
        if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && value.EndsWith("?=", StringComparison.Ordinal))
        {
            try
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(value[prefix.Length..^2]));
            }
            catch (FormatException)
            {
                return value;
            }
        }

        return value;
    }

    // RFC 5987: charset'language'percent-encoded
    private static string? DecodeExtended(string value)
    {
        var first = value.IndexOf('\'', StringComparison.Ordinal);
        var second = first < 0 ? -1 : value.IndexOf('\'', first + 1);
        return second < 0 ? null : Uri.UnescapeDataString(value[(second + 1)..]);
    }
}
