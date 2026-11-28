using System.Net;
using Enx.NetCord.Testing.State;
using NetCord.Rest;
using Xunit;
using static Enx.NetCord.Testing.Tests.Rest.Messages.MessagesTestSupport;
using static Enx.NetCord.Testing.Tests.State.StateTestSupport;

namespace Enx.NetCord.Testing.Tests.Rest.Reactions;

public sealed class AddReactionTests
{
    private const ulong CustomEmojiId = 123456789012345678;

    [Fact]
    [Parity("PUT /channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}/@me")]
    public async Task AddMessageReactionAsync_UnicodeEmoji_StoresItAndPublishesReactionAdded()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var alice = guild.Member("alice");
        var message = general.Message("react to me", alice);
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        await rest.AddMessageReactionAsync(general.Id, message, "👍", cancellationToken: Ct);

        var read = await rest.GetMessageAsync(general.Id, message, cancellationToken: Ct);
        var reaction = Assert.Single(read.Reactions);
        Assert.Equal("👍", reaction.Emoji.Name);
        Assert.Null(reaction.Emoji.Id);
        Assert.Equal((1, true, false), (reaction.Count, reaction.Me, reaction.MeBurst));
        Assert.Equal((1, 0), (reaction.CountDetails.Normal, reaction.CountDetails.Burst));
        var added = Assert.IsType<ReactionAdded>(Assert.Single(Drain(subscription)));
        Assert.Equal((guild.Id, general.Id, message, alice, discord.ApplicationId), (added.GuildId, added.ChannelId, added.MessageId, added.MessageAuthorId, added.UserId));
        Assert.Equal(new ReactionEmoji("👍", null), added.Emoji);
        Assert.False(added.Burst);
        Assert.Equal(discord.ApplicationId, added.Member?.UserId);
        discord.Recorded.ShouldHaveRequestedOnce($"PUT /channels/{general.Id}/messages/{message}/reactions/👍/@me");
        Assert.Equal(HttpStatusCode.NoContent, discord.Recorded.Requests.First(r => r.Method == "PUT").StatusCode);
    }

    [Fact]
    [Parity("PUT /channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}/@me")]
    public async Task AddMessageReactionAsync_CustomEmoji_IsStoredByItsId()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var message = general.Message("hello", guild.Member("alice"));
        using var rest = discord.CreateRestClient();

        await rest.AddMessageReactionAsync(general.Id, message, new ReactionEmojiProperties("wave", CustomEmojiId), cancellationToken: Ct);
        await rest.AddMessageReactionAsync(general.Id, message, new ReactionEmojiProperties("renamed", CustomEmojiId), cancellationToken: Ct);

        var reaction = Assert.Single((await rest.GetMessageAsync(general.Id, message, cancellationToken: Ct)).Reactions);
        Assert.Equal((CustomEmojiId, "wave"), (reaction.Emoji.Id, reaction.Emoji.Name));
        Assert.Equal(1, reaction.Count);
    }

    [Fact]
    [Parity("PUT /channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}/@me")]
    public async Task AddMessageReactionAsync_SameEmojiTwice_Returns204AndCommitsOnce()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();
        var message = await rest.SendMessageAsync(general.Id, "x", cancellationToken: Ct);
        await rest.AddMessageReactionAsync(general.Id, message.Id, "🔥", cancellationToken: Ct);
        using var subscription = discord.State.SubscribeGateway();

        await rest.AddMessageReactionAsync(general.Id, message.Id, "🔥", cancellationToken: Ct);

        Assert.Empty(Drain(subscription));
        Assert.Equal(1, Assert.Single((await rest.GetMessageAsync(general.Id, message.Id, cancellationToken: Ct)).Reactions).Count);
        Assert.All(discord.Recorded.Requests.Where(r => r.Method == "PUT"), r => Assert.Equal(HttpStatusCode.NoContent, r.StatusCode));
    }

    [Fact]
    [Parity("PUT /channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}/@me")]
    public async Task AddMessageReactionAsync_OtherUsersAlreadyReacted_AddsTheBotToTheCount()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var alice = guild.Member("alice");
        var message = general.Message("hello", alice);
        general.Reaction(message, "👍", alice);
        general.Reaction(message, "👍", guild.Member("bob"));
        using var rest = discord.CreateRestClient();

        var before = Assert.Single((await rest.GetMessageAsync(general.Id, message, cancellationToken: Ct)).Reactions);
        await rest.AddMessageReactionAsync(general.Id, message, "👍", cancellationToken: Ct);
        var after = Assert.Single((await rest.GetMessageAsync(general.Id, message, cancellationToken: Ct)).Reactions);

        Assert.Equal((2, false), (before.Count, before.Me));
        Assert.Equal((3, true), (after.Count, after.Me));
    }

    [Fact]
    [Parity("PUT /channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}/@me")]
    public async Task AddMessageReactionAsync_DirectMessageChannel_IsAccepted()
    {
        await using var discord = FakeDiscord.Create();
        var aliceId = discord.Seed.User("alice");
        using var rest = discord.CreateRestClient();
        var dm = await rest.GetDMChannelAsync(aliceId, cancellationToken: Ct);
        var sent = await rest.SendMessageAsync(dm.Id, "hi", cancellationToken: Ct);

        await rest.AddMessageReactionAsync(dm.Id, sent.Id, "👋", cancellationToken: Ct);

        Assert.Single((await rest.GetMessageAsync(dm.Id, sent.Id, cancellationToken: Ct)).Reactions);
    }

    [Fact]
    [Parity("PUT /channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}/@me")]
    [Parity("30010")]
    public async Task AddMessageReactionAsync_TwentyFirstDistinctEmoji_Returns400MaximumReactionsReached()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var alice = guild.Member("alice");
        var message = general.Message("popular", alice);
        for (var i = 0; i < FakeDiscordState.MaxDistinctReactions; i++)
        {
            general.Reaction(message, char.ConvertFromUtf32(0x1F600 + i), alice);
        }

        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.AddMessageReactionAsync(general.Id, message, "🚀", cancellationToken: Ct));
        await rest.AddMessageReactionAsync(general.Id, message, char.ConvertFromUtf32(0x1F600), cancellationToken: Ct);

        AssertError(exception, HttpStatusCode.BadRequest, 30010);
        var reactions = (await rest.GetMessageAsync(general.Id, message, cancellationToken: Ct)).Reactions;
        Assert.Equal(FakeDiscordState.MaxDistinctReactions, reactions.Count);
        Assert.DoesNotContain(reactions, r => r.Emoji.Name == "🚀");
        Assert.Equal(2, Assert.Single(reactions, r => r.Emoji.Name == char.ConvertFromUtf32(0x1F600)).Count);
        Assert.IsType<ReactionAdded>(Assert.Single(Drain(subscription)));
    }

    [Fact]
    [Parity("PUT /channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}/@me")]
    [Parity("10003")]
    [Parity("10008")]
    [Parity("50001")]
    public async Task AddMessageReactionAsync_UnknownChannelMessageOrGuildTheBotIsNotIn_ReturnsTheMatchingError()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        var secret = discord.Seed.Guild("Elsewhere", withBot: false).TextChannel("secret");
        var hidden = secret.Message("hidden", discord.Seed.User("alice"));
        using var rest = discord.CreateRestClient();

        var unknownChannel = await Assert.ThrowsAsync<RestException>(() => rest.AddMessageReactionAsync(123456789, 1, "👍", cancellationToken: Ct));
        var unknownMessage = await Assert.ThrowsAsync<RestException>(() => rest.AddMessageReactionAsync(general.Id, 424242, "👍", cancellationToken: Ct));
        var forbidden = await Assert.ThrowsAsync<RestException>(() => rest.AddMessageReactionAsync(secret.Id, hidden, "👍", cancellationToken: Ct));

        AssertError(unknownChannel, HttpStatusCode.NotFound, 10003);
        AssertError(unknownMessage, HttpStatusCode.NotFound, 10008);
        AssertError(forbidden, HttpStatusCode.Forbidden, 50001);
        Assert.Empty(discord.State.FindMessage(secret.Id, hidden)!.Reactions);
    }

    [Fact]
    [Parity("PUT /channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}/@me")]
    [Parity("10008")]
    public async Task AddMessageReactionAsync_MessageOfAnotherChannel_Returns10008()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var other = guild.TextChannel("other");
        var message = other.Message("elsewhere", guild.Member("alice"));
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.AddMessageReactionAsync(general.Id, message, "👍", cancellationToken: Ct));

        AssertError(exception, HttpStatusCode.NotFound, 10008);
        Assert.Empty(discord.State.FindMessage(other.Id, message)!.Reactions);
    }

    [Theory]
    [Parity("PUT /channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}/@me")]
    [Parity("10014")]
    [InlineData("abc")]
    [InlineData("thumbsup")]
    [InlineData("1")]
    [InlineData(":123456789012345678")]
    [InlineData("wave:")]
    [InlineData("wave:abc")]
    [InlineData("wave:0")]
    [InlineData("a:wave:123456789012345678")]
    public async Task SendRequest_PutWithASegmentThatIsNoEmoji_Returns404UnknownEmojiAndStoresNothing(string segment)
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var message = general.Message("hello", guild.Member("alice"));
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        var exception = await Assert.ThrowsAsync<RestException>(async () =>
        {
            await using var stream = await rest.SendRequestAsync(HttpMethod.Put, $"/channels/{general.Id}/messages/{message}/reactions/{segment}/@me", cancellationToken: Ct);
        });

        AssertError(exception, HttpStatusCode.NotFound, 10014);
        Assert.Empty(discord.State.FindMessage(general.Id, message)!.Reactions);
        Assert.Empty(Drain(subscription));
    }

    [Theory]
    [Parity("PUT /channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}/@me")]
    [InlineData("1️⃣")]
    [InlineData("👨‍👩‍👧‍👦")]
    [InlineData("🇫🇷")]
    [InlineData("❤️")]
    [InlineData("star_struck:123")]
    public async Task SendRequest_PutWithAUnicodeSequenceOrACustomEmoji_IsAccepted(string segment)
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var message = general.Message("hello", guild.Member("alice"));
        using var rest = discord.CreateRestClient();

        await using var stream = await rest.SendRequestAsync(HttpMethod.Put, $"/channels/{general.Id}/messages/{message}/reactions/{segment}/@me", cancellationToken: Ct);

        Assert.Single(discord.State.FindMessage(general.Id, message)!.Reactions);
    }

    [Fact]
    [Parity("PUT /channels/{channel_id}/messages/{message_id}/reactions/{emoji_name}/@me")]
    [Parity("50035")]
    public async Task SendRequest_PutWithMalformedIds_Returns50035BeforeLookingAtTheEmoji()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(async () =>
        {
            await using var stream = await rest.SendRequestAsync(HttpMethod.Put, $"/channels/{general.Id}/messages/{"abc"}/reactions/{"nope"}/@me", cancellationToken: Ct);
        });

        Assert.Equal("NUMBER_TYPE_COERCE", Assert.Single(FieldErrors(exception, "message_id")).Code);
    }
}
