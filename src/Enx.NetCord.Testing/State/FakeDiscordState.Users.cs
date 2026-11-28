namespace Enx.NetCord.Testing.State;

internal sealed partial class FakeDiscordState
{
    /// <summary>
    /// Applies a partial profile update to a user. A change is committed (and <see cref="UserUpdated"/> published) only
    /// when at least one value actually differs, so a repeated or empty update is observable as such.
    /// </summary>
    /// <param name="input">The update.</param>
    /// <returns>The user after the update, or <see cref="StateFailureKind.UnknownUser"/>.</returns>
    internal StateResult<UserSnapshot> UpdateUser(UpdateUserInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        lock (_gate)
        {
            if (!_users.TryGetValue(input.UserId, out var user))
            {
                return new StateFailure(StateFailureKind.UnknownUser, input.UserId);
            }

            var changed = false;
            if (input.Username is { } username && !string.Equals(user.Username, username, StringComparison.Ordinal))
            {
                user.Username = username;
                changed = true;
            }

            if (input.ChangeAvatar && !string.Equals(user.AvatarHash, input.AvatarHash, StringComparison.Ordinal))
            {
                user.AvatarHash = input.AvatarHash;
                changed = true;
            }

            if (input.ChangeBanner && !string.Equals(user.BannerHash, input.BannerHash, StringComparison.Ordinal))
            {
                user.BannerHash = input.BannerHash;
                changed = true;
            }

            var snapshot = user.ToSnapshot();
            if (changed)
            {
                Publish(new UserUpdated(NextChangeNumber(), snapshot));
            }

            return snapshot;
        }
    }
}
