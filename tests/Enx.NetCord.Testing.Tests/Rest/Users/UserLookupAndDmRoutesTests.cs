using System.Globalization;
using System.Net;
using System.Text.Json;
using Enx.NetCord.Testing.State;
using NetCord;
using NetCord.Rest;
using Xunit;
using static Enx.NetCord.Testing.Tests.Rest.Users.UsersTestSupport;
using static Enx.NetCord.Testing.Tests.State.StateTestSupport;

namespace Enx.NetCord.Testing.Tests.Rest.Users;

public sealed class UserLookupAndDmRoutesTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    [Parity("GET /users/{user_id}")]
    public async Task GetUserAsync_SeededUser_ReturnsItsProfile()
    {
        await using var discord = FakeDiscord.Create();
        var aliceId = discord.Seed.User("alice");
        using var rest = discord.CreateRestClient();

        var user = await rest.GetUserAsync(aliceId, cancellationToken: Ct);

        Assert.Equal(aliceId, user.Id);
        Assert.Equal("alice", user.Username);
        Assert.False(user.IsBot);
        Assert.Equal(0, user.Discriminator);
        Assert.Null(user.GlobalName);
        Assert.Null(user.AvatarHash);
        var attempt = Assert.Single(discord.Journal.Snapshot());
        Assert.Equal("/users/{user_id}", attempt.RouteTemplate);
    }

    [Fact]
    [Parity("GET /users/{user_id}")]
    public async Task GetUserAsync_Response_HasTheShapeOfUserResponseWithoutAccountFields()
    {
        await using var discord = FakeDiscord.Create();
        var aliceId = discord.Seed.User("alice");
        using var rest = discord.CreateRestClient();

        using var json = await ReadJsonAsync(rest.SendRequestAsync(HttpMethod.Get, $"/users/{aliceId}", cancellationToken: Ct), Ct);

        var root = json.RootElement;
        Assert.Equal(aliceId.ToString(CultureInfo.InvariantCulture), root.GetProperty("id").GetString());
        Assert.Equal("alice", root.GetProperty("username").GetString());
        Assert.Equal("0", root.GetProperty("discriminator").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("avatar").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("global_name").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("primary_guild").ValueKind);
        Assert.False(root.TryGetProperty("bot", out _)); // Discord omits `bot` for humans
        Assert.False(root.TryGetProperty("mfa_enabled", out _)); // account fields belong to /users/@me only
        Assert.False(root.TryGetProperty("locale", out _));
    }

    [Fact]
    [Parity("GET /users/{user_id}")]
    public async Task GetUserAsync_TheBotItself_ReturnsABotUser()
    {
        await using var discord = FakeDiscord.Create();
        using var rest = discord.CreateRestClient();

        var user = await rest.GetUserAsync(discord.Token.Id, cancellationToken: Ct);

        Assert.True(user.IsBot);
        Assert.Equal("FakeBot", user.Username);
    }

    [Fact]
    [Parity("GET /users/{user_id}")]
    [Parity("10013")]
    public async Task GetUserAsync_UnknownUser_Returns404UnknownUser()
    {
        await using var discord = FakeDiscord.Create();
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.GetUserAsync(123456789, cancellationToken: Ct));

        Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
        Assert.Equal(10013, exception.Error!.Code);
        Assert.Equal("Unknown User", exception.Error.Message);
    }

    [Fact]
    [Parity("GET /users/{user_id}")]
    [Parity("50035")]
    public async Task SendRequest_GetUserWithANonSnowflakeId_Returns50035OnUserId()
    {
        await using var discord = FakeDiscord.Create();
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(
            () => rest.SendRequestAsync(HttpMethod.Get, $"/users/abc", cancellationToken: Ct));

        var error = Assert.Single(FieldErrors(exception, "user_id"));
        Assert.Equal("NUMBER_TYPE_COERCE", error.Code);
        Assert.Equal("Value \"abc\" is not snowflake.", error.Message);
    }

    [Fact]
    [Parity("POST /users/@me/channels")]
    public async Task GetDMChannelAsync_SeededUser_CreatesADmWithThatRecipient()
    {
        await using var discord = FakeDiscord.Create();
        var aliceId = discord.Seed.User("alice");
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        var channel = await rest.GetDMChannelAsync(aliceId, cancellationToken: Ct);

        Assert.IsType<DMChannel>(channel);
        var recipient = Assert.Single(channel.Users.Values);
        Assert.Equal(aliceId, recipient.Id);
        Assert.Equal("alice", recipient.Username);
        Assert.Null(channel.LastMessageId);
        var stored = discord.State.FindDmChannel(aliceId)!;
        Assert.Equal(stored.Id, channel.Id);
        Assert.Equal(ChannelKind.Dm, stored.Kind);
        var created = Assert.IsType<ChannelCreated>(Assert.Single(Drain(subscription)));
        Assert.Equal(channel.Id, created.Channel.Id);
    }

    [Fact]
    [Parity("POST /users/@me/channels")]
    public async Task GetDMChannelAsync_CalledTwiceForTheSameUser_ReturnsTheSameChannelWithoutANewChange()
    {
        await using var discord = FakeDiscord.Create();
        var aliceId = discord.Seed.User("alice");
        var bobId = discord.Seed.User("bob");
        using var rest = discord.CreateRestClient();

        var first = await rest.GetDMChannelAsync(aliceId, cancellationToken: Ct);
        using var subscription = discord.State.SubscribeGateway();
        var second = await rest.GetDMChannelAsync(aliceId, cancellationToken: Ct);
        var other = await rest.GetDMChannelAsync(bobId, cancellationToken: Ct);

        Assert.Equal(first.Id, second.Id);
        Assert.NotEqual(first.Id, other.Id);
        Assert.Equal(bobId, Assert.Single(other.Users.Values).Id);
        Assert.Equal(other.Id, Assert.IsType<ChannelCreated>(Assert.Single(Drain(subscription))).Channel.Id);
    }

    [Fact]
    [Parity("POST /users/@me/channels")]
    public async Task SendRequest_CreateDm_Response_HasTheShapeOfPrivateChannelResponse()
    {
        await using var discord = FakeDiscord.Create();
        var aliceId = discord.Seed.User("alice");
        using var rest = discord.CreateRestClient();

        // The recipient is also accepted as a string, the documented snowflake form (NetCord itself sends a number).
        using var json = await ReadJsonAsync(
            rest.SendRequestAsync(HttpMethod.Post, Json($$"""{"recipient_id":"{{aliceId}}"}"""), $"/users/@me/channels", cancellationToken: Ct),
            Ct);

        var root = json.RootElement;
        Assert.Equal(1, root.GetProperty("type").GetInt32());
        Assert.Equal(discord.State.FindDmChannel(aliceId)!.Id.ToString(CultureInfo.InvariantCulture), root.GetProperty("id").GetString());
        Assert.Equal(0, root.GetProperty("flags").GetInt32());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("last_message_id").ValueKind);
        var recipient = Assert.Single(root.GetProperty("recipients").EnumerateArray());
        Assert.Equal(aliceId.ToString(CultureInfo.InvariantCulture), recipient.GetProperty("id").GetString());
        Assert.Equal("alice", recipient.GetProperty("username").GetString());
    }

    [Fact]
    [Parity("POST /users/@me/channels")]
    public async Task GetDMChannelAsync_ChannelWithMessages_ReportsTheLastMessageId()
    {
        await using var discord = FakeDiscord.Create();
        var aliceId = discord.Seed.User("alice");
        using var rest = discord.CreateRestClient();
        var channel = await rest.GetDMChannelAsync(aliceId, cancellationToken: Ct);
        var message = discord.State.CreateMessage(StateCaller.Fixture, new CreateMessageInput(channel.Id, aliceId, "hello")).Value;

        var again = await rest.GetDMChannelAsync(aliceId, cancellationToken: Ct);

        Assert.Equal(channel.Id, again.Id);
        Assert.Equal(message.Id, again.LastMessageId);
    }

    [Fact]
    [Parity("POST /users/@me/channels")]
    [Parity("10013")]
    public async Task GetDMChannelAsync_UnknownRecipient_Returns404UnknownUserAndCreatesNothing()
    {
        await using var discord = FakeDiscord.Create();
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.GetDMChannelAsync(123456789, cancellationToken: Ct));

        Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
        Assert.Equal(10013, exception.Error!.Code);
        Assert.Empty(Drain(subscription));
    }

    [Theory]
    [Parity("POST /users/@me/channels")]
    [Parity("50035")]
    [InlineData("{}", "BASE_TYPE_REQUIRED")]
    [InlineData("""{"recipient_id":null}""", "BASE_TYPE_REQUIRED")]
    [InlineData("""{"recipient_id":"abc"}""", "NUMBER_TYPE_COERCE")]
    [InlineData("""{"recipient_id":true}""", "NUMBER_TYPE_COERCE")]
    [InlineData("""{"recipient_id":1.5}""", "NUMBER_TYPE_COERCE")]
    public async Task SendRequest_CreateDmWithAnInvalidRecipient_Returns50035OnRecipientId(string body, string code)
    {
        await using var discord = FakeDiscord.Create();
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(
            () => rest.SendRequestAsync(HttpMethod.Post, Json(body), $"/users/@me/channels", cancellationToken: Ct));

        Assert.Equal(code, Assert.Single(FieldErrors(exception, "recipient_id")).Code);
    }

    [Fact]
    [Parity("POST /users/@me/channels")]
    [Parity("50109")]
    public async Task SendRequest_CreateDmWithMalformedJson_Returns50109()
    {
        await using var discord = FakeDiscord.Create();
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(
            () => rest.SendRequestAsync(HttpMethod.Post, Json("{"), $"/users/@me/channels", cancellationToken: Ct));

        Assert.Equal(50109, exception.Error!.Code);
    }

    [Fact]
    [Parity("POST /users/@me/channels")]
    [Parity("50025")]
    public async Task CreateGroupDMChannelAsync_WithAccessTokens_IsRefusedBecauseTheFakeKnowsNoOAuth2Grant()
    {
        await using var discord = FakeDiscord.Create();
        var aliceId = discord.Seed.User("alice");
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        var exception = await Assert.ThrowsAsync<RestException>(
            () => rest.CreateGroupDMChannelAsync(
                new GroupDMChannelProperties(["not-a-granted-token"]) { Nicknames = new Dictionary<ulong, string> { [aliceId] = "Al" } },
                cancellationToken: Ct));

        Assert.Equal(HttpStatusCode.Forbidden, exception.StatusCode);
        Assert.Equal(50025, exception.Error!.Code);
        Assert.Empty(Drain(subscription));
    }

    [Fact]
    [Parity("POST /users/@me/channels")]
    [Parity("50035")]
    public async Task SendRequest_CreateGroupDmWithWrongTypes_Returns50035PerField()
    {
        await using var discord = FakeDiscord.Create();
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(
            () => rest.SendRequestAsync(
                HttpMethod.Post,
                Json("""{"access_tokens":["ok",3],"nicks":"x"}"""),
                $"/users/@me/channels",
                cancellationToken: Ct));

        Assert.Equal("BASE_TYPE_BAD_TYPE", Assert.Single(FieldErrors(exception, "access_tokens", "1")).Code);
        Assert.Equal("BASE_TYPE_BAD_TYPE", Assert.Single(FieldErrors(exception, "nicks")).Code);
    }
}
