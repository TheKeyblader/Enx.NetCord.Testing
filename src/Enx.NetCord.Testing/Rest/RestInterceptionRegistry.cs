namespace Enx.NetCord.Testing.Rest;

/// <summary>Holds the pending one-shot effects of one fake, keyed by <c>METHOD /template</c>.</summary>
internal sealed class RestInterceptionRegistry(RouteTable routes)
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, Registration> _pending = new(StringComparer.Ordinal);
    private bool _closed;

    /// <summary>Gets a value indicating whether the owning fake has been disposed.</summary>
    public bool IsClosed
    {
        get
        {
            lock (_gate)
            {
                return _closed;
            }
        }
    }

    /// <summary>Arms an effect for the next eligible attempt on an implemented operation.</summary>
    /// <param name="route">The exact operation key.</param>
    /// <param name="effect">The effect.</param>
    /// <returns>A registration whose disposal disarms the effect if it was not consumed.</returns>
    /// <exception cref="ObjectDisposedException">The fake has been disposed.</exception>
    /// <exception cref="ArgumentException">The key is not an implemented operation of the route table.</exception>
    /// <exception cref="InvalidOperationException">Another effect is already pending for the key.</exception>
    public IDisposable Arm(string route, RestEffect effect)
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(effect);

        lock (_gate)
        {
            if (_closed)
            {
                throw new ObjectDisposedException(RestPipelineContext.DisposedObjectName);
            }

            if (!routes.TryGetEntry(route, out var entry) || entry.Handler is null)
            {
                throw new ArgumentException(
                    $"'{route}' is not an implemented REST operation; use the exact 'METHOD /template' key of an operation the fake implements.",
                    nameof(route));
            }

            if (_pending.ContainsKey(route))
            {
                throw new InvalidOperationException($"An effect is already pending for '{route}'.");
            }

            var registration = new Registration(this, route, effect);
            _pending[route] = registration;
            return registration;
        }
    }

    /// <summary>Atomically takes and removes the pending effect for a key, if any.</summary>
    /// <param name="key">The operation key.</param>
    /// <returns>The effect, or <see langword="null"/>.</returns>
    public RestEffect? TryTake(string key)
    {
        lock (_gate)
        {
            if (_closed || !_pending.Remove(key, out var registration))
            {
                return null;
            }

            return registration.Effect;
        }
    }

    /// <summary>Closes the registry: pending effects are dropped and new ones are refused.</summary>
    public void Close()
    {
        lock (_gate)
        {
            _closed = true;
            _pending.Clear();
        }
    }

    private void Disarm(Registration registration)
    {
        lock (_gate)
        {
            if (_pending.TryGetValue(registration.Key, out var current) && ReferenceEquals(current, registration))
            {
                _pending.Remove(registration.Key);
            }
        }
    }

    private sealed class Registration(RestInterceptionRegistry owner, string key, RestEffect effect) : IDisposable
    {
        public string Key { get; } = key;

        public RestEffect Effect { get; } = effect;

        public void Dispose() => owner.Disarm(this);
    }
}
