namespace Enx.NetCord.Testing.State;

/// <summary>
/// Input of <see cref="FakeDiscordState.UpdateUser"/>. A <see langword="null"/> <see cref="Username"/> leaves the name unchanged;
/// the avatar and banner use an explicit flag because <see langword="null"/> is a legitimate new value (the image is removed).
/// </summary>
internal sealed record UpdateUserInput(
    ulong UserId,
    string? Username = null,
    bool ChangeAvatar = false,
    string? AvatarHash = null,
    bool ChangeBanner = false,
    string? BannerHash = null);
