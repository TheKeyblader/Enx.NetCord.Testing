using System.Globalization;
using Enx.NetCord.Testing.Serialization;
using Enx.NetCord.Testing.State;

namespace Enx.NetCord.Testing.Rest.Routes.Users;

/// <summary>Projects detached state values into the wire DTOs of the users routes.</summary>
internal static class UserWire
{
    /// <summary>The locale of every fake account (Discord's default).</summary>
    private const string DefaultLocale = "en-US";

    public static string Id(ulong id) => id.ToString(CultureInfo.InvariantCulture);

    public static UserResponse ToUser(UserSnapshot user) => new()
    {
        Id = Id(user.Id),
        Username = user.Username,
        Avatar = user.AvatarHash,
        Discriminator = "0",
        PublicFlags = 0,
        Flags = 0,
        Bot = user.IsBot,
        Banner = user.BannerHash,
        GlobalName = user.GlobalName,
    };

    public static CurrentUserResponse ToCurrentUser(UserSnapshot user) => new()
    {
        Id = Id(user.Id),
        Username = user.Username,
        Avatar = user.AvatarHash,
        Discriminator = "0",
        PublicFlags = 0,
        Flags = 0,
        Bot = user.IsBot,
        Banner = user.BannerHash,
        GlobalName = user.GlobalName,
        MfaEnabled = false,
        Locale = DefaultLocale,
        Verified = true,
    };

    public static PrivateChannelResponse ToDmChannel(ChannelSnapshot channel, IEnumerable<UserSnapshot> recipients) => new()
    {
        Id = Id(channel.Id),
        Type = (int)channel.Kind,
        LastMessageId = channel.LastMessageId is { } last ? Id(last) : null,
        Flags = 0,
        Recipients = [.. recipients.Select(ToUser)],
    };
}
