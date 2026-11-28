using System.Net;
using Xunit;
using static Enx.NetCord.Testing.Tests.Assertions.AssertionTestSupport;

namespace Enx.NetCord.Testing.Tests.Assertions;

public sealed class RoutePatternTests
{
    [Theory]
    [InlineData("post /users/@me", "METHOD /path")]
    [InlineData("GET  /users/@me", "METHOD /path")]
    [InlineData("/users/@me", "METHOD /path")]
    [InlineData("GET", "METHOD /path")]
    [InlineData("GET users/@me", "METHOD /path")]
    [InlineData("", "METHOD /path")]
    [InlineData("GET /api/v10/users/@me", "API prefix")]
    [InlineData("GET /users/@me?x=1", "query string")]
    [InlineData("GET /users/@me#top", "fragment")]
    [InlineData("GET /users//x", "empty segment")]
    [InlineData("GET /users/", "empty segment")]
    [InlineData("GET /nowhere", "no known operation")]
    [InlineData("PUT /users/@me", "no PUT operation exists for /users/@me; the inventory has: GET, PATCH")]
    [InlineData("GET /channels/{id}", "{channel_id}")]
    [InlineData("GET /channels/{channel.Id}", "'$' prefix")]
    [InlineData("GET /users/%40me", "percent-encoded")]
    [InlineData("PUT /channels/1/messages/2/reactions/%F0%9F%91%8D/@me", "percent-encoded")]
    [InlineData("GET /channels/general", "Snowflake")]
    [InlineData("GET /channels/007", "Snowflake")]
    [InlineData("GET /channels/-1", "Snowflake")]
    [InlineData("GET /channels/+1", "Snowflake")]
    [InlineData("GET /channels/ 1", "Snowflake")]
    [InlineData("GET /channels/18446744073709551616", "Snowflake")]
    [InlineData("GET /channels/١٢٣", "Snowflake")]
    [InlineData("POST /webhooks/1/abc", "masked in the journal")]
    [InlineData("GET /invites/[redacted]", "mask of the journal")]
    [InlineData("GET /channels/[redacted]", "mask of the journal")]
    public async Task ShouldNotHaveRequested_RefusedPattern_ThrowsAnArgumentExceptionOnTheRouteParameter(string route, string reason)
    {
        await using var discord = CreateFake();

        var exception = Assert.Throws<ArgumentException>(() => discord.Recorded.ShouldNotHaveRequested(route));

        Assert.Equal("route", exception.ParamName);
        Assert.Contains(reason, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldNotHaveRequested_SeedHandleInsteadOfItsId_IsRefusedWithAReminderOfTheShape()
    {
        await using var discord = CreateFake();
        var channel = discord.Seed.Guild("Test").TextChannel("general");

        var exception = Assert.Throws<ArgumentException>(() => discord.Recorded.ShouldNotHaveRequested($"GET /channels/{channel}"));

        Assert.Contains("'channel_id' is a Snowflake", exception.Message, StringComparison.Ordinal);
        discord.Recorded.ShouldNotHaveRequested($"GET /channels/{channel.Id}");
    }

    [Theory]
    [InlineData("GET /users/@me")]
    [InlineData("GET /users/{user_id}")]
    [InlineData("GET /users/0")]
    [InlineData("GET /channels/18446744073709551615")]
    [InlineData("GET /channels/{channel_id}")]
    [InlineData("DELETE /channels/100/messages/{message_id}")]
    [InlineData("GET /invites/anything-goes")]
    [InlineData("GET /invites/{code}")]
    [InlineData("GET /webhooks/{webhook_id}/{webhook_token}")]
    [InlineData("GET /webhooks/55/{webhook_token}")]
    [InlineData("PUT /channels/1/messages/2/reactions/👍/@me")]
    [InlineData("PUT /channels/1/messages/2/reactions/{emoji_name}/@me")]
    [InlineData("GET /applications/1/activity-instances/not-a-number")]
    [InlineData("GET /applications/1/activity-instances/{instance_id}")]
    [InlineData("GET /channels/1/polls/2/answers/not-checked-as-a-snowflake")]
    [InlineData("GET /guilds/1/channels")]
    public async Task ShouldNotHaveRequested_AcceptedPattern_DoesNotThrow(string route)
    {
        await using var discord = CreateFake();

        discord.Recorded.ShouldNotHaveRequested(route);
    }

    [Fact]
    public async Task ShouldNotHaveRequested_LiteralAtSign_NeverMatchesTheParameterRoute()
    {
        await using var discord = CreateFake();
        using var rest = discord.CreateRestClient();
        await SendAsync(rest, HttpMethod.Get, $"/users/{5}");

        discord.Recorded.ShouldNotHaveRequested("GET /users/@me");
        discord.Recorded.ShouldHaveRequestedOnce("GET /users/{user_id}");
        discord.Recorded.ShouldHaveRequestedOnce($"GET /users/{5}");
    }

    [Fact]
    public async Task ShouldHaveRequestedOnce_EncodedValueInTheJournal_MatchesTheDecodedValueOfThePattern()
    {
        await using var discord = CreateFake();
        using var rest = discord.CreateRestClient();
        await SendAsync(rest, HttpMethod.Put, $"/channels/{100}/messages/{300}/reactions/{"👍"}/@me");
        await SendAsync(rest, HttpMethod.Put, $"/channels/{100}/messages/{300}/reactions/{"🎉"}/@me");

        var thumbs = discord.Recorded.ShouldHaveRequestedOnce("PUT /channels/100/messages/{message_id}/reactions/👍/@me");

        Assert.Equal("👍", thumbs.RouteParameters["emoji_name"]);
        Assert.Equal(2, discord.Recorded.ShouldHaveRequested("PUT /channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}/@me").Count);
    }

    [Fact]
    public async Task ShouldHaveRequestedOnce_TokenPlaceholder_MatchesTheMaskedAttempt()
    {
        await using var discord = CreateFake();
        using var rest = discord.CreateRestClient();
        await SendAsync(rest, HttpMethod.Get, $"/webhooks/{55}/{"s3cr3t-token"}");

        var attempt = discord.Recorded.ShouldHaveRequestedOnce("GET /webhooks/55/{webhook_token}");

        Assert.Equal("/webhooks/55/[redacted]", attempt.Path);
        discord.Recorded.ShouldNotHaveRequested("GET /webhooks/56/{webhook_token}");
    }

    [Fact]
    public async Task ShouldHaveRequestedOnce_FreeStringParameter_IsComparedAsText()
    {
        await using var discord = CreateFake();
        using var rest = discord.CreateRestClient();
        await SendAsync(rest, HttpMethod.Get, $"/invites/{"abc123"}");

        discord.Recorded.ShouldHaveRequestedOnce("GET /invites/abc123");
        discord.Recorded.ShouldNotHaveRequested("GET /invites/ABC123");
    }

    [Fact]
    public async Task ShouldNotHaveRequested_WellFormedButWrongId_MatchesNothingAndPasses()
    {
        await using var discord = CreateFake();
        var channel = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();
        await SendAsync(rest, HttpMethod.Post, $"/channels/{channel.Id}/messages", "{}");

        // Documented limit of the shape checks (ADR 0012): the ID is plausible, so nothing flags it.
        discord.Recorded.ShouldNotHaveRequested($"POST /channels/{channel.Id + 1}/messages");
    }

    [Fact]
    public async Task ShouldHaveRequested_MethodNotAllowedAttempt_IsNotCountedForAnInventoriedMethod()
    {
        await using var discord = CreateFake();
        using var rest = discord.CreateRestClient();
        await SendAsync(rest, HttpMethod.Put, $"/users/@me");

        Assert.Equal(HttpStatusCode.MethodNotAllowed, Assert.Single(discord.Recorded.Requests).StatusCode);
        discord.Recorded.ShouldNotHaveRequested("GET /users/@me");
        discord.Recorded.ShouldNotHaveRequested("PATCH /users/@me");
    }

    [Fact]
    public async Task Resolution_FakeTableAndDefaultTable_AcceptTheSamePatternsOverTheSameInventory()
    {
        await using var discord = CreateFake();
        IReadOnlyList<RecordedRestRequest> empty = [];

        empty.ShouldNotHaveRequested("GET /guilds/{guild_id}");
        discord.Recorded.ShouldNotHaveRequested("GET /guilds/{guild_id}");
        Assert.Throws<ArgumentException>(() => empty.ShouldNotHaveRequested("GET /guilds/{id}"));
        Assert.Throws<ArgumentException>(() => discord.Recorded.ShouldNotHaveRequested("GET /guilds/{id}"));
    }
}
