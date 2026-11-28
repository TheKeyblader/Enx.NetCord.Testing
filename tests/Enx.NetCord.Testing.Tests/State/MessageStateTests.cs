using Enx.NetCord.Testing.State;
using Microsoft.Extensions.Time.Testing;
using Xunit;
using static Enx.NetCord.Testing.Tests.State.StateTestSupport;

namespace Enx.NetCord.Testing.Tests.State;

/// <summary>State rules of the message operations that need the clock or an invariant the REST tests cannot reach.</summary>
public sealed class MessageStateTests
{
    [Fact]
    public void CreateMessage_EnforcedNonceAfterTheWindow_CreatesANewMessage()
    {
        var clock = new FakeTimeProvider(Start);
        var state = NewState(clock);
        var (_, channel, user) = Seed(state);
        CreateMessageInput Input(string content) => new(channel.Id, user.Id, content, Nonce: "n", EnforceNonce: true);

        var first = state.CreateMessage(StateCaller.Fixture, Input("a")).Value;
        clock.Advance(TimeSpan.FromMinutes(4));
        var within = state.CreateMessage(StateCaller.Fixture, Input("b")).Value;
        clock.Advance(TimeSpan.FromMinutes(2));
        var after = state.CreateMessage(StateCaller.Fixture, Input("c")).Value;

        Assert.Equal(first.Id, within.Id);
        Assert.NotEqual(first.Id, after.Id);
        Assert.Equal("c", after.Content);
    }

    [Fact]
    public void CreateMessage_SameNonceFromAnotherAuthor_IsNotADuplicate()
    {
        var state = NewState();
        var (guild, channel, alice) = Seed(state);
        var bob = state.CreateUser(new CreateUserInput("bob"));
        state.AddMember(StateCaller.Fixture, new AddMemberInput(guild.Id, bob.Id)).ValueOrThrow();

        var a = state.CreateMessage(StateCaller.Fixture, new CreateMessageInput(channel.Id, alice.Id, "a", Nonce: "n", EnforceNonce: true)).Value;
        var b = state.CreateMessage(StateCaller.Fixture, new CreateMessageInput(channel.Id, bob.Id, "b", Nonce: "n", EnforceNonce: true)).Value;

        Assert.NotEqual(a.Id, b.Id);
    }

    [Fact]
    public void CreateMessage_EnforcedNonceDuplicateInADm_IsProjectedWithTheGuildOfTheDuplicateNotOfTheTarget()
    {
        var state = NewState();
        var (_, channel, user) = Seed(state);
        var dm = state.GetOrCreateDmChannel(new CreateDmChannelInput(user.Id)).Value.Channel;
        var first = state.CreateMessage(StateCaller.Fixture, new CreateMessageInput(dm.Id, user.Id, "a", Nonce: "n", EnforceNonce: true)).Value;

        var duplicate = state.CreateMessage(StateCaller.Fixture, new CreateMessageInput(channel.Id, user.Id, "b", Nonce: "n", EnforceNonce: true)).Value;

        Assert.Equal(first.Id, duplicate.Id);
        Assert.Equal(dm.Id, duplicate.ChannelId);
        Assert.Null(duplicate.GuildId);
    }

    [Fact]
    public void BulkDeleteMessages_ExactlyTwoWeeksOld_IsAccepted()
    {
        var clock = new FakeTimeProvider(Start);
        var state = NewState(clock);
        var (_, channel, user) = Seed(state);
        var boundary = IdAt(Start - FakeDiscordState.BulkDeleteMaxAge);
        var justOver = IdAt(Start - FakeDiscordState.BulkDeleteMaxAge - TimeSpan.FromMilliseconds(1));
        state.CreateMessage(StateCaller.Fixture, new CreateMessageInput(channel.Id, user.Id, "edge", Id: boundary));
        var fresh = state.CreateMessage(StateCaller.Fixture, new CreateMessageInput(channel.Id, user.Id, "fresh")).Value;

        var accepted = state.BulkDeleteMessages(StateCaller.Fixture, channel.Id, [boundary, fresh.Id]);
        var refused = state.BulkDeleteMessages(StateCaller.Fixture, channel.Id, [justOver, fresh.Id]);

        Assert.Equal([boundary, fresh.Id], accepted.Value.AsEnumerable());
        Assert.Equal(StateFailureKind.MessageTooOldForBulkDelete, refused.Failure.Kind);
        Assert.Equal(justOver, refused.Failure.ResourceId);
    }

    [Fact]
    public void BulkDeleteMessages_RepeatedIds_AreDeletedAndReportedOnce()
    {
        var state = NewState();
        var (_, channel, user) = Seed(state);
        var message = state.CreateMessage(StateCaller.Fixture, new CreateMessageInput(channel.Id, user.Id, "x")).Value;
        using var subscription = state.SubscribeGateway();

        var deleted = state.BulkDeleteMessages(StateCaller.Fixture, channel.Id, [message.Id, message.Id]).Value;

        Assert.Equal([message.Id], deleted.AsEnumerable());
        var change = Assert.IsType<MessagesBulkDeleted>(Assert.Single(Drain(subscription)));
        Assert.Equal([message.Id], change.MessageIds.AsEnumerable());
    }

    [Fact]
    public void BulkDeleteMessages_NothingToDelete_CommitsNothing()
    {
        var state = NewState();
        var (_, channel, _) = Seed(state);
        using var subscription = state.SubscribeGateway();

        var deleted = state.BulkDeleteMessages(StateCaller.Fixture, channel.Id, [IdAt(Start, 1), IdAt(Start, 2)]).Value;

        Assert.Empty(deleted);
        Assert.Empty(Drain(subscription));
    }

    [Fact]
    public void ListMessages_Around_ReturnsHalfBeforeThenTheTargetAndTheFollowingOnes()
    {
        var state = NewState();
        var (_, channel, user) = Seed(state);
        var ids = Enumerable.Range(1, 7)
            .Select(i => state.CreateMessage(StateCaller.Fixture, new CreateMessageInput(channel.Id, user.Id, $"m{i}", Id: (ulong)i * 10)).Value.Id)
            .ToArray();

        Assert.Equal([60UL, 50, 40, 30, 20], Ids(state.ListMessages(StateCaller.Fixture, channel.Id, 5, around: 40)));
        Assert.Equal([70UL, 60, 50, 40, 30], Ids(state.ListMessages(StateCaller.Fixture, channel.Id, 5, around: 45)));
        Assert.Equal([30UL, 20, 10], Ids(state.ListMessages(StateCaller.Fixture, channel.Id, 3, around: 0)));
        Assert.Equal(ids.Length, state.ListMessages(StateCaller.Fixture, channel.Id, 100, around: 1000).Value.Length);
        Assert.Throws<ArgumentException>(() => state.ListMessages(StateCaller.Fixture, channel.Id, 5, before: 1, around: 2));
    }

    [Fact]
    public void CrosspostMessage_TextChannelAndDm_AreRefusedWithoutAChange()
    {
        var state = NewState();
        var (_, channel, user) = Seed(state);
        var message = state.CreateMessage(StateCaller.Fixture, new CreateMessageInput(channel.Id, user.Id, "x")).Value;
        var dm = state.GetOrCreateDmChannel(new CreateDmChannelInput(user.Id)).Value.Channel;
        var dmMessage = state.CreateMessage(StateCaller.Fixture, new CreateMessageInput(dm.Id, user.Id, "y")).Value;
        using var subscription = state.SubscribeGateway();

        Assert.Equal(StateFailureKind.CannotExecuteOnChannelType, state.CrosspostMessage(StateCaller.Fixture, channel.Id, message.Id).Failure.Kind);
        Assert.Equal(StateFailureKind.CannotExecuteInDm, state.CrosspostMessage(StateCaller.Fixture, dm.Id, dmMessage.Id).Failure.Kind);
        Assert.Equal(StateFailureKind.UnknownMessage, state.CrosspostMessage(StateCaller.Fixture, channel.Id, 999).Failure.Kind);
        Assert.Empty(Drain(subscription));
    }

    [Fact]
    public void EditMessage_ByTheBotOnAnotherAuthorsMessage_IsRefusedButAFixtureMayEdit()
    {
        var state = NewState();
        state.CreateBotIdentity(new CreateBotIdentityInput("bot", Id: 42));
        var (guild, channel, alice) = Seed(state);
        state.AddMember(StateCaller.Fixture, new AddMemberInput(guild.Id, 42)).ValueOrThrow();
        var message = state.CreateMessage(StateCaller.Fixture, new CreateMessageInput(channel.Id, alice.Id, "theirs")).Value;

        var asBot = state.EditMessage(StateCaller.Bot, new EditMessageInput(channel.Id, message.Id, ChangeContent: true, Content: "mine"));
        var asFixture = state.EditMessage(StateCaller.Fixture, new EditMessageInput(channel.Id, message.Id, ChangeContent: true, Content: "fixed"));

        Assert.Equal(StateFailureKind.CannotEditOtherAuthor, asBot.Failure.Kind);
        Assert.Equal("fixed", asFixture.Value.Content);
    }

    private static List<ulong> Ids(StateResult<System.Collections.Immutable.ImmutableArray<MessageSnapshot>> result)
        => [.. result.Value.Select(m => m.Id)];
}
