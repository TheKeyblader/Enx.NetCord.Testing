using System.Text.Json.Serialization;

namespace Enx.NetCord.Testing.Serialization;

/// <summary>
/// The Discord <c>GuildMemberResponse</c> (also what <c>PrivateGuildMemberResponse</c> adds nothing required to: its optional
/// <c>permissions</c>, <c>avatar_decoration_data</c> and <c>collectibles</c> are not modeled and stay absent).
/// </summary>
internal sealed class GuildMemberResponse
{
    /// <summary>Gets the user the membership represents.</summary>
    [JsonPropertyName("user")]
    public required UserResponse User { get; init; }

    /// <summary>Gets the guild nickname, or <see langword="null"/> for none.</summary>
    [JsonPropertyName("nick")]
    public string? Nick { get; init; }

    /// <summary>Gets the guild avatar hash, or <see langword="null"/>.</summary>
    [JsonPropertyName("avatar")]
    public string? Avatar { get; init; }

    /// <summary>Gets the guild banner hash, or <see langword="null"/>.</summary>
    [JsonPropertyName("banner")]
    public string? Banner { get; init; }

    /// <summary>Gets the role IDs, snowflake strings; <c>@everyone</c> is never listed.</summary>
    [JsonPropertyName("roles")]
    public required List<string> Roles { get; init; }

    /// <summary>Gets when the user joined, ISO 8601.</summary>
    [JsonPropertyName("joined_at")]
    public required string JoinedAt { get; init; }

    /// <summary>Gets when the user started boosting the guild; always <see langword="null"/> (boosts are not modeled).</summary>
    [JsonPropertyName("premium_since")]
    public string? PremiumSince { get; init; }

    /// <summary>Gets whether the user is deafened in voice channels.</summary>
    [JsonPropertyName("deaf")]
    public required bool Deaf { get; init; }

    /// <summary>Gets whether the user is muted in voice channels.</summary>
    [JsonPropertyName("mute")]
    public required bool Mute { get; init; }

    /// <summary>Gets the guild member flags.</summary>
    [JsonPropertyName("flags")]
    public required int Flags { get; init; }

    /// <summary>Gets whether the user has not passed the membership screening; always <see langword="false"/> (screening is not modeled).</summary>
    [JsonPropertyName("pending")]
    public required bool Pending { get; init; }

    /// <summary>Gets when the timeout ends, ISO 8601, or <see langword="null"/>.</summary>
    [JsonPropertyName("communication_disabled_until")]
    public string? CommunicationDisabledUntil { get; init; }
}
