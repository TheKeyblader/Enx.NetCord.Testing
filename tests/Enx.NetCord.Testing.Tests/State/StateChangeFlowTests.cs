using Enx.NetCord.Testing.State;
using Xunit;
using static Enx.NetCord.Testing.Tests.State.StateTestSupport;

namespace Enx.NetCord.Testing.Tests.State;

public sealed class StateChangeFlowTests
{
    [Fact]
    public void Mutations_WhenSubscribed_ProduceOneConsecutivelyNumberedChangeEach()
    {
        var state = NewState();
        using var subscription = state.SubscribeGateway();

        var bot = state.CreateBotIdentity(new CreateBotIdentityInput("bot"));
        var (guild, channel, user) = Seed(state);
        var role = state.CreateRole(new CreateRoleInput(guild.Id, "r")).Value;
        var message = state.CreateMessage(new CreateMessageInput(channel.Id, user.Id, "v1")).Value;
        state.EditMessageContent(channel.Id, message.Id, "v2");
        state.DeleteMessage(channel.Id, message.Id);

        var changes = Drain(subscription);

        Assert.Equal(Enumerable.Range(1, changes.Count).Select(i => (long)i), changes.Select(c => c.Number));
        Assert.Collection(
            changes,
            c => Assert.Equal(bot.User, Assert.IsType<BotIdentityCreated>(c).User),
            c => Assert.Equal(guild.Id, Assert.IsType<GuildCreated>(c).Everyone.Id),
            c => Assert.Equal(channel, Assert.IsType<ChannelCreated>(c).Channel),
            c => Assert.Equal(user, Assert.IsType<UserCreated>(c).User),
            c => Assert.Equal(user.Id, Assert.IsType<MemberAdded>(c).User.Id),
            c => Assert.Equal(role, Assert.IsType<RoleCreated>(c).Role),
            c => Assert.Equal(message, Assert.IsType<MessageCreated>(c).Message),
            c => Assert.Equal("v2", Assert.IsType<MessageEdited>(c).Message.Content),
            c =>
            {
                var deleted = Assert.IsType<MessageDeleted>(c);
                Assert.Equal((guild.Id, channel.Id, message.Id), (deleted.GuildId, deleted.ChannelId, deleted.MessageId));
            });
    }

    [Fact]
    public void MessageCreated_OfAGuildMember_CarriesAuthorAndMembershipAsOfTheCommit()
    {
        var state = NewState();
        var (_, channel, user) = Seed(state);
        using var subscription = state.SubscribeGateway();

        state.CreateMessage(new CreateMessageInput(channel.Id, user.Id, "hi"));
        state.EditMessageContent(channel.Id, state.ListMessages(channel.Id, 1).Value[0].Id, "edited");

        var changes = Drain(subscription);
        var created = Assert.IsType<MessageCreated>(changes[0]);
        Assert.Equal("hi", created.Message.Content); // not the later edit
        Assert.Null(created.Message.EditedTimestamp);
        Assert.Equal(user, created.Author);
        Assert.Equal(user.Id, created.AuthorMember!.UserId);
        var edited = Assert.IsType<MessageEdited>(changes[1]);
        Assert.Equal("edited", edited.Message.Content);
        Assert.Equal(user, edited.Author);
        Assert.Equal(user.Id, edited.AuthorMember!.UserId);
    }

    [Fact]
    public void MessageEdited_InADmChannel_HasNoAuthorMember()
    {
        var state = NewState();
        var user = state.CreateUser(new CreateUserInput("u"));
        var dm = state.GetOrCreateDmChannel(new CreateDmChannelInput(user.Id)).Value.Channel;
        var message = state.CreateMessage(new CreateMessageInput(dm.Id, user.Id, "a")).Value;
        using var subscription = state.SubscribeGateway();

        state.EditMessageContent(dm.Id, message.Id, "b");

        var edited = Assert.IsType<MessageEdited>(Assert.Single(Drain(subscription)));
        Assert.Null(edited.AuthorMember);
    }

    [Fact]
    public void Reads_Always_ProduceNoChange()
    {
        var state = NewState();
        var (guild, channel, user) = Seed(state);
        var message = state.CreateMessage(new CreateMessageInput(channel.Id, user.Id, "x")).Value;
        using var subscription = state.SubscribeGateway();
        var bound = subscription.Initial.LastChangeNumber;

        state.FindUser(user.Id);
        state.FindGuild(guild.Id);
        state.FindMessage(channel.Id, message.Id);
        state.ListMessages(channel.Id, 10);
        state.ListGuildRoles(guild.Id);
        state.TakeProjection();

        Assert.Empty(Drain(subscription));
        Assert.Equal(bound, state.TakeProjection().LastChangeNumber);
    }

    [Fact]
    public void FailedOperations_ProduceNoChangeAndConsumeNoNumber()
    {
        var state = NewState();
        using var subscription = state.SubscribeGateway();

        state.CreateMessage(new CreateMessageInput(1, 2, "x"));
        Assert.Throws<ArgumentOutOfRangeException>(() => state.CreateUser(new CreateUserInput("a", Id: 0)));
        var user = state.CreateUser(new CreateUserInput("ok"));

        var change = Assert.Single(Drain(subscription));
        Assert.Equal(1, change.Number);
        Assert.Equal(user, Assert.IsType<UserCreated>(change).User);
    }

    [Fact]
    public void Mutations_WithoutSubscriber_RetainNothingButStillAdvanceTheNumber()
    {
        var state = NewState();
        state.CreateUser(new CreateUserInput("a"));
        state.CreateUser(new CreateUserInput("b"));

        using var subscription = state.SubscribeGateway();

        Assert.Empty(Drain(subscription));
        Assert.Equal(2, subscription.Initial.LastChangeNumber);
    }

    [Fact]
    public void Subscribe_AfterSomeState_ReturnsProjectionAndBoundThenOnlyLaterChanges()
    {
        var state = NewState();
        var (guild, channel, user) = Seed(state);

        using var subscription = state.SubscribeGateway();
        var later = state.CreateUser(new CreateUserInput("later"));

        var initial = subscription.Initial;
        Assert.Equal(4, initial.LastChangeNumber);
        Assert.Equal([guild.Id], initial.Guilds.Select(g => g.Id));
        Assert.Equal([channel.Id], initial.Channels.Select(c => c.Id));
        Assert.Equal([guild.Id], initial.Roles.Select(r => r.Id));
        Assert.Equal([user.Id], initial.Members.Select(m => m.UserId));
        Assert.DoesNotContain(initial.Users, u => u.Id == later.Id);
        var change = Assert.Single(Drain(subscription));
        Assert.Equal(initial.LastChangeNumber + 1, change.Number);
    }

    [Fact]
    public void Projection_WithBotIdentity_ExposesIt()
    {
        var state = NewState();
        var bot = state.CreateBotIdentity(new CreateBotIdentityInput("bot"));

        Assert.Equal(bot, state.TakeProjection().Bot);
        Assert.Null(NewState().TakeProjection().Bot);
    }

    [Fact]
    public void SubscribeGateway_WhileSubscribed_Throws()
    {
        var state = NewState();
        using var subscription = state.SubscribeGateway();

        Assert.Throws<InvalidOperationException>(() => state.SubscribeGateway());
    }

    [Fact]
    public async Task Dispose_Subscription_CompletesTheReaderAfterBufferedChangesAndAllowsResubscribing()
    {
        var state = NewState();
        var subscription = state.SubscribeGateway();
        state.CreateUser(new CreateUserInput("a"));

        subscription.Dispose();
        subscription.Dispose(); // idempotent
        state.CreateUser(new CreateUserInput("after"));

        var buffered = await subscription.Reader.ReadAsync(TestContext.Current.CancellationToken);
        Assert.IsType<UserCreated>(buffered);
        await subscription.Reader.Completion.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.False(subscription.Reader.TryRead(out _)); // the change after Dispose was not queued

        using var second = state.SubscribeGateway();
        Assert.Equal(2, second.Initial.LastChangeNumber);
        Assert.Empty(Drain(second));
    }

    [Fact]
    public void Dispose_OldSubscription_DoesNotDetachANewerOne()
    {
        var state = NewState();
        var first = state.SubscribeGateway();
        first.Dispose();
        using var second = state.SubscribeGateway();

        first.Dispose();
        state.CreateUser(new CreateUserInput("a"));

        Assert.Single(Drain(second));
    }

    [Fact]
    public void Subscription_WithManyPendingChanges_NeverBlocksWriters()
    {
        var state = NewState();
        using var subscription = state.SubscribeGateway();

        for (var i = 0; i < 10_000; i++)
        {
            state.CreateUser(new CreateUserInput("u"));
        }

        var changes = Drain(subscription);
        Assert.Equal(10_000, changes.Count);
        Assert.Equal(changes.Select(c => c.Number).OrderBy(n => n), changes.Select(c => c.Number));
    }

    [Fact]
    public async Task Commits_FromManyThreads_KeepTheQueueOrderedByNumber()
    {
        var state = NewState();
        using var subscription = state.SubscribeGateway();

        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < 500; i++)
            {
                state.CreateUser(new CreateUserInput("u"));
            }
        }))).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        var numbers = Drain(subscription).Select(c => c.Number).ToList();
        Assert.Equal(Enumerable.Range(1, 2000).Select(i => (long)i), numbers);
    }
}
