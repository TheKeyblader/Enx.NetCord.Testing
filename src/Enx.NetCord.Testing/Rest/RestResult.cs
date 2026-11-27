using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Enx.NetCord.Testing.Rest;

/// <summary>
/// The business outcome of a handler: a success status with an optional JSON body, or a <see cref="DiscordError"/>.
/// The pipeline turns it into the HTTP response; handlers never build raw responses.
/// </summary>
internal sealed class RestResult
{
    private RestResult(HttpStatusCode status, byte[]? body, DiscordError? error)
    {
        Status = status;
        Body = body;
        Error = error;
    }

    /// <summary>Gets the HTTP status.</summary>
    public HttpStatusCode Status { get; }

    /// <summary>Gets the serialized JSON body, or <see langword="null"/> for none (204).</summary>
    public byte[]? Body { get; }

    /// <summary>Gets the Discord error when the operation failed.</summary>
    public DiscordError? Error { get; }

    /// <summary>Creates a 200 response with a JSON body.</summary>
    /// <typeparam name="T">The wire DTO type.</typeparam>
    /// <param name="value">The detached value.</param>
    /// <param name="typeInfo">Source-generated metadata for <typeparamref name="T"/>.</param>
    /// <returns>The result.</returns>
    public static RestResult Ok<T>(T value, JsonTypeInfo<T> typeInfo) => Json(HttpStatusCode.OK, value, typeInfo);

    /// <summary>Creates a response with the operation's own success status and a JSON body.</summary>
    /// <typeparam name="T">The wire DTO type.</typeparam>
    /// <param name="status">The success status of the operation.</param>
    /// <param name="value">The detached value.</param>
    /// <param name="typeInfo">Source-generated metadata for <typeparamref name="T"/>.</param>
    /// <returns>The result.</returns>
    public static RestResult Json<T>(HttpStatusCode status, T value, JsonTypeInfo<T> typeInfo)
        => new(status, JsonSerializer.SerializeToUtf8Bytes(value, typeInfo), null);

    /// <summary>Creates a 204 response without body.</summary>
    /// <returns>The result.</returns>
    public static RestResult NoContent() => new(HttpStatusCode.NoContent, null, null);

    /// <summary>Creates a failure from the Discord error catalog.</summary>
    /// <param name="error">The error.</param>
    /// <returns>The result.</returns>
    public static RestResult Failure(DiscordError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new(error.Status, error.ToJson(), error);
    }

    /// <summary>Converts an error to a failed result.</summary>
    /// <param name="error">The error.</param>
    public static implicit operator RestResult(DiscordError error) => Failure(error);
}
