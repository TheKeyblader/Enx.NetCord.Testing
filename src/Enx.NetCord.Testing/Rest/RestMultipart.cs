using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Enx.NetCord.Testing.Rest;

/// <summary>One part of a <c>multipart/form-data</c> request body.</summary>
/// <param name="Name">The form field name, such as <c>payload_json</c> or <c>files[0]</c>.</param>
/// <param name="FileName">The file name of a file part, or <see langword="null"/> for a plain field.</param>
/// <param name="ContentType">The <c>Content-Type</c> of the part, if any.</param>
/// <param name="Content">The exact bytes of the part.</param>
internal sealed record MultipartPart(string Name, string? FileName, string? ContentType, byte[] Content)
{
    /// <summary>Gets the content decoded as UTF-8 (the encoding of <c>payload_json</c>).</summary>
    public string Text => Encoding.UTF8.GetString(Content);
}

/// <summary>
/// A minimal reader for the <c>multipart/form-data</c> bodies NetCord builds (<c>payload_json</c> and <c>files[n]</c>, see
/// <c>IMessageProperties.Serialize</c>). It is not a general MIME parser: parts are delimited by the first line of the body, as the
/// journal does, and every part needs a <c>Content-Disposition</c> with a <c>name</c>.
/// </summary>
internal static class RestMultipart
{
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
    /// <remarks>Latin-1 maps every byte to one char, so the body is split as text and each part converted back to its exact bytes.</remarks>
    /// <param name="body">The body.</param>
    /// <param name="parts">The parts, in order, when the body is well formed.</param>
    /// <returns><see langword="false"/> when the body is not a multipart body this reader understands.</returns>
    public static bool TryParse(RestRequestBody body, [NotNullWhen(true)] out IReadOnlyList<MultipartPart>? parts)
    {
        ArgumentNullException.ThrowIfNull(body);
        parts = null;
        if (!IsMultipart(body))
        {
            return false;
        }

        var text = Encoding.Latin1.GetString(body.Content.Span);
        var lineEnd = text.IndexOf("\r\n", StringComparison.Ordinal);
        if (!text.StartsWith("--", StringComparison.Ordinal) || lineEnd < 0)
        {
            return false;
        }

        var delimiter = text[..lineEnd];
        var result = new List<MultipartPart>();
        foreach (var segment in text.Split(delimiter).Skip(1))
        {
            if (segment.StartsWith("--", StringComparison.Ordinal))
            {
                break; // closing delimiter
            }

            var part = segment.StartsWith("\r\n", StringComparison.Ordinal) ? segment[2..] : segment;
            var headerEnd = part.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            if (headerEnd < 0)
            {
                return false;
            }

            var content = part[(headerEnd + 4)..];
            if (content.EndsWith("\r\n", StringComparison.Ordinal))
            {
                content = content[..^2];
            }

            if (!TryReadHeaders(part[..headerEnd], out var name, out var fileName, out var contentType))
            {
                return false;
            }

            result.Add(new MultipartPart(name, fileName, contentType, Encoding.Latin1.GetBytes(content)));
        }

        parts = result;
        return true;
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
