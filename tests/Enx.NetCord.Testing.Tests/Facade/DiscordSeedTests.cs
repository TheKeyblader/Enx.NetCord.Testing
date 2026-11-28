using Enx.NetCord.Testing.State;
using Enx.NetCord.Testing.Tests.State;
using Xunit;

namespace Enx.NetCord.Testing.Tests.Facade;

public sealed class DiscordSeedTests
{
    [Fact]
    public async Task Guild_Nominal_CreatesTheGuildAndItsEveryoneRoleWithTheGuildId()
    {
        await using var discord = FakeDiscord.Create();

        var guild = discord.Seed.Guild("Test");

        var snapshot = discord.State.FindGuild(guild.Id)!;
        var everyone = discord.State.FindRole(guild.Id)!;
        Assert.Equal("Test", snapshot.Name);
        Assert.True(everyone.IsEveryone);
        Assert.Equal("@everyone", everyone.Name);
        Assert.Equal(guild.Id, everyone.GuildId);
    }

    [Fact]
    public async Task Guild_ExplicitId_UsesItAndDerivesTheCreationDateFromTheSnowflake()
    {
        await using var discord = FakeDiscord.Create();
        var historic = new DateTimeOffset(2019, 5, 6, 7, 8, 9, 10, TimeSpan.Zero);
        var id = StateTestSupport.IdAt(historic, counter: 7);

        var guild = discord.Seed.Guild("Old", id);

        Assert.Equal(id, guild.Id);
        Assert.Equal(historic, discord.State.FindGuild(id)!.CreatedAt);
    }

    [Fact]
    public async Task Guild_ZeroId_ThrowsArgumentOutOfRangeExceptionWithoutCreatingAnything()
    {
        await using var discord = FakeDiscord.Create();
        using var subscription = discord.State.SubscribeGateway();

        Assert.Throws<ArgumentOutOfRangeException>(() => discord.Seed.Guild("Zero", id: 0));

        Assert.Empty(StateTestSupport.Drain(subscription));
        Assert.Empty(subscription.Initial.Guilds);
    }

    [Fact]
    public async Task Guild_NullName_ThrowsArgumentNullException()
    {
        await using var discord = FakeDiscord.Create();

        Assert.Throws<ArgumentNullException>(() => discord.Seed.Guild(null!));
    }

    [Fact]
    public async Task Guild_Nominal_CommitsGuildCreatedWithoutAnyRestRequest()
    {
        await using var discord = FakeDiscord.Create();
        using var subscription = discord.State.SubscribeGateway();

        var guild = discord.Seed.Guild("Test");

        var created = Assert.IsType<GuildCreated>(Assert.Single(StateTestSupport.Drain(subscription)));
        Assert.Equal(guild.Id, created.Guild.Id);
        Assert.Empty(discord.Journal.Snapshot());
    }

    [Fact]
    public async Task Guild_Default_TheBotIsAnOrdinaryMemberFromTheSameCommit()
    {
        await using var discord = FakeDiscord.Create();
        using var subscription = discord.State.SubscribeGateway();

        var guild = discord.Seed.Guild("Test");

        var created = Assert.IsType<GuildCreated>(Assert.Single(StateTestSupport.Drain(subscription)));
        var membership = discord.State.FindMember(guild.Id, discord.ApplicationId)!;
        Assert.Equal(membership, created.BotMember);
        Assert.Empty(membership.RoleIds);
        Assert.Equal(created.Guild.CreatedAt, membership.JoinedAt); // virtual clock: creation and join share the instant
        Assert.Equal(
            discord.ApplicationId,
            Assert.Single(discord.State.ListGuildMembers(StateCaller.Fixture, guild.Id).Value).UserId);
        Assert.NotNull(discord.State.GetGuild(StateCaller.Bot, guild.Id).Value);
    }

    [Fact]
    public async Task Guild_WithBotFalse_TheBotCannotSeeItButTheSeedStillFillsIt()
    {
        await using var discord = FakeDiscord.Create();
        using var subscription = discord.State.SubscribeGateway();

        var elsewhere = discord.Seed.Guild("Elsewhere", withBot: false);
        var channel = elsewhere.TextChannel("general");
        var carol = elsewhere.Member("carol");
        channel.Message("hi", carol);

        Assert.Null(Assert.IsType<GuildCreated>(StateTestSupport.Drain(subscription)[0]).BotMember);
        Assert.Null(discord.State.FindMember(elsewhere.Id, discord.ApplicationId));
        Assert.Equal(StateFailureKind.UnknownGuild, discord.State.GetGuild(StateCaller.Bot, elsewhere.Id).Failure.Kind);
        Assert.Equal(StateFailureKind.MissingAccess, discord.State.GetChannel(StateCaller.Bot, channel.Id).Failure.Kind);
        Assert.Equal([carol], discord.State.ListGuildMembers(StateCaller.Fixture, elsewhere.Id).Value.Select(m => m.UserId));
    }

    [Fact]
    public async Task Guild_WithOrWithoutBot_AllocatesTheSameIds()
    {
        await using var with = FakeDiscord.Create();
        await using var without = FakeDiscord.Create();

        var a = with.Seed.Guild("g");
        var b = without.Seed.Guild("g", withBot: false);

        Assert.Equal(b.Id, a.Id);
        Assert.Equal(without.Seed.User("u"), with.Seed.User("u"));
    }

    [Fact]
    public async Task MemberById_BotJoinsAGuildCreatedWithoutIt_BecomesVisibleAndPublishesTheJoin()
    {
        await using var discord = FakeDiscord.Create();
        var elsewhere = discord.Seed.Guild("Elsewhere", withBot: false);
        using var subscription = discord.State.SubscribeGateway();

        var returned = elsewhere.Member(discord.ApplicationId);

        Assert.Equal(discord.ApplicationId, returned);
        Assert.NotNull(discord.State.GetGuild(StateCaller.Bot, elsewhere.Id).Value);
        var added = Assert.IsType<MemberAdded>(Assert.Single(StateTestSupport.Drain(subscription)));
        Assert.Equal(discord.ApplicationId, added.Member.UserId);
        Assert.True(added.User.IsBot);
    }

    [Fact]
    public async Task MemberById_BotAlreadyInTheGuild_ThrowsWithoutMutationOrNotification()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        using var subscription = discord.State.SubscribeGateway();

        var exception = Assert.Throws<InvalidOperationException>(() => guild.Member(discord.ApplicationId));

        Assert.Contains("already a member", exception.Message, StringComparison.Ordinal);
        Assert.Empty(StateTestSupport.Drain(subscription));
    }

    [Fact]
    public async Task ApplicationId_Always_IsTheBotAndApplicationIdAndSurvivesDisposal()
    {
        var discord = FakeDiscord.Create(new FakeDiscordOptions { ApplicationId = 42 });
        var identity = discord.State.FindBotIdentity()!;

        await discord.DisposeAsync();

        Assert.Equal(42UL, discord.ApplicationId);
        Assert.Equal(discord.ApplicationId, identity.User.Id);
        Assert.Equal(discord.ApplicationId, identity.Application.Id);
    }

    [Fact]
    public async Task User_Nominal_CreatesTheUser()
    {
        await using var discord = FakeDiscord.Create();

        var id = discord.Seed.User("bob");

        var user = discord.State.FindUser(id)!;
        Assert.Equal("bob", user.Username);
        Assert.False(user.IsBot);
    }

    [Fact]
    public async Task TextChannel_Nominal_CreatesTheChannelInTheGuild()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");

        var channel = guild.TextChannel("general");

        var snapshot = discord.State.FindChannel(channel.Id)!;
        Assert.Equal("general", snapshot.Name);
        Assert.Equal(guild.Id, snapshot.GuildId);
        Assert.Equal(ChannelKind.GuildText, snapshot.Kind);
    }

    [Fact]
    public async Task TextChannel_UnknownGuild_ThrowsInvalidOperationExceptionWithoutMutationOrNotification()
    {
        await using var discord = FakeDiscord.Create();
        var orphan = new SeededGuild(discord, 999);
        using var subscription = discord.State.SubscribeGateway();

        var exception = Assert.Throws<InvalidOperationException>(() => orphan.TextChannel("general"));

        Assert.Contains("999", exception.Message, StringComparison.Ordinal);
        Assert.Empty(StateTestSupport.Drain(subscription));
        Assert.Empty(subscription.Initial.Channels);
    }

    [Fact]
    public async Task Role_Nominal_CreatesTheRoleInTheGuild()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");

        var roleId = guild.Role("reader");

        var role = discord.State.FindRole(roleId)!;
        Assert.Equal("reader", role.Name);
        Assert.Equal(guild.Id, role.GuildId);
        Assert.False(role.IsEveryone);
    }

    [Fact]
    public async Task Role_UnknownGuild_ThrowsInvalidOperationException()
    {
        await using var discord = FakeDiscord.Create();

        Assert.Throws<InvalidOperationException>(() => new SeededGuild(discord, 999).Role("reader"));
    }

    [Fact]
    public async Task MemberByName_Nominal_CreatesTheUserAndTheMembershipAtomically()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        using var subscription = discord.State.SubscribeGateway();

        var alice = guild.Member("alice");

        Assert.Equal("alice", discord.State.FindUser(alice)!.Username);
        Assert.NotNull(discord.State.FindMember(guild.Id, alice));
        Assert.Collection(
            StateTestSupport.Drain(subscription),
            change => Assert.Equal(alice, Assert.IsType<UserCreated>(change).User.Id),
            change => Assert.Equal(alice, Assert.IsType<MemberAdded>(change).Member.UserId));
    }

    [Fact]
    public async Task MemberByName_CollidingExplicitId_ThrowsAndCreatesNeitherUserNorMembership()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var taken = discord.Seed.User("first", id: 5);
        using var subscription = discord.State.SubscribeGateway();

        Assert.Throws<InvalidOperationException>(() => guild.Member("second", id: taken));

        Assert.Equal("first", discord.State.FindUser(taken)!.Username);
        Assert.Null(discord.State.FindMember(guild.Id, taken));
        Assert.Empty(StateTestSupport.Drain(subscription));
    }

    [Fact]
    public async Task MemberByName_UnknownGuild_ThrowsBeforeCreatingTheUser()
    {
        await using var discord = FakeDiscord.Create();
        using var subscription = discord.State.SubscribeGateway();

        Assert.Throws<InvalidOperationException>(() => new SeededGuild(discord, 999).Member("alice", id: 77));

        Assert.Null(discord.State.FindUser(77));
        Assert.Empty(StateTestSupport.Drain(subscription));
        // The ID was not consumed by the failed attempt.
        Assert.Equal(77UL, discord.Seed.User("alice", id: 77));
    }

    [Fact]
    public async Task MemberById_ExistingUser_AddsTheMembershipAndReturnsTheSameId()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var bob = discord.Seed.User("bob");

        var returned = guild.Member(bob);

        Assert.Equal(bob, returned);
        Assert.NotNull(discord.State.FindMember(guild.Id, bob));
    }

    [Fact]
    public async Task MemberById_AlreadyAMember_ThrowsWithoutMutationOrNotification()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var alice = guild.Member("alice");
        using var subscription = discord.State.SubscribeGateway();

        Assert.Throws<InvalidOperationException>(() => guild.Member(alice));

        Assert.Empty(StateTestSupport.Drain(subscription));
        // The bot (a member from creation) and alice.
        Assert.Equal(2, discord.State.ListGuildMembers(StateCaller.Fixture, guild.Id).Value.Length);
    }

    [Fact]
    public async Task MemberById_UnknownUser_ThrowsInvalidOperationException()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");

        var exception = Assert.Throws<InvalidOperationException>(() => guild.Member(123456));

        Assert.Contains("123456", exception.Message, StringComparison.Ordinal);
        // Only the bot: the failed call added nobody.
        Assert.Equal(
            [discord.ApplicationId],
            discord.State.ListGuildMembers(StateCaller.Fixture, guild.Id).Value.Select(m => m.UserId));
    }

    [Fact]
    public async Task MemberById_UserOfAnotherFake_IsResolvedInThisFakeOnly()
    {
        await using var other = FakeDiscord.Create();
        await using var discord = FakeDiscord.Create();
        var foreign = other.Seed.User("elsewhere");
        var guild = discord.Seed.Guild("Test");

        // Both fakes are deterministic: the foreign ID is not in this fake, whatever the number looks like.
        Assert.Null(discord.State.FindUser(foreign));
        Assert.Throws<InvalidOperationException>(() => guild.Member(foreign));
    }

    [Fact]
    public async Task Message_Nominal_CreatesTheMessageInTheChannel()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var channel = guild.TextChannel("general");
        var alice = guild.Member("alice");

        var messageId = channel.Message("hello", alice);

        var message = discord.State.FindMessage(channel.Id, messageId)!;
        Assert.Equal("hello", message.Content);
        Assert.Equal(alice, message.AuthorId);
        Assert.Equal(guild.Id, message.GuildId);
    }

    [Fact]
    public async Task Message_ExplicitOldSnowflake_PreservesTheHistoricalTimestamp()
    {
        await using var discord = FakeDiscord.Create();
        var channel = discord.Seed.Guild("Test").TextChannel("general");
        var alice = discord.Seed.User("alice");
        var historic = new DateTimeOffset(2019, 12, 17, 0, 0, 0, TimeSpan.Zero); // older than the fake's 2020 clock
        var oldId = StateTestSupport.IdAt(historic, counter: 1);

        var id = channel.Message("archive", alice, oldId);

        Assert.Equal(oldId, id);
        Assert.Equal(historic, discord.State.FindMessage(channel.Id, id)!.Timestamp);

        // Allocation continues from the clock, unaffected by the old fixture.
        var recent = channel.Message("fresh", alice);
        Assert.True(recent > oldId);
    }

    [Fact]
    public async Task Message_UnknownAuthor_ThrowsWithoutMutationOrNotification()
    {
        await using var discord = FakeDiscord.Create();
        var channel = discord.Seed.Guild("Test").TextChannel("general");
        using var subscription = discord.State.SubscribeGateway();

        Assert.Throws<InvalidOperationException>(() => channel.Message("hello", authorId: 4242));

        Assert.Empty(StateTestSupport.Drain(subscription));
        Assert.Empty(discord.State.ListMessages(StateCaller.Fixture, channel.Id, 10).Value);
    }

    [Fact]
    public async Task Message_UnknownChannel_ThrowsInvalidOperationException()
    {
        await using var discord = FakeDiscord.Create();
        var alice = discord.Seed.User("alice");

        Assert.Throws<InvalidOperationException>(() => new SeededTextChannel(discord, 999).Message("hello", alice));
    }

    [Fact]
    public async Task Message_ZeroId_ThrowsArgumentOutOfRangeException()
    {
        await using var discord = FakeDiscord.Create();
        var channel = discord.Seed.Guild("Test").TextChannel("general");
        var alice = discord.Seed.User("alice");

        Assert.Throws<ArgumentOutOfRangeException>(() => channel.Message("hello", alice, id: 0));
    }

    [Fact]
    public async Task Seed_ExplicitIdThenAllocationOfTheSameId_ThrowsOnTheAllocationAndLaterAllocationsSucceed()
    {
        await using var discord = FakeDiscord.Create();
        var botId = discord.State.FindBotIdentity()!.User.Id;
        // The default generator is deterministic: the next allocation is the bot's ID plus one.
        discord.Seed.User("early", id: botId + 1);

        Assert.Throws<InvalidOperationException>(() => discord.Seed.Guild("collides"));

        var next = discord.Seed.Guild("fine");
        Assert.NotEqual(botId + 1, next.Id);
        Assert.Equal("early", discord.State.FindUser(botId + 1)!.Username);
        Assert.Single(discord.State.TakeProjection().Guilds);
    }

    [Fact]
    public async Task Seed_AllocationThenExplicitIdOfTheSameId_ThrowsOnTheSeed()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("allocated");

        Assert.Throws<InvalidOperationException>(() => discord.Seed.User("clone", id: guild.Id));

        Assert.Null(discord.State.FindUser(guild.Id));
    }

    [Fact]
    public async Task Seed_ExplicitIdAfterDeletion_IsNeverReassigned()
    {
        await using var discord = FakeDiscord.Create();
        var channel = discord.Seed.Guild("Test").TextChannel("general");
        var alice = discord.Seed.User("alice");
        var id = channel.Message("gone", alice, id: 777);
        discord.State.DeleteMessage(StateCaller.Fixture, channel.Id, id);
        Assert.Null(discord.State.FindMessage(channel.Id, id));

        Assert.Throws<InvalidOperationException>(() => channel.Message("again", alice, id: id));
        Assert.Throws<InvalidOperationException>(() => discord.Seed.User("clone", id: id));
    }

    [Fact]
    public async Task Seed_TwoFakes_ProduceTheSameIdsWithoutSharingObjects()
    {
        await using var first = FakeDiscord.Create();
        await using var second = FakeDiscord.Create();

        var a = first.Seed.Guild("A");
        var b = second.Seed.Guild("B");
        first.Seed.User("only-in-first");

        Assert.Equal(a.Id, b.Id);
        Assert.Equal("A", first.State.FindGuild(a.Id)!.Name);
        Assert.Equal("B", second.State.FindGuild(b.Id)!.Name);
        Assert.Single(first.State.TakeProjection().Guilds);
        Assert.Empty(second.State.TakeProjection().Users.Where(u => !u.IsBot));
    }

    [Fact]
    public async Task Seed_ManyMembersAtTheFixedInstant_AllocatesDistinctIds()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Big");

        var ids = Enumerable.Range(0, 500).Select(i => guild.Member($"member-{i}")).ToList();

        Assert.Equal(500, ids.Distinct().Count());
        Assert.Equal(501, discord.State.ListGuildMembers(StateCaller.Fixture, guild.Id).Value.Length); // 500 + the bot
    }
}
