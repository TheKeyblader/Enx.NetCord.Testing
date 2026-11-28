using System.Globalization;
using System.Text.Json;
using Enx.NetCord.Testing.Rest.Routes.Channels;
using Enx.NetCord.Testing.Rest.Routes.Users;
using Enx.NetCord.Testing.State;

namespace Enx.NetCord.Testing.Rest.Routes.Guilds;

/// <summary>The validated request of <c>GET /guilds/{guild_id}</c>.</summary>
/// <param name="GuildId">The guild ID of the route.</param>
/// <param name="WithCounts">Whether the approximate member and presence counts are requested.</param>
internal sealed record GetGuildRequest(ulong GuildId, bool WithCounts);

/// <summary>The validated request of <c>PATCH /guilds/{guild_id}/channels</c>.</summary>
/// <param name="GuildId">The guild ID of the route.</param>
/// <param name="Placements">The entries, in order of the array.</param>
internal sealed record ChannelPositionsRequest(ulong GuildId, IReadOnlyList<ChannelPlacement> Placements);

/// <summary>
/// Structural validators of the guild routes (ADR 0005, step 3): no state is read. The generic parts live in
/// <see cref="FormValidation"/>; the channel fields shared with <c>PATCH /channels/{channel_id}</c> are read with the helpers of
/// <see cref="ChannelValidators"/>; only what is specific to guilds stays here.
/// </summary>
internal static class GuildValidators
{
    internal const int GuildNameMinLength = 2;
    internal const int GuildNameMaxLength = 100;
    internal const int DescriptionMaxLength = 300;
    internal const int MaxPermissionOverwrites = 100;
    internal const int MaxFeatures = 1521;

    private static readonly int[] VerificationLevels = [0, 1, 2, 3, 4];
    private static readonly int[] MessageNotificationLevels = [0, 1];
    private static readonly int[] ContentFilters = [0, 1, 2];
    private static readonly int[] AfkTimeouts = [60, 300, 900, 1800, 3600];

    // The AvailableLocalesEnum of the pinned spec.
    private static readonly string[] Locales =
    [
        "ar", "bg", "cs", "da", "de", "el", "en-GB", "en-US", "es-419", "es-ES", "fi", "fr", "he", "hi", "hr", "hu", "id", "it", "ja", "ko",
        "lt", "nl", "no", "pl", "pt-BR", "ro", "ru", "sv-SE", "th", "tr", "uk", "vi", "zh-CN", "zh-TW",
    ];

    /// <summary>Validates <c>GET /guilds/{guild_id}/preview</c> and <c>GET /guilds/{guild_id}/channels</c>: the route parameter must be a snowflake.</summary>
    /// <param name="request">The request.</param>
    /// <returns>The parsed guild ID as payload, or <c>50035</c>.</returns>
    public static RestValidation ValidateGuildId(FakeRestRequest request)
        => FormValidation.ValidateRouteSnowflake(request, "guild_id");

    /// <summary>
    /// Validates <c>GET /guilds/{guild_id}</c>: the guild ID and the optional <c>with_counts</c> query. NetCord writes the query as
    /// <c>True</c>/<c>False</c> (the .NET text of a <see cref="bool"/>), so the match ignores case; <c>1</c> and <c>0</c> are accepted too.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>A <see cref="GetGuildRequest"/> payload, or <c>50035</c>.</returns>
    public static RestValidation ValidateGetGuild(FakeRestRequest request)
    {
        var route = ValidateGuildId(request);
        if (route.IsInvalid)
        {
            return route;
        }

        var withCounts = false;
        if (request.GetQuery("with_counts") is { } raw)
        {
            if (raw.Equals("true", StringComparison.OrdinalIgnoreCase) || raw == "1")
            {
                withCounts = true;
            }
            else if (!raw.Equals("false", StringComparison.OrdinalIgnoreCase) && raw != "0")
            {
                return DiscordError.InvalidFormBody(new FormErrors().Add("with_counts", "BOOLEAN_TYPE_COERCE", $"Value \"{raw}\" is not boolean."));
            }
        }

        return RestValidation.Valid(new GetGuildRequest((ulong)route.Payload!, withCounts));
    }

    /// <summary>
    /// Validates <c>PATCH /guilds/{guild_id}</c> with the spec's <c>GuildPatchRequestPartial</c>: every field is optional. <c>region</c>
    /// and <c>features</c> are validated and ignored (<c>features</c> are not modeled: M4-REST-10, and the region is deprecated);
    /// <c>owner_id</c>, which NetCord can send but the spec does not list, is ignored like any unknown field. Whether a channel setting
    /// names a channel of the guild is decided by the state.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>An <see cref="UpdateGuildInput"/> payload, or <c>50109</c>/<c>50035</c>.</returns>
    public static RestValidation ValidateGuildPatch(FakeRestRequest request)
    {
        var route = ValidateGuildId(request);
        if (route.IsInvalid)
        {
            return route;
        }

        if (!FormValidation.TryReadJsonObject(request, out var document, out var error))
        {
            return error;
        }

        using (document)
        {
            var root = document.RootElement;
            var errors = new FormErrors();

            var name = FormValidation.ReadOptionalString(root, "name", errors);
            if (name is not null)
            {
                FormValidation.CheckLength(name, "name", GuildNameMinLength, GuildNameMaxLength, errors);
            }

            var description = FormValidation.ReadNullableString(root, "description", errors, out var changeDescription);
            if (description is not null)
            {
                FormValidation.CheckLength(description, "description", 0, DescriptionMaxLength, errors);
            }

            _ = FormValidation.ReadNullableString(root, "region", errors, out _);
            var icon = ReadImage(root, "icon", errors);
            var verification = ChannelValidators.ReadChoice(root, "verification_level", VerificationLevels, errors);
            var notifications = ChannelValidators.ReadChoice(root, "default_message_notifications", MessageNotificationLevels, errors);
            var contentFilter = ChannelValidators.ReadChoice(root, "explicit_content_filter", ContentFilters, errors);
            var locale = ReadLocale(root, errors);
            var afkTimeout = ChannelValidators.ReadChoice(root, "afk_timeout", AfkTimeouts, errors);
            var afkChannel = ReadChannelReference(root, "afk_channel_id", errors);
            var systemChannel = ReadChannelReference(root, "system_channel_id", errors);
            var splash = ReadImage(root, "splash", errors);
            var banner = ReadImage(root, "banner", errors);
            var systemFlags = FormValidation.ReadOptionalInteger(root, "system_channel_flags", 0, int.MaxValue, errors);
            ValidateFeatures(root, errors);
            var discoverySplash = ReadImage(root, "discovery_splash", errors);
            var homeHeader = ReadImage(root, "home_header", errors);
            var rulesChannel = ReadChannelReference(root, "rules_channel_id", errors);
            var safetyAlertsChannel = ReadChannelReference(root, "safety_alerts_channel_id", errors);
            var publicUpdatesChannel = ReadChannelReference(root, "public_updates_channel_id", errors);
            var progressBar = FormValidation.ReadOptionalBoolean(root, "premium_progress_bar_enabled", errors);

            return errors.IsEmpty
                ? RestValidation.Valid(new UpdateGuildInput(
                    (ulong)route.Payload!,
                    name,
                    changeDescription ? Change<string>.To(description) : default,
                    icon,
                    splash,
                    banner,
                    discoverySplash,
                    homeHeader,
                    verification,
                    notifications,
                    contentFilter,
                    locale,
                    afkTimeout,
                    afkChannel,
                    systemChannel,
                    systemFlags,
                    rulesChannel,
                    publicUpdatesChannel,
                    safetyAlertsChannel,
                    progressBar))
                : DiscordError.InvalidFormBody(errors);
        }
    }

    /// <summary>
    /// Validates <c>POST /guilds/{guild_id}/channels</c> with the spec's <c>CreateGuildChannelRequest</c>: <c>name</c> (1-100) is
    /// required, <c>type</c> defaults to <c>0</c>. The widest bounds of the spec are checked here (topic 4096, bitrate at least 8000,
    /// user limit at least 0); those of the channel type (topic 1024, voice ceilings), the parent invariants and the 500-channel limit
    /// belong to the state. A field the type has no use for is validated and ignored; <c>permission_overwrites</c> is checked for its
    /// type and size only (M4-REST-05) and <c>available_tags</c>, <c>default_reaction_emoji</c> and <c>default_tag_setting</c> are
    /// validated and ignored (M4-REST-01).
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>A <see cref="CreateGuildChannelInput"/> payload, or <c>50109</c>/<c>50035</c>.</returns>
    public static RestValidation ValidateCreateGuildChannel(FakeRestRequest request)
    {
        var route = ValidateGuildId(request);
        if (route.IsInvalid)
        {
            return route;
        }

        if (!FormValidation.TryReadJsonObject(request, out var document, out var error))
        {
            return error;
        }

        using (document)
        {
            var root = document.RootElement;
            var errors = new FormErrors();

            var type = FormValidation.ReadOptionalInteger(root, "type", int.MinValue, int.MaxValue, errors);
            if (type is { } typeValue && typeValue != ChannelValidators.MediaChannelType && !ChannelValidators.GuildChannelTypes.Contains(typeValue))
            {
                ChannelValidators.AddChoices(errors, "type", ChannelValidators.GuildChannelTypes);
                type = null;
            }

            var name = FormValidation.ReadOptionalString(root, "name", errors);
            if (name is null)
            {
                if (!root.TryGetProperty("name", out _))
                {
                    FormValidation.AddRequired(errors, "name");
                }
            }
            else
            {
                FormValidation.CheckLength(name, "name", 1, ChannelValidators.NameMaxLength, errors);
            }

            var position = FormValidation.ReadOptionalInteger(root, "position", 0, int.MaxValue, errors);
            var topic = FormValidation.ReadNullableString(root, "topic", errors, out _);
            if (topic is not null)
            {
                FormValidation.CheckLength(topic, "topic", 0, ChannelValidators.TopicMaxLength, errors);
            }

            var bitrate = FormValidation.ReadOptionalInteger(root, "bitrate", ChannelValidators.MinBitrate, int.MaxValue, errors);
            var userLimit = FormValidation.ReadOptionalInteger(root, "user_limit", 0, int.MaxValue, errors);
            var nsfw = FormValidation.ReadOptionalBoolean(root, "nsfw", errors);
            var rateLimit = FormValidation.ReadOptionalInteger(root, "rate_limit_per_user", 0, ChannelValidators.MaxRateLimitPerUser, errors);

            ulong? parentId = null;
            if (root.TryGetProperty("parent_id", out var parent)
                && parent.ValueKind != JsonValueKind.Null
                && FormValidation.TryReadSnowflake(parent, "parent_id", errors, out var parsedParent))
            {
                parentId = parsedParent;
            }

            if (FormValidation.TryReadArray(root, "permission_overwrites", errors, out var overwrites))
            {
                FormValidation.CheckMaxCount(overwrites.GetArrayLength(), "permission_overwrites", MaxPermissionOverwrites, errors);
            }

            var rtcRegion = FormValidation.ReadNullableString(root, "rtc_region", errors, out _);
            var videoQualityMode = ChannelValidators.ReadChoice(root, "video_quality_mode", ChannelValidators.VideoQualityModes, errors);
            var autoArchive = ChannelValidators.ReadChoice(root, "default_auto_archive_duration", ChannelValidators.AutoArchiveDurations, errors);
            ChannelValidators.ValidateDefaultReactionEmoji(root, errors);
            var threadRateLimit = FormValidation.ReadOptionalInteger(root, "default_thread_rate_limit_per_user", 0, ChannelValidators.MaxRateLimitPerUser, errors);
            var sortOrder = ChannelValidators.ReadChoice(root, "default_sort_order", ChannelValidators.SortOrders, errors);
            var forumLayout = ChannelValidators.ReadChoice(root, "default_forum_layout", ChannelValidators.ForumLayouts, errors);
            ChannelValidators.ValidateTagSetting(root, errors);
            ChannelValidators.ValidateAvailableTags(root, errors);

            return errors.IsEmpty
                ? RestValidation.Valid(new CreateGuildChannelInput(
                    (ulong)route.Payload!,
                    (ChannelKind)(type ?? (int)ChannelKind.GuildText),
                    name!,
                    ParentId: parentId,
                    Position: position,
                    Topic: topic,
                    Nsfw: nsfw ?? false,
                    RateLimitPerUser: rateLimit ?? 0,
                    Voice: new VoiceSettings(bitrate ?? ChannelTraits.DefaultBitrate, userLimit ?? 0, rtcRegion, videoQualityMode),
                    Forum: new ForumSettings(sortOrder, forumLayout ?? 0),
                    DefaultAutoArchiveDuration: autoArchive,
                    DefaultThreadRateLimitPerUser: threadRateLimit ?? 0))
                : DiscordError.InvalidFormBody(errors);
        }
    }

    /// <summary>
    /// Validates <c>PATCH /guilds/{guild_id}/channels</c>: a JSON array of <c>{id, position?, parent_id?, lock_permissions?}</c>. <c>id</c> is
    /// required (the spec marks nothing required, but an entry without a channel means nothing). <c>position</c> is at least 0 and
    /// <c>null</c> leaves it; <c>parent_id</c> distinguishes absent (unchanged), <c>null</c> (leave the category) and an ID;
    /// <c>lock_permissions</c> is validated and has no effect before M4-REST-05. Errors are keyed by array index (<c>0.id</c>).
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>A <see cref="ChannelPositionsRequest"/> payload, or <c>50109</c>/<c>50035</c>.</returns>
    public static RestValidation ValidateChannelPositions(FakeRestRequest request)
    {
        var route = ValidateGuildId(request);
        if (route.IsInvalid)
        {
            return route;
        }

        if (request.Body is null)
        {
            return DiscordError.InvalidJson();
        }

        if (!request.Body.TryParseJson(out var document, out var error))
        {
            return error;
        }

        using (document)
        {
            var errors = new FormErrors();
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                FormValidation.AddBadType(errors, string.Empty, "array");
                return DiscordError.InvalidFormBody(errors);
            }

            var placements = new List<ChannelPlacement>();
            var index = 0;
            foreach (var entry in document.RootElement.EnumerateArray())
            {
                var path = index.ToString(CultureInfo.InvariantCulture);
                index++;
                var scope = errors.Scope(path);
                if (entry.ValueKind != JsonValueKind.Object)
                {
                    scope.Add(string.Empty, "MODEL_TYPE_CONVERT", FormValidation.OnlyDictionariesMessage);
                    continue;
                }

                ulong channelId = 0;
                if (!entry.TryGetProperty("id", out var id) || id.ValueKind == JsonValueKind.Null)
                {
                    FormValidation.AddRequired(scope, "id");
                }
                else
                {
                    FormValidation.TryReadSnowflake(id, "id", scope, out channelId);
                }

                var position = FormValidation.ReadOptionalInteger(entry, "position", 0, int.MaxValue, scope);
                var parent = ReadChannelReference(entry, "parent_id", scope);
                _ = FormValidation.ReadOptionalBoolean(entry, "lock_permissions", scope);
                placements.Add(new ChannelPlacement(channelId, position, parent));
            }

            return errors.IsEmpty
                ? RestValidation.Valid(new ChannelPositionsRequest((ulong)route.Payload!, placements))
                : DiscordError.InvalidFormBody(errors);
        }
    }

    // A snowflake or null (clears the setting); absent leaves it. A value that is not a snowflake is NUMBER_TYPE_COERCE.
    private static Change<ulong?> ReadChannelReference(JsonElement root, string name, FormErrors errors)
    {
        if (!root.TryGetProperty(name, out var element))
        {
            return default;
        }

        if (element.ValueKind == JsonValueKind.Null)
        {
            return Change<ulong?>.To(null);
        }

        return FormValidation.TryReadSnowflake(element, name, errors, out var id) ? Change<ulong?>.To(id) : default;
    }

    // `preferred_locale`: one of the locales of the spec; null and absent leave the setting.
    private static string? ReadLocale(JsonElement root, FormErrors errors)
    {
        var locale = FormValidation.ReadNullableString(root, "preferred_locale", errors, out _);
        if (locale is not null && !Locales.Contains(locale, StringComparer.Ordinal))
        {
            errors.Add("preferred_locale", "BASE_TYPE_CHOICES", $"Value must be one of ({string.Join(", ", Locales.Select(l => $"'{l}'"))}).");
            return null;
        }

        return locale;
    }

    // `features`: an array of at most 1521 unique strings, validated and ignored (features are not modeled).
    private static void ValidateFeatures(JsonElement root, FormErrors errors)
    {
        if (!FormValidation.TryReadArray(root, "features", errors, out var features))
        {
            return;
        }

        if (!FormValidation.CheckMaxCount(features.GetArrayLength(), "features", MaxFeatures, errors))
        {
            return;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        var unique = true;
        foreach (var feature in features.EnumerateArray())
        {
            if (feature.ValueKind == JsonValueKind.String)
            {
                unique &= seen.Add(feature.GetString()!);
            }
            else if (feature.ValueKind != JsonValueKind.Null)
            {
                FormValidation.AddBadType(errors, $"features.{index.ToString(CultureInfo.InvariantCulture)}", "string");
            }

            index++;
        }

        if (!unique)
        {
            errors.Add("features", "BASE_TYPE_ARRAY_UNIQUE", "Array values must be unique.");
        }
    }

    // An image: absent leaves it, null removes it, a base64 data URI sets it, anything else is IMAGE_INVALID (like the user's avatar).
    private static Change<string> ReadImage(JsonElement root, string name, FormErrors errors)
    {
        if (!root.TryGetProperty(name, out var element))
        {
            return default;
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.Null:
                return Change<string>.To(null);

            case JsonValueKind.String:
                if (ImageData.TryGetHash(element.GetString()!, out var hash))
                {
                    return Change<string>.To(hash);
                }

                errors.Add(name, "IMAGE_INVALID", "Invalid image data");
                return default;

            default:
                FormValidation.AddBadType(errors, name, "string");
                return default;
        }
    }
}
