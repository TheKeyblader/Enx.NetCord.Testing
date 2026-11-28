using Enx.NetCord.Testing.State;

namespace Enx.NetCord.Testing.Rest.Routes.Typing;

/// <summary>The handler of <c>POST /channels/{channel_id}/typing</c>.</summary>
/// <remarks>
/// The docs answer <c>204</c> (the spec also lists a <c>200</c> with an empty object, which Discord does not send). The channel is
/// resolved with <see cref="StateCaller.Bot"/> (ADR 0004, amendment 04B). The <c>SEND_MESSAGES</c> permission is not computed yet
/// (M4-REST-05). The state publishes a <see cref="TypingStarted"/> fact; the <c>TYPING_START</c> dispatch is M3-03.
/// </remarks>
internal static class TypingRoutes
{
    /// <summary>Gets the bindings of this family, registered in <see cref="RestRoutes.Bindings"/>.</summary>
    public static IReadOnlyList<RouteBinding> Bindings { get; } =
    [
        RouteBinding.Of("POST /channels/{channel_id}/typing", Trigger, request => FormValidation.ValidateRouteSnowflake(request, "channel_id")),
    ];

    private static RestResult Trigger(FakeRestRequest request)
    {
        var result = request.State.TriggerTyping(StateCaller.Bot, request.GetPayload<ulong>(), request.Caller.UserId);
        return result.IsSuccess ? RestResult.NoContent() : DiscordError.FromStateFailure(result.Failure);
    }
}
