using Enx.NetCord.Testing.Rest.Routes.Users;
using Enx.NetCord.Testing.Serialization;
using Enx.NetCord.Testing.State;

namespace Enx.NetCord.Testing.Rest.Routes.Reactions;

/// <summary>The handlers of the <c>/channels/{channel_id}/messages/{message_id}/reactions</c> resource: add, remove, list and clear.</summary>
/// <remarks>
/// Every lookup goes through the access-controlled state operations with <see cref="StateCaller.Bot"/> (ADR 0004, amendment 04B): an
/// unknown channel is <c>10003</c>, a channel of a guild the bot is not a member of <c>50001</c>, an unknown message <c>10008</c> (after the
/// channel). The permissions (<c>READ_MESSAGE_HISTORY</c>, <c>ADD_REACTIONS</c>, <c>MANAGE_MESSAGES</c>) are not computed yet (M4-REST-05).
/// The custom emojis of a guild are not modeled either (M4-REST-04): any well-formed <c>name:id</c> is accepted. The state operations publish
/// the <see cref="StateChange"/> facts; the Gateway translation is M3-03.
/// </remarks>
internal static class ReactionRoutes
{
    /// <summary>Gets the bindings of this family, registered in <see cref="RestRoutes.Bindings"/>.</summary>
    public static IReadOnlyList<RouteBinding> Bindings { get; } =
    [
        RouteBinding.Of("DELETE /channels/{channel_id}/messages/{message_id}/reactions", ClearAll, ReactionValidators.ValidateMessageRoute),
        RouteBinding.Of("GET /channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}", ListUsers, ReactionValidators.ValidateList),
        RouteBinding.Of("DELETE /channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}", ClearEmoji, ReactionValidators.ValidateEmojiRoute),
        RouteBinding.Of("PUT /channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}/@me", AddOwn, ReactionValidators.ValidateEmojiRoute),
        RouteBinding.Of("DELETE /channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}/@me", RemoveOwn, ReactionValidators.ValidateEmojiRoute),
        RouteBinding.Of("DELETE /channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}/{user_id}", RemoveUser, ReactionValidators.ValidateUserRoute),
    ];

    private static RestResult AddOwn(FakeRestRequest request)
    {
        var route = request.GetPayload<ReactionEmojiRoute>();
        return NoContent(request.State.AddReaction(StateCaller.Bot, route.ChannelId, route.MessageId, route.Emoji, request.Caller.UserId));
    }

    private static RestResult RemoveOwn(FakeRestRequest request)
    {
        var route = request.GetPayload<ReactionEmojiRoute>();
        return NoContent(request.State.RemoveReaction(StateCaller.Bot, route.ChannelId, route.MessageId, route.Emoji, request.Caller.UserId));
    }

    private static RestResult RemoveUser(FakeRestRequest request)
    {
        var route = request.GetPayload<ReactionUserRoute>();
        return NoContent(request.State.RemoveReaction(StateCaller.Bot, route.ChannelId, route.MessageId, route.Emoji, route.UserId));
    }

    private static RestResult ClearAll(FakeRestRequest request)
    {
        var route = request.GetPayload<ReactionsRoute>();
        return NoContent(request.State.ClearReactions(StateCaller.Bot, route.ChannelId, route.MessageId));
    }

    private static RestResult ClearEmoji(FakeRestRequest request)
    {
        var route = request.GetPayload<ReactionEmojiRoute>();
        return NoContent(request.State.ClearEmojiReactions(StateCaller.Bot, route.ChannelId, route.MessageId, route.Emoji));
    }

    private static RestResult ListUsers(FakeRestRequest request)
    {
        var query = request.GetPayload<ReactionListQuery>();
        var result = request.State.ListReactionUsers(StateCaller.Bot, query.ChannelId, query.MessageId, query.Emoji, query.Burst, query.After, query.Limit);
        if (!result.TryGetValue(out var users))
        {
            return DiscordError.FromStateFailure(result.Failure);
        }

        List<UserResponse> body = [.. users.Select(UserWire.ToUser)];
        return RestResult.Ok(body, FakeDiscordJsonContext.Default.ListUserResponse);
    }

    private static RestResult NoContent(StateResult<bool> result)
        => result.IsSuccess ? RestResult.NoContent() : DiscordError.FromStateFailure(result.Failure);
}
