using System.Text.Json.Serialization;

namespace Enx.NetCord.Testing.Serialization;

/// <summary>The Discord error body: <c>message</c>, integer <c>code</c> and, for 50035, the <c>errors</c> tree.</summary>
internal sealed class ErrorResponse
{
    /// <summary>Gets or sets the message.</summary>
    [JsonPropertyName("message")]
    public required string Message { get; init; }

    /// <summary>Gets or sets the Discord JSON error code (0 for a general error).</summary>
    [JsonPropertyName("code")]
    public required int Code { get; init; }

    /// <summary>Gets or sets the field errors; omitted (never <c>null</c>) for simple errors.</summary>
    [JsonPropertyName("errors")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public FormErrorNode? Errors { get; init; }
}

/// <summary>The per-user 429 body.</summary>
internal sealed class RateLimitedResponse
{
    /// <summary>Gets or sets the message.</summary>
    [JsonPropertyName("message")]
    public required string Message { get; init; }

    /// <summary>Gets or sets the number of seconds to wait.</summary>
    [JsonPropertyName("retry_after")]
    public required int RetryAfter { get; init; }

    /// <summary>Gets or sets whether the limit is global.</summary>
    [JsonPropertyName("global")]
    public required bool Global { get; init; }
}
