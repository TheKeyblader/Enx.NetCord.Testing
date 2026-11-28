using Enx.NetCord.Testing.State;
using Xunit;
using static Enx.NetCord.Testing.Tests.State.StateTestSupport;

namespace Enx.NetCord.Testing.Tests.State;

/// <summary>The channel types, traits and hierarchy invariants of ADR 0013, below the REST layer.</summary>
public sealed class ChannelStateTests
{
    private static (FakeDiscordState State, GuildSnapshot Guild) NewGuild()
    {
        var state = NewState();
        return (state, state.CreateGuild(new CreateGuildInput("g")).Value.Guild);
    }

    private static ChannelSnapshot Create(
        FakeDiscordState state, ulong guildId, ChannelKind kind, string name, ulong? parentId = null)
        => state.CreateGuildChannel(StateCaller.Fixture, new CreateGuildChannelInput(guildId, kind, name, ParentId: parentId)).Value;

    [Theory]
    [InlineData(0, "Text", "Textual", true)]
    [InlineData(5, "Text", "Textual", true)]
    [InlineData(2, "Voice", "Voice", true)]
    [InlineData(13, "Voice", "Voice", true)]
    [InlineData(4, "Category", "Category", false)]
    [InlineData(15, "Forum", "Textual", true)]
    [InlineData(16, "Forum", "Textual", true)]
    [InlineData(1, "Private", "None", false)]
    public void ChannelTraits_Of_GivesFamilySortGroupAndParentRule(int kind, string family, string group, bool canHaveParent)
    {
        var traits = ChannelTraits.Of((ChannelKind)kind);

        Assert.Equal((family, group, canHaveParent), (traits.Family.ToString(), traits.SortGroup.ToString(), traits.CanHaveParent));
    }

    [Fact]
    public void ChannelTraits_Of_UnmodeledKind_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ChannelTraits.Of((ChannelKind)14));
    }

    [Fact]
    public void CreateGuildChannel_VoiceAndForum_CarryTheirFamilyBlockOnly()
    {
        var (state, guild) = NewGuild();

        var voice = Create(state, guild.Id, ChannelKind.GuildVoice, "v");
        var forum = Create(state, guild.Id, ChannelKind.GuildForum, "f");
        var text = Create(state, guild.Id, ChannelKind.GuildText, "t");

        Assert.Equal(new VoiceSettings(64000, 0, null, null), voice.Voice);
        Assert.Null(voice.Forum);
        Assert.Equal(new ForumSettings(null, 0), forum.Forum);
        Assert.Null(forum.Voice);
        Assert.Null(text.Voice);
        Assert.Null(text.Forum);
    }

    [Fact]
    public void CreateGuildChannel_SettingWithoutObjectForTheType_IsNotStored()
    {
        var (state, guild) = NewGuild();

        var voice = state.CreateGuildChannel(
            StateCaller.Fixture,
            new CreateGuildChannelInput(guild.Id, ChannelKind.GuildVoice, "v", Topic: "no", RateLimitPerUser: 5)).Value;
        var category = state.CreateGuildChannel(
            StateCaller.Fixture,
            new CreateGuildChannelInput(guild.Id, ChannelKind.GuildCategory, "c", Nsfw: true, RateLimitPerUser: 5)).Value;

        Assert.Null(voice.Topic);
        Assert.Equal(5, voice.RateLimitPerUser);
        Assert.False(category.Nsfw);
        Assert.Equal(0, category.RateLimitPerUser);
    }

    [Fact]
    public void CreateGuildChannel_Positions_AreCountedPerSortingGroupAcrossCategories()
    {
        var (state, guild) = NewGuild();
        var lobby = Create(state, guild.Id, ChannelKind.GuildCategory, "lobby");
        var archive = Create(state, guild.Id, ChannelKind.GuildCategory, "archive");

        var general = Create(state, guild.Id, ChannelKind.GuildText, "general", lobby.Id);
        var news = Create(state, guild.Id, ChannelKind.GuildAnnouncement, "news", archive.Id);
        var forum = Create(state, guild.Id, ChannelKind.GuildForum, "forum");
        var voice = Create(state, guild.Id, ChannelKind.GuildVoice, "voice", lobby.Id);
        var stage = Create(state, guild.Id, ChannelKind.GuildStageVoice, "stage");

        Assert.Equal((0, 1), (lobby.Position, archive.Position));
        Assert.Equal((0, 1, 2), (general.Position, news.Position, forum.Position));
        Assert.Equal((0, 1), (voice.Position, stage.Position));
    }

    [Fact]
    public void CreateGuildChannel_ExplicitPosition_IsKeptAndTheNextDefaultFollowsIt()
    {
        var (state, guild) = NewGuild();

        var first = state.CreateGuildChannel(
            StateCaller.Fixture, new CreateGuildChannelInput(guild.Id, ChannelKind.GuildText, "a", Position: 7)).Value;
        var second = Create(state, guild.Id, ChannelKind.GuildText, "b");

        Assert.Equal((7, 8), (first.Position, second.Position));
    }

    [Fact]
    public void CreateGuildChannel_ParentInTheSameGuild_IsStored()
    {
        var (state, guild) = NewGuild();
        var lobby = Create(state, guild.Id, ChannelKind.GuildCategory, "lobby");

        var general = Create(state, guild.Id, ChannelKind.GuildText, "general", lobby.Id);

        Assert.Equal(lobby.Id, general.ParentId);
        Assert.Null(lobby.ParentId);
    }

    [Fact]
    public void CreateGuildChannel_UnknownOrForeignParent_IsInvalidChannelParent()
    {
        var (state, guild) = NewGuild();
        var other = state.CreateGuild(new CreateGuildInput("other")).Value.Guild;
        var foreign = Create(state, other.Id, ChannelKind.GuildCategory, "foreign");

        var unknown = state.CreateGuildChannel(StateCaller.Fixture, new CreateGuildChannelInput(guild.Id, ChannelKind.GuildText, "a", ParentId: 12345));
        var elsewhere = state.CreateGuildChannel(StateCaller.Fixture, new CreateGuildChannelInput(guild.Id, ChannelKind.GuildText, "b", ParentId: foreign.Id));

        Assert.Equal(new StateFailure(StateFailureKind.InvalidChannelParent, 12345), unknown.Failure);
        Assert.Equal(new StateFailure(StateFailureKind.InvalidChannelParent, foreign.Id), elsewhere.Failure);
    }

    [Fact]
    public void CreateGuildChannel_ParentThatIsNotACategory_IsChannelParentNotCategory()
    {
        var (state, guild) = NewGuild();
        var rules = Create(state, guild.Id, ChannelKind.GuildText, "rules");

        var result = state.CreateGuildChannel(StateCaller.Fixture, new CreateGuildChannelInput(guild.Id, ChannelKind.GuildText, "a", ParentId: rules.Id));

        Assert.Equal(new StateFailure(StateFailureKind.ChannelParentNotCategory, rules.Id), result.Failure);
    }

    [Fact]
    public void CreateGuildChannel_CategoryWithAParent_IsChannelCannotHaveParent()
    {
        var (state, guild) = NewGuild();
        var lobby = Create(state, guild.Id, ChannelKind.GuildCategory, "lobby");

        var result = state.CreateGuildChannel(StateCaller.Fixture, new CreateGuildChannelInput(guild.Id, ChannelKind.GuildCategory, "sub", ParentId: lobby.Id));

        Assert.Equal(new StateFailure(StateFailureKind.ChannelCannotHaveParent, lobby.Id), result.Failure);
    }

    [Fact]
    public void CreateGuildChannel_FiftyOneChildren_TheLastIsChannelParentFullAndNothingIsCreated()
    {
        var (state, guild) = NewGuild();
        var lobby = Create(state, guild.Id, ChannelKind.GuildCategory, "lobby");
        for (var i = 0; i < FakeDiscordState.MaxChannelsPerCategory; i++)
        {
            Create(state, guild.Id, ChannelKind.GuildText, $"c{i}", lobby.Id);
        }

        using var subscription = state.SubscribeGateway();
        var result = state.CreateGuildChannel(StateCaller.Fixture, new CreateGuildChannelInput(guild.Id, ChannelKind.GuildVoice, "one-too-many", ParentId: lobby.Id));

        Assert.Equal(new StateFailure(StateFailureKind.ChannelParentFull, lobby.Id), result.Failure);
        Assert.Empty(Drain(subscription));
        Assert.Equal(FakeDiscordState.MaxChannelsPerCategory + 1, state.ListGuildChannels(StateCaller.Fixture, guild.Id).Value.Length);
    }

    [Fact]
    public void CreateGuildChannel_FiveHundredChannels_TheNextIsMaxGuildChannelsAndComesBeforeTheParentChecks()
    {
        var (state, guild) = NewGuild();
        for (var i = 0; i < FakeDiscordState.MaxChannelsPerGuild; i++)
        {
            Create(state, guild.Id, ChannelKind.GuildText, $"c{i}");
        }

        var result = state.CreateGuildChannel(StateCaller.Fixture, new CreateGuildChannelInput(guild.Id, ChannelKind.GuildText, "late", ParentId: 12345));

        Assert.Equal(new StateFailure(StateFailureKind.MaxGuildChannels, guild.Id), result.Failure);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(14)]
    [InlineData(10)]
    public void CreateGuildChannel_KindThatIsNotAGuildChannel_IsInvalidChannelType(int kind)
    {
        var (state, guild) = NewGuild();

        var result = state.CreateGuildChannel(StateCaller.Fixture, new CreateGuildChannelInput(guild.Id, (ChannelKind)kind, "x"));

        Assert.Equal(StateFailureKind.InvalidChannelType, result.Failure.Kind);
    }

    [Fact]
    public void CreateGuildChannel_Refused_DoesNotConsumeAGeneratedId()
    {
        var generator = new ScriptedGenerator(new DefaultSnowflakeGenerator().Next);
        var state = NewState(ids: generator);
        var guild = state.CreateGuild(new CreateGuildInput("g")).Value.Guild;
        var callsBefore = generator.Calls;

        state.CreateGuildChannel(StateCaller.Fixture, new CreateGuildChannelInput(guild.Id, ChannelKind.GuildText, "x", ParentId: 99));

        Assert.Equal(callsBefore, generator.Calls);
    }

    [Fact]
    public void DeleteChannel_Category_DetachesItsChildrenByAscendingIdThenDeletesItInOneOrderedFlow()
    {
        var (state, guild) = NewGuild();
        var lobby = Create(state, guild.Id, ChannelKind.GuildCategory, "lobby");
        var general = Create(state, guild.Id, ChannelKind.GuildText, "general", lobby.Id);
        var voice = Create(state, guild.Id, ChannelKind.GuildVoice, "voice", lobby.Id);
        var rules = Create(state, guild.Id, ChannelKind.GuildText, "rules");
        using var subscription = state.SubscribeGateway();

        var deleted = state.DeleteChannel(StateCaller.Fixture, lobby.Id).Value;

        Assert.Equal(lobby.Id, deleted.Id);
        var changes = Drain(subscription);
        Assert.Collection(
            changes,
            c => AssertUpdated(c, general.Id),
            c => AssertUpdated(c, voice.Id),
            c => Assert.Equal(lobby.Id, Assert.IsType<ChannelDeleted>(c).Channel.Id));
        Assert.True(changes[0].Number < changes[1].Number && changes[1].Number < changes[2].Number);
        Assert.Null(state.FindChannel(general.Id)!.ParentId);
        Assert.Null(state.FindChannel(voice.Id)!.ParentId);
        Assert.Null(state.FindChannel(lobby.Id));
        Assert.Equal(rules.Id, state.FindChannel(rules.Id)!.Id);
    }

    [Fact]
    public void DeleteChannel_Category_UpdatesChildrenByAscendingIdEvenWhenCreatedInAnotherOrder()
    {
        var (state, guild) = NewGuild();
        var lobby = Create(state, guild.Id, ChannelKind.GuildCategory, "lobby");
        var created1st = state.CreateGuildChannel(
            StateCaller.Fixture,
            new CreateGuildChannelInput(guild.Id, ChannelKind.GuildText, "late-id", Id: IdAt(Start.AddDays(2)), ParentId: lobby.Id)).Value;
        var created2nd = state.CreateGuildChannel(
            StateCaller.Fixture,
            new CreateGuildChannelInput(guild.Id, ChannelKind.GuildVoice, "early-id", Id: IdAt(Start.AddDays(1)), ParentId: lobby.Id)).Value;
        using var subscription = state.SubscribeGateway();

        state.DeleteChannel(StateCaller.Fixture, lobby.Id);

        Assert.Collection(
            Drain(subscription),
            c => AssertUpdated(c, created2nd.Id),
            c => AssertUpdated(c, created1st.Id),
            c => Assert.IsType<ChannelDeleted>(c));
    }

    [Fact]
    public void DeleteChannel_Category_ChildrenKeepTheirPositionAndMessages()
    {
        var (state, guild) = NewGuild();
        var lobby = Create(state, guild.Id, ChannelKind.GuildCategory, "lobby");
        var general = Create(state, guild.Id, ChannelKind.GuildText, "general", lobby.Id);
        var user = state.CreateUser(new CreateUserInput("alice"));
        var message = state.CreateMessage(StateCaller.Fixture, new CreateMessageInput(general.Id, user.Id, "hi")).Value;

        state.DeleteChannel(StateCaller.Fixture, lobby.Id);

        Assert.Equal(general.Position, state.FindChannel(general.Id)!.Position);
        Assert.Equal("hi", state.FindMessage(general.Id, message.Id)!.Content);
    }

    [Fact]
    public void DeleteChannel_CategoryWithoutChildren_PublishesOnlyChannelDeleted()
    {
        var (state, guild) = NewGuild();
        var lobby = Create(state, guild.Id, ChannelKind.GuildCategory, "lobby");
        using var subscription = state.SubscribeGateway();

        state.DeleteChannel(StateCaller.Fixture, lobby.Id);

        Assert.IsType<ChannelDeleted>(Assert.Single(Drain(subscription)));
    }

    [Fact]
    public void DeleteChannel_ChannelInACategory_DoesNotTouchItsSiblings()
    {
        var (state, guild) = NewGuild();
        var lobby = Create(state, guild.Id, ChannelKind.GuildCategory, "lobby");
        var general = Create(state, guild.Id, ChannelKind.GuildText, "general", lobby.Id);
        var other = Create(state, guild.Id, ChannelKind.GuildText, "other", lobby.Id);
        using var subscription = state.SubscribeGateway();

        state.DeleteChannel(StateCaller.Fixture, general.Id);

        Assert.IsType<ChannelDeleted>(Assert.Single(Drain(subscription)));
        Assert.Equal(lobby.Id, state.FindChannel(other.Id)!.ParentId);
    }

    [Fact]
    public void CreateGuildChannel_ChildOfADeletedCategory_IsInvalidChannelParent()
    {
        var (state, guild) = NewGuild();
        var lobby = Create(state, guild.Id, ChannelKind.GuildCategory, "lobby");
        state.DeleteChannel(StateCaller.Fixture, lobby.Id);

        var result = state.CreateGuildChannel(StateCaller.Fixture, new CreateGuildChannelInput(guild.Id, ChannelKind.GuildText, "late", ParentId: lobby.Id));

        Assert.Equal(new StateFailure(StateFailureKind.InvalidChannelParent, lobby.Id), result.Failure);
    }

    [Fact]
    public void UpdateChannel_FieldsTheTypeHasNoUseFor_AreIgnoredWithoutAChange()
    {
        var (state, guild) = NewGuild();
        var voice = Create(state, guild.Id, ChannelKind.GuildVoice, "voice");
        var lobby = Create(state, guild.Id, ChannelKind.GuildCategory, "lobby");
        using var subscription = state.SubscribeGateway();

        state.UpdateChannel(StateCaller.Fixture, new UpdateChannelInput(voice.Id, ChangeTopic: true, Topic: "x"));
        state.UpdateChannel(StateCaller.Fixture, new UpdateChannelInput(lobby.Id, Nsfw: true, RateLimitPerUser: 9));

        Assert.Empty(Drain(subscription));
        Assert.Null(state.FindChannel(voice.Id)!.Topic);
        Assert.False(state.FindChannel(lobby.Id)!.Nsfw);
    }

    [Fact]
    public void ListGuildChannels_MixedTypes_IncludesEveryTypeByAscendingId()
    {
        var (state, guild) = NewGuild();
        var a = Create(state, guild.Id, ChannelKind.GuildCategory, "a");
        var b = Create(state, guild.Id, ChannelKind.GuildVoice, "b");
        var c = Create(state, guild.Id, ChannelKind.GuildForum, "c");

        var ids = state.ListGuildChannels(StateCaller.Fixture, guild.Id).Value.Select(x => x.Id);

        Assert.Equal([a.Id, b.Id, c.Id], ids);
    }

    private static void AssertUpdated(StateChange change, ulong childId)
    {
        var updated = Assert.IsType<ChannelUpdated>(change);
        Assert.Equal(childId, updated.Channel.Id);
        Assert.Null(updated.Channel.ParentId);
    }
}
