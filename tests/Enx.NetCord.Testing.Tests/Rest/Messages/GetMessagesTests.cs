using System.Net;
using System.Text.Json;
using Enx.NetCord.Testing.State;
using NetCord.Rest;
using Xunit;
using static Enx.NetCord.Testing.Tests.Rest.Messages.MessagesTestSupport;

namespace Enx.NetCord.Testing.Tests.Rest.Messages;

public sealed class GetMessagesTests
{
    private static (SeededTextChannel Channel, ulong[] Ids, ulong Author, SeededGuild Guild) SeedFive(FakeDiscord discord)
    {
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var author = guild.Member("alice");
        var ids = Enumerable.Range(1, 5).Select(i => general.Message($"m{i}", author)).ToArray();
        return (general, ids, author, guild);
    }

    [Fact]
    [Parity("GET /channels/{channel_id}/messages")]
    public async Task GetMessagesAsync_NoCursor_ReturnsTheNewestFirst()
    {
        await using var discord = FakeDiscord.Create();
        var (general, ids, _, _) = SeedFive(discord);
        using var rest = discord.CreateRestClient();

        var messages = await rest.GetMessagesAsync(general.Id).ToListAsync(Ct);

        Assert.Equal(ids.OrderDescending(), messages.Select(m => m.Id));
        Assert.Equal(["m5", "m4", "m3", "m2", "m1"], messages.Select(m => m.Content));
    }

    [Fact]
    [Parity("GET /channels/{channel_id}/messages")]
    public async Task GetMessagesAsync_WithoutLimit_UsesTheDefaultOfFifty()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var author = guild.Member("alice");
        for (var i = 0; i < 60; i++)
        {
            general.Message($"m{i}", author);
        }

        using var rest = discord.CreateRestClient();
        using var json = await GetRawAsync(rest, $"/channels/{general.Id}/messages");

        Assert.Equal(50, json.RootElement.GetArrayLength());
        Assert.Equal("m59", json.RootElement[0].GetProperty("content").GetString());
    }

    [Fact]
    [Parity("GET /channels/{channel_id}/messages")]
    public async Task GetMessagesAsync_Limit_ReturnsAtMostThatManyNewest()
    {
        await using var discord = FakeDiscord.Create();
        var (general, ids, _, _) = SeedFive(discord);
        using var rest = discord.CreateRestClient();

        var messages = await rest.GetMessagesAsync(general.Id, new PaginationProperties<ulong> { BatchSize = 2 }).Take(2).ToListAsync(Ct);

        Assert.Equal([ids[4], ids[3]], messages.Select(m => m.Id));
    }

    [Fact]
    [Parity("GET /channels/{channel_id}/messages")]
    public async Task GetMessagesAsync_Before_ReturnsTheOlderOnesNewestFirst()
    {
        await using var discord = FakeDiscord.Create();
        var (general, ids, _, _) = SeedFive(discord);
        using var rest = discord.CreateRestClient();

        var messages = await rest.GetMessagesAsync(general.Id, new PaginationProperties<ulong> { From = ids[3], Direction = PaginationDirection.Before }).ToListAsync(Ct);

        Assert.Equal([ids[2], ids[1], ids[0]], messages.Select(m => m.Id));
    }

    [Fact]
    [Parity("GET /channels/{channel_id}/messages")]
    public async Task GetMessagesAsync_After_ReturnsTheNewerOnes()
    {
        await using var discord = FakeDiscord.Create();
        var (general, ids, _, _) = SeedFive(discord);
        using var rest = discord.CreateRestClient();

        var messages = await rest.GetMessagesAsync(general.Id, new PaginationProperties<ulong> { From = ids[1], Direction = PaginationDirection.After }).ToListAsync(Ct);

        Assert.Equal([ids[2], ids[3], ids[4]], messages.Select(m => m.Id));
    }

    [Fact]
    [Parity("GET /channels/{channel_id}/messages")]
    public async Task GetMessagesAroundAsync_CenteredOnAMessage_ReturnsItWithItsNeighbours()
    {
        await using var discord = FakeDiscord.Create();
        var (general, ids, _, _) = SeedFive(discord);
        using var rest = discord.CreateRestClient();

        var messages = await rest.GetMessagesAroundAsync(general.Id, ids[2], 4, cancellationToken: Ct);

        // limit / 2 older messages, then the target and the newer ones, all newest first.
        Assert.Equal([ids[3], ids[2], ids[1], ids[0]], messages.Select(m => m.Id));
        Assert.All(messages, m => Assert.NotNull(m.Author));
    }

    [Fact]
    [Parity("GET /channels/{channel_id}/messages")]
    public async Task GetMessagesAroundAsync_DefaultLimit_ReturnsEverythingOfASmallChannel()
    {
        await using var discord = FakeDiscord.Create();
        var (general, ids, _, _) = SeedFive(discord);
        using var rest = discord.CreateRestClient();

        var messages = await rest.GetMessagesAroundAsync(general.Id, ids[2], cancellationToken: Ct);

        Assert.Equal(ids.OrderDescending(), messages.Select(m => m.Id));
    }

    [Fact]
    [Parity("GET /channels/{channel_id}/messages")]
    public async Task GetMessagesAsync_EmptyChannel_ReturnsAnEmptyList()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();

        Assert.Empty(await rest.GetMessagesAsync(general.Id).ToListAsync(Ct));
    }

    [Fact]
    [Parity("GET /channels/{channel_id}/messages")]
    public async Task GetMessagesAsync_DirectMessageChannel_ListsItsMessages()
    {
        await using var discord = FakeDiscord.Create();
        var aliceId = discord.Seed.User("alice");
        using var rest = discord.CreateRestClient();
        var dm = await rest.GetDMChannelAsync(aliceId, cancellationToken: Ct);
        var sent = await rest.SendMessageAsync(dm.Id, "psst", cancellationToken: Ct);

        var messages = await rest.GetMessagesAsync(dm.Id).ToListAsync(Ct);

        Assert.Equal(sent.Id, Assert.Single(messages).Id);
    }

    [Fact]
    [Parity("GET /channels/{channel_id}/messages")]
    public async Task GetMessagesAsync_ResponseItems_HaveTheShapeOfMessageResponse()
    {
        await using var discord = FakeDiscord.Create();
        var (general, ids, authorId, _) = SeedFive(discord);
        using var rest = discord.CreateRestClient();

        using var json = await GetRawAsync(rest, $"/channels/{general.Id}/messages", "?limit=1");

        var message = Assert.Single(json.RootElement.EnumerateArray());
        Assert.Equal(ids[4].ToString(), message.GetProperty("id").GetString());
        Assert.Equal(general.Id.ToString(), message.GetProperty("channel_id").GetString());
        Assert.Equal(authorId.ToString(), message.GetProperty("author").GetProperty("id").GetString());
        Assert.Equal(JsonValueKind.Null, message.GetProperty("edited_timestamp").ValueKind);
        Assert.Equal(JsonValueKind.String, message.GetProperty("timestamp").ValueKind);
        foreach (var array in new[] { "mentions", "mention_roles", "attachments", "embeds", "components" })
        {
            Assert.Equal(0, message.GetProperty(array).GetArrayLength());
        }

        Assert.False(message.GetProperty("pinned").GetBoolean());
        Assert.False(message.GetProperty("tts").GetBoolean());
        Assert.False(message.GetProperty("mention_everyone").GetBoolean());
        Assert.Equal(0, message.GetProperty("type").GetInt32());
        Assert.Equal(0, message.GetProperty("flags").GetInt32());
        Assert.False(message.TryGetProperty("guild_id", out _));
    }

    [Fact]
    [Parity("GET /channels/{channel_id}/messages")]
    [Parity("10003")]
    public async Task GetMessagesAsync_UnknownChannel_Returns404UnknownChannel()
    {
        await using var discord = FakeDiscord.Create();
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.GetMessagesAsync(123456789).ToListAsync(Ct).AsTask());

        AssertError(exception, HttpStatusCode.NotFound, 10003);
    }

    [Fact]
    [Parity("GET /channels/{channel_id}/messages")]
    [Parity("50001")]
    public async Task GetMessagesAsync_ChannelOfAGuildTheBotIsNotIn_Returns403MissingAccess()
    {
        await using var discord = FakeDiscord.Create();
        var secret = discord.Seed.Guild("Elsewhere", withBot: false).TextChannel("secret");
        secret.Message("hidden", discord.Seed.User("alice"));
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.GetMessagesAsync(secret.Id).ToListAsync(Ct).AsTask());

        AssertError(exception, HttpStatusCode.Forbidden, 50001);
    }

    [Theory]
    [Parity("GET /channels/{channel_id}/messages")]
    [Parity("50035")]
    [InlineData("limit=0", "limit", "NUMBER_TYPE_MIN")]
    [InlineData("limit=101", "limit", "NUMBER_TYPE_MAX")]
    [InlineData("limit=abc", "limit", "NUMBER_TYPE_COERCE")]
    [InlineData("before=abc", "before", "NUMBER_TYPE_COERCE")]
    [InlineData("after=-1", "after", "NUMBER_TYPE_COERCE")]
    [InlineData("around=1.5", "around", "NUMBER_TYPE_COERCE")]
    public async Task SendRequest_InvalidQuery_Returns50035AtTheParameter(string query, string path, string code)
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(async () =>
        {
            await using var stream = await rest.SendRequestAsync(HttpMethod.Get, $"/channels/{general.Id}/messages", query: $"?{query}", cancellationToken: Ct);
        });

        Assert.Equal(code, Assert.Single(FieldErrors(exception, path)).Code);
    }

    [Fact]
    [Parity("GET /channels/{channel_id}/messages")]
    public async Task SendRequest_SeveralCursors_AroundWinsThenBefore()
    {
        await using var discord = FakeDiscord.Create();
        var (general, ids, _, _) = SeedFive(discord);
        using var rest = discord.CreateRestClient();

        using var withBoth = await GetRawAsync(rest, $"/channels/{general.Id}/messages", $"?before={ids[1]}&after={ids[3]}");

        Assert.Equal([ids[0].ToString()], withBoth.RootElement.EnumerateArray().Select(m => m.GetProperty("id").GetString()));
    }

    [Fact]
    [Parity("GET /channels/{channel_id}/messages/{message_id}")]
    public async Task GetMessageAsync_ExistingMessage_ReturnsIt()
    {
        await using var discord = FakeDiscord.Create();
        var (general, ids, authorId, _) = SeedFive(discord);
        using var rest = discord.CreateRestClient();

        var message = await rest.GetMessageAsync(general.Id, ids[2], cancellationToken: Ct);

        Assert.Equal(ids[2], message.Id);
        Assert.Equal("m3", message.Content);
        Assert.Equal(authorId, message.Author.Id);
        Assert.Equal("alice", message.Author.Username);
        discord.Recorded.ShouldHaveRequestedOnce($"GET /channels/{general.Id}/messages/{ids[2]}");
    }

    [Fact]
    [Parity("GET /channels/{channel_id}/messages/{message_id}")]
    [Parity("10008")]
    public async Task GetMessageAsync_UnknownMessage_Returns404UnknownMessage()
    {
        await using var discord = FakeDiscord.Create();
        var (general, _, _, _) = SeedFive(discord);
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.GetMessageAsync(general.Id, 424242, cancellationToken: Ct));

        AssertError(exception, HttpStatusCode.NotFound, 10008);
    }

    [Fact]
    [Parity("GET /channels/{channel_id}/messages/{message_id}")]
    [Parity("10008")]
    public async Task GetMessageAsync_MessageOfAnotherChannel_Returns404UnknownMessage()
    {
        await using var discord = FakeDiscord.Create();
        var (general, ids, _, guild) = SeedFive(discord);
        var other = guild.TextChannel("other");
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.GetMessageAsync(other.Id, ids[0], cancellationToken: Ct));

        AssertError(exception, HttpStatusCode.NotFound, 10008);
        Assert.Equal(ids[0], (await rest.GetMessageAsync(general.Id, ids[0], cancellationToken: Ct)).Id);
    }

    [Fact]
    [Parity("GET /channels/{channel_id}/messages/{message_id}")]
    [Parity("10003")]
    [Parity("50001")]
    public async Task GetMessageAsync_UnknownChannelOrChannelOfAGuildTheBotIsNotIn_Returns10003Or50001BeforeLookingForTheMessage()
    {
        await using var discord = FakeDiscord.Create();
        var secret = discord.Seed.Guild("Elsewhere", withBot: false).TextChannel("secret");
        var hidden = secret.Message("hidden", discord.Seed.User("alice"));
        using var rest = discord.CreateRestClient();

        var unknownChannel = await Assert.ThrowsAsync<RestException>(() => rest.GetMessageAsync(123456789, 1, cancellationToken: Ct));
        var forbidden = await Assert.ThrowsAsync<RestException>(() => rest.GetMessageAsync(secret.Id, hidden, cancellationToken: Ct));
        var forbiddenUnknownMessage = await Assert.ThrowsAsync<RestException>(() => rest.GetMessageAsync(secret.Id, 1, cancellationToken: Ct));

        AssertError(unknownChannel, HttpStatusCode.NotFound, 10003);
        AssertError(forbidden, HttpStatusCode.Forbidden, 50001);
        AssertError(forbiddenUnknownMessage, HttpStatusCode.Forbidden, 50001);
    }

    [Fact]
    [Parity("GET /channels/{channel_id}/messages/{message_id}")]
    [Parity("50035")]
    public async Task SendRequest_GetMessageWithAMalformedMessageId_Returns50035OnMessageId()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(async () =>
        {
            await using var stream = await rest.SendRequestAsync(HttpMethod.Get, $"/channels/{general.Id}/messages/{"abc"}", cancellationToken: Ct);
        });

        Assert.Equal("NUMBER_TYPE_COERCE", Assert.Single(FieldErrors(exception, "message_id")).Code);
    }
}
