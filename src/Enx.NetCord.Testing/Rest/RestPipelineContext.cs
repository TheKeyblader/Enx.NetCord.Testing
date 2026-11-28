using Enx.NetCord.Testing.State;

namespace Enx.NetCord.Testing.Rest;

/// <summary>
/// The internal configuration shared by every transport of one fake: state, clock, route table, identity, strictness,
/// interception registry and journal sink. <c>FakeDiscord</c> builds one per fake.
/// </summary>
internal sealed class RestPipelineContext
{
    /// <summary>The sentinel hostname given to NetCord clients; it only recognizes requests, nothing is sent to it.</summary>
    public const string DefaultHostname = "fake.discord.test";

    /// <summary>The object name reported by <see cref="ObjectDisposedException"/> after the fake is disposed.</summary>
    public const string DisposedObjectName = "FakeDiscord";

    /// <summary>Initializes a new instance of the <see cref="RestPipelineContext"/> class.</summary>
    /// <param name="routes">The route table.</param>
    /// <param name="state">The fake's state, handed to the route handlers through <see cref="FakeRestRequest.State"/>.</param>
    /// <param name="identities">The resolver mapping an <c>Authorization</c> value to a caller.</param>
    /// <param name="unimplementedRoutes">The policy for routes without a handler.</param>
    /// <param name="journal">The sink receiving terminal attempts, or <see langword="null"/> to discard them.</param>
    /// <param name="hostname">The sentinel hostname.</param>
    public RestPipelineContext(
        RouteTable routes,
        FakeDiscordState state,
        IRestIdentityResolver identities,
        UnimplementedRouteMode unimplementedRoutes = UnimplementedRouteMode.Lenient,
        IRestAttemptSink? journal = null,
        string hostname = DefaultHostname)
    {
        Routes = routes;
        State = state;
        Identities = identities;
        UnimplementedRoutes = unimplementedRoutes;
        Journal = journal ?? NullRestAttemptSink.Instance;
        Hostname = hostname;
        Interceptions = new RestInterceptionRegistry(routes);
    }

    /// <summary>Gets the route table.</summary>
    public RouteTable Routes { get; }

    /// <summary>Gets the state shared by every transport of the fake.</summary>
    public FakeDiscordState State { get; }

    /// <summary>Gets the identity resolver.</summary>
    public IRestIdentityResolver Identities { get; }

    /// <summary>Gets the clock: the single clock of the fake, owned by its state.</summary>
    public TimeProvider TimeProvider => State.Clock;

    /// <summary>Gets the policy for routes without a handler.</summary>
    public UnimplementedRouteMode UnimplementedRoutes { get; }

    /// <summary>Gets the journal sink.</summary>
    public IRestAttemptSink Journal { get; }

    /// <summary>Gets the sentinel hostname.</summary>
    public string Hostname { get; }

    /// <summary>Gets the pending one-shot effects.</summary>
    public RestInterceptionRegistry Interceptions { get; }

    /// <summary>Closes the REST side of the fake: later attempts and registrations throw <see cref="ObjectDisposedException"/>.</summary>
    public void Close() => Interceptions.Close();

    /// <summary>Throws when the fake has been closed.</summary>
    /// <exception cref="ObjectDisposedException">The fake has been disposed.</exception>
    public void ThrowIfClosed()
    {
        if (Interceptions.IsClosed)
        {
            throw new ObjectDisposedException(DisposedObjectName);
        }
    }
}
