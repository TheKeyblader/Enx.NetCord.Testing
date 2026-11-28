using System.Globalization;
using Enx.NetCord.Testing.Rest.Routes.Messages;
using Enx.NetCord.Testing.Serialization;
using Enx.NetCord.Testing.State;

namespace Enx.NetCord.Testing.Rest.Routes.Pins;

/// <summary>The handlers of the <c>/channels/{channel_id}/messages/pins</c> resource: list, pin and unpin.</summary>
/// <remarks>
/// Every lookup goes through the access-controlled state operations with <see cref="StateCaller.Bot"/> (ADR 0004, amendment 04B): an
/// unknown channel is <c>10003</c>, a channel of a guild the bot is not a member of <c>50001</c>, an unknown message <c>10008</c> (after the
/// channel). The permissions (<c>VIEW_CHANNEL</c>, <c>READ_MESSAGE_HISTORY</c>, <c>PIN_MESSAGES</c>) are not computed yet (M4-REST-05).
/// The state operations publish the <see cref="StateChange"/> facts; the Gateway translation is M3-03. The deprecated
/// <c>/channels/{channel_id}/pins</c> routes are not exposed by NetCord and are not served.
/// </remarks>
internal static class PinRoutes
{
    /// <summary>Gets the bindings of this family, registered in <see cref="RestRoutes.Bindings"/>.</summary>
    public static IReadOnlyList<RouteBinding> Bindings { get; } =
    [
        RouteBinding.Of("GET /channels/{channel_id}/messages/pins", List, PinValidators.ValidateList),
        RouteBinding.Of("PUT /channels/{channel_id}/messages/pins/{message_id}", Pin, PinValidators.ValidatePinRoute),
        RouteBinding.Of("DELETE /channels/{channel_id}/messages/pins/{message_id}", Unpin, PinValidators.ValidatePinRoute),
    ];

    private static RestResult List(FakeRestRequest request)
    {
        var query = request.GetPayload<PinListQuery>();
        var result = request.State.ListPins(StateCaller.Bot, query.ChannelId, query.Before, query.Limit);
        if (!result.TryGetValue(out var page))
        {
            return DiscordError.FromStateFailure(result.Failure);
        }

        PinnedMessagesResponse body = new()
        {
            Items =
            [
                .. page.Items.Select(pin => new PinnedMessageResponse
                {
                    PinnedAt = pin.PinnedAt.ToString("O", CultureInfo.InvariantCulture),
                    Message = MessageWire.ToResponse(pin.Message, request.State.FindUser),
                }),
            ],
            HasMore = page.HasMore,
        };
        return RestResult.Ok(body, FakeDiscordJsonContext.Default.PinnedMessagesResponse);
    }

    private static RestResult Pin(FakeRestRequest request)
    {
        var route = request.GetPayload<PinRoute>();
        return NoContent(request.State.PinMessage(StateCaller.Bot, route.ChannelId, route.MessageId, request.Caller.UserId));
    }

    private static RestResult Unpin(FakeRestRequest request)
    {
        var route = request.GetPayload<PinRoute>();
        return NoContent(request.State.UnpinMessage(StateCaller.Bot, route.ChannelId, route.MessageId));
    }

    private static RestResult NoContent(StateResult<bool> result)
        => result.IsSuccess ? RestResult.NoContent() : DiscordError.FromStateFailure(result.Failure);
}
