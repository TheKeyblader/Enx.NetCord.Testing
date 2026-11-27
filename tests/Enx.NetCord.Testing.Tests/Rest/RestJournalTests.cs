using System.Net;
using System.Text;
using Enx.NetCord.Testing.Rest;
using NetCord.Rest;
using Xunit;

namespace Enx.NetCord.Testing.Tests.Rest;

public sealed class RestJournalTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SendAsync_NominalAttempt_ProducesOneDetachedTerminalEntry()
    {
        using var harness = new RestTestHarness();
        using var client = harness.CreateClient();

        await using var stream = await client.SendRequestAsync(
            HttpMethod.Post,
            new StringContent("{\"content\":\"hi\"}", Encoding.UTF8, "application/json"),
            $"/channels/{123UL}/messages",
            "?a=1&a=2&b=%20x",
            cancellationToken: Ct);

        var attempt = Assert.Single(harness.Journal.Attempts);
        Assert.Equal("POST", attempt.Method);
        Assert.Equal("/channels/123/messages", attempt.Path);
        Assert.Equal("/channels/{channel_id}/messages", attempt.RouteTemplate);
        Assert.Equal("123", attempt.RouteParameters["channel_id"]);
        Assert.Equal([("a", "1"), ("a", "2"), ("b", " x")], attempt.Query.Select(p => (p.Key, p.Value)));
        Assert.True(attempt.RequestBodyRead);
        Assert.Equal("{\"content\":\"hi\"}", Encoding.UTF8.GetString(attempt.RequestBody!));
        Assert.Equal(RestAttemptOutcome.Responded, attempt.Outcome);
        Assert.Equal(HttpStatusCode.OK, attempt.StatusCode);
        Assert.Equal("{\"id\":\"1\",\"username\":\"message\"}", Encoding.UTF8.GetString(attempt.ResponseBody!));
        Assert.Contains(attempt.ResponseHeaders, h => h.Key == "X-RateLimit-Bucket");
        Assert.Null(attempt.FaultMessage);
    }

    [Fact]
    public async Task SendAsync_RequestWithoutContent_RecordsAnEmptyBodyDistinctFromNotRead()
    {
        using var harness = new RestTestHarness();
        using var client = harness.CreateClient();

        await client.GetCurrentUserAsync(cancellationToken: Ct);
        using var denied = await harness.SendRawAsync(HttpMethod.Get, "/users/@me", authorization: null, cancellationToken: Ct);

        var attempts = harness.Journal.Attempts;
        Assert.True(attempts[0].RequestBodyRead);
        Assert.Empty(attempts[0].RequestBody!);
        Assert.False(attempts[1].RequestBodyRead);
        Assert.Null(attempts[1].RequestBody);
    }

    [Fact]
    public async Task SendAsync_PreCanceledToken_RecordsACanceledAttemptWithoutAResponse()
    {
        using var harness = new RestTestHarness();
        using var client = harness.CreateClient();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetCurrentUserAsync(cancellationToken: cancellation.Token));

        var attempt = Assert.Single(harness.Journal.Attempts);
        Assert.Equal(RestAttemptOutcome.Canceled, attempt.Outcome);
        Assert.Null(attempt.StatusCode);
        Assert.Equal(0, harness.Calls("GET /users/@me"));
    }

    [Fact]
    public async Task SendAsync_HandlerThrows_RecordsAFaultAndPropagates()
    {
        using var faulty = new RestTestHarness(
            defaultBindings: false,
            extraBindings: [new RouteBinding("GET /users/@me", (_, _) => throw new InvalidOperationException("boom"))]);
        using var client = faulty.CreateClient();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetCurrentUserAsync(cancellationToken: Ct));

        Assert.Equal("boom", exception.Message);
        var attempt = Assert.Single(faulty.Journal.Attempts);
        Assert.Equal(RestAttemptOutcome.Faulted, attempt.Outcome);
        Assert.Null(attempt.StatusCode);
        Assert.Equal("boom", attempt.FaultMessage);
    }

    [Fact]
    public async Task SendAsync_TokensInPathQueryAndHeaders_NeverReachTheJournalOrExceptions()
    {
        const string Secret = "SECRET-webhook-token-value-0123456789abcdefghijklmnop";
        using var harness = new RestTestHarness();
        using var client = harness.CreateClient();

        await using var stream = await client.SendRequestAsync(
            HttpMethod.Get, $"/webhooks/{55UL}/{Secret}", "?token=hunter2&wait=true", cancellationToken: Ct);

        var attempt = Assert.Single(harness.Journal.Attempts);
        Assert.Equal("/webhooks/55/[redacted]", attempt.Path);
        Assert.Equal("/webhooks/{webhook_id}/{webhook_token}", attempt.RouteTemplate);
        Assert.Equal("[redacted]", attempt.RouteParameters["webhook_token"]);
        Assert.Equal("55", attempt.RouteParameters["webhook_id"]);
        Assert.Equal([("token", "[redacted]"), ("wait", "true")], attempt.Query.Select(p => (p.Key, p.Value)));

        var everything = string.Join(
            '|',
            attempt.Path,
            attempt.RouteTemplate,
            string.Join(',', attempt.RouteParameters.Values),
            string.Join(',', attempt.Query.Select(p => p.Value)),
            string.Join(',', attempt.ResponseHeaders.Select(h => h.Value)),
            Encoding.UTF8.GetString(attempt.ResponseBody ?? []),
            attempt.FaultMessage);
        Assert.DoesNotContain(Secret, everything, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2", everything, StringComparison.Ordinal);
        Assert.DoesNotContain(RestTestHarness.BotRawToken, everything, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_UnknownPathWithLongSegment_MasksItInTheJournal()
    {
        using var harness = new RestTestHarness();
        using var client = harness.CreateClient();
        var secret = new string('x', 80);

        await Assert.ThrowsAsync<RestException>(
            () => client.SendRequestAsync(HttpMethod.Get, $"/unknown/{secret}/path", cancellationToken: Ct));

        var attempt = Assert.Single(harness.Journal.Attempts);
        Assert.Equal("/unknown/[redacted]/path", attempt.Path);
        Assert.Empty(attempt.RouteParameters);
        Assert.Null(attempt.RouteTemplate);
    }

    [Fact]
    public async Task SendAsync_RetriedRateLimit_ProducesOneEntryPerTransportCall()
    {
        using var harness = new RestTestHarness();
        using var client = harness.CreateClient();
        using var registration = harness.Rest.Intercept("GET /users/@me", RestEffect.RateLimited(1));

        await client.GetCurrentUserAsync(cancellationToken: Ct);

        Assert.Equal(2, harness.Journal.Attempts.Count);
    }
}
