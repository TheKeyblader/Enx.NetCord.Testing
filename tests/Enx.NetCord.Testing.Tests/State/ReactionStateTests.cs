using Enx.NetCord.Testing.State;
using Xunit;
using static Enx.NetCord.Testing.Tests.State.StateTestSupport;

namespace Enx.NetCord.Testing.Tests.State;

public sealed class ReactionStateTests
{
    private static readonly ReactionEmoji Thumbs = new("👍", null);

    private static (FakeDiscordState State, ChannelSnapshot Channel, UserSnapshot Alice, ulong MessageId) Arrange()
    {
        var state = NewState();
        var (_, channel, alice) = Seed(state);
        var message = state.CreateMessage(StateCaller.Fixture, new CreateMessageInput(channel.Id, alice.Id, "hello")).Value;
        return (state, channel, alice, message.Id);
    }

    [Theory]
    [InlineData("👍")]
    [InlineData("1️⃣")]
    [InlineData("👨‍👩‍👧‍👦")]
    [InlineData("🏴󠁧󠁢󠁥󠁮󠁧󠁿")]
    [InlineData("wave:123456789012345678")]
    [InlineData("a_b_9:1")]
    public void TryParse_Emoji_IsAccepted(string segment)
    {
        Assert.True(ReactionEmoji.TryParse(segment, out var emoji));
        Assert.Equal(segment.Contains(':', StringComparison.Ordinal), emoji.Id is not null);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("abc")]
    [InlineData("1")]
    [InlineData("👍 ")]
    [InlineData("👍\n")]
    [InlineData("�")]
    [InlineData(":1")]
    [InlineData("wave:")]
    [InlineData("wave:0")]
    [InlineData("wave:-1")]
    [InlineData("wave:1:2")]
    [InlineData("wa ve:1")]
    [InlineData("wave!:1")]
    [InlineData("a:b:123")]
    public void TryParse_NotAnEmoji_IsRejected(string? segment)
    {
        Assert.False(ReactionEmoji.TryParse(segment, out _));
    }

    [Fact]
    public void TryParse_CustomEmojiTooLongName_IsRejected()
    {
        Assert.False(ReactionEmoji.TryParse(new string('a', 33) + ":1", out _));
        Assert.True(ReactionEmoji.TryParse(new string('a', 32) + ":1", out _));
    }

    [Fact]
    public void Equals_CustomEmojiWithAnotherName_IsTheSameEmoji()
    {
        var first = new ReactionEmoji("wave", 1);
        var renamed = new ReactionEmoji("renamed", 1);

        Assert.Equal(first, renamed);
        Assert.Equal(first.GetHashCode(), renamed.GetHashCode());
        Assert.NotEqual(first, new ReactionEmoji("wave", 2));
        Assert.NotEqual(first, new ReactionEmoji("wave", null));
        Assert.Equal(Thumbs, new ReactionEmoji("👍", null));
    }

    [Fact]
    public void AddReaction_NewReaction_IsVisibleInTheMessageSnapshotAndPublishesOneChange()
    {
        var (state, channel, alice, messageId) = Arrange();
        using var subscription = state.SubscribeGateway();

        var added = state.AddReaction(StateCaller.Fixture, channel.Id, messageId, Thumbs, alice.Id);

        Assert.True(added.Value);
        var reaction = Assert.Single(state.FindMessage(channel.Id, messageId)!.Reactions);
        Assert.Equal((Thumbs, 1, 0), (reaction.Emoji, reaction.NormalCount, reaction.BurstCount));
        var change = Assert.IsType<ReactionAdded>(Assert.Single(Drain(subscription)));
        Assert.Equal((alice.Id, messageId, alice.Id), (change.UserId, change.MessageId, change.MessageAuthorId));
        Assert.Equal(alice.Id, change.Member?.UserId);
    }

    [Fact]
    public void AddReaction_SameUserTwice_IsAnIdempotentNoOp()
    {
        var (state, channel, alice, messageId) = Arrange();
        state.AddReaction(StateCaller.Fixture, channel.Id, messageId, Thumbs, alice.Id);
        using var subscription = state.SubscribeGateway();

        var again = state.AddReaction(StateCaller.Fixture, channel.Id, messageId, Thumbs, alice.Id);

        Assert.False(again.Value);
        Assert.Empty(Drain(subscription));
        Assert.Equal(1, Assert.Single(state.FindMessage(channel.Id, messageId)!.Reactions).NormalCount);
    }

    [Fact]
    public void AddReaction_NormalAndBurstOfTheSameUser_AreTwoReactions()
    {
        var (state, channel, alice, messageId) = Arrange();

        state.AddReaction(StateCaller.Fixture, channel.Id, messageId, Thumbs, alice.Id);
        var burst = state.AddReaction(StateCaller.Fixture, channel.Id, messageId, Thumbs, alice.Id, burst: true);

        Assert.True(burst.Value);
        var reaction = Assert.Single(state.FindMessage(channel.Id, messageId)!.Reactions);
        Assert.Equal((1, 1), (reaction.NormalCount, reaction.BurstCount));
    }

    [Fact]
    public void AddReaction_UnknownUser_FailsWithUnknownUserAndWritesNothing()
    {
        var (state, channel, _, messageId) = Arrange();

        var result = state.AddReaction(StateCaller.Fixture, channel.Id, messageId, Thumbs, 424242);

        Assert.Equal(StateFailureKind.UnknownUser, result.Failure.Kind);
        Assert.Empty(state.FindMessage(channel.Id, messageId)!.Reactions);
    }

    [Fact]
    public void AddReaction_UnknownMessage_FailsWithUnknownMessage()
    {
        var (state, channel, alice, _) = Arrange();

        var result = state.AddReaction(StateCaller.Fixture, channel.Id, 424242, Thumbs, alice.Id);

        Assert.Equal(StateFailureKind.UnknownMessage, result.Failure.Kind);
    }

    [Fact]
    public void AddReaction_TheTwentyFirstDistinctEmoji_FailsWithMaxReactionsButAnExistingOneStillWorks()
    {
        var (state, channel, alice, messageId) = Arrange();
        for (var i = 0; i < FakeDiscordState.MaxDistinctReactions; i++)
        {
            state.AddReaction(StateCaller.Fixture, channel.Id, messageId, new ReactionEmoji(char.ConvertFromUtf32(0x1F600 + i), null), alice.Id);
        }

        var refused = state.AddReaction(StateCaller.Fixture, channel.Id, messageId, new ReactionEmoji("🚀", null), alice.Id);
        var other = state.CreateUser(new CreateUserInput("bob"));
        var existing = state.AddReaction(StateCaller.Fixture, channel.Id, messageId, new ReactionEmoji(char.ConvertFromUtf32(0x1F600), null), other.Id);

        Assert.Equal(StateFailureKind.MaxReactions, refused.Failure.Kind);
        Assert.True(existing.Value);
        Assert.Equal(FakeDiscordState.MaxDistinctReactions, state.FindMessage(channel.Id, messageId)!.Reactions.Length);
    }

    [Fact]
    public void RemoveReaction_LastReactor_RemovesTheEmojiFromTheMessage()
    {
        var (state, channel, alice, messageId) = Arrange();
        state.AddReaction(StateCaller.Fixture, channel.Id, messageId, Thumbs, alice.Id);
        using var subscription = state.SubscribeGateway();

        var removed = state.RemoveReaction(StateCaller.Fixture, channel.Id, messageId, Thumbs, alice.Id);

        Assert.True(removed.Value);
        Assert.Empty(state.FindMessage(channel.Id, messageId)!.Reactions);
        Assert.IsType<ReactionRemoved>(Assert.Single(Drain(subscription)));
    }

    [Fact]
    public void RemoveReaction_OnlyABurstReactionExists_FailsWithUnknownEmoji()
    {
        var (state, channel, alice, messageId) = Arrange();
        state.AddReaction(StateCaller.Fixture, channel.Id, messageId, Thumbs, alice.Id, burst: true);

        var result = state.RemoveReaction(StateCaller.Fixture, channel.Id, messageId, Thumbs, alice.Id);

        Assert.Equal(StateFailureKind.UnknownEmoji, result.Failure.Kind);
        Assert.Equal(1, Assert.Single(state.FindMessage(channel.Id, messageId)!.Reactions).BurstCount);
    }

    [Fact]
    public void ListReactionUsers_AfterAndLimit_PageByAscendingUserId()
    {
        var (state, channel, alice, messageId) = Arrange();
        var bob = state.CreateUser(new CreateUserInput("bob"));
        var carol = state.CreateUser(new CreateUserInput("carol"));
        foreach (var user in new[] { carol, alice, bob })
        {
            state.AddReaction(StateCaller.Fixture, channel.Id, messageId, Thumbs, user.Id);
        }

        var all = state.ListReactionUsers(StateCaller.Fixture, channel.Id, messageId, Thumbs, burst: false, after: null, limit: 10).Value;
        var page = state.ListReactionUsers(StateCaller.Fixture, channel.Id, messageId, Thumbs, burst: false, after: alice.Id, limit: 1).Value;
        var beyond = state.ListReactionUsers(StateCaller.Fixture, channel.Id, messageId, Thumbs, burst: false, after: ulong.MaxValue, limit: 10).Value;

        Assert.Equal([alice.Id, bob.Id, carol.Id], all.Select(u => u.Id));
        Assert.Equal([bob.Id], page.Select(u => u.Id));
        Assert.Empty(beyond);
    }

    [Fact]
    public void ClearReactions_ThenClearEmoji_PublishOnlyEffectiveChanges()
    {
        var (state, channel, alice, messageId) = Arrange();
        state.AddReaction(StateCaller.Fixture, channel.Id, messageId, Thumbs, alice.Id);
        state.AddReaction(StateCaller.Fixture, channel.Id, messageId, new ReactionEmoji("🔥", null), alice.Id);
        using var subscription = state.SubscribeGateway();

        var emoji = state.ClearEmojiReactions(StateCaller.Fixture, channel.Id, messageId, Thumbs);
        var all = state.ClearReactions(StateCaller.Fixture, channel.Id, messageId);
        var none = state.ClearReactions(StateCaller.Fixture, channel.Id, messageId);
        var missing = state.ClearEmojiReactions(StateCaller.Fixture, channel.Id, messageId, Thumbs);

        Assert.True(emoji.Value);
        Assert.True(all.Value);
        Assert.False(none.Value);
        Assert.Equal(StateFailureKind.UnknownEmoji, missing.Failure.Kind);
        Assert.Collection(
            Drain(subscription),
            change => Assert.IsType<ReactionEmojiCleared>(change),
            change => Assert.IsType<ReactionsCleared>(change));
    }

    [Fact]
    public void AddReaction_BotCaller_OnAGuildTheBotIsNotIn_FailsWithMissingAccess()
    {
        var state = NewState();
        var bot = state.CreateBotIdentity(new CreateBotIdentityInput("bot"));
        var guild = state.CreateGuild(new CreateGuildInput("Elsewhere", WithBot: false)).Value.Guild;
        var channel = state.CreateGuildChannel(StateCaller.Fixture, new CreateGuildChannelInput(guild.Id, ChannelKind.GuildText, "secret")).Value;
        var alice = state.CreateUser(new CreateUserInput("alice"));
        var message = state.CreateMessage(StateCaller.Fixture, new CreateMessageInput(channel.Id, alice.Id, "hidden")).Value;

        var result = state.AddReaction(StateCaller.Bot, channel.Id, message.Id, Thumbs, bot.User.Id);

        Assert.Equal(StateFailureKind.MissingAccess, result.Failure.Kind);
        Assert.Empty(state.FindMessage(channel.Id, message.Id)!.Reactions);
    }

    [Fact]
    public void MessageSnapshot_Reactions_MeIsRelativeToTheBot()
    {
        var state = NewState();
        var bot = state.CreateBotIdentity(new CreateBotIdentityInput("bot"));
        var guild = state.CreateGuild(new CreateGuildInput("Test", WithBot: true)).Value.Guild;
        var channel = state.CreateGuildChannel(StateCaller.Fixture, new CreateGuildChannelInput(guild.Id, ChannelKind.GuildText, "general")).Value;
        var alice = state.CreateUser(new CreateUserInput("alice"));
        var message = state.CreateMessage(StateCaller.Fixture, new CreateMessageInput(channel.Id, alice.Id, "hello")).Value;
        state.AddReaction(StateCaller.Fixture, channel.Id, message.Id, Thumbs, alice.Id);
        state.AddReaction(StateCaller.Fixture, channel.Id, message.Id, Thumbs, bot.User.Id, burst: true);

        var reaction = Assert.Single(state.GetMessage(StateCaller.Bot, channel.Id, message.Id).Value.Reactions);

        Assert.Equal((false, true), (reaction.Me, reaction.MeBurst));
    }
}
