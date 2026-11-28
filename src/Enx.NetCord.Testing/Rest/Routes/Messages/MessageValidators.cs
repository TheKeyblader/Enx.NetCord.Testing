using System.Globalization;
using System.Text.Json;
using Enx.NetCord.Testing.State;

namespace Enx.NetCord.Testing.Rest.Routes.Messages;

/// <summary>The route parameters of a request on one message.</summary>
/// <param name="ChannelId">The channel ID.</param>
/// <param name="MessageId">The message ID.</param>
internal sealed record MessageRoute(ulong ChannelId, ulong MessageId);

/// <summary>The validated query of <c>GET /channels/{channel_id}/messages</c>; at most one cursor is set.</summary>
/// <param name="ChannelId">The channel ID.</param>
/// <param name="Limit">The number of messages, 1 to 100 (default 50).</param>
/// <param name="Before">The exclusive upper bound, or <see langword="null"/>.</param>
/// <param name="After">The exclusive lower bound, or <see langword="null"/>.</param>
/// <param name="Around">The center, or <see langword="null"/>.</param>
internal sealed record MessageListQuery(ulong ChannelId, int Limit, ulong? Before, ulong? After, ulong? Around);

/// <summary>The validated body of <c>POST /channels/{channel_id}/messages/bulk-delete</c>.</summary>
/// <param name="ChannelId">The channel ID.</param>
/// <param name="MessageIds">The 2 to 100 distinct IDs.</param>
internal sealed record BulkDeleteRequest(ulong ChannelId, IReadOnlyList<ulong> MessageIds);

/// <summary>
/// Structural validators of the message routes (ADR 0005, step 3): no state is read. The generic parts live in
/// <see cref="FormValidation"/> and <see cref="EmbedValidation"/>; only what is specific to messages stays here.
/// </summary>
internal static class MessageValidators
{
    internal const int DefaultLimit = 50;
    internal const int MaxLimit = 100;
    internal const int MaxContentLength = 2000;
    internal const int MaxAttachments = 10;
    internal const int MaxNonceLength = 25;
    internal const int MinBulkDelete = 2;
    internal const int MaxBulkDelete = 100;
    internal const int MaxAllowedMentionIds = 100;

    private const int MaxAttachmentText = 1024;

    // SUPPRESS_EMBEDS, SUPPRESS_NOTIFICATIONS, IS_VOICE_MESSAGE, IS_COMPONENTS_V2: the bits Discord lets a client set on a new message.
    private const int CreatableFlags = (1 << 2) | (1 << 12) | (1 << 13) | (1 << 15);

    private static readonly string[] ParseTypes = ["roles", "users", "everyone"];

    /// <summary>Validates the route of <c>GET /channels/{channel_id}/messages</c> and its query.</summary>
    /// <param name="request">The request.</param>
    /// <returns>A <see cref="MessageListQuery"/> payload, or <c>50035</c>.</returns>
    public static RestValidation ValidateList(FakeRestRequest request)
    {
        var channel = FormValidation.ValidateRouteSnowflake(request, "channel_id");
        if (channel.IsInvalid)
        {
            return channel;
        }

        var errors = new FormErrors();
        var limit = FormValidation.ReadQueryInteger(request, "limit", 1, MaxLimit, errors);
        var around = FormValidation.ReadQuerySnowflake(request, "around", errors);
        var before = FormValidation.ReadQuerySnowflake(request, "before", errors);
        var after = FormValidation.ReadQuerySnowflake(request, "after", errors);
        if (!errors.IsEmpty)
        {
            return DiscordError.InvalidFormBody(errors);
        }

        // The cursors are documented as mutually exclusive without saying what happens otherwise: the first of around, before, after
        // wins (hypothesis, docs/parity/rest.md).
        return RestValidation.Valid(new MessageListQuery(
            (ulong)channel.Payload!,
            limit ?? DefaultLimit,
            around is null ? before : null,
            around is null && before is null ? after : null,
            around));
    }

    /// <summary>Validates the <c>channel_id</c> and <c>message_id</c> of a route.</summary>
    /// <param name="request">The request.</param>
    /// <returns>A <see cref="MessageRoute"/> payload, or <c>50035</c>.</returns>
    public static RestValidation ValidateMessageRoute(FakeRestRequest request)
    {
        var errors = new FormErrors();
        var channelId = ReadRouteSnowflake(request, "channel_id", errors);
        var messageId = ReadRouteSnowflake(request, "message_id", errors);
        return errors.IsEmpty
            ? RestValidation.Valid(new MessageRoute(channelId, messageId))
            : DiscordError.InvalidFormBody(errors);
    }

    /// <summary>Validates <c>POST /channels/{channel_id}/messages</c>: a JSON body, or a multipart one with <c>payload_json</c> and <c>files[n]</c>.</summary>
    /// <param name="request">The request.</param>
    /// <returns>A <see cref="CreateMessageInput"/> payload with a zero author (the handler sets the caller), <c>50035</c>, <c>50109</c> or <c>50006</c>.</returns>
    public static RestValidation ValidateCreate(FakeRestRequest request)
    {
        var channel = FormValidation.ValidateRouteSnowflake(request, "channel_id");
        if (channel.IsInvalid)
        {
            return channel;
        }

        if (!TryReadBody(request, out var document, out var files, out var error))
        {
            return error;
        }

        using (document)
        {
            var root = document.RootElement;
            var errors = new FormErrors();

            var content = FormValidation.ReadNullableString(root, "content", errors, out _);
            if (content is not null)
            {
                FormValidation.CheckMaxLength(content, "content", MaxContentLength, errors);
            }

            var tts = FormValidation.ReadOptionalBoolean(root, "tts", errors) ?? false;
            var flags = FormValidation.ReadOptionalInteger(root, "flags", int.MinValue, int.MaxValue, errors) ?? 0;
            var (nonce, enforceNonce) = ReadNonce(root, errors);
            var embeds = EmbedValidation.ReadEmbeds(root, errors, out _);
            var allowedMentions = ReadAllowedMentions(root, errors);
            var reference = ReadReference(root, errors);
            var attachments = ReadNewAttachments(root, files, errors, out _);
            var stickerIds = FormValidation.ReadSnowflakeArray(root, "sticker_ids", 0, 3, false, errors);
            var hasOtherParts = stickerIds is { Count: > 0 } || HasValue(root, "components") || HasValue(root, "poll");

            if (!errors.IsEmpty)
            {
                return DiscordError.InvalidFormBody(errors);
            }

            // Stickers, components, polls and shared themes are accepted but not modeled; they only keep a message from being empty.
            var isEmpty = string.IsNullOrEmpty(content) && embeds.Count == 0 && attachments.Count == 0 && !hasOtherParts;
            if (isEmpty)
            {
                return DiscordError.EmptyMessage();
            }

            return RestValidation.Valid(new CreateMessageInput(
                (ulong)channel.Payload!,
                0,
                content ?? string.Empty,
                Tts: tts,
                Flags: flags & CreatableFlags,
                Embeds: embeds,
                Attachments: attachments,
                AllowedMentions: allowedMentions,
                Reference: reference,
                Nonce: nonce,
                EnforceNonce: enforceNonce));
        }
    }

    /// <summary>
    /// Validates <c>PATCH /channels/{channel_id}/messages/{message_id}</c> (<c>MessageEditRequestPartial</c>: every field is optional and nullable).
    /// A <c>null</c> content or embed list clears it. When <c>attachments</c> is sent it lists what to keep: an ID that is not the index of an
    /// uploaded file is an existing attachment to retain, and uploaded files are appended.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>An <see cref="EditMessageInput"/> payload, <c>50035</c> or <c>50109</c>.</returns>
    public static RestValidation ValidateEdit(FakeRestRequest request)
    {
        var route = ValidateMessageRoute(request);
        if (route.IsInvalid)
        {
            return route;
        }

        if (!TryReadBody(request, out var document, out var files, out var error))
        {
            return error;
        }

        using (document)
        {
            var root = document.RootElement;
            var errors = new FormErrors();

            var content = FormValidation.ReadNullableString(root, "content", errors, out var changeContent);
            if (content is not null)
            {
                FormValidation.CheckMaxLength(content, "content", MaxContentLength, errors);
            }

            var embeds = EmbedValidation.ReadEmbeds(root, errors, out var embedsPresent);
            var flags = FormValidation.ReadOptionalInteger(root, "flags", int.MinValue, int.MaxValue, errors);
            var allowedMentions = ReadAllowedMentions(root, errors);
            var newAttachments = ReadNewAttachments(root, files, errors, out var retained);
            _ = FormValidation.ReadSnowflakeArray(root, "sticker_ids", 0, 1521, false, errors);

            if (!errors.IsEmpty)
            {
                return DiscordError.InvalidFormBody(errors);
            }

            var target = (MessageRoute)route.Payload!;
            return RestValidation.Valid(new EditMessageInput(
                target.ChannelId,
                target.MessageId,
                ChangeContent: changeContent,
                Content: content,
                Embeds: embedsPresent ? embeds : null,
                Flags: flags,
                AllowedMentions: allowedMentions,
                RetainedAttachmentIds: retained,
                NewAttachments: newAttachments));
        }
    }

    /// <summary>
    /// Validates <c>POST /channels/{channel_id}/messages/bulk-delete</c>: <c>messages</c> is required and holds 2 to 100 distinct snowflakes.
    /// Message IDs that do not exist still count, as Discord documents.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>A <see cref="BulkDeleteRequest"/> payload, <c>50035</c> or <c>50109</c>.</returns>
    public static RestValidation ValidateBulkDelete(FakeRestRequest request)
    {
        var channel = FormValidation.ValidateRouteSnowflake(request, "channel_id");
        if (channel.IsInvalid)
        {
            return channel;
        }

        if (!FormValidation.TryReadJsonObject(request, out var document, out var error))
        {
            return error;
        }

        using (document)
        {
            var errors = new FormErrors();
            var ids = FormValidation.ReadSnowflakeArray(document.RootElement, "messages", MinBulkDelete, MaxBulkDelete, true, errors);
            if (ids is null && errors.IsEmpty)
            {
                // Absent or null: the field is required.
                FormValidation.AddRequired(errors, "messages");
            }

            return errors.IsEmpty && ids is not null
                ? RestValidation.Valid(new BulkDeleteRequest((ulong)channel.Payload!, ids))
                : DiscordError.InvalidFormBody(errors);
        }
    }

    private static ulong ReadRouteSnowflake(FakeRestRequest request, string name, FormErrors errors)
    {
        var raw = request.RouteParameters[name];
        if (FormValidation.TryParseSnowflake(raw, out var id))
        {
            return id;
        }

        errors.Add(name, "NUMBER_TYPE_COERCE", FormValidation.NotSnowflakeMessage(raw));
        return 0;
    }

    private static bool HasValue(JsonElement root, string name)
        => root.TryGetProperty(name, out var element)
            && (element.ValueKind == JsonValueKind.Array ? element.GetArrayLength() > 0 : element.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined));

    /// <summary>
    /// Reads the content of a message request: the JSON body itself, or the <c>payload_json</c> part of a multipart body (an
    /// absent part is an empty object) and its <c>files[n]</c> parts.
    /// </summary>
    private static bool TryReadBody(
        FakeRestRequest request,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out JsonDocument? document,
        out IReadOnlyDictionary<int, MultipartPart> files,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(false)] out DiscordError? error)
    {
        files = new Dictionary<int, MultipartPart>();
        if (request.Body is null || !RestMultipart.IsMultipart(request.Body))
        {
            return FormValidation.TryReadJsonObject(request, out document, out error);
        }

        if (!RestMultipart.TryParse(request.Body, out var parts))
        {
            document = null;
            error = DiscordError.InvalidJson();
            return false;
        }

        var uploaded = new Dictionary<int, MultipartPart>();
        MultipartPart? payload = null;
        foreach (var part in parts)
        {
            if (part.Name == "payload_json")
            {
                payload = part;
            }
            else if (TryReadFileIndex(part.Name, out var index))
            {
                uploaded[index] = part;
            }
        }

        files = uploaded;
        if (payload is null)
        {
            document = JsonDocument.Parse("{}");
            error = null;
            return true;
        }

        return FormValidation.TryReadJsonObject(new RestRequestBody(payload.Content, "application/json"), out document, out error);
    }

    private static bool TryReadFileIndex(string name, out int index)
    {
        index = 0;
        return name.StartsWith("files[", StringComparison.Ordinal)
            && name.EndsWith(']')
            && int.TryParse(name.AsSpan(6, name.Length - 7), NumberStyles.None, CultureInfo.InvariantCulture, out index);
    }

    // Reads `attachments` (metadata of the uploaded files, and on edit the ones to retain) and pairs it with the uploaded files.
    private static IReadOnlyList<NewAttachment> ReadNewAttachments(
        JsonElement root,
        IReadOnlyDictionary<int, MultipartPart> files,
        FormErrors errors,
        out IReadOnlyList<ulong>? retained)
    {
        retained = null;
        var metadata = new Dictionary<int, (string? FileName, string? Title, string? Description)>();
        if (FormValidation.TryReadArray(root, "attachments", errors, out var array))
        {
            if (!FormValidation.CheckMaxCount(array.GetArrayLength(), "attachments", MaxAttachments, errors))
            {
                return [];
            }

            var keep = new List<ulong>();
            var index = 0;
            foreach (var item in array.EnumerateArray())
            {
                var path = $"attachments.{index.ToString(CultureInfo.InvariantCulture)}";
                index++;
                if (item.ValueKind != JsonValueKind.Object)
                {
                    errors.Add(path, "MODEL_TYPE_CONVERT", FormValidation.OnlyDictionariesMessage);
                    continue;
                }

                var scope = errors.Scope(path);
                if (!item.TryGetProperty("id", out var idElement))
                {
                    FormValidation.AddRequired(scope, "id");
                    continue;
                }

                if (!FormValidation.TryReadSnowflake(idElement, "id", scope, out var id))
                {
                    continue;
                }

                var fileName = FormValidation.ReadNullableString(item, "filename", scope, out _);
                if (fileName is not null)
                {
                    FormValidation.CheckLength(fileName, "filename", 1, MaxAttachmentText, scope);
                }

                var title = FormValidation.ReadNullableString(item, "title", scope, out _);
                var description = FormValidation.ReadNullableString(item, "description", scope, out _);
                if (title is not null)
                {
                    FormValidation.CheckMaxLength(title, "title", MaxAttachmentText, scope);
                }

                if (description is not null)
                {
                    FormValidation.CheckMaxLength(description, "description", MaxAttachmentText, scope);
                }

                if (id <= int.MaxValue && files.ContainsKey((int)id))
                {
                    metadata[(int)id] = (fileName, title, description);
                }
                else
                {
                    keep.Add(id);
                }
            }

            retained = keep;
        }

        if (files.Count > MaxAttachments)
        {
            FormValidation.CheckMaxCount(files.Count, "attachments", MaxAttachments, errors);
            return [];
        }

        var result = new List<NewAttachment>();
        foreach (var (index, part) in files.OrderBy(pair => pair.Key))
        {
            metadata.TryGetValue(index, out var meta);
            result.Add(new NewAttachment(
                meta.FileName ?? part.FileName ?? "file",
                part.Content.Length,
                part.ContentType,
                meta.Title,
                meta.Description));
        }

        return result;
    }

    private static (string? Nonce, bool Enforce) ReadNonce(JsonElement root, FormErrors errors)
    {
        var enforce = FormValidation.ReadOptionalBoolean(root, "enforce_nonce", errors) ?? false;
        if (!root.TryGetProperty("nonce", out var element) || element.ValueKind == JsonValueKind.Null)
        {
            return (null, enforce);
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                var text = element.GetString()!;
                FormValidation.CheckMaxLength(text, "nonce", MaxNonceLength, errors);
                return (text, enforce);

            case JsonValueKind.Number when element.TryGetInt64(out var number):
                return (number.ToString(CultureInfo.InvariantCulture), enforce);

            default:
                errors.Add("nonce", "NUMBER_TYPE_COERCE", $"Value \"{element.GetRawText()}\" is not int64.");
                return (null, enforce);
        }
    }

    private static MessageReferenceInput? ReadReference(JsonElement root, FormErrors errors)
    {
        if (!FormValidation.TryReadObject(root, "message_reference", errors, out var reference))
        {
            return null;
        }

        var scope = errors.Scope("message_reference");
        ulong messageId = 0;
        if (!reference.TryGetProperty("message_id", out var messageElement) || messageElement.ValueKind == JsonValueKind.Null)
        {
            FormValidation.AddRequired(scope, "message_id");
        }
        else
        {
            FormValidation.TryReadSnowflake(messageElement, "message_id", scope, out messageId);
        }

        ulong? channelId = null;
        if (reference.TryGetProperty("channel_id", out var channelElement) && channelElement.ValueKind != JsonValueKind.Null
            && FormValidation.TryReadSnowflake(channelElement, "channel_id", scope, out var parsedChannel))
        {
            channelId = parsedChannel;
        }

        if (reference.TryGetProperty("guild_id", out var guildElement) && guildElement.ValueKind != JsonValueKind.Null)
        {
            FormValidation.TryReadSnowflake(guildElement, "guild_id", scope, out _);
        }

        var failIfNotExists = FormValidation.ReadOptionalBoolean(reference, "fail_if_not_exists", scope) ?? true;

        // Only the default reference (a reply) is modeled; a forward needs message snapshots.
        var type = FormValidation.ReadOptionalInteger(reference, "type", int.MinValue, int.MaxValue, scope);
        if (type is { } value and not 0)
        {
            scope.Add("type", "BASE_TYPE_CHOICES", "Value must be one of (0,).");
        }

        return scope.IsEmpty ? new MessageReferenceInput(messageId, channelId, failIfNotExists) : null;
    }

    private static AllowedMentionsValue? ReadAllowedMentions(JsonElement root, FormErrors errors)
    {
        if (!FormValidation.TryReadObject(root, "allowed_mentions", errors, out var allowed))
        {
            return null;
        }

        var scope = errors.Scope("allowed_mentions");
        bool parseUsers = false, parseRoles = false, parseEveryone = false;
        if (FormValidation.TryReadArray(allowed, "parse", scope, out var parse))
        {
            var index = 0;
            foreach (var item in parse.EnumerateArray())
            {
                var kind = item.ValueKind == JsonValueKind.String ? item.GetString() : null;
                switch (kind)
                {
                    case "users":
                        parseUsers = true;
                        break;
                    case "roles":
                        parseRoles = true;
                        break;
                    case "everyone":
                        parseEveryone = true;
                        break;
                    default:
                        scope.Add(
                            $"parse.{index.ToString(CultureInfo.InvariantCulture)}",
                            "BASE_TYPE_CHOICES",
                            $"Value must be one of ({string.Join(", ", ParseTypes.Select(t => $"'{t}'"))}).");
                        break;
                }

                index++;
            }
        }

        var users = FormValidation.ReadSnowflakeArray(allowed, "users", 0, MaxAllowedMentionIds, true, scope) ?? [];
        var roles = FormValidation.ReadSnowflakeArray(allowed, "roles", 0, MaxAllowedMentionIds, true, scope) ?? [];
        var repliedUser = FormValidation.ReadOptionalBoolean(allowed, "replied_user", scope) ?? false;

        // Hypothesis (docs/parity/errors.md): `parse` is exclusive with the explicit list of the same kind.
        if (parseUsers && users.Count > 0)
        {
            scope.Add("users", "MESSAGE_ALLOWED_MENTIONS_PARSE_EXCLUSIVE", "Parse users and allowed users cannot both be specified.");
        }

        if (parseRoles && roles.Count > 0)
        {
            scope.Add("roles", "MESSAGE_ALLOWED_MENTIONS_PARSE_EXCLUSIVE", "Parse roles and allowed roles cannot both be specified.");
        }

        return new AllowedMentionsValue(parseUsers, parseRoles, parseEveryone, [.. users], [.. roles], repliedUser);
    }
}
