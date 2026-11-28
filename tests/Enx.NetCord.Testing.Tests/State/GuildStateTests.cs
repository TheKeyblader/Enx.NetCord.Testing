using Enx.NetCord.Testing.State;
using Xunit;
using static Enx.NetCord.Testing.Tests.State.StateTestSupport;

namespace Enx.NetCord.Testing.Tests.State;

/// <summary>The guild operations of M1-REST-06 (update, reads, channel placement) and the channel-name bounds of M1-DEBT-06, below the REST layer.</summary>
public sealed class GuildStateTests
{
    private static (FakeDiscordState State, GuildSnapshot Guild, ulong BotId) NewGuildWithBot()
    {
        var state = NewState();
        var bot = state.CreateBotIdentity(new CreateBotIdentityInput("bot")).User.Id;
        return (state, state.CreateGuild(new CreateGuildInput("g", WithBot: true)).Value.Guild, bot);
    }

    private static ChannelSnapshot Channel(FakeDiscordState state, ulong guildId, ChannelKind kind, string name, ulong? parentId = null)
        => state.CreateGuildChannel(StateCaller.Fixture, new CreateGuildChannelInput(guildId, kind, name, ParentId: parentId)).Value;

    [Theory]
    [InlineData("")]
    [InlineData("0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789x")]
    [Parity("50035")]
    public void CreateGuildChannel_NameOutsideOneToHundred_IsRefusedAndAllocatesNoId(string name)
    {
        var generator = new ScriptedGenerator(timestamp => IdAt(timestamp, 1));
        var state = NewState(ids: generator);
        var guild = state.CreateGuild(new CreateGuildInput("g", Id: 1000)).Value.Guild;
        var calls = generator.Calls;

        var result = state.CreateGuildChannel(StateCaller.Fixture, new CreateGuildChannelInput(guild.Id, ChannelKind.GuildText, name));

        Assert.Equal(StateFailureKind.InvalidChannelName, result.Failure.Kind);
        Assert.Equal(calls, generator.Calls);
        Assert.Empty(state.ListGuildChannels(StateCaller.Fixture, guild.Id).Value);
    }

    [Fact]
    public void CreateGuildChannel_NameOfExactlyOneAndHundredCodePoints_IsAccepted()
    {
        var (state, guild, _) = NewGuildWithBot();

        var one = Channel(state, guild.Id, ChannelKind.GuildText, "a");
        var emojis = string.Concat(Enumerable.Repeat("😀", 100)); // 100 code points, 200 UTF-16 units
        var hundred = Channel(state, guild.Id, ChannelKind.GuildText, emojis);

        Assert.Equal(["a", emojis], new[] { one.Name, hundred.Name });
    }

    [Fact]
    public void CreateGuildChannel_RefusalOrder_IsTypeThenParentThenNameThenTopicThenVoiceBounds()
    {
        var (state, guild, _) = NewGuildWithBot();

        // Everything wrong at once: the type is judged first, then the parent, then the name, then the topic.
        var type = state.CreateGuildChannel(StateCaller.Fixture, new CreateGuildChannelInput(guild.Id, (ChannelKind)14, "", ParentId: 424242, Topic: new string('t', 5000)));
        var parent = state.CreateGuildChannel(StateCaller.Fixture, new CreateGuildChannelInput(guild.Id, ChannelKind.GuildText, "", ParentId: 424242, Topic: new string('t', 5000)));
        var name = state.CreateGuildChannel(StateCaller.Fixture, new CreateGuildChannelInput(guild.Id, ChannelKind.GuildText, "", Topic: new string('t', 5000)));
        var topic = state.CreateGuildChannel(StateCaller.Fixture, new CreateGuildChannelInput(guild.Id, ChannelKind.GuildText, "x", Topic: new string('t', 5000)));
        var bitrate = state.CreateGuildChannel(
            StateCaller.Fixture, new CreateGuildChannelInput(guild.Id, ChannelKind.GuildVoice, "v", Voice: new VoiceSettings(96001, 100, null, null)));
        var userLimit = state.CreateGuildChannel(
            StateCaller.Fixture, new CreateGuildChannelInput(guild.Id, ChannelKind.GuildStageVoice, "s", Voice: new VoiceSettings(64000, 10001, null, null)));

        Assert.Equal(
            [StateFailureKind.InvalidChannelType, StateFailureKind.InvalidChannelParent, StateFailureKind.InvalidChannelName, StateFailureKind.InvalidChannelTopic, StateFailureKind.ChannelBitrateTooHigh, StateFailureKind.InvalidChannelUserLimit],
            new[] { type.Failure.Kind, parent.Failure.Kind, name.Failure.Kind, topic.Failure.Kind, bitrate.Failure.Kind, userLimit.Failure.Kind });
        Assert.Equal(10000, userLimit.Failure.Limit);
        Assert.Empty(state.ListGuildChannels(StateCaller.Fixture, guild.Id).Value);
    }

    [Fact]
    public void CreateGuildChannel_TopicOfAForumAndOfAChannelWithoutTopic_FollowTheTypeTraits()
    {
        var (state, guild, _) = NewGuildWithBot();

        var forum = state.CreateGuildChannel(StateCaller.Fixture, new CreateGuildChannelInput(guild.Id, ChannelKind.GuildForum, "f", Topic: new string('t', 4096)));
        var voice = state.CreateGuildChannel(StateCaller.Fixture, new CreateGuildChannelInput(guild.Id, ChannelKind.GuildVoice, "v", Topic: new string('t', 5000)));
        var text = state.CreateGuildChannel(StateCaller.Fixture, new CreateGuildChannelInput(guild.Id, ChannelKind.GuildText, "t", Topic: new string('t', 1025)));

        Assert.True(forum.IsSuccess);
        Assert.True(voice.IsSuccess); // a topic on a voice channel is ignored, not judged
        Assert.Null(voice.Value.Topic);
        Assert.Equal(StateFailureKind.InvalidChannelTopic, text.Failure.Kind);
    }

    [Fact]
    public void CreateGuildChannel_ThreadDefaults_AreStoredOnTheFamiliesThatHaveThem()
    {
        var (state, guild, _) = NewGuildWithBot();

        var text = state.CreateGuildChannel(StateCaller.Fixture, new CreateGuildChannelInput(guild.Id, ChannelKind.GuildText, "t", DefaultAutoArchiveDuration: 60, DefaultThreadRateLimitPerUser: 9)).Value;
        var voice = state.CreateGuildChannel(StateCaller.Fixture, new CreateGuildChannelInput(guild.Id, ChannelKind.GuildVoice, "v", DefaultAutoArchiveDuration: 60, DefaultThreadRateLimitPerUser: 9)).Value;

        Assert.Equal((60, 9), (text.DefaultAutoArchiveDuration, text.DefaultThreadRateLimitPerUser));
        Assert.Equal((null, 0), (voice.DefaultAutoArchiveDuration, voice.DefaultThreadRateLimitPerUser));
    }

    [Fact]
    public void UpdateChannel_NameOfMoreThanHundredCodePoints_IsRefused()
    {
        var (state, guild, _) = NewGuildWithBot();
        var channel = Channel(state, guild.Id, ChannelKind.GuildText, "general");

        var result = state.UpdateChannel(StateCaller.Fixture, new UpdateChannelInput(channel.Id, Name: new string('n', 101)));

        Assert.Equal(StateFailureKind.InvalidChannelName, result.Failure.Kind);
        Assert.Equal("general", state.FindChannel(channel.Id)!.Name);
    }

    [Fact]
    public void GetGuildDetails_Guild_CarriesRolesAndMemberCount()
    {
        var (state, guild, bot) = NewGuildWithBot();
        var alice = state.CreateUser(new CreateUserInput("alice"));
        state.AddMember(StateCaller.Fixture, new AddMemberInput(guild.Id, alice.Id)).ValueOrThrow();
        var role = state.CreateRole(StateCaller.Fixture, new CreateRoleInput(guild.Id, "mods")).Value;

        var details = state.GetGuildDetails(StateCaller.Bot, guild.Id).Value;

        Assert.Equal(2, details.MemberCount);
        Assert.Equal([guild.Id, role.Id], details.Roles.Select(r => r.Id));
        Assert.Equal(guild.Id, details.Guild.Id);
        Assert.NotEqual(0UL, bot);
    }

    [Fact]
    [Parity("10004")]
    public void GetGuildDetails_UnknownGuildAndGuildWithoutTheBot_AreBothUnknownForTheBotButReadableByTheFixture()
    {
        var state = NewState();
        state.CreateBotIdentity(new CreateBotIdentityInput("bot"));
        var elsewhere = state.CreateGuild(new CreateGuildInput("elsewhere")).Value.Guild;

        Assert.Equal(StateFailureKind.UnknownGuild, state.GetGuildDetails(StateCaller.Bot, 999).Failure.Kind);
        Assert.Equal(StateFailureKind.UnknownGuild, state.GetGuildDetails(StateCaller.Bot, elsewhere.Id).Failure.Kind);
        Assert.True(state.GetGuildDetails(StateCaller.Fixture, elsewhere.Id).IsSuccess);
    }

    [Fact]
    public void UpdateGuild_SameValues_CommitsAndPublishesNothing()
    {
        var (state, guild, _) = NewGuildWithBot();
        using var subscription = state.SubscribeGateway();

        var result = state.UpdateGuild(StateCaller.Bot, new UpdateGuildInput(guild.Id, Name: "g", AfkTimeout: GuildSettings.DefaultAfkTimeout, VerificationLevel: 0));

        Assert.True(result.IsSuccess);
        Assert.Empty(Drain(subscription));
    }

    [Fact]
    public void UpdateGuild_ChannelSettingOfTheRightType_IsStoredAndAnInvalidOneRefusesTheWholeUpdate()
    {
        var (state, guild, _) = NewGuildWithBot();
        var voice = Channel(state, guild.Id, ChannelKind.GuildVoice, "afk");
        var news = Channel(state, guild.Id, ChannelKind.GuildAnnouncement, "news");
        var category = Channel(state, guild.Id, ChannelKind.GuildCategory, "cat");
        var forum = Channel(state, guild.Id, ChannelKind.GuildForum, "forum");
        var stage = Channel(state, guild.Id, ChannelKind.GuildStageVoice, "stage");

        var accepted = state.UpdateGuild(
            StateCaller.Bot,
            new UpdateGuildInput(guild.Id, AfkChannel: Change<ulong?>.To(voice.Id), SystemChannel: Change<ulong?>.To(news.Id)));
        var categoryAsSystem = state.UpdateGuild(StateCaller.Bot, new UpdateGuildInput(guild.Id, Name: "renamed", SystemChannel: Change<ulong?>.To(category.Id)));
        var forumAsRules = state.UpdateGuild(StateCaller.Bot, new UpdateGuildInput(guild.Id, RulesChannel: Change<ulong?>.To(forum.Id)));
        var stageAsAfk = state.UpdateGuild(StateCaller.Bot, new UpdateGuildInput(guild.Id, AfkChannel: Change<ulong?>.To(stage.Id)));

        Assert.Equal((voice.Id, news.Id), (accepted.Value.Guild.Settings.AfkChannelId, accepted.Value.Guild.Settings.SystemChannelId));
        Assert.Equal(("system_channel_id", category.Id), (categoryAsSystem.Failure.Field, categoryAsSystem.Failure.ResourceId));
        Assert.Equal("rules_channel_id", forumAsRules.Failure.Field);
        Assert.Equal("afk_channel_id", stageAsAfk.Failure.Field);
        Assert.Equal("g", state.FindGuild(guild.Id)!.Name);
    }

    [Fact]
    public void UpdateGuild_NullChannelSetting_ClearsItAndPublishesOneGuildUpdated()
    {
        var (state, guild, _) = NewGuildWithBot();
        var system = Channel(state, guild.Id, ChannelKind.GuildText, "system");
        state.UpdateGuild(StateCaller.Bot, new UpdateGuildInput(guild.Id, SystemChannel: Change<ulong?>.To(system.Id)));
        using var subscription = state.SubscribeGateway();

        var result = state.UpdateGuild(StateCaller.Bot, new UpdateGuildInput(guild.Id, SystemChannel: Change<ulong?>.To(null)));

        Assert.Null(result.Value.Guild.Settings.SystemChannelId);
        Assert.IsType<GuildUpdated>(Assert.Single(Drain(subscription)));
    }

    [Fact]
    public void UpdateGuild_GuildWithoutTheBot_IsHiddenForTheBotAndNothingChanges()
    {
        var state = NewState();
        state.CreateBotIdentity(new CreateBotIdentityInput("bot"));
        var elsewhere = state.CreateGuild(new CreateGuildInput("elsewhere")).Value.Guild;

        var result = state.UpdateGuild(StateCaller.Bot, new UpdateGuildInput(elsewhere.Id, Name: "hijacked"));

        Assert.Equal(StateFailureKind.UnknownGuild, result.Failure.Kind);
        Assert.Equal("elsewhere", state.FindGuild(elsewhere.Id)!.Name);
    }

    [Fact]
    public void DeleteChannel_ChannelThatSeveralSettingsPointTo_ClearsAllInTheSameCommit()
    {
        var (state, guild, _) = NewGuildWithBot();
        var shared = Channel(state, guild.Id, ChannelKind.GuildText, "shared");
        var other = Channel(state, guild.Id, ChannelKind.GuildText, "other");
        state.UpdateGuild(
            StateCaller.Bot,
            new UpdateGuildInput(
                guild.Id,
                SystemChannel: Change<ulong?>.To(shared.Id),
                RulesChannel: Change<ulong?>.To(shared.Id),
                PublicUpdatesChannel: Change<ulong?>.To(other.Id)));
        using var subscription = state.SubscribeGateway();

        state.DeleteChannel(StateCaller.Bot, shared.Id).ValueOrThrow();

        var settings = state.FindGuild(guild.Id)!.Settings;
        Assert.Equal((null, null, other.Id), (settings.SystemChannelId, settings.RulesChannelId, settings.PublicUpdatesChannelId));
        Assert.Equal([typeof(ChannelDeleted), typeof(GuildUpdated)], Drain(subscription).Select(change => change.GetType()));
    }

    [Fact]
    public void UpdateChannelPositions_ParentChangeIsCountedPerChannelAgainstTheCurrentParent()
    {
        var (state, guild, _) = NewGuildWithBot();
        var lobby = Channel(state, guild.Id, ChannelKind.GuildCategory, "lobby");
        var archive = Channel(state, guild.Id, ChannelKind.GuildCategory, "archive");
        var inside = Channel(state, guild.Id, ChannelKind.GuildText, "inside", lobby.Id);
        var other = Channel(state, guild.Id, ChannelKind.GuildText, "other");
        var third = Channel(state, guild.Id, ChannelKind.GuildText, "third");

        // One channel named twice with two parents is still one channel changing its parent.
        var one = state.UpdateChannelPositions(
            StateCaller.Bot,
            guild.Id,
            [
                new ChannelPlacement(other.Id, null, Change<ulong?>.To(lobby.Id)),
                new ChannelPlacement(other.Id, 4, Change<ulong?>.To(archive.Id)),
                new ChannelPlacement(inside.Id, null, Change<ulong?>.To(lobby.Id)), // the current parent: no change
            ]);
        var two = state.UpdateChannelPositions(
            StateCaller.Bot,
            guild.Id,
            [new ChannelPlacement(other.Id, null, Change<ulong?>.To(null)), new ChannelPlacement(third.Id, null, Change<ulong?>.To(lobby.Id))]);

        Assert.Equal(1, one.Value);
        var moved = state.FindChannel(other.Id)!;
        Assert.Equal((archive.Id, 4), (moved.ParentId, moved.Position));
        Assert.Equal(StateFailureKind.TooManyParentChanges, two.Failure.Kind);
        Assert.Null(state.FindChannel(third.Id)!.ParentId);
    }

    [Fact]
    public void UpdateChannelPositions_CategoryGivenAParent_IsRefusedWithTheCategoryRule()
    {
        var (state, guild, _) = NewGuildWithBot();
        var lobby = Channel(state, guild.Id, ChannelKind.GuildCategory, "lobby");
        var other = Channel(state, guild.Id, ChannelKind.GuildCategory, "other");

        var result = state.UpdateChannelPositions(StateCaller.Bot, guild.Id, [new ChannelPlacement(other.Id, null, Change<ulong?>.To(lobby.Id))]);

        Assert.Equal(StateFailureKind.ChannelCannotHaveParent, result.Failure.Kind);
    }

    [Fact]
    public void UpdateChannelPositions_SequenceOfEntries_IsAppliedInOrderSoTheLastPositionWins()
    {
        var (state, guild, _) = NewGuildWithBot();
        var a = Channel(state, guild.Id, ChannelKind.GuildText, "a");

        var result = state.UpdateChannelPositions(
            StateCaller.Bot,
            guild.Id,
            [new ChannelPlacement(a.Id, 5, default), new ChannelPlacement(a.Id, 0, default)]); // back to where it was: no net change

        Assert.Equal(0, result.Value);
        Assert.Equal(0, state.FindChannel(a.Id)!.Position);
    }

    [Fact]
    public void UpdateChannelPositions_NullPlacementList_Throws()
    {
        var (state, guild, _) = NewGuildWithBot();

        Assert.Throws<ArgumentNullException>(() => state.UpdateChannelPositions(StateCaller.Bot, guild.Id, null!));
    }
}
