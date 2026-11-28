using Enx.NetCord.Testing.State;
using Xunit;
using static Enx.NetCord.Testing.Tests.State.StateTestSupport;

namespace Enx.NetCord.Testing.Tests.State;

/// <summary>The parent, conversion and per-type rules of <see cref="FakeDiscordState.UpdateChannel"/> (ADR 0013, § 2, § 5), below the REST layer.</summary>
public sealed class ChannelUpdateStateTests
{
    private static (FakeDiscordState State, GuildSnapshot Guild) NewGuild()
    {
        var state = NewState();
        return (state, state.CreateGuild(new CreateGuildInput("g")).Value.Guild);
    }

    private static ChannelSnapshot Create(
        FakeDiscordState state, ulong guildId, ChannelKind kind, string name, ulong? parentId = null)
        => state.CreateGuildChannel(StateCaller.Fixture, new CreateGuildChannelInput(guildId, kind, name, ParentId: parentId)).Value;

    [Fact]
    public void UpdateChannel_ChangeParentWithoutAnId_LeavesTheCategoryAndPublishesOneUpdate()
    {
        var (state, guild) = NewGuild();
        var lobby = Create(state, guild.Id, ChannelKind.GuildCategory, "lobby");
        var general = Create(state, guild.Id, ChannelKind.GuildText, "general", lobby.Id);
        using var subscription = state.SubscribeGateway();

        var result = state.UpdateChannel(StateCaller.Fixture, new UpdateChannelInput(general.Id, ChangeParent: true)).Value;

        Assert.Null(result.ParentId);
        Assert.Null(state.FindChannel(general.Id)!.ParentId);
        Assert.Equal(general.Id, Assert.IsType<ChannelUpdated>(Assert.Single(Drain(subscription))).Channel.Id);
    }

    [Fact]
    public void UpdateChannel_ParentIdWithoutChangeParent_IsNotAMove()
    {
        // The flag is what says "the key was sent": an ID alone changes nothing.
        var (state, guild) = NewGuild();
        var lobby = Create(state, guild.Id, ChannelKind.GuildCategory, "lobby");
        var general = Create(state, guild.Id, ChannelKind.GuildText, "general");

        state.UpdateChannel(StateCaller.Fixture, new UpdateChannelInput(general.Id, ParentId: lobby.Id));

        Assert.Null(state.FindChannel(general.Id)!.ParentId);
    }

    [Fact]
    public void UpdateChannel_ChangeParentOfACategoryToNull_ChangesNothing()
    {
        var (state, guild) = NewGuild();
        var lobby = Create(state, guild.Id, ChannelKind.GuildCategory, "lobby");
        using var subscription = state.SubscribeGateway();

        state.UpdateChannel(StateCaller.Fixture, new UpdateChannelInput(lobby.Id, ChangeParent: true));

        Assert.Empty(Drain(subscription));
    }

    [Fact]
    public void UpdateChannel_FullCategoryAndSameParent_IsAcceptedWithoutAChange()
    {
        var (state, guild) = NewGuild();
        var lobby = Create(state, guild.Id, ChannelKind.GuildCategory, "lobby");
        var children = Enumerable.Range(0, FakeDiscordState.MaxChannelsPerCategory)
            .Select(i => Create(state, guild.Id, ChannelKind.GuildText, $"c{i}", lobby.Id))
            .ToList();
        using var subscription = state.SubscribeGateway();

        var result = state.UpdateChannel(StateCaller.Fixture, new UpdateChannelInput(children[0].Id, ChangeParent: true, ParentId: lobby.Id));

        Assert.True(result.IsSuccess);
        Assert.Empty(Drain(subscription));
    }

    [Fact]
    public void UpdateChannel_MoveIntoAFullCategory_IsChannelParentFull()
    {
        var (state, guild) = NewGuild();
        var lobby = Create(state, guild.Id, ChannelKind.GuildCategory, "lobby");
        for (var i = 0; i < FakeDiscordState.MaxChannelsPerCategory; i++)
        {
            Create(state, guild.Id, ChannelKind.GuildText, $"c{i}", lobby.Id);
        }

        var outsider = Create(state, guild.Id, ChannelKind.GuildVoice, "outsider");

        var result = state.UpdateChannel(StateCaller.Fixture, new UpdateChannelInput(outsider.Id, ChangeParent: true, ParentId: lobby.Id));

        Assert.Equal(new StateFailure(StateFailureKind.ChannelParentFull, lobby.Id), result.Failure);
    }

    [Fact]
    public void UpdateChannel_ParentRefusals_FollowTheOrderOfTheInvariants()
    {
        var (state, guild) = NewGuild();
        var other = state.CreateGuild(new CreateGuildInput("other")).Value.Guild;
        var foreign = Create(state, other.Id, ChannelKind.GuildCategory, "foreign");
        var lobby = Create(state, guild.Id, ChannelKind.GuildCategory, "lobby");
        var archive = Create(state, guild.Id, ChannelKind.GuildCategory, "archive");
        var rules = Create(state, guild.Id, ChannelKind.GuildText, "rules");

        StateFailure Refused(ulong channelId, ulong parentId)
            => state.UpdateChannel(StateCaller.Fixture, new UpdateChannelInput(channelId, ChangeParent: true, ParentId: parentId)).Failure;

        Assert.Equal(StateFailureKind.InvalidChannelParent, Refused(lobby.Id, 12345).Kind);
        Assert.Equal(StateFailureKind.InvalidChannelParent, Refused(rules.Id, foreign.Id).Kind);
        Assert.Equal(StateFailureKind.ChannelParentNotCategory, Refused(lobby.Id, rules.Id).Kind);
        Assert.Equal(StateFailureKind.ChannelCannotHaveParent, Refused(lobby.Id, archive.Id).Kind);
    }

    [Fact]
    public void UpdateChannel_Refusals_FollowTheOrderTypeParentNameTopicBitrateUserLimit()
    {
        var (state, guild) = NewGuild();
        var voice = Create(state, guild.Id, ChannelKind.GuildVoice, "voice");
        var text = Create(state, guild.Id, ChannelKind.GuildText, "text");

        StateFailureKind Refused(UpdateChannelInput input) => state.UpdateChannel(StateCaller.Fixture, input).Failure.Kind;

        var longTopic = new string('t', 1025);
        Assert.Equal(
            StateFailureKind.InvalidChannelType,
            Refused(new UpdateChannelInput(text.Id, Name: "", ChangeTopic: true, Topic: longTopic, Type: 2, ChangeParent: true, ParentId: 1)));
        Assert.Equal(
            StateFailureKind.InvalidChannelParent,
            Refused(new UpdateChannelInput(text.Id, Name: "", ChangeTopic: true, Topic: longTopic, ChangeParent: true, ParentId: 1)));
        Assert.Equal(
            StateFailureKind.InvalidChannelName,
            Refused(new UpdateChannelInput(text.Id, Name: "", ChangeTopic: true, Topic: longTopic)));
        Assert.Equal(
            StateFailureKind.InvalidChannelTopic,
            Refused(new UpdateChannelInput(text.Id, ChangeTopic: true, Topic: longTopic)));
        Assert.Equal(
            StateFailureKind.ChannelBitrateTooHigh,
            Refused(new UpdateChannelInput(voice.Id, Bitrate: 96001, UserLimit: 100)));
        Assert.Equal(
            new StateFailure(StateFailureKind.InvalidChannelUserLimit, voice.Id, 99),
            state.UpdateChannel(StateCaller.Fixture, new UpdateChannelInput(voice.Id, UserLimit: 100)).Failure);
    }

    [Fact]
    public void UpdateChannel_TextToAnnouncement_ConvertsInPlaceWithOneUpdate()
    {
        var (state, guild) = NewGuild();
        var general = state.CreateGuildChannel(
            StateCaller.Fixture,
            new CreateGuildChannelInput(guild.Id, ChannelKind.GuildText, "general", Topic: "hi", RateLimitPerUser: 30)).Value;
        using var subscription = state.SubscribeGateway();

        var result = state.UpdateChannel(StateCaller.Fixture, new UpdateChannelInput(general.Id, Type: 5, Name: "news", RateLimitPerUser: 10)).Value;

        Assert.Equal((general.Id, ChannelKind.GuildAnnouncement, "news", "hi", 0), (result.Id, result.Kind, result.Name, result.Topic, result.RateLimitPerUser));
        Assert.Equal(ChannelKind.GuildAnnouncement, state.FindChannel(general.Id)!.Kind);
        Assert.Equal(ChannelKind.GuildAnnouncement, Assert.IsType<ChannelUpdated>(Assert.Single(Drain(subscription))).Channel.Kind);
    }

    [Fact]
    public void UpdateChannel_RefusedConversion_WritesNothing()
    {
        var (state, guild) = NewGuild();
        var general = Create(state, guild.Id, ChannelKind.GuildText, "general");
        using var subscription = state.SubscribeGateway();

        var result = state.UpdateChannel(StateCaller.Fixture, new UpdateChannelInput(general.Id, Type: 2, Name: "renamed"));

        Assert.Equal(new StateFailure(StateFailureKind.InvalidChannelType, general.Id), result.Failure);
        Assert.Equal("general", state.FindChannel(general.Id)!.Name);
        Assert.Empty(Drain(subscription));
    }

    [Fact]
    public void UpdateChannel_VoiceFields_UpdateTheBlockAndNothingElse()
    {
        var (state, guild) = NewGuild();
        var voice = Create(state, guild.Id, ChannelKind.GuildVoice, "voice");

        var result = state.UpdateChannel(
            StateCaller.Fixture,
            new UpdateChannelInput(voice.Id, Bitrate: 8000, UserLimit: 5, ChangeRtcRegion: true, RtcRegion: "us-east", VideoQualityMode: 2)).Value;

        Assert.Equal(new VoiceSettings(8000, 5, "us-east", 2), result.Voice);
        Assert.Null(result.Forum);
    }

    [Fact]
    public void UpdateChannel_SameVoiceValues_PublishNothing()
    {
        var (state, guild) = NewGuild();
        var voice = Create(state, guild.Id, ChannelKind.GuildVoice, "voice");
        using var subscription = state.SubscribeGateway();

        state.UpdateChannel(StateCaller.Fixture, new UpdateChannelInput(voice.Id, Bitrate: 64000, UserLimit: 0, ChangeRtcRegion: true));

        Assert.Empty(Drain(subscription));
    }

    [Fact]
    public void UpdateChannel_ForumFields_UpdateTheForumBlockAndLayoutIsTypeFifteenOnly()
    {
        var (state, guild) = NewGuild();
        var forum = Create(state, guild.Id, ChannelKind.GuildForum, "forum");
        var media = Create(state, guild.Id, ChannelKind.GuildMedia, "media");

        var forumResult = state.UpdateChannel(
            StateCaller.Fixture, new UpdateChannelInput(forum.Id, ChangeDefaultSortOrder: true, DefaultSortOrder: 1, DefaultForumLayout: 2)).Value;
        var mediaResult = state.UpdateChannel(
            StateCaller.Fixture, new UpdateChannelInput(media.Id, ChangeDefaultSortOrder: true, DefaultSortOrder: 1, DefaultForumLayout: 2)).Value;

        Assert.Equal(new ForumSettings(1, 2), forumResult.Forum);
        Assert.Equal(new ForumSettings(1, 0), mediaResult.Forum);
    }

    [Fact]
    public void UpdateChannel_ThreadDefaults_AreForTextAndForumFamiliesOnly()
    {
        var (state, guild) = NewGuild();
        var text = Create(state, guild.Id, ChannelKind.GuildText, "text");
        var voice = Create(state, guild.Id, ChannelKind.GuildVoice, "voice");

        var textResult = state.UpdateChannel(
            StateCaller.Fixture, new UpdateChannelInput(text.Id, DefaultAutoArchiveDuration: 4320, DefaultThreadRateLimitPerUser: 15)).Value;
        var voiceResult = state.UpdateChannel(
            StateCaller.Fixture, new UpdateChannelInput(voice.Id, DefaultAutoArchiveDuration: 4320, DefaultThreadRateLimitPerUser: 15)).Value;

        Assert.Equal((4320, 15), (textResult.DefaultAutoArchiveDuration, textResult.DefaultThreadRateLimitPerUser));
        Assert.Equal((null, 0), (voiceResult.DefaultAutoArchiveDuration, voiceResult.DefaultThreadRateLimitPerUser));
    }

    [Fact]
    public void UpdateChannel_ParentAndConversionOnADm_AreIgnored()
    {
        var state = NewState();
        var alice = state.CreateUser(new CreateUserInput("alice"));
        var dm = state.GetOrCreateDmChannel(new CreateDmChannelInput(alice.Id)).Value.Channel;
        using var subscription = state.SubscribeGateway();

        var result = state.UpdateChannel(StateCaller.Fixture, new UpdateChannelInput(dm.Id, Type: 5, ChangeParent: true, ParentId: 12345, Bitrate: 96001));

        Assert.True(result.IsSuccess);
        Assert.Empty(Drain(subscription));
    }
}
