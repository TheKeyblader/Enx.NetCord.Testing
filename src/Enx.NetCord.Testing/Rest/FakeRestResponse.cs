using System.Net;

namespace Enx.NetCord.Testing.Rest;

/// <summary>A response ready to be put on the wire, with the headers the pipeline adds.</summary>
internal sealed class FakeRestResponse
{
    internal FakeRestResponse(HttpStatusCode status, byte[]? body)
    {
        Status = status;
        Body = body;
    }

    /// <summary>Gets the HTTP status.</summary>
    public HttpStatusCode Status { get; }

    /// <summary>Gets the JSON body, or <see langword="null"/> when there is none.</summary>
    public byte[]? Body { get; }

    /// <summary>Gets the response headers in emission order (rate limit headers, <c>Allow</c>, <c>Retry-After</c>).</summary>
    public List<KeyValuePair<string, string>> Headers { get; } = [];

    /// <summary>Creates a response from a handler result.</summary>
    /// <param name="result">The result.</param>
    /// <returns>The response.</returns>
    public static FakeRestResponse From(RestResult result) => new(result.Status, result.Body);

    /// <summary>Creates a response from an error.</summary>
    /// <param name="error">The error.</param>
    /// <returns>The response.</returns>
    public static FakeRestResponse From(DiscordError error) => new(error.Status, error.ToJson());
}
