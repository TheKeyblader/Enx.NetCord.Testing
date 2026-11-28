using System.Text.Json.Serialization;

namespace Enx.NetCord.Testing.Serialization;

/// <summary>The Discord <c>GuildChannelResponse</c> (spec schema of <c>get_channel</c>) for a guild text channel.</summary>
internal sealed class GuildChannelResponse
{
    /// <summary>Gets the channel ID, a snowflake string.</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>Gets the channel type; <c>0</c> for a guild text channel.</summary>
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

    /// <summary>Gets the ID of the parent category; the fake models no category, so it is always <see langword="null"/>.</summary>
    [JsonPropertyName("parent_id")]
    public string? ParentId { get; init; }

    /// <summary>Gets the slowmode in seconds.</summary>
    [JsonPropertyName("rate_limit_per_user")]
    public required int RateLimitPerUser { get; init; }

    /// <summary>Gets the topic.</summary>
    [JsonPropertyName("topic")]
    public string? Topic { get; init; }

    /// <summary>Gets the position.</summary>
    [JsonPropertyName("position")]
    public required int Position { get; init; }

    /// <summary>Gets the permission overwrites; the fake models none, so the list is empty.</summary>
    [JsonPropertyName("permission_overwrites")]
    public required List<string> PermissionOverwrites { get; init; }

    /// <summary>Gets whether the channel is age-restricted.</summary>
    [JsonPropertyName("nsfw")]
    public required bool Nsfw { get; init; }

    /// <summary>Gets the initial slowmode of new threads.</summary>
    [JsonPropertyName("default_thread_rate_limit_per_user")]
    public required int DefaultThreadRateLimitPerUser { get; init; }
}
