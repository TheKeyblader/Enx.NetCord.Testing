namespace Enx.NetCord.Testing.Rest;

/// <summary>Masks secrets before anything reaches the journal or an exception message.</summary>
internal static class RestRedaction
{
    /// <summary>The placeholder that replaces a secret value.</summary>
    internal const string Mask = "[redacted]";

    // Longest segment that is still considered publishable on an unresolved path. Discord webhook and
    // interaction tokens are far longer, bot tokens also contain dots.
    private const int MaxPublishableSegmentLength = 32;

    /// <summary>Returns whether a route parameter or query name carries a token (<c>webhook_token</c>, <c>interaction_token</c>, <c>token</c>).</summary>
    /// <param name="name">The name.</param>
    /// <returns><see langword="true"/> when the value must be masked.</returns>
    public static bool IsTokenName(string name) => name.EndsWith("token", StringComparison.OrdinalIgnoreCase);

    /// <summary>Masks route parameter values identified as tokens.</summary>
    /// <param name="parameters">The decoded parameters.</param>
    /// <returns>A masked copy.</returns>
    public static IReadOnlyDictionary<string, string> RedactParameters(IReadOnlyDictionary<string, string> parameters)
    {
        var result = new Dictionary<string, string>(parameters.Count, StringComparer.Ordinal);
        foreach (var pair in parameters)
        {
            result[pair.Key] = IsTokenName(pair.Key) ? Mask : pair.Value;
        }

        return result;
    }

    /// <summary>Masks query values whose name carries a token.</summary>
    /// <param name="query">The decoded pairs.</param>
    /// <returns>A masked copy.</returns>
    public static IReadOnlyList<KeyValuePair<string, string>> RedactQuery(IReadOnlyList<KeyValuePair<string, string>> query)
        => [.. query.Select(static p => IsTokenName(p.Key) ? new KeyValuePair<string, string>(p.Key, Mask) : p)];

    /// <summary>Builds the path of a resolved route, masking token parameter segments.</summary>
    /// <param name="template">The winning template.</param>
    /// <param name="segments">The decoded segments.</param>
    /// <returns>The masked path.</returns>
    public static string RedactResolvedPath(string template, IReadOnlyList<string> segments)
    {
        var templateSegments = template.Split('/')[1..];
        var parts = new string[segments.Count];
        for (var i = 0; i < parts.Length; i++)
        {
            var t = templateSegments[i];
            parts[i] = t.Length > 2 && t[0] == '{' && IsTokenName(t[1..^1]) ? Mask : segments[i];
        }

        return "/" + string.Join('/', parts);
    }

    /// <summary>Builds a path for an unresolved route: segments are not assumed publishable, so long or dotted ones are masked.</summary>
    /// <param name="segments">The decoded segments.</param>
    /// <returns>The conservatively masked path.</returns>
    public static string RedactUnresolvedPath(IReadOnlyList<string> segments)
        => "/" + string.Join('/', segments.Select(static s => s.Length > MaxPublishableSegmentLength || s.Contains('.') ? Mask : s));
}
