using System.Net;
using Enx.NetCord.Testing.State;
using NetCord.Rest;
using Xunit;
using static Enx.NetCord.Testing.Tests.Rest.Messages.MessagesTestSupport;
using static Enx.NetCord.Testing.Tests.State.StateTestSupport;

namespace Enx.NetCord.Testing.Tests.Rest.Reactions;

public sealed class RemoveReactionTests
{
    [Fact]
    [Parity("DELETE /channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}/@me")]
    public async Task DeleteCurrentUserMessageReactionAsync_OwnReaction_RemovesItAndKeepsTheOthers()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var alice = guild.Member("alice");
        var message = general.Message("hello", alice);
        general.Reaction(message, "👍", alice);
        using var rest = discord.CreateRestClient();
        await rest.AddMessageReactionAsync(general.Id, message, "👍", cancellationToken: Ct);
        await rest.AddMessageReactionAsync(general.Id, message, "🔥", cancellationToken: Ct);
        using var subscription = discord.State.SubscribeGateway();

        await rest.DeleteCurrentUserMessageReactionAsync(general.Id, message, "👍", cancellationToken: Ct);

        var reactions = (await rest.GetMessageAsync(general.Id, message, cancellationToken: Ct)).Reactions;
        Assert.Equal(["👍", "🔥"], reactions.Select(r => r.Emoji.Name));
        Assert.Equal((1, false), (reactions[0].Count, reactions[0].Me));
        Assert.Equal((1, true), (reactions[1].Count, reactions[1].Me));
        var removed = Assert.IsType<ReactionRemoved>(Assert.Single(Drain(subscription)));
        Assert.Equal((guild.Id, general.Id, message, discord.ApplicationId), (removed.GuildId, removed.ChannelId, removed.MessageId, removed.UserId));
        Assert.Equal(new ReactionEmoji("👍", null), removed.Emoji);
        Assert.Equal(HttpStatusCode.NoContent, discord.Recorded.Requests.First(r => r.Method == "DELETE").StatusCode);
    }

    [Fact]
    [Parity("DELETE /channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}/@me")]
    public async Task DeleteCurrentUserMessageReactionAsync_LastReaction_DropsTheReactionsFromTheMessage()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();
        var message = await rest.SendMessageAsync(general.Id, "x", cancellationToken: Ct);
        await rest.AddMessageReactionAsync(general.Id, message.Id, "👍", cancellationToken: Ct);

        await rest.DeleteCurrentUserMessageReactionAsync(general.Id, message.Id, "👍", cancellationToken: Ct);

        Assert.Empty((await rest.GetMessageAsync(general.Id, message.Id, cancellationToken: Ct)).Reactions);
    }

    [Fact]
    [Parity("DELETE /channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}/@me")]
    [Parity("10014")]
    public async Task DeleteCurrentUserMessageReactionAsync_BotNeverReacted_Returns404UnknownEmojiAndChangesNothing()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var alice = guild.Member("alice");
        var message = general.Message("hello", alice);
        general.Reaction(message, "👍", alice);
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        var sameEmoji = await Assert.ThrowsAsync<RestException>(() => rest.DeleteCurrentUserMessageReactionAsync(general.Id, message, "👍", cancellationToken: Ct));
        var otherEmoji = await Assert.ThrowsAsync<RestException>(() => rest.DeleteCurrentUserMessageReactionAsync(general.Id, message, "🔥", cancellationToken: Ct));

        AssertError(sameEmoji, HttpStatusCode.NotFound, 10014);
        AssertError(otherEmoji, HttpStatusCode.NotFound, 10014);
        Assert.Equal(1, Assert.Single(discord.State.FindMessage(general.Id, message)!.Reactions).NormalCount);
        Assert.Empty(Drain(subscription));
    }

    [Fact]
    [Parity("DELETE /channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}/@me")]
    [Parity("10003")]
    [Parity("10008")]
    [Parity("50001")]
    public async Task DeleteCurrentUserMessageReactionAsync_UnknownChannelMessageOrGuildTheBotIsNotIn_ReturnsTheMatchingError()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        var secret = discord.Seed.Guild("Elsewhere", withBot: false).TextChannel("secret");
        var alice = discord.Seed.User("alice");
        var hidden = secret.Message("hidden", alice);
        secret.Reaction(hidden, "👍", alice);
        using var rest = discord.CreateRestClient();

        var unknownChannel = await Assert.ThrowsAsync<RestException>(() => rest.DeleteCurrentUserMessageReactionAsync(123456789, 1, "👍", cancellationToken: Ct));
        var unknownMessage = await Assert.ThrowsAsync<RestException>(() => rest.DeleteCurrentUserMessageReactionAsync(general.Id, 424242, "👍", cancellationToken: Ct));
        var forbidden = await Assert.ThrowsAsync<RestException>(() => rest.DeleteCurrentUserMessageReactionAsync(secret.Id, hidden, "👍", cancellationToken: Ct));

        AssertError(unknownChannel, HttpStatusCode.NotFound, 10003);
        AssertError(unknownMessage, HttpStatusCode.NotFound, 10008);
        AssertError(forbidden, HttpStatusCode.Forbidden, 50001);
    }

    [Fact]
    [Parity("DELETE /channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}/@me")]
    [Parity("10014")]
    public async Task SendRequest_DeleteOwnReactionWithASegmentThatIsNoEmoji_Returns404UnknownEmoji()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var message = general.Message("hello", guild.Member("alice"));
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(async () =>
        {
            await using var stream = await rest.SendRequestAsync(HttpMethod.Delete, $"/channels/{general.Id}/messages/{message}/reactions/{"nope"}/@me", cancellationToken: Ct);
        });

        AssertError(exception, HttpStatusCode.NotFound, 10014);
    }

    [Fact]
    [Parity("DELETE /channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}/{user_id}")]
    public async Task DeleteUserMessageReactionAsync_AnotherUsersReaction_RemovesOnlyThatOne()
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
        using var subscription = discord.State.SubscribeGateway();

        await rest.DeleteUserMessageReactionAsync(general.Id, message, "👍", alice, cancellationToken: Ct);

        var remaining = await rest.GetMessageReactionsAsync(general.Id, message, "👍").ToListAsync(Ct);
        Assert.Equal([bob], remaining.Select(u => u.Id));
        var removed = Assert.IsType<ReactionRemoved>(Assert.Single(Drain(subscription)));
        Assert.Equal((guild.Id, general.Id, message, alice), (removed.GuildId, removed.ChannelId, removed.MessageId, removed.UserId));
        discord.Recorded.ShouldHaveRequestedOnce($"DELETE /channels/{general.Id}/messages/{message}/reactions/👍/{alice}");
    }

    [Fact]
    [Parity("DELETE /channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}/{user_id}")]
    public async Task DeleteUserMessageReactionAsync_CustomEmoji_RemovesIt()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var alice = guild.Member("alice");
        var message = general.Message("hello", alice);
        general.Reaction(message, "wave:123456789012345678", alice);
        using var rest = discord.CreateRestClient();

        await rest.DeleteUserMessageReactionAsync(general.Id, message, new ReactionEmojiProperties("wave", 123456789012345678), alice, cancellationToken: Ct);

        Assert.Empty(discord.State.FindMessage(general.Id, message)!.Reactions);
    }

    [Fact]
    [Parity("DELETE /channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}/{user_id}")]
    [Parity("10014")]
    public async Task DeleteUserMessageReactionAsync_UserDidNotReact_Returns404UnknownEmoji()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var alice = guild.Member("alice");
        var message = general.Message("hello", alice);
        general.Reaction(message, "👍", alice);
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.DeleteUserMessageReactionAsync(general.Id, message, "👍", guild.Member("bob"), cancellationToken: Ct));

        AssertError(exception, HttpStatusCode.NotFound, 10014);
        Assert.Equal(1, Assert.Single(discord.State.FindMessage(general.Id, message)!.Reactions).NormalCount);
    }

    [Fact]
    [Parity("DELETE /channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}/{user_id}")]
    [Parity("10003")]
    [Parity("10008")]
    [Parity("50001")]
    public async Task DeleteUserMessageReactionAsync_UnknownChannelMessageOrGuildTheBotIsNotIn_ReturnsTheMatchingError()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        var secret = discord.Seed.Guild("Elsewhere", withBot: false).TextChannel("secret");
        var alice = discord.Seed.User("alice");
        var hidden = secret.Message("hidden", alice);
        secret.Reaction(hidden, "👍", alice);
        using var rest = discord.CreateRestClient();

        var unknownChannel = await Assert.ThrowsAsync<RestException>(() => rest.DeleteUserMessageReactionAsync(123456789, 1, "👍", alice, cancellationToken: Ct));
        var unknownMessage = await Assert.ThrowsAsync<RestException>(() => rest.DeleteUserMessageReactionAsync(general.Id, 424242, "👍", alice, cancellationToken: Ct));
        var forbidden = await Assert.ThrowsAsync<RestException>(() => rest.DeleteUserMessageReactionAsync(secret.Id, hidden, "👍", alice, cancellationToken: Ct));

        AssertError(unknownChannel, HttpStatusCode.NotFound, 10003);
        AssertError(unknownMessage, HttpStatusCode.NotFound, 10008);
        AssertError(forbidden, HttpStatusCode.Forbidden, 50001);
        Assert.Single(discord.State.FindMessage(secret.Id, hidden)!.Reactions);
    }

    [Fact]
    [Parity("DELETE /channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}/{user_id}")]
    [Parity("50035")]
    [Parity("10014")]
    public async Task SendRequest_DeleteUserReactionWithAMalformedUserIdOrEmoji_Returns50035Or10014()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var message = general.Message("hello", guild.Member("alice"));
        using var rest = discord.CreateRestClient();

        var badUser = await Assert.ThrowsAsync<RestException>(async () =>
        {
            await using var stream = await rest.SendRequestAsync(HttpMethod.Delete, $"/channels/{general.Id}/messages/{message}/reactions/{"👍"}/{"abc"}", cancellationToken: Ct);
        });
        var badEmoji = await Assert.ThrowsAsync<RestException>(async () =>
        {
            await using var stream = await rest.SendRequestAsync(HttpMethod.Delete, $"/channels/{general.Id}/messages/{message}/reactions/{"nope"}/{123UL}", cancellationToken: Ct);
        });

        Assert.Equal("NUMBER_TYPE_COERCE", Assert.Single(FieldErrors(badUser, "user_id")).Code);
        AssertError(badEmoji, HttpStatusCode.NotFound, 10014);
    }
}
