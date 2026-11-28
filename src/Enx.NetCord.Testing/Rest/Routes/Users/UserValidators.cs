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

/// <summary>
/// Structural validators of the users routes (ADR 0005, step 3): no state is read. The generic parts (snowflakes,
/// JSON object body, typed fields) live in <see cref="FormValidation"/>; only what is specific to users stays here.
/// </summary>
internal static class UserValidators
{
    internal const int UsernameMinLength = 2;
    internal const int UsernameMaxLength = 32;

    /// <summary>Validates <c>GET /users/{user_id}</c>: the route parameter must be a snowflake.</summary>
    /// <param name="request">The request.</param>
    /// <returns>The parsed user ID as payload, or <c>50035</c>.</returns>
    public static RestValidation ValidateUserId(FakeRestRequest request)
        => FormValidation.ValidateRouteSnowflake(request, "user_id");

    /// <summary>Validates <c>PATCH /users/@me</c>. All fields are optional, as in Discord's documentation.</summary>
    /// <param name="request">The request.</param>
    /// <returns>A <see cref="CurrentUserPatch"/> payload, or <c>50109</c>/<c>50035</c>.</returns>
    public static RestValidation ValidateCurrentUserPatch(FakeRestRequest request)
    {
        if (!FormValidation.TryReadJsonObject(request, out var document, out var error))
        {
            return error;
        }

        using (document)
        {
            var root = document.RootElement;
            var errors = new FormErrors();

            var username = FormValidation.ReadOptionalString(root, "username", errors);
            if (username is not null)
            {
                FormValidation.CheckLength(username, "username", UsernameMinLength, UsernameMaxLength, errors);
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
        if (!FormValidation.TryReadJsonObject(request, out var document, out var error))
        {
            return error;
        }

        using (document)
        {
            var root = document.RootElement;
            var errors = new FormErrors();

            var isGroup = false;
            if (root.TryGetProperty("access_tokens", out var tokens) && tokens.ValueKind != JsonValueKind.Null)
            {
                if (tokens.ValueKind != JsonValueKind.Array)
                {
                    FormValidation.AddBadType(errors, "access_tokens", "array");
                }
                else
                {
                    var index = 0;
                    foreach (var token in tokens.EnumerateArray())
                    {
                        if (token.ValueKind != JsonValueKind.String)
                        {
                            FormValidation.AddBadType(errors, $"access_tokens.{index.ToString(CultureInfo.InvariantCulture)}", "string");
                        }

                        index++;
                    }

                    // An empty array is treated as absent (same choice as fauxcord): there is nothing to open a group with.
                    isGroup = index > 0;
                }
            }

            if (root.TryGetProperty("nicks", out var nicks) && nicks.ValueKind is not (JsonValueKind.Null or JsonValueKind.Object))
            {
                FormValidation.AddBadType(errors, "nicks", "object");
            }

            ulong? recipientId = null;
            if (!isGroup)
            {
                if (!root.TryGetProperty("recipient_id", out var recipient) || recipient.ValueKind == JsonValueKind.Null)
                {
                    FormValidation.AddRequired(errors, "recipient_id");
                }
                else if (FormValidation.TryReadSnowflake(recipient, "recipient_id", errors, out var id))
                {
                    recipientId = id;
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
                FormValidation.AddBadType(errors, name, "string");
                return (false, null);
        }
    }
}
