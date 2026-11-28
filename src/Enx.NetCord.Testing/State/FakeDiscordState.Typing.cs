namespace Enx.NetCord.Testing.State;

internal sealed partial class FakeDiscordState
{
    // ----- Typing ---------------------------------------------------------------------------

    /// <summary>
    /// Records that a user started typing in a channel: nothing is stored, a <see cref="TypingStarted"/> is committed every time (the
    /// indicator lasts 10 seconds on Discord, and a client repeats it). A channel without chat (a category, a forum) refuses it with
    /// <see cref="StateFailureKind.CannotExecuteOnChannelType"/> (hypothesis, <c>docs/parity/errors.md</c>). The <c>SEND_MESSAGES</c>
    /// permission is not computed yet (M4-REST-05).
    /// </summary>
    /// <param name="caller">Who asks; <see cref="StateCaller.Bot"/> from <c>Rest/</c>.</param>
    /// <param name="channelId">The channel.</param>
    /// <param name="userId">The user who types, which must exist.</param>
    /// <returns><see langword="true"/> on success.</returns>
    internal StateResult<bool> TriggerTyping(StateCaller caller, ulong channelId, ulong userId)
    {
        lock (_gate)
        {
            var access = _access.ResolveChannel(caller, channelId);
            if (!access.TryGetValue(out var channel))
            {
                return access.Failure;
            }

            if (!ChannelTraits.Of(channel.Kind).IsMessageable)
            {
                return new StateFailure(StateFailureKind.CannotExecuteOnChannelType, channel.Id);
            }

            if (!_users.ContainsKey(userId))
            {
                return new StateFailure(StateFailureKind.UnknownUser, userId);
            }

            Publish(new TypingStarted(NextChangeNumber(), channel.GuildId, channel.Id, userId, ReadClock(), AuthorMemberOf(channel, userId)));
            return true;
        }
    }
}
