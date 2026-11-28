using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using Enx.NetCord.Testing.State;

namespace Enx.NetCord.Testing.Rest.Routes.Messages;

/// <summary>
/// Structural validation of the <c>embeds</c> of a message (<c>RichEmbed</c> in the spec): types, lengths, the 10-embed and 25-field limits,
/// and the 6000-character budget of the whole message. Only what Discord lets a client set is read: <c>type</c>, <c>provider</c> and <c>video</c> are ignored.
/// </summary>
internal static class EmbedValidation
{
    internal const int MaxEmbeds = 10;
    internal const int MaxFields = 25;
    internal const int MaxTotalCharacters = 6000;
    internal const int MaxColor = 0xFFFFFF;

    private const int MaxTitle = 256;
    private const int MaxDescription = 4096;
    private const int MaxUrl = 2048;
    private const int MaxFooterText = 2048;
    private const int MaxAuthorName = 256;
    private const int MaxFieldName = 256;
    private const int MaxFieldValue = 1024;

    /// <summary>Reads the <c>embeds</c> property.</summary>
    /// <param name="root">The message object.</param>
    /// <param name="errors">The collector.</param>
    /// <param name="present">Whether the property was sent, <c>null</c> included.</param>
    /// <returns>The embeds, empty when absent or <c>null</c>.</returns>
    public static IReadOnlyList<EmbedValue> ReadEmbeds(JsonElement root, FormErrors errors, out bool present)
    {
        present = root.TryGetProperty("embeds", out var property);
        if (!present)
        {
            return [];
        }

        if (property.ValueKind == JsonValueKind.Null)
        {
            return [];
        }

        if (!FormValidation.TryReadArray(root, "embeds", errors, out var array))
        {
            present = false;
            return [];
        }

        if (!FormValidation.CheckMaxCount(array.GetArrayLength(), "embeds", MaxEmbeds, errors))
        {
            return [];
        }

        var embeds = new List<EmbedValue>();
        var totalCharacters = 0;
        var index = 0;
        foreach (var item in array.EnumerateArray())
        {
            var path = $"embeds.{index.ToString(CultureInfo.InvariantCulture)}";
            index++;
            if (item.ValueKind != JsonValueKind.Object)
            {
                errors.Add(path, "MODEL_TYPE_CONVERT", FormValidation.OnlyDictionariesMessage);
                continue;
            }

            var embed = ReadEmbed(item, errors.Scope(path), out var characters);
            totalCharacters += characters;
            embeds.Add(embed);
        }

        if (totalCharacters > MaxTotalCharacters)
        {
            // Hypothesis: the code of Discord's budget error is MAX_EMBED_SIZE_EXCEEDED (docs/parity/errors.md).
            errors.Add("embeds", "MAX_EMBED_SIZE_EXCEEDED", $"Embed size exceeds maximum size of {MaxTotalCharacters}");
        }

        return embeds;
    }

    private static EmbedValue ReadEmbed(JsonElement root, FormErrors errors, out int characters)
    {
        characters = 0;

        var title = ReadText(root, "title", MaxTitle, errors, ref characters);
        var description = ReadText(root, "description", MaxDescription, errors, ref characters);
        var url = ReadUrl(root, "url", errors, allowAttachment: false);
        var color = FormValidation.ReadOptionalInteger(root, "color", 0, MaxColor, errors);
        var timestamp = FormValidation.ReadNullableString(root, "timestamp", errors, out _);

        EmbedFooterValue? footer = null;
        if (FormValidation.TryReadObject(root, "footer", errors, out var footerObject))
        {
            var scope = errors.Scope("footer");
            footer = new EmbedFooterValue(
                ReadText(footerObject, "text", MaxFooterText, scope, ref characters),
                ReadUrl(footerObject, "icon_url", scope, allowAttachment: true));
        }

        EmbedAuthorValue? author = null;
        if (FormValidation.TryReadObject(root, "author", errors, out var authorObject))
        {
            var scope = errors.Scope("author");
            author = new EmbedAuthorValue(
                ReadText(authorObject, "name", MaxAuthorName, scope, ref characters),
                ReadUrl(authorObject, "url", scope, allowAttachment: false),
                ReadUrl(authorObject, "icon_url", scope, allowAttachment: true));
        }

        var image = ReadMedia(root, "image", errors);
        var thumbnail = ReadMedia(root, "thumbnail", errors);
        var fields = ReadFields(root, errors, ref characters);
        return new EmbedValue(title, description, url, color, timestamp, footer, image, thumbnail, author, fields);
    }

    private static ImmutableArray<EmbedFieldValue> ReadFields(JsonElement root, FormErrors errors, ref int characters)
    {
        if (!FormValidation.TryReadArray(root, "fields", errors, out var array)
            || !FormValidation.CheckMaxCount(array.GetArrayLength(), "fields", MaxFields, errors))
        {
            return [];
        }

        var fields = ImmutableArray.CreateBuilder<EmbedFieldValue>();
        var index = 0;
        foreach (var item in array.EnumerateArray())
        {
            var path = $"fields.{index.ToString(CultureInfo.InvariantCulture)}";
            index++;
            if (item.ValueKind != JsonValueKind.Object)
            {
                errors.Add(path, "MODEL_TYPE_CONVERT", FormValidation.OnlyDictionariesMessage);
                continue;
            }

            var scope = errors.Scope(path);
            var name = ReadRequiredText(item, "name", MaxFieldName, scope, ref characters);
            var value = ReadRequiredText(item, "value", MaxFieldValue, scope, ref characters);
            var inline = FormValidation.ReadOptionalBoolean(item, "inline", scope) ?? false;
            if (name is not null && value is not null)
            {
                fields.Add(new EmbedFieldValue(name, value, inline));
            }
        }

        return fields.ToImmutable();
    }

    private static EmbedMediaValue? ReadMedia(JsonElement root, string name, FormErrors errors)
    {
        if (!FormValidation.TryReadObject(root, name, errors, out var media))
        {
            return null;
        }

        var scope = errors.Scope(name);
        var url = ReadUrl(media, "url", scope, allowAttachment: true);
        var width = FormValidation.ReadOptionalInteger(media, "width", 0, int.MaxValue, scope);
        var height = FormValidation.ReadOptionalInteger(media, "height", 0, int.MaxValue, scope);
        return new EmbedMediaValue(url, width, height);
    }

    // A nullable string with an upper bound; the characters count towards the budget of the message.
    private static string? ReadText(JsonElement root, string name, int maxLength, FormErrors errors, ref int characters)
    {
        var text = FormValidation.ReadNullableString(root, name, errors, out _);
        if (text is null)
        {
            return null;
        }

        FormValidation.CheckMaxLength(text, name, maxLength, errors);
        characters += text.EnumerateRunes().Count();
        return text;
    }

    private static string? ReadRequiredText(JsonElement root, string name, int maxLength, FormErrors errors, ref int characters)
    {
        if (!root.TryGetProperty(name, out var element) || element.ValueKind == JsonValueKind.Null)
        {
            FormValidation.AddRequired(errors, name);
            return null;
        }

        var text = FormValidation.ReadOptionalString(root, name, errors);
        if (text is null)
        {
            return null;
        }

        if (!FormValidation.CheckLength(text, name, 1, maxLength, errors))
        {
            return null;
        }

        characters += text.EnumerateRunes().Count();
        return text;
    }

    private static string? ReadUrl(JsonElement root, string name, FormErrors errors, bool allowAttachment)
    {
        var url = FormValidation.ReadNullableString(root, name, errors, out _);
        if (url is null)
        {
            return null;
        }

        if (!FormValidation.CheckMaxLength(url, name, MaxUrl, errors))
        {
            return url;
        }

        var wellFormed = Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && (uri.Scheme is "http" or "https" || allowAttachment && uri.Scheme == "attachment");
        if (!wellFormed)
        {
            errors.Add(name, "URL_TYPE_INVALID_URL", "Not a well formed URL.");
        }

        return url;
    }
}
