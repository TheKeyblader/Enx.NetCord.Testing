using System.Net;
using Enx.NetCord.Testing.Gateway;
using Enx.NetCord.Testing.Rest;
using Enx.NetCord.Testing.Tests.Rest;
using NetCord.Gateway;
using NetCord.Gateway.Compression;
using NetCord.Rest;
using Xunit;

namespace Enx.NetCord.Testing.Tests.Gateway;

/// <summary>
/// M2-12: <c>FakeDiscord.CreateGatewayClient()</c> before the in-memory Gateway (ADR 0008, § 2). No row of
/// <c>docs/parity</c> covers a client factory: these tests carry no <c>[Parity]</c> key.
/// </summary>
public sealed class CreateGatewayClientTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CreateGatewayClient_RestCall_ReachesTheFakeAndIsRecorded()
    {
        await using var discord = FakeDiscord.Create();
        using var gateway = discord.CreateGatewayClient();

        var user = await gateway.Rest.GetCurrentUserAsync(cancellationToken: Ct);

        Assert.Equal(discord.ApplicationId, user.Id);
        Assert.Equal(discord.ApplicationId, gateway.Id);
        var request = discord.Recorded.ShouldHaveRequestedOnce("GET /users/@me");
        Assert.Equal(HttpStatusCode.OK, request.StatusCode);
    }

    [Fact]
    public async Task CreateGatewayClient_RestCall_SeesTheStateSeededInTheFake()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var gateway = discord.CreateGatewayClient();

        var channel = await gateway.Rest.GetChannelAsync(general.Id, cancellationToken: Ct);

        Assert.Equal(general.Id, channel.Id);
        discord.Recorded.ShouldHaveRequestedOnce($"GET /channels/{general.Id}");
    }

    [Fact]
    public async Task StartAsync_BeforeTheInMemoryGateway_ThrowsNotSupportedExceptionWithoutConnecting()
    {
        await using var discord = FakeDiscord.Create();
        using var gateway = discord.CreateGatewayClient();
        var connecting = 0;
        gateway.Connecting += () =>
        {
            Interlocked.Increment(ref connecting);
            return default;
        };

        var failure = await Assert.ThrowsAsync<NotSupportedException>(() => gateway.StartAsync(cancellationToken: Ct).AsTask());

        Assert.Equal(UnavailableWebSocketConnectionProvider.NotAvailableMessage, failure.Message);
        Assert.Equal(WebSocketStatus.Disconnected, gateway.Status);
        Assert.Equal(0, Volatile.Read(ref connecting));
        Assert.Empty(discord.Recorded.Requests);
    }

    [Fact]
    public async Task StartAsync_CalledAgainAfterTheRefusal_FailsTheSameWayInsteadOfReportingAStartedClient()
    {
        await using var discord = FakeDiscord.Create();
        using var gateway = discord.CreateGatewayClient();
        await Assert.ThrowsAsync<NotSupportedException>(() => gateway.StartAsync(cancellationToken: Ct).AsTask());

        // NetCord resets its state when the connection fails: no retry loop, no "already started".
        await Assert.ThrowsAsync<NotSupportedException>(() => gateway.StartAsync(cancellationToken: Ct).AsTask());
    }

    [Fact]
    public async Task StartAsync_AfterTheFakeIsDisposed_ThrowsObjectDisposedException()
    {
        var discord = FakeDiscord.Create();
        using var gateway = discord.CreateGatewayClient();
        await discord.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => gateway.StartAsync(cancellationToken: Ct).AsTask());
    }

    [Fact]
    public async Task CreateGatewayClient_AfterDispose_ThrowsObjectDisposedException()
    {
        var discord = FakeDiscord.Create();
        await discord.DisposeAsync();

        Assert.Throws<ObjectDisposedException>(() => discord.CreateGatewayClient());
    }

    [Fact]
    public async Task RestCall_ThroughAnOlderGatewayClientAfterDispose_ThrowsObjectDisposedException()
    {
        var discord = FakeDiscord.Create();
        using var gateway = discord.CreateGatewayClient();
        Assert.NotNull(await gateway.Rest.GetCurrentUserAsync(cancellationToken: Ct));
        await discord.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => gateway.Rest.GetCurrentUserAsync(cancellationToken: Ct));
    }

    [Fact]
    public async Task Dispose_OneGatewayClient_DoesNotCloseTheFakeNorAnotherClient()
    {
        await using var discord = FakeDiscord.Create();
        var first = discord.CreateGatewayClient();
        using var second = discord.CreateGatewayClient();
        using var rest = discord.CreateRestClient();
        Assert.NotSame(first.Rest, second.Rest);
        first.Dispose();

        Assert.Equal(discord.ApplicationId, (await second.Rest.GetCurrentUserAsync(cancellationToken: Ct)).Id);
        Assert.Equal(discord.ApplicationId, (await rest.GetCurrentUserAsync(cancellationToken: Ct)).Id);
        using var third = discord.CreateGatewayClient();
        Assert.Equal(discord.ApplicationId, (await third.Rest.GetCurrentUserAsync(cancellationToken: Ct)).Id);
        Assert.Equal(3, discord.Recorded.ShouldHaveRequested("GET /users/@me").Count);
    }

    [Fact]
    public void CreateConfiguration_FakeContext_DisablesCompressionAndWiresEveryTransportToTheFake()
    {
        // GatewayClient exposes neither its compression nor its URI: the configuration is checked directly.
        using var harness = new RestTestHarness();
        var context = harness.Context;

        var configuration = GatewayClientFactory.CreateConfiguration(context);
        var other = GatewayClientFactory.CreateConfiguration(context);

        Assert.IsType<UncompressedGatewayCompression>(configuration.Compression);
        Assert.Equal(RestPipelineContext.DefaultHostname, configuration.Hostname);
        Assert.IsType<UnavailableWebSocketConnectionProvider>(configuration.WebSocketConnectionProvider);
        var rest = Assert.IsType<RestClientConfiguration>(configuration.RestClientConfiguration);
        Assert.Equal(RestPipelineContext.DefaultHostname, rest.Hostname);
        Assert.IsType<FakeRestRequestHandler>(rest.RequestHandler);
        Assert.IsType<NoWaitRateLimitManager>(rest.RateLimitManager);
        Assert.Null(configuration.Intents);
        Assert.Null(configuration.Shard);

        // Nothing is shared between two clients: disposing one cannot break another.
        Assert.NotSame(configuration.Compression, other.Compression);
        Assert.NotSame(configuration.WebSocketConnectionProvider, other.WebSocketConnectionProvider);
        Assert.NotSame(rest.RequestHandler, other.RestClientConfiguration!.RequestHandler);
        Assert.NotSame(rest.RateLimitManager, other.RestClientConfiguration.RateLimitManager);
    }
}
