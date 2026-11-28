using System.Net;
using Enx.NetCord.Testing.Serialization;
using Enx.NetCord.Testing.State;

namespace Enx.NetCord.Testing.Rest.Routes.Messages;

/// <summary>The handlers of the <c>/channels/{channel_id}/messages</c> resource: list, read, send, edit, delete, bulk delete and crosspost.</summary>
/// <remarks>
/// Every lookup goes through the access-controlled state operations with <see cref="StateCaller.Bot"/> (ADR 0004, amendment 04B): an
/// unknown channel is <c>10003</c>, a channel of a guild the bot is not a member of is <c>50001</c>, an unknown message <c>10008</c> (after the
/// channel). The permissions (<c>SEND_MESSAGES</c>, <c>READ_MESSAGE_HISTORY</c>, <c>MANAGE_MESSAGES</c>…) are not computed yet (M4-REST-05).
/// The state operations publish the <see cref="StateChange"/> facts; the Gateway translation is M3.
/// </remarks>
internal static class MessageRoutes
{
    /// <summary>Gets the bindings of this family, registered in <see cref="RestRoutes.Bindings"/>.</summary>
    public static IReadOnlyList<RouteBinding> Bindings { get; } =
    [
        RouteBinding.Of("GET /channels/{channel_id}/messages", ListMessages, MessageValidators.ValidateList),
        RouteBinding.Of("POST /channels/{channel_id}/messages", CreateMessage, MessageValidators.ValidateCreate),
        RouteBinding.Of("POST /channels/{channel_id}/messages/bulk-delete", BulkDelete, MessageValidators.ValidateBulkDelete),
        RouteBinding.Of("GET /channels/{channel_id}/messages/{message_id}", GetMessage, MessageValidators.ValidateMessageRoute),
        RouteBinding.Of("PATCH /channels/{channel_id}/messages/{message_id}", EditMessage, MessageValidators.ValidateEdit),
        RouteBinding.Of("DELETE /channels/{channel_id}/messages/{message_id}", DeleteMessage, MessageValidators.ValidateMessageRoute),
        RouteBinding.Of("POST /channels/{channel_id}/messages/{message_id}/crosspost", Crosspost, MessageValidators.ValidateMessageRoute),
    ];

    private static RestResult ListMessages(FakeRestRequest request)
    {
        var query = request.GetPayload<MessageListQuery>();
        var result = request.State.ListMessages(StateCaller.Bot, query.ChannelId, query.Limit, query.Before, query.After, query.Around);
        if (!result.TryGetValue(out var messages))
        {
            return DiscordError.FromStateFailure(result.Failure);
        }

        List<MessageResponse> body = [.. messages.Select(message => MessageWire.ToResponse(message, request.State.FindUser))];
        return RestResult.Ok(body, FakeDiscordJsonContext.Default.ListMessageResponse);
    }

    private static RestResult CreateMessage(FakeRestRequest request)
    {
        var input = request.GetPayload<CreateMessageInput>() with { AuthorId = request.Caller.UserId };
        return Render(request, request.State.CreateMessage(StateCaller.Bot, input));
    }

    private static RestResult GetMessage(FakeRestRequest request)
    {
        var route = request.GetPayload<MessageRoute>();
        return Render(request, request.State.GetMessage(StateCaller.Bot, route.ChannelId, route.MessageId));
    }

    private static RestResult EditMessage(FakeRestRequest request)
        => Render(request, request.State.EditMessage(StateCaller.Bot, request.GetPayload<EditMessageInput>()));

    private static RestResult DeleteMessage(FakeRestRequest request)
    {
        var route = request.GetPayload<MessageRoute>();
        var result = request.State.DeleteMessage(StateCaller.Bot, route.ChannelId, route.MessageId);
        return result.IsSuccess ? RestResult.NoContent() : DiscordError.FromStateFailure(result.Failure);
    }

    private static RestResult BulkDelete(FakeRestRequest request)
    {
        var bulk = request.GetPayload<BulkDeleteRequest>();
        var result = request.State.BulkDeleteMessages(StateCaller.Bot, bulk.ChannelId, bulk.MessageIds);
        return result.IsSuccess ? RestResult.NoContent() : DiscordError.FromStateFailure(result.Failure);
    }

    private static RestResult Crosspost(FakeRestRequest request)
    {
        var route = request.GetPayload<MessageRoute>();
        return Render(request, request.State.CrosspostMessage(StateCaller.Bot, route.ChannelId, route.MessageId));
    }

    private static RestResult Render(FakeRestRequest request, StateResult<MessageSnapshot> result)
        => result.TryGetValue(out var message)
            ? RestResult.Json(HttpStatusCode.OK, MessageWire.ToResponse(message, request.State.FindUser), FakeDiscordJsonContext.Default.MessageResponse)
            : DiscordError.FromStateFailure(result.Failure);
}
