using System.Globalization;
using Enx.NetCord.Testing.Rest;

namespace Enx.NetCord.Testing.Assertions;

/// <summary>
/// A route pattern of an assertion: <c>METHOD /path</c> where each parameter segment is its placeholder, such as
/// <c>{channel_id}</c>, or a concrete decoded value (ADR 0012, section 3).
/// </summary>
internal sealed class RoutePattern
{
    private const string RouteParameterName = "route";

    private readonly (string Name, string Value)[] _constraints;

    private RoutePattern(string text, string method, string template, (string Name, string Value)[] constraints)
    {
        Text = text;
        Method = method;
        Template = template;
        _constraints = constraints;
    }

    /// <summary>Gets the pattern as the test wrote it.</summary>
    public string Text { get; }

    /// <summary>Gets the HTTP method of the resolved operation.</summary>
    public string Method { get; }

    /// <summary>Gets the template of the resolved operation, such as <c>/channels/{channel_id}/messages</c>.</summary>
    public string Template { get; }

    /// <summary>Resolves a pattern against a route table and checks the shape of its concrete values.</summary>
    /// <param name="route">The pattern.</param>
    /// <param name="table">The route table; only its inventory is read.</param>
    /// <returns>The resolved pattern.</returns>
    /// <exception cref="ArgumentException">The pattern is malformed, unknown, or holds a value that cannot match.</exception>
    public static RoutePattern Parse(string route, RouteTable table)
    {
        var separator = route.IndexOf(' ', StringComparison.Ordinal);
        if (separator <= 0 || !IsUpperCaseMethod(route.AsSpan(0, separator)) || route.Length == separator + 1 || route[separator + 1] != '/')
        {
            throw Refuse(route, "expected the form 'METHOD /path' with an upper-case method and a single space");
        }

        var method = route[..separator];
        var path = route[(separator + 1)..];
        if (HasApiPrefix(path))
        {
            throw Refuse(route, "the path must not start with the API prefix (/api/v{N}); write the path of the route template");
        }

        if (path.AsSpan().ContainsAny('?', '#'))
        {
            throw Refuse(route, "the pattern must not contain a query string or a fragment");
        }

        var segments = path[1..].Split('/');
        foreach (var segment in segments)
        {
            if (segment.Length == 0)
            {
                throw Refuse(route, "the path must not contain an empty segment");
            }

            if (HasPercentEncoding(segment))
            {
                throw Refuse(route, $"segment '{segment}' is percent-encoded, but a pattern is not decoded; write the decoded value (such as '@me' or an emoji)");
            }

            if (string.Equals(segment, RestRedaction.Mask, StringComparison.Ordinal))
            {
                throw Refuse(route, $"'{RestRedaction.Mask}' is the mask of the journal, not a value; write the placeholder of the parameter");
            }
        }

        var resolution = table.Resolve(method, segments);
        switch (resolution.Kind)
        {
            case RouteResolutionKind.NotFound:
                throw Refuse(route, "no known operation matches this path");
            case RouteResolutionKind.MethodNotAllowed:
                throw Refuse(route, $"no {method} operation exists for {resolution.Template}; the inventory has: {string.Join(", ", resolution.AllowedMethods)}");
        }

        var template = resolution.Template!;
        var templateSegments = template[1..].Split('/');
        var constraints = new List<(string Name, string Value)>();
        for (var i = 0; i < templateSegments.Length; i++)
        {
            var templateSegment = templateSegments[i];
            if (templateSegment.Length < 3 || templateSegment[0] != '{')
            {
                continue;
            }

            var name = templateSegment[1..^1];
            var value = segments[i];
            if (string.Equals(value, templateSegment, StringComparison.Ordinal))
            {
                continue;
            }

            CheckValue(route, name, value);
            constraints.Add((name, value));
        }

        return new RoutePattern(route, method, template, [.. constraints]);
    }

    /// <summary>Returns whether a recorded attempt is on the resolved operation and carries every concrete value.</summary>
    /// <param name="request">The attempt.</param>
    /// <returns><see langword="true"/> when the attempt matches.</returns>
    public bool Matches(RecordedRestRequest request)
    {
        if (!IsSameOperation(request))
        {
            return false;
        }

        foreach (var (name, value) in _constraints)
        {
            if (!request.RouteParameters.TryGetValue(name, out var actual) || !string.Equals(actual, value, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns whether a recorded attempt is on the resolved operation, whatever its parameter values.</summary>
    /// <param name="request">The attempt.</param>
    /// <returns><see langword="true"/> for the same method and template.</returns>
    public bool IsSameOperation(RecordedRestRequest request)
        => string.Equals(request.Method, Method, StringComparison.Ordinal)
            && string.Equals(request.RouteTemplate, Template, StringComparison.Ordinal);

    private static void CheckValue(string route, string name, string value)
    {
        if (value.AsSpan().ContainsAny('{', '}'))
        {
            throw Refuse(
                route,
                $"'{value}' is not the placeholder '{{{name}}}' expected at this position; write '{{{name}}}' or a value (an interpolated pattern needs the '$' prefix)");
        }

        if (RestRedaction.IsTokenName(name))
        {
            throw Refuse(route, $"'{name}' is masked in the journal, so no value can match it; write the placeholder '{{{name}}}'");
        }

        if (RouteInventory.IsSnowflakeParameter(name) && !IsCanonicalSnowflake(value))
        {
            throw Refuse(
                route,
                $"'{name}' is a Snowflake: expected the canonical decimal form of a ulong, got '{value}' (pass the Id of a seeded resource, not its handle)");
        }
    }

    private static bool IsCanonicalSnowflake(string value)
        => ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
            && string.Equals(id.ToString(CultureInfo.InvariantCulture), value, StringComparison.Ordinal);

    private static bool IsUpperCaseMethod(ReadOnlySpan<char> method)
    {
        foreach (var c in method)
        {
            if (c is < 'A' or > 'Z')
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasApiPrefix(string path)
    {
        const string Prefix = "/api/v";
        return path.StartsWith(Prefix, StringComparison.Ordinal)
            && path.Length > Prefix.Length
            && char.IsAsciiDigit(path[Prefix.Length]);
    }

    private static bool HasPercentEncoding(string segment)
    {
        for (var i = 0; i + 2 < segment.Length; i++)
        {
            if (segment[i] == '%' && char.IsAsciiHexDigit(segment[i + 1]) && char.IsAsciiHexDigit(segment[i + 2]))
            {
                return true;
            }
        }

        return false;
    }

    private static ArgumentException Refuse(string route, string reason)
        => new($"The route pattern '{route}' is not valid: {reason}.", RouteParameterName);
}
