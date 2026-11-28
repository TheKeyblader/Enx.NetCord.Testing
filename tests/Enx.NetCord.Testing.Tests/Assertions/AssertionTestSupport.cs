using System.Text;
using Enx.NetCord.Testing.Rest;
using Enx.NetCord.Testing.Tests.Rest;
using NetCord.Rest;
using Xunit;

namespace Enx.NetCord.Testing.Tests.Assertions;

internal static class AssertionTestSupport
{
    public const string PostMessage = "POST /channels/{channel_id}/messages";

    /// <summary>
    /// A fake whose table inventories every operation and binds stand-in handlers for a few of them
    /// (no real endpoint exists yet): the others are inventoried without a handler.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <returns>The fake.</returns>
    public static FakeDiscord CreateFake(FakeDiscordOptions? options = null)
    {
        RouteBinding[] bindings =
        [
            RouteBinding.Of("GET /users/@me", _ => Ok("42", "bot")),
            RouteBinding.Of(
                PostMessage,
                request => request.RouteParameters["channel_id"] == "200" ? DiscordError.MissingAccess() : Ok("1", "created")),
            RouteBinding.Of("DELETE /channels/{channel_id}/messages/{message_id}", _ => RestResult.NoContent()),
            RouteBinding.Of("PUT /channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}/@me", _ => RestResult.NoContent()),
            RouteBinding.Of("GET /webhooks/{webhook_id}/{webhook_token}", _ => Ok("1", "webhook")),
            RouteBinding.Of("GET /invites/{code}", _ => Ok("1", "invite")),
        ];
        return FakeDiscord.Create(options, RouteTable.Create(RouteInventory.All, bindings));
    }

    /// <summary>Sends a request through the real NetCord client and swallows the Discord error, if any.</summary>
    /// <param name="rest">The client.</param>
    /// <param name="method">The HTTP method.</param>
    /// <param name="route">The route as NetCord writes it: an interpolated string whose arguments NetCord encodes.</param>
    /// <param name="json">An optional JSON body.</param>
    /// <param name="query">An optional query string, with its <c>?</c>.</param>
    /// <returns>A task that completes when the request ended.</returns>
    public static async Task SendAsync(RestClient rest, HttpMethod method, FormattableString route, string? json = null, string? query = null)
    {
        var ct = TestContext.Current.CancellationToken;
        try
        {
            if (json is null)
            {
                await using var stream = await rest.SendRequestAsync(method, route, query, cancellationToken: ct);
            }
            else
            {
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                await using var stream = await rest.SendRequestAsync(method, content, route, query, cancellationToken: ct);
            }
        }
        catch (RestException)
        {
            // Failed attempts are part of what the assertions observe.
        }
    }

    private static RestResult Ok(string id, string username)
        => RestResult.Ok(new TestUserDto(id, username), TestJsonContext.Default.TestUserDto);
}
