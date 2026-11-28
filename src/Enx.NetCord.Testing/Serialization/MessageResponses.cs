using System.Text.Json.Serialization;

namespace Enx.NetCord.Testing.Serialization;

/// <summary>The Discord <c>MessageEmbedFooterResponse</c>.</summary>
internal sealed class MessageEmbedFooterResponse
{
    /// <summary>Gets the footer text.</summary>
    [JsonPropertyName("text")]
    public string? Text { get; init; }

    /// <summary>Gets the footer icon URL.</summary>
    [JsonPropertyName("icon_url")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? IconUrl { get; init; }
}

/// <summary>The Discord <c>MessageEmbedAuthorResponse</c>.</summary>
internal sealed class MessageEmbedAuthorResponse
{
    /// <summary>Gets the author name.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>Gets the author URL.</summary>
    [JsonPropertyName("url")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Url { get; init; }

    /// <summary>Gets the author icon URL.</summary>
    [JsonPropertyName("icon_url")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? IconUrl { get; init; }
}

/// <summary>The Discord <c>MessageEmbedImageResponse</c> (image or thumbnail).</summary>
internal sealed class MessageEmbedImageResponse
{
    /// <summary>Gets the image URL.</summary>
    [JsonPropertyName("url")]
    public string? Url { get; init; }

    /// <summary>Gets the width, if sent.</summary>
    [JsonPropertyName("width")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Width { get; init; }

    /// <summary>Gets the height, if sent.</summary>
    [JsonPropertyName("height")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Height { get; init; }
}

/// <summary>The Discord <c>MessageEmbedFieldResponse</c>.</summary>
internal sealed class MessageEmbedFieldResponse
{
    /// <summary>Gets the field name.</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>Gets the field value.</summary>
    [JsonPropertyName("value")]
    public required string Value { get; init; }

    /// <summary>Gets whether the field is displayed inline.</summary>
    [JsonPropertyName("inline")]
    public required bool Inline { get; init; }
}

/// <summary>The Discord <c>MessageEmbedResponse</c>; the fake only produces <c>rich</c> embeds, and omits what was not sent.</summary>
internal sealed class MessageEmbedResponse
{
    /// <summary>Gets the embed type, always <c>rich</c>.</summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>Gets the title.</summary>
    [JsonPropertyName("title")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Title { get; init; }

    /// <summary>Gets the description.</summary>
    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }

    /// <summary>Gets the URL.</summary>
    [JsonPropertyName("url")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Url { get; init; }

    /// <summary>Gets the color.</summary>
    [JsonPropertyName("color")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Color { get; init; }

    /// <summary>Gets the ISO 8601 timestamp.</summary>
    [JsonPropertyName("timestamp")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Timestamp { get; init; }

    /// <summary>Gets the footer.</summary>
    [JsonPropertyName("footer")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MessageEmbedFooterResponse? Footer { get; init; }

    /// <summary>Gets the image.</summary>
    [JsonPropertyName("image")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MessageEmbedImageResponse? Image { get; init; }

    /// <summary>Gets the thumbnail.</summary>
    [JsonPropertyName("thumbnail")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MessageEmbedImageResponse? Thumbnail { get; init; }

    /// <summary>Gets the author.</summary>
    [JsonPropertyName("author")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MessageEmbedAuthorResponse? Author { get; init; }

    /// <summary>Gets the fields.</summary>
    [JsonPropertyName("fields")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<MessageEmbedFieldResponse>? Fields { get; init; }
}

/// <summary>The Discord <c>MessageAttachmentResponse</c>. The URLs are deterministic and fictitious: the fake serves no file.</summary>
internal sealed class MessageAttachmentResponse
{
    /// <summary>Gets the attachment ID.</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>Gets the file name.</summary>
    [JsonPropertyName("filename")]
    public required string FileName { get; init; }

    /// <summary>Gets the size in bytes.</summary>
    [JsonPropertyName("size")]
    public required long Size { get; init; }

    /// <summary>Gets the URL.</summary>
    [JsonPropertyName("url")]
    public required string Url { get; init; }

    /// <summary>Gets the proxy URL.</summary>
    [JsonPropertyName("proxy_url")]
    public required string ProxyUrl { get; init; }

    /// <summary>Gets the media type.</summary>
    [JsonPropertyName("content_type")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ContentType { get; init; }

    /// <summary>Gets the title.</summary>
    [JsonPropertyName("title")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Title { get; init; }

    /// <summary>Gets the description.</summary>
    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }
}

/// <summary>The Discord <c>MessageReferenceResponse</c>.</summary>
internal sealed class MessageReferenceResponse
{
    /// <summary>Gets the reference type; <c>0</c> for a reply.</summary>
    [JsonPropertyName("type")]
    public required int Type { get; init; }

    /// <summary>Gets the channel ID.</summary>
    [JsonPropertyName("channel_id")]
    public required string ChannelId { get; init; }

    /// <summary>Gets the message ID.</summary>
    [JsonPropertyName("message_id")]
    public required string MessageId { get; init; }

    /// <summary>Gets the guild ID, absent in a DM.</summary>
    [JsonPropertyName("guild_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? GuildId { get; init; }
}

/// <summary>A message component. The fake models none, so a message always carries an empty list of this type.</summary>
internal sealed class MessageComponentResponse;

/// <summary>The Discord <c>MessageResponse</c> (spec schema of <c>get_message</c>), as sent in REST responses: no <c>guild_id</c> and no <c>member</c>.</summary>
internal sealed class MessageResponse
{
    /// <summary>Gets the message ID.</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>Gets the channel ID.</summary>
    [JsonPropertyName("channel_id")]
    public required string ChannelId { get; init; }

    /// <summary>Gets the author.</summary>
    [JsonPropertyName("author")]
    public required UserResponse Author { get; init; }

    /// <summary>Gets the content.</summary>
    [JsonPropertyName("content")]
    public required string Content { get; init; }

    /// <summary>Gets the creation instant, ISO 8601.</summary>
    [JsonPropertyName("timestamp")]
    public required string Timestamp { get; init; }

    /// <summary>Gets the instant of the last edit, <see langword="null"/> if never edited.</summary>
    [JsonPropertyName("edited_timestamp")]
    public required string? EditedTimestamp { get; init; }

    /// <summary>Gets whether the message is text-to-speech.</summary>
    [JsonPropertyName("tts")]
    public required bool Tts { get; init; }

    /// <summary>Gets whether <c>@everyone</c> or <c>@here</c> is mentioned.</summary>
    [JsonPropertyName("mention_everyone")]
    public required bool MentionEveryone { get; init; }

    /// <summary>Gets the mentioned users.</summary>
    [JsonPropertyName("mentions")]
    public required List<UserResponse> Mentions { get; init; }

    /// <summary>Gets the mentioned role IDs.</summary>
    [JsonPropertyName("mention_roles")]
    public required List<string> MentionRoles { get; init; }

    /// <summary>Gets the attachments.</summary>
    [JsonPropertyName("attachments")]
    public required List<MessageAttachmentResponse> Attachments { get; init; }

    /// <summary>Gets the embeds.</summary>
    [JsonPropertyName("embeds")]
    public required List<MessageEmbedResponse> Embeds { get; init; }

    /// <summary>Gets whether the message is pinned.</summary>
    [JsonPropertyName("pinned")]
    public required bool Pinned { get; init; }

    /// <summary>Gets the message type.</summary>
    [JsonPropertyName("type")]
    public required int Type { get; init; }

    /// <summary>Gets the message flags.</summary>
    [JsonPropertyName("flags")]
    public required int Flags { get; init; }

    /// <summary>Gets the components; always empty.</summary>
    [JsonPropertyName("components")]
    public required List<MessageComponentResponse> Components { get; init; }

    /// <summary>Gets the nonce the client sent.</summary>
    [JsonPropertyName("nonce")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Nonce { get; init; }

    /// <summary>Gets the reply reference.</summary>
    [JsonPropertyName("message_reference")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MessageReferenceResponse? MessageReference { get; init; }

    /// <summary>Gets the replied-to message, if it still exists.</summary>
    [JsonPropertyName("referenced_message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MessageResponse? ReferencedMessage { get; init; }
}
