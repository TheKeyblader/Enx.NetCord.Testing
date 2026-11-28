using System.Diagnostics.CodeAnalysis;
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

    /// <summary>400 with code 50034: a message named by a bulk delete is older than two weeks.</summary>
    /// <returns>The error.</returns>
    public static DiscordError MessageTooOldToBulkDelete() => new(HttpStatusCode.BadRequest, 50034, "You can only bulk delete messages that are under 14 days old.");

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

            // Hypotheses, see docs/parity/errors.md: the codes of these two field errors are not documented by Discord.
            StateFailureKind.InvalidChannelType => InvalidFormBody(new FormErrors().Add("type", "CHANNEL_TYPE_INVALID", "Invalid channel type.")),
            StateFailureKind.InvalidChannelParent => InvalidFormBody(new FormErrors().Add("parent_id", "CHANNEL_PARENT_INVALID", "Invalid parent channel.")),
            StateFailureKind.InvalidChannelName => InvalidFormBody(new FormErrors().Add("name", "BASE_TYPE_BAD_LENGTH", "Must be between 1 and 100 in length.")),

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
