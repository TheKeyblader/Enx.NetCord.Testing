using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Enx.NetCord.Testing.Assertions;

/// <summary>What an assertion on recorded requests expects to find.</summary>
internal enum RequestExpectation
{
    /// <summary>At least one matching attempt.</summary>
    AtLeastOne,

    /// <summary>Exactly one matching attempt.</summary>
    ExactlyOne,

    /// <summary>No matching attempt.</summary>
    None,
}

/// <summary>Builds the failure message of the request assertions (ADR 0012, section 5).</summary>
/// <remarks>The text is not a compatibility contract: it is written for the person reading a failing test.</remarks>
internal static class AssertionMessage
{
    private const int MaxLinesPerSection = 10;
    private const int MaxBodyLength = 200;

    /// <summary>Builds the message of a failed assertion.</summary>
    /// <param name="pattern">The resolved pattern.</param>
    /// <param name="expectation">What the assertion expected.</param>
    /// <param name="predicateText">The source text of the predicate, or <see langword="null"/> when there is none.</param>
    /// <param name="examined">The examined attempts, in order.</param>
    /// <param name="isMatch">For each examined attempt, whether it satisfies the pattern and the predicate.</param>
    /// <param name="found">The number of matching attempts.</param>
    /// <returns>The message, with lines separated by <c>\n</c>.</returns>
    public static string Build(
        RoutePattern pattern,
        RequestExpectation expectation,
        string? predicateText,
        IReadOnlyList<RecordedRestRequest> examined,
        IReadOnlyList<bool> isMatch,
        int found)
    {
        var builder = new StringBuilder();
        builder.Append(Sentence(pattern, expectation, predicateText, found));

        if (examined.Count == 0)
        {
            builder.Append("\n\nNo request was recorded.");
            return builder.ToString();
        }

        var sameOperation = new List<int>();
        var others = new List<int>();
        for (var i = 0; i < examined.Count; i++)
        {
            (pattern.IsSameOperation(examined[i]) ? sameOperation : others).Add(i);
        }

        if (sameOperation.Count > 0)
        {
            builder.Append(CultureInfo.InvariantCulture, $"\n\nRequests to {pattern.Method} {pattern.Template} ({sameOperation.Count} of {examined.Count} examined; * = match):");
            AppendLines(builder, sameOperation, examined, isMatch, withBody: true);
        }

        if (expectation != RequestExpectation.None && others.Count > 0)
        {
            builder.Append(CultureInfo.InvariantCulture, $"\n\nOther requests ({others.Count}):");
            AppendLines(builder, others, examined, isMatch, withBody: false);
        }

        return builder.ToString();
    }

    private static string Sentence(RoutePattern pattern, RequestExpectation expectation, string? predicateText, int found)
    {
        var where = predicateText is null ? string.Empty : $" where {predicateText}";
        var subject = $"{pattern.Text}{where}";
        return expectation switch
        {
            RequestExpectation.AtLeastOne => $"Expected at least one request matching {subject}, but found none.",
            RequestExpectation.ExactlyOne when found == 0 => $"Expected exactly one request matching {subject}, but found none.",
            RequestExpectation.ExactlyOne => string.Create(CultureInfo.InvariantCulture, $"Expected exactly one request matching {subject}, but found {found}."),
            _ => string.Create(CultureInfo.InvariantCulture, $"Expected no request matching {subject}, but found {found}."),
        };
    }

    private static void AppendLines(
        StringBuilder builder,
        List<int> indexes,
        IReadOnlyList<RecordedRestRequest> examined,
        IReadOnlyList<bool> isMatch,
        bool withBody)
    {
        foreach (var index in indexes.Take(MaxLinesPerSection))
        {
            builder.Append('\n').Append(isMatch[index] ? "* " : "  ");
            AppendRequest(builder, index + 1, examined[index], withBody);
        }

        if (indexes.Count > MaxLinesPerSection)
        {
            builder.Append(CultureInfo.InvariantCulture, $"\n  … and {indexes.Count - MaxLinesPerSection} more");
        }
    }

    private static void AppendRequest(StringBuilder builder, int rank, RecordedRestRequest request, bool withBody)
    {
        builder.Append(CultureInfo.InvariantCulture, $"#{rank} {request.Method} {request.Path}");
        if (request.Query.Count > 0)
        {
            builder.Append('?').Append(string.Join('&', request.Query.Select(static p => $"{p.Key}={p.Value}")));
        }

        builder.Append(" -> ");
        if (request.StatusCode is { } status)
        {
            builder.Append(CultureInfo.InvariantCulture, $"{(int)status}");
            if (DiscordCode(request) is { } code)
            {
                builder.Append(CultureInfo.InvariantCulture, $" ({code})");
            }
        }
        else
        {
            builder.Append("no response");
        }

        if (withBody && request.RequestBody is { } body)
        {
            builder.Append("  ").Append(Shorten(body.GetRawText()));
        }
    }

    private static int? DiscordCode(RecordedRestRequest request)
        => request.StatusCode is >= System.Net.HttpStatusCode.BadRequest
            && request.ResponseBody is { ValueKind: JsonValueKind.Object } response
            && response.TryGetProperty("code", out var code)
            && code.ValueKind == JsonValueKind.Number
            && code.TryGetInt32(out var value)
                ? value
                : null;

    private static string Shorten(string text)
    {
        var singleLine = text.Replace('\r', ' ').Replace('\n', ' ');
        return singleLine.Length <= MaxBodyLength ? singleLine : singleLine[..MaxBodyLength] + "…";
    }
}
