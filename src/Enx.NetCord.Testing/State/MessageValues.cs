using System.Collections.Immutable;

namespace Enx.NetCord.Testing.State;

// Immutable values carried by messages. They are plain data (no behaviour) shared by inputs, entities and snapshots,
// so a message never exposes anything mutable. Embeds keep the strings as sent: Discord does not normalize them.

/// <summary>The footer of an embed.</summary>
internal sealed record EmbedFooterValue(string? Text, string? IconUrl);

/// <summary>The author block of an embed.</summary>
internal sealed record EmbedAuthorValue(string? Name, string? Url, string? IconUrl);

/// <summary>An embed image or thumbnail. The fake never fetches or resizes anything, so width and height are the ones sent.</summary>
internal sealed record EmbedMediaValue(string? Url, int? Width, int? Height);

/// <summary>A field of an embed.</summary>
internal sealed record EmbedFieldValue(string Name, string Value, bool Inline);

/// <summary>A <c>rich</c> embed. <see cref="Timestamp"/> is the ISO 8601 text as sent.</summary>
internal sealed record EmbedValue(
    string? Title,
    string? Description,
    string? Url,
    int? Color,
    string? Timestamp,
    EmbedFooterValue? Footer,
    EmbedMediaValue? Image,
    EmbedMediaValue? Thumbnail,
    EmbedAuthorValue? Author,
    ImmutableArray<EmbedFieldValue> Fields);

/// <summary>An attachment of a message. Only the metadata is kept: the fake serves no file.</summary>
internal sealed record AttachmentValue(
    ulong Id,
    string FileName,
    long Size,
    string? ContentType,
    string? Title,
    string? Description);

/// <summary>An uploaded file, before the state gives it an ID.</summary>
internal sealed record NewAttachment(
    string FileName,
    long Size,
    string? ContentType = null,
    string? Title = null,
    string? Description = null);

/// <summary>The <c>allowed_mentions</c> of a request. An absent object (<see langword="null"/> where it is used) allows everything.</summary>
internal sealed record AllowedMentionsValue(
    bool ParseUsers,
    bool ParseRoles,
    bool ParseEveryone,
    ImmutableArray<ulong> Users,
    ImmutableArray<ulong> Roles,
    bool RepliedUser);

/// <summary>The <c>message_reference</c> of a reply, as sent.</summary>
internal sealed record MessageReferenceInput(ulong MessageId, ulong? ChannelId, bool FailIfNotExists);

/// <summary>The resolved reply target stored on a message.</summary>
internal sealed record MessageReferenceValue(ulong MessageId, ulong ChannelId, ulong? GuildId);
