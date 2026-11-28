using Enx.NetCord.Testing.State;
using Xunit;

namespace Enx.NetCord.Testing.Tests.Facade;

/// <summary>The channel fixtures of ADR 0013, § 6: handles, options, and the invariants the state enforces for the seed.</summary>
public sealed class SeedChannelTests
{
    [Fact]
    public async Task Category_Nominal_CreatesACategoryAndItsChannelsKeepThatParent()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");

        var lobby = guild.Category("Lobby");
        var general = lobby.TextChannel("general");
        var news = lobby.AnnouncementChannel("news");
        var voice = lobby.VoiceChannel("Voice");
        var stage = lobby.StageChannel("Stage");
        var forum = lobby.ForumChannel("Forum");

        Assert.Equal(ChannelKind.GuildCategory, discord.State.FindChannel(lobby.Id)!.Kind);
        Assert.All(
            new[] { (general.Id, ChannelKind.GuildText), (news.Id, ChannelKind.GuildAnnouncement), (voice.Id, ChannelKind.GuildVoice), (stage.Id, ChannelKind.GuildStageVoice), (forum.Id, ChannelKind.GuildForum) },
            pair =>
            {
                var snapshot = discord.State.FindChannel(pair.Item1)!;
                Assert.Equal((pair.Item2, lobby.Id, guild.Id), (snapshot.Kind, snapshot.ParentId, snapshot.GuildId));
            });
    }

    [Fact]
    public async Task GuildChannels_OutsideAnyCategory_HaveNoParent()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");

        var voice = guild.VoiceChannel("Voice");

        Assert.Null(discord.State.FindChannel(voice.Id)!.ParentId);
    }

    [Fact]
    public async Task TextChannel_Options_AreApplied()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");

        var memes = guild.TextChannel("memes", options: new() { Nsfw = true, Topic = "18+", Position = 7, Slowmode = 9 });

        var snapshot = discord.State.FindChannel(memes.Id)!;
        Assert.Equal((true, "18+", 7, 9), (snapshot.Nsfw, snapshot.Topic, snapshot.Position, snapshot.RateLimitPerUser));
    }

    [Fact]
    public async Task Channels_ExplicitId_IsUsedAndNeverReusedAcrossTypes()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var category = guild.Category("Lobby", id: 777);

        Assert.Equal(777UL, category.Id);
        Assert.Throws<InvalidOperationException>(() => guild.VoiceChannel("Voice", id: 777));
    }

    [Fact]
    public async Task Category_OptionsWithNoObjectForACategory_ThrowArgumentException()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");

        Assert.Throws<ArgumentException>(() => guild.Category("c", options: new() { Topic = "t" }));
        Assert.Throws<ArgumentException>(() => guild.Category("c", options: new() { Nsfw = true }));
        Assert.Throws<ArgumentException>(() => guild.Category("c", options: new() { Slowmode = 1 }));
        Assert.Empty(discord.State.ListGuildChannels(StateCaller.Fixture, guild.Id).Value);
    }

    [Fact]
    public async Task Category_PositionAndDefaultOptions_AreAccepted()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");

        var category = guild.Category("c", options: new() { Position = 4 });

        Assert.Equal(4, discord.State.FindChannel(category.Id)!.Position);
        guild.Category("d", options: new());
    }

    [Fact]
    public async Task VoiceAndStageChannels_TopicOption_ThrowsArgumentExceptionButNsfwAndSlowmodeApply()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");

        Assert.Throws<ArgumentException>(() => guild.VoiceChannel("Voice", options: new() { Topic = "x" }));
        Assert.Throws<ArgumentException>(() => guild.StageChannel("Stage", options: new() { Topic = "x" }));
        var voice = guild.VoiceChannel("Voice", options: new() { Nsfw = true, Slowmode = 4 });

        var snapshot = discord.State.FindChannel(voice.Id)!;
        Assert.Equal((true, 4), (snapshot.Nsfw, snapshot.RateLimitPerUser));
    }

    [Fact]
    public async Task AnnouncementChannel_Slowmode_ThrowsArgumentException()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");

        Assert.Throws<ArgumentException>(() => guild.AnnouncementChannel("news", options: new() { Slowmode = 5 }));
    }

    [Fact]
    public async Task Options_OutOfBounds_ThrowArgumentOutOfRangeException()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");

        Assert.Throws<ArgumentOutOfRangeException>(() => guild.TextChannel("t", options: new() { Topic = new string('x', 1025) }));
        Assert.Throws<ArgumentOutOfRangeException>(() => guild.ForumChannel("f", options: new() { Topic = new string('x', 4097) }));
        Assert.Throws<ArgumentOutOfRangeException>(() => guild.TextChannel("t", options: new() { Slowmode = 21601 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => guild.TextChannel("t", options: new() { Slowmode = -1 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => guild.TextChannel("t", options: new() { Position = -1 }));
        guild.ForumChannel("f", options: new() { Topic = new string('x', 4096) });
        guild.TextChannel("t", options: new() { Topic = new string('x', 1024), Slowmode = 21600 });
    }

    [Fact]
    public async Task Channels_NullNameOrZeroId_ThrowTheArgumentExceptions()
    {
        await using var discord = FakeDiscord.Create();
        var lobby = discord.Seed.Guild("Test").Category("Lobby");

        Assert.Throws<ArgumentNullException>(() => lobby.TextChannel(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => lobby.ForumChannel("f", id: 0));
    }

    [Fact]
    public async Task Category_FiftyFirstChild_ThrowsInvalidOperationExceptionAndCreatesNothing()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var lobby = guild.Category("Lobby");
        for (var i = 0; i < FakeDiscordState.MaxChannelsPerCategory; i++)
        {
            lobby.TextChannel($"c{i}");
        }

        using var subscription = discord.State.SubscribeGateway();
        var exception = Assert.Throws<InvalidOperationException>(() => lobby.VoiceChannel("one-too-many"));

        Assert.Contains(lobby.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), exception.Message, StringComparison.Ordinal);
        Assert.Empty(StateTestSupportDrain(subscription));
        Assert.Equal(51, discord.State.ListGuildChannels(StateCaller.Fixture, guild.Id).Value.Length);
    }

    [Fact]
    public async Task Guild_FiveHundredAndFirstChannel_ThrowsInvalidOperationException()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        for (var i = 0; i < FakeDiscordState.MaxChannelsPerGuild; i++)
        {
            guild.TextChannel($"c{i}");
        }

        Assert.Throws<InvalidOperationException>(() => guild.Category("late"));
        Assert.Equal(FakeDiscordState.MaxChannelsPerGuild, discord.State.ListGuildChannels(StateCaller.Fixture, guild.Id).Value.Length);
    }

    [Fact]
    public async Task Category_AfterItsGuildIsGone_ThrowsInvalidOperationException()
    {
        await using var discord = FakeDiscord.Create();
        var orphan = new SeededCategory(discord, 999, 1000);

        Assert.Throws<InvalidOperationException>(() => orphan.TextChannel("x"));
    }

    private static List<StateChange> StateTestSupportDrain(ChangeSubscription subscription)
        => Tests.State.StateTestSupport.Drain(subscription);
}
