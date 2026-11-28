using System.Globalization;
using Enx.NetCord.Testing.State;

namespace Enx.NetCord.Testing.Rest.Routes.Reactions;

/// <summary>The route parameters of a request on every reaction of a message.</summary>
/// <param name="ChannelId">The channel ID.</param>
/// <param name="MessageId">The message ID.</param>
internal sealed record ReactionsRoute(ulong ChannelId, ulong MessageId);

/// <summary>The route parameters of a request on the reactions of one emoji.</summary>
/// <param name="ChannelId">The channel ID.</param>
/// <param name="MessageId">The message ID.</param>
/// <param name="Emoji">The emoji.</param>
internal sealed record ReactionEmojiRoute(ulong ChannelId, ulong MessageId, ReactionEmoji Emoji);

/// <summary>The route parameters of a request on the reaction of one user.</summary>
/// <param name="ChannelId">The channel ID.</param>
/// <param name="MessageId">The message ID.</param>
/// <param name="Emoji">The emoji.</param>
/// <param name="UserId">The user ID.</param>
internal sealed record ReactionUserRoute(ulong ChannelId, ulong MessageId, ReactionEmoji Emoji, ulong UserId);

/// <summary>The validated route and query of <c>GET …/reactions/{emoji_name}</c>.</summary>
/// <param name="ChannelId">The channel ID.</param>
/// <param name="MessageId">The message ID.</param>
/// <param name="Emoji">The emoji.</param>
/// <param name="Burst">Whether <c>type</c> is <c>1</c> (super reactions).</param>
/// <param name="After">The exclusive lower bound on the user ID, or <see langword="null"/>.</param>
/// <param name="Limit">The number of users, 1 to 100 (default 25).</param>
internal sealed record ReactionListQuery(ulong ChannelId, ulong MessageId, ReactionEmoji Emoji, bool Burst, ulong? After, int Limit);

/// <summary>
/// Structural validators of the reaction routes (ADR 0005, step 3): no state is read. The IDs of the route and the query are checked
/// first (<c>50035</c>); the <c>emoji_name</c> segment then, which is <c>10014</c> when it is no emoji (the docs: an emoji that is not URL-encoded
/// "will fail with <c>10014: Unknown Emoji</c>").
/// </summary>
internal static class ReactionValidators
{
    internal const int DefaultLimit = 25;
    internal const int MaxLimit = 100;

    private static readonly int[] ReactionTypes = [0, 1];

    /// <summary>Validates the <c>channel_id</c> and <c>message_id</c> of a route.</summary>
    /// <param name="request">The request.</param>
    /// <returns>A <see cref="ReactionsRoute"/> payload, or <c>50035</c>.</returns>
    public static RestValidation ValidateMessageRoute(FakeRestRequest request)
    {
        var errors = new FormErrors();
        var channelId = ReadRouteSnowflake(request, "channel_id", errors);
        var messageId = ReadRouteSnowflake(request, "message_id", errors);
        return errors.IsEmpty
            ? RestValidation.Valid(new ReactionsRoute(channelId, messageId))
            : DiscordError.InvalidFormBody(errors);
    }

    /// <summary>Validates the route of an operation on one emoji: the IDs, then the emoji.</summary>
    /// <param name="request">The request.</param>
    /// <returns>A <see cref="ReactionEmojiRoute"/> payload, <c>50035</c> or <c>10014</c>.</returns>
    public static RestValidation ValidateEmojiRoute(FakeRestRequest request)
    {
        var errors = new FormErrors();
        var channelId = ReadRouteSnowflake(request, "channel_id", errors);
        var messageId = ReadRouteSnowflake(request, "message_id", errors);
        if (!errors.IsEmpty)
        {
            return DiscordError.InvalidFormBody(errors);
        }

        return ReactionEmoji.TryParse(request.RouteParameters["emoji_name"], out var emoji)
            ? RestValidation.Valid(new ReactionEmojiRoute(channelId, messageId, emoji))
            : DiscordError.UnknownEmoji();
    }

    /// <summary>Validates the route of <c>DELETE …/reactions/{emoji_name}/{user_id}</c>.</summary>
    /// <param name="request">The request.</param>
    /// <returns>A <see cref="ReactionUserRoute"/> payload, <c>50035</c> or <c>10014</c>.</returns>
    public static RestValidation ValidateUserRoute(FakeRestRequest request)
    {
        var errors = new FormErrors();
        var channelId = ReadRouteSnowflake(request, "channel_id", errors);
        var messageId = ReadRouteSnowflake(request, "message_id", errors);
        var userId = ReadRouteSnowflake(request, "user_id", errors);
        if (!errors.IsEmpty)
        {
            return DiscordError.InvalidFormBody(errors);
        }

        return ReactionEmoji.TryParse(request.RouteParameters["emoji_name"], out var emoji)
            ? RestValidation.Valid(new ReactionUserRoute(channelId, messageId, emoji, userId))
            : DiscordError.UnknownEmoji();
    }

    /// <summary>Validates the route and the query (<c>type</c>, <c>after</c>, <c>limit</c>) of <c>GET …/reactions/{emoji_name}</c>.</summary>
    /// <param name="request">The request.</param>
    /// <returns>A <see cref="ReactionListQuery"/> payload, <c>50035</c> or <c>10014</c>.</returns>
    public static RestValidation ValidateList(FakeRestRequest request)
    {
        var errors = new FormErrors();
        var channelId = ReadRouteSnowflake(request, "channel_id", errors);
        var messageId = ReadRouteSnowflake(request, "message_id", errors);
        var type = FormValidation.ReadQueryInteger(request, "type", int.MinValue, int.MaxValue, errors);
        if (type is { } value && !ReactionTypes.Contains(value))
        {
            errors.Add(
                "type",
                "BASE_TYPE_CHOICES",
                $"Value must be one of ({string.Join(", ", ReactionTypes.Select(choice => choice.ToString(CultureInfo.InvariantCulture)))}).");
        }

        var after = FormValidation.ReadQuerySnowflake(request, "after", errors);
        var limit = FormValidation.ReadQueryInteger(request, "limit", 1, MaxLimit, errors);
        if (!errors.IsEmpty)
        {
            return DiscordError.InvalidFormBody(errors);
        }

        return ReactionEmoji.TryParse(request.RouteParameters["emoji_name"], out var emoji)
            ? RestValidation.Valid(new ReactionListQuery(channelId, messageId, emoji, type == 1, after, limit ?? DefaultLimit))
            : DiscordError.UnknownEmoji();
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
}
