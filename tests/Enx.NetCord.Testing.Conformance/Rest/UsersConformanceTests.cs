using System.Net;
using Enx.NetCord.Testing.Conformance.Contract;
using NetCord;
using NetCord.Rest;
using Xunit;

namespace Enx.NetCord.Testing.Conformance.Rest;

/// <summary>Users routes driven through the real NetCord <see cref="RestClient"/>, with every response checked against the OpenAPI contract.</summary>
[Trait("Category", "Conformance")]
public sealed class UsersConformanceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    [Parity("GET /users/@me")]
    public async Task GetCurrentUserAsync_Bot_ReturnsTheBotAndConformsToUserPIIResponse()
    {
        await using var discord = FakeDiscord.Create();
        using var rest = discord.CreateRestClient();

        var user = await rest.GetCurrentUserAsync(cancellationToken: Ct);

        Assert.Equal(discord.ApplicationId, user.Id);
        Assert.False(string.IsNullOrEmpty(user.Username));
        Assert.True(user.IsBot);
        ContractAssert.Conforms(discord, "GET /users/@me");
    }

    [Fact]
    [Parity("PATCH /users/@me")]
    public async Task ModifyCurrentUserAsync_Username_ReturnsTheUpdatedBotAndConformsToUserPIIResponse()
    {
        await using var discord = FakeDiscord.Create();
        using var rest = discord.CreateRestClient();

        var user = await rest.ModifyCurrentUserAsync(o => o.Username = "RenamedBot", cancellationToken: Ct);

        Assert.Equal("RenamedBot", user.Username);
        Assert.Equal("RenamedBot", (await rest.GetCurrentUserAsync(cancellationToken: Ct)).Username);
        ContractAssert.Conforms(discord, "PATCH /users/@me", "GET /users/@me");
    }

    [Fact]
    [Parity("GET /users/{user_id}")]
    public async Task GetUserAsync_SeededUser_ReturnsItAndConformsToUserResponse()
    {
        await using var discord = FakeDiscord.Create();
        var aliceId = discord.Seed.User("alice");
        using var rest = discord.CreateRestClient();

        var user = await rest.GetUserAsync(aliceId, cancellationToken: Ct);

        Assert.Equal(aliceId, user.Id);
        Assert.Equal("alice", user.Username);
        Assert.False(user.IsBot);
        ContractAssert.Conforms(discord, "GET /users/{user_id}");
    }

    [Fact]
    [Parity("GET /users/{user_id}")]
    [Parity("10013")]
    public async Task GetUserAsync_UnknownUser_Returns10013WithTheDiscordErrorEnvelope()
    {
        await using var discord = FakeDiscord.Create();
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.GetUserAsync(123456789, cancellationToken: Ct));

        Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
        Assert.Equal(10013, exception.Error!.Code);
        ContractAssert.Conforms(discord, "GET /users/{user_id}");
    }

    [Fact]
    [Parity("POST /users/@me/channels")]
    public async Task GetDMChannelAsync_SeededUser_ReturnsADmChannelAndConformsToPrivateChannelResponse()
    {
        await using var discord = FakeDiscord.Create();
        var aliceId = discord.Seed.User("alice");
        using var rest = discord.CreateRestClient();

        var channel = await rest.GetDMChannelAsync(aliceId, cancellationToken: Ct);

        Assert.IsType<DMChannel>(channel);
        Assert.Equal(aliceId, Assert.Single(channel.Users.Values).Id);
        ContractAssert.Conforms(discord, "POST /users/@me/channels");
    }

    [Fact]
    [Parity("POST /users/@me/channels")]
    [Parity("10013")]
    public async Task GetDMChannelAsync_UnknownRecipient_Returns10013WithTheDiscordErrorEnvelope()
    {
        await using var discord = FakeDiscord.Create();
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.GetDMChannelAsync(123456789, cancellationToken: Ct));

        Assert.Equal(10013, exception.Error!.Code);
        ContractAssert.Conforms(discord, "POST /users/@me/channels");
    }
}
