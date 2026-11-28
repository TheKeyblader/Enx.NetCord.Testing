using System.Globalization;

namespace Enx.NetCord.Testing.Rest.Routes.Pins;

/// <summary>The validated route and query of <c>GET /channels/{channel_id}/messages/pins</c>.</summary>
/// <param name="ChannelId">The channel ID.</param>
/// <param name="Before">The exclusive upper bound on the pin time, or <see langword="null"/>.</param>
/// <param name="Limit">The number of pins, 1 to 50 (default 50).</param>
internal sealed record PinListQuery(ulong ChannelId, DateTimeOffset? Before, int Limit);

/// <summary>The validated route of a request on one pinned message.</summary>
/// <param name="ChannelId">The channel ID.</param>
/// <param name="MessageId">The message ID.</param>
internal sealed record PinRoute(ulong ChannelId, ulong MessageId);

/// <summary>
/// Structural validators of the pin routes (ADR 0005, step 3): no state is read. The IDs of the route come first, then the query, in
/// one <c>50035</c>.
/// </summary>
internal static class PinValidators
{
    internal const int DefaultLimit = 50;
    internal const int MaxLimit = 50;

    /// <summary>Validates the <c>channel_id</c> and <c>message_id</c> of <c>PUT</c> and <c>DELETE …/messages/pins/{message_id}</c>.</summary>
    /// <param name="request">The request.</param>
    /// <returns>A <see cref="PinRoute"/> payload, or <c>50035</c>.</returns>
    public static RestValidation ValidatePinRoute(FakeRestRequest request)
    {
        var errors = new FormErrors();
        var channelId = FormValidation.ReadRouteSnowflake(request, "channel_id", errors);
        var messageId = FormValidation.ReadRouteSnowflake(request, "message_id", errors);
        return errors.IsEmpty
            ? RestValidation.Valid(new PinRoute(channelId, messageId))
            : DiscordError.InvalidFormBody(errors);
    }

    /// <summary>Validates the route and the query (<c>before</c>, <c>limit</c>) of <c>GET …/messages/pins</c>.</summary>
    /// <param name="request">The request.</param>
    /// <returns>A <see cref="PinListQuery"/> payload, or <c>50035</c>.</returns>
    public static RestValidation ValidateList(FakeRestRequest request)
    {
        var errors = new FormErrors();
        var channelId = FormValidation.ReadRouteSnowflake(request, "channel_id", errors);
        var before = ReadQueryTimestamp(request, "before", errors);
        var limit = FormValidation.ReadQueryInteger(request, "limit", 1, MaxLimit, errors);
        return errors.IsEmpty
            ? RestValidation.Valid(new PinListQuery(channelId, before, limit ?? DefaultLimit))
            : DiscordError.InvalidFormBody(errors);
    }

    // An ISO 8601 date-time. NetCord writes the bound with the "s" format (no offset): it is read as UTC. The error code is a hypothesis.
    private static DateTimeOffset? ReadQueryTimestamp(FakeRestRequest request, string name, FormErrors errors)
    {
        if (request.GetQuery(name) is not { } raw)
        {
            return null;
        }

        if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var value))
        {
            return value;
        }

        errors.Add(name, "DATE_TYPE_PARSE", $"Could not parse \"{raw}\" as a date-time.");
        return null;
    }
}
