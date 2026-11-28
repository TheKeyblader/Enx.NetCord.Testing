using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Enx.NetCord.Testing.Rest;
using Enx.NetCord.Testing.State;
using Microsoft.Extensions.Time.Testing;
using NetCord;
using NetCord.Rest;

namespace Enx.NetCord.Testing.Tests.Rest;

internal sealed record TestUserDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("username")] string Username);

[JsonSerializable(typeof(TestUserDto))]
internal sealed partial class TestJsonContext : JsonSerializerContext;

internal sealed class RecordingSink : IRestAttemptSink
{
    private readonly List<RestAttempt> _attempts = [];

    public IReadOnlyList<RestAttempt> Attempts
    {
        get
        {
            lock (_attempts)
            {
                return [.. _attempts];
            }
        }
    }

    public void Record(RestAttempt attempt)
    {
        lock (_attempts)
        {
            _attempts.Add(attempt);
        }
    }
}

/// <summary>Builds a pipeline context with stand-in handlers (no real endpoint exists yet) and real NetCord clients.</summary>
internal sealed class RestTestHarness : IDisposable
{
    public const ulong BotId = 42;
    public const string BotRawToken = "NDI.fake.test";
    public static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly List<IDisposable> _owned = [];
    private readonly Dictionary<string, int> _calls = new(StringComparer.Ordinal);
    private readonly Dictionary<string, RestCaller> _callers = new(StringComparer.Ordinal);

    public RestTestHarness(
        UnimplementedRouteMode mode = UnimplementedRouteMode.Lenient,
        TimeProvider? timeProvider = null,
        IEnumerable<RouteBinding>? extraBindings = null,
        bool defaultBindings = true)
    {
        Clock = new FakeTimeProvider(Start);
        Journal = new RecordingSink();
        var bindings = new List<RouteBinding>();
        if (defaultBindings)
        {
            bindings.AddRange(DefaultBindings());
        }

        if (extraBindings is not null)
        {
            bindings.AddRange(extraBindings);
        }

        Context = new RestPipelineContext(
            RouteTable.Create(RouteInventory.All, bindings),
            new FakeDiscordState(timeProvider ?? Clock, new DefaultSnowflakeGenerator()),
            new BotIdentityResolver(BotRawToken, BotId),
            timeProvider ?? Clock,
            mode,
            Journal);
        Rest = new FakeDiscordRest(Context.Interceptions);
    }

    public FakeTimeProvider Clock { get; }

    public RecordingSink Journal { get; }

    public RestPipelineContext Context { get; }

    public FakeDiscordRest Rest { get; }

    public int Calls(string key) => _calls.GetValueOrDefault(key);

    public RestCaller LastCaller(string key) => _callers[key];

    public RestClient CreateClient(string? rawToken = BotRawToken)
    {
        var client = rawToken is null
            ? new RestClient(new RestClientConfiguration
            {
                Hostname = Context.Hostname,
                RequestHandler = new FakeRestRequestHandler(Context),
                RateLimitManager = new NoWaitRateLimitManager(),
            })
            : RestClientFactory.Create(Context, new BotToken(rawToken));
        _owned.Add(client);
        return client;
    }

    public RestClient CreateBearerClient(string rawToken)
    {
        var client = RestClientFactory.Create(Context, new BearerToken(rawToken));
        _owned.Add(client);
        return client;
    }

    public async Task<HttpResponseMessage> SendRawAsync(
        HttpMethod method,
        string pathAndQuery,
        string? authorization = "Bot " + BotRawToken,
        HttpContent? content = null,
        string host = RestPipelineContext.DefaultHostname,
        CancellationToken cancellationToken = default)
    {
        using var handler = new FakeRestRequestHandler(Context);
        if (authorization is not null)
        {
            handler.AddDefaultHeader("Authorization", [authorization]);
        }

        using var request = new HttpRequestMessage(method, $"https://{host}/api/v10{pathAndQuery}") { Content = content };
        return await handler.SendAsync(request, cancellationToken);
    }

    public void Dispose()
    {
        foreach (var owned in _owned)
        {
            owned.Dispose();
        }
    }

    private IEnumerable<RouteBinding> DefaultBindings()
    {
        yield return Bind("GET /users/@me", _ => UserResult("42", "bot"));
        yield return Bind("GET /users/{user_id}", r => UserResult(r.RouteParameters["user_id"], "user"));
        yield return Bind("GET /channels/{channel_id}", r => r.RouteParameters["channel_id"] == "404"
            ? DiscordError.UnknownChannel()
            : UserResult(r.RouteParameters["channel_id"], "channel"));
        yield return Bind("GET /channels/{channel_id}/messages/{message_id}", _ => DiscordError.UnknownMessage());
        yield return Bind(
            "POST /channels/{channel_id}/messages",
            r => UserResult("1", r.GetPayload<string>()),
            ValidateMessage);
        yield return Bind("POST /interactions/{interaction_id}/{interaction_token}/callback", _ => RestResult.NoContent());
        yield return Bind("GET /webhooks/{webhook_id}/{webhook_token}", _ => UserResult("1", "webhook"));
        yield return Bind("PATCH /channels/{channel_id}", _ => DiscordError.MissingPermissions());
    }

    private static RestValidation ValidateMessage(FakeRestRequest request)
    {
        if (request.Body is null)
        {
            return DiscordError.InvalidJson();
        }

        if (!request.Body.TryParseJson(out var document, out var error))
        {
            return error;
        }

        using (document)
        {
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("content", out var content)
                && content.ValueKind == JsonValueKind.String)
            {
                return RestValidation.Valid(content.GetString());
            }
        }

        return DiscordError.InvalidFormBody(
            new FormErrors().Add("content", "BASE_TYPE_REQUIRED", "This field is required"));
    }

    private RouteBinding Bind(string key, Func<FakeRestRequest, RestResult> handler, RestRequestValidator? validator = null)
        => RouteBinding.Of(
            key,
            request =>
            {
                lock (_calls)
                {
                    _calls[key] = _calls.GetValueOrDefault(key) + 1;
                    _callers[key] = request.Caller;
                }

                return handler(request);
            },
            validator);

    private static RestResult UserResult(string id, string username)
        => RestResult.Ok(new TestUserDto(id, username), TestJsonContext.Default.TestUserDto);
}

/// <summary>A request body whose serialization waits for a gate: proves the pipeline reads content before interception.</summary>
internal sealed class GatedContent(byte[] payload) : HttpContent
{
    private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Started => _started.Task;

    public bool Read => _started.Task.IsCompleted;

    public void Release() => _gate.TrySetResult();

    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
    {
        _started.TrySetResult();
        await _gate.Task;
        await stream.WriteAsync(payload);
    }

    protected override bool TryComputeLength(out long length)
    {
        length = -1;
        return false;
    }
}

/// <summary>A request body that records whether it was ever serialized.</summary>
internal sealed class SpyContent(string json) : HttpContent
{
    public bool WasRead { get; private set; }

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
    {
        WasRead = true;
        return stream.WriteAsync(Encoding.UTF8.GetBytes(json)).AsTask();
    }

    protected override bool TryComputeLength(out long length)
    {
        length = Encoding.UTF8.GetByteCount(json);
        return true;
    }
}

/// <summary>Decorates a virtual clock and reports when a timer with a finite due time is armed (ADR 0005 example).</summary>
internal sealed class TimerProbe(TimeProvider clock) : TimeProvider
{
    private readonly TaskCompletionSource _armed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _timers;

    public Task Armed => _armed.Task;

    public int TimerCount => Volatile.Read(ref _timers);

    public override DateTimeOffset GetUtcNow() => clock.GetUtcNow();

    public override long GetTimestamp() => clock.GetTimestamp();

    public override long TimestampFrequency => clock.TimestampFrequency;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = clock.CreateTimer(callback, state, dueTime, period);
        if (dueTime != Timeout.InfiniteTimeSpan)
        {
            Interlocked.Increment(ref _timers);
            _armed.TrySetResult();
        }

        return timer;
    }
}

internal static class TestJson
{
    public static async Task<JsonDocument> ReadAsync(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync());

    public static string Text(HttpResponseMessage response, string name)
        => response.Headers.TryGetValues(name, out var values) ? values.Single() : throw new InvalidOperationException($"Missing header {name}");

    public static string Unix(DateTimeOffset time, int addSeconds = 0)
    {
        var ms = time.ToUnixTimeMilliseconds() + (addSeconds * 1000L);
        return string.Create(CultureInfo.InvariantCulture, $"{ms / 1000}.{ms % 1000:D3}");
    }

    public static HttpStatusCode Status(RestException exception) => exception.StatusCode;
}
