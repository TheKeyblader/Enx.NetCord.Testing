using System.Collections.Immutable;

namespace Enx.NetCord.Testing.State;

// The member routes of M1-REST-07: list, search, read, update, give or take a role, remove. Adding a member is AddMember and
// CreateUserAndMember (FakeDiscordState.cs); the bot's own membership is part of the seed (ADR 0004, amendment 04B).
internal sealed partial class FakeDiscordState
{
    /// <summary>The <c>ADMINISTRATOR</c> permission bit.</summary>
    private const ulong AdministratorPermission = 1UL << 3;

    /// <summary>The furthest a member timeout may end from now, in days (documented by <c>PATCH /guilds/{guild_id}/members/{user_id}</c>).</summary>
    internal const int MaxMemberTimeoutDays = 28;

    /// <summary>
    /// Lists the members of a guild by ascending user ID, starting after <paramref name="after"/> (<c>GET /guilds/{guild_id}/members</c>).
    /// </summary>
    /// <param name="caller">The caller.</param>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="limit">The page size.</param>
    /// <param name="after">The highest user ID of the previous page, <c>0</c> for the first page.</param>
    /// <returns>The page, <see cref="StateFailureKind.UnknownGuild"/> or <see cref="StateFailureKind.MissingAccess"/>.</returns>
    internal StateResult<ImmutableArray<MemberDetails>> ListGuildMembersPage(StateCaller caller, ulong guildId, int limit, ulong after)
    {
        lock (_gate)
        {
            var access = _access.ResolveGuild(caller, guildId, GuildAccessProfile.Denied);
            if (!access.TryGetValue(out var guild))
            {
                return access.Failure;
            }

            return guild.MemberUserIds
                .Where(userId => userId > after)
                .Order()
                .Take(limit)
                .Select(userId => DetailsOf(guild.Id, userId))
                .ToImmutableArray();
        }
    }

    /// <summary>
    /// Finds the members whose username or nickname starts with <paramref name="query"/>, ignoring case, by ascending user ID
    /// (<c>GET /guilds/{guild_id}/members/search</c>; the order of Discord's relevance ranking is not modeled).
    /// </summary>
    /// <param name="caller">The caller.</param>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="query">The prefix.</param>
    /// <param name="limit">The maximum number of members.</param>
    /// <returns>The members, <see cref="StateFailureKind.UnknownGuild"/> or <see cref="StateFailureKind.MissingAccess"/>.</returns>
    internal StateResult<ImmutableArray<MemberDetails>> SearchGuildMembers(StateCaller caller, ulong guildId, string query, int limit)
    {
        ArgumentNullException.ThrowIfNull(query);

        lock (_gate)
        {
            var access = _access.ResolveGuild(caller, guildId, GuildAccessProfile.Denied);
            if (!access.TryGetValue(out var guild))
            {
                return access.Failure;
            }

            return guild.MemberUserIds
                .Order()
                .Where(userId => _users[userId].Username.StartsWith(query, StringComparison.OrdinalIgnoreCase)
                    || _members[(guild.Id, userId)].Nick?.StartsWith(query, StringComparison.OrdinalIgnoreCase) == true)
                .Take(limit)
                .Select(userId => DetailsOf(guild.Id, userId))
                .ToImmutableArray();
        }
    }

    /// <summary>
    /// Checks that a caller may reach the members of a guild, for an operation that refuses before it needs a member
    /// (<c>PUT /guilds/{guild_id}/members/{user_id}</c>, which cannot validate an OAuth2 token yet).
    /// </summary>
    /// <param name="caller">The caller.</param>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>Success, <see cref="StateFailureKind.UnknownGuild"/> or <see cref="StateFailureKind.MissingAccess"/>.</returns>
    internal StateResult<bool> CheckGuildMemberAccess(StateCaller caller, ulong guildId)
    {
        lock (_gate)
        {
            var access = _access.ResolveGuild(caller, guildId, GuildAccessProfile.Denied);
            return access.IsSuccess ? true : access.Failure;
        }
    }

    /// <summary>Reads one member of a guild (<c>GET /guilds/{guild_id}/members/{user_id}</c>).</summary>
    /// <param name="caller">The caller.</param>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <returns>The member, <see cref="StateFailureKind.UnknownGuild"/>, <see cref="StateFailureKind.MissingAccess"/> or <see cref="StateFailureKind.UnknownMember"/>.</returns>
    internal StateResult<MemberDetails> GetGuildMember(StateCaller caller, ulong guildId, ulong userId)
    {
        lock (_gate)
        {
            var resolved = ResolveMember(caller, guildId, userId);
            return resolved.TryGetValue(out var member) ? DetailsOf(member) : resolved.Failure;
        }
    }

    /// <summary>
    /// Applies a partial update to a member (<c>PATCH /guilds/{guild_id}/members/{user_id}</c> and <c>/@me</c>). Every refusal comes
    /// before the first write, in this order: the guild, the member, the roles (the first unknown one), the timeout (more than 28 days
    /// away, then the owner and administrators, which cannot be timed out). A <see cref="MemberUpdated"/> is published only when a
    /// value actually changes; <c>@everyone</c> in the role list is ignored, like in <see cref="AddMember"/>.
    /// </summary>
    /// <param name="caller">The caller.</param>
    /// <param name="input">The update.</param>
    /// <returns>The member after the update, or the refusal.</returns>
    internal StateResult<MemberDetails> UpdateGuildMember(StateCaller caller, UpdateMemberInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var requestedRoleIds = input.RoleIds?.ToArray(); // copy: the caller's list may change under us

        lock (_gate)
        {
            var resolved = ResolveMember(caller, input.GuildId, input.UserId);
            if (!resolved.TryGetValue(out var member))
            {
                return resolved.Failure;
            }

            var guild = _guilds[input.GuildId];
            List<ulong>? roleIds = null;
            if (requestedRoleIds is not null)
            {
                roleIds = [];
                foreach (var roleId in requestedRoleIds)
                {
                    if (!_roles.TryGetValue(roleId, out var role) || role.GuildId != guild.Id)
                    {
                        return new StateFailure(StateFailureKind.UnknownRole, roleId);
                    }

                    if (roleId != guild.Id && !roleIds.Contains(roleId))
                    {
                        roleIds.Add(roleId);
                    }
                }
            }

            var now = ReadClock();
            if (input.CommunicationDisabledUntil is { IsSet: true, Value: { } until } && until > now)
            {
                if (until > now.AddDays(MaxMemberTimeoutDays))
                {
                    return new StateFailure(StateFailureKind.InvalidMemberTimeout, member.UserId);
                }

                if (IsOwnerOrAdministrator(guild, member))
                {
                    return new StateFailure(StateFailureKind.MissingPermissions, member.UserId);
                }
            }

            var changed = false;
            if (input.Nick.IsSet)
            {
                var nick = string.IsNullOrEmpty(input.Nick.Value) ? null : input.Nick.Value;
                if (!string.Equals(member.Nick, nick, StringComparison.Ordinal))
                {
                    member.Nick = nick;
                    changed = true;
                }
            }

            if (roleIds is not null && !roleIds.ToHashSet().SetEquals(member.RoleIds))
            {
                member.RoleIds.Clear();
                member.RoleIds.AddRange(roleIds);
                changed = true;
            }

            if (input.Mute is { } mute && member.Mute != mute)
            {
                member.Mute = mute;
                changed = true;
            }

            if (input.Deaf is { } deaf && member.Deaf != deaf)
            {
                member.Deaf = deaf;
                changed = true;
            }

            if (input.CommunicationDisabledUntil.IsSet && member.CommunicationDisabledUntil != input.CommunicationDisabledUntil.Value)
            {
                member.CommunicationDisabledUntil = input.CommunicationDisabledUntil.Value;
                changed = true;
            }

            if (input.Flags is { } flags && member.Flags != flags)
            {
                member.Flags = flags;
                changed = true;
            }

            if (input.Avatar.IsSet && !string.Equals(member.AvatarHash, input.Avatar.Value, StringComparison.Ordinal))
            {
                member.AvatarHash = input.Avatar.Value;
                changed = true;
            }

            if (input.Banner.IsSet && !string.Equals(member.BannerHash, input.Banner.Value, StringComparison.Ordinal))
            {
                member.BannerHash = input.Banner.Value;
                changed = true;
            }

            if (changed)
            {
                Publish(new MemberUpdated(NextChangeNumber(), member.ToSnapshot(), _users[member.UserId].ToSnapshot()));
            }

            return DetailsOf(member);
        }
    }

    /// <summary>
    /// Gives a role to a member (<c>PUT /guilds/{guild_id}/members/{user_id}/roles/{role_id}</c>): the guild, the member, then the role
    /// (<see cref="StateFailureKind.InvalidRole"/> for <c>@everyone</c>, <see cref="StateFailureKind.UnknownRole"/> for a role of another guild
    /// or none). Idempotent: a member that already has the role changes nothing and publishes nothing.
    /// </summary>
    /// <param name="caller">The caller.</param>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="roleId">The role ID.</param>
    /// <returns><see langword="true"/> when the member did not have the role, or the refusal.</returns>
    internal StateResult<bool> AddMemberRole(StateCaller caller, ulong guildId, ulong userId, ulong roleId)
        => ChangeMemberRole(caller, guildId, userId, roleId, give: true);

    /// <summary>
    /// Takes a role away from a member (<c>DELETE /guilds/{guild_id}/members/{user_id}/roles/{role_id}</c>), with the checks and the
    /// idempotence of <see cref="AddMemberRole"/>.
    /// </summary>
    /// <param name="caller">The caller.</param>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="roleId">The role ID.</param>
    /// <returns><see langword="true"/> when the member had the role, or the refusal.</returns>
    internal StateResult<bool> RemoveMemberRole(StateCaller caller, ulong guildId, ulong userId, ulong roleId)
        => ChangeMemberRole(caller, guildId, userId, roleId, give: false);

    private StateResult<bool> ChangeMemberRole(StateCaller caller, ulong guildId, ulong userId, ulong roleId, bool give)
    {
        lock (_gate)
        {
            var resolved = ResolveMember(caller, guildId, userId);
            if (!resolved.TryGetValue(out var member))
            {
                return resolved.Failure;
            }

            if (roleId == guildId)
            {
                return new StateFailure(StateFailureKind.InvalidRole, roleId);
            }

            if (!_roles.TryGetValue(roleId, out var role) || role.GuildId != guildId)
            {
                return new StateFailure(StateFailureKind.UnknownRole, roleId);
            }

            var has = member.RoleIds.Contains(role.Id);
            if (has == give)
            {
                return false;
            }

            if (give)
            {
                member.RoleIds.Add(role.Id);
            }
            else
            {
                member.RoleIds.Remove(role.Id);
            }

            Publish(new MemberUpdated(NextChangeNumber(), member.ToSnapshot(), _users[member.UserId].ToSnapshot()));
            return true;
        }
    }

    /// <summary>
    /// Removes a member from a guild (<c>DELETE /guilds/{guild_id}/members/{user_id}</c>, a kick). The user stays. The bot itself and the
    /// guild owner cannot be removed this way (<see cref="StateFailureKind.MissingPermissions"/>): the bot leaves a guild by
    /// <c>DELETE /users/@me/guilds/{guild_id}</c>, and the role hierarchy that would also protect other members is M4-REST-05.
    /// </summary>
    /// <param name="caller">The caller.</param>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <returns>Success, or the refusal.</returns>
    internal StateResult<bool> RemoveGuildMember(StateCaller caller, ulong guildId, ulong userId)
    {
        lock (_gate)
        {
            var resolved = ResolveMember(caller, guildId, userId);
            if (!resolved.TryGetValue(out var member))
            {
                return resolved.Failure;
            }

            var guild = _guilds[guildId];
            if (userId == _botId || userId == (guild.OwnerId ?? _botId))
            {
                return new StateFailure(StateFailureKind.MissingPermissions, userId);
            }

            var snapshot = member.ToSnapshot();
            var user = _users[userId].ToSnapshot();
            _members.Remove((guildId, userId));
            guild.MemberUserIds.Remove(userId);

            Publish(new MemberRemoved(NextChangeNumber(), snapshot, user));
            return true;
        }
    }

    /// <summary>Resolves the guild for a caller (the bot must be in it) then one of its members. Call only under the lock.</summary>
    private StateResult<MemberEntity> ResolveMember(StateCaller caller, ulong guildId, ulong userId)
    {
        var access = _access.ResolveGuild(caller, guildId, GuildAccessProfile.Denied);
        if (!access.TryGetValue(out var guild))
        {
            return access.Failure;
        }

        return _members.TryGetValue((guild.Id, userId), out var member)
            ? member
            : new StateFailure(StateFailureKind.UnknownMember, userId);
    }

    private MemberDetails DetailsOf(MemberEntity member) => new(member.ToSnapshot(), _users[member.UserId].ToSnapshot());

    private MemberDetails DetailsOf(ulong guildId, ulong userId) => DetailsOf(_members[(guildId, userId)]);

    /// <summary>Whether the member owns the guild or has the <c>ADMINISTRATOR</c> permission through <c>@everyone</c> or one of their roles. Call only under the lock.</summary>
    private bool IsOwnerOrAdministrator(GuildEntity guild, MemberEntity member)
    {
        if (member.UserId == (guild.OwnerId ?? _botId))
        {
            return true;
        }

        return member.RoleIds.Append(guild.Id).Any(roleId => _roles.TryGetValue(roleId, out var role) && (role.Permissions & AdministratorPermission) != 0);
    }
}
