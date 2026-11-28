using System.Text.Json.Serialization;

namespace Enx.NetCord.Testing.Serialization;

/// <summary>The Discord <c>PinnedMessageResponse</c>: a message and the time it was pinned.</summary>
internal sealed class PinnedMessageResponse
{
    /// <summary>Gets when the message was pinned (ISO 8601).</summary>
    [JsonPropertyName("pinned_at")]
    public required string PinnedAt { get; init; }

    /// <summary>Gets the pinned message.</summary>
    [JsonPropertyName("message")]
    public required MessageResponse Message { get; init; }
}

/// <summary>The Discord <c>PinnedMessagesResponse</c>, the body of <c>GET /channels/{channel_id}/messages/pins</c>.</summary>
internal sealed class PinnedMessagesResponse
{
    /// <summary>Gets the pins of the page, newest first.</summary>
    [JsonPropertyName("items")]
    public required List<PinnedMessageResponse> Items { get; init; }

    /// <summary>Gets whether older pins remain.</summary>
    [JsonPropertyName("has_more")]
    public required bool HasMore { get; init; }
}
