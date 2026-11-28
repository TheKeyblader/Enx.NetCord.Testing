using System.Globalization;
using System.Text.Json;

namespace Enx.NetCord.Testing.Rest.Routes.Users;

/// <summary>The validated body of <c>PATCH /users/@me</c>: which fields were sent, and their new values.</summary>
/// <param name="Username">The new username, or <see langword="null"/> when not sent.</param>
/// <param name="ChangeAvatar">Whether <c>avatar</c> was sent.</param>
/// <param name="AvatarHash">The new avatar hash, <see langword="null"/> to remove it.</param>
/// <param name="ChangeBanner">Whether <c>banner</c> was sent.</param>
/// <param name="BannerHash">The new banner hash, <see langword="null"/> to remove it.</param>
internal sealed record CurrentUserPatch(string? Username, bool ChangeAvatar, string? AvatarHash, bool ChangeBanner, string? BannerHash);

/// <summary>The validated body of <c>POST /users/@me/channels</c>.</summary>
/// <param name="RecipientId">The recipient of a 1:1 DM, or <see langword="null"/> for a group DM request.</param>
internal sealed record CreateDmRequest(ulong? RecipientId);

/// <summary>Structural validators of the users routes (ADR 0005, step 3): no state is read.</summary>
internal static class UserValidators
{
    internal const int UsernameMinLength = 2;
    internal const int UsernameMaxLength = 32;

    /// <summary>Validates <c>GET /users/{user_id}</c>: the route parameter must be a snowflake.</summary>
    /// <param name="request">The request.</param>
    /// <returns>The parsed user ID as payload, or <c>50035</c>.</returns>
    public static RestValidation ValidateUserId(FakeRestRequest request)
    {
        var raw = request.RouteParameters["user_id"];
        return TryParseSnowflake(raw, out var id)
            ? RestValidation.Valid(id)
            : DiscordError.InvalidFormBody(new FormErrors().Add("user_id", "NUMBER_TYPE_COERCE", NotSnowflakeMessage(raw)));
    }

    /// <summary>Validates <c>PATCH /users/@me</c>. All fields are optional, as in Discord's documentation.</summary>
    /// <param name="request">The request.</param>
    /// <returns>A <see cref="CurrentUserPatch"/> payload, or <c>50109</c>/<c>50035</c>.</returns>
    public static RestValidation ValidateCurrentUserPatch(FakeRestRequest request)
    {
        if (request.Body is null)
        {
            return DiscordError.InvalidJson();
        }

        if (!request.Body.TryParseJson(out var document, out var parseError))
        {
            return parseError;
        }

        using (document)
        {
            var root = document.RootElement;
            var errors = new FormErrors();
            if (root.ValueKind != JsonValueKind.Object)
            {
                return DiscordError.InvalidFormBody(errors.Add(string.Empty, "MODEL_TYPE_CONVERT", "Only dictionaries may be used in a ModelType"));
            }

            string? username = null;
            if (root.TryGetProperty("username", out var usernameElement))
            {
                if (usernameElement.ValueKind != JsonValueKind.String)
                {
                    errors.Add("username", "BASE_TYPE_BAD_TYPE", "Value must be of type string.");
                }
                else
                {
                    username = usernameElement.GetString()!;
                    var length = username.EnumerateRunes().Count();
                    if (length is < UsernameMinLength or > UsernameMaxLength)
                    {
                        errors.Add(
                            "username",
                            "BASE_TYPE_BAD_LENGTH",
                            $"Must be between {UsernameMinLength} and {UsernameMaxLength} in length.");
                    }
                }
            }

            var (changeAvatar, avatarHash) = ReadImage(root, "avatar", errors);
            var (changeBanner, bannerHash) = ReadImage(root, "banner", errors);

            return errors.IsEmpty
                ? RestValidation.Valid(new CurrentUserPatch(username, changeAvatar, avatarHash, changeBanner, bannerHash))
                : DiscordError.InvalidFormBody(errors);
        }
    }

    /// <summary>
    /// Validates <c>POST /users/@me/channels</c>: <c>recipient_id</c> (a snowflake sent as a string or, like NetCord does,
    /// a number) for a DM, or a non-empty <c>access_tokens</c> array for a group DM.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>A <see cref="CreateDmRequest"/> payload, or <c>50109</c>/<c>50035</c>.</returns>
    public static RestValidation ValidateCreateDm(FakeRestRequest request)
    {
        if (request.Body is null)
        {
            return DiscordError.InvalidJson();
        }

        if (!request.Body.TryParseJson(out var document, out var parseError))
        {
            return parseError;
        }

        using (document)
        {
            var root = document.RootElement;
            var errors = new FormErrors();
            if (root.ValueKind != JsonValueKind.Object)
            {
                return DiscordError.InvalidFormBody(errors.Add(string.Empty, "MODEL_TYPE_CONVERT", "Only dictionaries may be used in a ModelType"));
            }

            var isGroup = false;
            if (root.TryGetProperty("access_tokens", out var tokens) && tokens.ValueKind != JsonValueKind.Null)
            {
                if (tokens.ValueKind != JsonValueKind.Array)
                {
                    errors.Add("access_tokens", "BASE_TYPE_BAD_TYPE", "Value must be of type array.");
                }
                else
                {
                    var index = 0;
                    foreach (var token in tokens.EnumerateArray())
                    {
                        if (token.ValueKind != JsonValueKind.String)
                        {
                            errors.Add($"access_tokens.{index.ToString(CultureInfo.InvariantCulture)}", "BASE_TYPE_BAD_TYPE", "Value must be of type string.");
                        }

                        index++;
                    }

                    // An empty array is treated as absent (same choice as fauxcord): there is nothing to open a group with.
                    isGroup = index > 0;
                }
            }

            if (root.TryGetProperty("nicks", out var nicks) && nicks.ValueKind is not (JsonValueKind.Null or JsonValueKind.Object))
            {
                errors.Add("nicks", "BASE_TYPE_BAD_TYPE", "Value must be of type object.");
            }

            ulong? recipientId = null;
            if (!isGroup)
            {
                if (!root.TryGetProperty("recipient_id", out var recipient) || recipient.ValueKind == JsonValueKind.Null)
                {
                    errors.Add("recipient_id", "BASE_TYPE_REQUIRED", "This field is required");
                }
                else
                {
                    var raw = recipient.ValueKind switch
                    {
                        JsonValueKind.String => recipient.GetString()!,
                        JsonValueKind.Number => recipient.GetRawText(),
                        _ => null,
                    };
                    if (raw is not null && TryParseSnowflake(raw, out var id))
                    {
                        recipientId = id;
                    }
                    else
                    {
                        errors.Add("recipient_id", "NUMBER_TYPE_COERCE", NotSnowflakeMessage(raw ?? recipient.GetRawText()));
                    }
                }
            }

            return errors.IsEmpty
                ? RestValidation.Valid(new CreateDmRequest(recipientId))
                : DiscordError.InvalidFormBody(errors);
        }
    }

    private static (bool Changed, string? Hash) ReadImage(JsonElement root, string name, FormErrors errors)
    {
        if (!root.TryGetProperty(name, out var element))
        {
            return (false, null);
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.Null:
                return (true, null);

            case JsonValueKind.String:
                if (ImageData.TryGetHash(element.GetString()!, out var hash))
                {
                    return (true, hash);
                }

                errors.Add(name, "IMAGE_INVALID", "Invalid image data");
                return (false, null);

            default:
                errors.Add(name, "BASE_TYPE_BAD_TYPE", "Value must be of type string.");
                return (false, null);
        }
    }

    private static bool TryParseSnowflake(string raw, out ulong id)
        => ulong.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out id);

    private static string NotSnowflakeMessage(string raw) => $"Value \"{raw}\" is not snowflake.";
}
