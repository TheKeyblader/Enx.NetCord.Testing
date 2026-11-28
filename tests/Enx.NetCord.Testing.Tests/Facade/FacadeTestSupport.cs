using Enx.NetCord.Testing.Rest;
using Enx.NetCord.Testing.Tests.Rest;

namespace Enx.NetCord.Testing.Tests.Facade;

internal static class FacadeTestSupport
{
    public const string MeKey = "GET /users/@me";

    /// <summary>A fake whose table inventories every operation but binds no handler: each route is "not implemented".</summary>
    /// <param name="options">The options.</param>
    /// <returns>The fake.</returns>
    public static FakeDiscord CreateWithoutHandlers(FakeDiscordOptions? options = null)
        => FakeDiscord.Create(options, RouteTable.Create(RouteInventory.All, []));

    /// <summary>
    /// A fake whose route table binds a stand-in <c>GET /users/@me</c> that reads the bot user from the fake's state
    /// through <see cref="FakeRestRequest.State"/> (no real endpoint exists before M1-REST).
    /// </summary>
    public static FakeDiscord CreateWithCurrentUserStandIn(FakeDiscordOptions? options = null)
    {
        var binding = RouteBinding.Of(
            MeKey,
            request =>
            {
                var user = request.State.FindUser(request.Caller.UserId)!;
                return RestResult.Ok(
                    new TestUserDto(user.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), user.Username),
                    TestJsonContext.Default.TestUserDto);
            });
        return FakeDiscord.Create(options, RouteTable.Create(RouteInventory.All, [binding]));
    }
}
