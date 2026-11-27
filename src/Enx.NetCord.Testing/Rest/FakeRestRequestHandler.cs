using NetCord.Rest;

namespace Enx.NetCord.Testing.Rest;

/// <summary>
/// The in-memory <see cref="IRestRequestHandler"/> given to one NetCord <c>RestClient</c>. Default headers belong to
/// this client; the route table, state and journal belong to the fake. Disposing it affects nothing else.
/// </summary>
internal sealed class FakeRestRequestHandler : IRestRequestHandler
{
    private readonly RestPipelineContext _context;
    private readonly RestPipeline _pipeline;
    private readonly Lock _gate = new();
    private readonly Dictionary<string, string[]> _defaultHeaders = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="FakeRestRequestHandler"/> class.</summary>
    /// <param name="context">The shared pipeline configuration.</param>
    public FakeRestRequestHandler(RestPipelineContext context)
    {
        _context = context;
        _pipeline = new RestPipeline(context);
    }

    /// <inheritdoc />
    public void AddDefaultHeader(string name, IEnumerable<string> values)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(values);

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _defaultHeaders[name] = [.. values];
        }
    }

    /// <inheritdoc />
    public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        string? authorization;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            authorization = _defaultHeaders.TryGetValue("Authorization", out var values) ? values.FirstOrDefault() : null;
        }

        _context.ThrowIfClosed();
        return _pipeline.SendAsync(request, authorization, cancellationToken);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
        }
    }
}
