using System.Net;
using System.Text;
using System.Text.Json;
using Enx.NetCord.Testing.Rest;
using Enx.NetCord.Testing.State;
using NetCord.Rest;
using Xunit;

namespace Enx.NetCord.Testing.Tests.Rest.Users;

internal static class UsersTestSupport
{
    /// <summary>A harness over the real <see cref="RestRoutes.Bindings"/> (not stand-ins), with the bot user 42 in its state.</summary>
    public static RestTestHarness CreateRealRoutesHarness()
    {
        var harness = new RestTestHarness(defaultBindings: false, extraBindings: RestRoutes.Bindings);
        harness.Context.State.CreateBotIdentity(new CreateBotIdentityInput("bot", Id: RestTestHarness.BotId));
        return harness;
    }

    public static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    public static async Task<JsonDocument> ReadJsonAsync(Task<Stream> response, CancellationToken cancellationToken)
    {
        await using var stream = await response;
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    /// <summary>Returns the field errors NetCord parsed at a path of the <c>errors</c> tree of a 50035 response.</summary>
    public static IReadOnlyList<RestErrorDetail> FieldErrors(RestException exception, params string[] path)
    {
        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Equal(50035, exception.Error!.Code);
        var node = exception.Error.Error!;
        foreach (var name in path)
        {
            node = Assert.IsType<RestErrorGroup>(node).Errors[name];
        }

        return Assert.IsType<RestErrorDetailGroup>(node).Errors;
    }
}
