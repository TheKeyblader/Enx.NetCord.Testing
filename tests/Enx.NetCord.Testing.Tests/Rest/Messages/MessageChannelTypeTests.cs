using System.Net;
using Enx.NetCord.Testing.State;
using NetCord;
using NetCord.Rest;
using Xunit;
using static Enx.NetCord.Testing.Tests.Rest.Messages.MessagesTestSupport;
using static Enx.NetCord.Testing.Tests.State.StateTestSupport;

namespace Enx.NetCord.Testing.Tests.Rest.Messages;

/// <summary>What the message routes do per channel type (ADR 0013): chats, categories, forum and media channels, second crosspost.</summary>
public sealed class MessageChannelTypeTests
{
    private static ulong CreateMedia(FakeDiscord discord, ulong guildId)
        => discord.State.CreateGuildChannel(StateCaller.Fixture, new CreateGuildChannelInput(guildId, ChannelKind.GuildMedia, "gallery")).Value.Id;

    [Theory]
    [InlineData("voice")]
    [InlineData("stage")]
    [Parity("POST /channels/{channel_id}/messages")]
    [Parity("GET /channels/{channel_id}/messages")]
    [Parity("GET /channels/{channel_id}/messages/{message_id}")]
    public async Task SendMessageAsync_VoiceOrStageChat_CreatesAndReadsBackTheMessage(string kind)
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var channel = kind == "voice" ? guild.VoiceChannel("lounge") : guild.StageChannel("stage");
        using var rest = discord.CreateRestClient();

        var sent = await rest.SendMessageAsync(channel.Id, "hello", cancellationToken: Ct);

        Assert.Equal("hello", (await rest.GetMessageAsync(channel.Id, sent.Id, cancellationToken: Ct)).Content);
        Assert.Equal(sent.Id, Assert.Single(await rest.GetMessagesAsync(channel.Id).ToListAsync(Ct)).Id);
        Assert.Equal(sent.Id, discord.State.FindChannel(channel.Id)?.LastMessageId);
    }

    [Fact]
    [Parity("GET /channels/{channel_id}/messages")]
    [Parity("PATCH /channels/{channel_id}/messages/{message_id}")]
    [Parity("DELETE /channels/{channel_id}/messages/{message_id}")]
    [Parity("POST /channels/{channel_id}/messages/bulk-delete")]
    public async Task SeededVoiceChannelMessage_Seeded_IsListedEditedAndDeletedByTheBot()
    {
        await using var discord = FakeDiscord.Create();
        var lounge = discord.Seed.Guild("Test").VoiceChannel("lounge");
        var alice = discord.Seed.User("alice");
        var first = lounge.Message("one", alice);
        var second = lounge.Message("two", alice);
        var third = lounge.Message("three", discord.ApplicationId);
        using var rest = discord.CreateRestClient();

        var listed = await rest.GetMessagesAsync(lounge.Id).ToListAsync(Ct);
        var edited = await rest.ModifyMessageAsync(lounge.Id, third, m => m.Content = "3", cancellationToken: Ct);
        await rest.DeleteMessageAsync(lounge.Id, first, cancellationToken: Ct);
        await rest.DeleteMessagesAsync(lounge.Id, [second, third], cancellationToken: Ct);

        Assert.Equal([third, second, first], listed.Select(m => m.Id));
        Assert.Equal("3", edited.Content);
        Assert.Empty(await rest.GetMessagesAsync(lounge.Id).ToListAsync(Ct));
    }

    [Fact]
    [Parity("POST /channels/{channel_id}/messages")]
    public async Task Message_VoiceAndStageChannelsOfTheSeed_AreAssignedAndTimestampedLikeTheTextOnes()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var voice = guild.VoiceChannel("lounge");
        var stage = guild.StageChannel("stage");
        var alice = discord.Seed.User("alice");
        var explicitId = IdAt(Start - TimeSpan.FromDays(30), 5);

        var a = voice.Message("a", alice);
        var b = stage.Message("b", alice, explicitId);

        Assert.Equal(explicitId, b);
        Assert.Equal("a", discord.State.FindMessage(voice.Id, a)?.Content);
        Assert.Equal(stage.Id, discord.State.FindMessage(stage.Id, b)?.ChannelId);
        Assert.Throws<InvalidOperationException>(() => voice.Message("again", alice, a));
        Assert.Throws<ArgumentOutOfRangeException>(() => voice.Message("zero", alice, 0));
        Assert.Throws<ArgumentNullException>(() => voice.Message(null!, alice));
        Assert.Throws<InvalidOperationException>(() => voice.Message("ghost", 424242));
    }

    [Fact]
    [Parity("POST /channels/{channel_id}/messages")]
    [Parity("50008")]
    public async Task SendMessageAsync_Category_Returns400CannotSendMessagesInANonTextChannelAndCommitsNothing()
    {
        await using var discord = FakeDiscord.Create();
        var lobby = discord.Seed.Guild("Test").Category("Lobby");
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();
        Drain(subscription);

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.SendMessageAsync(lobby.Id, "hello", cancellationToken: Ct));

        AssertError(exception, HttpStatusCode.BadRequest, 50008);
        Assert.Empty(Drain(subscription));
        Assert.Null(discord.State.FindChannel(lobby.Id)?.LastMessageId);
        discord.Recorded.ShouldHaveRequestedOnce($"POST /channels/{lobby.Id}/messages");
    }

    [Fact]
    [Parity("POST /channels/{channel_id}/messages")]
    [Parity("40058")]
    public async Task SendMessageAsync_Forum_Returns400CannotSendAMessageInAForumChannelAndCommitsNothing()
    {
        await using var discord = FakeDiscord.Create();
        var ideas = discord.Seed.Guild("Test").ForumChannel("ideas");
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();
        Drain(subscription);

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.SendMessageAsync(ideas.Id, "hello", cancellationToken: Ct));

        AssertError(exception, HttpStatusCode.BadRequest, 40058);
        Assert.Empty(Drain(subscription));
        Assert.Null(discord.State.FindChannel(ideas.Id)?.LastMessageId);
    }

    [Fact]
    [Parity("POST /channels/{channel_id}/messages")]
    [Parity("40058")]
    public async Task SendMessageAsync_Media_Returns400CannotSendAMessageInAForumChannel()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var gallery = CreateMedia(discord, guild.Id);
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.SendMessageAsync(gallery, "hello", cancellationToken: Ct));

        AssertError(exception, HttpStatusCode.BadRequest, 40058);
    }

    [Fact]
    [Parity("POST /channels/{channel_id}/messages")]
    [Parity("50008")]
    [Parity("10003")]
    [Parity("50001")]
    public async Task SendMessageAsync_ChannelChecksPrecedeTheTypeCheck_ReportsUnknownChannelOrMissingAccess()
    {
        await using var discord = FakeDiscord.Create();
        var hidden = discord.Seed.Guild("Elsewhere", withBot: false).Category("Secret");
        using var rest = discord.CreateRestClient();

        var unknown = await Assert.ThrowsAsync<RestException>(() => rest.SendMessageAsync(123456789, "x", cancellationToken: Ct));
        var forbidden = await Assert.ThrowsAsync<RestException>(() => rest.SendMessageAsync(hidden.Id, "x", cancellationToken: Ct));

        AssertError(unknown, HttpStatusCode.NotFound, 10003);
        AssertError(forbidden, HttpStatusCode.Forbidden, 50001);
    }

    [Fact]
    [Parity("POST /channels/{channel_id}/messages")]
    [Parity("50035")]
    public async Task SendMessageAsync_InvalidBodyToACategory_Returns50035BeforeTheTypeCheck()
    {
        await using var discord = FakeDiscord.Create();
        var lobby = discord.Seed.Guild("Test").Category("Lobby");
        using var rest = discord.CreateRestClient();

        var exception = await SendRawFailingAsync(rest, HttpMethod.Post, $"/channels/{lobby.Id}/messages", """{"content":5}""");

        Assert.Equal(50035, exception.Error!.Code);
    }

    private static ulong NonMessageableChannel(FakeDiscord discord, string kind)
    {
        var guild = discord.Seed.Guild("Test");
        return kind switch
        {
            "category" => guild.Category("Lobby").Id,
            "forum" => guild.ForumChannel("ideas").Id,
            "media" => CreateMedia(discord, guild.Id),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    [Theory]
    [InlineData("category")]
    [InlineData("forum")]
    [InlineData("media")]
    [Parity("GET /channels/{channel_id}/messages")]
    [Parity("50024")]
    public async Task GetMessagesAsync_CategoryForumOrMedia_Returns400CannotExecuteOnThisChannelType(string kind)
    {
        await using var discord = FakeDiscord.Create();
        var channelId = NonMessageableChannel(discord, kind);
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(async () => await rest.GetMessagesAsync(channelId).ToListAsync(Ct));

        AssertError(exception, HttpStatusCode.BadRequest, 50024);
    }

    [Theory]
    [InlineData("category")]
    [InlineData("forum")]
    [InlineData("media")]
    [Parity("POST /channels/{channel_id}/messages/bulk-delete")]
    [Parity("50024")]
    public async Task DeleteMessagesAsync_CategoryForumOrMedia_Returns400CannotExecuteOnThisChannelType(string kind)
    {
        await using var discord = FakeDiscord.Create();
        var channelId = NonMessageableChannel(discord, kind);
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.DeleteMessagesAsync(channelId, [IdAt(Start, 1), IdAt(Start, 2)], cancellationToken: Ct));

        AssertError(exception, HttpStatusCode.BadRequest, 50024);
    }

    [Theory]
    [InlineData("category")]
    [InlineData("forum")]
    [InlineData("media")]
    [Parity("GET /channels/{channel_id}/messages/{message_id}")]
    [Parity("PATCH /channels/{channel_id}/messages/{message_id}")]
    [Parity("DELETE /channels/{channel_id}/messages/{message_id}")]
    [Parity("10008")]
    public async Task MessageRoutes_ForAMessageThatCannotExistInThisChannelType_Return404UnknownMessage(string kind)
    {
        await using var discord = FakeDiscord.Create();
        var channelId = NonMessageableChannel(discord, kind);
        using var rest = discord.CreateRestClient();

        var get = await Assert.ThrowsAsync<RestException>(() => rest.GetMessageAsync(channelId, 424242, cancellationToken: Ct));
        var edit = await Assert.ThrowsAsync<RestException>(() => rest.ModifyMessageAsync(channelId, 424242, m => m.Content = "x", cancellationToken: Ct));
        var delete = await Assert.ThrowsAsync<RestException>(() => rest.DeleteMessageAsync(channelId, 424242, cancellationToken: Ct));

        AssertError(get, HttpStatusCode.NotFound, 10008);
        AssertError(edit, HttpStatusCode.NotFound, 10008);
        AssertError(delete, HttpStatusCode.NotFound, 10008);
    }

    [Fact]
    [Parity("POST /channels/{channel_id}/messages/{message_id}/crosspost")]
    [Parity("40033")]
    public async Task CrosspostMessageAsync_SecondCrosspostOfTheSameMessage_Returns400AlreadyCrosspostedAndCommitsNothing()
    {
        await using var discord = FakeDiscord.Create();
        var news = discord.Seed.Guild("Test").AnnouncementChannel("news");
        var messageId = news.Message("release", discord.Seed.User("alice"));
        var other = news.Message("other", discord.Seed.User("bob"));
        using var rest = discord.CreateRestClient();
        await rest.CrosspostMessageAsync(news.Id, messageId, cancellationToken: Ct);
        using var subscription = discord.State.SubscribeGateway();
        Drain(subscription);

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.CrosspostMessageAsync(news.Id, messageId, cancellationToken: Ct));
        var otherMessage = await rest.CrosspostMessageAsync(news.Id, other, cancellationToken: Ct);

        AssertError(exception, HttpStatusCode.BadRequest, 40033);
        Assert.True(otherMessage.Flags.HasFlag(MessageFlags.Crossposted));
        var change = Assert.IsType<MessageEdited>(Assert.Single(Drain(subscription)));
        Assert.Equal(other, change.Message.Id);
        Assert.True((await rest.GetMessageAsync(news.Id, messageId, cancellationToken: Ct)).Flags.HasFlag(MessageFlags.Crossposted));
    }

    [Theory]
    [InlineData("category")]
    [InlineData("forum")]
    [InlineData("media")]
    [Parity("POST /channels/{channel_id}/messages/{message_id}/crosspost")]
    [Parity("10008")]
    public async Task CrosspostMessageAsync_ChannelTypeWithoutMessages_Returns404UnknownMessage(string kind)
    {
        await using var discord = FakeDiscord.Create();
        var channelId = NonMessageableChannel(discord, kind);
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.CrosspostMessageAsync(channelId, 424242, cancellationToken: Ct));

        AssertError(exception, HttpStatusCode.NotFound, 10008);
    }
}
