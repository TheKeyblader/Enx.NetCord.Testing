using System.Collections.Immutable;

namespace Enx.NetCord.Testing.State;

// Mutable server-side entities. They never leave FakeDiscordState: callers only get snapshots.
// Relations are IDs, not references.

internal sealed class UserEntity(ulong id, string username, long ordinal)
{
    internal ulong Id { get; } = id;
    internal long Ordinal { get; } = ordinal;
    internal string Username { get; set; } = username;
    internal string? GlobalName { get; set; }
    internal string? AvatarHash { get; set; }
    internal bool IsBot { get; set; }

    internal UserSnapshot ToSnapshot() =>
        new(Id, Username, GlobalName, AvatarHash, IsBot, Snowflake.CreatedAt(Id), Ordinal);
}

internal sealed class ApplicationEntity(ulong id, string name, long ordinal)
{
    internal ulong Id { get; } = id;
    internal long Ordinal { get; } = ordinal;
    internal string Name { get; set; } = name;
    internal string Description { get; set; } = string.Empty;

    internal ApplicationSnapshot ToSnapshot() =>
        new(Id, Name, Description, Snowflake.CreatedAt(Id), Ordinal);
}

internal sealed class GuildEntity(ulong id, string name, long ordinal)
{
    internal ulong Id { get; } = id;
    internal long Ordinal { get; } = ordinal;
    internal string Name { get; set; } = name;
    internal ulong? OwnerId { get; set; }
    internal string? IconHash { get; set; }
    internal string? Description { get; set; }

    // Children in creation order.
    internal List<ulong> ChannelIds { get; } = [];
    internal List<ulong> RoleIds { get; } = [];
    internal List<ulong> MemberUserIds { get; } = [];

    internal GuildSnapshot ToSnapshot() =>
        new(Id, Name, OwnerId, IconHash, Description, Snowflake.CreatedAt(Id), Ordinal);
}

internal sealed class ChannelEntity(ulong id, ChannelKind kind, long ordinal)
{
    internal ulong Id { get; } = id;
    internal ChannelKind Kind { get; } = kind;
    internal long Ordinal { get; } = ordinal;
    internal ulong? GuildId { get; set; }
    internal string? Name { get; set; }
    internal string? Topic { get; set; }
    internal int Position { get; set; }
    internal bool Nsfw { get; set; }
    internal ulong? LastMessageId { get; set; }
    internal List<ulong> RecipientIds { get; } = [];

    // Message IDs ordered by ID (Discord's ordering), for list queries.
    internal SortedSet<ulong> MessageIds { get; } = [];

    internal ChannelSnapshot ToSnapshot() =>
        new(Id, Kind, GuildId, Name, Topic, Position, Nsfw, LastMessageId,
            [.. RecipientIds], Snowflake.CreatedAt(Id), Ordinal);
}

internal sealed class RoleEntity(ulong id, ulong guildId, string name, long ordinal)
{
    internal ulong Id { get; } = id;
    internal ulong GuildId { get; } = guildId;
    internal long Ordinal { get; } = ordinal;
    internal string Name { get; set; } = name;
    internal ulong Permissions { get; set; }
    internal int Color { get; set; }
    internal bool Hoist { get; set; }
    internal bool Mentionable { get; set; }
    internal bool Managed { get; set; }
    internal int Position { get; set; }

    internal RoleSnapshot ToSnapshot() =>
        new(Id, GuildId, Name, Permissions, Color, Hoist, Mentionable, Managed, Position,
            Snowflake.CreatedAt(Id), Ordinal);
}

internal sealed class MemberEntity(ulong guildId, ulong userId, DateTimeOffset joinedAt, long ordinal)
{
    internal ulong GuildId { get; } = guildId;
    internal ulong UserId { get; } = userId;
    internal long Ordinal { get; } = ordinal;
    internal DateTimeOffset JoinedAt { get; set; } = joinedAt;
    internal string? Nick { get; set; }
    internal bool Deaf { get; set; }
    internal bool Mute { get; set; }

    // Excludes the implicit @everyone role, as the Discord `roles` member field does.
    internal List<ulong> RoleIds { get; } = [];

    internal MemberSnapshot ToSnapshot() =>
        new(GuildId, UserId, Nick, [.. RoleIds], JoinedAt, Deaf, Mute, Ordinal);
}

internal sealed class MessageEntity(ulong id, ulong channelId, ulong authorId, long ordinal)
{
    internal ulong Id { get; } = id;
    internal ulong ChannelId { get; } = channelId;
    internal ulong AuthorId { get; } = authorId;
    internal long Ordinal { get; } = ordinal;
    internal string Content { get; set; } = string.Empty;
    internal DateTimeOffset? EditedTimestamp { get; set; }
    internal bool Tts { get; set; }
    internal bool Pinned { get; set; }
    internal int Type { get; set; }
    internal int Flags { get; set; }

    internal MessageSnapshot ToSnapshot(ulong? guildId) =>
        new(Id, ChannelId, guildId, AuthorId, Content, Snowflake.CreatedAt(Id), EditedTimestamp,
            Tts, Pinned, Type, Flags, Ordinal);
}
