namespace Enx.NetCord.Testing.Rest;

/// <summary>A request URI split into API-relative decoded segments and query pairs.</summary>
/// <param name="Segments">The decoded path segments after <c>/api/v10</c>.</param>
/// <param name="Query">The decoded query pairs in wire order.</param>
internal sealed record ParsedRequestTarget(IReadOnlyList<string> Segments, IReadOnlyList<KeyValuePair<string, string>> Query);

/// <summary>Parses the absolute URI NetCord builds (<c>https://{Hostname}/api/v10{route}{query}</c>) without any normalization.</summary>
internal static class RestRequestParser
{
    private const string ApiPrefix = "/api/v10";

    /// <summary>Splits the target of a request.</summary>
    /// <param name="uri">The request URI.</param>
    /// <param name="hostname">The sentinel host this fake serves.</param>
    /// <returns>The parsed target.</returns>
    /// <exception cref="InvalidOperationException">The URI targets another host or API version: a local configuration error, never a network call.</exception>
    public static ParsedRequestTarget Parse(Uri? uri, string hostname)
    {
        if (uri is null || !uri.IsAbsoluteUri)
        {
            throw new InvalidOperationException("The fake REST transport needs an absolute request URI built by a NetCord RestClient.");
        }

        if (!string.Equals(uri.Host, hostname, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"The fake REST transport only serves host '{hostname}', not '{uri.Host}'. It never falls back to the network.");
        }

        var text = uri.OriginalString;
        var fragment = text.IndexOf('#', StringComparison.Ordinal);
        if (fragment >= 0)
        {
            text = text[..fragment];
        }

        var authorityStart = text.IndexOf("://", StringComparison.Ordinal) + 3;
        var pathStart = text.IndexOf('/', authorityStart);
        var rest = pathStart < 0 ? string.Empty : text[pathStart..];

        string path;
        string rawQuery;
        var queryStart = rest.IndexOf('?', StringComparison.Ordinal);
        if (queryStart >= 0)
        {
            path = rest[..queryStart];
            rawQuery = rest[(queryStart + 1)..];
        }
        else
        {
            path = rest;
            rawQuery = string.Empty;
        }

        if (!path.StartsWith(ApiPrefix, StringComparison.Ordinal)
            || (path.Length > ApiPrefix.Length && path[ApiPrefix.Length] != '/'))
        {
            throw new InvalidOperationException($"The fake REST transport only serves the '{ApiPrefix}' API prefix.");
        }

        var relative = path[ApiPrefix.Length..];
        IReadOnlyList<string> segments = relative.Length == 0
            ? []
            : [.. relative[1..].Split('/').Select(static s => Uri.UnescapeDataString(s))];

        return new ParsedRequestTarget(segments, ParseQuery(rawQuery));
    }

    private static List<KeyValuePair<string, string>> ParseQuery(string rawQuery)
    {
        var pairs = new List<KeyValuePair<string, string>>();
        if (rawQuery.Length == 0)
        {
            return pairs;
        }

        foreach (var part in rawQuery.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var equals = part.IndexOf('=', StringComparison.Ordinal);
            var name = equals < 0 ? part : part[..equals];
            var value = equals < 0 ? string.Empty : part[(equals + 1)..];
            pairs.Add(new(Decode(name), Decode(value)));
        }

        return pairs;
    }

    private static string Decode(string value) => Uri.UnescapeDataString(value.Replace('+', ' '));
}
