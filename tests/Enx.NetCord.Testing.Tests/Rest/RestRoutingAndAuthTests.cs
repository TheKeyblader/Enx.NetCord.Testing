using System.Net;
using System.Text.Json;
using Enx.NetCord.Testing.Rest;
using NetCord;
using NetCord.Rest;
using Xunit;

namespace Enx.NetCord.Testing.Tests.Rest;

public sealed class RestRoutingAndAuthTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<string> ReadAsync(Task<Stream> stream)
    {
        await using var s = await stream;
        using var reader = new StreamReader(s);
        return await reader.ReadToEndAsync(Ct);
    }

    [Fact]
    public async Task GetCurrentUser_ThroughRealRestClient_ReturnsHandlerResult()
    {
        using var harness = new RestTestHarness();
        using var client = harness.CreateClient();

        var user = await client.GetCurrentUserAsync(cancellationToken: Ct);

        Assert.Equal(RestTestHarness.BotId, user.Id);
        Assert.Equal("bot", user.Username);
        Assert.Equal(1, harness.Calls("GET /users/@me"));
    }

    [Fact]
    public async Task SendRequest_PercentEncodedAtMe_ResolvesToTheLiteralRoute()
    {
        using var harness = new RestTestHarness();
        using var client = harness.CreateClient();

        var json = await ReadAsync(client.SendRequestAsync(HttpMethod.Get, $"/users/%40me", cancellationToken: Ct));

        Assert.Contains("\"username\":\"bot\"", json, StringComparison.Ordinal);
        Assert.Equal(1, harness.Calls("GET /users/@me"));
        Assert.Equal(0, harness.Calls("GET /users/{user_id}"));
    }

    [Fact]
    public async Task SendRequest_EncodedSlashInParameter_StaysInOneSegment()
    {
        using var harness = new RestTestHarness();
        using var client = harness.CreateClient();

        var json = await ReadAsync(client.SendRequestAsync(HttpMethod.Get, $"/users/{"a%2Fb"}", cancellationToken: Ct));

        Assert.Contains("\"id\":\"a/b\"", json, StringComparison.Ordinal);
        var attempt = Assert.Single(harness.Journal.Attempts);
        Assert.Equal("/users/{user_id}", attempt.RouteTemplate);
        Assert.Equal("a/b", attempt.RouteParameters["user_id"]);
    }

    [Fact]
    public async Task SendRequest_DoubleEncodedAtMe_IsNotTheMeRoute()
    {
        using var harness = new RestTestHarness();
        using var client = harness.CreateClient();

        var json = await ReadAsync(client.SendRequestAsync(HttpMethod.Get, $"/users/%2540me", cancellationToken: Ct));

        Assert.Contains("\"id\":\"%40me\"", json, StringComparison.Ordinal);
        Assert.Equal(0, harness.Calls("GET /users/@me"));
    }

    [Fact]
    [Parity("http:404")]
    [Parity("0")]
    public async Task GetCurrentUser_WhenLiteralRouteIsNotImplemented_DoesNotFallBackToParameterRoute()
    {
        using var harness = new RestTestHarness(defaultBindings: false, extraBindings:
        [
            RouteBinding.Of("GET /users/{user_id}", _ => RestResult.NoContent()),
        ]);
        using var client = harness.CreateClient();

        var exception = await Assert.ThrowsAsync<RestException>(() => client.GetCurrentUserAsync(cancellationToken: Ct));

        Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
        Assert.Equal(0, exception.Error!.Code);
        Assert.Equal("404: Not Found", exception.Error.Message);
    }

    [Fact]
    [Parity("http:405")]
    [Parity("0")]
    public async Task SendRequest_MethodMissingOnKnownPath_Returns405WithAllowInBothModes()
    {
        foreach (var mode in new[] { UnimplementedRouteMode.Lenient, UnimplementedRouteMode.Strict })
        {
            using var harness = new RestTestHarness(mode);
            using var client = harness.CreateClient();

            var exception = await Assert.ThrowsAsync<RestException>(
                () => client.SendRequestAsync(HttpMethod.Put, $"/users/@me", cancellationToken: Ct));
            using var raw = await harness.SendRawAsync(HttpMethod.Put, "/users/@me", cancellationToken: Ct);

            Assert.Equal(HttpStatusCode.MethodNotAllowed, exception.StatusCode);
            Assert.Equal(0, exception.Error!.Code);
            Assert.Equal("405: Method Not Allowed", exception.Error.Message);
            Assert.Equal(["GET", "PATCH"], raw.Content.Headers.Allow.Order());
            Assert.False(raw.Headers.Contains("X-RateLimit-Bucket"));
        }
    }

    [Fact]
    [Parity("http:404")]
    [Parity("0")]
    public async Task SendRequest_UnknownPathInLenientMode_Returns404CodeZero()
    {
        using var harness = new RestTestHarness();
        using var client = harness.CreateClient();

        var exception = await Assert.ThrowsAsync<RestException>(
            () => client.SendRequestAsync(HttpMethod.Get, $"/this-route-does-not-exist", cancellationToken: Ct));

        Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
        Assert.Equal(0, exception.Error!.Code);
        var attempt = Assert.Single(harness.Journal.Attempts);
        Assert.Equal(RestAttemptOutcome.Unimplemented, attempt.Outcome);
        Assert.Equal(HttpStatusCode.NotFound, attempt.StatusCode);
        Assert.Null(attempt.RouteTemplate);
    }

    [Fact]
    [Parity("http:404")]
    public async Task GetGuild_InventoriedButNotImplementedInLenientMode_Returns404CodeZeroAndKeepsUnimplementedMarker()
    {
        using var harness = new RestTestHarness();
        using var client = harness.CreateClient();

        var exception = await Assert.ThrowsAsync<RestException>(() => client.GetGuildAsync(123, cancellationToken: Ct));

        Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
        Assert.Equal(0, exception.Error!.Code);
        var attempt = Assert.Single(harness.Journal.Attempts);
        Assert.Equal(RestAttemptOutcome.Unimplemented, attempt.Outcome);
        Assert.Equal("/guilds/{guild_id}", attempt.RouteTemplate);
        Assert.Equal(404, (int)attempt.StatusCode!.Value);
    }

    [Fact]
    public async Task GetGuild_InStrictMode_ThrowsNotSupportedAfterRecordingWithoutAStatus()
    {
        using var harness = new RestTestHarness(UnimplementedRouteMode.Strict);
        using var client = harness.CreateClient();

        var exception = await Assert.ThrowsAsync<NotSupportedException>(() => client.GetGuildAsync(123, cancellationToken: Ct));

        Assert.Contains("GET /guilds/{guild_id}", exception.Message, StringComparison.Ordinal);
        var attempt = Assert.Single(harness.Journal.Attempts);
        Assert.Equal(RestAttemptOutcome.Unimplemented, attempt.Outcome);
        Assert.Null(attempt.StatusCode);
        Assert.Null(attempt.ResponseBody);
    }

    [Fact]
    public async Task SendRequest_UnknownPathInStrictMode_ThrowsWithTheMaskedPath()
    {
        using var harness = new RestTestHarness(UnimplementedRouteMode.Strict);
        using var client = harness.CreateClient();
        var secret = new string('s', 70);

        var exception = await Assert.ThrowsAsync<NotSupportedException>(
            () => client.SendRequestAsync(HttpMethod.Get, $"/nothing/{secret}/here", cancellationToken: Ct));

        Assert.DoesNotContain(secret, exception.Message, StringComparison.Ordinal);
        Assert.Contains("[redacted]", exception.Message, StringComparison.Ordinal);
        Assert.Contains("GET", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Parity("10003")]
    [Parity("http:404")]
    public async Task GetChannel_HandlerReturnsUnknownChannelInStrictMode_StaysA404WithCode10003()
    {
        using var harness = new RestTestHarness(UnimplementedRouteMode.Strict);
        using var client = harness.CreateClient();

        var exception = await Assert.ThrowsAsync<RestException>(() => client.GetChannelAsync(404, cancellationToken: Ct));

        Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
        Assert.Equal(10003, exception.Error!.Code);
        Assert.Equal("Unknown Channel", exception.Error.Message);
        Assert.Equal(RestAttemptOutcome.Responded, Assert.Single(harness.Journal.Attempts).Outcome);
    }

    [Fact]
    [Parity("http:401")]
    [Parity("0")]
    public async Task GetCurrentUser_WithoutCredentials_Returns401CodeZeroWithoutRunningTheHandler()
    {
        using var harness = new RestTestHarness();
        using var client = harness.CreateClient(rawToken: null);

        var exception = await Assert.ThrowsAsync<RestException>(() => client.GetCurrentUserAsync(cancellationToken: Ct));

        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
        Assert.Equal(0, exception.Error!.Code);
        Assert.Equal("401: Unauthorized", exception.Error.Message);
        Assert.Equal(0, harness.Calls("GET /users/@me"));
    }

    [Fact]
    [Parity("http:401")]
    public async Task GetCurrentUser_WithUnknownBotToken_Returns401()
    {
        using var harness = new RestTestHarness();
        using var client = harness.CreateClient("NDM.other.token");

        var exception = await Assert.ThrowsAsync<RestException>(() => client.GetCurrentUserAsync(cancellationToken: Ct));

        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
        Assert.Equal(0, harness.Calls("GET /users/@me"));
    }

    [Fact]
    [Parity("http:401")]
    public async Task GetCurrentUser_WithBearerPrefixedBotToken_Returns401()
    {
        using var harness = new RestTestHarness();
        using var client = harness.CreateBearerClient(RestTestHarness.BotRawToken);

        var exception = await Assert.ThrowsAsync<RestException>(() => client.GetCurrentUserAsync(cancellationToken: Ct));

        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
        Assert.Equal(0, harness.Calls("GET /users/@me"));
    }

    [Fact]
    public async Task SendRequest_UnauthenticatedOnInventoriedUnimplementedRoute_Returns401EvenInStrictMode()
    {
        using var harness = new RestTestHarness(UnimplementedRouteMode.Strict);
        using var client = harness.CreateClient(rawToken: null);

        var exception = await Assert.ThrowsAsync<RestException>(() => client.GetGuildAsync(1, cancellationToken: Ct));

        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
    }

    [Fact]
    public async Task SendRequest_RejectedByAuth_DoesNotReadTheRequestContent()
    {
        using var harness = new RestTestHarness();
        var content = new SpyContent("{}");

        using var response = await harness.SendRawAsync(
            HttpMethod.Post, "/channels/1/messages", authorization: null, content: content, cancellationToken: Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(content.WasRead);
        Assert.False(Assert.Single(harness.Journal.Attempts).RequestBodyRead);
    }

    [Fact]
    public async Task InteractionCallback_WithoutCredentials_IsAccepted()
    {
        using var harness = new RestTestHarness();
        using var client = harness.CreateClient(rawToken: null);

        await using var stream = await client.SendRequestAsync(
            HttpMethod.Post,
            new StringContent("{\"type\":1}", System.Text.Encoding.UTF8, "application/json"),
            $"/interactions/{1UL}/{"interaction-token"}/callback",
            cancellationToken: Ct);

        Assert.Equal(1, harness.Calls("POST /interactions/{interaction_id}/{interaction_token}/callback"));
    }

    [Fact]
    public async Task InteractionCallback_WithAnUnknownBotToken_IsAcceptedAndTheCallerStaysUnrecognized()
    {
        const string Key = "POST /interactions/{interaction_id}/{interaction_token}/callback";
        using var harness = new RestTestHarness();

        using var response = await harness.SendRawAsync(
            HttpMethod.Post, "/interactions/1/some-token/callback", authorization: "Bot NDM.other.token", cancellationToken: Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(1, harness.Calls(Key));
        Assert.Equal(RestCallerKind.Unrecognized, harness.LastCaller(Key).Kind);
    }

    [Fact]
    public async Task SendRequest_OnlyOAuth2Operation_RefusesTheBotToken()
    {
        using var harness = new RestTestHarness(extraBindings: [RouteBinding.Of("GET /users/@me/guilds/{guild_id}/member", _ => RestResult.NoContent())]);
        using var client = harness.CreateClient();

        var exception = await Assert.ThrowsAsync<RestException>(() => client.GetCurrentUserGuildUserAsync(1, cancellationToken: Ct));

        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
    }

    [Fact]
    public async Task Clients_AuthenticateIndependentlyAndDisposingOneLeavesTheOtherWorking()
    {
        using var harness = new RestTestHarness();
        var good = harness.CreateClient();
        var stranger = harness.CreateClient("NDM.other.token");
        var second = harness.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await Assert.ThrowsAsync<RestException>(() => stranger.GetCurrentUserAsync(cancellationToken: Ct))).StatusCode);
        Assert.Equal(RestTestHarness.BotId, (await good.GetCurrentUserAsync(cancellationToken: Ct)).Id);

        good.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => good.GetCurrentUserAsync(cancellationToken: Ct));
        Assert.Equal(RestTestHarness.BotId, (await second.GetCurrentUserAsync(cancellationToken: Ct)).Id);
    }

    [Fact]
    public async Task SendAsync_RequestAuthorizationHeader_TakesPrecedenceOverTheDefaultHeader()
    {
        using var harness = new RestTestHarness();
        using var handler = new FakeRestRequestHandler(harness.Context);
        handler.AddDefaultHeader("authorization", ["Bot NDM.other.token"]);

        using var denied = await handler.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "https://fake.discord.test/api/v10/users/@me"), Ct);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://fake.discord.test/api/v10/users/@me");
        request.Headers.TryAddWithoutValidation("Authorization", "Bot " + RestTestHarness.BotRawToken);
        using var allowed = await handler.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    [Fact]
    public async Task SendAsync_OtherHostOrNoAbsoluteUri_IsALocalErrorNotANetworkCall()
    {
        using var harness = new RestTestHarness();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => harness.SendRawAsync(HttpMethod.Get, "/users/@me", host: "discord.com", cancellationToken: Ct));

        var attempt = Assert.Single(harness.Journal.Attempts);
        Assert.Equal(RestAttemptOutcome.Faulted, attempt.Outcome);
        Assert.Null(attempt.StatusCode);
        Assert.Contains("discord.com", attempt.FaultMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_AfterTheFakeIsClosed_ThrowsObjectDisposedAndRecordsNothing()
    {
        using var harness = new RestTestHarness();
        using var client = harness.CreateClient();
        harness.Context.Close();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => client.GetCurrentUserAsync(cancellationToken: Ct));

        Assert.Empty(harness.Journal.Attempts);
    }

    [Fact]
    public async Task SendRequest_ResponseIsJsonWithTheUtf8ContentType()
    {
        using var harness = new RestTestHarness();

        using var response = await harness.SendRawAsync(HttpMethod.Get, "/users/@me", cancellationToken: Ct);
        using var document = await TestJson.ReadAsync(response);

        Assert.Equal("application/json", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("42", document.RootElement.GetProperty("id").GetString());
    }
}
