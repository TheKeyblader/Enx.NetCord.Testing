using System.Net;
using Enx.NetCord.Testing.Serialization;
using Enx.NetCord.Testing.State;

namespace Enx.NetCord.Testing.Rest.Routes.Members;

/// <summary>The handlers of <c>/guilds/{guild_id}/members</c> (M1-REST-07).</summary>
/// <remarks>
/// Every lookup goes through the access-controlled state operations with <see cref="StateCaller.Bot"/> (ADR 0004, amendment 04B): a
/// guild the bot is not a member of is <c>50001</c>, as for any sub-resource of a guild. The permissions (<c>KICK_MEMBERS</c>,
/// <c>MANAGE_ROLES</c>, <c>MANAGE_NICKNAMES</c>, <c>MODERATE_MEMBERS</c>…) and the role hierarchy are not computed yet (M4-REST-05).
/// </remarks>
internal static class MemberRoutes
{
    /// <summary>Gets the bindings of this family, registered in <see cref="RestRoutes.Bindings"/>.</summary>
    public static IReadOnlyList<RouteBinding> Bindings { get; } =
    [
        RouteBinding.Of("GET /guilds/{guild_id}/members", ListMembers, MemberValidators.ValidateListMembers),
        RouteBinding.Of("GET /guilds/{guild_id}/members/search", SearchMembers, MemberValidators.ValidateSearchMembers),
        RouteBinding.Of("PATCH /guilds/{guild_id}/members/@me", ModifyCurrentMember, MemberValidators.ValidateModifyCurrentMember),
        RouteBinding.Of("GET /guilds/{guild_id}/members/{user_id}", GetMember, MemberValidators.ValidateMemberRoute),
        RouteBinding.Of("PATCH /guilds/{guild_id}/members/{user_id}", ModifyMember, MemberValidators.ValidateModifyMember),
        RouteBinding.Of("PUT /guilds/{guild_id}/members/{user_id}", AddMember, MemberValidators.ValidateAddMember),
        RouteBinding.Of("DELETE /guilds/{guild_id}/members/{user_id}", RemoveMember, MemberValidators.ValidateMemberRoute),
        RouteBinding.Of("PUT /guilds/{guild_id}/members/{user_id}/roles/{role_id}", AddMemberRole, MemberValidators.ValidateMemberRoleRoute),
        RouteBinding.Of("DELETE /guilds/{guild_id}/members/{user_id}/roles/{role_id}", RemoveMemberRole, MemberValidators.ValidateMemberRoleRoute),
    ];

    private static RestResult ListMembers(FakeRestRequest request)
    {
        var query = request.GetPayload<ListMembersRequest>();
        var result = request.State.ListGuildMembersPage(StateCaller.Bot, query.GuildId, query.Limit, query.After);
        return result.TryGetValue(out var members)
            ? RestResult.Ok([.. members.Select(MemberWire.ToMember)], FakeDiscordJsonContext.Default.ListGuildMemberResponse)
            : DiscordError.FromStateFailure(result.Failure);
    }

    private static RestResult SearchMembers(FakeRestRequest request)
    {
        var query = request.GetPayload<SearchMembersRequest>();
        var result = request.State.SearchGuildMembers(StateCaller.Bot, query.GuildId, query.Query, query.Limit);
        return result.TryGetValue(out var members)
            ? RestResult.Ok([.. members.Select(MemberWire.ToMember)], FakeDiscordJsonContext.Default.ListGuildMemberResponse)
            : DiscordError.FromStateFailure(result.Failure);
    }

    private static RestResult GetMember(FakeRestRequest request)
    {
        var route = request.GetPayload<MemberRoute>();
        return Render(request.State.GetGuildMember(StateCaller.Bot, route.GuildId, route.UserId));
    }

    private static RestResult ModifyMember(FakeRestRequest request)
        => Render(request.State.UpdateGuildMember(StateCaller.Bot, request.GetPayload<UpdateMemberInput>()));

    private static RestResult ModifyCurrentMember(FakeRestRequest request)
        => Render(request.State.UpdateGuildMember(StateCaller.Bot, request.GetPayload<UpdateMemberInput>()));

    private static RestResult AddMember(FakeRestRequest request)
    {
        // Discord adds the user named by an OAuth2 access token granted with `guilds.join`. The fake has no OAuth2 grants, so no
        // token it could be given is valid; the guild is still checked first, as for any other member route.
        var route = request.GetPayload<MemberRoute>();
        var access = request.State.CheckGuildMemberAccess(StateCaller.Bot, route.GuildId);
        return access.IsSuccess ? DiscordError.InvalidOAuth2AccessToken() : DiscordError.FromStateFailure(access.Failure);
    }

    private static RestResult RemoveMember(FakeRestRequest request)
    {
        var route = request.GetPayload<MemberRoute>();
        var result = request.State.RemoveGuildMember(StateCaller.Bot, route.GuildId, route.UserId);
        return result.IsSuccess ? RestResult.NoContent() : DiscordError.FromStateFailure(result.Failure);
    }

    private static RestResult AddMemberRole(FakeRestRequest request)
    {
        var route = request.GetPayload<MemberRoleRoute>();
        var result = request.State.AddMemberRole(StateCaller.Bot, route.GuildId, route.UserId, route.RoleId);
        return result.IsSuccess ? RestResult.NoContent() : DiscordError.FromStateFailure(result.Failure);
    }

    private static RestResult RemoveMemberRole(FakeRestRequest request)
    {
        var route = request.GetPayload<MemberRoleRoute>();
        var result = request.State.RemoveMemberRole(StateCaller.Bot, route.GuildId, route.UserId, route.RoleId);
        return result.IsSuccess ? RestResult.NoContent() : DiscordError.FromStateFailure(result.Failure);
    }

    private static RestResult Render(StateResult<MemberDetails> result)
        => result.TryGetValue(out var member)
            ? RestResult.Json(HttpStatusCode.OK, MemberWire.ToMember(member), FakeDiscordJsonContext.Default.GuildMemberResponse)
            : DiscordError.FromStateFailure(result.Failure);
}
