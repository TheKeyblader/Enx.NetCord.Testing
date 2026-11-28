using System.Runtime.CompilerServices;
using Enx.NetCord.Testing.Assertions;
using Enx.NetCord.Testing.Rest;

namespace Enx.NetCord.Testing;

/// <summary>Asserts on the REST attempts recorded by a fake.</summary>
/// <remarks>
/// A route pattern is <c>METHOD /path</c> where each parameter segment is either its placeholder, such as
/// <c>{channel_id}</c>, or a concrete decoded value. Attempts are matched whatever their outcome, error responses included:
/// a retry after a 429 is two attempts, and "was this message created" is a question for the state, asked with the real client.
/// Assertions check a snapshot at once and never wait.
/// </remarks>
public static class RecordedRequestAssertions
{
    /// <summary>Asserts that at least one recorded attempt matches the route pattern and the predicate.</summary>
    /// <param name="recording">The journal of the fake.</param>
    /// <param name="route">The route pattern, such as <c>POST /channels/{channel_id}/messages</c>.</param>
    /// <param name="predicate">An optional filter applied to the attempts that match the pattern.</param>
    /// <param name="predicateExpression">The source text of <paramref name="predicate"/>, supplied by the compiler.</param>
    /// <returns>The matching attempts in recording order; never empty.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="recording"/> or <paramref name="route"/> is <see langword="null"/>.</exception>
    /// <exception cref="FakeDiscordAssertionException">No attempt matches.</exception>
    /// <exception cref="ArgumentException">The pattern does not identify a known REST operation, or holds a value that cannot match.</exception>
    /// <exception cref="ObjectDisposedException">The fake has been disposed.</exception>
    public static IReadOnlyList<RecordedRestRequest> ShouldHaveRequested(
        this DiscordRecording recording,
        string route,
        Func<RecordedRestRequest, bool>? predicate = null,
        [CallerArgumentExpression(nameof(predicate))] string? predicateExpression = null)
    {
        var (requests, pattern) = Prepare(recording, route);
        return AtLeastOne(requests, pattern, predicate, predicateExpression);
    }

    /// <summary>Asserts that exactly one recorded attempt matches the route pattern and the predicate.</summary>
    /// <param name="recording">The journal of the fake.</param>
    /// <param name="route">The route pattern, such as <c>POST /channels/{channel_id}/messages</c>.</param>
    /// <param name="predicate">An optional filter applied to the attempts that match the pattern.</param>
    /// <param name="predicateExpression">The source text of <paramref name="predicate"/>, supplied by the compiler.</param>
    /// <returns>The matching attempt.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="recording"/> or <paramref name="route"/> is <see langword="null"/>.</exception>
    /// <exception cref="FakeDiscordAssertionException">No attempt, or more than one, matches.</exception>
    /// <exception cref="ArgumentException">The pattern does not identify a known REST operation, or holds a value that cannot match.</exception>
    /// <exception cref="ObjectDisposedException">The fake has been disposed.</exception>
    public static RecordedRestRequest ShouldHaveRequestedOnce(
        this DiscordRecording recording,
        string route,
        Func<RecordedRestRequest, bool>? predicate = null,
        [CallerArgumentExpression(nameof(predicate))] string? predicateExpression = null)
    {
        var (requests, pattern) = Prepare(recording, route);
        return ExactlyOne(requests, pattern, predicate, predicateExpression);
    }

    /// <summary>Asserts that no recorded attempt matches the route pattern and the predicate.</summary>
    /// <param name="recording">The journal of the fake.</param>
    /// <param name="route">The route pattern, such as <c>POST /channels/{channel_id}/messages</c>.</param>
    /// <param name="predicate">An optional filter applied to the attempts that match the pattern.</param>
    /// <param name="predicateExpression">The source text of <paramref name="predicate"/>, supplied by the compiler.</param>
    /// <exception cref="ArgumentNullException"><paramref name="recording"/> or <paramref name="route"/> is <see langword="null"/>.</exception>
    /// <exception cref="FakeDiscordAssertionException">At least one attempt matches.</exception>
    /// <exception cref="ArgumentException">The pattern does not identify a known REST operation, or holds a value that cannot match.</exception>
    /// <exception cref="ObjectDisposedException">The fake has been disposed.</exception>
    public static void ShouldNotHaveRequested(
        this DiscordRecording recording,
        string route,
        Func<RecordedRestRequest, bool>? predicate = null,
        [CallerArgumentExpression(nameof(predicate))] string? predicateExpression = null)
    {
        var (requests, pattern) = Prepare(recording, route);
        None(requests, pattern, predicate, predicateExpression);
    }

    /// <summary>Asserts that at least one attempt of the sequence matches the route pattern and the predicate.</summary>
    /// <param name="requests">Recorded attempts, such as a slice of <see cref="DiscordRecording.Requests"/>; enumerated once.</param>
    /// <param name="route">The route pattern, such as <c>POST /channels/{channel_id}/messages</c>.</param>
    /// <param name="predicate">An optional filter applied to the attempts that match the pattern.</param>
    /// <param name="predicateExpression">The source text of <paramref name="predicate"/>, supplied by the compiler.</param>
    /// <returns>The matching attempts in sequence order; never empty.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="requests"/> or <paramref name="route"/> is <see langword="null"/>.</exception>
    /// <exception cref="FakeDiscordAssertionException">No attempt matches.</exception>
    /// <exception cref="ArgumentException">
    /// The pattern does not identify a known REST operation, or holds a value that cannot match; or the sequence has a <see langword="null"/> element.
    /// The pattern is checked before the sequence is enumerated.
    /// </exception>
    public static IReadOnlyList<RecordedRestRequest> ShouldHaveRequested(
        this IEnumerable<RecordedRestRequest> requests,
        string route,
        Func<RecordedRestRequest, bool>? predicate = null,
        [CallerArgumentExpression(nameof(predicate))] string? predicateExpression = null)
    {
        var pattern = Prepare(requests, route);
        return AtLeastOne(requests, pattern, predicate, predicateExpression);
    }

    /// <summary>Asserts that exactly one attempt of the sequence matches the route pattern and the predicate.</summary>
    /// <param name="requests">Recorded attempts, such as a slice of <see cref="DiscordRecording.Requests"/>; enumerated once.</param>
    /// <param name="route">The route pattern, such as <c>POST /channels/{channel_id}/messages</c>.</param>
    /// <param name="predicate">An optional filter applied to the attempts that match the pattern.</param>
    /// <param name="predicateExpression">The source text of <paramref name="predicate"/>, supplied by the compiler.</param>
    /// <returns>The matching attempt.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="requests"/> or <paramref name="route"/> is <see langword="null"/>.</exception>
    /// <exception cref="FakeDiscordAssertionException">No attempt, or more than one, matches.</exception>
    /// <exception cref="ArgumentException">
    /// The pattern does not identify a known REST operation, or holds a value that cannot match; or the sequence has a <see langword="null"/> element.
    /// The pattern is checked before the sequence is enumerated.
    /// </exception>
    public static RecordedRestRequest ShouldHaveRequestedOnce(
        this IEnumerable<RecordedRestRequest> requests,
        string route,
        Func<RecordedRestRequest, bool>? predicate = null,
        [CallerArgumentExpression(nameof(predicate))] string? predicateExpression = null)
    {
        var pattern = Prepare(requests, route);
        return ExactlyOne(requests, pattern, predicate, predicateExpression);
    }

    /// <summary>Asserts that no attempt of the sequence matches the route pattern and the predicate.</summary>
    /// <param name="requests">Recorded attempts, such as a slice of <see cref="DiscordRecording.Requests"/>; enumerated once.</param>
    /// <param name="route">The route pattern, such as <c>POST /channels/{channel_id}/messages</c>.</param>
    /// <param name="predicate">An optional filter applied to the attempts that match the pattern.</param>
    /// <param name="predicateExpression">The source text of <paramref name="predicate"/>, supplied by the compiler.</param>
    /// <exception cref="ArgumentNullException"><paramref name="requests"/> or <paramref name="route"/> is <see langword="null"/>.</exception>
    /// <exception cref="FakeDiscordAssertionException">At least one attempt matches.</exception>
    /// <exception cref="ArgumentException">
    /// The pattern does not identify a known REST operation, or holds a value that cannot match; or the sequence has a <see langword="null"/> element.
    /// The pattern is checked before the sequence is enumerated.
    /// </exception>
    public static void ShouldNotHaveRequested(
        this IEnumerable<RecordedRestRequest> requests,
        string route,
        Func<RecordedRestRequest, bool>? predicate = null,
        [CallerArgumentExpression(nameof(predicate))] string? predicateExpression = null)
    {
        var pattern = Prepare(requests, route);
        None(requests, pattern, predicate, predicateExpression);
    }

    // Order of checks (ADR 0012, section 4): null arguments, closed fake, pattern, then examination.
    private static (IReadOnlyList<RecordedRestRequest> Requests, RoutePattern Pattern) Prepare(DiscordRecording recording, string route)
    {
        ArgumentNullException.ThrowIfNull(recording);
        ArgumentNullException.ThrowIfNull(route);

        recording.ThrowIfDisposed();
        var pattern = RoutePattern.Parse(route, recording.Routes);

        // One snapshot per call, taken once the call is known to be valid.
        return (recording.Requests, pattern);
    }

    private static RoutePattern Prepare(IEnumerable<RecordedRestRequest> requests, string route)
    {
        ArgumentNullException.ThrowIfNull(requests);
        ArgumentNullException.ThrowIfNull(route);

        // A detached suite has no fake: the default table holds the same inventory as any public fake (ADR 0012).
        return RoutePattern.Parse(route, RouteTable.Default);
    }

    private static IReadOnlyList<RecordedRestRequest> AtLeastOne(
        IEnumerable<RecordedRestRequest> requests,
        RoutePattern pattern,
        Func<RecordedRestRequest, bool>? predicate,
        string? predicateExpression)
    {
        var examination = Examine(requests, pattern, predicate);
        if (examination.Matches.Count == 0)
        {
            throw Fail(pattern, RequestExpectation.AtLeastOne, predicate, predicateExpression, examination);
        }

        return examination.Matches;
    }

    private static RecordedRestRequest ExactlyOne(
        IEnumerable<RecordedRestRequest> requests,
        RoutePattern pattern,
        Func<RecordedRestRequest, bool>? predicate,
        string? predicateExpression)
    {
        var examination = Examine(requests, pattern, predicate);
        if (examination.Matches.Count != 1)
        {
            throw Fail(pattern, RequestExpectation.ExactlyOne, predicate, predicateExpression, examination);
        }

        return examination.Matches[0];
    }

    private static void None(
        IEnumerable<RecordedRestRequest> requests,
        RoutePattern pattern,
        Func<RecordedRestRequest, bool>? predicate,
        string? predicateExpression)
    {
        var examination = Examine(requests, pattern, predicate);
        if (examination.Matches.Count != 0)
        {
            throw Fail(pattern, RequestExpectation.None, predicate, predicateExpression, examination);
        }
    }

    private static Examination Examine(IEnumerable<RecordedRestRequest> requests, RoutePattern pattern, Func<RecordedRestRequest, bool>? predicate)
    {
        var examined = new List<RecordedRestRequest>();
        var isMatch = new List<bool>();
        var matches = new List<RecordedRestRequest>();
        foreach (var request in requests)
        {
            if (request is null)
            {
                throw new ArgumentException("The sequence contains a null element.", nameof(requests));
            }

            // The predicate only sees attempts that already match the pattern, once each, in order.
            var matched = pattern.Matches(request) && (predicate is null || predicate(request));
            examined.Add(request);
            isMatch.Add(matched);
            if (matched)
            {
                matches.Add(request);
            }
        }

        return new Examination(examined, isMatch, matches);
    }

    private static FakeDiscordAssertionException Fail(
        RoutePattern pattern,
        RequestExpectation expectation,
        Func<RecordedRestRequest, bool>? predicate,
        string? predicateExpression,
        Examination examination)
    {
        var predicateText = predicate is null ? null : predicateExpression ?? "<predicate>";
        return new FakeDiscordAssertionException(
            AssertionMessage.Build(pattern, expectation, predicateText, examination.Examined, examination.IsMatch, examination.Matches.Count));
    }

    private sealed record Examination(List<RecordedRestRequest> Examined, List<bool> IsMatch, List<RecordedRestRequest> Matches);
}
