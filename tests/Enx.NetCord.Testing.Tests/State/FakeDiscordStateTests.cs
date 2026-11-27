using Enx.NetCord.Testing.State;
using Microsoft.Extensions.Time.Testing;
using Xunit;
using static Enx.NetCord.Testing.Tests.State.StateTestSupport;

namespace Enx.NetCord.Testing.Tests.State;

public sealed class FakeDiscordStateTests
{
    // ----- Atomicity, isolation, detachment -------------------------------------------------

    [Fact]
    public void CreateGuild_Always_CreatesEveryoneRoleWithTheGuildId()
    {
        var state = NewState();

        var creation = state.CreateGuild(new CreateGuildInput("Test")).Value;

        Assert.Equal(creation.Guild.Id, creation.Everyone.Id);
        Assert.Equal(creation.Guild.Id, creation.Everyone.GuildId);
        Assert.True(creation.Everyone.IsEveryone);
        Assert.Equal(FakeDiscordState.DefaultEveryonePermissions, creation.Everyone.Permissions);
        var roles = state.ListGuildRoles(creation.Guild.Id).Value;
        Assert.Equal(creation.Everyone, Assert.Single(roles));
        Assert.Equal(creation.Everyone, state.FindRole(creation.Guild.Id));
    }

    [Fact]
    public async Task CreateGuild_WhileAnotherThreadReads_NeverExposesAGuildWithoutItsEveryoneRole()
    {
        var state = NewState();
        var writersDone = false;

        var reader = Task.Run(() =>
        {
            var checks = 0;
            while (!Volatile.Read(ref writersDone))
            {
                var projection = state.TakeProjection();
                var roleIds = projection.Roles.Select(r => r.Id).ToHashSet();
                Assert.All(projection.Guilds, g => Assert.Contains(g.Id, roleIds));
                checks++;
            }

            return checks;
        });
        var writers = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < 250; i++)
            {
                state.CreateGuild(new CreateGuildInput("g"));
            }
        }));

        await Task.WhenAll(writers).WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        Volatile.Write(ref writersDone, true);
        await reader.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        var final = state.TakeProjection();
        Assert.Equal(1000, final.Guilds.Length);
        Assert.Equal(1000, final.Roles.Length);
        Assert.Equal(1000, final.Guilds.Select(g => g.Id).Distinct().Count());
    }

    [Fact]
    public void States_TwoInstances_ShareNothing()
    {
        var first = NewState();
        var second = NewState();

        var inFirst = first.CreateUser(new CreateUserInput("alice"));
        var inSecond = second.CreateUser(new CreateUserInput("bob"));

        Assert.Equal(inFirst.Id, inSecond.Id); // deterministic IDs, never shared objects
        Assert.Equal("alice", first.FindUser(inFirst.Id)!.Username);
        Assert.Equal("bob", second.FindUser(inSecond.Id)!.Username);
        Assert.Empty(first.TakeProjection().Guilds);
        first.CreateGuild(new CreateGuildInput("only here"));
        Assert.Empty(second.TakeProjection().Guilds);
    }

    [Fact]
    public void Snapshots_AfterLaterMutations_AreNotAffected()
    {
        var state = NewState();
        var (_, channel, user) = Seed(state);
        var message = state.CreateMessage(new CreateMessageInput(channel.Id, user.Id, "before")).Value;
        var listBefore = state.ListMessages(channel.Id, 10).Value;

        state.EditMessageContent(channel.Id, message.Id, "after");
        state.DeleteMessage(channel.Id, message.Id);

        Assert.Equal("before", message.Content);
        Assert.Null(message.EditedTimestamp);
        Assert.Equal("before", Assert.Single(listBefore).Content);
        Assert.Empty(state.ListMessages(channel.Id, 10).Value);
    }

    [Fact]
    public void Find_CalledTwice_ReturnsEqualButDistinctSnapshots()
    {
        var state = NewState();
        var (guild, channel, user) = Seed(state);

        Assert.NotSame(state.FindGuild(guild.Id), state.FindGuild(guild.Id));
        Assert.Equal(state.FindChannel(channel.Id), state.FindChannel(channel.Id));
        Assert.NotSame(state.FindMember(guild.Id, user.Id), state.FindMember(guild.Id, user.Id));
    }

    [Fact]
    public void Projection_AfterLaterMutations_IsNotAffected()
    {
        var state = NewState();
        Seed(state);
        var projection = state.TakeProjection();

        state.CreateGuild(new CreateGuildInput("later"));

        Assert.Single(projection.Guilds);
        Assert.Equal(2, state.TakeProjection().Guilds.Length);
    }

    // ----- Clock and IDs --------------------------------------------------------------------

    [Fact]
    public void Create_WithoutOptions_UsesFixedVirtualClockAndDefaultGenerator()
    {
        var state = FakeDiscordState.Create();

        var first = state.CreateUser(new CreateUserInput("a"));
        var second = state.CreateUser(new CreateUserInput("b"));

        Assert.Equal(new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero), first.CreatedAt);
        Assert.Equal(first.CreatedAt, second.CreatedAt);
        Assert.Equal(first.Id + 1, second.Id);
    }

    [Fact]
    public void Create_WithOptions_UsesTheInjectedClockAndGenerator()
    {
        var clock = new FakeTimeProvider(Start);
        var ids = new ScriptedGenerator(t => IdAt(t, 77));

        var state = FakeDiscordState.Create(new FakeDiscordOptions { TimeProvider = clock, SnowflakeGenerator = ids });
        var user = state.CreateUser(new CreateUserInput("a"));

        Assert.Equal(IdAt(Start, 77), user.Id);
        Assert.Equal(1, ids.Calls);
    }

    [Fact]
    public void Mutation_Always_ReadsTheClockExactlyOnce()
    {
        var clock = new CountingTimeProvider(Start);
        var state = NewState(clock);

        var guild = state.CreateGuild(new CreateGuildInput("g")).Value.Guild; // guild + @everyone
        Assert.Equal(1, clock.Reads);

        state.CreateTextChannel(new CreateTextChannelInput(guild.Id, "c"));
        Assert.Equal(2, clock.Reads);

        var user = state.CreateUser(new CreateUserInput("u"));
        Assert.Equal(3, clock.Reads);

        state.AddMember(new AddMemberInput(guild.Id, user.Id)); // joined_at + no allocation
        Assert.Equal(4, clock.Reads);
    }

    [Fact]
    public void Find_Always_DoesNotReadTheClock()
    {
        var clock = new CountingTimeProvider(Start);
        var state = NewState(clock);
        var user = state.CreateUser(new CreateUserInput("u"));
        var before = clock.Reads;

        state.FindUser(user.Id);
        state.TakeProjection();

        Assert.Equal(before, clock.Reads);
    }

    [Fact]
    public void CreateUser_GeneratedIds_AreStrictlyIncreasingAndEncodeTheClock()
    {
        var clock = new FakeTimeProvider(Start);
        var state = NewState(clock);

        var a = state.CreateUser(new CreateUserInput("a"));
        clock.Advance(TimeSpan.FromSeconds(3));
        var b = state.CreateUser(new CreateUserInput("b"));

        Assert.True(b.Id > a.Id);
        Assert.Equal(Start, a.CreatedAt);
        Assert.Equal(Start.AddSeconds(3), b.CreatedAt);
    }

    [Fact]
    public void CreateUser_ClockOutOfSnowflakeRange_ThrowsAndCommitsNothing()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.FromUnixTimeMilliseconds(Snowflake.EpochUnixMilliseconds - 1));
        var ids = new ScriptedGenerator(t => IdAt(t));
        var state = NewState(clock, ids);
        using var subscription = state.SubscribeGateway();

        Assert.Throws<InvalidOperationException>(() => state.CreateUser(new CreateUserInput("a")));

        Assert.Equal(0, ids.Calls);
        Assert.Empty(state.TakeProjection().Users);
        Assert.Empty(Drain(subscription));
        Assert.Equal(0, state.TakeProjection().LastChangeNumber);
    }

    [Fact]
    public void CreateUser_ClockGoesBackWithDefaultGenerator_ThrowsAndCommitsNothing()
    {
        var clock = new SettableTimeProvider(Start);
        var state = NewState(clock);
        state.CreateUser(new CreateUserInput("a"));
        clock.Now = Start.AddSeconds(-1);
        using var subscription = state.SubscribeGateway();

        Assert.Throws<InvalidOperationException>(() => state.CreateUser(new CreateUserInput("b")));

        Assert.Single(state.TakeProjection().Users);
        Assert.Empty(Drain(subscription));
    }

    [Theory]
    [InlineData("zero")]
    [InlineData("not-increasing")]
    [InlineData("wrong-millisecond")]
    public void CreateUser_InvalidInjectedGenerator_ThrowsAndCommitsNothing(string violation)
    {
        var calls = 0;
        var ids = new ScriptedGenerator(t =>
        {
            calls++;
            if (calls == 1)
            {
                return IdAt(t, 10);
            }

            return violation switch
            {
                "zero" => 0UL,
                "not-increasing" => IdAt(t, 10),
                _ => IdAt(t.AddMilliseconds(1), 0),
            };
        });
        var state = NewState(ids: ids);
        var first = state.CreateUser(new CreateUserInput("a"));
        using var subscription = state.SubscribeGateway();

        Assert.Throws<InvalidOperationException>(() => state.CreateUser(new CreateUserInput("b")));

        Assert.Equal([first], state.TakeProjection().Users);
        Assert.Empty(Drain(subscription));
        Assert.Equal(1, state.TakeProjection().LastChangeNumber);
    }

    [Fact]
    public void CreateGuild_InvalidInjectedGenerator_DoesNotCommitGuildNorEveryone()
    {
        var state = NewState(ids: new ScriptedGenerator(_ => 0));

        Assert.Throws<InvalidOperationException>(() => state.CreateGuild(new CreateGuildInput("g")));

        var projection = state.TakeProjection();
        Assert.Empty(projection.Guilds);
        Assert.Empty(projection.Roles);
    }

    // ----- Explicit IDs ---------------------------------------------------------------------

    [Fact]
    public void CreateUser_ExplicitZeroId_ThrowsArgumentOutOfRange()
    {
        var state = NewState();

        Assert.Throws<ArgumentOutOfRangeException>(() => state.CreateUser(new CreateUserInput("a", Id: 0)));

        Assert.Empty(state.TakeProjection().Users);
    }

    [Fact]
    public void CreateUser_ExplicitId_UsesItWithoutCallingTheGenerator()
    {
        var ids = new ScriptedGenerator(t => IdAt(t));
        var state = NewState(ids: ids);
        var old = IdAt(Start.AddDays(-15), 1);

        var user = state.CreateUser(new CreateUserInput("a", Id: old));

        Assert.Equal(old, user.Id);
        Assert.Equal(Start.AddDays(-15), user.CreatedAt);
        Assert.Equal(0, ids.Calls);
    }

    [Fact]
    public void CreateUser_ExplicitIdWhileClockIsOutOfRange_Succeeds()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UnixEpoch); // before the Discord epoch
        var state = NewState(clock);

        var user = state.CreateUser(new CreateUserInput("a", Id: 5));

        Assert.Equal(5UL, user.Id);
    }

    [Fact]
    public void Create_ExplicitIdsNotInChronologicalOrder_AreAccepted()
    {
        var state = NewState();
        var recent = state.CreateUser(new CreateUserInput("recent", Id: IdAt(Start.AddDays(10))));
        var older = state.CreateUser(new CreateUserInput("older", Id: IdAt(Start.AddDays(-10))));
        var generated = state.CreateUser(new CreateUserInput("generated"));

        Assert.True(older.Id < recent.Id);
        Assert.Equal(Start, generated.CreatedAt); // generated IDs ignore the explicit ones
    }

    [Fact]
    public void CreateUser_ExplicitIdAlreadyUsedByAnotherEntityType_ThrowsAndCommitsNothing()
    {
        var state = NewState();
        var guild = state.CreateGuild(new CreateGuildInput("g")).Value.Guild;
        using var subscription = state.SubscribeGateway();

        Assert.Throws<InvalidOperationException>(() => state.CreateUser(new CreateUserInput("a", Id: guild.Id)));

        Assert.Empty(state.TakeProjection().Users);
        Assert.Empty(Drain(subscription));
    }

    [Fact]
    public void CreateGuild_ExplicitIdEqualToEveryoneOfAnotherGuild_Throws()
    {
        var state = NewState();
        var guild = state.CreateGuild(new CreateGuildInput("g")).Value.Guild;

        Assert.Throws<InvalidOperationException>(() => state.CreateGuild(new CreateGuildInput("h", Id: guild.Id)));
    }

    [Fact]
    public void CreateUser_GeneratedIdCollidingWithEarlierExplicitId_ThrowsAndCommitsNothing()
    {
        var next = IdAt(Start);
        var state = NewState(ids: new ScriptedGenerator(_ => next));
        state.CreateUser(new CreateUserInput("explicit", Id: next));
        using var subscription = state.SubscribeGateway();

        Assert.Throws<InvalidOperationException>(() => state.CreateUser(new CreateUserInput("generated")));

        Assert.Single(state.TakeProjection().Users);
        Assert.Empty(Drain(subscription));
    }

    [Fact]
    public void CreateUser_ExplicitIdCollidingWithEarlierGeneratedId_Throws()
    {
        var state = NewState();
        var generated = state.CreateUser(new CreateUserInput("generated"));

        Assert.Throws<InvalidOperationException>(() => state.CreateUser(new CreateUserInput("explicit", Id: generated.Id)));
    }

    [Fact]
    public void CreateMessage_ExplicitIdOfADeletedMessage_StillCollides()
    {
        var state = NewState();
        var (_, channel, user) = Seed(state);
        var message = state.CreateMessage(new CreateMessageInput(channel.Id, user.Id, "x")).Value;
        state.DeleteMessage(channel.Id, message.Id);

        Assert.Throws<InvalidOperationException>(() =>
            state.CreateMessage(new CreateMessageInput(channel.Id, user.Id, "again", Id: message.Id)));
    }

    [Fact]
    public void CreateMessage_ExplicitHistoricalId_DerivesTimestampFromTheId()
    {
        var clock = new FakeTimeProvider(Start);
        var state = NewState(clock);
        var (_, channel, user) = Seed(state);
        var oldId = IdAt(Start.AddDays(-15), 1);

        var message = state.CreateMessage(new CreateMessageInput(channel.Id, user.Id, "archive", Id: oldId)).Value;

        Assert.Equal(oldId, message.Id);
        Assert.Equal(Start.AddDays(-15), message.Timestamp);
        Assert.Equal(oldId, state.FindChannel(channel.Id)!.LastMessageId);
    }

    [Fact]
    public void AddMember_ExplicitJoinedAt_IsKeptWithoutTouchingTheClock()
    {
        var state = NewState();
        var guild = state.CreateGuild(new CreateGuildInput("g")).Value.Guild;
        var user = state.CreateUser(new CreateUserInput("u"));
        var joined = Start.AddYears(-1);

        var member = state.AddMember(new AddMemberInput(guild.Id, user.Id, JoinedAt: joined)).Value;

        Assert.Equal(joined, member.JoinedAt);
    }

    [Fact]
    public void AddMember_WithoutJoinedAt_UsesTheClock()
    {
        var state = NewState();
        var (guild, _, _) = Seed(state);
        var user = state.CreateUser(new CreateUserInput("late"));

        var member = state.AddMember(new AddMemberInput(guild.Id, user.Id)).Value;

        Assert.Equal(Start, member.JoinedAt);
    }

    // ----- Bot identity ---------------------------------------------------------------------

    [Fact]
    public void CreateBotIdentity_Always_SharesOneIdBetweenUserAndApplication()
    {
        var state = NewState();

        var bot = state.CreateBotIdentity(new CreateBotIdentityInput("bot", "My App"));

        Assert.Equal(bot.User.Id, bot.Application.Id);
        Assert.Equal(bot.User.Id, bot.Application.BotUserId);
        Assert.True(bot.User.IsBot);
        Assert.Equal("My App", bot.Application.Name);
        Assert.Equal(bot, state.FindBotIdentity());
        Assert.Equal(bot.User, state.FindUser(bot.User.Id));
        Assert.Equal(bot.Application, state.FindApplication(bot.User.Id));
    }

    [Fact]
    public void CreateBotIdentity_ExplicitId_ReservesItOnce()
    {
        var state = NewState();

        var bot = state.CreateBotIdentity(new CreateBotIdentityInput("bot", Id: 42));

        Assert.Equal(42UL, bot.User.Id);
        Assert.Throws<InvalidOperationException>(() => state.CreateUser(new CreateUserInput("other", Id: 42)));
    }

    [Fact]
    public void CreateBotIdentity_Twice_Throws()
    {
        var state = NewState();
        state.CreateBotIdentity(new CreateBotIdentityInput("bot"));

        Assert.Throws<InvalidOperationException>(() => state.CreateBotIdentity(new CreateBotIdentityInput("bot2")));
    }

    [Fact]
    public void FindBotIdentity_BeforeCreation_ReturnsNull()
    {
        Assert.Null(NewState().FindBotIdentity());
    }

    // ----- Relations and business failures ---------------------------------------------------

    [Fact]
    public void CreateMessage_UnknownChannel_FailsWithoutCommitOrAllocation()
    {
        var ids = new ScriptedGenerator(t => IdAt(t, (ulong)Random.Shared.Next(1, 1000)));
        var state = NewState(ids: ids);
        var user = state.CreateUser(new CreateUserInput("u", Id: 7));
        using var subscription = state.SubscribeGateway();

        var result = state.CreateMessage(new CreateMessageInput(12345, user.Id, "hi"));

        Assert.False(result.IsSuccess);
        Assert.Equal(new StateFailure(StateFailureKind.UnknownChannel, 12345), result.Failure);
        Assert.Equal(0, ids.Calls);
        Assert.Empty(Drain(subscription));
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void CreateMessage_UnknownAuthor_Fails()
    {
        var state = NewState();
        var (_, channel, _) = Seed(state);

        var result = state.CreateMessage(new CreateMessageInput(channel.Id, 999, "hi"));

        Assert.Equal(new StateFailure(StateFailureKind.UnknownUser, 999), result.Failure);
        Assert.Empty(state.ListMessages(channel.Id, 10).Value);
    }

    [Fact]
    public void CreateGuild_UnknownOwner_Fails()
    {
        var state = NewState();

        var result = state.CreateGuild(new CreateGuildInput("g", OwnerId: 5));

        Assert.Equal(new StateFailure(StateFailureKind.UnknownUser, 5), result.Failure);
        Assert.Empty(state.TakeProjection().Guilds);
        Assert.Empty(state.TakeProjection().Roles);
    }

    [Fact]
    public void CreateTextChannel_UnknownGuild_Fails()
    {
        var result = NewState().CreateTextChannel(new CreateTextChannelInput(1, "c"));

        Assert.Equal(new StateFailure(StateFailureKind.UnknownGuild, 1), result.Failure);
    }

    [Fact]
    public void CreateRole_UnknownGuild_Fails()
    {
        var result = NewState().CreateRole(new CreateRoleInput(1, "r"));

        Assert.Equal(new StateFailure(StateFailureKind.UnknownGuild, 1), result.Failure);
    }

    [Fact]
    public void CreateRole_Default_IsPositionedAboveExistingRoles()
    {
        var state = NewState();
        var guild = state.CreateGuild(new CreateGuildInput("g")).Value.Guild;

        var first = state.CreateRole(new CreateRoleInput(guild.Id, "a", Permissions: 8)).Value;
        var second = state.CreateRole(new CreateRoleInput(guild.Id, "b")).Value;

        Assert.Equal(1, first.Position);
        Assert.Equal(2, second.Position);
        Assert.Equal(8UL, first.Permissions);
        Assert.False(first.IsEveryone);
        Assert.Equal(
            [guild.Id, first.Id, second.Id],
            state.ListGuildRoles(guild.Id).Value.Select(r => r.Id));
    }

    [Fact]
    public void AddMember_UnknownGuildUserOrRole_FailsAndDuplicateIsRefused()
    {
        var state = NewState();
        var (guild, _, user) = Seed(state);
        var other = state.CreateGuild(new CreateGuildInput("other")).Value.Guild;
        var foreignRole = state.CreateRole(new CreateRoleInput(other.Id, "foreign")).Value;
        var newcomer = state.CreateUser(new CreateUserInput("newcomer"));

        Assert.Equal(StateFailureKind.UnknownGuild, state.AddMember(new AddMemberInput(1, user.Id)).Failure.Kind);
        Assert.Equal(StateFailureKind.UnknownUser, state.AddMember(new AddMemberInput(guild.Id, 1)).Failure.Kind);
        Assert.Equal(StateFailureKind.MemberAlreadyExists, state.AddMember(new AddMemberInput(guild.Id, user.Id)).Failure.Kind);
        var badRole = state.AddMember(new AddMemberInput(guild.Id, newcomer.Id, RoleIds: [foreignRole.Id]));
        Assert.Equal(new StateFailure(StateFailureKind.UnknownRole, foreignRole.Id), badRole.Failure);

        Assert.Null(state.FindMember(guild.Id, newcomer.Id)); // the whole lot was validated before writing
    }

    [Fact]
    public void AddMember_WithRolesIncludingEveryone_KeepsDistinctNonEveryoneRoles()
    {
        var state = NewState();
        var guild = state.CreateGuild(new CreateGuildInput("g")).Value.Guild;
        var role = state.CreateRole(new CreateRoleInput(guild.Id, "r")).Value;
        var user = state.CreateUser(new CreateUserInput("u"));

        var member = state.AddMember(new AddMemberInput(guild.Id, user.Id, "nick", [guild.Id, role.Id, role.Id])).Value;

        Assert.Equal([role.Id], member.RoleIds);
        Assert.Equal("nick", member.Nick);
        Assert.Equal([user.Id], state.ListGuildMembers(guild.Id).Value.Select(m => m.UserId));
    }

    [Fact]
    public void Members_SameUserInTwoGuilds_AreIndependentRows()
    {
        var state = NewState();
        var (guildA, _, user) = Seed(state);
        var guildB = state.CreateGuild(new CreateGuildInput("b")).Value.Guild;

        state.AddMember(new AddMemberInput(guildB.Id, user.Id, "other nick"));

        Assert.Null(state.FindMember(guildA.Id, user.Id)!.Nick);
        Assert.Equal("other nick", state.FindMember(guildB.Id, user.Id)!.Nick);
    }

    [Fact]
    public void ListGuildChildren_UnknownGuild_Fails()
    {
        var state = NewState();

        Assert.Equal(StateFailureKind.UnknownGuild, state.ListGuildChannels(1).Failure.Kind);
        Assert.Equal(StateFailureKind.UnknownGuild, state.ListGuildRoles(1).Failure.Kind);
        Assert.Equal(StateFailureKind.UnknownGuild, state.ListGuildMembers(1).Failure.Kind);
    }

    [Fact]
    public void CreateTextChannel_Several_AreListedInCreationOrderWithPositions()
    {
        var state = NewState();
        var guild = state.CreateGuild(new CreateGuildInput("g")).Value.Guild;

        var a = state.CreateTextChannel(new CreateTextChannelInput(guild.Id, "a", Topic: "t")).Value;
        var b = state.CreateTextChannel(new CreateTextChannelInput(guild.Id, "b", Nsfw: true)).Value;

        Assert.Equal(ChannelKind.GuildText, a.Kind);
        Assert.Equal(guild.Id, a.GuildId);
        Assert.Equal((0, 1), (a.Position, b.Position));
        Assert.Equal("t", a.Topic);
        Assert.True(b.Nsfw);
        Assert.Equal([a.Id, b.Id], state.ListGuildChannels(guild.Id).Value.Select(c => c.Id));
    }

    [Fact]
    public void CreateDmChannel_ForAUser_HasNoGuildAndIsFoundByRecipient()
    {
        var state = NewState();
        var user = state.CreateUser(new CreateUserInput("u"));

        var dm = state.CreateDmChannel(new CreateDmChannelInput(user.Id)).Value;

        Assert.Equal(ChannelKind.Dm, dm.Kind);
        Assert.Null(dm.GuildId);
        Assert.Equal([user.Id], dm.RecipientIds);
        Assert.Equal(dm.Id, state.FindDmChannel(user.Id)!.Id);
        Assert.Null(state.FindDmChannel(999));
        Assert.Equal(StateFailureKind.UnknownUser, state.CreateDmChannel(new CreateDmChannelInput(999)).Failure.Kind);
    }

    [Fact]
    public void CreateMessage_InADmChannel_HasNoGuildId()
    {
        var state = NewState();
        var user = state.CreateUser(new CreateUserInput("u"));
        var dm = state.CreateDmChannel(new CreateDmChannelInput(user.Id)).Value;

        var message = state.CreateMessage(new CreateMessageInput(dm.Id, user.Id, "hi")).Value;

        Assert.Null(message.GuildId);
    }

    // ----- Messages -------------------------------------------------------------------------

    [Fact]
    public void CreateMessage_Nominal_IsReadableAndUpdatesTheLastMessageId()
    {
        var clock = new FakeTimeProvider(Start);
        var state = NewState(clock);
        var (guild, channel, user) = Seed(state);

        var message = state.CreateMessage(new CreateMessageInput(channel.Id, user.Id, "hello", Tts: true)).Value;

        Assert.Equal(message, state.FindMessage(channel.Id, message.Id));
        Assert.Equal("hello", message.Content);
        Assert.Equal(guild.Id, message.GuildId);
        Assert.Equal(user.Id, message.AuthorId);
        Assert.Equal(Start, message.Timestamp);
        Assert.Null(message.EditedTimestamp);
        Assert.True(message.Tts);
        Assert.Equal(message.Id, state.FindChannel(channel.Id)!.LastMessageId);
    }

    [Fact]
    public void FindMessage_InAnotherChannel_ReturnsNull()
    {
        var state = NewState();
        var (guild, channel, user) = Seed(state);
        var other = state.CreateTextChannel(new CreateTextChannelInput(guild.Id, "other")).Value;
        var message = state.CreateMessage(new CreateMessageInput(channel.Id, user.Id, "x")).Value;

        Assert.Null(state.FindMessage(other.Id, message.Id));
        Assert.Null(state.FindMessage(999, message.Id));
        Assert.Equal(StateFailureKind.UnknownMessage, state.DeleteMessage(other.Id, message.Id).Failure.Kind);
        Assert.Equal(StateFailureKind.UnknownChannel, state.EditMessageContent(999, message.Id, "y").Failure.Kind);
    }

    [Fact]
    public void EditMessageContent_NewContent_SetsEditedTimestampFromTheClock()
    {
        var clock = new FakeTimeProvider(Start);
        var state = NewState(clock);
        var (_, channel, user) = Seed(state);
        var message = state.CreateMessage(new CreateMessageInput(channel.Id, user.Id, "v1")).Value;
        clock.Advance(TimeSpan.FromMinutes(5));

        var edited = state.EditMessageContent(channel.Id, message.Id, "v2").Value;

        Assert.Equal("v2", edited.Content);
        Assert.Equal(Start.AddMinutes(5), edited.EditedTimestamp);
        Assert.Equal(message.Timestamp, edited.Timestamp);
        Assert.Equal(edited, state.FindMessage(channel.Id, message.Id));
    }

    [Fact]
    public void EditMessageContent_SameContent_IsNotAMutation()
    {
        var state = NewState();
        var (_, channel, user) = Seed(state);
        var message = state.CreateMessage(new CreateMessageInput(channel.Id, user.Id, "same")).Value;
        using var subscription = state.SubscribeGateway();

        var result = state.EditMessageContent(channel.Id, message.Id, "same").Value;

        Assert.Equal(message, result);
        Assert.Empty(Drain(subscription));
    }

    [Fact]
    public void DeleteMessage_Existing_RemovesItAndKeepsTheLastMessageId()
    {
        var state = NewState();
        var (_, channel, user) = Seed(state);
        var message = state.CreateMessage(new CreateMessageInput(channel.Id, user.Id, "bye")).Value;

        var deleted = state.DeleteMessage(channel.Id, message.Id).Value;

        Assert.Equal(message, deleted);
        Assert.Null(state.FindMessage(channel.Id, message.Id));
        Assert.Equal(StateFailureKind.UnknownMessage, state.DeleteMessage(channel.Id, message.Id).Failure.Kind);
        Assert.Equal(message.Id, state.FindChannel(channel.Id)!.LastMessageId);
    }

    [Fact]
    public void ListMessages_WithCursors_ReturnsNewestFirstByIdNotByCreationOrder()
    {
        var state = NewState();
        var (_, channel, user) = Seed(state);
        ulong Send(ulong id) =>
            state.CreateMessage(new CreateMessageInput(channel.Id, user.Id, id.ToString(), Id: id)).Value.Id;
        // Created out of order on purpose.
        Send(30);
        Send(10);
        Send(50);
        Send(20);
        Send(40);

        ulong[] Ids(StateResult<System.Collections.Immutable.ImmutableArray<MessageSnapshot>> r) =>
            [.. r.Value.Select(m => m.Id)];

        Assert.Equal([50UL, 40, 30, 20, 10], Ids(state.ListMessages(channel.Id, 100)));
        Assert.Equal([50UL, 40], Ids(state.ListMessages(channel.Id, 2)));
        Assert.Equal([20UL, 10], Ids(state.ListMessages(channel.Id, 100, before: 30)));
        Assert.Equal([20UL, 10], Ids(state.ListMessages(channel.Id, 2, before: 30)));
        Assert.Equal([30UL, 20], Ids(state.ListMessages(channel.Id, 2, before: 40)));
        Assert.Equal([30UL, 20], Ids(state.ListMessages(channel.Id, 2, after: 10)));
        Assert.Equal([50UL, 40], Ids(state.ListMessages(channel.Id, 100, after: 30)));
        Assert.Empty(Ids(state.ListMessages(channel.Id, 100, before: 0)));
        Assert.Empty(Ids(state.ListMessages(channel.Id, 100, after: ulong.MaxValue)));
        Assert.Empty(Ids(state.ListMessages(channel.Id, 0)));
        Assert.Equal(StateFailureKind.UnknownChannel, state.ListMessages(999, 10).Failure.Kind);
    }

    [Fact]
    public void ListMessages_BothCursors_ThrowsArgumentException()
    {
        var state = NewState();
        var (_, channel, _) = Seed(state);

        Assert.Throws<ArgumentException>(() => state.ListMessages(channel.Id, 10, before: 1, after: 2));
    }

    [Fact]
    public void Entities_CreatedAtTheSameInstant_AreOrderedByCreationOrdinal()
    {
        var state = NewState();

        var a = state.CreateUser(new CreateUserInput("a"));
        var b = state.CreateUser(new CreateUserInput("b"));
        var c = state.CreateUser(new CreateUserInput("c", Id: 1)); // explicit ID sorts first by ID, last by ordinal

        Assert.Equal(a.CreatedAt, b.CreatedAt);
        Assert.True(a.Ordinal < b.Ordinal && b.Ordinal < c.Ordinal);
        Assert.Equal([a.Id, b.Id, c.Id], state.TakeProjection().Users.Select(u => u.Id));
    }

    [Fact]
    public void ValueOrThrow_OnFailure_ThrowsInvalidOperation()
    {
        var result = NewState().CreateTextChannel(new CreateTextChannelInput(1, "c"));

        Assert.Throws<InvalidOperationException>(() => result.ValueOrThrow());
    }
}
