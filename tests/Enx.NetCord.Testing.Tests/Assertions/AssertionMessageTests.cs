using System.Net;
using Xunit;
using static Enx.NetCord.Testing.Tests.Assertions.AssertionTestSupport;

namespace Enx.NetCord.Testing.Tests.Assertions;

/// <summary>The exact text is not a contract: these tests check the elements of ADR 0012, section 5.</summary>
public sealed class AssertionMessageTests
{
    [Fact]
    public async Task ShouldHaveRequestedOnce_TwoMatches_ListsSameOperationFirstThenTheOthers()
    {
        await using var discord = CreateFake();
        using var rest = discord.CreateRestClient();
        await SendAsync(rest, HttpMethod.Get, $"/users/@me");
        await SendAsync(rest, HttpMethod.Post, $"/channels/{100}/messages", "{\"content\":\"Welcome bob!\"}");
        await SendAsync(rest, HttpMethod.Post, $"/channels/{200}/messages", "{\"content\":\"Welcome bob!\"}");
        await SendAsync(rest, HttpMethod.Put, $"/channels/{100}/messages/{300}/reactions/{"👍"}/@me", "{\"secret\":\"hidden\"}");
        await SendAsync(rest, HttpMethod.Post, $"/channels/{100}/messages", "{\"content\":\"Welcome bob!\"}");

        var exception = Assert.Throws<FakeDiscordAssertionException>(() => discord.Recorded.ShouldHaveRequestedOnce("POST /channels/100/messages"));

        var lines = exception.Message.Split('\n');
        Assert.Equal("Expected exactly one request matching POST /channels/100/messages, but found 2.", lines[0]);
        Assert.Equal(string.Empty, lines[1]);
        Assert.Equal("Requests to POST /channels/{channel_id}/messages (3 of 5 examined; * = match):", lines[2]);
        Assert.Equal("* #2 POST /channels/100/messages -> 200  {\"content\":\"Welcome bob!\"}", lines[3]);
        Assert.Equal("  #3 POST /channels/200/messages -> 403 (50001)  {\"content\":\"Welcome bob!\"}", lines[4]);
        Assert.Equal("* #5 POST /channels/100/messages -> 200  {\"content\":\"Welcome bob!\"}", lines[5]);
        Assert.Equal(string.Empty, lines[6]);
        Assert.Equal("Other requests (2):", lines[7]);
        Assert.Equal("  #1 GET /users/@me -> 200", lines[8]);
        Assert.Equal("  #4 PUT /channels/100/messages/300/reactions/👍/@me -> 204", lines[9]);
        Assert.Equal(10, lines.Length);
        Assert.DoesNotContain("hidden", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldNotHaveRequested_AMatch_ShowsTheOperationSectionButNotTheOtherRequests()
    {
        await using var discord = CreateFake();
        using var rest = discord.CreateRestClient();
        await SendAsync(rest, HttpMethod.Get, $"/users/@me");
        await SendAsync(rest, HttpMethod.Post, $"/channels/{100}/messages", "{}");

        var exception = Assert.Throws<FakeDiscordAssertionException>(() => discord.Recorded.ShouldNotHaveRequested(PostMessage));

        Assert.Contains("Requests to POST /channels/{channel_id}/messages (1 of 2 examined; * = match):", exception.Message, StringComparison.Ordinal);
        Assert.Contains("* #2 POST /channels/100/messages -> 200  {}", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Other requests", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("#1", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldHaveRequested_PredicateRejectsAnAttempt_MarksOnlyTheAcceptedOnes()
    {
        await using var discord = CreateFake();
        using var rest = discord.CreateRestClient();
        await SendAsync(rest, HttpMethod.Post, $"/channels/{200}/messages", "{}");

        var exception = Assert.Throws<FakeDiscordAssertionException>(
            () => discord.Recorded.ShouldHaveRequested(PostMessage, r => r.StatusCode == HttpStatusCode.OK));

        Assert.Contains(" where r => r.StatusCode == HttpStatusCode.OK, but found none.", exception.Message, StringComparison.Ordinal);
        Assert.Contains("\n  #1 POST /channels/200/messages -> 403 (50001)", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("* #1", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldHaveRequested_EmptyJournal_SaysNoRequestWasRecorded()
    {
        await using var discord = CreateFake();

        var exception = Assert.Throws<FakeDiscordAssertionException>(() => discord.Recorded.ShouldHaveRequested(PostMessage));

        Assert.Equal(
            $"Expected at least one request matching {PostMessage}, but found none.\n\nNo request was recorded.",
            exception.Message);
    }

    [Fact]
    public async Task ShouldHaveRequested_OnlyOtherRequests_ListsThemWithoutAnEmptyOperationSection()
    {
        await using var discord = CreateFake();
        using var rest = discord.CreateRestClient();
        await SendAsync(rest, HttpMethod.Get, $"/users/@me", query: "?a=1&b=two");

        var exception = Assert.Throws<FakeDiscordAssertionException>(() => discord.Recorded.ShouldHaveRequested(PostMessage));

        Assert.DoesNotContain("Requests to", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Other requests (1):\n  #1 GET /users/@me?a=1&b=two -> 200", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldHaveRequested_AttemptWithoutAResponse_IsShownAsNoResponse()
    {
        await using var discord = Facade.FacadeTestSupport.CreateWithoutHandlers(new FakeDiscordOptions { UnimplementedRoutes = UnimplementedRouteMode.Strict });
        using var rest = discord.CreateRestClient();
        await Assert.ThrowsAsync<NotSupportedException>(() => rest.GetCurrentUserAsync(cancellationToken: TestContext.Current.CancellationToken));

        var exception = Assert.Throws<FakeDiscordAssertionException>(() => discord.Recorded.ShouldHaveRequested(PostMessage));

        Assert.Contains("#1 GET /users/@me -> no response", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldNotHaveRequested_ManyMatches_CapsTheSectionAtTenLines()
    {
        await using var discord = CreateFake();
        using var rest = discord.CreateRestClient();
        for (var i = 0; i < 12; i++)
        {
            await SendAsync(rest, HttpMethod.Post, $"/channels/{100}/messages", "{}");
        }

        var exception = Assert.Throws<FakeDiscordAssertionException>(() => discord.Recorded.ShouldNotHaveRequested(PostMessage));

        Assert.StartsWith($"Expected no request matching {PostMessage}, but found 12.", exception.Message, StringComparison.Ordinal);
        Assert.Contains("(12 of 12 examined; * = match):", exception.Message, StringComparison.Ordinal);
        Assert.Contains("* #10 POST", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("#11", exception.Message, StringComparison.Ordinal);
        Assert.EndsWith("\n  … and 2 more", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldHaveRequested_ManyOtherRequests_CapsTheOtherSectionAtTenLines()
    {
        await using var discord = CreateFake();
        using var rest = discord.CreateRestClient();
        for (var i = 0; i < 13; i++)
        {
            await SendAsync(rest, HttpMethod.Get, $"/users/@me");
        }

        var exception = Assert.Throws<FakeDiscordAssertionException>(() => discord.Recorded.ShouldHaveRequested(PostMessage));

        Assert.Contains("Other requests (13):", exception.Message, StringComparison.Ordinal);
        Assert.Contains("#10 GET /users/@me", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("#11", exception.Message, StringComparison.Ordinal);
        Assert.EndsWith("\n  … and 3 more", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldNotHaveRequested_LongBody_IsCutAtTwoHundredCharacters()
    {
        await using var discord = CreateFake();
        using var rest = discord.CreateRestClient();
        var content = new string('x', 300);
        await SendAsync(rest, HttpMethod.Post, $"/channels/{100}/messages", $"{{\"content\":\"{content}\"}}");

        var exception = Assert.Throws<FakeDiscordAssertionException>(() => discord.Recorded.ShouldNotHaveRequested(PostMessage));

        Assert.DoesNotContain(content, exception.Message, StringComparison.Ordinal);
        Assert.Contains(new string('x', 150), exception.Message, StringComparison.Ordinal);
        Assert.Contains("…", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldHaveRequested_SuiteRanks_AreRelativeToTheExaminedSequence()
    {
        await using var discord = CreateFake();
        using var rest = discord.CreateRestClient();
        await SendAsync(rest, HttpMethod.Get, $"/users/@me");
        await SendAsync(rest, HttpMethod.Get, $"/users/@me");

        var exception = Assert.Throws<FakeDiscordAssertionException>(() => discord.Recorded.Requests.Skip(1).ShouldHaveRequestedOnce("GET /users/@me", r => false));

        Assert.Contains("(1 of 1 examined; * = match):", exception.Message, StringComparison.Ordinal);
        Assert.Contains("\n  #1 GET /users/@me -> 200", exception.Message, StringComparison.Ordinal);
    }
}
