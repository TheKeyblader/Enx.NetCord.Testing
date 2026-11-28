using System.Globalization;
using System.Text.Json;
using Enx.NetCord.Testing.Rest.Routes.Guilds;
using Enx.NetCord.Testing.State;

namespace Enx.NetCord.Testing.Rest.Routes.Members;

/// <summary>The validated request of <c>GET /guilds/{guild_id}/members</c>.</summary>
/// <param name="GuildId">The guild ID of the route.</param>
/// <param name="Limit">The page size, 1 when not sent (the documented default).</param>
/// <param name="After">The highest user ID of the previous page, 0 when not sent.</param>
internal sealed record ListMembersRequest(ulong GuildId, int Limit, ulong After);

/// <summary>The validated request of <c>GET /guilds/{guild_id}/members/search</c>.</summary>
/// <param name="GuildId">The guild ID of the route.</param>
/// <param name="Query">The prefix to look for, 1-100 characters.</param>
/// <param name="Limit">The maximum number of members, 1 when not sent (the documented default).</param>
internal sealed record SearchMembersRequest(ulong GuildId, string Query, int Limit);

/// <summary>The validated route of a request on one member.</summary>
/// <param name="GuildId">The guild ID of the route.</param>
/// <param name="UserId">The user ID of the route (the caller for <c>@me</c>).</param>
internal sealed record MemberRoute(ulong GuildId, ulong UserId);

/// <summary>The validated route of a request on a role of one member.</summary>
/// <param name="GuildId">The guild ID of the route.</param>
/// <param name="UserId">The user ID of the route.</param>
/// <param name="RoleId">The role ID of the route.</param>
internal sealed record MemberRoleRoute(ulong GuildId, ulong UserId, ulong RoleId);

/// <summary>
/// Structural validators of the member routes (ADR 0005, step 3): no state is read. Whether the guild, the member and the roles exist,
/// and what the bot may do to them, is decided by the state.
/// </summary>
internal static class MemberValidators
{
    internal const int NickMaxLength = 32;
    internal const int BioMaxLength = 300;
    internal const int QueryMaxLength = 100;
    internal const int MaxLimit = 1000;
    internal const int MaxModifyRoles = 350;
    internal const int MaxAddRoles = 250;
    internal const int AccessTokenMaxLength = 10240;

    /// <summary>Validates <c>GET /guilds/{guild_id}/members</c>: the guild ID, <c>limit</c> (1-1000, default 1) and <c>after</c>.</summary>
    /// <param name="request">The request.</param>
    /// <returns>A <see cref="ListMembersRequest"/> payload, or <c>50035</c>.</returns>
    public static RestValidation ValidateListMembers(FakeRestRequest request)
    {
        var errors = new FormErrors();
        var guildId = FormValidation.ReadRouteSnowflake(request, "guild_id", errors);
        var limit = FormValidation.ReadQueryInteger(request, "limit", 1, MaxLimit, errors);
        var after = FormValidation.ReadQuerySnowflake(request, "after", errors);

        return errors.IsEmpty
            ? RestValidation.Valid(new ListMembersRequest(guildId, limit ?? 1, after ?? 0))
            : DiscordError.InvalidFormBody(errors);
    }

    /// <summary>Validates <c>GET /guilds/{guild_id}/members/search</c>: the guild ID, <c>query</c> (required, 1-100 characters) and <c>limit</c> (1-1000, default 1).</summary>
    /// <param name="request">The request.</param>
    /// <returns>A <see cref="SearchMembersRequest"/> payload, or <c>50035</c>.</returns>
    public static RestValidation ValidateSearchMembers(FakeRestRequest request)
    {
        var errors = new FormErrors();
        var guildId = FormValidation.ReadRouteSnowflake(request, "guild_id", errors);
        var query = request.GetQuery("query");
        if (query is null)
        {
            FormValidation.AddRequired(errors, "query");
        }
        else
        {
            FormValidation.CheckLength(query, "query", 1, QueryMaxLength, errors);
        }

        var limit = FormValidation.ReadQueryInteger(request, "limit", 1, MaxLimit, errors);

        return errors.IsEmpty
            ? RestValidation.Valid(new SearchMembersRequest(guildId, query!, limit ?? 1))
            : DiscordError.InvalidFormBody(errors);
    }

    /// <summary>Validates a route with a guild and a user: <c>GET</c>, <c>DELETE</c> of <c>/guilds/{guild_id}/members/{user_id}</c>.</summary>
    /// <param name="request">The request.</param>
    /// <returns>A <see cref="MemberRoute"/> payload, or <c>50035</c>.</returns>
    public static RestValidation ValidateMemberRoute(FakeRestRequest request)
    {
        var errors = new FormErrors();
        var route = ReadMemberRoute(request, errors);
        return errors.IsEmpty ? RestValidation.Valid(route) : DiscordError.InvalidFormBody(errors);
    }

    /// <summary>Validates <c>PUT</c> and <c>DELETE</c> of <c>/guilds/{guild_id}/members/{user_id}/roles/{role_id}</c>: the three IDs.</summary>
    /// <param name="request">The request.</param>
    /// <returns>A <see cref="MemberRoleRoute"/> payload, or <c>50035</c>.</returns>
    public static RestValidation ValidateMemberRoleRoute(FakeRestRequest request)
    {
        var errors = new FormErrors();
        var route = ReadMemberRoute(request, errors);
        var roleId = FormValidation.ReadRouteSnowflake(request, "role_id", errors);
        return errors.IsEmpty
            ? RestValidation.Valid(new MemberRoleRoute(route.GuildId, route.UserId, roleId))
            : DiscordError.InvalidFormBody(errors);
    }

    /// <summary>
    /// Validates <c>PATCH /guilds/{guild_id}/members/{user_id}</c> with the spec's <c>update_guild_member</c> body: every field is
    /// optional and nullable. <c>nick</c> (at most 32 characters, <c>null</c> or empty clears it), <c>roles</c> (at most 350 unique IDs),
    /// <c>mute</c>, <c>deaf</c>, <c>communication_disabled_until</c> (an ISO 8601 date-time, <c>null</c> clears it) and <c>flags</c> are
    /// handed to the state; <c>channel_id</c> is validated and ignored (voice states are not modeled).
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>An <see cref="UpdateMemberInput"/> payload, or <c>50109</c>/<c>50035</c>.</returns>
    public static RestValidation ValidateModifyMember(FakeRestRequest request)
    {
        var errors = new FormErrors();
        var route = ReadMemberRoute(request, errors);
        if (!FormValidation.TryReadJsonObject(request, out var document, out var error))
        {
            return errors.IsEmpty ? error : DiscordError.InvalidFormBody(errors);
        }

        using (document)
        {
            var root = document.RootElement;
            var nick = ReadNick(root, errors);
            var roles = FormValidation.ReadSnowflakeArray(root, "roles", 0, MaxModifyRoles, unique: true, errors);
            var mute = FormValidation.ReadOptionalBoolean(root, "mute", errors);
            var deaf = FormValidation.ReadOptionalBoolean(root, "deaf", errors);
            if (root.TryGetProperty("channel_id", out var channel) && channel.ValueKind != JsonValueKind.Null)
            {
                FormValidation.TryReadSnowflake(channel, "channel_id", errors, out _);
            }

            var timeout = ReadTimeout(root, errors);
            var flags = FormValidation.ReadOptionalInteger(root, "flags", 0, int.MaxValue, errors);

            return errors.IsEmpty
                ? RestValidation.Valid(new UpdateMemberInput(route.GuildId, route.UserId, nick, roles, mute, deaf, timeout, flags))
                : DiscordError.InvalidFormBody(errors);
        }
    }

    /// <summary>
    /// Validates <c>PATCH /guilds/{guild_id}/members/@me</c> with the spec's <c>update_my_guild_member</c> body: <c>nick</c> (at most 32
    /// characters), <c>avatar</c> and <c>banner</c> (image data, <c>null</c> removes them) and <c>bio</c> (at most 300 characters,
    /// validated and ignored: a bio is not part of a member object). The member is the caller.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>An <see cref="UpdateMemberInput"/> payload for the caller, or <c>50109</c>/<c>50035</c>.</returns>
    public static RestValidation ValidateModifyCurrentMember(FakeRestRequest request)
    {
        var errors = new FormErrors();
        var guildId = FormValidation.ReadRouteSnowflake(request, "guild_id", errors);
        if (!FormValidation.TryReadJsonObject(request, out var document, out var error))
        {
            return errors.IsEmpty ? error : DiscordError.InvalidFormBody(errors);
        }

        using (document)
        {
            var root = document.RootElement;
            var nick = ReadNick(root, errors);
            var avatar = GuildValidators.ReadImage(root, "avatar", errors);
            var banner = GuildValidators.ReadImage(root, "banner", errors);
            var bio = FormValidation.ReadNullableString(root, "bio", errors, out _);
            if (bio is not null)
            {
                FormValidation.CheckMaxLength(bio, "bio", BioMaxLength, errors);
            }

            return errors.IsEmpty
                ? RestValidation.Valid(new UpdateMemberInput(guildId, request.Caller.UserId, nick, Avatar: avatar, Banner: banner))
                : DiscordError.InvalidFormBody(errors);
        }
    }

    /// <summary>
    /// Validates <c>PUT /guilds/{guild_id}/members/{user_id}</c> with the spec's <c>BotAddGuildMemberRequest</c>: <c>access_token</c> is
    /// required (at most 10240 characters); <c>nick</c>, <c>roles</c> (at most 250 unique IDs), <c>mute</c>, <c>deaf</c> and <c>flags</c> are
    /// checked for their type. Whether the token is valid is decided by the handler.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>A <see cref="MemberRoute"/> payload, or <c>50109</c>/<c>50035</c>.</returns>
    public static RestValidation ValidateAddMember(FakeRestRequest request)
    {
        var errors = new FormErrors();
        var route = ReadMemberRoute(request, errors);
        if (!FormValidation.TryReadJsonObject(request, out var document, out var error))
        {
            return errors.IsEmpty ? error : DiscordError.InvalidFormBody(errors);
        }

        using (document)
        {
            var root = document.RootElement;
            var token = FormValidation.ReadOptionalString(root, "access_token", errors);
            if (token is null)
            {
                if (!root.TryGetProperty("access_token", out _))
                {
                    FormValidation.AddRequired(errors, "access_token");
                }
            }
            else
            {
                FormValidation.CheckMaxLength(token, "access_token", AccessTokenMaxLength, errors);
            }

            _ = ReadNick(root, errors);
            _ = FormValidation.ReadSnowflakeArray(root, "roles", 0, MaxAddRoles, unique: true, errors);
            _ = FormValidation.ReadOptionalBoolean(root, "mute", errors);
            _ = FormValidation.ReadOptionalBoolean(root, "deaf", errors);
            _ = FormValidation.ReadOptionalInteger(root, "flags", 0, int.MaxValue, errors);

            return errors.IsEmpty ? RestValidation.Valid(route) : DiscordError.InvalidFormBody(errors);
        }
    }

    private static MemberRoute ReadMemberRoute(FakeRestRequest request, FormErrors errors)
        => new(
            FormValidation.ReadRouteSnowflake(request, "guild_id", errors),
            FormValidation.ReadRouteSnowflake(request, "user_id", errors));

    // `nick`: absent leaves it, null and the empty string clear it (NetCord sends "" to remove a nickname).
    private static Change<string> ReadNick(JsonElement root, FormErrors errors)
    {
        var nick = FormValidation.ReadNullableString(root, "nick", errors, out var present);
        if (nick is not null)
        {
            FormValidation.CheckMaxLength(nick, "nick", NickMaxLength, errors);
        }

        return present ? Change<string>.To(nick) : default;
    }

    // `communication_disabled_until`: absent leaves it, null clears it, an ISO 8601 date-time sets it.
    private static Change<DateTimeOffset?> ReadTimeout(JsonElement root, FormErrors errors)
    {
        if (!root.TryGetProperty("communication_disabled_until", out var element))
        {
            return default;
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.Null:
                return Change<DateTimeOffset?>.To(null);

            case JsonValueKind.String:
                return FormValidation.TryReadDateTime(element.GetString()!, "communication_disabled_until", errors, out var parsed)
                    ? Change<DateTimeOffset?>.To(parsed)
                    : default;

            default:
                FormValidation.AddBadType(errors, "communication_disabled_until", "string");
                return default;
        }
    }
}
