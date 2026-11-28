using System.Net;
using Enx.NetCord.Testing.State;
using NetCord;
using NetCord.Rest;
using Xunit;
using static Enx.NetCord.Testing.Tests.Rest.Messages.MessagesTestSupport;
using static Enx.NetCord.Testing.Tests.State.StateTestSupport;

namespace Enx.NetCord.Testing.Tests.Rest.Messages;

public sealed class DeleteMessageTests
{
    private static readonly DateTimeOffset Now = VirtualTimeProvider.DefaultNow;

    [Fact]
    [Parity("DELETE /channels/{channel_id}/messages/{message_id}")]
    public async Task DeleteMessageAsync_OwnMessage_RemovesItAndPublishesMessageDeleted()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        using var rest = discord.CreateRestClient();
        var sent = await rest.SendMessageAsync(general.Id, "bye", cancellationToken: Ct);
        using var subscription = discord.State.SubscribeGateway();

        await rest.DeleteMessageAsync(general.Id, sent.Id, cancellationToken: Ct);

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.GetMessageAsync(general.Id, sent.Id, cancellationToken: Ct));
        AssertError(exception, HttpStatusCode.NotFound, 10008);
        Assert.Empty(await rest.GetMessagesAsync(general.Id).ToListAsync(Ct));
        var deleted = Assert.IsType<MessageDeleted>(Assert.Single(Drain(subscription)));
        Assert.Equal((guild.Id, general.Id, sent.Id), (deleted.GuildId, deleted.ChannelId, deleted.MessageId));
        discord.Recorded.ShouldHaveRequested($"DELETE /channels/{general.Id}/messages/{sent.Id}");
        Assert.Equal(HttpStatusCode.NoContent, discord.Recorded.Requests.Single(r => r.Method == "DELETE").StatusCode);
    }

    [Fact]
    [Parity("DELETE /channels/{channel_id}/messages/{message_id}")]
    public async Task DeleteMessageAsync_MessageOfAnotherUser_IsAllowedBecausePermissionsAreNotModeled()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var theirs = general.Message("theirs", guild.Member("alice"));
        using var rest = discord.CreateRestClient();

        await rest.DeleteMessageAsync(general.Id, theirs, cancellationToken: Ct);

        Assert.Null(discord.State.FindMessage(general.Id, theirs));
    }

    [Fact]
    [Parity("DELETE /channels/{channel_id}/messages/{message_id}")]
    [Parity("50003")]
    public async Task DeleteMessageAsync_DirectMessageOfTheOtherParty_Returns403CannotExecuteOnDmAndKeepsIt()
    {
        await using var discord = FakeDiscord.Create();
        var aliceId = discord.Seed.User("alice");
        using var rest = discord.CreateRestClient();
        var dm = await rest.GetDMChannelAsync(aliceId, cancellationToken: Ct);
        var theirs = discord.State.CreateMessage(StateCaller.Fixture, new CreateMessageInput(dm.Id, aliceId, "from alice")).Value;
        var mine = await rest.SendMessageAsync(dm.Id, "from the bot", cancellationToken: Ct);
        using var subscription = discord.State.SubscribeGateway();

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.DeleteMessageAsync(dm.Id, theirs.Id, cancellationToken: Ct));
        await rest.DeleteMessageAsync(dm.Id, mine.Id, cancellationToken: Ct);

        AssertError(exception, HttpStatusCode.Forbidden, 50003);
        Assert.NotNull(discord.State.FindMessage(dm.Id, theirs.Id));
        Assert.Null(discord.State.FindMessage(dm.Id, mine.Id));
        Assert.Equal(mine.Id, Assert.IsType<MessageDeleted>(Assert.Single(Drain(subscription))).MessageId);
    }

    [Fact]
    [Parity("DELETE /channels/{channel_id}/messages/{message_id}")]
    [Parity("10008")]
    public async Task DeleteMessageAsync_UnknownOrAlreadyDeleted_Returns404UnknownMessage()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();
        var sent = await rest.SendMessageAsync(general.Id, "x", cancellationToken: Ct);
        await rest.DeleteMessageAsync(general.Id, sent.Id, cancellationToken: Ct);
        using var subscription = discord.State.SubscribeGateway();

        var again = await Assert.ThrowsAsync<RestException>(() => rest.DeleteMessageAsync(general.Id, sent.Id, cancellationToken: Ct));

        AssertError(again, HttpStatusCode.NotFound, 10008);
        Assert.Empty(Drain(subscription));
    }

    [Fact]
    [Parity("DELETE /channels/{channel_id}/messages/{message_id}")]
    [Parity("10003")]
    [Parity("50001")]
    public async Task DeleteMessageAsync_UnknownChannelOrGuildTheBotIsNotIn_Returns10003Or50001AndDeletesNothing()
    {
        await using var discord = FakeDiscord.Create();
        var secret = discord.Seed.Guild("Elsewhere", withBot: false).TextChannel("secret");
        var hidden = secret.Message("hidden", discord.Seed.User("alice"));
        using var rest = discord.CreateRestClient();

        var unknown = await Assert.ThrowsAsync<RestException>(() => rest.DeleteMessageAsync(123456789, 1, cancellationToken: Ct));
        var forbidden = await Assert.ThrowsAsync<RestException>(() => rest.DeleteMessageAsync(secret.Id, hidden, cancellationToken: Ct));

        AssertError(unknown, HttpStatusCode.NotFound, 10003);
        AssertError(forbidden, HttpStatusCode.Forbidden, 50001);
        Assert.NotNull(discord.State.FindMessage(secret.Id, hidden));
    }

    [Fact]
    [Parity("DELETE /channels/{channel_id}/messages/{message_id}")]
    [Parity("50035")]
    public async Task SendRequest_DeleteWithAMalformedMessageId_Returns50035()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(async () =>
        {
            await using var stream = await rest.SendRequestAsync(HttpMethod.Delete, $"/channels/{general.Id}/messages/{"abc"}", cancellationToken: Ct);
        });

        Assert.Equal("NUMBER_TYPE_COERCE", Assert.Single(FieldErrors(exception, "message_id")).Code);
    }

    [Fact]
    [Parity("POST /channels/{channel_id}/messages/bulk-delete")]
    public async Task DeleteMessagesAsync_SeveralMessages_RemovesThemAndPublishesOneBulkChange()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var author = guild.Member("alice");
        var ids = Enumerable.Range(0, 4).Select(i => general.Message($"m{i}", author)).ToArray();
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        await rest.DeleteMessagesAsync(general.Id, [ids[0], ids[2], ids[3]], cancellationToken: Ct);

        Assert.Equal([ids[1]], (await rest.GetMessagesAsync(general.Id).ToListAsync(Ct)).Select(m => m.Id));
        var change = Assert.IsType<MessagesBulkDeleted>(Assert.Single(Drain(subscription)));
        Assert.Equal((guild.Id, general.Id), (change.GuildId, change.ChannelId));
        Assert.Equal([ids[0], ids[2], ids[3]], change.MessageIds.AsEnumerable());
        discord.Recorded.ShouldHaveRequestedOnce($"POST /channels/{general.Id}/messages/bulk-delete");
        Assert.Equal(HttpStatusCode.NoContent, discord.Recorded.Requests.Single(r => r.Method == "POST").StatusCode);
    }

    [Fact]
    [Parity("POST /channels/{channel_id}/messages/bulk-delete")]
    public async Task SendRequest_BulkDeleteWithUnknownIds_IgnoresThemButStillCountsThem()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var author = guild.Member("alice");
        var real = general.Message("real", author);
        var otherChannel = guild.TextChannel("other");
        var elsewhere = otherChannel.Message("elsewhere", author);
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        var unknown = IdAt(Now, 999);
        using var content = Json($$"""{"messages":["{{real}}","{{unknown}}","{{elsewhere}}"]}""");
        await using var stream = await rest.SendRequestAsync(HttpMethod.Post, content, $"/channels/{general.Id}/messages/bulk-delete", cancellationToken: Ct);

        Assert.Null(discord.State.FindMessage(general.Id, real));
        Assert.NotNull(discord.State.FindMessage(otherChannel.Id, elsewhere));
        var change = Assert.IsType<MessagesBulkDeleted>(Assert.Single(Drain(subscription)));
        Assert.Equal([real], change.MessageIds.AsEnumerable());
    }

    [Fact]
    [Parity("POST /channels/{channel_id}/messages/bulk-delete")]
    [Parity("50034")]
    public async Task DeleteMessagesAsync_AMessageOlderThanTwoWeeks_Returns50034AndDeletesNothing()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var author = guild.Member("alice");
        var recent = general.Message("recent", author);
        var old = general.Message("old", author, IdAt(Now - TimeSpan.FromDays(15)));
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.DeleteMessagesAsync(general.Id, [recent, old], cancellationToken: Ct));

        AssertError(exception, HttpStatusCode.BadRequest, 50034);
        Assert.NotNull(discord.State.FindMessage(general.Id, recent));
        Assert.NotNull(discord.State.FindMessage(general.Id, old));
        Assert.Empty(Drain(subscription));
    }

    [Fact]
    [Parity("POST /channels/{channel_id}/messages/bulk-delete")]
    [Parity("50034")]
    public async Task DeleteMessagesAsync_AnOldIdThatNamesNoMessage_StillReturns50034()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var recent = general.Message("recent", guild.Member("alice"));
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(
            () => rest.DeleteMessagesAsync(general.Id, [recent, IdAt(Now - TimeSpan.FromDays(30))], cancellationToken: Ct));

        AssertError(exception, HttpStatusCode.BadRequest, 50034);
    }

    [Fact]
    [Parity("POST /channels/{channel_id}/messages/bulk-delete")]
    public async Task DeleteMessagesAsync_AMessageJustUnderTwoWeeks_IsAccepted()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var author = guild.Member("alice");
        var a = general.Message("a", author, IdAt(Now - TimeSpan.FromDays(13), 1));
        var b = general.Message("b", author);
        using var rest = discord.CreateRestClient();

        await rest.DeleteMessagesAsync(general.Id, [a, b], cancellationToken: Ct);

        Assert.Empty(await rest.GetMessagesAsync(general.Id).ToListAsync(Ct));
    }

    [Theory]
    [Parity("POST /channels/{channel_id}/messages/bulk-delete")]
    [Parity("50035")]
    [InlineData("""{"messages":["1"]}""", "messages", "BASE_TYPE_BAD_LENGTH")]
    [InlineData("""{"messages":[]}""", "messages", "BASE_TYPE_BAD_LENGTH")]
    [InlineData("""{"messages":["1","1"]}""", "messages", "BASE_TYPE_ARRAY_UNIQUE")]
    [InlineData("""{"messages":["1","abc"]}""", "messages.1", "NUMBER_TYPE_COERCE")]
    [InlineData("""{"messages":"1"}""", "messages", "BASE_TYPE_BAD_TYPE")]
    [InlineData("""{}""", "messages", "BASE_TYPE_REQUIRED")]
    [InlineData("""{"messages":null}""", "messages", "BASE_TYPE_REQUIRED")]
    public async Task SendRequest_BulkDeleteWithInvalidMessages_Returns50035OnMessages(string body, string path, string code)
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();

        var exception = await SendRawFailingAsync(rest, HttpMethod.Post, $"/channels/{general.Id}/messages/bulk-delete", body);

        Assert.Equal(code, FieldErrors(exception, path.Split('.')).First().Code);
    }

    [Fact]
    [Parity("POST /channels/{channel_id}/messages/bulk-delete")]
    [Parity("50035")]
    public async Task SendRequest_BulkDeleteOfOneHundredAndOneMessages_Returns50035()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();
        var ids = string.Join(",", Enumerable.Range(1, 101).Select(i => $"\"{i}\""));

        var exception = await SendRawFailingAsync(rest, HttpMethod.Post, $"/channels/{general.Id}/messages/bulk-delete", $$"""{"messages":[{{ids}}]}""");

        Assert.Equal("BASE_TYPE_BAD_LENGTH", Assert.Single(FieldErrors(exception, "messages")).Code);
    }

    [Fact]
    [Parity("POST /channels/{channel_id}/messages/bulk-delete")]
    public async Task SendRequest_BulkDeleteOfOneHundredMessages_IsAccepted()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();
        var ids = string.Join(",", Enumerable.Range(1, 100).Select(i => IdAt(Now, (ulong)i)).Select(id => $"\"{id}\""));

        using var content = Json($$"""{"messages":[{{ids}}]}""");
        await using var stream = await rest.SendRequestAsync(HttpMethod.Post, content, $"/channels/{general.Id}/messages/bulk-delete", cancellationToken: Ct);

        Assert.Equal(HttpStatusCode.NoContent, discord.Recorded.Requests.Single().StatusCode);
    }

    [Fact]
    [Parity("POST /channels/{channel_id}/messages/bulk-delete")]
    [Parity("50003")]
    public async Task DeleteMessagesAsync_DirectMessageChannel_Returns403CannotExecuteOnDm()
    {
        await using var discord = FakeDiscord.Create();
        var aliceId = discord.Seed.User("alice");
        using var rest = discord.CreateRestClient();
        var dm = await rest.GetDMChannelAsync(aliceId, cancellationToken: Ct);
        var a = await rest.SendMessageAsync(dm.Id, "a", cancellationToken: Ct);
        var b = await rest.SendMessageAsync(dm.Id, "b", cancellationToken: Ct);

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.DeleteMessagesAsync(dm.Id, [a.Id, b.Id], cancellationToken: Ct));

        AssertError(exception, HttpStatusCode.Forbidden, 50003);
        Assert.Equal(2, (await rest.GetMessagesAsync(dm.Id).ToListAsync(Ct)).Count);
    }

    [Fact]
    [Parity("POST /channels/{channel_id}/messages/bulk-delete")]
    [Parity("10003")]
    [Parity("50001")]
    public async Task DeleteMessagesAsync_UnknownChannelOrGuildTheBotIsNotIn_Returns10003Or50001()
    {
        await using var discord = FakeDiscord.Create();
        var secret = discord.Seed.Guild("Elsewhere", withBot: false).TextChannel("secret");
        var author = discord.Seed.User("alice");
        var a = secret.Message("a", author);
        var b = secret.Message("b", author);
        using var rest = discord.CreateRestClient();

        var unknown = await Assert.ThrowsAsync<RestException>(() => rest.DeleteMessagesAsync(123456789, [1, 2], cancellationToken: Ct));
        var forbidden = await Assert.ThrowsAsync<RestException>(() => rest.DeleteMessagesAsync(secret.Id, [a, b], cancellationToken: Ct));

        AssertError(unknown, HttpStatusCode.NotFound, 10003);
        AssertError(forbidden, HttpStatusCode.Forbidden, 50001);
        Assert.NotNull(discord.State.FindMessage(secret.Id, a));
    }

    [Fact]
    [Parity("POST /channels/{channel_id}/messages/{message_id}/crosspost")]
    [Parity("50024")]
    public async Task CrosspostMessageAsync_InATextChannel_Returns400CannotExecuteOnThisChannelType()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();
        var sent = await rest.SendMessageAsync(general.Id, "news", cancellationToken: Ct);
        using var subscription = discord.State.SubscribeGateway();

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.CrosspostMessageAsync(general.Id, sent.Id, cancellationToken: Ct));

        AssertError(exception, HttpStatusCode.BadRequest, 50024);
        Assert.Empty(Drain(subscription));
        Assert.Equal((MessageFlags)0, (await rest.GetMessageAsync(general.Id, sent.Id, cancellationToken: Ct)).Flags);
    }

    [Fact]
    [Parity("POST /channels/{channel_id}/messages/{message_id}/crosspost")]
    [Parity("50003")]
    public async Task CrosspostMessageAsync_InADirectMessageChannel_Returns403CannotExecuteOnDm()
    {
        await using var discord = FakeDiscord.Create();
        var aliceId = discord.Seed.User("alice");
        using var rest = discord.CreateRestClient();
        var dm = await rest.GetDMChannelAsync(aliceId, cancellationToken: Ct);
        var sent = await rest.SendMessageAsync(dm.Id, "x", cancellationToken: Ct);

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.CrosspostMessageAsync(dm.Id, sent.Id, cancellationToken: Ct));

        AssertError(exception, HttpStatusCode.Forbidden, 50003);
    }

    [Fact]
    [Parity("POST /channels/{channel_id}/messages/{message_id}/crosspost")]
    [Parity("10003")]
    [Parity("10008")]
    [Parity("50001")]
    public async Task CrosspostMessageAsync_UnknownChannelMessageOrGuildTheBotIsNotIn_ReturnsTheMatchingError()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        var secret = discord.Seed.Guild("Elsewhere", withBot: false).TextChannel("secret");
        var hidden = secret.Message("hidden", discord.Seed.User("alice"));
        using var rest = discord.CreateRestClient();

        var unknownChannel = await Assert.ThrowsAsync<RestException>(() => rest.CrosspostMessageAsync(123456789, 1, cancellationToken: Ct));
        var unknownMessage = await Assert.ThrowsAsync<RestException>(() => rest.CrosspostMessageAsync(general.Id, 424242, cancellationToken: Ct));
        var forbidden = await Assert.ThrowsAsync<RestException>(() => rest.CrosspostMessageAsync(secret.Id, hidden, cancellationToken: Ct));

        AssertError(unknownChannel, HttpStatusCode.NotFound, 10003);
        AssertError(unknownMessage, HttpStatusCode.NotFound, 10008);
        AssertError(forbidden, HttpStatusCode.Forbidden, 50001);
    }

    [Fact]
    [Parity("POST /channels/{channel_id}/messages/{message_id}/crosspost")]
    [Parity("50035")]
    public async Task SendRequest_CrosspostWithAMalformedMessageId_Returns50035()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(async () =>
        {
            await using var stream = await rest.SendRequestAsync(HttpMethod.Post, $"/channels/{general.Id}/messages/{"abc"}/crosspost", cancellationToken: Ct);
        });

        Assert.Equal("NUMBER_TYPE_COERCE", Assert.Single(FieldErrors(exception, "message_id")).Code);
    }
}
