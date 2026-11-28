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

        // The kind of a channel never changes, so checking it before the update cannot race with another writer.
        var current = request.State.GetChannel(StateCaller.Bot, patch.ChannelId);
        if (!current.TryGetValue(out var channel))
        {
            return DiscordError.FromStateFailure(current.Failure);
        }

        if (channel.GuildId is not null)
        {
            // The fake models neither categories nor channel types other than guild text, so these can only be refused (hypothesis,
            // see docs/parity/errors.md): a guild text channel cannot become another type, and no category exists to be its parent.
            var errors = new FormErrors();
            if (patch.Type is { } type && type != (int)channel.Kind)
            {
                errors.Add("type", "CHANNEL_TYPE_INVALID", "Invalid channel type.");
            }

            if (patch.ParentId is not null)
            {
                errors.Add("parent_id", "CHANNEL_PARENT_INVALID", "Invalid parent channel.");
            }

            if (!errors.IsEmpty)
            {
                return DiscordError.InvalidFormBody(errors);
            }
        }

        var updated = request.State.UpdateChannel(
            StateCaller.Bot,
            new UpdateChannelInput(
                patch.ChannelId,
                patch.Name,
                patch.ChangeTopic,
                patch.Topic,
                patch.Nsfw,
                patch.RateLimitPerUser,
                patch.Position));
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
            return RestResult.Ok(ChannelWire.ToGuildChannel(channel), FakeDiscordJsonContext.Default.GuildChannelResponse);
        }

        var recipients = channel.RecipientIds
            .Select(request.State.FindUser)
            .OfType<UserSnapshot>()
            .ToList();
        return RestResult.Ok(UserWire.ToDmChannel(channel, recipients), FakeDiscordJsonContext.Default.PrivateChannelResponse);
    }
}
