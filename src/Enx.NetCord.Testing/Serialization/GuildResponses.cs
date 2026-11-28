using System.Text.Json;
using System.Text.Json.Serialization;

namespace Enx.NetCord.Testing.Serialization;

/// <summary>The Discord <c>GuildRoleColorsResponse</c>.</summary>
internal sealed class GuildRoleColorsResponse
{
    /// <summary>Gets the primary color.</summary>
    [JsonPropertyName("primary_color")]
    public required int PrimaryColor { get; init; }

    /// <summary>Gets the secondary color, <see langword="null"/> for none.</summary>
    [JsonPropertyName("secondary_color")]
    public int? SecondaryColor { get; init; }

    /// <summary>Gets the tertiary color, <see langword="null"/> for none.</summary>
    [JsonPropertyName("tertiary_color")]
    public int? TertiaryColor { get; init; }
}

/// <summary>The Discord <c>GuildRoleResponse</c>; <c>tags</c> (bot, integration, boost roles) are not modeled and stay absent.</summary>
internal sealed class GuildRoleResponse
{
    /// <summary>Gets the role ID, a snowflake string.</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>Gets the name.</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>Gets the permissions, a stringified bitfield.</summary>
    [JsonPropertyName("permissions")]
    public required string Permissions { get; init; }

    /// <summary>Gets the position.</summary>
    [JsonPropertyName("position")]
    public required int Position { get; init; }

    /// <summary>Gets the integer color; <c>0</c> for none.</summary>
    [JsonPropertyName("color")]
    public required int Color { get; init; }

    /// <summary>Gets the color set.</summary>
    [JsonPropertyName("colors")]
    public required GuildRoleColorsResponse Colors { get; init; }

    /// <summary>Gets whether the role is displayed separately.</summary>
    [JsonPropertyName("hoist")]
    public required bool Hoist { get; init; }

    /// <summary>Gets whether an integration manages the role.</summary>
    [JsonPropertyName("managed")]
    public required bool Managed { get; init; }

    /// <summary>Gets whether the role can be mentioned.</summary>
    [JsonPropertyName("mentionable")]
    public required bool Mentionable { get; init; }

    /// <summary>Gets the icon hash; always <see langword="null"/> (role icons are not modeled).</summary>
    [JsonPropertyName("icon")]
    public string? Icon { get; init; }

    /// <summary>Gets the Unicode emoji of the role; always <see langword="null"/>.</summary>
    [JsonPropertyName("unicode_emoji")]
    public string? UnicodeEmoji { get; init; }

    /// <summary>Gets the role flags.</summary>
    [JsonPropertyName("flags")]
    public required int Flags { get; init; }
}

/// <summary>An element of the <c>emojis</c> list of a guild; emojis are not modeled yet, so the list is always empty and this shape never appears.</summary>
internal sealed class GuildEmojiPlaceholder
{
    /// <summary>Gets the emoji ID.</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }
}

/// <summary>An element of the <c>stickers</c> list of a guild; stickers are not modeled yet, so the list is always empty and this shape never appears.</summary>
internal sealed class GuildStickerPlaceholder
{
    /// <summary>Gets the sticker ID.</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }
}

/// <summary>The value of <c>incidents_data</c> when a guild has incidents; none is modeled, so the property is always <see langword="null"/> and this shape never appears.</summary>
internal sealed class GuildIncidentsDataPlaceholder
{
    /// <summary>Gets when invites get enabled again.</summary>
    [JsonPropertyName("invites_disabled_until")]
    public string? InvitesDisabledUntil { get; init; }

    /// <summary>Gets when direct messages get enabled again.</summary>
    [JsonPropertyName("dms_disabled_until")]
    public string? DmsDisabledUntil { get; init; }
}

/// <summary>
/// The Discord <c>GuildResponse</c>, which is also <c>GuildWithCountsResponse</c> when the two approximate counts are set
/// (<c>GET /guilds/{guild_id}?with_counts=true</c>); without them the properties are absent (ADR 0006, rule 4).
/// </summary>
internal sealed class GuildResponse
{
    /// <summary>Gets the guild ID, a snowflake string.</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>Gets the name.</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>Gets the icon hash, or <see langword="null"/>.</summary>
    [JsonPropertyName("icon")]
    public string? Icon { get; init; }

    /// <summary>Gets the description, or <see langword="null"/>.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>Gets the home header hash, or <see langword="null"/>.</summary>
    [JsonPropertyName("home_header")]
    public string? HomeHeader { get; init; }

    /// <summary>Gets the invite splash hash, or <see langword="null"/>.</summary>
    [JsonPropertyName("splash")]
    public string? Splash { get; init; }

    /// <summary>Gets the discovery splash hash, or <see langword="null"/>.</summary>
    [JsonPropertyName("discovery_splash")]
    public string? DiscoverySplash { get; init; }

    /// <summary>Gets the enabled features; always empty (M4-REST-10).</summary>
    [JsonPropertyName("features")]
    public required List<string> Features { get; init; }

    /// <summary>Gets the banner hash, or <see langword="null"/>.</summary>
    [JsonPropertyName("banner")]
    public string? Banner { get; init; }

    /// <summary>Gets the owner ID, a snowflake string.</summary>
    [JsonPropertyName("owner_id")]
    public required string OwnerId { get; init; }

    /// <summary>Gets the ID of the application that created the guild, or <see langword="null"/>.</summary>
    [JsonPropertyName("application_id")]
    public string? ApplicationId { get; init; }

    /// <summary>Gets the deprecated voice region.</summary>
    [JsonPropertyName("region")]
    public required string Region { get; init; }

    /// <summary>Gets the AFK channel ID, or <see langword="null"/>.</summary>
    [JsonPropertyName("afk_channel_id")]
    public string? AfkChannelId { get; init; }

    /// <summary>Gets the AFK timeout in seconds.</summary>
    [JsonPropertyName("afk_timeout")]
    public required int AfkTimeout { get; init; }

    /// <summary>Gets the system channel ID, or <see langword="null"/>.</summary>
    [JsonPropertyName("system_channel_id")]
    public string? SystemChannelId { get; init; }

    /// <summary>Gets the system channel flags.</summary>
    [JsonPropertyName("system_channel_flags")]
    public required int SystemChannelFlags { get; init; }

    /// <summary>Gets whether the server widget is enabled.</summary>
    [JsonPropertyName("widget_enabled")]
    public required bool WidgetEnabled { get; init; }

    /// <summary>Gets the widget channel ID, or <see langword="null"/>.</summary>
    [JsonPropertyName("widget_channel_id")]
    public string? WidgetChannelId { get; init; }

    /// <summary>Gets the verification level.</summary>
    [JsonPropertyName("verification_level")]
    public required int VerificationLevel { get; init; }

    /// <summary>Gets the roles.</summary>
    [JsonPropertyName("roles")]
    public required List<GuildRoleResponse> Roles { get; init; }

    /// <summary>Gets the default message notification level.</summary>
    [JsonPropertyName("default_message_notifications")]
    public required int DefaultMessageNotifications { get; init; }

    /// <summary>Gets the required MFA level.</summary>
    [JsonPropertyName("mfa_level")]
    public required int MfaLevel { get; init; }

    /// <summary>Gets the explicit content filter level.</summary>
    [JsonPropertyName("explicit_content_filter")]
    public required int ExplicitContentFilter { get; init; }

    /// <summary>Gets the maximum number of presences, or <see langword="null"/>.</summary>
    [JsonPropertyName("max_presences")]
    public int? MaxPresences { get; init; }

    /// <summary>Gets the maximum number of members.</summary>
    [JsonPropertyName("max_members")]
    public required int MaxMembers { get; init; }

    /// <summary>Gets the maximum number of users in a stage video channel.</summary>
    [JsonPropertyName("max_stage_video_channel_users")]
    public required int MaxStageVideoChannelUsers { get; init; }

    /// <summary>Gets the maximum number of users in a video channel.</summary>
    [JsonPropertyName("max_video_channel_users")]
    public required int MaxVideoChannelUsers { get; init; }

    /// <summary>Gets the vanity URL code, or <see langword="null"/>.</summary>
    [JsonPropertyName("vanity_url_code")]
    public string? VanityUrlCode { get; init; }

    /// <summary>Gets the boost tier.</summary>
    [JsonPropertyName("premium_tier")]
    public required int PremiumTier { get; init; }

    /// <summary>Gets the number of boosts.</summary>
    [JsonPropertyName("premium_subscription_count")]
    public required int PremiumSubscriptionCount { get; init; }

    /// <summary>Gets the preferred locale.</summary>
    [JsonPropertyName("preferred_locale")]
    public required string PreferredLocale { get; init; }

    /// <summary>Gets the rules channel ID, or <see langword="null"/>.</summary>
    [JsonPropertyName("rules_channel_id")]
    public string? RulesChannelId { get; init; }

    /// <summary>Gets the safety alerts channel ID, or <see langword="null"/>.</summary>
    [JsonPropertyName("safety_alerts_channel_id")]
    public string? SafetyAlertsChannelId { get; init; }

    /// <summary>Gets the public updates channel ID, or <see langword="null"/>.</summary>
    [JsonPropertyName("public_updates_channel_id")]
    public string? PublicUpdatesChannelId { get; init; }

    /// <summary>Gets whether the boost progress bar is shown.</summary>
    [JsonPropertyName("premium_progress_bar_enabled")]
    public required bool PremiumProgressBarEnabled { get; init; }

    /// <summary>Gets whether the guild is age-restricted.</summary>
    [JsonPropertyName("nsfw")]
    public required bool Nsfw { get; init; }

    /// <summary>Gets the guild NSFW level.</summary>
    [JsonPropertyName("nsfw_level")]
    public required int NsfwLevel { get; init; }

    /// <summary>Gets the custom emojis; always empty.</summary>
    [JsonPropertyName("emojis")]
    public required List<GuildEmojiPlaceholder> Emojis { get; init; }

    /// <summary>Gets the custom stickers; always empty.</summary>
    [JsonPropertyName("stickers")]
    public required List<GuildStickerPlaceholder> Stickers { get; init; }

    /// <summary>Gets the incidents data; always <see langword="null"/> (no incident is modeled).</summary>
    [JsonPropertyName("incidents_data")]
    public GuildIncidentsDataPlaceholder? IncidentsData { get; init; }

    /// <summary>Gets the approximate member count; absent unless <c>with_counts</c> was requested.</summary>
    [JsonPropertyName("approximate_member_count")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ApproximateMemberCount { get; init; }

    /// <summary>Gets the approximate presence count; absent unless <c>with_counts</c> was requested.</summary>
    [JsonPropertyName("approximate_presence_count")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ApproximatePresenceCount { get; init; }
}

/// <summary>The Discord <c>GuildPreviewResponse</c>.</summary>
internal sealed class GuildPreviewResponse
{
    /// <summary>Gets the guild ID, a snowflake string.</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>Gets the name.</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>Gets the icon hash, or <see langword="null"/>.</summary>
    [JsonPropertyName("icon")]
    public string? Icon { get; init; }

    /// <summary>Gets the description, or <see langword="null"/>.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>Gets the home header hash, or <see langword="null"/>.</summary>
    [JsonPropertyName("home_header")]
    public string? HomeHeader { get; init; }

    /// <summary>Gets the invite splash hash, or <see langword="null"/>.</summary>
    [JsonPropertyName("splash")]
    public string? Splash { get; init; }

    /// <summary>Gets the discovery splash hash, or <see langword="null"/>.</summary>
    [JsonPropertyName("discovery_splash")]
    public string? DiscoverySplash { get; init; }

    /// <summary>Gets the enabled features; always empty (M4-REST-10).</summary>
    [JsonPropertyName("features")]
    public required List<string> Features { get; init; }

    /// <summary>Gets the approximate member count.</summary>
    [JsonPropertyName("approximate_member_count")]
    public required int ApproximateMemberCount { get; init; }

    /// <summary>Gets the approximate presence count.</summary>
    [JsonPropertyName("approximate_presence_count")]
    public required int ApproximatePresenceCount { get; init; }

    /// <summary>Gets the custom emojis; always empty.</summary>
    [JsonPropertyName("emojis")]
    public required List<GuildEmojiPlaceholder> Emojis { get; init; }

    /// <summary>Gets the custom stickers; always empty.</summary>
    [JsonPropertyName("stickers")]
    public required List<GuildStickerPlaceholder> Stickers { get; init; }
}

/// <summary>
/// One element of the list returned by <c>GET /guilds/{guild_id}/channels</c>: exactly one of the channel DTO variants, picked by the
/// family of the channel. The list mixes variants, so it is written by a closed converter (ADR 0006, rule 5), never through <see cref="object"/>.
/// </summary>
[JsonConverter(typeof(GuildChannelUnionConverter))]
internal sealed class GuildChannelUnion
{
    /// <summary>Gets the text or announcement channel, when this is one.</summary>
    public TextChannelResponse? Text { get; init; }

    /// <summary>Gets the voice or stage channel, when this is one.</summary>
    public VoiceChannelResponse? Voice { get; init; }

    /// <summary>Gets the category, when this is one.</summary>
    public CategoryChannelResponse? Category { get; init; }

    /// <summary>Gets the forum or media channel, when this is one.</summary>
    public ForumChannelResponse? Forum { get; init; }
}

/// <summary>Writes the variant a <see cref="GuildChannelUnion"/> holds with its generated metadata; the fake never reads it.</summary>
internal sealed class GuildChannelUnionConverter : JsonConverter<GuildChannelUnion>
{
    /// <inheritdoc />
    public override GuildChannelUnion Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => throw new NotSupportedException("The fake only writes channel lists.");

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, GuildChannelUnion value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);

        if (value.Text is { } text)
        {
            JsonSerializer.Serialize(writer, text, FakeDiscordJsonContext.Default.TextChannelResponse);
        }
        else if (value.Voice is { } voice)
        {
            JsonSerializer.Serialize(writer, voice, FakeDiscordJsonContext.Default.VoiceChannelResponse);
        }
        else if (value.Category is { } category)
        {
            JsonSerializer.Serialize(writer, category, FakeDiscordJsonContext.Default.CategoryChannelResponse);
        }
        else if (value.Forum is { } forum)
        {
            JsonSerializer.Serialize(writer, forum, FakeDiscordJsonContext.Default.ForumChannelResponse);
        }
        else
        {
            throw new InvalidOperationException("A guild channel union holds exactly one variant.");
        }
    }
}
