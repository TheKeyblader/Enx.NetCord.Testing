using System.Net;
using System.Text;
using System.Text.Json;
using Enx.NetCord.Testing.Rest;
using Enx.NetCord.Testing.Tests.Rest;
using NetCord.Rest;
using Xunit;

namespace Enx.NetCord.Testing.Tests.Recording;

public sealed class RecordedRequestsTests
{
    private const string Me = "GET /users/@me";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Requests_NominalRequest_ExposesMethodPathTemplateParametersBodyAndResponse()
    {
        await using var discord = CreateFake();
        using var rest = discord.CreateRestClient();

        await using var stream = await rest.SendRequestAsync(
            HttpMethod.Post,
            new StringContent("{\"content\":\"hello\"}", Encoding.UTF8, "application/json"),
            $"/channels/123/messages",
            cancellationToken: Ct);

        var post = Assert.Single(discord.Recorded.Requests);
        Assert.Equal("POST", post.Method);
        Assert.Equal("/channels/123/messages", post.Path);
        Assert.Equal("/channels/{channel_id}/messages", post.RouteTemplate);
        Assert.Equal("123", post.RouteParameters["channel_id"]);
        Assert.Empty(post.Query);
        Assert.Equal("hello", post.RequestBody!.Value.GetProperty("content").GetString());
        Assert.Equal(HttpStatusCode.OK, post.StatusCode);
        Assert.Equal("created", post.ResponseBody!.Value.GetProperty("username").GetString());
    }

    [Fact]
    public async Task Requests_RequestWithoutContent_HasNoRequestBody()
    {
        await using var discord = CreateFake();
        using var rest = discord.CreateRestClient();

        await rest.GetCurrentUserAsync(cancellationToken: Ct);

        var request = Assert.Single(discord.Recorded.Requests);
        Assert.Null(request.RequestBody);
        Assert.Equal("/users/@me", request.Path);
        Assert.Equal("/users/@me", request.RouteTemplate);
    }

    [Fact]
    public async Task Requests_ErrorResponse_IsRecordedWithItsStatusAndDiscordBody()
    {
        await using var discord = CreateFake();
        using var rest = discord.CreateRestClient();

        await Assert.ThrowsAsync<RestException>(() => rest.GetChannelAsync(404, cancellationToken: Ct));

        var request = Assert.Single(discord.Recorded.Requests);
        Assert.Equal(HttpStatusCode.NotFound, request.StatusCode);
        Assert.Equal(10003, request.ResponseBody!.Value.GetProperty("code").GetInt32());
        Assert.Equal("404", request.RouteParameters["channel_id"]);
    }

    [Fact]
    public async Task Requests_UnimplementedRouteInLenientMode_RecordsThe404WithCodeZero()
    {
        await using var discord = FakeDiscord.Create();
        using var rest = discord.CreateRestClient();

        await Assert.ThrowsAsync<RestException>(() => rest.GetCurrentUserAsync(cancellationToken: Ct));

        var request = Assert.Single(discord.Recorded.Requests);
        Assert.Equal(HttpStatusCode.NotFound, request.StatusCode);
        Assert.Equal(0, request.ResponseBody!.Value.GetProperty("code").GetInt32());
        Assert.Equal("/users/@me", request.RouteTemplate);
    }

    [Fact]
    public async Task Requests_UnimplementedRouteInStrictMode_RecordsNoStatusAndNoResponse()
    {
        await using var discord = FakeDiscord.Create(new FakeDiscordOptions { UnimplementedRoutes = UnimplementedRouteMode.Strict });
        using var rest = discord.CreateRestClient();

        await Assert.ThrowsAsync<NotSupportedException>(() => rest.GetCurrentUserAsync(cancellationToken: Ct));

        var request = Assert.Single(discord.Recorded.Requests);
        Assert.Null(request.StatusCode);
        Assert.Null(request.ResponseBody);
        Assert.Equal("GET", request.Method);
    }

    [Fact]
    public async Task Requests_HandlerThrows_RecordsTheAttemptWithoutAStatus()
    {
        await using var discord = CreateFake();
        using var rest = discord.CreateRestClient();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => rest.SendRequestAsync(HttpMethod.Patch, $"/users/@me", cancellationToken: Ct));

        var request = Assert.Single(discord.Recorded.Requests);
        Assert.Equal("PATCH", request.Method);
        Assert.Null(request.StatusCode);
        Assert.Null(request.ResponseBody);
    }

    [Fact]
    public async Task Requests_MethodNotAllowed_RecordsThe405ForTheKnownPath()
    {
        await using var discord = CreateFake();
        using var rest = discord.CreateRestClient();

        await Assert.ThrowsAsync<RestException>(
            () => rest.SendRequestAsync(HttpMethod.Put, $"/users/@me", cancellationToken: Ct));

        var request = Assert.Single(discord.Recorded.Requests);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, request.StatusCode);
        Assert.Equal("PUT", request.Method);
        Assert.Equal("/users/@me", request.Path);
    }

    [Fact]
    public async Task Requests_RepeatedQueryName_KeepsPairsInWireOrderAndDecoded()
    {
        await using var discord = CreateFake();
        using var rest = discord.CreateRestClient();

        await rest.SendRequestAsync(HttpMethod.Get, $"/users/@me", "?a=1&b=%20x&a=2", cancellationToken: Ct);

        var request = Assert.Single(discord.Recorded.Requests);
        Assert.Equal([("a", "1"), ("b", " x"), ("a", "2")], request.Query.Select(pair => (pair.Key, pair.Value)));
    }

    [Fact]
    public async Task Requests_EncodedAtSignInPath_IsDecodedOnce()
    {
        await using var discord = CreateFake();
        using var rest = discord.CreateRestClient();

        await rest.SendRequestAsync(HttpMethod.Get, $"/users/%40me", cancellationToken: Ct);

        var request = Assert.Single(discord.Recorded.Requests);
        Assert.Equal("/users/@me", request.Path);
        Assert.Equal("/users/@me", request.RouteTemplate);
    }

    [Fact]
    public async Task Requests_RouteTokenInPathAndQuery_IsMaskedEverywhere()
    {
        const string Secret = "SECRET-webhook-token-value-0123456789abcdefghijklmnop";
        await using var discord = CreateFake();
        using var rest = discord.CreateRestClient();

        await rest.SendRequestAsync(HttpMethod.Get, $"/webhooks/55/{Secret}", "?token=hunter2&wait=true", cancellationToken: Ct);

        var request = Assert.Single(discord.Recorded.Requests);
        Assert.Equal("/webhooks/55/[redacted]", request.Path);
        Assert.Equal("[redacted]", request.RouteParameters["webhook_token"]);
        Assert.Equal("55", request.RouteParameters["webhook_id"]);
        Assert.Equal([("token", "[redacted]"), ("wait", "true")], request.Query.Select(pair => (pair.Key, pair.Value)));
        var everything = string.Join('|', request.Path, string.Join(',', request.RouteParameters.Values), request.ResponseBody?.GetRawText());
        Assert.DoesNotContain(Secret, everything, StringComparison.Ordinal);
        Assert.DoesNotContain(discord.Token.RawToken, everything, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Requests_MultipartRequest_KeepsPayloadJsonAndDropsFileBytes()
    {
        await using var discord = CreateFake();
        using var rest = discord.CreateRestClient();
        using var content = new MultipartFormDataContent
        {
            { new StringContent("{\"content\":\"héllo\"}", Encoding.UTF8, "application/json"), "payload_json" },
            { new ByteArrayContent([0x00, 0xFF, 0x0D, 0x0A, 0x2D, 0x2D, 0x7B]), "files[0]", "a.bin" },
        };

        await rest.SendRequestAsync(HttpMethod.Post, content, $"/channels/123/messages", cancellationToken: Ct);

        var request = Assert.Single(discord.Recorded.Requests);
        Assert.Equal("héllo", request.RequestBody!.Value.GetProperty("content").GetString());
    }

    [Fact]
    public async Task Requests_MultipartWithoutPayloadJson_HasNoRequestBody()
    {
        await using var discord = CreateFake();
        using var rest = discord.CreateRestClient();
        using var content = new MultipartFormDataContent { { new ByteArrayContent([1, 2, 3]), "files[0]", "a.bin" } };

        await rest.SendRequestAsync(HttpMethod.Post, content, $"/channels/123/messages", cancellationToken: Ct);

        Assert.Null(Assert.Single(discord.Recorded.Requests).RequestBody);
    }

    [Fact]
    public async Task Requests_NonJsonBody_IsNullAndDoesNotChangeTheResponse()
    {
        await using var discord = CreateFake();
        using var rest = discord.CreateRestClient();

        await rest.SendRequestAsync(
            HttpMethod.Post, new StringContent("just text", Encoding.UTF8, "text/plain"), $"/channels/123/messages", cancellationToken: Ct);

        var request = Assert.Single(discord.Recorded.Requests);
        Assert.Null(request.RequestBody);
        Assert.Equal(HttpStatusCode.OK, request.StatusCode);
    }

    [Fact]
    public async Task Requests_UnreadableJsonBody_IsNullAndDoesNotChangeTheResponse()
    {
        await using var discord = CreateFake();
        using var rest = discord.CreateRestClient();

        await rest.SendRequestAsync(
            HttpMethod.Post, new StringContent("{not json", Encoding.UTF8, "application/json"), $"/channels/123/messages", cancellationToken: Ct);

        var request = Assert.Single(discord.Recorded.Requests);
        Assert.Null(request.RequestBody);
        Assert.Equal(HttpStatusCode.OK, request.StatusCode);
    }

    [Fact]
    public async Task Requests_JsonNullLiteral_IsAJsonElementOfKindNull()
    {
        await using var discord = CreateFake();
        using var rest = discord.CreateRestClient();

        await rest.SendRequestAsync(
            HttpMethod.Post, new StringContent("null", Encoding.UTF8, "application/json"), $"/channels/123/messages", cancellationToken: Ct);

        var body = Assert.Single(discord.Recorded.Requests).RequestBody;
        Assert.NotNull(body);
        Assert.Equal(JsonValueKind.Null, body.Value.ValueKind);
    }

    [Fact]
    public async Task Requests_SnapshotTakenBeforeAnAction_IsNotChangedByLaterRequests()
    {
        await using var discord = CreateFake();
        using var rest = discord.CreateRestClient();
        var empty = discord.Recorded.Requests;

        await rest.GetCurrentUserAsync(cancellationToken: Ct);
        var one = discord.Recorded.Requests;
        await rest.GetCurrentUserAsync(cancellationToken: Ct);

        Assert.Empty(empty);
        Assert.Single(one);
        Assert.Equal(2, discord.Recorded.Requests.Count);
        Assert.NotSame(discord.Recorded.Requests, discord.Recorded.Requests);
    }

    [Fact]
    public async Task Requests_SnapshotElements_StayReadableAfterLaterReads()
    {
        await using var discord = CreateFake();
        using var rest = discord.CreateRestClient();
        await rest.GetCurrentUserAsync(cancellationToken: Ct);
        var first = Assert.Single(discord.Recorded.Requests);

        for (var i = 0; i < 20; i++)
        {
            _ = discord.Recorded.Requests;
        }

        Assert.Equal("bot", first.ResponseBody!.Value.GetProperty("username").GetString());
    }

    [Fact]
    public async Task Requests_TwoClients_ShareOneJournalInRecordingOrder()
    {
        await using var discord = CreateFake();
        using var first = discord.CreateRestClient();
        using var second = discord.CreateRestClient();

        await first.GetCurrentUserAsync(cancellationToken: Ct);
        await second.SendRequestAsync(HttpMethod.Get, $"/webhooks/1/tok", cancellationToken: Ct);

        Assert.Equal(["/users/@me", "/webhooks/{webhook_id}/{webhook_token}"], discord.Recorded.Requests.Select(r => r.RouteTemplate));
    }

    [Fact]
    public async Task Requests_TwoFakes_HaveIndependentJournals()
    {
        await using var first = CreateFake();
        await using var second = CreateFake();
        using var rest = first.CreateRestClient();

        await rest.GetCurrentUserAsync(cancellationToken: Ct);

        Assert.Single(first.Recorded.Requests);
        Assert.Empty(second.Recorded.Requests);
    }

    [Fact]
    public async Task Requests_AfterDispose_ThrowsObjectDisposedException()
    {
        var discord = CreateFake();
        await discord.DisposeAsync();

        Assert.Throws<ObjectDisposedException>(() => discord.Recorded.Requests);
    }

    private static FakeDiscord CreateFake()
    {
        RouteBinding[] bindings =
        [
            RouteBinding.Of(Me, _ => User("42", "bot")),
            RouteBinding.Of("GET /channels/{channel_id}", _ => DiscordError.UnknownChannel()),
            RouteBinding.Of("POST /channels/{channel_id}/messages", _ => User("1", "created")),
            RouteBinding.Of("GET /webhooks/{webhook_id}/{webhook_token}", _ => User("1", "webhook")),
            new RouteBinding("PATCH /users/@me", (_, _) => throw new InvalidOperationException("boom")),
        ];
        return FakeDiscord.Create(null, RouteTable.Create(RouteInventory.All, bindings));
    }

    private static RestResult User(string id, string username)
        => RestResult.Ok(new TestUserDto(id, username), TestJsonContext.Default.TestUserDto);
}
