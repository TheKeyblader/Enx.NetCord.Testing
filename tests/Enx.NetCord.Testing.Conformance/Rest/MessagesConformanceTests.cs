using System.Net;
using Enx.NetCord.Testing.Conformance.Contract;
using NetCord;
using NetCord.Rest;
using Xunit;

namespace Enx.NetCord.Testing.Conformance.Rest;

/// <summary>Message routes driven through the real NetCord <see cref="RestClient"/>, with every response checked against the OpenAPI contract.</summary>
[Trait("Category", "Conformance")]
public sealed class MessagesConformanceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    [Parity("POST /channels/{channel_id}/messages")]
    public async Task SendMessageAsync_Content_ReturnsTheMessageAndConformsToMessageResponse()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();

        var message = await rest.SendMessageAsync(general.Id, "hello", cancellationToken: Ct);

        Assert.Equal("hello", message.Content);
        Assert.Equal(general.Id, message.ChannelId);
        Assert.Equal(discord.ApplicationId, message.Author.Id);
        ContractAssert.Conforms(discord, "POST /channels/{channel_id}/messages");
    }

    [Fact]
    [Parity("POST /channels/{channel_id}/messages")]
    public async Task SendMessageAsync_EmbedAndReply_ConformsToMessageResponse()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();
        var first = await rest.SendMessageAsync(general.Id, "first", cancellationToken: Ct);

        var reply = await rest.SendMessageAsync(
            general.Id,
            new MessageProperties
            {
                Content = "second",
                Embeds = [new EmbedProperties { Title = "Title", Description = "Body", Color = new Color(0x112233) }],
                MessageReference = MessageReferenceProperties.Reply(first.Id),
            },
            cancellationToken: Ct);

        Assert.Single(reply.Embeds);
        Assert.Equal(first.Id, reply.MessageReference?.MessageId);
        ContractAssert.Conforms(discord, "POST /channels/{channel_id}/messages");
    }

    [Fact]
    [Parity("POST /channels/{channel_id}/messages")]
    [Parity("10003")]
    public async Task SendMessageAsync_UnknownChannel_Returns10003WithTheDiscordErrorEnvelope()
    {
        await using var discord = FakeDiscord.Create();
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.SendMessageAsync(123456789, "hello", cancellationToken: Ct));

        Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
        Assert.Equal(10003, exception.Error!.Code);
        ContractAssert.Conforms(discord, "POST /channels/{channel_id}/messages");
    }

    [Fact]
    [Parity("POST /channels/{channel_id}/messages")]
    [Parity("50006")]
    public async Task SendMessageAsync_EmptyMessage_Returns50006WithTheDiscordErrorEnvelope()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.SendMessageAsync(general.Id, new MessageProperties(), cancellationToken: Ct));

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Equal(50006, exception.Error!.Code);
        ContractAssert.Conforms(discord, "POST /channels/{channel_id}/messages");
    }

    [Fact]
    [Parity("GET /channels/{channel_id}/messages/{message_id}")]
    public async Task GetMessageAsync_SentMessage_ReturnsItAndConformsToMessageResponse()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();
        var sent = await rest.SendMessageAsync(general.Id, "hello", cancellationToken: Ct);

        var read = await rest.GetMessageAsync(general.Id, sent.Id, cancellationToken: Ct);

        Assert.Equal(sent.Id, read.Id);
        Assert.Equal("hello", read.Content);
        ContractAssert.Conforms(discord, "GET /channels/{channel_id}/messages/{message_id}");
    }

    [Fact]
    [Parity("GET /channels/{channel_id}/messages/{message_id}")]
    [Parity("10008")]
    public async Task GetMessageAsync_UnknownMessage_Returns10008WithTheDiscordErrorEnvelope()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.GetMessageAsync(general.Id, 123456789, cancellationToken: Ct));

        Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
        Assert.Equal(10008, exception.Error!.Code);
        ContractAssert.Conforms(discord, "GET /channels/{channel_id}/messages/{message_id}");
    }

    [Fact]
    [Parity("GET /channels/{channel_id}/messages")]
    public async Task GetMessagesAsync_SeededMessages_ReturnsThemNewestFirstAndConformsToMessageResponseArray()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var alice = guild.Member("alice");
        var first = general.Message("m1", alice);
        var second = general.Message("m2", alice);
        using var rest = discord.CreateRestClient();

        var messages = await rest.GetMessagesAsync(general.Id).ToListAsync(Ct);

        Assert.Equal([second, first], messages.Select(m => m.Id));
        Assert.All(messages, m => Assert.Equal(alice, m.Author.Id));
        ContractAssert.Conforms(discord, "GET /channels/{channel_id}/messages");
    }

    [Fact]
    [Parity("GET /channels/{channel_id}/messages")]
    public async Task GetMessagesAsync_EmptyChannel_ReturnsNothingAndConformsToMessageResponseArray()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();

        var messages = await rest.GetMessagesAsync(general.Id).ToListAsync(Ct);

        Assert.Empty(messages);
        ContractAssert.Conforms(discord, "GET /channels/{channel_id}/messages");
    }

    [Fact]
    [Parity("GET /channels/{channel_id}/messages")]
    [Parity("10003")]
    public async Task GetMessagesAsync_UnknownChannel_Returns10003WithTheDiscordErrorEnvelope()
    {
        await using var discord = FakeDiscord.Create();
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(async () => await rest.GetMessagesAsync(123456789).ToListAsync(Ct));

        Assert.Equal(10003, exception.Error!.Code);
        ContractAssert.Conforms(discord, "GET /channels/{channel_id}/messages");
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}/messages/{message_id}")]
    public async Task ModifyMessageAsync_OwnMessage_ReturnsTheEditedMessageAndConformsToMessageResponse()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();
        var sent = await rest.SendMessageAsync(general.Id, "v1", cancellationToken: Ct);

        var edited = await rest.ModifyMessageAsync(general.Id, sent.Id, o => o.Content = "v2", cancellationToken: Ct);

        Assert.Equal("v2", edited.Content);
        Assert.NotNull(edited.EditedAt);
        ContractAssert.Conforms(discord, "PATCH /channels/{channel_id}/messages/{message_id}");
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}/messages/{message_id}")]
    [Parity("10008")]
    public async Task ModifyMessageAsync_UnknownMessage_Returns10008WithTheDiscordErrorEnvelope()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(
            () => rest.ModifyMessageAsync(general.Id, 123456789, o => o.Content = "v2", cancellationToken: Ct));

        Assert.Equal(10008, exception.Error!.Code);
        ContractAssert.Conforms(discord, "PATCH /channels/{channel_id}/messages/{message_id}");
    }

    [Fact]
    [Parity("DELETE /channels/{channel_id}/messages/{message_id}")]
    public async Task DeleteMessageAsync_OwnMessage_Returns204WithoutABody()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();
        var sent = await rest.SendMessageAsync(general.Id, "bye", cancellationToken: Ct);

        await rest.DeleteMessageAsync(general.Id, sent.Id, cancellationToken: Ct);

        Assert.Empty(await rest.GetMessagesAsync(general.Id).ToListAsync(Ct));
        ContractAssert.Conforms(discord, "DELETE /channels/{channel_id}/messages/{message_id}");
    }

    [Fact]
    [Parity("DELETE /channels/{channel_id}/messages/{message_id}")]
    [Parity("10008")]
    public async Task DeleteMessageAsync_UnknownMessage_Returns10008WithTheDiscordErrorEnvelope()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.DeleteMessageAsync(general.Id, 123456789, cancellationToken: Ct));

        Assert.Equal(10008, exception.Error!.Code);
        ContractAssert.Conforms(discord, "DELETE /channels/{channel_id}/messages/{message_id}");
    }

    [Fact]
    [Parity("POST /channels/{channel_id}/messages/bulk-delete")]
    public async Task DeleteMessagesAsync_SeveralMessages_Returns204WithoutABody()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var alice = guild.Member("alice");
        var a = general.Message("a", alice);
        var b = general.Message("b", alice);
        using var rest = discord.CreateRestClient();

        await rest.DeleteMessagesAsync(general.Id, [a, b], cancellationToken: Ct);

        Assert.Empty(await rest.GetMessagesAsync(general.Id).ToListAsync(Ct));
        ContractAssert.Conforms(discord, "POST /channels/{channel_id}/messages/bulk-delete");
    }

    [Fact]
    [Parity("POST /channels/{channel_id}/messages/bulk-delete")]
    [Parity("10003")]
    public async Task DeleteMessagesAsync_UnknownChannel_Returns10003WithTheDiscordErrorEnvelope()
    {
        await using var discord = FakeDiscord.Create();
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.DeleteMessagesAsync(123456789, [1, 2], cancellationToken: Ct));

        Assert.Equal(10003, exception.Error!.Code);
        ContractAssert.Conforms(discord, "POST /channels/{channel_id}/messages/bulk-delete");
    }

    [Fact]
    [Parity("POST /channels/{channel_id}/messages/{message_id}/crosspost")]
    [Parity("50024")]
    public async Task CrosspostMessageAsync_TextChannel_Returns50024WithTheDiscordErrorEnvelope()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();
        var sent = await rest.SendMessageAsync(general.Id, "news", cancellationToken: Ct);

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.CrosspostMessageAsync(general.Id, sent.Id, cancellationToken: Ct));

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Equal(50024, exception.Error!.Code);
        ContractAssert.Conforms(discord, "POST /channels/{channel_id}/messages/{message_id}/crosspost");
    }

    [Fact]
    [Parity("POST /channels/{channel_id}/messages/{message_id}/crosspost")]
    public async Task CrosspostMessageAsync_AnnouncementChannel_ReturnsTheMessageAndConformsToMessageResponse()
    {
        await using var discord = FakeDiscord.Create();
        var news = discord.Seed.Guild("Test").AnnouncementChannel("news");
        using var rest = discord.CreateRestClient();
        var sent = await rest.SendMessageAsync(news.Id, "breaking", cancellationToken: Ct);

        var crossposted = await rest.CrosspostMessageAsync(news.Id, sent.Id, cancellationToken: Ct);

        Assert.Equal(sent.Id, crossposted.Id);
        Assert.Equal(news.Id, crossposted.ChannelId);
        Assert.True(crossposted.Flags.HasFlag(MessageFlags.Crossposted));
        ContractAssert.Conforms(discord, "POST /channels/{channel_id}/messages/{message_id}/crosspost");
    }
}
