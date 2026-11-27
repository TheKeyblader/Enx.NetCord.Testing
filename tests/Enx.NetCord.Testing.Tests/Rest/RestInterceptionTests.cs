using System.Net;
using System.Text;
using Enx.NetCord.Testing.Rest;
using Microsoft.Extensions.Time.Testing;
using NetCord.Rest;
using Xunit;

namespace Enx.NetCord.Testing.Tests.Rest;

public sealed class RestInterceptionTests
{
    private const string Me = "GET /users/@me";

    private static readonly TimeSpan Guard = TimeSpan.FromSeconds(5);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, 50013)]
    [InlineData(HttpStatusCode.InternalServerError, 0)]
    [InlineData(HttpStatusCode.BadGateway, 0)]
    public void Error_SupportedPair_IsCreated(HttpStatusCode status, int code)
    {
        var effect = RestEffect.Error(status, code);

        Assert.Equal(RestEffectKind.Error, effect.Kind);
        Assert.Equal(status, effect.ErrorValue!.Status);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, 0)]
    [InlineData(HttpStatusCode.BadRequest, 50035)]
    [InlineData(HttpStatusCode.TooManyRequests, 0)]
    [InlineData(HttpStatusCode.NotFound, 99999)]
    public void Error_UnknownPairOr50035Or429_ThrowsArgumentException(HttpStatusCode status, int code)
    {
        Assert.Throws<ArgumentException>(() => RestEffect.Error(status, code));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(2_147_483)]
    public void RateLimited_InsideRange_IsCreated(int seconds)
    {
        Assert.Equal(seconds, RestEffect.RateLimited(seconds).RetryAfterSeconds);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2_147_484)]
    public void RateLimited_OutsideRange_Throws(int seconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RestEffect.RateLimited(seconds));
    }

    [Fact]
    public void Delay_FiniteNonNegative_IsCreatedAndOtherValuesThrow()
    {
        Assert.Equal(TimeSpan.Zero, RestEffect.Delay(TimeSpan.Zero).DelayDuration);
        Assert.Equal(TimeSpan.FromDays(1), RestEffect.Delay(TimeSpan.FromDays(1)).DelayDuration);
        Assert.Throws<ArgumentOutOfRangeException>(() => RestEffect.Delay(TimeSpan.FromSeconds(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => RestEffect.Delay(Timeout.InfiniteTimeSpan));
        Assert.Throws<ArgumentOutOfRangeException>(() => RestEffect.Delay(TimeSpan.FromDays(60)));
    }

    [Theory]
    [InlineData("get /users/@me")]
    [InlineData("GET /users/123")]
    [InlineData("GET users/@me")]
    [InlineData("GET /guilds/{guild_id}")]
    [InlineData("GET /nothing")]
    [InlineData("")]
    public void Intercept_KeyNotBoundToAnImplementedOperation_ThrowsArgumentException(string key)
    {
        using var harness = new RestTestHarness();

        Assert.Throws<ArgumentException>(() => harness.Rest.Intercept(key, RestEffect.RateLimited(1)));
    }

    [Fact]
    public void Intercept_SecondPendingRuleOnTheSameKey_ThrowsInvalidOperation()
    {
        using var harness = new RestTestHarness();
        using var first = harness.Rest.Intercept(Me, RestEffect.RateLimited(1));

        Assert.Throws<InvalidOperationException>(() => harness.Rest.Intercept(Me, RestEffect.RateLimited(2)));
    }

    [Fact]
    public async Task Intercept_DisposeBeforeUse_DisarmsAndIsIdempotentWithoutAffectingALaterRule()
    {
        using var harness = new RestTestHarness();
        using var client = harness.CreateClient();
        var first = harness.Rest.Intercept(Me, RestEffect.Error(HttpStatusCode.BadGateway));
        first.Dispose();
        using var second = harness.Rest.Intercept(Me, RestEffect.Error(HttpStatusCode.InternalServerError));
        first.Dispose();

        var exception = await Assert.ThrowsAsync<RestException>(() => client.GetCurrentUserAsync(cancellationToken: Ct));

        Assert.Equal(HttpStatusCode.InternalServerError, exception.StatusCode);
    }

    [Fact]
    public async Task Intercept_DisposedRegistration_LeavesTheNextRequestUntouched()
    {
        using var harness = new RestTestHarness();
        using var client = harness.CreateClient();
        harness.Rest.Intercept(Me, RestEffect.Error(HttpStatusCode.BadGateway)).Dispose();

        var user = await client.GetCurrentUserAsync(cancellationToken: Ct);

        Assert.Equal(RestTestHarness.BotId, user.Id);
    }

    [Fact]
    public async Task Intercept_ConsumedRule_AllowsANewRuleAndDisposeBecomesANoOp()
    {
        using var harness = new RestTestHarness();
        using var client = harness.CreateClient();
        var registration = harness.Rest.Intercept(Me, RestEffect.Error(HttpStatusCode.BadGateway));
        await Assert.ThrowsAsync<RestException>(() => client.GetCurrentUserAsync(cancellationToken: Ct));

        using var again = harness.Rest.Intercept(Me, RestEffect.Error(HttpStatusCode.InternalServerError));
        registration.Dispose();

        var exception = await Assert.ThrowsAsync<RestException>(() => client.GetCurrentUserAsync(cancellationToken: Ct));
        Assert.Equal(HttpStatusCode.InternalServerError, exception.StatusCode);
    }

    [Fact]
    public void Intercept_AfterTheFakeIsClosed_ThrowsObjectDisposedEvenThroughAnOldReference()
    {
        using var harness = new RestTestHarness();
        var rest = harness.Rest;
        harness.Context.Close();

        Assert.Throws<ObjectDisposedException>(() => rest.Intercept(Me, RestEffect.RateLimited(1)));
    }

    [Fact]
    [Parity("50013")]
    [Parity("http:403")]
    public async Task Intercept_Error403With50013_ShortCircuitsBeforeTheHandlerAndIsOneShot()
    {
        using var harness = new RestTestHarness();
        using var client = harness.CreateClient();
        using var registration = harness.Rest.Intercept(Me, RestEffect.Error(HttpStatusCode.Forbidden, 50013));

        var denied = await Assert.ThrowsAsync<RestException>(() => client.GetCurrentUserAsync(cancellationToken: Ct));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal(50013, denied.Error!.Code);
        Assert.Equal(0, harness.Calls(Me));

        var user = await client.GetCurrentUserAsync(cancellationToken: Ct);
        Assert.Equal(RestTestHarness.BotId, user.Id);
        Assert.Equal(1, harness.Calls(Me));
        var attempts = harness.Journal.Attempts;
        Assert.Equal(RestEffectKind.Error, attempts[0].Effect);
        Assert.Null(attempts[1].Effect);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, "500: Internal Server Error")]
    [InlineData(HttpStatusCode.BadGateway, "502: Bad Gateway")]
    [Parity("http:5xx")]
    public async Task Intercept_ServerError_IsSeenByNetCordAsRestExceptionWithCodeZero(HttpStatusCode status, string message)
    {
        using var harness = new RestTestHarness();
        using var client = harness.CreateClient();
        using var registration = harness.Rest.Intercept(Me, RestEffect.Error(status));

        var exception = await Assert.ThrowsAsync<RestException>(() => client.GetCurrentUserAsync(cancellationToken: Ct));

        Assert.Equal(status, exception.StatusCode);
        Assert.Equal(0, exception.Error!.Code);
        Assert.Equal(message, exception.Error.Message);
        Assert.Equal(0, harness.Calls(Me));
    }

    [Fact]
    [Parity("http:429")]
    public async Task Intercept_RateLimitedWithNoRetry_ThrowsRestRateLimitedExceptionWithResetAfterInMilliseconds()
    {
        using var harness = new RestTestHarness();
        using var client = harness.CreateClient();
        using var registration = harness.Rest.Intercept(Me, RestEffect.RateLimited(2));
        var once = new RestRequestProperties { RateLimitHandling = RestRateLimitHandling.NoRetry };

        var limited = await Assert.ThrowsAsync<RestRateLimitedException>(() => client.GetCurrentUserAsync(once, Ct));

        Assert.Equal(2000, limited.ResetAfter);
        Assert.Equal(RateLimitScope.User, limited.Scope);
        Assert.Equal(0, harness.Calls(Me));
    }

    [Fact]
    [Parity("http:429")]
    public async Task Intercept_RateLimitedWithDefaultRetry_RetriesImmediatelyAndSucceeds()
    {
        using var harness = new RestTestHarness();
        using var client = harness.CreateClient();
        using var registration = harness.Rest.Intercept(Me, RestEffect.RateLimited(5));

        var user = await client.GetCurrentUserAsync(cancellationToken: Ct);

        Assert.Equal(RestTestHarness.BotId, user.Id);
        Assert.Equal(1, harness.Calls(Me));
        var attempts = harness.Journal.Attempts;
        Assert.Equal(2, attempts.Count);
        Assert.Equal(HttpStatusCode.TooManyRequests, attempts[0].StatusCode);
        Assert.Equal(RestEffectKind.RateLimited, attempts[0].Effect);
        Assert.Equal(HttpStatusCode.OK, attempts[1].StatusCode);
        Assert.Null(attempts[1].Effect);
    }

    [Fact]
    [Parity("http:429")]
    public async Task Intercept_RateLimited_WritesTheDiscordBodyAndHeadersForAPerUserLimit()
    {
        using var harness = new RestTestHarness();
        harness.Clock.Advance(TimeSpan.FromMilliseconds(500));
        using var registration = harness.Rest.Intercept(Me, RestEffect.RateLimited(2));

        using var response = await harness.SendRawAsync(HttpMethod.Get, "/users/@me", cancellationToken: Ct);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal(
            "{\"message\":\"You are being rate limited.\",\"retry_after\":2,\"global\":false}",
            await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal("2", TestJson.Text(response, "Retry-After"));
        Assert.Equal("user", TestJson.Text(response, "X-RateLimit-Scope"));
        Assert.Equal("5", TestJson.Text(response, "X-RateLimit-Limit"));
        Assert.Equal("0", TestJson.Text(response, "X-RateLimit-Remaining"));
        Assert.Equal("2", TestJson.Text(response, "X-RateLimit-Reset-After"));
        Assert.Equal("1767225602.500", TestJson.Text(response, "X-RateLimit-Reset"));
        Assert.False(response.Headers.Contains("X-RateLimit-Global"));
        Assert.Equal(64, TestJson.Text(response, "X-RateLimit-Bucket").Length);
    }

    [Fact]
    public async Task Intercept_RuleIsNotConsumedByAnUnauthenticatedOrUnknownRequest()
    {
        using var harness = new RestTestHarness();
        using var registration = harness.Rest.Intercept(Me, RestEffect.Error(HttpStatusCode.BadGateway));

        using var denied = await harness.SendRawAsync(HttpMethod.Get, "/users/@me", authorization: null, cancellationToken: Ct);
        using var other = await harness.SendRawAsync(HttpMethod.Get, "/users/7", cancellationToken: Ct);
        using var effect = await harness.SendRawAsync(HttpMethod.Get, "/users/@me", cancellationToken: Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        Assert.Equal(HttpStatusCode.OK, other.StatusCode);
        Assert.Equal(HttpStatusCode.BadGateway, effect.StatusCode);
    }

    [Fact]
    public async Task Intercept_AppliesToTheNextAttemptOfAnyClientOfTheSameFake()
    {
        using var harness = new RestTestHarness();
        using var first = harness.CreateClient();
        using var second = harness.CreateClient();
        using var registration = harness.Rest.Intercept(Me, RestEffect.Error(HttpStatusCode.BadGateway));

        await Assert.ThrowsAsync<RestException>(() => second.GetCurrentUserAsync(cancellationToken: Ct));

        Assert.Equal(RestTestHarness.BotId, (await first.GetCurrentUserAsync(cancellationToken: Ct)).Id);
    }

    [Fact]
    [Parity("50109")]
    [Parity("50035")]
    public async Task Intercept_InvalidBodyOrFields_AreRejectedBeforeTheRuleIsTakenAndTheNextValidRequestGetsTheEffect()
    {
        const string Key = "POST /channels/{channel_id}/messages";
        using var harness = new RestTestHarness();
        using var client = harness.CreateClient();
        using var registration = harness.Rest.Intercept(Key, RestEffect.Error(HttpStatusCode.BadGateway));

        var malformed = await Assert.ThrowsAsync<RestException>(() => Post(client, "{\"content\":"));
        Assert.Equal(50109, malformed.Error!.Code);

        var missingField = await Assert.ThrowsAsync<RestException>(() => Post(client, "{}"));
        Assert.Equal(50035, missingField.Error!.Code);

        var effect = await Assert.ThrowsAsync<RestException>(() => Post(client, "{\"content\":\"hi\"}"));
        Assert.Equal(HttpStatusCode.BadGateway, effect.StatusCode);
        Assert.Equal(0, harness.Calls(Key));

        await using var stream = await Post(client, "{\"content\":\"hi\"}");
        Assert.Equal(1, harness.Calls(Key));

        var attempts = harness.Journal.Attempts;
        Assert.Equal([null, null, RestEffectKind.Error, null], attempts.Select(a => a.Effect));
        Assert.Equal(
            [HttpStatusCode.BadRequest, HttpStatusCode.BadRequest, HttpStatusCode.BadGateway, HttpStatusCode.OK],
            attempts.Select(a => a.StatusCode!.Value));

        static Task<Stream> Post(global::NetCord.Rest.RestClient client, string json)
            => client.SendRequestAsync(
                HttpMethod.Post, new StringContent(json, Encoding.UTF8, "application/json"), $"/channels/{1UL}/messages", cancellationToken: Ct);
    }

    [Fact]
    public async Task Intercept_RejectedByValidatorWithDelayRule_DoesNotArmATimer()
    {
        var clock = new FakeTimeProvider(RestTestHarness.Start);
        var probe = new TimerProbe(clock);
        using var harness = new RestTestHarness(timeProvider: probe);
        using var client = harness.CreateClient();
        using var registration = harness.Rest.Intercept("POST /channels/{channel_id}/messages", RestEffect.Delay(TimeSpan.FromSeconds(3)));

        var exception = await Assert.ThrowsAsync<RestException>(() => client.SendRequestAsync(
            HttpMethod.Post, new StringContent("{", Encoding.UTF8, "application/json"), $"/channels/{1UL}/messages", cancellationToken: Ct));

        Assert.Equal(50109, exception.Error!.Code);
        Assert.Equal(0, probe.TimerCount);
    }

    [Fact]
    public void GetPayload_WithoutAValidatorPayload_IsABindingBugReportedWithTheOperationKey()
    {
        var request = new FakeRestRequest(
            new RouteOperation("GET", "/x", null, RouteSecurity.Bot),
            new Dictionary<string, string>(),
            [],
            null,
            RestCaller.Anonymous,
            TimeProvider.System);

        var exception = Assert.Throws<InvalidOperationException>(() => request.GetPayload<string>());

        Assert.Contains("GET /x", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Delay_AfterReadingASuspendedBody_ArmsOnlyOnInterceptionAndRunsTheHandlerOnceAtTheDeadline()
    {
        const string Key = "POST /channels/{channel_id}/messages";
        var clock = new FakeTimeProvider(RestTestHarness.Start);
        var probe = new TimerProbe(clock);
        using var harness = new RestTestHarness(timeProvider: probe);
        using var client = harness.CreateClient();
        using var registration = harness.Rest.Intercept(Key, RestEffect.Delay(TimeSpan.FromSeconds(3)));
        var content = new GatedContent(Encoding.UTF8.GetBytes("{\"content\":\"hi\"}"));
        content.Headers.ContentType = new("application/json");

        Assert.False(probe.Armed.IsCompleted);
        var pending = client.SendRequestAsync(HttpMethod.Post, content, $"/channels/{1UL}/messages", cancellationToken: Ct);
        await content.Started.WaitAsync(Guard, Ct);

        Assert.False(probe.Armed.IsCompleted);
        Assert.Equal(0, probe.TimerCount);

        content.Release();
        Assert.Same(probe.Armed, await Task.WhenAny(probe.Armed, pending).WaitAsync(Guard, Ct));
        Assert.False(pending.IsCompleted);
        Assert.Equal(0, harness.Calls(Key));

        clock.Advance(TimeSpan.FromSeconds(2));
        Assert.False(pending.IsCompleted);
        Assert.Equal(0, harness.Calls(Key));

        clock.Advance(TimeSpan.FromSeconds(1));
        await using var stream = await pending.WaitAsync(Guard, Ct);
        Assert.Equal(1, harness.Calls(Key));
        Assert.Equal(1, probe.TimerCount);
        Assert.Equal(RestEffectKind.Delay, Assert.Single(harness.Journal.Attempts).Effect);
    }

    [Fact]
    public async Task Delay_ZeroDuration_RunsTheHandlerWithoutArmingATimer()
    {
        var clock = new FakeTimeProvider(RestTestHarness.Start);
        var probe = new TimerProbe(clock);
        using var harness = new RestTestHarness(timeProvider: probe);
        using var client = harness.CreateClient();
        using var registration = harness.Rest.Intercept(Me, RestEffect.Delay(TimeSpan.Zero));

        await client.GetCurrentUserAsync(cancellationToken: Ct);

        Assert.Equal(0, probe.TimerCount);
        Assert.Equal(1, harness.Calls(Me));
    }

    [Fact]
    public async Task Delay_CanceledWhileWaiting_ConsumesTheRuleAndRecordsCancellation()
    {
        var clock = new FakeTimeProvider(RestTestHarness.Start);
        var probe = new TimerProbe(clock);
        using var harness = new RestTestHarness(timeProvider: probe);
        using var client = harness.CreateClient();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        using var registration = harness.Rest.Intercept(Me, RestEffect.Delay(TimeSpan.FromSeconds(3)));

        var pending = client.GetCurrentUserAsync(cancellationToken: cancellation.Token);
        await probe.Armed.WaitAsync(Guard, Ct);
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(Guard, Ct));
        var attempt = Assert.Single(harness.Journal.Attempts);
        Assert.Equal(RestAttemptOutcome.Canceled, attempt.Outcome);
        Assert.Equal(RestEffectKind.Delay, attempt.Effect);
        Assert.Null(attempt.StatusCode);
        Assert.Equal(0, harness.Calls(Me));

        Assert.Equal(RestTestHarness.BotId, (await client.GetCurrentUserAsync(cancellationToken: Ct)).Id);
        Assert.Equal(1, harness.Calls(Me));
    }
}
