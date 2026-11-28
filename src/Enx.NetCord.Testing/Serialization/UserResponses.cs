using System.Text.Json.Serialization;

namespace Enx.NetCord.Testing.Serialization;

/// <summary>The primary-guild object of a user; the fake never sets one, so it only appears as <c>null</c>.</summary>
internal sealed class UserPrimaryGuildResponse
{
    /// <summary>Gets the ID of the user's primary guild.</summary>
    [JsonPropertyName("identity_guild_id")]
    public string? IdentityGuildId { get; init; }

    /// <summary>Gets whether the server tag is displayed.</summary>
    [JsonPropertyName("identity_enabled")]
    public bool? IdentityEnabled { get; init; }

    /// <summary>Gets the server tag.</summary>
    [JsonPropertyName("tag")]
    public string? Tag { get; init; }

    /// <summary>Gets the server tag badge hash.</summary>
    [JsonPropertyName("badge")]
    public string? Badge { get; init; }
}

/// <summary>The Discord <c>UserResponse</c> (spec schema of <c>get_user</c>); nullable fields are written as <c>null</c>.</summary>
internal class UserResponse
{
    /// <summary>Gets the user ID, a snowflake string.</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>Gets the username.</summary>
    [JsonPropertyName("username")]
    public required string Username { get; init; }

    /// <summary>Gets the avatar hash.</summary>
    [JsonPropertyName("avatar")]
    public required string? Avatar { get; init; }

    /// <summary>Gets the discriminator; always <c>"0"</c> (users migrated to unique usernames).</summary>
    [JsonPropertyName("discriminator")]
    public required string Discriminator { get; init; }

    /// <summary>Gets the public flags.</summary>
    [JsonPropertyName("public_flags")]
    public required int PublicFlags { get; init; }

    /// <summary>Gets the flags.</summary>
    [JsonPropertyName("flags")]
    public required long Flags { get; init; }

    /// <summary>Gets whether the user is a bot; written only when <see langword="true"/>, as Discord does.</summary>
    [JsonPropertyName("bot")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Bot { get; init; }

    /// <summary>Gets the banner hash.</summary>
    [JsonPropertyName("banner")]
    public string? Banner { get; init; }

    /// <summary>Gets the accent color.</summary>
    [JsonPropertyName("accent_color")]
    public int? AccentColor { get; init; }

    /// <summary>Gets the display name.</summary>
    [JsonPropertyName("global_name")]
    public required string? GlobalName { get; init; }

    /// <summary>Gets the primary guild, always <see langword="null"/>.</summary>
    [JsonPropertyName("primary_guild")]
    public UserPrimaryGuildResponse? PrimaryGuild { get; init; }
}

/// <summary>The Discord <c>UserPIIResponse</c> of <c>get_my_user</c> and <c>update_my_user</c>: a user plus account fields.</summary>
internal sealed class CurrentUserResponse : UserResponse
{
    /// <summary>Gets whether two-factor authentication is enabled.</summary>
    [JsonPropertyName("mfa_enabled")]
    public required bool MfaEnabled { get; init; }

    /// <summary>Gets the locale.</summary>
    [JsonPropertyName("locale")]
    public required string Locale { get; init; }

    /// <summary>Gets whether the account is verified.</summary>
    [JsonPropertyName("verified")]
    public bool Verified { get; init; }

    /// <summary>Gets the email, <see langword="null"/> for a bot.</summary>
    [JsonPropertyName("email")]
    public string? Email { get; init; }
}

/// <summary>The Discord <c>PrivateChannelResponse</c> (type 1) returned by <c>create_dm</c>.</summary>
internal sealed class PrivateChannelResponse
{
    /// <summary>Gets the channel ID.</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>Gets the channel type; <c>1</c> for a DM.</summary>
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

    /// <summary>Gets the recipients (the other party of the DM, never the bot itself).</summary>
    [JsonPropertyName("recipients")]
    public required List<UserResponse> Recipients { get; init; }
}
