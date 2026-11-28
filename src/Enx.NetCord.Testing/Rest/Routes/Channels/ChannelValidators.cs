using System.Globalization;
using System.Text.Json;
using Enx.NetCord.Testing.State;

namespace Enx.NetCord.Testing.Rest.Routes.Channels;

/// <summary>The validated body of <c>PATCH /channels/{channel_id}</c>: which fields were sent, and their new values.</summary>
/// <param name="ChannelId">The channel ID of the route.</param>
/// <param name="Name">The new name, or <see langword="null"/> when not sent.</param>
/// <param name="ChangeTopic">Whether <c>topic</c> was sent (<see langword="null"/> clears it).</param>
/// <param name="Topic">The new topic.</param>
/// <param name="Nsfw">The new age restriction, or <see langword="null"/> when not sent.</param>
/// <param name="RateLimitPerUser">The new slowmode, or <see langword="null"/> when not sent.</param>
/// <param name="Position">The new position, or <see langword="null"/> when not sent.</param>
/// <param name="Type">The requested channel type, or <see langword="null"/> when not sent.</param>
/// <param name="ChangeParent">Whether <c>parent_id</c> was sent (<see langword="null"/> leaves the category).</param>
/// <param name="ParentId">The requested parent category when <paramref name="ChangeParent"/> is set; <see langword="null"/> leaves the category.</param>
/// <param name="Bitrate">The new bitrate (at least 8000), or <see langword="null"/> when not sent.</param>
/// <param name="UserLimit">The new user limit, or <see langword="null"/> when not sent.</param>
/// <param name="ChangeRtcRegion">Whether <c>rtc_region</c> was sent (<see langword="null"/> is automatic).</param>
/// <param name="RtcRegion">The new voice region.</param>
/// <param name="VideoQualityMode">The new video quality mode (1 or 2), or <see langword="null"/> when not sent.</param>
/// <param name="DefaultAutoArchiveDuration">The new default auto-archive duration, or <see langword="null"/> when not sent.</param>
/// <param name="DefaultThreadRateLimitPerUser">The new default thread slowmode, or <see langword="null"/> when not sent.</param>
/// <param name="ChangeDefaultSortOrder">Whether <c>default_sort_order</c> was sent (<see langword="null"/> clears it).</param>
/// <param name="DefaultSortOrder">The new default sort order.</param>
/// <param name="DefaultForumLayout">The new default forum layout, or <see langword="null"/> when not sent.</param>
internal sealed record ChannelPatch(
    ulong ChannelId,
    string? Name,
    bool ChangeTopic,
    string? Topic,
    bool? Nsfw,
    int? RateLimitPerUser,
    int? Position,
    int? Type,
    bool ChangeParent,
    ulong? ParentId,
    int? Bitrate,
    int? UserLimit,
    bool ChangeRtcRegion,
    string? RtcRegion,
    int? VideoQualityMode,
    int? DefaultAutoArchiveDuration,
    int? DefaultThreadRateLimitPerUser,
    bool ChangeDefaultSortOrder,
    int? DefaultSortOrder,
    int? DefaultForumLayout);

/// <summary>
/// Structural validators of the channel routes (ADR 0005, step 3): no state is read. The generic parts live in
/// <see cref="FormValidation"/>; only what is specific to channels stays here.
/// </summary>
internal static class ChannelValidators
{
    internal const int NameMaxLength = 100;

    /// <summary>The largest topic of any channel type (forum and media); the bound of the type is checked by the state.</summary>
    internal const int TopicMaxLength = 4096;
    internal const int MaxRateLimitPerUser = 21600;
    internal const int MinBitrate = 8000;
    internal const int MaxAvailableTags = 20;
    internal const int TagNameMaxLength = 50;
    internal const int EmojiNameMaxLength = 100;

    // The spec's `ChannelTypes` accepted for a guild channel update (UpdateGuildChannelRequestPartial.type).
    // The message of BASE_TYPE_CHOICES lists this enumeration as the spec gives it.
    private static readonly int[] GuildChannelTypes = [0, 2, 4, 5, 13, 14, 15];

    // Type 16 (media) is described by the docs and typed by NetCord but absent from the spec's enumeration: accepted too (ADR 0013, § 1).
    private const int MediaChannelType = 16;

    private static readonly int[] VideoQualityModes = [1, 2];
    private static readonly int[] AutoArchiveDurations = [60, 1440, 4320, 10080];
    private static readonly int[] SortOrders = [0, 1];
    private static readonly int[] ForumLayouts = [0, 1, 2];
    private static readonly string[] TagSettings = ["match_all", "match_some"];

    /// <summary>Validates <c>GET</c> and <c>DELETE /channels/{channel_id}</c>: the route parameter must be a snowflake.</summary>
    /// <param name="request">The request.</param>
    /// <returns>The parsed channel ID as payload, or <c>50035</c>.</returns>
    public static RestValidation ValidateChannelId(FakeRestRequest request)
        => FormValidation.ValidateRouteSnowflake(request, "channel_id");

    /// <summary>
    /// Validates <c>PATCH /channels/{channel_id}</c> structurally, with the guild-channel schema of the spec (<c>UpdateGuildChannelRequestPartial</c>)
    /// except for <c>name</c> (see below); every field is optional. The bounds that depend on the type of the channel (a topic of
    /// 1024 outside forum and media, a bitrate or user limit above the type's maximum) are not known here and are checked by the
    /// state. Fields that do not apply to the channel's type are validated like any other and ignored by the state;
    /// <c>available_tags</c>, <c>default_reaction_emoji</c>, <c>default_tag_setting</c> and <c>flags</c> are validated and ignored for every
    /// type (ADR 0013, § 1); <c>permission_overwrites</c> is neither validated nor applied yet (M4-REST-05). The other fields of a DM, which has only
    /// <c>name</c> (<c>UpdateDMRequestPartial</c>), are validated like a guild channel's: a known gap, since the kind is not known here.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>A <see cref="ChannelPatch"/> payload, or <c>50035</c>/<c>50109</c>.</returns>
    public static RestValidation ValidateChannelPatch(FakeRestRequest request)
    {
        var routeValidation = ValidateChannelId(request);
        if (routeValidation.IsInvalid)
        {
            return routeValidation;
        }

        if (!FormValidation.TryReadJsonObject(request, out var document, out var error))
        {
            return error;
        }

        using (document)
        {
            var root = document.RootElement;
            var errors = new FormErrors();

            // The schema depends on the kind of channel, which only the state knows: a DM accepts an empty or null name
            // (UpdateDMRequestPartial, 0-100), a guild channel needs 1-100. Here: a string of at most 100 (null = not sent,
            // the message is the guild one); an empty name on a guild channel is refused by the state operation.
            var name = FormValidation.ReadNullableString(root, "name", errors, out _);
            if (!string.IsNullOrEmpty(name))
            {
                FormValidation.CheckLength(name, "name", 1, NameMaxLength, errors);
            }

            var topic = FormValidation.ReadNullableString(root, "topic", errors, out var changeTopic);
            if (topic is not null)
            {
                FormValidation.CheckLength(topic, "topic", 0, TopicMaxLength, errors);
            }

            var nsfw = FormValidation.ReadOptionalBoolean(root, "nsfw", errors);
            var rateLimit = FormValidation.ReadOptionalInteger(root, "rate_limit_per_user", 0, MaxRateLimitPerUser, errors);
            var position = FormValidation.ReadOptionalInteger(root, "position", 0, int.MaxValue, errors);

            var type = FormValidation.ReadOptionalInteger(root, "type", int.MinValue, int.MaxValue, errors);
            if (type is { } typeValue && typeValue != MediaChannelType && !GuildChannelTypes.Contains(typeValue))
            {
                AddChoices(errors, "type", GuildChannelTypes);
                type = null;
            }

            // Three states: absent = unchanged, null = leave the category, an ID = move there.
            ulong? parentId = null;
            var changeParent = false;
            if (root.TryGetProperty("parent_id", out var parent))
            {
                if (parent.ValueKind == JsonValueKind.Null)
                {
                    changeParent = true;
                }
                else if (FormValidation.TryReadSnowflake(parent, "parent_id", errors, out var parsedParent))
                {
                    changeParent = true;
                    parentId = parsedParent;
                }
            }

            var bitrate = FormValidation.ReadOptionalInteger(root, "bitrate", MinBitrate, int.MaxValue, errors);
            var userLimit = FormValidation.ReadOptionalInteger(root, "user_limit", 0, int.MaxValue, errors);
            var rtcRegion = FormValidation.ReadNullableString(root, "rtc_region", errors, out var changeRtcRegion);
            var videoQualityMode = ReadChoice(root, "video_quality_mode", VideoQualityModes, errors);
            var autoArchive = ReadChoice(root, "default_auto_archive_duration", AutoArchiveDurations, errors);
            var threadRateLimit = FormValidation.ReadOptionalInteger(root, "default_thread_rate_limit_per_user", 0, MaxRateLimitPerUser, errors);
            var sortOrder = ReadChoice(root, "default_sort_order", SortOrders, errors);
            var changeSortOrder = sortOrder is not null || (root.TryGetProperty("default_sort_order", out var sortElement) && sortElement.ValueKind == JsonValueKind.Null);
            var forumLayout = ReadChoice(root, "default_forum_layout", ForumLayouts, errors);

            // Validated, then ignored for every type (ADR 0013, § 1): posts, tags and flags wait for M4-REST-01.
            _ = FormValidation.ReadOptionalInteger(root, "flags", int.MinValue, int.MaxValue, errors);
            ValidateTagSetting(root, errors);
            ValidateDefaultReactionEmoji(root, errors);
            ValidateAvailableTags(root, errors);

            return errors.IsEmpty
                ? RestValidation.Valid(new ChannelPatch(
                    (ulong)routeValidation.Payload!,
                    name,
                    changeTopic,
                    topic,
                    nsfw,
                    rateLimit,
                    position,
                    type,
                    changeParent,
                    parentId,
                    bitrate,
                    userLimit,
                    changeRtcRegion,
                    rtcRegion,
                    videoQualityMode,
                    autoArchive,
                    threadRateLimit,
                    changeSortOrder,
                    sortOrder,
                    forumLayout))
                : DiscordError.InvalidFormBody(errors);
        }
    }

    // An integer enumeration of the spec: a value outside it is BASE_TYPE_CHOICES; absent and null give null.
    private static int? ReadChoice(JsonElement root, string name, int[] choices, FormErrors errors)
    {
        var value = FormValidation.ReadOptionalInteger(root, name, int.MinValue, int.MaxValue, errors);
        if (value is { } chosen && !choices.Contains(chosen))
        {
            AddChoices(errors, name, choices);
            return null;
        }

        return value;
    }

    private static void AddChoices(FormErrors errors, string path, int[] choices)
        => errors.Add(
            path,
            "BASE_TYPE_CHOICES",
            $"Value must be one of ({string.Join(", ", choices.Select(choice => choice.ToString(CultureInfo.InvariantCulture)))}).");

    private static void ValidateTagSetting(JsonElement root, FormErrors errors)
    {
        var setting = FormValidation.ReadNullableString(root, "default_tag_setting", errors, out _);
        if (setting is not null && !TagSettings.Contains(setting, StringComparer.Ordinal))
        {
            errors.Add("default_tag_setting", "BASE_TYPE_CHOICES", $"Value must be one of ({string.Join(", ", TagSettings.Select(s => $"'{s}'"))}).");
        }
    }

    private static void ValidateDefaultReactionEmoji(JsonElement root, FormErrors errors)
    {
        if (FormValidation.TryReadObject(root, "default_reaction_emoji", errors, out var emoji))
        {
            ValidateEmoji(emoji, errors.Scope("default_reaction_emoji"));
        }
    }

    private static void ValidateAvailableTags(JsonElement root, FormErrors errors)
    {
        if (!FormValidation.TryReadArray(root, "available_tags", errors, out var tags))
        {
            return;
        }

        if (!FormValidation.CheckMaxCount(tags.GetArrayLength(), "available_tags", MaxAvailableTags, errors))
        {
            return;
        }

        var index = 0;
        foreach (var tag in tags.EnumerateArray())
        {
            var scope = errors.Scope($"available_tags.{index.ToString(CultureInfo.InvariantCulture)}");
            index++;
            if (tag.ValueKind != JsonValueKind.Object)
            {
                scope.Add(string.Empty, "MODEL_TYPE_CONVERT", FormValidation.OnlyDictionariesMessage);
                continue;
            }

            var tagName = FormValidation.ReadOptionalString(tag, "name", scope);
            if (tagName is null)
            {
                if (!tag.TryGetProperty("name", out _))
                {
                    FormValidation.AddRequired(scope, "name");
                }
            }
            else
            {
                FormValidation.CheckLength(tagName, "name", 0, TagNameMaxLength, scope);
            }

            _ = FormValidation.ReadOptionalBoolean(tag, "moderated", scope);
            ValidateOptionalSnowflake(tag, "id", scope);
            ValidateEmoji(tag, scope);
        }
    }

    // `emoji_id` (snowflake or null) and `emoji_name` (string of at most 100 or null), shared by tags and the default reaction.
    private static void ValidateEmoji(JsonElement emoji, FormErrors errors)
    {
        ValidateOptionalSnowflake(emoji, "emoji_id", errors);
        var emojiName = FormValidation.ReadNullableString(emoji, "emoji_name", errors, out _);
        if (emojiName is not null)
        {
            FormValidation.CheckMaxLength(emojiName, "emoji_name", EmojiNameMaxLength, errors);
        }
    }

    private static void ValidateOptionalSnowflake(JsonElement root, string name, FormErrors errors)
    {
        if (root.TryGetProperty(name, out var element) && element.ValueKind != JsonValueKind.Null)
        {
            FormValidation.TryReadSnowflake(element, name, errors, out _);
        }
    }
}
