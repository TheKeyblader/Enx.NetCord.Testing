using System.Collections;
using System.Net;
using Enx.NetCord.Testing.Rest;
using Xunit;
using static Enx.NetCord.Testing.Tests.Assertions.AssertionTestSupport;

namespace Enx.NetCord.Testing.Tests.Assertions;

public sealed class RecordedRequestAssertionsTests
{
    [Fact]
    public async Task ShouldHaveRequested_SeveralMatches_ReturnsTheJournalInstancesInRecordingOrder()
    {
        await using var discord = CreateFake();
        var channel = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();
        await SendAsync(rest, HttpMethod.Post, $"/channels/{channel.Id}/messages", "{\"content\":\"one\"}");
        await SendAsync(rest, HttpMethod.Get, $"/users/@me");
        await SendAsync(rest, HttpMethod.Post, $"/channels/{300}/messages", "{\"content\":\"two\"}");
        await SendAsync(rest, HttpMethod.Post, $"/channels/{channel.Id}/messages", "{\"content\":\"three\"}");

        var anywhere = discord.Recorded.ShouldHaveRequested(PostMessage);
        var here = discord.Recorded.ShouldHaveRequested($"POST /channels/{channel.Id}/messages");

        var journal = discord.Recorded.Requests;
        Assert.Equal([journal[0], journal[2], journal[3]], anywhere);
        Assert.Equal([journal[0], journal[3]], here);
        Assert.Same(journal[0], here[0]);
    }

    [Fact]
    public async Task ShouldHaveRequested_NoMatch_ThrowsAnAssertionExceptionThatNamesThePattern()
    {
        await using var discord = CreateFake();
        using var rest = discord.CreateRestClient();
        await SendAsync(rest, HttpMethod.Get, $"/users/@me");

        var exception = Assert.Throws<FakeDiscordAssertionException>(() => discord.Recorded.ShouldHaveRequested(PostMessage));

        Assert.StartsWith($"Expected at least one request matching {PostMessage}, but found none.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldHaveRequestedOnce_ExactlyOneMatch_ReturnsThatAttempt()
    {
        await using var discord = CreateFake();
        var channel = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();
        await SendAsync(rest, HttpMethod.Post, $"/channels/{channel.Id}/messages", "{\"content\":\"hi\"}");

        var post = discord.Recorded.ShouldHaveRequestedOnce($"POST /channels/{channel.Id}/messages");

        Assert.Same(Assert.Single(discord.Recorded.Requests), post);
        Assert.Equal(HttpStatusCode.OK, post.StatusCode);
        Assert.Equal("hi", post.RequestBody!.Value.GetProperty("content").GetString());
    }

    [Fact]
    public async Task ShouldHaveRequestedOnce_NoMatch_SaysItFoundNone()
    {
        await using var discord = CreateFake();

        var exception = Assert.Throws<FakeDiscordAssertionException>(() => discord.Recorded.ShouldHaveRequestedOnce(PostMessage));

        Assert.StartsWith($"Expected exactly one request matching {PostMessage}, but found none.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldHaveRequestedOnce_TwoMatches_SaysHowManyWereFound()
    {
        await using var discord = CreateFake();
        using var rest = discord.CreateRestClient();
        await SendAsync(rest, HttpMethod.Post, $"/channels/{100}/messages", "{}");
        await SendAsync(rest, HttpMethod.Post, $"/channels/{100}/messages", "{}");

        var exception = Assert.Throws<FakeDiscordAssertionException>(() => discord.Recorded.ShouldHaveRequestedOnce($"POST /channels/{100}/messages"));

        Assert.StartsWith("Expected exactly one request matching POST /channels/100/messages, but found 2.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldNotHaveRequested_OtherChannelOnly_Passes()
    {
        await using var discord = CreateFake();
        using var rest = discord.CreateRestClient();
        await SendAsync(rest, HttpMethod.Post, $"/channels/{100}/messages", "{}");

        discord.Recorded.ShouldNotHaveRequested($"POST /channels/{101}/messages");
        discord.Recorded.ShouldNotHaveRequested("DELETE /channels/{channel_id}/messages/{message_id}");
    }

    [Fact]
    public async Task ShouldNotHaveRequested_AMatchExists_ThrowsAnAssertionExceptionWithTheCount()
    {
        await using var discord = CreateFake();
        using var rest = discord.CreateRestClient();
        await SendAsync(rest, HttpMethod.Post, $"/channels/{100}/messages", "{}");
        await SendAsync(rest, HttpMethod.Post, $"/channels/{100}/messages", "{}");

        var exception = Assert.Throws<FakeDiscordAssertionException>(() => discord.Recorded.ShouldNotHaveRequested(PostMessage));

        Assert.StartsWith($"Expected no request matching {PostMessage}, but found 2.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldHaveRequestedOnce_RejectedAttempt_CountsAsAnAttempt()
    {
        await using var discord = CreateFake();
        using var rest = discord.CreateRestClient();
        await SendAsync(rest, HttpMethod.Post, $"/channels/{200}/messages", "{}");

        var attempt = discord.Recorded.ShouldHaveRequestedOnce($"POST /channels/{200}/messages");

        Assert.Equal(HttpStatusCode.Forbidden, attempt.StatusCode);
    }

    [Fact]
    public async Task ShouldHaveRequestedOnce_InventoriedRouteWithoutHandler_CountsTheNotImplementedAttempt()
    {
        await using var discord = CreateFake();
        using var rest = discord.CreateRestClient();
        await SendAsync(rest, HttpMethod.Get, $"/guilds/{5}");

        var attempt = discord.Recorded.ShouldHaveRequestedOnce("GET /guilds/{guild_id}");

        Assert.Equal(HttpStatusCode.NotFound, attempt.StatusCode);
        discord.Recorded.ShouldNotHaveRequested($"GET /guilds/{6}");
    }

    [Fact]
    public async Task ShouldHaveRequested_AfterARateLimitRetry_SeesTwoAttemptsAndOnceFails()
    {
        await using var discord = CreateFake();
        var channel = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();

        using (discord.Rest.Intercept(PostMessage, RestEffect.RateLimited(0)))
        {
            await SendAsync(rest, HttpMethod.Post, $"/channels/{channel.Id}/messages", "{}");
        }

        var attempts = discord.Recorded.ShouldHaveRequested($"POST /channels/{channel.Id}/messages");
        Assert.Equal([HttpStatusCode.TooManyRequests, HttpStatusCode.OK], attempts.Select(static a => a.StatusCode));
        Assert.Throws<FakeDiscordAssertionException>(() => discord.Recorded.ShouldHaveRequestedOnce($"POST /channels/{channel.Id}/messages"));
    }

    [Fact]
    public async Task ShouldHaveRequested_TwoClientsOfTheSameFake_ShareTheJournal()
    {
        await using var discord = CreateFake();
        using var first = discord.CreateRestClient();
        using var second = discord.CreateRestClient();
        await SendAsync(first, HttpMethod.Get, $"/users/@me");
        await SendAsync(second, HttpMethod.Get, $"/users/@me");

        Assert.Equal(2, discord.Recorded.ShouldHaveRequested("GET /users/@me").Count);
    }

    [Fact]
    public async Task ShouldHaveRequested_Predicate_KeepsOnlyAttemptsItAccepts()
    {
        await using var discord = CreateFake();
        using var rest = discord.CreateRestClient();
        await SendAsync(rest, HttpMethod.Post, $"/channels/{100}/messages", "{}");
        await SendAsync(rest, HttpMethod.Post, $"/channels/{200}/messages", "{}");
        await SendAsync(rest, HttpMethod.Post, $"/channels/{100}/messages", "{}");

        var accepted = discord.Recorded.ShouldHaveRequested(PostMessage, r => r.StatusCode == HttpStatusCode.OK);
        var rejected = discord.Recorded.ShouldHaveRequestedOnce(PostMessage, r => r.StatusCode == HttpStatusCode.Forbidden);

        Assert.Equal(2, accepted.Count);
        Assert.Equal("200", rejected.RouteParameters["channel_id"]);
        discord.Recorded.ShouldNotHaveRequested(PostMessage, r => r.StatusCode == HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task ShouldHaveRequested_Predicate_SeesOnlyAttemptsOfThePatternOnceEachInOrder()
    {
        await using var discord = CreateFake();
        using var rest = discord.CreateRestClient();
        await SendAsync(rest, HttpMethod.Post, $"/channels/{100}/messages", "{}");
        await SendAsync(rest, HttpMethod.Get, $"/users/@me");
        await SendAsync(rest, HttpMethod.Post, $"/channels/{101}/messages", "{}");
        await SendAsync(rest, HttpMethod.Post, $"/channels/{200}/messages", "{}");
        var seen = new List<string>();

        discord.Recorded.ShouldHaveRequested(
            PostMessage,
            r =>
            {
                seen.Add(r.Path);
                return true;
            });

        Assert.Equal(["/channels/100/messages", "/channels/101/messages", "/channels/200/messages"], seen);
    }

    [Fact]
    public async Task ShouldHaveRequested_PredicateThatThrows_PropagatesItsOwnException()
    {
        await using var discord = CreateFake();
        using var rest = discord.CreateRestClient();
        await SendAsync(rest, HttpMethod.Post, $"/channels/{100}/messages", "{}");

        var exception = Assert.Throws<InvalidOperationException>(
            () => discord.Recorded.ShouldHaveRequested(PostMessage, static _ => throw new InvalidOperationException("boom")));

        Assert.Equal("boom", exception.Message);
    }

    [Fact]
    public async Task ShouldHaveRequested_PredicateOnAnAttemptWithoutBody_ReceivesANullRequestBody()
    {
        await using var discord = CreateFake();
        using var rest = discord.CreateRestClient();
        await SendAsync(rest, HttpMethod.Get, $"/users/@me");

        var found = discord.Recorded.ShouldHaveRequested("GET /users/@me", r => r.RequestBody is null);

        Assert.Single(found);
    }

    [Fact]
    public async Task ShouldHaveRequested_PredicateFailure_QuotesThePredicateSourceInTheMessage()
    {
        await using var discord = CreateFake();
        using var rest = discord.CreateRestClient();
        await SendAsync(rest, HttpMethod.Post, $"/channels/{100}/messages", "{}");

        var exception = Assert.Throws<FakeDiscordAssertionException>(
            () => discord.Recorded.ShouldHaveRequested(PostMessage, r => r.StatusCode == HttpStatusCode.Created));

        Assert.StartsWith(
            $"Expected at least one request matching {PostMessage} where r => r.StatusCode == HttpStatusCode.Created, but found none.",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sequence_SliceOfTheJournal_ScopesTheExamination()
    {
        await using var discord = CreateFake();
        using var rest = discord.CreateRestClient();
        await SendAsync(rest, HttpMethod.Post, $"/channels/{100}/messages", "{}");
        var before = discord.Recorded.Requests.Count;
        await SendAsync(rest, HttpMethod.Post, $"/channels/{101}/messages", "{}");

        var after = discord.Recorded.Requests.Skip(before);

        var only = after.ShouldHaveRequestedOnce(PostMessage);
        Assert.Equal("101", only.RouteParameters["channel_id"]);
        after.ShouldNotHaveRequested($"POST /channels/{100}/messages");
        Assert.Single(after.ShouldHaveRequested(PostMessage));
        Assert.Equal(2, discord.Recorded.ShouldHaveRequested(PostMessage).Count);
    }

    [Fact]
    public async Task Sequence_DetachedListAfterDispose_IsStillExaminable()
    {
        var discord = CreateFake();
        using var rest = discord.CreateRestClient();
        await SendAsync(rest, HttpMethod.Get, $"/users/@me");
        var snapshot = discord.Recorded.Requests;
        await discord.DisposeAsync();

        var found = snapshot.ShouldHaveRequestedOnce("GET /users/@me");

        Assert.Same(snapshot[0], found);
        snapshot.ShouldNotHaveRequested(PostMessage);
        Assert.Throws<ObjectDisposedException>(() => discord.Recorded.ShouldHaveRequested("GET /users/@me"));
    }

    [Fact]
    public async Task Sequence_Enumerable_IsEnumeratedExactlyOnce()
    {
        await using var discord = CreateFake();
        using var rest = discord.CreateRestClient();
        await SendAsync(rest, HttpMethod.Get, $"/users/@me");
        var counting = new CountingSequence(discord.Recorded.Requests);

        counting.ShouldHaveRequestedOnce("GET /users/@me");
        Assert.Equal(1, counting.Enumerations);

        counting.ShouldNotHaveRequested(PostMessage);
        Assert.Equal(2, counting.Enumerations);
    }

    [Fact]
    public void Sequence_RefusedPattern_DoesNotEnumerateALazySequence()
    {
        var enumerated = false;
        IEnumerable<RecordedRestRequest> Lazy()
        {
            enumerated = true;
            yield break;
        }

        Assert.Throws<ArgumentException>(() => Lazy().ShouldHaveRequested("GET /nowhere"));
        Assert.Throws<ArgumentException>(() => Lazy().ShouldHaveRequestedOnce("GET /channels/general"));
        Assert.Throws<ArgumentException>(() => Lazy().ShouldNotHaveRequested("get /users/@me"));

        Assert.False(enumerated);
    }

    [Fact]
    public async Task Sequence_NullElement_ThrowsAnArgumentExceptionNamingTheSequence()
    {
        await using var discord = CreateFake();
        using var rest = discord.CreateRestClient();
        await SendAsync(rest, HttpMethod.Get, $"/users/@me");
        RecordedRestRequest[] withNull = [discord.Recorded.Requests[0], null!];

        var exception = Assert.Throws<ArgumentException>(() => withNull.ShouldHaveRequested("GET /users/@me"));

        Assert.Equal("requests", exception.ParamName);
    }

    [Fact]
    public async Task Sequence_EmptySequence_ReportsThatNothingWasRecorded()
    {
        await using var discord = CreateFake();

        var exception = Assert.Throws<FakeDiscordAssertionException>(() => Array.Empty<RecordedRestRequest>().ShouldHaveRequested("GET /users/@me"));

        Assert.Contains("No request was recorded.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Arguments_NullReceiverOrRoute_ThrowArgumentNullException()
    {
        await using var discord = CreateFake();

        Assert.Throws<ArgumentNullException>(() => ((DiscordRecording)null!).ShouldHaveRequested("GET /users/@me"));
        Assert.Throws<ArgumentNullException>(() => ((DiscordRecording)null!).ShouldHaveRequestedOnce("GET /users/@me"));
        Assert.Throws<ArgumentNullException>(() => ((DiscordRecording)null!).ShouldNotHaveRequested("GET /users/@me"));
        Assert.Throws<ArgumentNullException>(() => discord.Recorded.ShouldHaveRequested(null!));
        Assert.Throws<ArgumentNullException>(() => discord.Recorded.ShouldHaveRequestedOnce(null!));
        Assert.Throws<ArgumentNullException>(() => discord.Recorded.ShouldNotHaveRequested(null!));
        Assert.Throws<ArgumentNullException>(() => ((IEnumerable<RecordedRestRequest>)null!).ShouldHaveRequested("GET /users/@me"));
        Assert.Throws<ArgumentNullException>(() => ((IEnumerable<RecordedRestRequest>)null!).ShouldHaveRequestedOnce("GET /users/@me"));
        Assert.Throws<ArgumentNullException>(() => ((IEnumerable<RecordedRestRequest>)null!).ShouldNotHaveRequested("GET /users/@me"));
        Assert.Throws<ArgumentNullException>(() => Array.Empty<RecordedRestRequest>().ShouldHaveRequested(null!));
    }

    [Fact]
    public async Task Arguments_ClosedFakeAndBadPattern_ReportTheClosedFakeFirst()
    {
        var discord = CreateFake();
        await discord.DisposeAsync();

        Assert.Throws<ObjectDisposedException>(() => discord.Recorded.ShouldHaveRequested("GET /nowhere"));
        Assert.Throws<ObjectDisposedException>(() => discord.Recorded.ShouldHaveRequestedOnce("GET /users/@me"));
        Assert.Throws<ObjectDisposedException>(() => discord.Recorded.ShouldNotHaveRequested("GET /users/@me"));
        Assert.Throws<ArgumentNullException>(() => discord.Recorded.ShouldHaveRequested(null!));
    }

    private sealed class CountingSequence(IReadOnlyList<RecordedRestRequest> items) : IEnumerable<RecordedRestRequest>
    {
        public int Enumerations { get; private set; }

        public IEnumerator<RecordedRestRequest> GetEnumerator()
        {
            Enumerations++;
            return items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
