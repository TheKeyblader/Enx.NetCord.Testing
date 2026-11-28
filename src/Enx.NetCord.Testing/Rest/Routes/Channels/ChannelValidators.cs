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
/// <param name="ParentId">A requested parent category, or <see langword="null"/> when not sent or cleared.</param>
internal sealed record ChannelPatch(
    ulong ChannelId,
    string? Name,
    bool ChangeTopic,
    string? Topic,
    bool? Nsfw,
    int? RateLimitPerUser,
    int? Position,
    int? Type,
    ulong? ParentId);

/// <summary>
/// Structural validators of the channel routes (ADR 0005, step 3): no state is read. The generic parts live in
/// <see cref="FormValidation"/>; only what is specific to channels stays here.
/// </summary>
internal static class ChannelValidators
{
    internal const int NameMinLength = 1;
    internal const int NameMaxLength = 100;
    internal const int TopicMaxLength = 1024;
    internal const int MaxRateLimitPerUser = 21600;

    // The spec's `ChannelTypes` accepted for a guild channel update (UpdateGuildChannelRequestPartial.type).
    private static readonly int[] GuildChannelTypes = [0, 2, 4, 5, 13, 14, 15];

    /// <summary>Validates <c>GET</c> and <c>DELETE /channels/{channel_id}</c>: the route parameter must be a snowflake.</summary>
    /// <param name="request">The request.</param>
    /// <returns>The parsed channel ID as payload, or <c>50035</c>.</returns>
    public static RestValidation ValidateChannelId(FakeRestRequest request)
        => FormValidation.ValidateRouteSnowflake(request, "channel_id");

    /// <summary>
    /// Validates <c>PATCH /channels/{channel_id}</c> against the guild-channel schema of the spec (<c>UpdateGuildChannelRequestPartial</c>);
    /// every field is optional. Fields that do not apply to a text channel (bitrate, voice or forum settings…) are accepted and ignored.
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

            var name = FormValidation.ReadOptionalString(root, "name", errors);
            if (name is not null)
            {
                FormValidation.CheckLength(name, "name", NameMinLength, NameMaxLength, errors);
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
            if (type is { } typeValue && !GuildChannelTypes.Contains(typeValue))
            {
                errors.Add(
                    "type",
                    "BASE_TYPE_CHOICES",
                    $"Value must be one of ({string.Join(", ", GuildChannelTypes.Select(t => t.ToString(CultureInfo.InvariantCulture)))}).");
                type = null;
            }

            ulong? parentId = null;
            if (root.TryGetProperty("parent_id", out var parent) && parent.ValueKind != JsonValueKind.Null
                && FormValidation.TryReadSnowflake(parent, "parent_id", errors, out var parsedParent))
            {
                parentId = parsedParent;
            }

            return errors.IsEmpty
                ? RestValidation.Valid(new ChannelPatch(
                    (ulong)routeValidation.Payload!, name, changeTopic, topic, nsfw, rateLimit, position, type, parentId))
                : DiscordError.InvalidFormBody(errors);
        }
    }
}
