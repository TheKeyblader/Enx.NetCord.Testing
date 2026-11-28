using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Text.Json;
using Enx.NetCord.Testing.Serialization;
using Enx.NetCord.Testing.State;

namespace Enx.NetCord.Testing.Rest;

/// <summary>
/// The catalog of Discord errors the fake can produce: an HTTP status and a JSON body.
/// Handlers never write error JSON by hand (see <c>docs/parity/errors.md</c>).
/// </summary>
internal sealed class DiscordError
{
    private readonly FormErrors? _formErrors;

    private DiscordError(HttpStatusCode status, int code, string message, FormErrors? formErrors = null)
    {
        Status = status;
        Code = code;
        Message = message;
        _formErrors = formErrors;
    }

    /// <summary>Gets the HTTP status.</summary>
    public HttpStatusCode Status { get; }

    /// <summary>Gets the Discord JSON error code.</summary>
    public int Code { get; }

    /// <summary>Gets the message.</summary>
    public string Message { get; }

    /// <summary>401 with code 0: required authentication is missing or unknown.</summary>
    /// <returns>The error.</returns>
    public static DiscordError Unauthorized() => new(HttpStatusCode.Unauthorized, 0, "401: Unauthorized");

    /// <summary>404 with code 0: the route (or operation, in lenient mode) does not exist.</summary>
    /// <returns>The error.</returns>
    public static DiscordError NotFound() => new(HttpStatusCode.NotFound, 0, "404: Not Found");

    /// <summary>405 with code 0: the method is not allowed on a known path.</summary>
    /// <returns>The error.</returns>
    public static DiscordError MethodNotAllowed() => new(HttpStatusCode.MethodNotAllowed, 0, "405: Method Not Allowed");

    /// <summary>404 with code 10003.</summary>
    /// <returns>The error.</returns>
    public static DiscordError UnknownChannel() => new(HttpStatusCode.NotFound, 10003, "Unknown Channel");

    /// <summary>404 with code 10004: the guild does not exist, or exists but the bot is not a member of it.</summary>
    /// <returns>The error.</returns>
    public static DiscordError UnknownGuild() => new(HttpStatusCode.NotFound, 10004, "Unknown Guild");

    /// <summary>403 with code 50001: the resource exists but the bot cannot see it.</summary>
    /// <returns>The error.</returns>
    public static DiscordError MissingAccess() => new(HttpStatusCode.Forbidden, 50001, "Missing Access");

    /// <summary>404 with code 10008.</summary>
    /// <returns>The error.</returns>
    public static DiscordError UnknownMessage() => new(HttpStatusCode.NotFound, 10008, "Unknown Message");

    /// <summary>404 with code 10013.</summary>
    /// <returns>The error.</returns>
    public static DiscordError UnknownUser() => new(HttpStatusCode.NotFound, 10013, "Unknown User");

    /// <summary>
    /// 404 with code 10014: the emoji is unknown. Discord documents it for an emoji that is not URL-encoded; the fake also answers it for a
    /// segment that is no emoji, and for the removal of a reaction nobody made (hypotheses, see <c>docs/parity/errors.md</c>).
    /// </summary>
    /// <returns>The error.</returns>
    public static DiscordError UnknownEmoji() => new(HttpStatusCode.NotFound, 10014, "Unknown Emoji");

    /// <summary>
    /// 400 with code 30010: the message already has 20 distinct reactions. The code and message are documented; the HTTP status is not
    /// (hypothesis, see <c>docs/parity/errors.md</c>).
    /// </summary>
    /// <returns>The error.</returns>
    public static DiscordError MaxReactionsReached() => new(HttpStatusCode.BadRequest, 30010, "Maximum number of reactions reached (20)");

    /// <summary>
    /// 403 with code 50025: an OAuth2 access token (group DM creation) is not one the fake knows. Discord documents
    /// <c>50025</c> with HTTP 403 for an invalid <c>external_auth_token</c> (provisional accounts, merging accounts); the same
    /// status is assumed for group DM creation, which is not documented (see <c>docs/parity/errors.md</c>).
    /// </summary>
    /// <returns>The error.</returns>
    public static DiscordError InvalidOAuth2AccessToken() => new(HttpStatusCode.Forbidden, 50025, "Invalid OAuth2 access token provided");

    /// <summary>403 with code 50003: the action is not available in a DM channel.</summary>
    /// <returns>The error.</returns>
    public static DiscordError CannotExecuteOnDm() => new(HttpStatusCode.Forbidden, 50003, "Cannot execute action on a DM channel");

    /// <summary>403 with code 50005: the bot edits the content of a message another user wrote.</summary>
    /// <returns>The error.</returns>
    public static DiscordError CannotEditOtherAuthor() => new(HttpStatusCode.Forbidden, 50005, "Cannot edit a message authored by another user");

    /// <summary>400 with code 50006: the message would have neither content, embed nor attachment.</summary>
    /// <returns>The error.</returns>
    public static DiscordError EmptyMessage() => new(HttpStatusCode.BadRequest, 50006, "Cannot send an empty message");

    /// <summary>400 with code 50024: the action does not apply to this type of channel.</summary>
    /// <returns>The error.</returns>
    public static DiscordError CannotExecuteOnChannelType() => new(HttpStatusCode.BadRequest, 50024, "Cannot execute action on this channel type");

    /// <summary>
    /// 400 with code 50008: a message is sent to a channel with no chat (a category). The code and message are documented;
    /// the HTTP status is not (hypothesis, see <c>docs/parity/errors.md</c>).
    /// </summary>
    /// <returns>The error.</returns>
    public static DiscordError CannotSendInNonTextChannel() => new(HttpStatusCode.BadRequest, 50008, "Cannot send messages in a non-text channel");

    /// <summary>
    /// 400 with code 40058: a message is sent to a forum or media channel. The code and message are documented (for forum channels);
    /// the HTTP status is not (hypothesis, see <c>docs/parity/errors.md</c>).
    /// </summary>
    /// <returns>The error.</returns>
    public static DiscordError CannotSendInForumChannel() => new(HttpStatusCode.BadRequest, 40058, "Cannot send a message in a forum channel");

    /// <summary>
    /// 400 with code 40033: the message was already crossposted. The code and message are documented; the HTTP status is not
    /// (hypothesis, see <c>docs/parity/errors.md</c>).
    /// </summary>
    /// <returns>The error.</returns>
    public static DiscordError MessageAlreadyCrossposted() => new(HttpStatusCode.BadRequest, 40033, "This message has already been crossposted");

    /// <summary>400 with code 50034: a message named by a bulk delete is older than two weeks.</summary>
    /// <returns>The error.</returns>
    public static DiscordError MessageTooOldToBulkDelete() => new(HttpStatusCode.BadRequest, 50034, "You can only bulk delete messages that are under 14 days old.");

    /// <summary>
    /// 400 with code 30052: the bitrate of a voice or stage channel exceeds what the channel type allows. The code and message are
    /// documented; the HTTP status is not (hypothesis, see <c>docs/parity/errors.md</c>).
    /// </summary>
    /// <returns>The error.</returns>
    public static DiscordError BitrateTooHigh() => new(HttpStatusCode.BadRequest, 30052, "Bitrate is too high for channel of this type");

    /// <summary>
    /// 400 with code 30013: the guild already has 500 channels. The code and message are documented
    /// (<c>opcodes-and-status-codes.mdx</c>); the HTTP status is not (hypothesis, see <c>docs/parity/errors.md</c>).
    /// </summary>
    /// <returns>The error.</returns>
    public static DiscordError MaxGuildChannels() => new(HttpStatusCode.BadRequest, 30013, "Maximum number of guild channels reached (500)");

    /// <summary>
    /// 400 with code 40009: more than one channel of a <c>PATCH /guilds/{guild_id}/channels</c> changes its parent. The code and message
    /// are documented; the HTTP status is not (hypothesis, see <c>docs/parity/errors.md</c>).
    /// </summary>
    /// <returns>The error.</returns>
    public static DiscordError OnlyOneParentChange() => new(HttpStatusCode.BadRequest, 40009, "Only one channel can have a parent_id modified at a time");

    /// <summary>400 <c>50035</c> on a guild setting that names a channel: it is not a channel of the guild, or of the wrong type. The field code and message are hypotheses.</summary>
    /// <param name="field">The wire field, such as <c>afk_channel_id</c>.</param>
    /// <returns>The error.</returns>
    public static DiscordError InvalidGuildChannelReference(string field)
    {
        ArgumentException.ThrowIfNullOrEmpty(field);
        return InvalidFormBody(new FormErrors().Add(field, "GUILD_CHANNEL_INVALID", "Invalid channel for this setting."));
    }

    /// <summary>400 <c>50035</c> on <c>type</c>: a guild channel cannot become the requested type (only text and announcement convert). The field code is a hypothesis.</summary>
    /// <returns>The error.</returns>
    public static DiscordError InvalidChannelType()
        => InvalidFormBody(new FormErrors().Add("type", "CHANNEL_TYPE_INVALID", "Invalid channel type."));

    /// <summary>400 <c>50035</c> on <c>parent_id</c>: the parent does not exist in the guild. The field code and message are hypotheses (the message is reported by a third party).</summary>
    /// <returns>The error.</returns>
    public static DiscordError InvalidChannelParent()
        => InvalidFormBody(new FormErrors().Add("parent_id", "CHANNEL_PARENT_INVALID", "Category does not exist"));

    /// <summary>400 <c>50035</c> on <c>parent_id</c>: the parent exists but is not a category. The field code and message are hypotheses.</summary>
    /// <returns>The error.</returns>
    public static DiscordError ChannelParentNotCategory()
        => InvalidFormBody(new FormErrors().Add("parent_id", "CHANNEL_PARENT_INVALID_TYPE", "Not a category"));

    /// <summary>400 <c>50035</c> on <c>parent_id</c>: the channel is a category, which cannot have a parent. The field code and message are hypotheses.</summary>
    /// <returns>The error.</returns>
    public static DiscordError ChannelCannotHaveParent()
        => InvalidFormBody(new FormErrors().Add("parent_id", "CHANNEL_PARENT_INVALID", "Categories cannot have subcategories"));

    /// <summary>400 <c>50035</c> on <c>parent_id</c>: the category already holds 50 channels (documented limit; field code and message are hypotheses).</summary>
    /// <returns>The error.</returns>
    public static DiscordError ChannelParentFull()
        => InvalidFormBody(new FormErrors().Add("parent_id", "CHANNEL_PARENT_MAX_CHANNELS", "Maximum number of channels in category reached (50)"));

    /// <summary>400 <c>50035</c> on <c>topic</c>: longer than the 1024 characters of a channel that is not a forum or media channel. The form is a hypothesis.</summary>
    /// <returns>The error.</returns>
    public static DiscordError InvalidChannelTopic()
        => InvalidFormBody(new FormErrors().Add("topic", "BASE_TYPE_BAD_LENGTH", "Must be between 0 and 1024 in length."));

    /// <summary>400 <c>50035</c> on <c>user_limit</c>: above what the voice (99) or stage (10000) channel allows. The form is a hypothesis.</summary>
    /// <param name="limit">The maximum of the channel type.</param>
    /// <returns>The error.</returns>
    public static DiscordError InvalidChannelUserLimit(int limit)
        => InvalidFormBody(new FormErrors().Add("user_limit", "NUMBER_TYPE_MAX", $"int value should be less than or equal to {limit.ToString(CultureInfo.InvariantCulture)}."));

    /// <summary>403 with code 50013.</summary>
    /// <returns>The error.</returns>
    public static DiscordError MissingPermissions() => new(HttpStatusCode.Forbidden, 50013, "Missing Permissions");

    /// <summary>400 with code 50109: the body is not syntactically valid JSON.</summary>
    /// <returns>The error.</returns>
    public static DiscordError InvalidJson() => new(HttpStatusCode.BadRequest, 50109, "The request body contains invalid JSON.");

    /// <summary>500 with code 0.</summary>
    /// <returns>The error.</returns>
    public static DiscordError InternalServerError() => new(HttpStatusCode.InternalServerError, 0, "500: Internal Server Error");

    /// <summary>502 with code 0.</summary>
    /// <returns>The error.</returns>
    public static DiscordError BadGateway() => new(HttpStatusCode.BadGateway, 0, "502: Bad Gateway");

    /// <summary>400 with code 50035 and the field error tree.</summary>
    /// <param name="errors">The collected field errors; must not be empty.</param>
    /// <returns>The error.</returns>
    /// <exception cref="ArgumentException"><paramref name="errors"/> is empty.</exception>
    public static DiscordError InvalidFormBody(FormErrors errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (errors.IsEmpty)
        {
            throw new ArgumentException("An Invalid Form Body error needs at least one field error.", nameof(errors));
        }

        return new DiscordError(HttpStatusCode.BadRequest, 50035, "Invalid Form Body", errors);
    }

    /// <summary>
    /// Translates a business refusal of the state into its catalog error, once for every route
    /// (ADR 0004, amendment 04B, § 2). A kind without a catalog entry yet is a programming error, not a silent 500.
    /// </summary>
    /// <param name="failure">The refusal.</param>
    /// <returns>The error.</returns>
    /// <exception cref="NotSupportedException">The kind has no catalog error yet.</exception>
    public static DiscordError FromStateFailure(StateFailure failure)
        => failure.Kind switch
        {
            StateFailureKind.UnknownGuild => UnknownGuild(),
            StateFailureKind.UnknownChannel => UnknownChannel(),
            StateFailureKind.UnknownMessage => UnknownMessage(),
            StateFailureKind.UnknownUser => UnknownUser(),
            StateFailureKind.MissingAccess => MissingAccess(),
            StateFailureKind.CannotEditOtherAuthor => CannotEditOtherAuthor(),
            StateFailureKind.EmptyMessage => EmptyMessage(),
            StateFailureKind.MessageTooOldForBulkDelete => MessageTooOldToBulkDelete(),
            StateFailureKind.CannotExecuteInDm => CannotExecuteOnDm(),
            StateFailureKind.CannotExecuteOnChannelType => CannotExecuteOnChannelType(),
            StateFailureKind.UnknownEmoji => UnknownEmoji(),
            StateFailureKind.MaxReactions => MaxReactionsReached(),
            StateFailureKind.CannotSendInNonTextChannel => CannotSendInNonTextChannel(),
            StateFailureKind.CannotSendInForumChannel => CannotSendInForumChannel(),
            StateFailureKind.MessageAlreadyCrossposted => MessageAlreadyCrossposted(),

            // Hypotheses, see docs/parity/errors.md: the codes of these field errors are not documented by Discord.
            StateFailureKind.InvalidChannelType => InvalidChannelType(),
            StateFailureKind.InvalidChannelParent => InvalidChannelParent(),
            StateFailureKind.ChannelParentNotCategory => ChannelParentNotCategory(),
            StateFailureKind.ChannelCannotHaveParent => ChannelCannotHaveParent(),
            StateFailureKind.ChannelParentFull => ChannelParentFull(),
            StateFailureKind.InvalidChannelName => InvalidFormBody(new FormErrors().Add("name", "BASE_TYPE_BAD_LENGTH", "Must be between 1 and 100 in length.")),
            StateFailureKind.InvalidChannelTopic => InvalidChannelTopic(),
            StateFailureKind.InvalidChannelUserLimit => InvalidChannelUserLimit(failure.Limit),
            StateFailureKind.ChannelBitrateTooHigh => BitrateTooHigh(),
            StateFailureKind.MaxGuildChannels => MaxGuildChannels(),
            StateFailureKind.TooManyParentChanges => OnlyOneParentChange(),
            StateFailureKind.InvalidGuildChannelReference => InvalidGuildChannelReference(
                failure.Field ?? throw new InvalidOperationException("An InvalidGuildChannelReference failure carries its field.")),

            // Hypothesis (docs/parity/errors.md): the code of the field error of a reply to a message that does not exist.
            StateFailureKind.UnknownMessageReference => InvalidFormBody(new FormErrors().Add("message_reference", "MESSAGE_REFERENCE_UNKNOWN_MESSAGE", "Unknown message")),
            _ => throw new NotSupportedException($"The state failure {failure.Kind} has no catalog error yet."),
        };

    /// <summary>Tries to create one of the simple (detail-free) catalog errors for a status/code pair.</summary>
    /// <param name="status">The HTTP status.</param>
    /// <param name="code">The Discord JSON code.</param>
    /// <param name="error">The error when the pair is in the catalog.</param>
    /// <returns><see langword="true"/> when the pair is supported.</returns>
    public static bool TryCreateSimple(HttpStatusCode status, int code, [NotNullWhen(true)] out DiscordError? error)
    {
        error = (status, code) switch
        {
            (HttpStatusCode.Unauthorized, 0) => Unauthorized(),
            (HttpStatusCode.Forbidden, 50013) => MissingPermissions(),
            (HttpStatusCode.NotFound, 0) => NotFound(),
            (HttpStatusCode.NotFound, 10003) => UnknownChannel(),
            (HttpStatusCode.NotFound, 10004) => UnknownGuild(),
            (HttpStatusCode.Forbidden, 50001) => MissingAccess(),
            (HttpStatusCode.NotFound, 10008) => UnknownMessage(),
            (HttpStatusCode.Forbidden, 50003) => CannotExecuteOnDm(),
            (HttpStatusCode.Forbidden, 50005) => CannotEditOtherAuthor(),
            (HttpStatusCode.BadRequest, 50006) => EmptyMessage(),
            (HttpStatusCode.BadRequest, 50024) => CannotExecuteOnChannelType(),
            (HttpStatusCode.BadRequest, 50034) => MessageTooOldToBulkDelete(),
            (HttpStatusCode.BadRequest, 50008) => CannotSendInNonTextChannel(),
            (HttpStatusCode.BadRequest, 40058) => CannotSendInForumChannel(),
            (HttpStatusCode.BadRequest, 40033) => MessageAlreadyCrossposted(),
            (HttpStatusCode.BadRequest, 30052) => BitrateTooHigh(),
            (HttpStatusCode.BadRequest, 30013) => MaxGuildChannels(),
            (HttpStatusCode.BadRequest, 40009) => OnlyOneParentChange(),
            (HttpStatusCode.NotFound, 10014) => UnknownEmoji(),
            (HttpStatusCode.BadRequest, 30010) => MaxReactionsReached(),
            (HttpStatusCode.NotFound, 10013) => UnknownUser(),
            (HttpStatusCode.Forbidden, 50025) => InvalidOAuth2AccessToken(),
            (HttpStatusCode.InternalServerError, 0) => InternalServerError(),
            (HttpStatusCode.BadGateway, 0) => BadGateway(),
            _ => null,
        };
        return error is not null;
    }

    /// <summary>Serializes the Discord JSON body.</summary>
    /// <returns>UTF-8 JSON bytes.</returns>
    public byte[] ToJson()
        => JsonSerializer.SerializeToUtf8Bytes(
            new ErrorResponse { Message = Message, Code = Code, Errors = _formErrors?.Root },
            FakeDiscordJsonContext.Default.ErrorResponse);
}
