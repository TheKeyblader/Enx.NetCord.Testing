using System.Security.Cryptography;
using System.Text;

namespace Enx.NetCord.Testing.Rest;

/// <summary>An inventoried operation with its optional handler and rate limit bucket.</summary>
internal sealed class RouteEntry
{
    internal RouteEntry(RouteOperation operation, RestRouteHandler? handler, RestRequestValidator? validator = null)
    {
        Operation = operation;
        Handler = handler;
        Validator = validator;
        Bucket = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(operation.Key)));
    }

    /// <summary>Gets the operation.</summary>
    public RouteOperation Operation { get; }

    /// <summary>Gets the handler, or <see langword="null"/> when the fake does not implement the operation yet.</summary>
    public RestRouteHandler? Handler { get; }

    /// <summary>Gets the structural validator, or <see langword="null"/> when the operation has none.</summary>
    public RestRequestValidator? Validator { get; }

    /// <summary>Gets the <c>X-RateLimit-Bucket</c> value: lower-case hexadecimal SHA-256 of the UTF-8 operation key.</summary>
    public string Bucket { get; }
}
