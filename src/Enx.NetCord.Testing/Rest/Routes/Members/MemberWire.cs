using System.Globalization;
using Enx.NetCord.Testing.Rest.Routes.Users;
using Enx.NetCord.Testing.Serialization;
using Enx.NetCord.Testing.State;

namespace Enx.NetCord.Testing.Rest.Routes.Members;

/// <summary>Projects detached members into the Discord <c>GuildMemberResponse</c>.</summary>
internal static class MemberWire
{
    public static GuildMemberResponse ToMember(MemberDetails details)
    {
        var member = details.Member;
        return new GuildMemberResponse
        {
            User = UserWire.ToUser(details.User),
            Nick = member.Nick,
            Avatar = member.AvatarHash,
            Banner = member.BannerHash,
            Roles = [.. member.RoleIds.Select(UserWire.Id)],
            JoinedAt = member.JoinedAt.ToString("O", CultureInfo.InvariantCulture),
            PremiumSince = null,
            Deaf = member.Deaf,
            Mute = member.Mute,
            Flags = member.Flags,
            Pending = false,
            CommunicationDisabledUntil = member.CommunicationDisabledUntil?.ToString("O", CultureInfo.InvariantCulture),
        };
    }
}
