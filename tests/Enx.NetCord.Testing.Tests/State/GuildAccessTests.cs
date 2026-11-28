using Enx.NetCord.Testing.State;
using Xunit;
using static Enx.NetCord.Testing.Tests.State.StateTestSupport;

namespace Enx.NetCord.Testing.Tests.State;

/// <summary>ADR 0004, amendment 04B: the bot's membership of a guild, and the visibility it implies for the bot caller.</summary>
public sealed class GuildAccessTests
{
    private static (FakeDiscordState State, ulong BotId) NewStateWithBot()
    {
        var state = NewState();
        var bot = state.CreateBotIdentity(new CreateBotIdentityInput("bot"));
        return (state, bot.User.Id);
    }

    private static GuildCreation CreateGuild(FakeDiscordState state, string name, bool withBot) =>
        state.CreateGuild(new CreateGuildInput(name, WithBot: withBot)).Value;

    // ----- Membership at creation -------------------------------------------------------------

    [Fact]
    public void CreateGuild_WithBot_CommitsTheGuildItsEveryoneRoleAndTheBotMembershipAsOneChange()
    {
        var (state, botId) = NewStateWithBot();
        using var subscription = state.SubscribeGateway();

        var creation = CreateGuild(state, "g", withBot: true);

        var created = Assert.IsType<GuildCreated>(Assert.Single(Drain(subscription)));
        Assert.Equal(creation.Guild, created.Guild);
        Assert.Equal(creation.Everyone, created.Everyone);
        var member = Assert.IsType<MemberSnapshot>(created.BotMember);
        Assert.Equal((creation.Guild.Id, botId), (member.GuildId, member.UserId));
        Assert.Equal(member, creation.BotMember);
        Assert.Equal(member, state.FindMember(creation.Guild.Id, botId));
        Assert.Equal([botId], state.ListGuildMembers(StateCaller.Fixture, creation.Guild.Id).Value.Select(m => m.UserId));
    }

    [Fact]
    public void CreateGuild_WithBot_MembershipIsOrdinaryClockDatedWithoutRoles()
    {
        var (state, _) = NewStateWithBot();

        var member = CreateGuild(state, "g", withBot: true).BotMember!;

        Assert.Equal(Start, member.JoinedAt);
        Assert.Empty(member.RoleIds);
        Assert.Null(member.Nick);
        Assert.Null(state.FindGuild(member.GuildId)!.OwnerId);
    }

    [Fact]
    public void CreateGuild_WithBot_AllocatesTheSameIdsAsWithoutBot()
    {
        var (withBot, _) = NewStateWithBot();
        var (withoutBot, _) = NewStateWithBot();

        var a = CreateGuild(withBot, "g", withBot: true);
        var b = CreateGuild(withoutBot, "g", withBot: false);
        var nextA = withBot.CreateUser(new CreateUserInput("alice")).Id;
        var nextB = withoutBot.CreateUser(new CreateUserInput("alice")).Id;

        Assert.Equal(b.Guild.Id, a.Guild.Id);
        Assert.Equal(nextB, nextA);
    }

    [Fact]
    public void CreateGuild_WithoutBot_HasNoBotMembership()
    {
        var (state, botId) = NewStateWithBot();
        using var subscription = state.SubscribeGateway();

        var creation = CreateGuild(state, "g", withBot: false);

        Assert.Null(creation.BotMember);
        Assert.Null(Assert.IsType<GuildCreated>(Assert.Single(Drain(subscription))).BotMember);
        Assert.Null(state.FindMember(creation.Guild.Id, botId));
        Assert.Empty(state.ListGuildMembers(StateCaller.Fixture, creation.Guild.Id).Value);
    }

    [Fact]
    public void CreateGuild_DefaultInput_DoesNotAddTheBot()
    {
        var (state, botId) = NewStateWithBot();

        var guild = state.CreateGuild(new CreateGuildInput("g")).Value.Guild;

        Assert.Null(state.FindMember(guild.Id, botId));
    }

    [Fact]
    public void CreateGuild_WithBotButNoBotIdentity_ThrowsBeforeAnyAllocationOrCommit()
    {
        var ids = new ScriptedGenerator(timestamp => IdAt(timestamp, counter: 1));
        var state = NewState(ids: ids);
        using var subscription = state.SubscribeGateway();

        Assert.Throws<InvalidOperationException>(() => state.CreateGuild(new CreateGuildInput("g", WithBot: true)));

        Assert.Equal(0, ids.Calls);
        Assert.Empty(Drain(subscription));
        Assert.Empty(state.TakeProjection().Guilds);
    }

    // ----- Guild visibility -------------------------------------------------------------------

    [Fact]
    public void GetGuild_BotCallerGuildWithoutBot_IsHiddenAsUnknownGuild()
    {
        var (state, _) = NewStateWithBot();
        var guild = CreateGuild(state, "elsewhere", withBot: false).Guild;

        var hidden = state.GetGuild(StateCaller.Bot, guild.Id);
        var absent = state.GetGuild(StateCaller.Bot, 999);

        Assert.Equal(new StateFailure(StateFailureKind.UnknownGuild, guild.Id), hidden.Failure);
        Assert.Equal(new StateFailure(StateFailureKind.UnknownGuild, 999), absent.Failure);
    }

    [Fact]
    public void GetGuild_BotCallerGuildWithBot_ReturnsTheGuild()
    {
        var (state, _) = NewStateWithBot();
        var guild = CreateGuild(state, "home", withBot: true).Guild;

        Assert.Equal(guild, state.GetGuild(StateCaller.Bot, guild.Id).Value);
    }

    [Fact]
    public void GetGuild_FixtureCaller_SeesAGuildWithoutBot()
    {
        var (state, _) = NewStateWithBot();
        var guild = CreateGuild(state, "elsewhere", withBot: false).Guild;

        Assert.Equal(guild, state.GetGuild(StateCaller.Fixture, guild.Id).Value);
    }

    [Fact]
    public void GetGuild_BotJoinsLater_GuildBecomesVisible()
    {
        var (state, botId) = NewStateWithBot();
        var guild = CreateGuild(state, "elsewhere", withBot: false).Guild;
        using var subscription = state.SubscribeGateway();

        var member = state.AddMember(StateCaller.Fixture, new AddMemberInput(guild.Id, botId)).Value;

        Assert.Equal(guild, state.GetGuild(StateCaller.Bot, guild.Id).Value);
        var added = Assert.IsType<MemberAdded>(Assert.Single(Drain(subscription)));
        Assert.Equal(member, added.Member);
        Assert.True(added.User.IsBot);
    }

    [Fact]
    public void AddMember_BotAlreadyAMember_FailsWithMemberAlreadyExists()
    {
        var (state, botId) = NewStateWithBot();
        var guild = CreateGuild(state, "home", withBot: true).Guild;

        var result = state.AddMember(StateCaller.Fixture, new AddMemberInput(guild.Id, botId));

        Assert.Equal(new StateFailure(StateFailureKind.MemberAlreadyExists, botId), result.Failure);
    }

    [Fact]
    public void GuildSubResources_BotCaller_UnknownGuildIsUnknownGuildAndGuildWithoutBotIsMissingAccess()
    {
        var (state, _) = NewStateWithBot();
        var guild = CreateGuild(state, "elsewhere", withBot: false).Guild;
        var denied = new StateFailure(StateFailureKind.MissingAccess, guild.Id);
        var unknown = new StateFailure(StateFailureKind.UnknownGuild, 999);

        Assert.Equal(denied, state.ListGuildRoles(StateCaller.Bot, guild.Id).Failure);
        Assert.Equal(denied, state.ListGuildChannels(StateCaller.Bot, guild.Id).Failure);
        Assert.Equal(denied, state.ListGuildMembers(StateCaller.Bot, guild.Id).Failure);
        Assert.Equal(unknown, state.ListGuildRoles(StateCaller.Bot, 999).Failure);
        Assert.Equal(unknown, state.ListGuildChannels(StateCaller.Bot, 999).Failure);
        Assert.Equal(unknown, state.ListGuildMembers(StateCaller.Bot, 999).Failure);
    }

    [Fact]
    public void GuildSubResources_BotCallerGuildWithBot_AreReadable()
    {
        var (state, botId) = NewStateWithBot();
        var guild = CreateGuild(state, "home", withBot: true).Guild;

        Assert.Single(state.ListGuildRoles(StateCaller.Bot, guild.Id).Value);
        Assert.Empty(state.ListGuildChannels(StateCaller.Bot, guild.Id).Value);
        Assert.Equal([botId], state.ListGuildMembers(StateCaller.Bot, guild.Id).Value.Select(m => m.UserId));
    }

    [Fact]
    public void GuildSubResources_FixtureCallerGuildWithoutBot_AreNotAccessControlled()
    {
        var (state, _) = NewStateWithBot();
        var guild = CreateGuild(state, "elsewhere", withBot: false).Guild;

        Assert.Single(state.ListGuildRoles(StateCaller.Fixture, guild.Id).Value);
        Assert.True(state.CreateGuildChannel(StateCaller.Fixture, new CreateGuildChannelInput(guild.Id, ChannelKind.GuildText, "c")).IsSuccess);
        Assert.True(state.CreateRole(StateCaller.Fixture, new CreateRoleInput(guild.Id, "r")).IsSuccess);
        Assert.True(state.CreateUserAndMember(StateCaller.Fixture, guild.Id, new CreateUserInput("carol")).IsSuccess);
    }

    [Fact]
    public void GuildMutations_BotCallerGuildWithoutBot_AreRefusedWithoutAnyEffect()
    {
        var (state, _) = NewStateWithBot();
        var guild = CreateGuild(state, "elsewhere", withBot: false).Guild;
        var user = state.CreateUser(new CreateUserInput("carol"));
        using var subscription = state.SubscribeGateway();
        var denied = new StateFailure(StateFailureKind.MissingAccess, guild.Id);

        Assert.Equal(denied, state.CreateGuildChannel(StateCaller.Bot, new CreateGuildChannelInput(guild.Id, ChannelKind.GuildText, "c")).Failure);
        Assert.Equal(denied, state.CreateRole(StateCaller.Bot, new CreateRoleInput(guild.Id, "r")).Failure);
        Assert.Equal(denied, state.AddMember(StateCaller.Bot, new AddMemberInput(guild.Id, user.Id)).Failure);
        Assert.Equal(denied, state.CreateUserAndMember(StateCaller.Bot, guild.Id, new CreateUserInput("dave")).Failure);

        Assert.Empty(Drain(subscription));
        Assert.Empty(state.ListGuildChannels(StateCaller.Fixture, guild.Id).Value);
    }

    [Fact]
    public void GuildOperations_BotCallerWithoutBotIdentity_NoGuildIsVisible()
    {
        var state = NewState();
        var guild = CreateGuild(state, "g", withBot: false).Guild;

        Assert.Equal(StateFailureKind.UnknownGuild, state.GetGuild(StateCaller.Bot, guild.Id).Failure.Kind);
        Assert.Equal(StateFailureKind.MissingAccess, state.ListGuildRoles(StateCaller.Bot, guild.Id).Failure.Kind);
    }

    // ----- Channel visibility -----------------------------------------------------------------

    [Fact]
    public void GetChannel_BotCaller_ChannelOfAGuildWithoutBotIsMissingAccessAndUnknownIsUnknownChannel()
    {
        var (state, _) = NewStateWithBot();
        var guild = CreateGuild(state, "elsewhere", withBot: false).Guild;
        var channel = state.CreateGuildChannel(StateCaller.Fixture, new CreateGuildChannelInput(guild.Id, ChannelKind.GuildText, "general")).Value;

        Assert.Equal(new StateFailure(StateFailureKind.MissingAccess, channel.Id), state.GetChannel(StateCaller.Bot, channel.Id).Failure);
        Assert.Equal(new StateFailure(StateFailureKind.UnknownChannel, 999), state.GetChannel(StateCaller.Bot, 999).Failure);
        Assert.Equal(channel, state.GetChannel(StateCaller.Fixture, channel.Id).Value);
    }

    [Fact]
    public void GetChannel_BotCaller_ChannelOfAGuildWithBotAndDmChannelAreVisible()
    {
        var (state, _) = NewStateWithBot();
        var guild = CreateGuild(state, "home", withBot: true).Guild;
        var channel = state.CreateGuildChannel(StateCaller.Fixture, new CreateGuildChannelInput(guild.Id, ChannelKind.GuildText, "general")).Value;
        var user = state.CreateUser(new CreateUserInput("alice"));
        var dm = state.GetOrCreateDmChannel(new CreateDmChannelInput(user.Id)).Value.Channel;

        Assert.Equal(channel, state.GetChannel(StateCaller.Bot, channel.Id).Value);
        Assert.Equal(dm.Id, state.GetChannel(StateCaller.Bot, dm.Id).Value.Id);
    }

    [Fact]
    public void MessageOperations_BotCallerChannelWithoutBot_AreRefusedAndAccessComesBeforeUnknownMessage()
    {
        var (state, _) = NewStateWithBot();
        var guild = CreateGuild(state, "elsewhere", withBot: false).Guild;
        var channel = state.CreateGuildChannel(StateCaller.Fixture, new CreateGuildChannelInput(guild.Id, ChannelKind.GuildText, "general")).Value;
        var alice = state.CreateUser(new CreateUserInput("alice"));
        var message = state.CreateMessage(StateCaller.Fixture, new CreateMessageInput(channel.Id, alice.Id, "hi")).Value;
        using var subscription = state.SubscribeGateway();
        var denied = new StateFailure(StateFailureKind.MissingAccess, channel.Id);

        Assert.Equal(denied, state.CreateMessage(StateCaller.Bot, new CreateMessageInput(channel.Id, alice.Id, "x")).Failure);
        Assert.Equal(denied, state.ListMessages(StateCaller.Bot, channel.Id, 10).Failure);
        Assert.Equal(denied, state.EditMessageContent(StateCaller.Bot, channel.Id, message.Id, "y").Failure);
        Assert.Equal(denied, state.EditMessageContent(StateCaller.Bot, channel.Id, 12345, "y").Failure);
        Assert.Equal(denied, state.DeleteMessage(StateCaller.Bot, channel.Id, message.Id).Failure);
        Assert.Equal(denied, state.DeleteMessage(StateCaller.Bot, channel.Id, 12345).Failure);

        Assert.Empty(Drain(subscription));
        Assert.Equal("hi", state.FindMessage(channel.Id, message.Id)!.Content);
    }

    [Fact]
    public void MessageOperations_BotCallerChannelWithBot_WorkAndUnknownMessageIsUnknownMessage()
    {
        var (state, _) = NewStateWithBot();
        var guild = CreateGuild(state, "home", withBot: true).Guild;
        var channel = state.CreateGuildChannel(StateCaller.Fixture, new CreateGuildChannelInput(guild.Id, ChannelKind.GuildText, "general")).Value;
        var alice = state.CreateUser(new CreateUserInput("alice"));

        var message = state.CreateMessage(StateCaller.Bot, new CreateMessageInput(channel.Id, alice.Id, "hi")).Value;

        Assert.Equal([message.Id], state.ListMessages(StateCaller.Bot, channel.Id, 10).Value.Select(m => m.Id));
        Assert.Equal("yo", state.EditMessageContent(StateCaller.Bot, channel.Id, message.Id, "yo").Value.Content);
        Assert.Equal(
            new StateFailure(StateFailureKind.UnknownMessage, 12345),
            state.DeleteMessage(StateCaller.Bot, channel.Id, 12345).Failure);
        Assert.Equal(message.Id, state.DeleteMessage(StateCaller.Bot, channel.Id, message.Id).Value.Id);
    }

    // ----- The caller is mandatory ------------------------------------------------------------

    [Fact]
    public void AccessControlledOperations_UndefinedCaller_AreRejected()
    {
        var (state, _) = NewStateWithBot();
        var guild = CreateGuild(state, "home", withBot: true).Guild;
        var undefined = default(StateCaller);

        Assert.Throws<ArgumentOutOfRangeException>(() => state.GetGuild(undefined, guild.Id));
        Assert.Throws<ArgumentOutOfRangeException>(() => state.ListGuildRoles(undefined, guild.Id));
        Assert.Throws<ArgumentOutOfRangeException>(() => state.GetChannel(undefined, 1));
    }
}
