using System.Net;
using NetCord.Rest;
using Xunit;

namespace Enx.NetCord.Testing.Tests.Facade;

public sealed class FakeDiscordLifetimeTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task DisposeAsync_CalledTwice_IsIdempotent()
    {
        var discord = FakeDiscord.Create();

        await discord.DisposeAsync();
        await discord.DisposeAsync();
    }

    [Fact]
    public async Task Seed_AfterDispose_ThrowsObjectDisposedException()
    {
        var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var channel = guild.TextChannel("general");
        var alice = guild.Member("alice");
        await discord.DisposeAsync();

        Assert.Throws<ObjectDisposedException>(() => discord.Seed.Guild("Late"));
        Assert.Throws<ObjectDisposedException>(() => discord.Seed.User("late"));
        Assert.Throws<ObjectDisposedException>(() => guild.TextChannel("late"));
        Assert.Throws<ObjectDisposedException>(() => guild.Role("late"));
        Assert.Throws<ObjectDisposedException>(() => guild.Member("late"));
        Assert.Throws<ObjectDisposedException>(() => guild.Member(alice));
        Assert.Throws<ObjectDisposedException>(() => channel.Message("late", alice));
    }

    [Fact]
    public async Task CreateRestClient_AfterDispose_ThrowsObjectDisposedException()
    {
        var discord = FakeDiscord.Create();
        await discord.DisposeAsync();

        Assert.Throws<ObjectDisposedException>(() => discord.CreateRestClient());
    }

    [Fact]
    public async Task SendRequest_ThroughAnOlderClientAfterDispose_ThrowsObjectDisposedException()
    {
        var discord = FacadeTestSupport.CreateWithCurrentUserStandIn();
        using var rest = discord.CreateRestClient();
        Assert.NotNull(await rest.GetCurrentUserAsync(cancellationToken: Ct));
        await discord.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => rest.GetCurrentUserAsync(cancellationToken: Ct));
    }

    [Fact]
    public async Task Intercept_ThroughAReferenceKeptBeforeDispose_ThrowsObjectDisposedException()
    {
        var discord = FacadeTestSupport.CreateWithCurrentUserStandIn();
        var rest = discord.Rest;
        await discord.DisposeAsync();

        Assert.Throws<ObjectDisposedException>(() => rest.Intercept(FacadeTestSupport.MeKey, RestEffect.Delay(TimeSpan.Zero)));
    }

    [Fact]
    public async Task Dispose_OneClient_DoesNotCloseTheFakeNorAnotherClient()
    {
        await using var discord = FacadeTestSupport.CreateWithCurrentUserStandIn();
        var first = discord.CreateRestClient();
        using var second = discord.CreateRestClient();
        first.Dispose();

        var user = await second.GetCurrentUserAsync(cancellationToken: Ct);

        Assert.Equal(discord.Token.Id, user.Id);
        using var third = discord.CreateRestClient();
        Assert.Equal(discord.Token.Id, (await third.GetCurrentUserAsync(cancellationToken: Ct)).Id);
    }

    [Fact]
    public async Task CreateRestClient_TwoClients_ShareStateAndJournalButNotTransport()
    {
        await using var discord = FacadeTestSupport.CreateWithCurrentUserStandIn();
        using var first = discord.CreateRestClient();
        using var second = discord.CreateRestClient();

        await first.GetCurrentUserAsync(cancellationToken: Ct);
        await second.GetCurrentUserAsync(cancellationToken: Ct);

        var attempts = discord.Journal.Snapshot();
        Assert.Equal(2, attempts.Count);
        Assert.All(attempts, attempt => Assert.Equal(HttpStatusCode.OK, attempt.StatusCode));
        Assert.All(attempts, attempt => Assert.Equal("/users/@me", attempt.RouteTemplate));
    }

    [Fact]
    public async Task Create_TwoFakes_AreIsolatedForRestAndInterception()
    {
        await using var first = FacadeTestSupport.CreateWithCurrentUserStandIn();
        await using var second = FacadeTestSupport.CreateWithCurrentUserStandIn();
        using var firstClient = first.CreateRestClient();
        using var secondClient = second.CreateRestClient();
        using var rule = first.Rest.Intercept(FacadeTestSupport.MeKey, RestEffect.Error(HttpStatusCode.Forbidden, 50013));

        // The rule armed on the first fake does not reach the second fake, nor does the journal.
        Assert.NotNull(await secondClient.GetCurrentUserAsync(cancellationToken: Ct));
        var denied = await Assert.ThrowsAsync<RestException>(() => firstClient.GetCurrentUserAsync(cancellationToken: Ct));

        Assert.Equal(50013, denied.Error!.Code);
        Assert.Single(second.Journal.Snapshot());
        Assert.Single(first.Journal.Snapshot());
    }
}
