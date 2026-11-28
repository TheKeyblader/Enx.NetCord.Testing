using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Enx.NetCord.Testing.Rest;
using Enx.NetCord.Testing.State;
using NetCord.Rest;
using Xunit;

namespace Enx.NetCord.Testing.Tests.Rest;

/// <summary>
/// ADR 0004, amendment 04B, § 2, through the real NetCord <see cref="RestClient"/>. No guild or channel route exists yet
/// (M1-REST-02, 06…): stand-in handlers apply the access helper exactly as the real ones must, passing <see cref="StateCaller.Bot"/>.
/// </summary>
public sealed class GuildAccessRestTests
{
    private const string GuildKey = "GET /guilds/{guild_id}";
    private const string RolesKey = "GET /guilds/{guild_id}/roles";
    private const string ChannelKey = "GET /channels/{channel_id}";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ulong Id(FakeRestRequest request, string name) =>
        ulong.Parse(request.RouteParameters[name], CultureInfo.InvariantCulture);

    private static RestResult Answer<T>(StateResult<T> result) =>
        result.IsSuccess ? RestResult.NoContent() : DiscordError.FromStateFailure(result.Failure);

    private static FakeDiscord CreateFake()
    {
        var emptyList = (JsonTypeInfo<object[]>)JsonSerializerOptions.Default.GetTypeInfo(typeof(object[]));
        RouteBinding[] bindings =
        [
            RouteBinding.Of(GuildKey, request => Answer(request.State.GetGuild(StateCaller.Bot, Id(request, "guild_id")))),
            RouteBinding.Of(
                RolesKey,
                request =>
                {
                    var roles = request.State.ListGuildRoles(StateCaller.Bot, Id(request, "guild_id"));
                    return roles.IsSuccess ? RestResult.Ok([], emptyList) : DiscordError.FromStateFailure(roles.Failure);
                }),
            RouteBinding.Of(ChannelKey, request => Answer(request.State.GetChannel(StateCaller.Bot, Id(request, "channel_id")))),
        ];
        return FakeDiscord.Create(null, RouteTable.Create(RouteInventory.All, bindings));
    }

    private static async Task AssertOkAsync(RestClient rest, FormattableString route)
    {
        await using var stream = await rest.SendRequestAsync(HttpMethod.Get, route, cancellationToken: Ct);
        Assert.NotNull(stream);
    }

    [Fact]
    [Parity("10004")]
    [Parity("http:404")]
    public async Task GetGuild_GuildWithoutBot_NetCordSees404With10004()
    {
        await using var discord = CreateFake();
        var elsewhere = discord.Seed.Guild("Elsewhere", withBot: false);
        using var rest = discord.CreateRestClient();

        var hidden = await Assert.ThrowsAsync<RestException>(() => rest.GetGuildAsync(elsewhere.Id, cancellationToken: Ct));

        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        Assert.Equal(10004, hidden.Error!.Code);
        Assert.Equal("Unknown Guild", hidden.Error.Message);
    }

    [Fact]
    [Parity("10004")]
    [Parity("http:404")]
    public async Task GetGuild_UnknownGuild_NetCordSees404With10004()
    {
        await using var discord = CreateFake();
        using var rest = discord.CreateRestClient();

        var unknown = await Assert.ThrowsAsync<RestException>(() => rest.GetGuildAsync(999, cancellationToken: Ct));

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(10004, unknown.Error!.Code);
    }

    [Fact]
    [Parity("50001")]
    [Parity("http:403")]
    public async Task GetGuildRoles_GuildWithoutBot_NetCordSees403With50001()
    {
        await using var discord = CreateFake();
        var elsewhere = discord.Seed.Guild("Elsewhere", withBot: false);
        using var rest = discord.CreateRestClient();

        var denied = await Assert.ThrowsAsync<RestException>(() => rest.GetGuildRolesAsync(elsewhere.Id, cancellationToken: Ct));

        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal(50001, denied.Error!.Code);
        Assert.Equal("Missing Access", denied.Error.Message);
    }

    [Fact]
    [Parity("10004")]
    public async Task GetGuildRoles_UnknownGuild_NetCordSees404With10004()
    {
        await using var discord = CreateFake();
        using var rest = discord.CreateRestClient();

        var unknown = await Assert.ThrowsAsync<RestException>(() => rest.GetGuildRolesAsync(999, cancellationToken: Ct));

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(10004, unknown.Error!.Code);
    }

    [Fact]
    [Parity("50001")]
    [Parity("10003")]
    public async Task GetChannel_ChannelOfAGuildWithoutBot_Is403With50001AndUnknownChannelStays10003()
    {
        await using var discord = CreateFake();
        var channel = discord.Seed.Guild("Elsewhere", withBot: false).TextChannel("general");
        using var rest = discord.CreateRestClient();

        var denied = await Assert.ThrowsAsync<RestException>(() => rest.GetChannelAsync(channel.Id, cancellationToken: Ct));
        var unknown = await Assert.ThrowsAsync<RestException>(() => rest.GetChannelAsync(999, cancellationToken: Ct));

        Assert.Equal((HttpStatusCode.Forbidden, 50001), (denied.StatusCode, denied.Error!.Code));
        Assert.Equal((HttpStatusCode.NotFound, 10003), (unknown.StatusCode, unknown.Error!.Code));
    }

    [Fact]
    public async Task Routes_DefaultSeed_GuildWithTheBotIsAccessible()
    {
        await using var discord = CreateFake();
        var guild = discord.Seed.Guild("Home");
        var channel = guild.TextChannel("general");
        using var rest = discord.CreateRestClient();

        await AssertOkAsync(rest, $"/guilds/{guild.Id}");
        Assert.Empty(await rest.GetGuildRolesAsync(guild.Id, cancellationToken: Ct));
        await AssertOkAsync(rest, $"/channels/{channel.Id}");
    }

    [Fact]
    [Parity("10004")]
    [Parity("50001")]
    public async Task Routes_BotJoinsLaterByApplicationId_TheGuildBecomesAccessible()
    {
        await using var discord = CreateFake();
        var elsewhere = discord.Seed.Guild("Elsewhere", withBot: false);
        var channel = elsewhere.TextChannel("general");
        using var rest = discord.CreateRestClient();
        await Assert.ThrowsAsync<RestException>(() => rest.GetGuildAsync(elsewhere.Id, cancellationToken: Ct));
        await Assert.ThrowsAsync<RestException>(() => rest.GetChannelAsync(channel.Id, cancellationToken: Ct));

        elsewhere.Member(discord.ApplicationId);

        await AssertOkAsync(rest, $"/guilds/{elsewhere.Id}");
        await AssertOkAsync(rest, $"/channels/{channel.Id}");
        Assert.Empty(await rest.GetGuildRolesAsync(elsewhere.Id, cancellationToken: Ct));
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, 10004, "Unknown Guild")]
    [InlineData(HttpStatusCode.Forbidden, 50001, "Missing Access")]
    [Parity("10004")]
    [Parity("50001")]
    public async Task Intercept_GuildAccessErrorOnAGuildOfTheBot_ShortCircuitsOnceWithoutSeedingAnything(
        HttpStatusCode status,
        int code,
        string message)
    {
        await using var discord = CreateFake();
        var home = discord.Seed.Guild("Home");
        using var rest = discord.CreateRestClient();
        using var registration = discord.Rest.Intercept(RolesKey, RestEffect.Error(status, code));

        var injected = await Assert.ThrowsAsync<RestException>(() => rest.GetGuildRolesAsync(home.Id, cancellationToken: Ct));

        Assert.Equal((status, code, message), (injected.StatusCode, injected.Error!.Code, injected.Error.Message));
        Assert.Empty(await rest.GetGuildRolesAsync(home.Id, cancellationToken: Ct)); // one-shot
    }
}
