using Enx.NetCord.Testing.Rest;

namespace Enx.NetCord.Testing;

/// <summary>Controls REST fault injection for one fake Discord instance.</summary>
public sealed class FakeDiscordRest
{
    private readonly RestInterceptionRegistry _interceptions;

    internal FakeDiscordRest(RestInterceptionRegistry interceptions)
    {
        _interceptions = interceptions;
    }

    /// <summary>
    /// Arms one effect for the next eligible request to an implemented route.
    /// Dispose the returned registration to disarm it before use.
    /// </summary>
    /// <param name="route">The exact operation key, <c>METHOD /template</c>, such as <c>GET /users/@me</c>.</param>
    /// <param name="effect">The effect to apply once.</param>
    /// <returns>A registration that disarms the effect when disposed.</returns>
    /// <exception cref="ObjectDisposedException">The fake has been disposed.</exception>
    /// <exception cref="ArgumentException">The key is not an implemented operation.</exception>
    /// <exception cref="InvalidOperationException">An effect is already pending for the key.</exception>
    public IDisposable Intercept(string route, RestEffect effect) => _interceptions.Arm(route, effect);
}
