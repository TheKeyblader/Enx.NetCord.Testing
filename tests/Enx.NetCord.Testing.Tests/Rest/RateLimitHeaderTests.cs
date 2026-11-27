using System.Net;
using System.Security.Cryptography;
using System.Text;
using Enx.NetCord.Testing.Rest;
using Xunit;

namespace Enx.NetCord.Testing.Tests.Rest;

public sealed class RateLimitHeaderTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly string[] Names =
    [
        "X-RateLimit-Bucket", "X-RateLimit-Limit", "X-RateLimit-Remaining", "X-RateLimit-Reset-After", "X-RateLimit-Reset",
    ];

    [Fact]
    public async Task Response_OrdinarySuccess_CarriesTheFiveHeadersWithFixedValues()
    {
        using var harness = new RestTestHarness();

        using var response = await harness.SendRawAsync(HttpMethod.Get, "/users/@me", cancellationToken: Ct);

        var bucket = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("GET /users/@me")));
        Assert.Equal(bucket, TestJson.Text(response, "X-RateLimit-Bucket"));
        Assert.Equal("5", TestJson.Text(response, "X-RateLimit-Limit"));
        Assert.Equal("4", TestJson.Text(response, "X-RateLimit-Remaining"));
        Assert.Equal("0", TestJson.Text(response, "X-RateLimit-Reset-After"));
        Assert.Equal(TestJson.Unix(RestTestHarness.Start), TestJson.Text(response, "X-RateLimit-Reset"));
        Assert.Equal("1767225600.000", TestJson.Text(response, "X-RateLimit-Reset"));
        Assert.False(response.Headers.Contains("Retry-After"));
        Assert.False(response.Headers.Contains("X-RateLimit-Scope"));
        Assert.False(response.Headers.Contains("X-RateLimit-Global"));
    }

    [Fact]
    public async Task Response_AfterTheClockMoves_UsesTheVirtualTimeWithMillisecondPrecision()
    {
        using var harness = new RestTestHarness();
        harness.Clock.Advance(TimeSpan.FromMilliseconds(1234));

        using var response = await harness.SendRawAsync(HttpMethod.Get, "/users/@me", cancellationToken: Ct);

        Assert.Equal("1767225601.234", TestJson.Text(response, "X-RateLimit-Reset"));
    }

    [Fact]
    public async Task Response_BucketIgnoresIdsAndSharesTheTemplate()
    {
        using var harness = new RestTestHarness();

        using var first = await harness.SendRawAsync(HttpMethod.Get, "/users/1", cancellationToken: Ct);
        using var second = await harness.SendRawAsync(HttpMethod.Get, "/users/2", cancellationToken: Ct);

        Assert.Equal(TestJson.Text(first, "X-RateLimit-Bucket"), TestJson.Text(second, "X-RateLimit-Bucket"));
    }

    [Theory]
    [InlineData("/users/@me", false, HttpStatusCode.OK)]
    [InlineData("/channels/404", false, HttpStatusCode.NotFound)]
    [InlineData("/users/@me", true, HttpStatusCode.Unauthorized)]
    [InlineData("/guilds/1", false, HttpStatusCode.NotFound)]
    public async Task Response_ResolvedOperation_CarriesTheHeadersEvenOnErrors(string path, bool anonymous, HttpStatusCode expected)
    {
        using var harness = new RestTestHarness();

        using var response = await harness.SendRawAsync(
            HttpMethod.Get, path, authorization: anonymous ? null : "Bot " + RestTestHarness.BotRawToken, cancellationToken: Ct);

        Assert.Equal(expected, response.StatusCode);
        foreach (var name in Names)
        {
            Assert.True(response.Headers.Contains(name), name);
        }
    }

    [Theory]
    [InlineData("PUT", "/users/@me")]
    [InlineData("GET", "/nothing/here")]
    public async Task Response_NoResolvedOperation_HasNoRateLimitHeaders(string method, string path)
    {
        using var harness = new RestTestHarness();

        using var response = await harness.SendRawAsync(new HttpMethod(method), path, cancellationToken: Ct);

        foreach (var name in Names)
        {
            Assert.False(response.Headers.Contains(name), name);
        }
    }

    [Fact]
    public async Task Burst_TwoHundredRequestsWithoutAdvancingTheClock_AreAllRecorded()
    {
        using var harness = new RestTestHarness();
        using var client = harness.CreateClient();

        for (var i = 0; i < 200; i++)
        {
            await client.GetCurrentUserAsync(cancellationToken: Ct);
        }

        Assert.Equal(200, harness.Journal.Attempts.Count);
    }

    [Fact]
    public void CreateConfiguration_EveryClient_GetsItsOwnNoWaitManagerHandlerAndTheSentinelHostname()
    {
        using var harness = new RestTestHarness();

        var first = RestClientFactory.CreateConfiguration(harness.Context);
        var second = RestClientFactory.CreateConfiguration(harness.Context);

        Assert.IsType<NoWaitRateLimitManager>(first.RateLimitManager);
        Assert.IsType<FakeRestRequestHandler>(first.RequestHandler);
        Assert.Equal(RestPipelineContext.DefaultHostname, first.Hostname);
        Assert.NotSame(first.RateLimitManager, second.RateLimitManager);
        Assert.NotSame(first.RequestHandler, second.RequestHandler);
    }

    [Fact]
    public async Task NoWaitRateLimitManager_AnyNumberOfAcquisitions_AreGrantedImmediately()
    {
        using var manager = new NoWaitRateLimitManager();
        var global = await manager.GetGlobalRateLimiterAsync(Ct);
        var route = await manager.GetRouteRateLimiterAsync(new global::NetCord.Rest.RateLimits.Route(HttpMethod.Get, "/x"), Ct);

        for (var i = 0; i < 1000; i++)
        {
            Assert.False((await global.TryAcquireAsync(Ct)).RateLimited);
            var result = await route.TryAcquireAsync(Ct);
            Assert.False(result.RateLimited);
            Assert.False(result.AlwaysRetry);
        }
    }
}
