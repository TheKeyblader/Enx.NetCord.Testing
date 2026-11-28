using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;

namespace Enx.NetCord.Testing.Rest;

/// <summary>
/// Shared building blocks for the structural validators of every route family (ADR 0005, step 3). They read no state
/// and only produce the errors Discord produces: <c>50109</c> for a body that is not JSON and <c>50035</c> with the
/// field-level codes of the <c>errors</c> tree (<c>MODEL_TYPE_CONVERT</c>, <c>BASE_TYPE_BAD_TYPE</c>,
/// <c>BASE_TYPE_REQUIRED</c>, <c>BASE_TYPE_BAD_LENGTH</c>, <c>NUMBER_TYPE_COERCE</c>, <c>NUMBER_TYPE_MIN</c>, <c>NUMBER_TYPE_MAX</c>,
/// <c>BOOLEAN_TYPE_COERCE</c>).
/// </summary>
/// <remarks>
/// Typical validator: <see cref="TryReadJsonObject"/> (disposing the document), read each field with the
/// <c>Read*</c>/<c>TryRead*</c> helpers while collecting errors in one <see cref="FormErrors"/>, then return
/// <see cref="RestValidation.Valid(object?)"/> with a detached record, or
/// <see cref="DiscordError.InvalidFormBody(FormErrors)"/> when <see cref="FormErrors.IsEmpty"/> is false.
/// A route that only needs a snowflake in its path uses <see cref="ValidateRouteSnowflake"/>.
/// </remarks>
internal static class FormValidation
{
    /// <summary>The Discord message of a root that is not a JSON object.</summary>
    internal const string OnlyDictionariesMessage = "Only dictionaries may be used in a ModelType";

    /// <summary>Parses a snowflake: decimal digits only, no sign, no whitespace, within 64 bits.</summary>
    /// <param name="raw">The raw text.</param>
    /// <param name="id">The parsed ID on success.</param>
    /// <returns><see langword="true"/> when <paramref name="raw"/> is a snowflake.</returns>
    public static bool TryParseSnowflake(string raw, out ulong id)
    {
        ArgumentNullException.ThrowIfNull(raw);
        return ulong.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out id);
    }

    /// <summary>Gets the Discord message for a value that is not a snowflake.</summary>
    /// <param name="raw">The offending value, as sent.</param>
    /// <returns>The message, <c>Value "{raw}" is not snowflake.</c>.</returns>
    public static string NotSnowflakeMessage(string raw) => $"Value \"{raw}\" is not snowflake.";

    /// <summary>Validates that a route parameter is a snowflake.</summary>
    /// <param name="request">The request.</param>
    /// <param name="parameterName">The template parameter name, such as <c>user_id</c>; it is the path of the error.</param>
    /// <returns>The parsed <see cref="ulong"/> as payload, or <c>50035</c> (<c>NUMBER_TYPE_COERCE</c>).</returns>
    public static RestValidation ValidateRouteSnowflake(FakeRestRequest request, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(request);
        var raw = request.RouteParameters[parameterName];
        return TryParseSnowflake(raw, out var id)
            ? RestValidation.Valid(id)
            : DiscordError.InvalidFormBody(new FormErrors().Add(parameterName, "NUMBER_TYPE_COERCE", NotSnowflakeMessage(raw)));
    }

    /// <summary>Reads the request body as a JSON object.</summary>
    /// <param name="request">The request.</param>
    /// <param name="document">The document when the body is an object; the caller disposes it and reads <see cref="JsonDocument.RootElement"/>.</param>
    /// <param name="error"><c>50109</c> when there is no body or it is not JSON, or <c>50035</c> <c>MODEL_TYPE_CONVERT</c> at the root when it is not an object.</param>
    /// <returns><see langword="true"/> when the body is a JSON object.</returns>
    public static bool TryReadJsonObject(
        FakeRestRequest request,
        [NotNullWhen(true)] out JsonDocument? document,
        [NotNullWhen(false)] out DiscordError? error)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Body is null)
        {
            document = null;
            error = DiscordError.InvalidJson();
            return false;
        }

        if (!request.Body.TryParseJson(out document, out error))
        {
            return false;
        }

        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            document.Dispose();
            document = null;
            error = DiscordError.InvalidFormBody(new FormErrors().Add(string.Empty, "MODEL_TYPE_CONVERT", OnlyDictionariesMessage));
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>Adds a <c>BASE_TYPE_BAD_TYPE</c> error.</summary>
    /// <param name="errors">The collector.</param>
    /// <param name="path">The error path.</param>
    /// <param name="expectedType">The expected JSON type, such as <c>string</c>, <c>array</c> or <c>object</c>.</param>
    public static void AddBadType(FormErrors errors, string path, string expectedType)
    {
        ArgumentNullException.ThrowIfNull(errors);
        errors.Add(path, "BASE_TYPE_BAD_TYPE", $"Value must be of type {expectedType}.");
    }

    /// <summary>Adds a <c>BASE_TYPE_REQUIRED</c> error.</summary>
    /// <param name="errors">The collector.</param>
    /// <param name="path">The error path.</param>
    public static void AddRequired(FormErrors errors, string path)
    {
        ArgumentNullException.ThrowIfNull(errors);
        errors.Add(path, "BASE_TYPE_REQUIRED", "This field is required");
    }

    /// <summary>Reads an optional string property: absent gives <see langword="null"/>; any non-string value (including <c>null</c>) is a bad type.</summary>
    /// <param name="root">The JSON object.</param>
    /// <param name="name">The property name, also the error path.</param>
    /// <param name="errors">The collector.</param>
    /// <returns>The value, or <see langword="null"/> when absent or of the wrong type (an error was then added).</returns>
    public static string? ReadOptionalString(JsonElement root, string name, FormErrors errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (!root.TryGetProperty(name, out var element))
        {
            return null;
        }

        if (element.ValueKind != JsonValueKind.String)
        {
            AddBadType(errors, name, "string");
            return null;
        }

        return element.GetString()!;
    }

    /// <summary>
    /// Reads an optional, nullable string property (<c>?string</c>): absent or <c>null</c> are both reported by <paramref name="present"/>
    /// so a caller can tell "not sent" from "cleared"; any other non-string value is a bad type.
    /// </summary>
    /// <param name="root">The JSON object.</param>
    /// <param name="name">The property name, also the error path.</param>
    /// <param name="errors">The collector.</param>
    /// <param name="present">Whether the property was sent (including as <c>null</c>) with a valid type.</param>
    /// <returns>The value, or <see langword="null"/> when absent, <c>null</c> or of the wrong type (an error was then added).</returns>
    public static string? ReadNullableString(JsonElement root, string name, FormErrors errors, out bool present)
    {
        ArgumentNullException.ThrowIfNull(errors);
        present = false;
        if (!root.TryGetProperty(name, out var element))
        {
            return null;
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.Null:
                present = true;
                return null;

            case JsonValueKind.String:
                present = true;
                return element.GetString()!;

            default:
                AddBadType(errors, name, "string");
                return null;
        }
    }

    /// <summary>
    /// Reads an optional, nullable integer property (<c>?integer</c>) and checks its bounds. Like Discord it accepts a JSON integer or a
    /// numeric string; anything else is <c>NUMBER_TYPE_COERCE</c>, a value out of range <c>NUMBER_TYPE_MIN</c>/<c>NUMBER_TYPE_MAX</c>.
    /// Absent and <c>null</c> both give <see langword="null"/> (the field is left as it is).
    /// </summary>
    /// <param name="root">The JSON object.</param>
    /// <param name="name">The property name, also the error path.</param>
    /// <param name="minimum">The inclusive lower bound.</param>
    /// <param name="maximum">The inclusive upper bound.</param>
    /// <param name="errors">The collector.</param>
    /// <returns>The value, or <see langword="null"/> when absent, <c>null</c> or invalid (an error was then added).</returns>
    public static int? ReadOptionalInteger(JsonElement root, string name, int minimum, int maximum, FormErrors errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (!root.TryGetProperty(name, out var element) || element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        string? raw = element.ValueKind switch
        {
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.String => element.GetString(),
            _ => null,
        };
        if (raw is null || !long.TryParse(raw, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value))
        {
            errors.Add(name, "NUMBER_TYPE_COERCE", $"Value \"{raw ?? element.GetRawText()}\" is not int.");
            return null;
        }

        if (value < minimum)
        {
            errors.Add(name, "NUMBER_TYPE_MIN", $"int value should be greater than or equal to {minimum.ToString(CultureInfo.InvariantCulture)}.");
            return null;
        }

        if (value > maximum)
        {
            errors.Add(name, "NUMBER_TYPE_MAX", $"int value should be less than or equal to {maximum.ToString(CultureInfo.InvariantCulture)}.");
            return null;
        }

        return (int)value;
    }

    /// <summary>
    /// Reads an optional, nullable boolean property (<c>?boolean</c>): a JSON boolean; anything else is <c>BOOLEAN_TYPE_COERCE</c>.
    /// Absent and <c>null</c> both give <see langword="null"/> (the field is left as it is).
    /// </summary>
    /// <param name="root">The JSON object.</param>
    /// <param name="name">The property name, also the error path.</param>
    /// <param name="errors">The collector.</param>
    /// <returns>The value, or <see langword="null"/> when absent, <c>null</c> or of the wrong type (an error was then added).</returns>
    public static bool? ReadOptionalBoolean(JsonElement root, string name, FormErrors errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (!root.TryGetProperty(name, out var element) || element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            default:
                errors.Add(name, "BOOLEAN_TYPE_COERCE", $"Value \"{(element.ValueKind == JsonValueKind.String ? element.GetString() : element.GetRawText())}\" is not boolean.");
                return null;
        }
    }

    /// <summary>Checks the length of a string in Unicode code points, as Discord does, and adds <c>BASE_TYPE_BAD_LENGTH</c> when out of range.</summary>
    /// <param name="value">The value.</param>
    /// <param name="path">The error path.</param>
    /// <param name="minLength">The inclusive minimum.</param>
    /// <param name="maxLength">The inclusive maximum.</param>
    /// <param name="errors">The collector.</param>
    /// <returns><see langword="true"/> when the length is in range.</returns>
    public static bool CheckLength(string value, string path, int minLength, int maxLength, FormErrors errors)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(errors);
        var length = value.EnumerateRunes().Count();
        if (length >= minLength && length <= maxLength)
        {
            return true;
        }

        errors.Add(path, "BASE_TYPE_BAD_LENGTH", $"Must be between {minLength} and {maxLength} in length.");
        return false;
    }

    /// <summary>
    /// Reads a snowflake sent as a JSON string or, like NetCord does for some fields, as a JSON number; anything else,
    /// or text that is not a snowflake, is a <c>NUMBER_TYPE_COERCE</c> error.
    /// </summary>
    /// <param name="element">The value.</param>
    /// <param name="path">The error path.</param>
    /// <param name="errors">The collector.</param>
    /// <param name="id">The parsed ID on success.</param>
    /// <returns><see langword="true"/> when the value is a snowflake.</returns>
    public static bool TryReadSnowflake(JsonElement element, string path, FormErrors errors, out ulong id)
    {
        ArgumentNullException.ThrowIfNull(errors);
        var raw = element.ValueKind switch
        {
            JsonValueKind.String => element.GetString()!,
            JsonValueKind.Number => element.GetRawText(),
            _ => null,
        };
        if (raw is not null && TryParseSnowflake(raw, out id))
        {
            return true;
        }

        errors.Add(path, "NUMBER_TYPE_COERCE", NotSnowflakeMessage(raw ?? element.GetRawText()));
        id = 0;
        return false;
    }
}
