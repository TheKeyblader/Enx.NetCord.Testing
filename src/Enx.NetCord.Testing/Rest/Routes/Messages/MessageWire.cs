using System.Globalization;
using Enx.NetCord.Testing.Rest.Routes.Users;
using Enx.NetCord.Testing.Serialization;
using Enx.NetCord.Testing.State;

namespace Enx.NetCord.Testing.Rest.Routes.Messages;

/// <summary>Projects detached message snapshots into the wire DTOs of the message routes.</summary>
internal static class MessageWire
{
    /// <summary>The host the deterministic attachment URLs point at; nothing is ever served there.</summary>
    internal const string CdnHost = "https://cdn.discordapp.com";

    private const string MediaHost = "https://media.discordapp.net";

    /// <summary>Projects a message and the users it names (author, mentions, replied-to author) with <paramref name="findUser"/>.</summary>
    /// <param name="message">The message.</param>
    /// <param name="findUser">Looks a user up in the state (<c>FindUser</c>).</param>
    /// <param name="nested">Whether the message is itself the replied-to message of another one: its own <c>referenced_message</c> is then omitted.</param>
    /// <returns>The DTO.</returns>
    public static MessageResponse ToResponse(MessageSnapshot message, Func<ulong, UserSnapshot?> findUser, bool nested = false)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(findUser);

        return new MessageResponse
        {
            Id = Id(message.Id),
            ChannelId = Id(message.ChannelId),
            Author = User(message.AuthorId, findUser),
            Content = message.Content,
            Timestamp = message.Timestamp.ToString("O", CultureInfo.InvariantCulture),
            EditedTimestamp = message.EditedTimestamp?.ToString("O", CultureInfo.InvariantCulture),
            Tts = message.Tts,
            MentionEveryone = message.MentionEveryone,
            Mentions = [.. message.MentionUserIds.Select(id => User(id, findUser))],
            MentionRoles = [.. message.MentionRoleIds.Select(Id)],
            Attachments = [.. message.Attachments.Select(a => ToAttachment(message.ChannelId, a))],
            Embeds = [.. message.Embeds.Select(ToEmbed)],
            Pinned = message.Pinned,
            Type = message.Type,
            Flags = message.Flags,
            Components = [],
            Nonce = message.Nonce,
            MessageReference = message.Reference is { } reference
                ? new MessageReferenceResponse
                {
                    Type = 0,
                    ChannelId = Id(reference.ChannelId),
                    MessageId = Id(reference.MessageId),
                    GuildId = reference.GuildId is { } guildId ? Id(guildId) : null,
                }
                : null,

            // A reference whose target is gone is written as an explicit null (Discord: absent = unknown, null = deleted). A
            // replied-to message is a BasicMessageResponse: it never carries its own referenced_message, whatever it replies to.
            ReferencedMessage = nested || message.Reference is null
                ? null
                : new ReferencedMessageResponse(message.ReferencedMessage is { } referenced ? ToResponse(referenced, findUser, nested: true) : null),
        };
    }

    private static UserResponse User(ulong id, Func<ulong, UserSnapshot?> findUser)
        => findUser(id) is { } user
            ? UserWire.ToUser(user)
            : throw new InvalidOperationException($"The user {id} of a message does not exist in the state.");

    private static MessageAttachmentResponse ToAttachment(ulong channelId, AttachmentValue attachment)
    {
        var path = $"/attachments/{Id(channelId)}/{Id(attachment.Id)}/{Uri.EscapeDataString(attachment.FileName)}";
        return new MessageAttachmentResponse
        {
            Id = Id(attachment.Id),
            FileName = attachment.FileName,
            Size = attachment.Size,
            Url = CdnHost + path,
            ProxyUrl = MediaHost + path,
            ContentType = attachment.ContentType,
            Title = attachment.Title,
            Description = attachment.Description,
        };
    }

    private static MessageEmbedResponse ToEmbed(EmbedValue embed) => new()
    {
        Type = "rich",
        Title = embed.Title,
        Description = embed.Description,
        Url = embed.Url,
        Color = embed.Color,
        Timestamp = embed.Timestamp,
        Footer = embed.Footer is { } footer ? new MessageEmbedFooterResponse { Text = footer.Text, IconUrl = footer.IconUrl } : null,
        Image = embed.Image is { } image ? ToImage(image) : null,
        Thumbnail = embed.Thumbnail is { } thumbnail ? ToImage(thumbnail) : null,
        Author = embed.Author is { } author
            ? new MessageEmbedAuthorResponse { Name = author.Name, Url = author.Url, IconUrl = author.IconUrl }
            : null,
        Fields = embed.Fields.IsEmpty
            ? null
            : [.. embed.Fields.Select(f => new MessageEmbedFieldResponse { Name = f.Name, Value = f.Value, Inline = f.Inline })],
    };

    private static MessageEmbedImageResponse ToImage(EmbedMediaValue media)
        => new() { Url = media.Url, Width = media.Width, Height = media.Height };

    private static string Id(ulong id) => id.ToString(CultureInfo.InvariantCulture);
}
