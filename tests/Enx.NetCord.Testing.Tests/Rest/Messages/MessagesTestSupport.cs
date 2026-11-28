using System.Net;
using System.Text;
using System.Text.Json;
using NetCord.Rest;
using Xunit;

namespace Enx.NetCord.Testing.Tests.Rest.Messages;

internal static class MessagesTestSupport
{
    public static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    /// <summary>Asserts the status and Discord code NetCord parsed from an error response.</summary>
    public static void AssertError(RestException exception, HttpStatusCode status, int code)
    {
        Assert.Equal(status, exception.StatusCode);
        Assert.Equal(code, exception.Error!.Code);
    }

    /// <summary>Sends a raw JSON request (the route is an interpolated literal, whose arguments NetCord encodes) and parses the response.</summary>
    public static async Task<JsonDocument> SendRawAsync(RestClient rest, HttpMethod method, FormattableString route, string json)
    {
        using var content = Json(json);
        await using var stream = await rest.SendRequestAsync(method, content, route, cancellationToken: Ct);
        return await JsonDocument.ParseAsync(stream, cancellationToken: Ct);
    }

    /// <summary>Sends a raw <c>GET</c> and parses the JSON response.</summary>
    public static async Task<JsonDocument> GetRawAsync(RestClient rest, FormattableString route, string? query = null)
    {
        await using var stream = await rest.SendRequestAsync(HttpMethod.Get, route, query, cancellationToken: Ct);
        return await JsonDocument.ParseAsync(stream, cancellationToken: Ct);
    }

    /// <summary>Sends a raw JSON request that Discord refuses and returns the error NetCord raised.</summary>
    public static async Task<RestException> SendRawFailingAsync(RestClient rest, HttpMethod method, FormattableString route, string json)
    {
        using var content = Json(json);
        return await Assert.ThrowsAsync<RestException>(async () =>
        {
            await using var stream = await rest.SendRequestAsync(method, content, route, cancellationToken: Ct);
        });
    }

    /// <summary>Returns the field errors NetCord parsed at a path of the <c>errors</c> tree of a 50035 response.</summary>
    public static IReadOnlyList<RestErrorDetail> FieldErrors(RestException exception, params string[] path)
        => Users.UsersTestSupport.FieldErrors(exception, path);
}
