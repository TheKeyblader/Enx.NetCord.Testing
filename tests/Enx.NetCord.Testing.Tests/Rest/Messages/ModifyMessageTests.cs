using System.Net;
using Enx.NetCord.Testing.State;
using NetCord;
using NetCord.Rest;
using Xunit;
using static Enx.NetCord.Testing.Tests.Rest.Messages.MessagesTestSupport;
using static Enx.NetCord.Testing.Tests.State.StateTestSupport;

namespace Enx.NetCord.Testing.Tests.Rest.Messages;

public sealed class ModifyMessageTests
{
    [Fact]
    [Parity("PATCH /channels/{channel_id}/messages/{message_id}")]
    public async Task ModifyMessageAsync_OwnMessage_ChangesTheContentAndStampsTheEditFromTheClock()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();
        var sent = await rest.SendMessageAsync(general.Id, "v1", cancellationToken: Ct);

        var edited = await rest.ModifyMessageAsync(general.Id, sent.Id, o => o.Content = "v2", cancellationToken: Ct);

        Assert.Equal("v2", edited.Content);
        Assert.Equal(sent.Id, edited.Id);
        Assert.Equal(VirtualTimeProvider.DefaultNow, edited.EditedAt);
        var read = await rest.GetMessageAsync(general.Id, sent.Id, cancellationToken: Ct);
        Assert.Equal("v2", read.Content);
        Assert.Equal(VirtualTimeProvider.DefaultNow, read.EditedAt);
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}/messages/{message_id}")]
    public async Task ModifyMessageAsync_Edit_PublishesOneMessageEditedWithTheNewContent()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();
        var sent = await rest.SendMessageAsync(general.Id, "v1", cancellationToken: Ct);
        using var subscription = discord.State.SubscribeGateway();

        await rest.ModifyMessageAsync(general.Id, sent.Id, o => o.Content = "v2", cancellationToken: Ct);

        var change = Assert.IsType<MessageEdited>(Assert.Single(Drain(subscription)));
        Assert.Equal("v2", change.Message.Content);
        Assert.NotNull(change.Message.EditedTimestamp);
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}/messages/{message_id}")]
    public async Task ModifyMessageAsync_OnlyEmbeds_LeavesTheContentAndReplacesTheEmbeds()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();
        var sent = await rest.SendMessageAsync(
            general.Id,
            new MessageProperties { Content = "keep", Embeds = [new EmbedProperties { Title = "old" }] },
            cancellationToken: Ct);

        var edited = await rest.ModifyMessageAsync(
            general.Id,
            sent.Id,
            o => o.Embeds = [new EmbedProperties { Title = "new" }],
            cancellationToken: Ct);

        Assert.Equal("keep", edited.Content);
        Assert.Equal("new", Assert.Single(edited.Embeds).Title);
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}/messages/{message_id}")]
    public async Task SendRequest_PatchWithNullContentAndEmbeds_ClearsBothButNotTheLastThing()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();
        var sent = await rest.SendMessageAsync(
            general.Id,
            new MessageProperties { Content = "text", Embeds = [new EmbedProperties { Title = "e" }] },
            cancellationToken: Ct);

        using var clearedContent = await SendRawAsync(rest, HttpMethod.Patch, $"/channels/{general.Id}/messages/{sent.Id}", """{"content":null}""");
        var exception = await SendRawFailingAsync(rest, HttpMethod.Patch, $"/channels/{general.Id}/messages/{sent.Id}", """{"embeds":null}""");

        Assert.Equal(string.Empty, clearedContent.RootElement.GetProperty("content").GetString());
        Assert.Equal(1, clearedContent.RootElement.GetProperty("embeds").GetArrayLength());
        AssertError(exception, HttpStatusCode.BadRequest, 50006);
        Assert.Single((await rest.GetMessageAsync(general.Id, sent.Id, cancellationToken: Ct)).Embeds);
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}/messages/{message_id}")]
    public async Task ModifyMessageAsync_Content_RebuildsTheMentionsFromScratch()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var aliceId = guild.Member("alice");
        using var rest = discord.CreateRestClient();
        var sent = await rest.SendMessageAsync(general.Id, $"<@{aliceId}>", cancellationToken: Ct);

        var edited = await rest.ModifyMessageAsync(general.Id, sent.Id, o => o.Content = "nobody", cancellationToken: Ct);

        Assert.Equal(aliceId, Assert.Single(sent.MentionedUsers).Id);
        Assert.Empty(edited.MentionedUsers);
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}/messages/{message_id}")]
    public async Task ModifyMessageAsync_SuppressEmbedsFlag_CanBeSetAndUnset()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();
        var sent = await rest.SendMessageAsync(general.Id, "link", cancellationToken: Ct);

        var set = await rest.ModifyMessageAsync(general.Id, sent.Id, o => o.Flags = MessageFlags.SuppressEmbeds | MessageFlags.Crossposted, cancellationToken: Ct);
        var unset = await rest.ModifyMessageAsync(general.Id, sent.Id, o => o.Flags = 0, cancellationToken: Ct);

        Assert.Equal(MessageFlags.SuppressEmbeds, set.Flags);
        Assert.Equal((MessageFlags)0, unset.Flags);
    }

    private static async Task<RestMessage> SendWithTwoFilesAsync(RestClient rest, ulong channelId)
        => await rest.SendMessageAsync(
            channelId,
            new MessageProperties
            {
                Content = "files",
                Attachments =
                [
                    new AttachmentProperties("a.txt", new MemoryStream([1])),
                    new AttachmentProperties("b.txt", new MemoryStream([1, 2])),
                ],
            },
            cancellationToken: Ct);

    [Fact]
    [Parity("PATCH /channels/{channel_id}/messages/{message_id}")]
    public async Task ModifyMessageAsync_ContentOnly_KeepsTheAttachments()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();
        var sent = await SendWithTwoFilesAsync(rest, general.Id);

        var edited = await rest.ModifyMessageAsync(general.Id, sent.Id, o => o.Content = "renamed", cancellationToken: Ct);

        Assert.Equal(sent.Attachments.Select(a => a.Id), edited.Attachments.Select(a => a.Id));
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}/messages/{message_id}")]
    public async Task ModifyMessageAsync_NewFiles_ReplaceTheExistingAttachments()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();
        var sent = await SendWithTwoFilesAsync(rest, general.Id);

        // NetCord numbers the attachments it sends from 0, so it can only replace them (API v10: `attachments` lists what remains).
        var edited = await rest.ModifyMessageAsync(
            general.Id,
            sent.Id,
            o => o.Attachments = [new AttachmentProperties("c.txt", new MemoryStream([1, 2, 3]))],
            cancellationToken: Ct);

        var attachment = Assert.Single(edited.Attachments);
        Assert.Equal(("c.txt", 3), (attachment.FileName, attachment.Size));
        Assert.DoesNotContain(attachment.Id, sent.Attachments.Select(a => a.Id));
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}/messages/{message_id}")]
    public async Task SendRequest_PatchListingAnExistingAttachmentId_RetainsOnlyThatOne()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();
        var sent = await SendWithTwoFilesAsync(rest, general.Id);
        var keep = sent.Attachments.Single(a => a.FileName == "b.txt");

        using var json = await SendRawAsync(rest, HttpMethod.Patch, $"/channels/{general.Id}/messages/{sent.Id}", $$"""{"attachments":[{"id":"{{keep.Id}}"}]}""");

        var attachment = Assert.Single(json.RootElement.GetProperty("attachments").EnumerateArray());
        Assert.Equal(keep.Id.ToString(), attachment.GetProperty("id").GetString());
        Assert.Equal("b.txt", attachment.GetProperty("filename").GetString());
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}/messages/{message_id}")]
    public async Task ModifyMessageAsync_AttachmentsListedEmpty_RemovesThemAll()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();
        var sent = await rest.SendMessageAsync(
            general.Id,
            new MessageProperties { Content = "files", Attachments = [new AttachmentProperties("a.txt", new MemoryStream([1]))] },
            cancellationToken: Ct);

        var edited = await rest.ModifyMessageAsync(general.Id, sent.Id, o => o.Attachments = [], cancellationToken: Ct);

        Assert.Empty(edited.Attachments);
        Assert.Equal("files", edited.Content);
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}/messages/{message_id}")]
    [Parity("50005")]
    public async Task ModifyMessageAsync_MessageOfAnotherUser_Returns403CannotEditAndChangesNothing()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var theirs = general.Message("theirs", guild.Member("alice"));
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        var exception = await Assert.ThrowsAsync<RestException>(
            () => rest.ModifyMessageAsync(general.Id, theirs, o => o.Content = "mine now", cancellationToken: Ct));

        AssertError(exception, HttpStatusCode.Forbidden, 50005);
        var read = await rest.GetMessageAsync(general.Id, theirs, cancellationToken: Ct);
        Assert.Equal("theirs", read.Content);
        Assert.Null(read.EditedAt);
        Assert.Empty(Drain(subscription));
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}/messages/{message_id}")]
    public async Task ModifyMessageAsync_OnlyFlagsOfAnotherUsersMessage_IsAllowed()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var theirs = general.Message("theirs", guild.Member("alice"));
        using var rest = discord.CreateRestClient();

        var edited = await rest.ModifyMessageAsync(general.Id, theirs, o => o.Flags = MessageFlags.SuppressEmbeds, cancellationToken: Ct);

        Assert.Equal(MessageFlags.SuppressEmbeds, edited.Flags);
        Assert.Equal("theirs", edited.Content);
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}/messages/{message_id}")]
    [Parity("10008")]
    public async Task ModifyMessageAsync_UnknownMessage_Returns404UnknownMessage()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.ModifyMessageAsync(general.Id, 424242, o => o.Content = "x", cancellationToken: Ct));

        AssertError(exception, HttpStatusCode.NotFound, 10008);
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}/messages/{message_id}")]
    [Parity("10003")]
    [Parity("50001")]
    public async Task ModifyMessageAsync_UnknownChannelOrGuildTheBotIsNotIn_Returns10003Or50001()
    {
        await using var discord = FakeDiscord.Create();
        var secret = discord.Seed.Guild("Elsewhere", withBot: false).TextChannel("secret");
        var hidden = secret.Message("hidden", discord.Seed.User("alice"));
        using var rest = discord.CreateRestClient();

        var unknown = await Assert.ThrowsAsync<RestException>(() => rest.ModifyMessageAsync(123456789, 1, o => o.Content = "x", cancellationToken: Ct));
        var forbidden = await Assert.ThrowsAsync<RestException>(() => rest.ModifyMessageAsync(secret.Id, hidden, o => o.Content = "x", cancellationToken: Ct));

        AssertError(unknown, HttpStatusCode.NotFound, 10003);
        AssertError(forbidden, HttpStatusCode.Forbidden, 50001);
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}/messages/{message_id}")]
    [Parity("50006")]
    public async Task ModifyMessageAsync_EmptyingAMessageWithNothingElse_Returns50006AndKeepsTheContent()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();
        var sent = await rest.SendMessageAsync(general.Id, "text", cancellationToken: Ct);

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.ModifyMessageAsync(general.Id, sent.Id, o => o.Content = string.Empty, cancellationToken: Ct));

        AssertError(exception, HttpStatusCode.BadRequest, 50006);
        Assert.Equal("text", (await rest.GetMessageAsync(general.Id, sent.Id, cancellationToken: Ct)).Content);
    }

    [Theory]
    [Parity("PATCH /channels/{channel_id}/messages/{message_id}")]
    [Parity("50035")]
    [InlineData("""{"content":5}""", "content", "BASE_TYPE_BAD_TYPE")]
    [InlineData("""{"embeds":{}}""", "embeds", "BASE_TYPE_BAD_TYPE")]
    [InlineData("""{"flags":"x"}""", "flags", "NUMBER_TYPE_COERCE")]
    [InlineData("""{"allowed_mentions":[]}""", "allowed_mentions", "MODEL_TYPE_CONVERT")]
    [InlineData("""{"attachments":[{}]}""", "attachments.0.id", "BASE_TYPE_REQUIRED")]
    [InlineData("""{"embeds":[{"title":null,"fields":[{"name":"n","value":"v","inline":"x"}]}]}""", "embeds.0.fields.0.inline", "BOOLEAN_TYPE_COERCE")]
    public async Task SendRequest_PatchWithAnInvalidField_Returns50035AtTheFieldPath(string body, string path, string code)
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();
        var sent = await rest.SendMessageAsync(general.Id, "text", cancellationToken: Ct);

        var exception = await SendRawFailingAsync(rest, HttpMethod.Patch, $"/channels/{general.Id}/messages/{sent.Id}", body);

        Assert.Equal(code, FieldErrors(exception, path.Split('.')).First().Code);
        Assert.Equal("text", (await rest.GetMessageAsync(general.Id, sent.Id, cancellationToken: Ct)).Content);
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}/messages/{message_id}")]
    [Parity("50035")]
    public async Task ModifyMessageAsync_ContentOver2000_Returns50035MaxLengthOnContent()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();
        var sent = await rest.SendMessageAsync(general.Id, "text", cancellationToken: Ct);

        var exception = await Assert.ThrowsAsync<RestException>(
            () => rest.ModifyMessageAsync(general.Id, sent.Id, o => o.Content = new string('x', 2001), cancellationToken: Ct));

        Assert.Equal("BASE_TYPE_MAX_LENGTH", Assert.Single(FieldErrors(exception, "content")).Code);
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}/messages/{message_id}")]
    [Parity("50035")]
    public async Task SendRequest_PatchWithMalformedIds_Returns50035OnEachRouteParameter()
    {
        await using var discord = FakeDiscord.Create();
        using var rest = discord.CreateRestClient();

        var exception = await SendRawFailingAsync(rest, HttpMethod.Patch, $"/channels/{"abc"}/messages/{"def"}", """{"content":"x"}""");

        Assert.Equal("NUMBER_TYPE_COERCE", Assert.Single(FieldErrors(exception, "channel_id")).Code);
        Assert.Equal("NUMBER_TYPE_COERCE", Assert.Single(FieldErrors(exception, "message_id")).Code);
    }
}
