namespace Enx.NetCord.Testing.State;

// A partial member update (PATCH /guilds/{guild_id}/members/{user_id} and /@me): null or an unset Change means "unchanged".
// Nick, Avatar, Banner and CommunicationDisabledUntil are three-state: a set Change with a null value clears the field (an empty
// nickname clears it too). RoleIds is the complete new list when given (the implicit @everyone role is ignored). Mute and Deaf are
// stored as sent: the state has no voice states yet, so it cannot tell whether the member is connected (see docs/parity/rest.md).
internal sealed record UpdateMemberInput(
    ulong GuildId,
    ulong UserId,
    Change<string> Nick = default,
    IReadOnlyList<ulong>? RoleIds = null,
    bool? Mute = null,
    bool? Deaf = null,
    Change<DateTimeOffset?> CommunicationDisabledUntil = default,
    int? Flags = null,
    Change<string> Avatar = default,
    Change<string> Banner = default);
