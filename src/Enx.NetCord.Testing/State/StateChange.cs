namespace Enx.NetCord.Testing.State;

/// <summary>
/// A committed business fact. <see cref="Number"/> is the fake-wide, strictly increasing change number
/// (not the Gateway sequence <c>s</c>). Payloads are immutable values captured at commit; a fact may map
/// to no Discord event at all. The Gateway translates facts explicitly (no cast of a generic payload).
/// </summary>
internal abstract record StateChange(long Number);

internal sealed record BotIdentityCreated(long Number, UserSnapshot User, ApplicationSnapshot Application)
    : StateChange(Number);

internal sealed record UserCreated(long Number, UserSnapshot User) : StateChange(Number);

internal sealed record GuildCreated(long Number, GuildSnapshot Guild, RoleSnapshot Everyone)
    : StateChange(Number);

internal sealed record ChannelCreated(long Number, ChannelSnapshot Channel) : StateChange(Number);

internal sealed record RoleCreated(long Number, RoleSnapshot Role) : StateChange(Number);

internal sealed record MemberAdded(long Number, MemberSnapshot Member, UserSnapshot User) : StateChange(Number);

internal sealed record MessageCreated(
    long Number,
    MessageSnapshot Message,
    UserSnapshot Author,
    MemberSnapshot? AuthorMember) : StateChange(Number);

internal sealed record MessageEdited(long Number, MessageSnapshot Message, UserSnapshot Author)
    : StateChange(Number);

internal sealed record MessageDeleted(long Number, ulong? GuildId, ulong ChannelId, ulong MessageId)
    : StateChange(Number);
