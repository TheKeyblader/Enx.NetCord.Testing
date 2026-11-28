using System.Net;
using Enx.NetCord.Testing.Rest;
using NetCord.Rest;
using Xunit;

namespace Enx.NetCord.Testing.Tests.Facade;

public sealed class FakeDiscordRestTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    [Parity("http:404")]
    [Parity("0")]
    public async Task CreateRestClient_UnimplementedRouteInExplicitLenientMode_ReturnsDiscord404WithCodeZero()
    {
        await using var discord = FacadeTestSupport.CreateWithoutHandlers(new FakeDiscordOptions { UnimplementedRoutes = UnimplementedRouteMode.Lenient });
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.GetCurrentUserAsync(cancellationToken: Ct));

        Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
        Assert.Equal(0, exception.Error!.Code);
        var attempt = Assert.Single(discord.Journal.Snapshot());
        Assert.Equal(RestAttemptOutcome.Unimplemented, attempt.Outcome);
        Assert.Equal(HttpStatusCode.NotFound, attempt.StatusCode);
    }

    [Fact]
    public async Task CreateRestClient_DefaultOptions_IsLenient()
    {
        await using var discord = FacadeTestSupport.CreateWithoutHandlers();
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.GetCurrentUserAsync(cancellationToken: Ct));

        // Options not set at all: the default must be Lenient (a 404 with code 0), not Strict (NotSupportedException).
        Assert.Equal(UnimplementedRouteMode.Lenient, new FakeDiscordOptions().UnimplementedRoutes);
        Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
        Assert.Equal(0, exception.Error!.Code);
    }

    [Fact]
    public async Task CreateRestClient_ClientCreatedByTheFacade_IsAuthenticatedAsTheBot()
    {
        await using var discord = FacadeTestSupport.CreateWithCurrentUserStandIn(new FakeDiscordOptions { BotToken = "NDI.fake.test" });
        using var rest = discord.CreateRestClient();

        var user = await rest.GetCurrentUserAsync(cancellationToken: Ct);

        // The handler read the bot user from the state through FakeRestRequest.State, using the caller's identity.
        Assert.Equal(42UL, user.Id);
        Assert.Equal("FakeBot", user.Username);
    }

    [Fact]
    public async Task CreateRestClient_UnimplementedRouteInStrictMode_ThrowsNotSupportedExceptionAndRecordsTheAttempt()
    {
        await using var discord = FacadeTestSupport.CreateWithoutHandlers(new FakeDiscordOptions { UnimplementedRoutes = UnimplementedRouteMode.Strict });
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<NotSupportedException>(() => rest.GetCurrentUserAsync(cancellationToken: Ct));

        Assert.Contains("GET /users/@me", exception.Message, StringComparison.Ordinal);
        var attempt = Assert.Single(discord.Journal.Snapshot());
        Assert.Equal(RestAttemptOutcome.Unimplemented, attempt.Outcome);
        Assert.Null(attempt.StatusCode);
    }

    [Fact]
    public async Task CreateRestClient_UnknownPathInStrictMode_ThrowsNotSupportedException()
    {
        await using var discord = FakeDiscord.Create(new FakeDiscordOptions { UnimplementedRoutes = UnimplementedRouteMode.Strict });
        using var rest = discord.CreateRestClient();

        await Assert.ThrowsAsync<NotSupportedException>(
            () => rest.SendRequestAsync(HttpMethod.Get, $"/this-route-does-not-exist", cancellationToken: Ct));

        Assert.Null(Assert.Single(discord.Journal.Snapshot()).StatusCode);
    }

    [Fact]
    public async Task CreateRestClient_ImplementedRouteInStrictMode_IsNotAffectedByStrictness()
    {
        await using var discord = FacadeTestSupport.CreateWithCurrentUserStandIn(
            new FakeDiscordOptions { UnimplementedRoutes = UnimplementedRouteMode.Strict });
        using var rest = discord.CreateRestClient();

        Assert.NotNull(await rest.GetCurrentUserAsync(cancellationToken: Ct));
    }

    [Fact]
    public async Task Intercept_ThroughTheFacade_AppliesAnErrorOnceThenRestoresNormalHandling()
    {
        await using var discord = FacadeTestSupport.CreateWithCurrentUserStandIn();
        using var rest = discord.CreateRestClient();

        using (discord.Rest.Intercept(FacadeTestSupport.MeKey, RestEffect.Error(HttpStatusCode.Forbidden, 50013)))
        {
            var denied = await Assert.ThrowsAsync<RestException>(() => rest.GetCurrentUserAsync(cancellationToken: Ct));
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            Assert.Equal(50013, denied.Error!.Code);
        }

        Assert.NotNull(await rest.GetCurrentUserAsync(cancellationToken: Ct));
        Assert.Equal(2, discord.Journal.Snapshot().Count);
    }

    [Fact]
    public async Task Intercept_RouteWithoutHandler_ThrowsArgumentException()
    {
        await using var discord = FacadeTestSupport.CreateWithoutHandlers();

        Assert.Throws<ArgumentException>(() => discord.Rest.Intercept(FacadeTestSupport.MeKey, RestEffect.Delay(TimeSpan.Zero)));
    }
}
