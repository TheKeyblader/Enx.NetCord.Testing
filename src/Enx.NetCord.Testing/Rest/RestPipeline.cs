using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Enx.NetCord.Testing.Serialization;

namespace Enx.NetCord.Testing.Rest;

/// <summary>
/// The REST pipeline of ADR 0005: resolution, authentication, structural reading, interception, handler,
/// serialization, rate limit headers and one journal entry per call.
/// </summary>
internal sealed class RestPipeline(RestPipelineContext context)
{
    /// <summary>Processes one transport attempt.</summary>
    /// <param name="request">The request NetCord built.</param>
    /// <param name="defaultAuthorization">The client's default <c>Authorization</c> value, used when the request has none.</param>
    /// <param name="cancellationToken">The attempt's cancellation token.</param>
    /// <returns>The HTTP response.</returns>
    /// <exception cref="ObjectDisposedException">The fake has been disposed.</exception>
    /// <exception cref="NotSupportedException">The operation is not implemented and the fake is in strict mode.</exception>
    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, string? defaultAuthorization, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        context.ThrowIfClosed();

        var attempt = new RestAttempt { Method = request.Method.Method.ToUpperInvariant() };
        FakeRestResponse response;
        try
        {
            response = await ProcessAsync(request, defaultAuthorization, attempt, cancellationToken).ConfigureAwait(false);
            if (attempt.Outcome != RestAttemptOutcome.Unimplemented)
            {
                attempt.Outcome = RestAttemptOutcome.Responded;
            }

            attempt.StatusCode = response.Status;
            attempt.ResponseHeaders = [.. response.Headers];
            attempt.ResponseBody = response.Body;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            attempt.Outcome = RestAttemptOutcome.Canceled;
            context.Journal.Record(attempt);
            throw;
        }
        catch (Exception exception)
        {
            attempt.FaultMessage = exception.Message;
            context.Journal.Record(attempt);
            throw;
        }

        context.Journal.Record(attempt);
        return Materialize(request, response);
    }

    private async Task<FakeRestResponse> ProcessAsync(
        HttpRequestMessage request,
        string? defaultAuthorization,
        RestAttempt attempt,
        CancellationToken cancellationToken)
    {
        var target = RestRequestParser.Parse(request.RequestUri, context.Hostname);
        var resolution = context.Routes.Resolve(attempt.Method, target.Segments);

        attempt.Query = RestRedaction.RedactQuery(target.Query);
        if (resolution.Template is { } template)
        {
            attempt.Path = RestRedaction.RedactResolvedPath(template, target.Segments);
            attempt.RouteTemplate = template;
            attempt.RouteParameters = RestRedaction.RedactParameters(resolution.Parameters);
        }
        else
        {
            attempt.Path = RestRedaction.RedactUnresolvedPath(target.Segments);
        }

        switch (resolution.Kind)
        {
            case RouteResolutionKind.NotFound:
                return HandleUnimplemented(attempt, null);

            case RouteResolutionKind.MethodNotAllowed:
                var notAllowed = FakeRestResponse.From(DiscordError.MethodNotAllowed());
                notAllowed.Headers.Add(new("Allow", string.Join(", ", resolution.AllowedMethods)));
                return notAllowed;
        }

        var entry = resolution.Entry!;
        var authorization = request.Headers.TryGetValues("Authorization", out var values)
            ? values.FirstOrDefault()
            : defaultAuthorization;
        var caller = context.Identities.Resolve(authorization);
        if (!caller.Satisfies(entry.Operation.Security))
        {
            return WithOrdinaryHeaders(FakeRestResponse.From(DiscordError.Unauthorized()), entry);
        }

        if (entry.Handler is not { } handler)
        {
            return HandleUnimplemented(attempt, entry);
        }

        var body = await ReadBodyAsync(request, attempt, cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();
        var effect = context.Interceptions.TryTake(entry.Operation.Key);
        if (effect is not null)
        {
            attempt.Effect = effect.Kind;
            switch (effect.Kind)
            {
                case RestEffectKind.Error:
                    return WithOrdinaryHeaders(FakeRestResponse.From(effect.ErrorValue!), entry);

                case RestEffectKind.RateLimited:
                    return CreateRateLimited(entry, effect.RetryAfterSeconds);

                case RestEffectKind.Delay:
                    await DelayAsync(effect.DelayDuration, cancellationToken).ConfigureAwait(false);
                    break;
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        var fakeRequest = new FakeRestRequest(
            entry.Operation,
            resolution.Parameters,
            target.Query,
            body,
            caller,
            context.TimeProvider);
        var result = await handler(fakeRequest, cancellationToken).ConfigureAwait(false);
        return WithOrdinaryHeaders(FakeRestResponse.From(result), entry);
    }

    private FakeRestResponse HandleUnimplemented(RestAttempt attempt, RouteEntry? entry)
    {
        attempt.Outcome = RestAttemptOutcome.Unimplemented;
        if (context.UnimplementedRoutes == UnimplementedRouteMode.Strict)
        {
            var what = entry is not null
                ? $"REST operation '{entry.Operation.Key}'"
                : $"a REST operation matching '{attempt.Method} {attempt.Path}'";
            throw new NotSupportedException($"The fake does not implement {what}.");
        }

        var response = FakeRestResponse.From(DiscordError.NotFound());
        return entry is null ? response : WithOrdinaryHeaders(response, entry);
    }

    private static async Task<RestRequestBody?> ReadBodyAsync(HttpRequestMessage request, RestAttempt attempt, CancellationToken cancellationToken)
    {
        attempt.RequestBodyRead = true;
        if (request.Content is not { } content)
        {
            attempt.RequestBody = [];
            return null;
        }

        var bytes = await content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        var contentType = content.Headers.ContentType?.MediaType;
        attempt.RequestBody = bytes;
        attempt.RequestContentType = contentType;
        return new RestRequestBody(bytes, contentType);
    }

    private async Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken)
    {
        if (duration <= TimeSpan.Zero)
        {
            return;
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // The timer is armed (finite due time) when CreateTimer returns: this is the arming barrier of ADR 0005.
        using var timer = context.TimeProvider.CreateTimer(
            static state => ((TaskCompletionSource)state!).TrySetResult(),
            completion,
            duration,
            Timeout.InfiniteTimeSpan);
        await completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private FakeRestResponse CreateRateLimited(RouteEntry entry, int retryAfterSeconds)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(
            new RateLimitedResponse { Message = "You are being rate limited.", RetryAfter = retryAfterSeconds, Global = false },
            FakeDiscordJsonContext.Default.RateLimitedResponse);
        var response = new FakeRestResponse(HttpStatusCode.TooManyRequests, body);
        RateLimitHeaders.AddRateLimited(response, entry, context.TimeProvider.GetUtcNow(), retryAfterSeconds);
        return response;
    }

    private FakeRestResponse WithOrdinaryHeaders(FakeRestResponse response, RouteEntry entry)
    {
        RateLimitHeaders.AddOrdinary(response, entry, context.TimeProvider.GetUtcNow());
        return response;
    }

    private static HttpResponseMessage Materialize(HttpRequestMessage request, FakeRestResponse response)
    {
        var content = new ByteArrayContent(response.Body ?? []);
        if (response.Body is not null)
        {
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        }

        var message = new HttpResponseMessage(response.Status) { RequestMessage = request, Content = content };
        foreach (var header in response.Headers)
        {
            if (string.Equals(header.Key, "Allow", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var method in header.Value.Split(", "))
                {
                    content.Headers.Allow.Add(method);
                }
            }
            else
            {
                message.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        return message;
    }
}
