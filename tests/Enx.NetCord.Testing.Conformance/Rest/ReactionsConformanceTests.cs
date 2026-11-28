using System.Net;
using Enx.NetCord.Testing.Conformance.Contract;
using NetCord;
using NetCord.Rest;
using Xunit;

namespace Enx.NetCord.Testing.Conformance.Rest;

/// <summary>Reaction routes driven through the real NetCord <see cref="RestClient"/>, with every response checked against the OpenAPI contract.</summary>
[Trait("Category", "Conformance")]
public sealed class ReactionsConformanceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    [Parity("PUT /channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}/@me")]
    public async Task AddMessageReactionAsync_UnicodeEmoji_Returns204AndConformsToTheContract()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var message = general.Message("hello", guild.Member("alice"));
        using var rest = discord.CreateRestClient();

        await rest.AddMessageReactionAsync(general.Id, message, "👍", cancellationToken: Ct);

        Assert.Equal("👍", Assert.Single((await rest.GetMessageAsync(general.Id, message, cancellationToken: Ct)).Reactions).Emoji.Name);
        ContractAssert.Conforms(discord, "PUT /channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}/@me");
    }

    [Fact]
    [Parity("PUT /channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}/@me")]
    [Parity("10014")]
    public async Task AddMessageReactionAsync_NotAnEmoji_Returns10014WithTheDiscordErrorEnvelope()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var message = general.Message("hello", guild.Member("alice"));
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(async () =>
        {
            await using var stream = await rest.SendRequestAsync(HttpMethod.Put, $"/channels/{general.Id}/messages/{message}/reactions/{"nope"}/@me", cancellationToken: Ct);
        });

        Assert.Equal((HttpStatusCode.NotFound, 10014), (exception.StatusCode, exception.Error!.Code));
        ContractAssert.Conforms(discord, "PUT /channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}/@me");
    }

    [Fact]
    [Parity("PUT /channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}/@me")]
    [Parity("30010")]
    public async Task AddMessageReactionAsync_TooManyDistinctEmojis_Returns30010WithTheDiscordErrorEnvelope()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var alice = guild.Member("alice");
        var message = general.Message("popular", alice);
        for (var i = 0; i < 20; i++)
        {
            general.Reaction(message, char.ConvertFromUtf32(0x1F600 + i), alice);
        }

        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.AddMessageReactionAsync(general.Id, message, "🚀", cancellationToken: Ct));

        Assert.Equal(30010, exception.Error!.Code);
        ContractAssert.Conforms(discord, "PUT /channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}/@me");
    }

    [Fact]
    [Parity("DELETE /channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}/@me")]
    public async Task DeleteCurrentUserMessageReactionAsync_OwnReaction_Returns204AndConformsToTheContract()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var message = general.Message("hello", guild.Member("alice"));
        using var rest = discord.CreateRestClient();
        await rest.AddMessageReactionAsync(general.Id, message, "👍", cancellationToken: Ct);

        await rest.DeleteCurrentUserMessageReactionAsync(general.Id, message, "👍", cancellationToken: Ct);

        Assert.Empty((await rest.GetMessageAsync(general.Id, message, cancellationToken: Ct)).Reactions);
        ContractAssert.Conforms(discord, "DELETE /channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}/@me");
    }

    [Fact]
    [Parity("DELETE /channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}/@me")]
    [Parity("10014")]
    public async Task DeleteCurrentUserMessageReactionAsync_NoSuchReaction_Returns10014WithTheDiscordErrorEnvelope()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var message = general.Message("hello", guild.Member("alice"));
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.DeleteCurrentUserMessageReactionAsync(general.Id, message, "👍", cancellationToken: Ct));

        Assert.Equal((HttpStatusCode.NotFound, 10014), (exception.StatusCode, exception.Error!.Code));
        ContractAssert.Conforms(discord, "DELETE /channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}/@me");
    }

    [Fact]
    [Parity("DELETE /channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}/{user_id}")]
    public async Task DeleteUserMessageReactionAsync_AnotherUsersReaction_Returns204AndConformsToTheContract()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var alice = guild.Member("alice");
        var message = general.Message("hello", alice);
        general.Reaction(message, "👍", alice);
        using var rest = discord.CreateRestClient();

        await rest.DeleteUserMessageReactionAsync(general.Id, message, "👍", alice, cancellationToken: Ct);

        Assert.Empty((await rest.GetMessageAsync(general.Id, message, cancellationToken: Ct)).Reactions);
        ContractAssert.Conforms(discord, "DELETE /channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}/{user_id}");
    }

    [Fact]
    [Parity("DELETE /channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}/{user_id}")]
    [Parity("10008")]
    public async Task DeleteUserMessageReactionAsync_UnknownMessage_Returns10008WithTheDiscordErrorEnvelope()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.DeleteUserMessageReactionAsync(general.Id, 123456789, "👍", 1, cancellationToken: Ct));

        Assert.Equal(10008, exception.Error!.Code);
        ContractAssert.Conforms(discord, "DELETE /channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}/{user_id}");
    }

    [Fact]
    [Parity("GET /channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}")]
    public async Task GetMessageReactionsAsync_SeededReactors_ReturnsThemAndConformsToTheUserArray()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var alice = guild.Member("alice");
        var bob = guild.Member("bob");
        var message = general.Message("hello", alice);
        general.Reaction(message, "👍", alice);
        general.Reaction(message, "👍", bob);
        using var rest = discord.CreateRestClient();

        var users = await rest.GetMessageReactionsAsync(general.Id, message, "👍").ToListAsync(Ct);

        Assert.Equal([alice, bob], users.Select(u => u.Id));
        ContractAssert.Conforms(discord, "GET /channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}");
    }

    [Fact]
    [Parity("GET /channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}")]
    public async Task GetMessageReactionsAsync_BurstTypeAndNobodyReacted_ReturnsAnEmptyArrayAndConforms()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var message = general.Message("hello", guild.Member("alice"));
        using var rest = discord.CreateRestClient();

        var users = await rest.GetMessageReactionsAsync(general.Id, message, "🔥", new MessageReactionsPaginationProperties { Type = ReactionType.Burst }).ToListAsync(Ct);

        Assert.Empty(users);
        ContractAssert.Conforms(discord, "GET /channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}");
    }

    [Fact]
    [Parity("GET /channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}")]
    [Parity("10003")]
    public async Task GetMessageReactionsAsync_UnknownChannel_Returns10003WithTheDiscordErrorEnvelope()
    {
        await using var discord = FakeDiscord.Create();
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(async () => await rest.GetMessageReactionsAsync(123456789, 1, "👍").ToListAsync(Ct));

        Assert.Equal(10003, exception.Error!.Code);
        ContractAssert.Conforms(discord, "GET /channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}");
    }

    [Fact]
    [Parity("DELETE /channels/{channel_id}/messages/{message_id}/reactions")]
    public async Task DeleteAllMessageReactionsAsync_ReactedMessage_Returns204AndConformsToTheContract()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var alice = guild.Member("alice");
        var message = general.Message("hello", alice);
        general.Reaction(message, "👍", alice);
        general.Reaction(message, "🔥", alice);
        using var rest = discord.CreateRestClient();

        await rest.DeleteAllMessageReactionsAsync(general.Id, message, cancellationToken: Ct);

        Assert.Empty((await rest.GetMessageAsync(general.Id, message, cancellationToken: Ct)).Reactions);
        ContractAssert.Conforms(discord, "DELETE /channels/{channel_id}/messages/{message_id}/reactions");
    }

    [Fact]
    [Parity("DELETE /channels/{channel_id}/messages/{message_id}/reactions")]
    [Parity("50001")]
    public async Task DeleteAllMessageReactionsAsync_GuildTheBotIsNotIn_Returns50001WithTheDiscordErrorEnvelope()
    {
        await using var discord = FakeDiscord.Create();
        var secret = discord.Seed.Guild("Elsewhere", withBot: false).TextChannel("secret");
        var hidden = secret.Message("hidden", discord.Seed.User("alice"));
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.DeleteAllMessageReactionsAsync(secret.Id, hidden, cancellationToken: Ct));

        Assert.Equal((HttpStatusCode.Forbidden, 50001), (exception.StatusCode, exception.Error!.Code));
        ContractAssert.Conforms(discord, "DELETE /channels/{channel_id}/messages/{message_id}/reactions");
    }

    [Fact]
    [Parity("DELETE /channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}")]
    public async Task DeleteAllMessageReactionsForEmojiAsync_ReactedEmoji_Returns204AndConformsToTheContract()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var alice = guild.Member("alice");
        var message = general.Message("hello", alice);
        general.Reaction(message, "👍", alice);
        general.Reaction(message, "🔥", alice);
        using var rest = discord.CreateRestClient();

        await rest.DeleteAllMessageReactionsForEmojiAsync(general.Id, message, "👍", cancellationToken: Ct);

        Assert.Equal("🔥", Assert.Single((await rest.GetMessageAsync(general.Id, message, cancellationToken: Ct)).Reactions).Emoji.Name);
        ContractAssert.Conforms(discord, "DELETE /channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}");
    }

    [Fact]
    [Parity("DELETE /channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}")]
    [Parity("10014")]
    public async Task DeleteAllMessageReactionsForEmojiAsync_NobodyReacted_Returns10014WithTheDiscordErrorEnvelope()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var message = general.Message("hello", guild.Member("alice"));
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.DeleteAllMessageReactionsForEmojiAsync(general.Id, message, "👍", cancellationToken: Ct));

        Assert.Equal(10014, exception.Error!.Code);
        ContractAssert.Conforms(discord, "DELETE /channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}");
    }

    [Fact]
    [Parity("GET /channels/{channel_id}/messages/{message_id}")]
    public async Task GetMessageAsync_ReactedMessage_ConformsToMessageResponseWithReactions()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var alice = guild.Member("alice");
        var message = general.Message("hello", alice);
        general.Reaction(message, "👍", alice);
        general.Reaction(message, "👍", guild.Member("bob"), burst: true);
        general.Reaction(message, "wave:123456789012345678", alice);
        using var rest = discord.CreateRestClient();
        await rest.AddMessageReactionAsync(general.Id, message, "👍", cancellationToken: Ct);

        var read = await rest.GetMessageAsync(general.Id, message, cancellationToken: Ct);

        Assert.Equal(2, read.Reactions.Count);
        Assert.Equal((3, true), (read.Reactions[0].Count, read.Reactions[0].Me));
        ContractAssert.Conforms(discord, "GET /channels/{channel_id}/messages/{message_id}");
    }
}
