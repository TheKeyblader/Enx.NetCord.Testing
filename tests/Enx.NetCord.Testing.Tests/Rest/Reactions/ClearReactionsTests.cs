using System.Net;
using Enx.NetCord.Testing.State;
using NetCord.Rest;
using Xunit;
using static Enx.NetCord.Testing.Tests.Rest.Messages.MessagesTestSupport;
using static Enx.NetCord.Testing.Tests.State.StateTestSupport;

namespace Enx.NetCord.Testing.Tests.Rest.Reactions;

public sealed class ClearReactionsTests
{
    [Fact]
    [Parity("DELETE /channels/{channel_id}/messages/{message_id}/reactions")]
    public async Task DeleteAllMessageReactionsAsync_SeveralEmojisAndUsers_RemovesEverythingAndPublishesReactionsCleared()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var alice = guild.Member("alice");
        var message = general.Message("hello", alice);
        general.Reaction(message, "👍", alice);
        general.Reaction(message, "🔥", alice, burst: true);
        using var rest = discord.CreateRestClient();
        await rest.AddMessageReactionAsync(general.Id, message, "👍", cancellationToken: Ct);
        using var subscription = discord.State.SubscribeGateway();

        await rest.DeleteAllMessageReactionsAsync(general.Id, message, cancellationToken: Ct);

        Assert.Empty((await rest.GetMessageAsync(general.Id, message, cancellationToken: Ct)).Reactions);
        var cleared = Assert.IsType<ReactionsCleared>(Assert.Single(Drain(subscription)));
        Assert.Equal((guild.Id, general.Id, message), (cleared.GuildId, cleared.ChannelId, cleared.MessageId));
        Assert.Equal(HttpStatusCode.NoContent, discord.Recorded.Requests.First(r => r.Method == "DELETE").StatusCode);
    }

    [Fact]
    [Parity("DELETE /channels/{channel_id}/messages/{message_id}/reactions")]
    public async Task DeleteAllMessageReactionsAsync_NoReactions_Returns204AndCommitsNothing()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();
        var message = await rest.SendMessageAsync(general.Id, "x", cancellationToken: Ct);
        using var subscription = discord.State.SubscribeGateway();

        await rest.DeleteAllMessageReactionsAsync(general.Id, message.Id, cancellationToken: Ct);

        Assert.Empty(Drain(subscription));
    }

    [Fact]
    [Parity("DELETE /channels/{channel_id}/messages/{message_id}/reactions")]
    [Parity("10003")]
    [Parity("10008")]
    [Parity("50001")]
    public async Task DeleteAllMessageReactionsAsync_UnknownChannelMessageOrGuildTheBotIsNotIn_ReturnsTheMatchingError()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        var secret = discord.Seed.Guild("Elsewhere", withBot: false).TextChannel("secret");
        var alice = discord.Seed.User("alice");
        var hidden = secret.Message("hidden", alice);
        secret.Reaction(hidden, "👍", alice);
        using var rest = discord.CreateRestClient();

        var unknownChannel = await Assert.ThrowsAsync<RestException>(() => rest.DeleteAllMessageReactionsAsync(123456789, 1, cancellationToken: Ct));
        var unknownMessage = await Assert.ThrowsAsync<RestException>(() => rest.DeleteAllMessageReactionsAsync(general.Id, 424242, cancellationToken: Ct));
        var forbidden = await Assert.ThrowsAsync<RestException>(() => rest.DeleteAllMessageReactionsAsync(secret.Id, hidden, cancellationToken: Ct));

        AssertError(unknownChannel, HttpStatusCode.NotFound, 10003);
        AssertError(unknownMessage, HttpStatusCode.NotFound, 10008);
        AssertError(forbidden, HttpStatusCode.Forbidden, 50001);
        Assert.Single(discord.State.FindMessage(secret.Id, hidden)!.Reactions);
    }

    [Fact]
    [Parity("DELETE /channels/{channel_id}/messages/{message_id}/reactions")]
    [Parity("50035")]
    public async Task SendRequest_DeleteAllReactionsWithAMalformedMessageId_Returns50035()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(async () =>
        {
            await using var stream = await rest.SendRequestAsync(HttpMethod.Delete, $"/channels/{general.Id}/messages/{"abc"}/reactions", cancellationToken: Ct);
        });

        Assert.Equal("NUMBER_TYPE_COERCE", Assert.Single(FieldErrors(exception, "message_id")).Code);
    }

    [Fact]
    [Parity("DELETE /channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}")]
    public async Task DeleteAllMessageReactionsForEmojiAsync_NormalAndBurst_RemovesThatEmojiOnly()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var alice = guild.Member("alice");
        var bob = guild.Member("bob");
        var message = general.Message("hello", alice);
        general.Reaction(message, "👍", alice);
        general.Reaction(message, "👍", bob, burst: true);
        general.Reaction(message, "🔥", alice);
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        await rest.DeleteAllMessageReactionsForEmojiAsync(general.Id, message, "👍", cancellationToken: Ct);

        var reactions = (await rest.GetMessageAsync(general.Id, message, cancellationToken: Ct)).Reactions;
        Assert.Equal("🔥", Assert.Single(reactions).Emoji.Name);
        var cleared = Assert.IsType<ReactionEmojiCleared>(Assert.Single(Drain(subscription)));
        Assert.Equal((guild.Id, general.Id, message), (cleared.GuildId, cleared.ChannelId, cleared.MessageId));
        Assert.Equal(new ReactionEmoji("👍", null), cleared.Emoji);
    }

    [Fact]
    [Parity("DELETE /channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}")]
    [Parity("10014")]
    public async Task DeleteAllMessageReactionsForEmojiAsync_NobodyReactedWithIt_Returns404UnknownEmoji()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var alice = guild.Member("alice");
        var message = general.Message("hello", alice);
        general.Reaction(message, "👍", alice);
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.DeleteAllMessageReactionsForEmojiAsync(general.Id, message, "🔥", cancellationToken: Ct));
        var notAnEmoji = await Assert.ThrowsAsync<RestException>(async () =>
        {
            await using var stream = await rest.SendRequestAsync(HttpMethod.Delete, $"/channels/{general.Id}/messages/{message}/reactions/{"nope"}", cancellationToken: Ct);
        });

        AssertError(exception, HttpStatusCode.NotFound, 10014);
        AssertError(notAnEmoji, HttpStatusCode.NotFound, 10014);
        Assert.Single(discord.State.FindMessage(general.Id, message)!.Reactions);
        Assert.Empty(Drain(subscription));
    }

    [Fact]
    [Parity("DELETE /channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}")]
    [Parity("10003")]
    [Parity("10008")]
    [Parity("50001")]
    public async Task DeleteAllMessageReactionsForEmojiAsync_UnknownChannelMessageOrGuildTheBotIsNotIn_ReturnsTheMatchingError()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        var secret = discord.Seed.Guild("Elsewhere", withBot: false).TextChannel("secret");
        var alice = discord.Seed.User("alice");
        var hidden = secret.Message("hidden", alice);
        secret.Reaction(hidden, "👍", alice);
        using var rest = discord.CreateRestClient();

        var unknownChannel = await Assert.ThrowsAsync<RestException>(() => rest.DeleteAllMessageReactionsForEmojiAsync(123456789, 1, "👍", cancellationToken: Ct));
        var unknownMessage = await Assert.ThrowsAsync<RestException>(() => rest.DeleteAllMessageReactionsForEmojiAsync(general.Id, 424242, "👍", cancellationToken: Ct));
        var forbidden = await Assert.ThrowsAsync<RestException>(() => rest.DeleteAllMessageReactionsForEmojiAsync(secret.Id, hidden, "👍", cancellationToken: Ct));

        AssertError(unknownChannel, HttpStatusCode.NotFound, 10003);
        AssertError(unknownMessage, HttpStatusCode.NotFound, 10008);
        AssertError(forbidden, HttpStatusCode.Forbidden, 50001);
    }
}
