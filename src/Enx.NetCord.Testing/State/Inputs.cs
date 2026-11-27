namespace Enx.NetCord.Testing.State;

// Inputs of state operations. `Id` is optional everywhere: null allocates through the generator; a
// value must be nonzero and never previously assigned in this fake, and is neither compared with the
// last allocated ID nor with the clock (its encoded millisecond is the entity's creation date).

internal sealed record CreateBotIdentityInput(
    string Username,
    string? ApplicationName = null,
    string? ApplicationDescription = null,
    ulong? Id = null);

internal sealed record CreateUserInput(
    string Username,
    ulong? Id = null,
    string? GlobalName = null,
    string? AvatarHash = null,
    bool IsBot = false);

internal sealed record CreateGuildInput(
    string Name,
    ulong? Id = null,
    ulong? OwnerId = null,
    string? IconHash = null,
    string? Description = null,
    ulong? EveryonePermissions = null);

internal sealed record CreateTextChannelInput(
    ulong GuildId,
    string Name,
    ulong? Id = null,
    string? Topic = null,
    int? Position = null,
    bool Nsfw = false);

internal sealed record CreateDmChannelInput(ulong RecipientId, ulong? Id = null);

internal sealed record CreateRoleInput(
    ulong GuildId,
    string Name,
    ulong? Id = null,
    ulong Permissions = 0,
    int Color = 0,
    bool Hoist = false,
    bool Mentionable = false,
    int? Position = null);

internal sealed record AddMemberInput(
    ulong GuildId,
    ulong UserId,
    string? Nick = null,
    IReadOnlyList<ulong>? RoleIds = null,
    DateTimeOffset? JoinedAt = null);

internal sealed record CreateMessageInput(
    ulong ChannelId,
    ulong AuthorId,
    string Content,
    ulong? Id = null,
    bool Tts = false,
    bool Pinned = false,
    int Type = 0,
    int Flags = 0);
