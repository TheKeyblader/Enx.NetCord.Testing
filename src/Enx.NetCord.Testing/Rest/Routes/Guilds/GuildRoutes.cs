using Enx.NetCord.Testing.Serialization;
using Enx.NetCord.Testing.State;

namespace Enx.NetCord.Testing.Rest.Routes.Guilds;

/// <summary>The handlers of the <c>/guilds/{guild_id}</c> resource itself and its channel collection (M1-REST-06).</summary>
/// <remarks>
/// Every lookup goes through the access-controlled state operations with <see cref="StateCaller.Bot"/> (ADR 0004, amendment 04B): the
/// guild itself (<c>GET</c>, <c>PATCH</c>, <c>preview</c>) is <c>10004</c> when the bot is not a member, its channels are <c>50001</c>.
/// The <c>MANAGE_GUILD</c>/<c>MANAGE_CHANNELS</c> permissions are not computed yet (M4-REST-05).
/// </remarks>
internal static class GuildRoutes
{
    /// <summary>Gets the bindings of this family, registered in <see cref="RestRoutes.Bindings"/>.</summary>
    public static IReadOnlyList<RouteBinding> Bindings { get; } =
    [
        RouteBinding.Of("GET /guilds/{guild_id}", GetGuild, GuildValidators.ValidateGetGuild),
        RouteBinding.Of("PATCH /guilds/{guild_id}", ModifyGuild, GuildValidators.ValidateGuildPatch),
        RouteBinding.Of("GET /guilds/{guild_id}/preview", GetGuildPreview, GuildValidators.ValidateGuildId),
        RouteBinding.Of("GET /guilds/{guild_id}/channels", ListGuildChannels, GuildValidators.ValidateGuildId),
        RouteBinding.Of("POST /guilds/{guild_id}/channels", CreateGuildChannel, GuildValidators.ValidateCreateGuildChannel),
        RouteBinding.Of("PATCH /guilds/{guild_id}/channels", ModifyGuildChannelPositions, GuildValidators.ValidateChannelPositions),
    ];

    private static RestResult GetGuild(FakeRestRequest request)
    {
        var query = request.GetPayload<GetGuildRequest>();
        var result = request.State.GetGuildDetails(StateCaller.Bot, query.GuildId);
        return result.TryGetValue(out var details)
            ? RestResult.Ok(GuildWire.ToGuild(details, request.Caller.UserId, query.WithCounts), FakeDiscordJsonContext.Default.GuildResponse)
            : DiscordError.FromStateFailure(result.Failure);
    }

    private static RestResult ModifyGuild(FakeRestRequest request)
    {
        // One state operation checks access and the channel settings, writes and commits under the lock; the update is a no-op
        // (and publishes nothing) when no value changes.
        var result = request.State.UpdateGuild(StateCaller.Bot, request.GetPayload<UpdateGuildInput>());
        return result.TryGetValue(out var details)
            ? RestResult.Ok(GuildWire.ToGuild(details, request.Caller.UserId, withCounts: false), FakeDiscordJsonContext.Default.GuildResponse)
            : DiscordError.FromStateFailure(result.Failure);
    }

    private static RestResult GetGuildPreview(FakeRestRequest request)
    {
        var result = request.State.GetGuildDetails(StateCaller.Bot, request.GetPayload<ulong>());
        return result.TryGetValue(out var details)
            ? RestResult.Ok(GuildWire.ToPreview(details), FakeDiscordJsonContext.Default.GuildPreviewResponse)
            : DiscordError.FromStateFailure(result.Failure);
    }

    private static RestResult ListGuildChannels(FakeRestRequest request)
    {
        var result = request.State.ListGuildChannels(StateCaller.Bot, request.GetPayload<ulong>());
        return result.TryGetValue(out var channels)
            ? RestResult.Ok([.. channels.Select(GuildWire.ToChannelUnion)], FakeDiscordJsonContext.Default.ListGuildChannelUnion)
            : DiscordError.FromStateFailure(result.Failure);
    }

    private static RestResult CreateGuildChannel(FakeRestRequest request)
    {
        var result = request.State.CreateGuildChannel(StateCaller.Bot, request.GetPayload<CreateGuildChannelInput>());
        return result.TryGetValue(out var channel) ? GuildWire.RenderCreatedChannel(channel) : DiscordError.FromStateFailure(result.Failure);
    }

    private static RestResult ModifyGuildChannelPositions(FakeRestRequest request)
    {
        var batch = request.GetPayload<ChannelPositionsRequest>();
        var result = request.State.UpdateChannelPositions(StateCaller.Bot, batch.GuildId, batch.Placements);
        return result.IsSuccess ? RestResult.NoContent() : DiscordError.FromStateFailure(result.Failure);
    }
}
