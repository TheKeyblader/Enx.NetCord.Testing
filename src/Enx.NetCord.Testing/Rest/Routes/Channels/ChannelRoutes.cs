using Enx.NetCord.Testing.Rest.Routes.Users;
using Enx.NetCord.Testing.Serialization;
using Enx.NetCord.Testing.State;

namespace Enx.NetCord.Testing.Rest.Routes.Channels;

/// <summary>The handlers of the <c>/channels/{channel_id}</c> resource: read, modify and delete or close a channel.</summary>
/// <remarks>
/// Every lookup goes through the access-controlled state operations with <see cref="StateCaller.Bot"/> (ADR 0004, amendment 04B):
/// an unknown channel is <c>10003</c>, a channel of a guild the bot is not a member of is <c>50001</c>. The <c>MANAGE_CHANNELS</c>
/// permission is not computed yet (M4-REST-05).
/// </remarks>
internal static class ChannelRoutes
{
    /// <summary>Gets the bindings of this family, registered in <see cref="RestRoutes.Bindings"/>.</summary>
    public static IReadOnlyList<RouteBinding> Bindings { get; } =
    [
        RouteBinding.Of("GET /channels/{channel_id}", GetChannel, ChannelValidators.ValidateChannelId),
        RouteBinding.Of("PATCH /channels/{channel_id}", ModifyChannel, ChannelValidators.ValidateChannelPatch),
        RouteBinding.Of("DELETE /channels/{channel_id}", DeleteChannel, ChannelValidators.ValidateChannelId),
    ];

    private static RestResult GetChannel(FakeRestRequest request)
    {
        var result = request.State.GetChannel(StateCaller.Bot, request.GetPayload<ulong>());
        return result.TryGetValue(out var channel) ? Render(request, channel) : DiscordError.FromStateFailure(result.Failure);
    }

    private static RestResult ModifyChannel(FakeRestRequest request)
    {
        var patch = request.GetPayload<ChannelPatch>();

        // One state operation checks access and the rules that depend on the channel (type conversion, parent invariants, bounds
        // of the type) and commits, all under the state lock (ADR 0005, ADR 0013); a field the channel's type has no use for is
        // ignored there.
        var updated = request.State.UpdateChannel(
            StateCaller.Bot,
            new UpdateChannelInput(
                patch.ChannelId,
                patch.Name,
                patch.ChangeTopic,
                patch.Topic,
                patch.Nsfw,
                patch.RateLimitPerUser,
                patch.Position,
                patch.Type,
                patch.ParentId,
                patch.ChangeParent,
                patch.Bitrate,
                patch.UserLimit,
                patch.ChangeRtcRegion,
                patch.RtcRegion,
                patch.VideoQualityMode,
                patch.DefaultAutoArchiveDuration,
                patch.DefaultThreadRateLimitPerUser,
                patch.ChangeDefaultSortOrder,
                patch.DefaultSortOrder,
                patch.DefaultForumLayout));
        return updated.TryGetValue(out var result) ? Render(request, result) : DiscordError.FromStateFailure(updated.Failure);
    }

    private static RestResult DeleteChannel(FakeRestRequest request)
    {
        var result = request.State.DeleteChannel(StateCaller.Bot, request.GetPayload<ulong>());
        return result.TryGetValue(out var channel) ? Render(request, channel) : DiscordError.FromStateFailure(result.Failure);
    }

    private static RestResult Render(FakeRestRequest request, ChannelSnapshot channel)
    {
        if (channel.GuildId is not null)
        {
            return ChannelWire.RenderGuildChannel(channel);
        }

        var recipients = channel.RecipientIds
            .Select(request.State.FindUser)
            .OfType<UserSnapshot>()
            .ToList();
        return RestResult.Ok(UserWire.ToDmChannel(channel, recipients), FakeDiscordJsonContext.Default.PrivateChannelResponse);
    }
}
