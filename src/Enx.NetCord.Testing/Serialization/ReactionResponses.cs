using System.Text.Json.Serialization;

namespace Enx.NetCord.Testing.Serialization;

/// <summary>The Discord <c>MessageReactionEmojiResponse</c>: <c>id</c> is <c>null</c> for a unicode emoji.</summary>
internal sealed class MessageReactionEmojiResponse
{
    /// <summary>Gets the custom emoji ID, written as <c>null</c> for a unicode emoji.</summary>
    [JsonPropertyName("id")]
    public required string? Id { get; init; }

    /// <summary>Gets the unicode emoji, or the name of the custom emoji.</summary>
    [JsonPropertyName("name")]
    public required string? Name { get; init; }
}

/// <summary>The Discord <c>MessageReactionCountDetailsResponse</c>.</summary>
internal sealed class MessageReactionCountDetailsResponse
{
    /// <summary>Gets the number of super reactions.</summary>
    [JsonPropertyName("burst")]
    public required int Burst { get; init; }

    /// <summary>Gets the number of normal reactions.</summary>
    [JsonPropertyName("normal")]
    public required int Normal { get; init; }
}

/// <summary>The Discord <c>MessageReactionResponse</c>, one element of the <c>reactions</c> of a message.</summary>
internal sealed class MessageReactionResponse
{
    /// <summary>Gets the emoji.</summary>
    [JsonPropertyName("emoji")]
    public required MessageReactionEmojiResponse Emoji { get; init; }

    /// <summary>Gets the total number of reactions, super reactions included.</summary>
    [JsonPropertyName("count")]
    public required int Count { get; init; }

    /// <summary>Gets the normal and super counts.</summary>
    [JsonPropertyName("count_details")]
    public required MessageReactionCountDetailsResponse CountDetails { get; init; }

    /// <summary>Gets the colors of the super reactions; the fake never assigns any.</summary>
    [JsonPropertyName("burst_colors")]
    public required List<string> BurstColors { get; init; }

    /// <summary>Gets whether the current user super-reacted with this emoji.</summary>
    [JsonPropertyName("me_burst")]
    public required bool MeBurst { get; init; }

    /// <summary>Gets whether the current user reacted with this emoji.</summary>
    [JsonPropertyName("me")]
    public required bool Me { get; init; }
}
