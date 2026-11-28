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
    ulong? EveryonePermissions = null,
    bool WithBot = false);

internal sealed record CreateTextChannelInput(
    ulong GuildId,
    string Name,
    ulong? Id = null,
    string? Topic = null,
    int? Position = null,
    bool Nsfw = false);

internal sealed record CreateDmChannelInput(ulong RecipientId, ulong? Id = null);

// A partial channel update: null means "unchanged", except for the topic, where a null Topic only clears it
// when ChangeTopic is set (the wire distinguishes an absent topic from a null one). Type and ParentId are requests the
// state checks under its lock: a type other than the channel's, or any parent, is refused.
internal sealed record UpdateChannelInput(
    ulong ChannelId,
    string? Name = null,
    bool ChangeTopic = false,
    string? Topic = null,
    bool? Nsfw = null,
    int? RateLimitPerUser = null,
    int? Position = null,
    int? Type = null,
    ulong? ParentId = null);

internal sealed record CreateRoleInput(
    ulong GuildId,
    string Name,
    ulong? Id = null,
    ulong Permissions = 0,
    int Color = 0,
    bool Hoist = false,
    bool Mentionable = false,
    int? Position = null);

// JoinedAt is an explicit instant, allowed by ADR 0003 ("instant explicite" for seeded fixtures): a member that
// joined a year ago is a legitimate fixture. Unlike an ID it is not validated against the clock or the last
// allocation, and it never feeds the allocator; null means "the clock's instant of this mutation".
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
    int Flags = 0,
    IReadOnlyList<EmbedValue>? Embeds = null,
    IReadOnlyList<NewAttachment>? Attachments = null,
    AllowedMentionsValue? AllowedMentions = null,
    MessageReferenceInput? Reference = null,
    string? Nonce = null,
    bool EnforceNonce = false);

// A partial message edit: null means "unchanged", except for the content, where ChangeContent distinguishes an absent
// content from a null one (which clears it). Embeds is the complete new list when set (empty clears). RetainedAttachmentIds
// lists the existing attachments to keep (null keeps all); NewAttachments are appended, as Discord appends uploaded files.
// Flags is the requested bitfield: only the bits a user may change are applied. AllowedMentions drives the rebuild of the
// mentions when the content changes.
internal sealed record EditMessageInput(
    ulong ChannelId,
    ulong MessageId,
    bool ChangeContent = false,
    string? Content = null,
    IReadOnlyList<EmbedValue>? Embeds = null,
    int? Flags = null,
    AllowedMentionsValue? AllowedMentions = null,
    IReadOnlyList<ulong>? RetainedAttachmentIds = null,
    IReadOnlyList<NewAttachment>? NewAttachments = null);
