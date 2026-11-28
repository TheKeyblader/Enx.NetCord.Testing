using System.Text.Json.Serialization;

namespace Enx.NetCord.Testing.Serialization;

// The spec has one schema for every guild channel (GuildChannelResponse) but the presence of a field depends on the channel family,
// not on its value (ADR 0013, § 1; ADR 0006, rules 4 and 5). One DTO per family, chosen by the channel kind.

/// <summary>The Discord <c>ChannelPermissionOverwriteResponse</c>; no overwrite is modeled before M4-REST-05, so the list that carries it is empty.</summary>
internal sealed class PermissionOverwriteResponse
{
    /// <summary>Gets the ID of the role or user the overwrite applies to.</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>Gets the overwrite type: <c>0</c> for a role, <c>1</c> for a member.</summary>
    [JsonPropertyName("type")]
    public required int Type { get; init; }

    /// <summary>Gets the allowed permissions, a stringified bitfield.</summary>
    [JsonPropertyName("allow")]
    public required string Allow { get; init; }

    /// <summary>Gets the denied permissions, a stringified bitfield.</summary>
    [JsonPropertyName("deny")]
    public required string Deny { get; init; }
}

/// <summary>A guild text (<c>0</c>) or announcement (<c>5</c>) channel.</summary>
internal sealed class TextChannelResponse
{
    /// <summary>Gets the channel ID, a snowflake string.</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>Gets the channel type.</summary>
    [JsonPropertyName("type")]
    public required int Type { get; init; }

    /// <summary>Gets the ID of the last message.</summary>
    [JsonPropertyName("last_message_id")]
    public string? LastMessageId { get; init; }

    /// <summary>Gets the channel flags.</summary>
    [JsonPropertyName("flags")]
    public required int Flags { get; init; }

    /// <summary>Gets the timestamp of the last pin.</summary>
    [JsonPropertyName("last_pin_timestamp")]
    public string? LastPinTimestamp { get; init; }

    /// <summary>Gets the ID of the guild.</summary>
    [JsonPropertyName("guild_id")]
    public required string GuildId { get; init; }

    /// <summary>Gets the name.</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>Gets the ID of the parent category, or <see langword="null"/>.</summary>
    [JsonPropertyName("parent_id")]
    public string? ParentId { get; init; }

    /// <summary>Gets the slowmode in seconds (always <c>0</c> for an announcement channel).</summary>
    [JsonPropertyName("rate_limit_per_user")]
    public required int RateLimitPerUser { get; init; }

    /// <summary>Gets the topic.</summary>
    [JsonPropertyName("topic")]
    public string? Topic { get; init; }

    /// <summary>Gets the position.</summary>
    [JsonPropertyName("position")]
    public required int Position { get; init; }

    /// <summary>Gets the permission overwrites; the fake models none yet (M4-REST-05), so the list is empty.</summary>
    [JsonPropertyName("permission_overwrites")]
    public required List<PermissionOverwriteResponse> PermissionOverwrites { get; init; }

    /// <summary>Gets whether the channel is age-restricted.</summary>
    [JsonPropertyName("nsfw")]
    public required bool Nsfw { get; init; }

    /// <summary>Gets the default thread auto-archive duration; absent when not defined.</summary>
    [JsonPropertyName("default_auto_archive_duration")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? DefaultAutoArchiveDuration { get; init; }

    /// <summary>Gets the initial slowmode of new threads.</summary>
    [JsonPropertyName("default_thread_rate_limit_per_user")]
    public required int DefaultThreadRateLimitPerUser { get; init; }
}

/// <summary>A guild voice (<c>2</c>) or stage (<c>13</c>) channel.</summary>
internal sealed class VoiceChannelResponse
{
    /// <summary>Gets the channel ID, a snowflake string.</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>Gets the channel type.</summary>
    [JsonPropertyName("type")]
    public required int Type { get; init; }

    /// <summary>Gets the ID of the last message of the channel's chat.</summary>
    [JsonPropertyName("last_message_id")]
    public string? LastMessageId { get; init; }

    /// <summary>Gets the channel flags.</summary>
    [JsonPropertyName("flags")]
    public required int Flags { get; init; }

    /// <summary>Gets the timestamp of the last pin.</summary>
    [JsonPropertyName("last_pin_timestamp")]
    public string? LastPinTimestamp { get; init; }

    /// <summary>Gets the ID of the guild.</summary>
    [JsonPropertyName("guild_id")]
    public required string GuildId { get; init; }

    /// <summary>Gets the name.</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>Gets the ID of the parent category, or <see langword="null"/>.</summary>
    [JsonPropertyName("parent_id")]
    public string? ParentId { get; init; }

    /// <summary>Gets the slowmode in seconds.</summary>
    [JsonPropertyName("rate_limit_per_user")]
    public required int RateLimitPerUser { get; init; }

    /// <summary>Gets the bitrate.</summary>
    [JsonPropertyName("bitrate")]
    public required int Bitrate { get; init; }

    /// <summary>Gets the user limit, <c>0</c> for none.</summary>
    [JsonPropertyName("user_limit")]
    public required int UserLimit { get; init; }

    /// <summary>Gets the voice region override, or <see langword="null"/> for automatic.</summary>
    [JsonPropertyName("rtc_region")]
    public string? RtcRegion { get; init; }

    /// <summary>Gets the position.</summary>
    [JsonPropertyName("position")]
    public required int Position { get; init; }

    /// <summary>Gets the permission overwrites; the fake models none yet (M4-REST-05), so the list is empty.</summary>
    [JsonPropertyName("permission_overwrites")]
    public required List<PermissionOverwriteResponse> PermissionOverwrites { get; init; }

    /// <summary>Gets whether the channel is age-restricted.</summary>
    [JsonPropertyName("nsfw")]
    public required bool Nsfw { get; init; }

    /// <summary>Gets the video quality mode; absent when not defined.</summary>
    [JsonPropertyName("video_quality_mode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? VideoQualityMode { get; init; }
}

/// <summary>A guild category (<c>4</c>).</summary>
internal sealed class CategoryChannelResponse
{
    /// <summary>Gets the channel ID, a snowflake string.</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>Gets the channel type; <c>4</c>.</summary>
    [JsonPropertyName("type")]
    public required int Type { get; init; }

    /// <summary>Gets the channel flags.</summary>
    [JsonPropertyName("flags")]
    public required int Flags { get; init; }

    /// <summary>Gets the ID of the guild.</summary>
    [JsonPropertyName("guild_id")]
    public required string GuildId { get; init; }

    /// <summary>Gets the name.</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>Gets the parent ID; always <see langword="null"/> for a category.</summary>
    [JsonPropertyName("parent_id")]
    public string? ParentId { get; init; }

    /// <summary>Gets the position.</summary>
    [JsonPropertyName("position")]
    public required int Position { get; init; }

    /// <summary>Gets the permission overwrites; the fake models none yet (M4-REST-05), so the list is empty.</summary>
    [JsonPropertyName("permission_overwrites")]
    public required List<PermissionOverwriteResponse> PermissionOverwrites { get; init; }

    /// <summary>Gets the age restriction; always <see langword="false"/> for a category.</summary>
    [JsonPropertyName("nsfw")]
    public required bool Nsfw { get; init; }
}

/// <summary>The Discord <c>GuildChannelTagResponse</c>; no tag is modeled before M4-REST-01, so the list that carries it is empty.</summary>
internal sealed class ForumTagResponse
{
    /// <summary>Gets the tag ID.</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>Gets the tag name.</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>Gets whether only moderators can apply the tag.</summary>
    [JsonPropertyName("moderated")]
    public required bool Moderated { get; init; }
}

/// <summary>The Discord <c>DefaultReactionEmojiResponse</c>; none is modeled before M4-REST-01, so the field that carries it is <see langword="null"/>.</summary>
internal sealed class ForumReactionEmojiResponse
{
    /// <summary>Gets the ID of a custom emoji, or <see langword="null"/>.</summary>
    [JsonPropertyName("emoji_id")]
    public string? EmojiId { get; init; }

    /// <summary>Gets the Unicode emoji, or <see langword="null"/>.</summary>
    [JsonPropertyName("emoji_name")]
    public string? EmojiName { get; init; }
}

/// <summary>A guild forum (<c>15</c>) or media (<c>16</c>) channel.</summary>
internal sealed class ForumChannelResponse
{
    /// <summary>Gets the channel ID, a snowflake string.</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>Gets the channel type.</summary>
    [JsonPropertyName("type")]
    public required int Type { get; init; }

    /// <summary>Gets the ID of the last post (thread) of the channel.</summary>
    [JsonPropertyName("last_message_id")]
    public string? LastMessageId { get; init; }

    /// <summary>Gets the channel flags.</summary>
    [JsonPropertyName("flags")]
    public required int Flags { get; init; }

    /// <summary>Gets the ID of the guild.</summary>
    [JsonPropertyName("guild_id")]
    public required string GuildId { get; init; }

    /// <summary>Gets the name.</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>Gets the ID of the parent category, or <see langword="null"/>.</summary>
    [JsonPropertyName("parent_id")]
    public string? ParentId { get; init; }

    /// <summary>Gets the slowmode in seconds.</summary>
    [JsonPropertyName("rate_limit_per_user")]
    public required int RateLimitPerUser { get; init; }

    /// <summary>Gets the guidelines.</summary>
    [JsonPropertyName("topic")]
    public string? Topic { get; init; }

    /// <summary>Gets the position.</summary>
    [JsonPropertyName("position")]
    public required int Position { get; init; }

    /// <summary>Gets the permission overwrites; the fake models none yet (M4-REST-05), so the list is empty.</summary>
    [JsonPropertyName("permission_overwrites")]
    public required List<PermissionOverwriteResponse> PermissionOverwrites { get; init; }

    /// <summary>Gets whether the channel is age-restricted.</summary>
    [JsonPropertyName("nsfw")]
    public required bool Nsfw { get; init; }

    /// <summary>Gets the default thread auto-archive duration; absent when not defined.</summary>
    [JsonPropertyName("default_auto_archive_duration")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? DefaultAutoArchiveDuration { get; init; }

    /// <summary>Gets the initial slowmode of new posts.</summary>
    [JsonPropertyName("default_thread_rate_limit_per_user")]
    public required int DefaultThreadRateLimitPerUser { get; init; }

    /// <summary>Gets the tags; empty until M4-REST-01.</summary>
    [JsonPropertyName("available_tags")]
    public required List<ForumTagResponse> AvailableTags { get; init; }

    /// <summary>Gets the default reaction emoji; <see langword="null"/> until M4-REST-01.</summary>
    [JsonPropertyName("default_reaction_emoji")]
    public ForumReactionEmojiResponse? DefaultReactionEmoji { get; init; }

    /// <summary>Gets the default sort order of posts, or <see langword="null"/>.</summary>
    [JsonPropertyName("default_sort_order")]
    public int? DefaultSortOrder { get; init; }

    /// <summary>Gets the default layout of posts; absent for a media channel.</summary>
    [JsonPropertyName("default_forum_layout")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? DefaultForumLayout { get; init; }
}
