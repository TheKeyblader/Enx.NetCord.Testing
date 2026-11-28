using System.Net;
using System.Text.Json;
using Enx.NetCord.Testing.State;
using NetCord;
using NetCord.Rest;
using Xunit;
using static Enx.NetCord.Testing.Tests.Rest.Users.UsersTestSupport;
using static Enx.NetCord.Testing.Tests.State.StateTestSupport;

namespace Enx.NetCord.Testing.Tests.Rest.Users;

public sealed class CurrentUserRoutesTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ImageProperties Image(ImageFormat format, params byte[] data) => new(format, data);

    [Fact]
    [Parity("GET /users/@me")]
    public async Task GetCurrentUserAsync_Bot_ReturnsTheBotIdentityOfTheFake()
    {
        await using var discord = FakeDiscord.Create();
        using var rest = discord.CreateRestClient();

        var user = await rest.GetCurrentUserAsync(cancellationToken: Ct);

        Assert.Equal(discord.Token.Id, user.Id);
        Assert.Equal("FakeBot", user.Username);
        Assert.True(user.IsBot);
        Assert.Equal(0, user.Discriminator);
        Assert.Null(user.AvatarHash);
        var attempt = Assert.Single(discord.Journal.Snapshot());
        Assert.Equal("/users/@me", attempt.RouteTemplate);
        Assert.Equal(HttpStatusCode.OK, attempt.StatusCode);
    }

    [Fact]
    [Parity("GET /users/@me")]
    public async Task GetCurrentUserAsync_Response_HasTheShapeOfUserPIIResponse()
    {
        await using var discord = FakeDiscord.Create();
        using var rest = discord.CreateRestClient();

        using var json = await ReadJsonAsync(rest.SendRequestAsync(HttpMethod.Get, $"/users/@me", cancellationToken: Ct), Ct);

        var root = json.RootElement;
        Assert.Equal(discord.Token.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), root.GetProperty("id").GetString());
        Assert.Equal("0", root.GetProperty("discriminator").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("avatar").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("global_name").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("primary_guild").ValueKind);
        Assert.Equal(0, root.GetProperty("public_flags").GetInt32());
        Assert.Equal(0, root.GetProperty("flags").GetInt64());
        Assert.True(root.GetProperty("bot").GetBoolean());
        Assert.False(root.GetProperty("mfa_enabled").GetBoolean());
        Assert.Equal("en-US", root.GetProperty("locale").GetString());
        Assert.True(root.GetProperty("verified").GetBoolean());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("email").ValueKind);
    }

    [Fact]
    [Parity("GET /users/@me")]
    public async Task GetCurrentUserAsync_PercentEncodedAtMe_StillReachesTheCurrentUser()
    {
        await using var discord = FakeDiscord.Create();
        using var rest = discord.CreateRestClient();

        using var json = await ReadJsonAsync(rest.SendRequestAsync(HttpMethod.Get, $"/users/%40me", cancellationToken: Ct), Ct);

        Assert.Equal("FakeBot", json.RootElement.GetProperty("username").GetString());
    }

    [Fact]
    [Parity("GET /users/@me")]
    [Parity("PATCH /users/@me")]
    [Parity("GET /users/{user_id}")]
    [Parity("POST /users/@me/channels")]
    [Parity("http:401")]
    public async Task UsersRoutes_WithoutCredentialsOrWithAnUnknownToken_Return401()
    {
        using var harness = CreateRealRoutesHarness();
        using var anonymous = harness.CreateClient(rawToken: null);
        using var stranger = harness.CreateClient("NDM.other.token");

        foreach (var client in new[] { anonymous, stranger })
        {
            var attempts = new Func<Task>[]
            {
                () => client.GetCurrentUserAsync(cancellationToken: Ct),
                () => client.ModifyCurrentUserAsync(o => o.Username = "renamed", cancellationToken: Ct),
                () => client.GetUserAsync(RestTestHarness.BotId, cancellationToken: Ct),
                () => client.GetDMChannelAsync(RestTestHarness.BotId, cancellationToken: Ct),
            };
            foreach (var attempt in attempts)
            {
                var exception = await Assert.ThrowsAsync<RestException>(attempt);
                Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
                Assert.Equal(0, exception.Error!.Code);
            }
        }

        Assert.Equal("bot", harness.Context.State.FindUser(RestTestHarness.BotId)!.Username);
    }

    [Fact]
    [Parity("PATCH /users/@me")]
    public async Task ModifyCurrentUserAsync_Username_ReturnsTheUpdatedUserAndIsReadBack()
    {
        await using var discord = FakeDiscord.Create();
        using var rest = discord.CreateRestClient();

        var updated = await rest.ModifyCurrentUserAsync(o => o.Username = "RenamedBot", cancellationToken: Ct);

        Assert.Equal("RenamedBot", updated.Username);
        Assert.Equal(discord.Token.Id, updated.Id);
        Assert.Equal("RenamedBot", (await rest.GetCurrentUserAsync(cancellationToken: Ct)).Username);
        Assert.Equal("RenamedBot", (await rest.GetUserAsync(discord.Token.Id, cancellationToken: Ct)).Username);
        Assert.Equal("RenamedBot", discord.State.FindUser(discord.Token.Id)!.Username);
    }

    [Fact]
    [Parity("PATCH /users/@me")]
    public async Task ModifyCurrentUserAsync_Username_CommitsOneUserUpdatedChange()
    {
        await using var discord = FakeDiscord.Create();
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        await rest.ModifyCurrentUserAsync(o => o.Username = "RenamedBot", cancellationToken: Ct);

        var change = Assert.IsType<UserUpdated>(Assert.Single(Drain(subscription)));
        Assert.Equal(discord.Token.Id, change.User.Id);
        Assert.Equal("RenamedBot", change.User.Username);
    }

    [Fact]
    [Parity("PATCH /users/@me")]
    public async Task ModifyCurrentUserAsync_AvatarAndBanner_StoreDeterministicHashesAndNullRemovesThem()
    {
        await using var discord = FakeDiscord.Create();
        using var rest = discord.CreateRestClient();

        var updated = await rest.ModifyCurrentUserAsync(
            o =>
            {
                o.Avatar = Image(ImageFormat.Png, 1, 2, 3);
                o.Banner = Image(ImageFormat.Gif, 4, 5, 6);
            },
            cancellationToken: Ct);

        Assert.Matches("^[0-9a-f]{32}$", updated.AvatarHash!);
        Assert.Matches("^a_[0-9a-f]{32}$", updated.BannerHash!);
        Assert.Equal("FakeBot", updated.Username);
        var again = await rest.ModifyCurrentUserAsync(o => o.Avatar = Image(ImageFormat.Png, 1, 2, 3), cancellationToken: Ct);
        Assert.Equal(updated.AvatarHash, again.AvatarHash);
        Assert.Equal(updated.BannerHash, again.BannerHash); // untouched by an avatar-only update
        var other = await rest.ModifyCurrentUserAsync(o => o.Avatar = Image(ImageFormat.Png, 9), cancellationToken: Ct);
        Assert.NotEqual(updated.AvatarHash, other.AvatarHash);

        var cleared = await rest.ModifyCurrentUserAsync(
            o =>
            {
                o.Avatar = ImageProperties.Empty;
                o.Banner = ImageProperties.Empty;
            },
            cancellationToken: Ct);

        Assert.Null(cleared.AvatarHash);
        Assert.Null(cleared.BannerHash);
        Assert.Null((await rest.GetCurrentUserAsync(cancellationToken: Ct)).AvatarHash);
    }

    [Fact]
    [Parity("PATCH /users/@me")]
    public async Task ModifyCurrentUserAsync_NoChange_ReturnsTheUserAndCommitsNothing()
    {
        await using var discord = FakeDiscord.Create();
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        var user = await rest.ModifyCurrentUserAsync(_ => { }, cancellationToken: Ct);
        await rest.ModifyCurrentUserAsync(o => o.Username = "FakeBot", cancellationToken: Ct);

        Assert.Equal("FakeBot", user.Username);
        Assert.Empty(Drain(subscription));
    }

    [Theory]
    [Parity("PATCH /users/@me")]
    [Parity("50035")]
    [InlineData("a")]
    [InlineData("")]
    [InlineData("0123456789012345678901234567890123")]
    public async Task ModifyCurrentUserAsync_UsernameOutOfRange_Returns50035AndChangesNothing(string username)
    {
        await using var discord = FakeDiscord.Create();
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        var exception = await Assert.ThrowsAsync<RestException>(
            () => rest.ModifyCurrentUserAsync(o => o.Username = username, cancellationToken: Ct));

        var error = Assert.Single(FieldErrors(exception, "username"));
        Assert.Equal("BASE_TYPE_BAD_LENGTH", error.Code);
        Assert.Equal("Must be between 2 and 32 in length.", error.Message);
        Assert.Equal("FakeBot", (await rest.GetCurrentUserAsync(cancellationToken: Ct)).Username);
        Assert.Empty(Drain(subscription));
    }

    [Fact]
    [Parity("PATCH /users/@me")]
    public async Task ModifyCurrentUserAsync_UsernameLengthCountsCharactersNotUtf16Units_AcceptsTwoEmoji()
    {
        await using var discord = FakeDiscord.Create();
        using var rest = discord.CreateRestClient();

        var user = await rest.ModifyCurrentUserAsync(o => o.Username = "\U0001F600\U0001F600", cancellationToken: Ct);

        Assert.Equal("\U0001F600\U0001F600", user.Username);
    }

    [Fact]
    [Parity("PATCH /users/@me")]
    [Parity("50035")]
    public async Task SendRequest_PatchWithInvalidImageAndWrongTypes_ReportsEveryFieldInOneResponse()
    {
        await using var discord = FakeDiscord.Create();
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(
            () => rest.SendRequestAsync(
                HttpMethod.Patch,
                Json("""{"username":5,"avatar":"not-an-image","banner":7}"""),
                $"/users/@me",
                cancellationToken: Ct));

        Assert.Equal("BASE_TYPE_BAD_TYPE", Assert.Single(FieldErrors(exception, "username")).Code);
        Assert.Equal("IMAGE_INVALID", Assert.Single(FieldErrors(exception, "avatar")).Code);
        Assert.Equal("BASE_TYPE_BAD_TYPE", Assert.Single(FieldErrors(exception, "banner")).Code);
    }

    [Fact]
    [Parity("PATCH /users/@me")]
    [Parity("50035")]
    public async Task SendRequest_PatchWithANonObjectBody_Returns50035AtTheRoot()
    {
        await using var discord = FakeDiscord.Create();
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(
            () => rest.SendRequestAsync(HttpMethod.Patch, Json("[]"), $"/users/@me", cancellationToken: Ct));

        Assert.Equal(50035, exception.Error!.Code);
        Assert.Equal("MODEL_TYPE_CONVERT", Assert.Single(Assert.IsType<RestErrorDetailGroup>(exception.Error.Error).Errors).Code);
    }

    [Fact]
    [Parity("PATCH /users/@me")]
    [Parity("50109")]
    public async Task SendRequest_PatchWithMalformedJson_Returns50109()
    {
        await using var discord = FakeDiscord.Create();
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(
            () => rest.SendRequestAsync(HttpMethod.Patch, Json("{not json"), $"/users/@me", cancellationToken: Ct));

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Equal(50109, exception.Error!.Code);
    }
}
