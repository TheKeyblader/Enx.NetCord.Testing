using System.Net;
using System.Text.Json;
using NetCord;
using NetCord.Rest;
using Xunit;
using static Enx.NetCord.Testing.Tests.Rest.Messages.MessagesTestSupport;

namespace Enx.NetCord.Testing.Tests.Rest.Reactions;

public sealed class ListReactionsTests
{
    [Fact]
    [Parity("GET /channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}")]
    public async Task GetMessageReactionsAsync_SeveralUsers_ReturnsThemByAscendingIdWithTheUserShape()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var alice = guild.Member("alice");
        var bob = guild.Member("bob");
        var carol = guild.Member("carol");
        var message = general.Message("hello", alice);
        general.Reaction(message, "👍", carol);
        general.Reaction(message, "👍", alice);
        general.Reaction(message, "🔥", bob);
        using var rest = discord.CreateRestClient();
        await rest.AddMessageReactionAsync(general.Id, message, "👍", cancellationToken: Ct);

        var users = await rest.GetMessageReactionsAsync(general.Id, message, "👍").ToListAsync(Ct);

        // The bot user is created with the fake, so its ID is the lowest.
        Assert.Equal([discord.ApplicationId, alice, carol], users.Select(u => u.Id));
        Assert.True(users[0].IsBot);
        Assert.Equal("alice", users[1].Username);
        discord.Recorded.ShouldHaveRequested($"GET /channels/{general.Id}/messages/{message}/reactions/👍");
        Assert.Equal([bob], (await rest.GetMessageReactionsAsync(general.Id, message, "🔥").ToListAsync(Ct)).Select(u => u.Id));
    }

    [Fact]
    [Parity("GET /channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}")]
    public async Task GetMessageReactionsAsync_BatchSmallerThanTheUsers_PagesWithAfter()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var author = guild.Member("author");
        var message = general.Message("hello", author);
        var reactors = Enumerable.Range(0, 5).Select(i => guild.Member($"user{i}")).ToArray();
        foreach (var reactor in reactors)
        {
            general.Reaction(message, "👍", reactor);
        }

        using var rest = discord.CreateRestClient();

        var users = await rest.GetMessageReactionsAsync(general.Id, message, "👍", new MessageReactionsPaginationProperties { BatchSize = 2 }).ToListAsync(Ct);

        Assert.Equal(reactors, users.Select(u => u.Id));
        var gets = discord.Recorded.Requests.Where(r => r.Method == "GET").ToList();
        Assert.Equal(3, gets.Count);
        Assert.All(gets, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
    }

    [Fact]
    [Parity("GET /channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}")]
    public async Task GetMessageReactionsAsync_FromAUser_ReturnsOnlyTheUsersAfterIt()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var message = general.Message("hello", guild.Member("author"));
        var first = guild.Member("first");
        var second = guild.Member("second");
        var third = guild.Member("third");
        foreach (var reactor in new[] { first, second, third })
        {
            general.Reaction(message, "👍", reactor);
        }

        using var rest = discord.CreateRestClient();

        var users = await rest.GetMessageReactionsAsync(general.Id, message, "👍", new MessageReactionsPaginationProperties { From = first }).ToListAsync(Ct);

        Assert.Equal([second, third], users.Select(u => u.Id));
    }

    [Fact]
    [Parity("GET /channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}")]
    public async Task GetMessageReactionsAsync_BurstType_ListsOnlyTheSuperReactions()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var alice = guild.Member("alice");
        var bob = guild.Member("bob");
        var message = general.Message("hello", alice);
        general.Reaction(message, "👍", alice);
        general.Reaction(message, "👍", bob, burst: true);
        using var rest = discord.CreateRestClient();

        var normal = await rest.GetMessageReactionsAsync(general.Id, message, "👍").ToListAsync(Ct);
        var burst = await rest.GetMessageReactionsAsync(general.Id, message, "👍", new MessageReactionsPaginationProperties { Type = ReactionType.Burst }).ToListAsync(Ct);

        Assert.Equal([alice], normal.Select(u => u.Id));
        Assert.Equal([bob], burst.Select(u => u.Id));
    }

    [Fact]
    [Parity("GET /channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}")]
    public async Task GetMessageReactionsAsync_NobodyReactedWithThatEmoji_ReturnsAnEmptyList()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var message = general.Message("hello", guild.Member("alice"));
        using var rest = discord.CreateRestClient();

        var users = await rest.GetMessageReactionsAsync(general.Id, message, "👍").ToListAsync(Ct);

        Assert.Empty(users);
    }

    [Fact]
    [Parity("GET /channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}")]
    public async Task SendRequest_GetReactionsWithoutLimit_ReturnsTwentyFiveUsers()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var message = general.Message("hello", guild.Member("author"));
        for (var i = 0; i < 30; i++)
        {
            general.Reaction(message, "👍", guild.Member($"user{i}"));
        }

        using var rest = discord.CreateRestClient();

        using var document = await GetRawAsync(rest, $"/channels/{general.Id}/messages/{message}/reactions/{"👍"}");
        using var limited = await GetRawAsync(rest, $"/channels/{general.Id}/messages/{message}/reactions/{"👍"}", "?limit=100");

        Assert.Equal(JsonValueKind.Array, document.RootElement.ValueKind);
        Assert.Equal(25, document.RootElement.GetArrayLength());
        Assert.Equal(30, limited.RootElement.GetArrayLength());
    }

    [Fact]
    [Parity("GET /channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}")]
    [Parity("10003")]
    [Parity("10008")]
    [Parity("50001")]
    public async Task GetMessageReactionsAsync_UnknownChannelMessageOrGuildTheBotIsNotIn_ReturnsTheMatchingError()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        var secret = discord.Seed.Guild("Elsewhere", withBot: false).TextChannel("secret");
        var alice = discord.Seed.User("alice");
        var hidden = secret.Message("hidden", alice);
        secret.Reaction(hidden, "👍", alice);
        using var rest = discord.CreateRestClient();

        var unknownChannel = await Assert.ThrowsAsync<RestException>(() => rest.GetMessageReactionsAsync(123456789, 1, "👍").ToListAsync(Ct).AsTask());
        var unknownMessage = await Assert.ThrowsAsync<RestException>(() => rest.GetMessageReactionsAsync(general.Id, 424242, "👍").ToListAsync(Ct).AsTask());
        var forbidden = await Assert.ThrowsAsync<RestException>(() => rest.GetMessageReactionsAsync(secret.Id, hidden, "👍").ToListAsync(Ct).AsTask());

        AssertError(unknownChannel, HttpStatusCode.NotFound, 10003);
        AssertError(unknownMessage, HttpStatusCode.NotFound, 10008);
        AssertError(forbidden, HttpStatusCode.Forbidden, 50001);
    }

    [Theory]
    [Parity("GET /channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}")]
    [Parity("50035")]
    [InlineData("?limit=0", "limit", "NUMBER_TYPE_MIN")]
    [InlineData("?limit=101", "limit", "NUMBER_TYPE_MAX")]
    [InlineData("?limit=abc", "limit", "NUMBER_TYPE_COERCE")]
    [InlineData("?after=abc", "after", "NUMBER_TYPE_COERCE")]
    [InlineData("?type=2", "type", "BASE_TYPE_CHOICES")]
    [InlineData("?type=-1", "type", "BASE_TYPE_CHOICES")]
    [InlineData("?type=abc", "type", "NUMBER_TYPE_COERCE")]
    public async Task SendRequest_GetReactionsWithAnInvalidQuery_Returns50035OnTheParameter(string query, string parameter, string code)
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var message = general.Message("hello", guild.Member("alice"));
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(async () =>
        {
            await using var stream = await rest.SendRequestAsync(HttpMethod.Get, $"/channels/{general.Id}/messages/{message}/reactions/{"👍"}", query, cancellationToken: Ct);
        });

        Assert.Equal(code, Assert.Single(FieldErrors(exception, parameter)).Code);
    }

    [Fact]
    [Parity("GET /channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}")]
    [Parity("10014")]
    public async Task SendRequest_GetReactionsWithASegmentThatIsNoEmoji_Returns404UnknownEmoji()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var message = general.Message("hello", guild.Member("alice"));
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(async () =>
        {
            await using var stream = await rest.SendRequestAsync(HttpMethod.Get, $"/channels/{general.Id}/messages/{message}/reactions/{"nope"}", cancellationToken: Ct);
        });

        AssertError(exception, HttpStatusCode.NotFound, 10014);
    }
}
