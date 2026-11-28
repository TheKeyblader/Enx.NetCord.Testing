using Enx.NetCord.Testing.Serialization;
using Enx.NetCord.Testing.State;

namespace Enx.NetCord.Testing.Rest.Routes.Users;

/// <summary>The handlers of the <c>/users</c> family: current user, user lookup and DM channels.</summary>
internal static class UserRoutes
{
    /// <summary>Gets the bindings of this family, registered in <see cref="RestRoutes.Bindings"/>.</summary>
    public static IReadOnlyList<RouteBinding> Bindings { get; } =
    [
        RouteBinding.Of("GET /users/@me", GetCurrentUser),
        RouteBinding.Of("PATCH /users/@me", ModifyCurrentUser, UserValidators.ValidateCurrentUserPatch),
        RouteBinding.Of("GET /users/{user_id}", GetUser, UserValidators.ValidateUserId),
        RouteBinding.Of("POST /users/@me/channels", CreateDm, UserValidators.ValidateCreateDm),
    ];

    private static RestResult GetCurrentUser(FakeRestRequest request)
        => request.State.FindUser(request.Caller.UserId) is { } user
            ? RestResult.Ok(UserWire.ToCurrentUser(user), FakeDiscordJsonContext.Default.CurrentUserResponse)
            : DiscordError.Unauthorized();

    private static RestResult ModifyCurrentUser(FakeRestRequest request)
    {
        var patch = request.GetPayload<CurrentUserPatch>();
        var result = request.State.UpdateUser(new UpdateUserInput(
            request.Caller.UserId,
            patch.Username,
            patch.ChangeAvatar,
            patch.AvatarHash,
            patch.ChangeBanner,
            patch.BannerHash));
        return result.TryGetValue(out var user)
            ? RestResult.Ok(UserWire.ToCurrentUser(user), FakeDiscordJsonContext.Default.CurrentUserResponse)
            : DiscordError.Unauthorized();
    }

    private static RestResult GetUser(FakeRestRequest request)
        => request.State.FindUser(request.GetPayload<ulong>()) is { } user
            ? RestResult.Ok(UserWire.ToUser(user), FakeDiscordJsonContext.Default.UserResponse)
            : DiscordError.UnknownUser();

    private static RestResult CreateDm(FakeRestRequest request)
    {
        var payload = request.GetPayload<CreateDmRequest>();
        if (payload.RecipientId is not { } recipientId)
        {
            // Group DM: Discord needs OAuth2 access tokens (scope gdm.join) granted by the users. The fake has no OAuth2
            // grants, so no token it could be given is valid.
            return DiscordError.InvalidOAuth2AccessToken();
        }

        var result = request.State.GetOrCreateDmChannel(new CreateDmChannelInput(recipientId));
        if (!result.TryGetValue(out var created))
        {
            return DiscordError.UnknownUser();
        }

        var recipients = created.Channel.RecipientIds
            .Select(request.State.FindUser)
            .OfType<UserSnapshot>()
            .ToList();
        return RestResult.Ok(UserWire.ToDmChannel(created.Channel, recipients), FakeDiscordJsonContext.Default.PrivateChannelResponse);
    }
}
