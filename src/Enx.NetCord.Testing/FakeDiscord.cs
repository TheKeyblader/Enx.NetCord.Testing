using System.Globalization;
using System.Text;
using Enx.NetCord.Testing.Rest;
using Enx.NetCord.Testing.State;
using NetCord;
using NetCord.Rest;

namespace Enx.NetCord.Testing;

/// <summary>Hosts one isolated in-memory Discord for a test.</summary>
/// <remarks>
/// Each fake owns its state, ID reservations, transports and journal; two fakes never share an object.
/// Dispose it after the requests of the test have completed.
/// </remarks>
public sealed class FakeDiscord : IAsyncDisposable
{
    private const string BotUsername = "FakeBot";
    private const string SyntheticTokenSuffix = ".fake.test";

    private readonly RestPipelineContext _context;
    private int _disposed;

    private FakeDiscord(FakeDiscordState state, RestJournal journal, RestPipelineContext context, BotToken token, ulong applicationId)
    {
        State = state;
        ApplicationId = applicationId;
        Journal = journal;
        _context = context;
        Token = token;
        Seed = new DiscordSeed(this);
        Rest = new FakeDiscordRest(context.Interceptions);
        Recorded = new DiscordRecording(this, journal);
    }

    /// <summary>Gets the ID shared by the bot user and its application.</summary>
    /// <remarks>Remains readable after the fake has been disposed.</remarks>
    public ulong ApplicationId { get; }

    /// <summary>Gets the fixture builder bound to this fake.</summary>
    public DiscordSeed Seed { get; }

    /// <summary>Gets the REST controls bound to this fake.</summary>
    public FakeDiscordRest Rest { get; }

    /// <summary>Gets the observations collected by this fake.</summary>
    public DiscordRecording Recorded { get; }

    /// <summary>Gets the state shared by every transport of this fake.</summary>
    internal FakeDiscordState State { get; }

    /// <summary>Gets the journal of terminal REST attempts.</summary>
    internal RestJournal Journal { get; }

    /// <summary>Gets the token the fake's clients authenticate with.</summary>
    internal BotToken Token { get; }

    /// <summary>Creates a fake with the supplied options or deterministic defaults.</summary>
    /// <param name="options">The options, or <see langword="null"/> for the defaults.</param>
    /// <returns>A new, isolated fake.</returns>
    /// <exception cref="ArgumentException">
    /// The token is rejected by <c>NetCord.BotToken</c>, encodes a zero ID, or disagrees with <see cref="FakeDiscordOptions.ApplicationId"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">The application ID is zero or the unimplemented-route mode is undefined.</exception>
    /// <exception cref="InvalidOperationException">The snowflake generator violates its contract while allocating the bot ID.</exception>
    public static FakeDiscord Create(FakeDiscordOptions? options = null) => Create(options, RouteTable.Default);

    /// <summary>Creates a fake over an explicit route table (tests register handlers that no real endpoint provides yet).</summary>
    /// <param name="options">The options, or <see langword="null"/> for the defaults.</param>
    /// <param name="routes">The route table.</param>
    /// <returns>A new, isolated fake.</returns>
    internal static FakeDiscord Create(FakeDiscordOptions? options, RouteTable routes)
    {
        ArgumentNullException.ThrowIfNull(routes);
        options ??= new FakeDiscordOptions();

        if (!Enum.IsDefined(options.UnimplementedRoutes))
        {
            throw new ArgumentOutOfRangeException(
                nameof(options), options.UnimplementedRoutes, "The unimplemented-route mode is not a defined value.");
        }

        var token = options.BotToken is { } raw ? new BotToken(raw) : null;
        var explicitId = ResolveExplicitId(options, token);

        var state = FakeDiscordState.Create(options);

        // One reservation for the pair: the user and the application share this ID (ADR 0004).
        var identity = state.CreateBotIdentity(new CreateBotIdentityInput(BotUsername, Id: explicitId));
        token ??= new BotToken(CreateSyntheticToken(identity.User.Id));

        var journal = new RestJournal();
        var context = new RestPipelineContext(
            routes,
            state,
            new BotIdentityResolver(token.RawToken, token.Id),
            options.UnimplementedRoutes,
            journal);
        return new FakeDiscord(state, journal, context, token, identity.User.Id);
    }

    /// <summary>Creates a caller-owned NetCord client authenticated against this fake.</summary>
    /// <remarks>
    /// Every client has its own in-memory handler and rate limit manager; they share this fake's state and journal.
    /// Disposing a client neither closes the fake nor affects another client.
    /// </remarks>
    /// <returns>A new <see cref="RestClient"/>; the caller disposes it.</returns>
    /// <exception cref="ObjectDisposedException">The fake has been disposed.</exception>
    public RestClient CreateRestClient()
    {
        ThrowIfDisposed();
        return RestClientFactory.Create(_context, Token);
    }

    /// <summary>Closes the fake and invalidates its transports and builders.</summary>
    /// <remarks>Idempotent. Later seeding, client creation, interception or request through an older client throws <see cref="ObjectDisposedException"/>.</remarks>
    /// <returns>A completed task.</returns>
    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _context.Close();
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>Throws when the fake has been disposed.</summary>
    /// <exception cref="ObjectDisposedException">The fake has been disposed.</exception>
    internal void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            throw new ObjectDisposedException(RestPipelineContext.DisposedObjectName);
        }
    }

    private static ulong? ResolveExplicitId(FakeDiscordOptions options, BotToken? token)
    {
        if (token is not null)
        {
            if (token.Id == 0)
            {
                throw new ArgumentException("The bot token must encode a nonzero ID.", nameof(options));
            }

            if (options.ApplicationId is { } applicationId && applicationId != token.Id)
            {
                throw new ArgumentException(
                    $"ApplicationId {applicationId} disagrees with the ID {token.Id} encoded in the bot token.", nameof(options));
            }

            return token.Id;
        }

        if (options.ApplicationId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "ApplicationId must be nonzero.");
        }

        return options.ApplicationId;
    }

    private static string CreateSyntheticToken(ulong id)
    {
        var digits = Encoding.ASCII.GetBytes(id.ToString(CultureInfo.InvariantCulture));
        return Convert.ToBase64String(digits).TrimEnd('=') + SyntheticTokenSuffix;
    }
}
