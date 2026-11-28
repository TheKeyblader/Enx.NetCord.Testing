using System.Net;
using System.Text.Json;
using Enx.NetCord.Testing.State;
using NetCord;
using NetCord.Rest;
using Xunit;
using static Enx.NetCord.Testing.Tests.Rest.Channels.ChannelsTestSupport;
using static Enx.NetCord.Testing.Tests.Rest.Users.UsersTestSupport;

namespace Enx.NetCord.Testing.Tests.Rest.Channels;

/// <summary>Reading the channel types of ADR 0013 through the real <c>RestClient</c>: wire shape per family, hierarchy and positions.</summary>
public sealed class GetChannelTypesTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<JsonDocument> GetRawAsync(RestClient rest, ulong id)
        => await ReadJsonAsync(rest.SendRequestAsync(HttpMethod.Get, $"/channels/{id}", cancellationToken: Ct), Ct);

    private static bool Has(JsonDocument json, string name) => json.RootElement.TryGetProperty(name, out _);

    [Fact]
    [Parity("GET /channels/{channel_id}")]
    public async Task GetChannelAsync_SeededCategory_ReturnsACategoryWithItsWireShape()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var lobby = guild.Category("Lobby");
        using var rest = discord.CreateRestClient();

        var channel = await rest.GetChannelAsync(lobby.Id, cancellationToken: Ct);
        using var json = await GetRawAsync(rest, lobby.Id);

        var category = Assert.IsType<CategoryGuildChannel>(channel);
        Assert.Equal((lobby.Id, guild.Id, "Lobby", 0), (category.Id, category.GuildId, category.Name, category.Position));
        var root = json.RootElement;
        Assert.Equal(4, root.GetProperty("type").GetInt32());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("parent_id").ValueKind);
        Assert.False(root.GetProperty("nsfw").GetBoolean());
        Assert.Empty(root.GetProperty("permission_overwrites").EnumerateArray());
        Assert.All(["topic", "last_message_id", "rate_limit_per_user", "bitrate", "available_tags"], name => Assert.False(Has(json, name), name));
    }

    [Fact]
    [Parity("GET /channels/{channel_id}")]
    public async Task GetChannelAsync_ChannelsInACategory_ReportTheirParent()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var lobby = guild.Category("Lobby");
        var general = lobby.TextChannel("general");
        var news = lobby.AnnouncementChannel("news");
        var voice = lobby.VoiceChannel("Voice");
        var stage = lobby.StageChannel("Stage");
        var forum = lobby.ForumChannel("Forum");
        var rules = guild.TextChannel("rules");
        using var rest = discord.CreateRestClient();

        Assert.Equal(lobby.Id, Assert.IsType<TextGuildChannel>(await rest.GetChannelAsync(general.Id, cancellationToken: Ct)).ParentId);
        Assert.Equal(lobby.Id, Assert.IsType<AnnouncementGuildChannel>(await rest.GetChannelAsync(news.Id, cancellationToken: Ct)).ParentId);
        Assert.Equal(lobby.Id, Assert.IsType<VoiceGuildChannel>(await rest.GetChannelAsync(voice.Id, cancellationToken: Ct)).ParentId);
        Assert.Equal(lobby.Id, Assert.IsType<StageGuildChannel>(await rest.GetChannelAsync(stage.Id, cancellationToken: Ct)).ParentId);
        Assert.Equal(lobby.Id, Assert.IsType<ForumGuildChannel>(await rest.GetChannelAsync(forum.Id, cancellationToken: Ct)).ParentId);
        Assert.Null(Assert.IsType<TextGuildChannel>(await rest.GetChannelAsync(rules.Id, cancellationToken: Ct)).ParentId);
    }

    [Fact]
    [Parity("GET /channels/{channel_id}")]
    public async Task GetChannelAsync_AnnouncementChannel_ReturnsTheTextFamilyShapeWithSlowmodeZero()
    {
        await using var discord = FakeDiscord.Create();
        var news = discord.Seed.Guild("Test").AnnouncementChannel("news", options: new() { Topic = "Releases", Nsfw = true });
        using var rest = discord.CreateRestClient();

        var channel = await rest.GetChannelAsync(news.Id, cancellationToken: Ct);
        using var json = await GetRawAsync(rest, news.Id);

        var announcement = Assert.IsType<AnnouncementGuildChannel>(channel);
        Assert.Equal(("Releases", true, 0), (announcement.Topic, announcement.Nsfw, announcement.Slowmode));
        var root = json.RootElement;
        Assert.Equal(5, root.GetProperty("type").GetInt32());
        Assert.Equal(0, root.GetProperty("rate_limit_per_user").GetInt32());
        Assert.Equal(0, root.GetProperty("default_thread_rate_limit_per_user").GetInt32());
        Assert.False(Has(json, "bitrate"));
        Assert.False(Has(json, "default_auto_archive_duration"));
    }

    [Fact]
    [Parity("GET /channels/{channel_id}")]
    public async Task GetChannelAsync_VoiceChannel_ReturnsTheVoiceFamilyShape()
    {
        await using var discord = FakeDiscord.Create();
        var voice = discord.Seed.Guild("Test").VoiceChannel("Voice", options: new() { Slowmode = 3, Nsfw = true });
        using var rest = discord.CreateRestClient();

        var channel = await rest.GetChannelAsync(voice.Id, cancellationToken: Ct);
        using var json = await GetRawAsync(rest, voice.Id);

        var read = Assert.IsType<VoiceGuildChannel>(channel);
        Assert.Equal((64000, 0, null, 3, true), (read.Bitrate, read.UserLimit, read.RtcRegion, read.Slowmode, read.Nsfw));
        var root = json.RootElement;
        Assert.Equal(2, root.GetProperty("type").GetInt32());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("rtc_region").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("last_message_id").ValueKind);
        Assert.All(["topic", "video_quality_mode", "default_thread_rate_limit_per_user", "available_tags"], name => Assert.False(Has(json, name), name));
    }

    [Fact]
    [Parity("GET /channels/{channel_id}")]
    public async Task GetChannelAsync_StageChannel_ReturnsAStageChannelWithTheStageBitrate()
    {
        await using var discord = FakeDiscord.Create();
        var stage = discord.Seed.Guild("Test").StageChannel("Stage");
        using var rest = discord.CreateRestClient();

        var read = Assert.IsType<StageGuildChannel>(await rest.GetChannelAsync(stage.Id, cancellationToken: Ct));

        Assert.Equal((64000, 0), (read.Bitrate, read.UserLimit));
    }

    [Fact]
    [Parity("GET /channels/{channel_id}")]
    public async Task GetChannelAsync_ForumChannel_ReturnsTheForumFamilyShape()
    {
        await using var discord = FakeDiscord.Create();
        var forum = discord.Seed.Guild("Test").ForumChannel("Forum", options: new() { Topic = new string('g', 3000), Slowmode = 10 });
        using var rest = discord.CreateRestClient();

        var channel = await rest.GetChannelAsync(forum.Id, cancellationToken: Ct);
        using var json = await GetRawAsync(rest, forum.Id);

        var read = Assert.IsType<ForumGuildChannel>(channel);
        Assert.Equal(3000, read.Topic!.Length);
        Assert.Equal((ForumLayoutType.NotSet, null), (read.DefaultForumLayout, read.DefaultSortOrder));
        Assert.Empty(read.AvailableTags);
        var root = json.RootElement;
        Assert.Equal(15, root.GetProperty("type").GetInt32());
        Assert.Equal(10, root.GetProperty("rate_limit_per_user").GetInt32());
        Assert.Empty(root.GetProperty("available_tags").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("default_reaction_emoji").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("default_sort_order").ValueKind);
        Assert.Equal(0, root.GetProperty("default_forum_layout").GetInt32());
        Assert.False(Has(json, "bitrate"));
    }

    [Fact]
    [Parity("GET /channels/{channel_id}")]
    public async Task GetChannelAsync_MediaChannel_ReturnsAMediaForumWithoutALayout()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var media = discord.State.CreateGuildChannel(
            StateCaller.Fixture, new CreateGuildChannelInput(guild.Id, ChannelKind.GuildMedia, "gallery")).Value;
        using var rest = discord.CreateRestClient();

        var channel = await rest.GetChannelAsync(media.Id, cancellationToken: Ct);
        using var json = await GetRawAsync(rest, media.Id);

        Assert.IsType<MediaForumGuildChannel>(channel);
        Assert.Equal(16, json.RootElement.GetProperty("type").GetInt32());
        Assert.False(Has(json, "default_forum_layout"));
    }

    [Fact]
    [Parity("GET /channels/{channel_id}")]
    public async Task GetChannelAsync_SeededOptions_AreReadBack()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var memes = guild.TextChannel("memes", options: new() { Nsfw = true, Topic = "18+", Position = 7, Slowmode = 15 });
        using var rest = discord.CreateRestClient();

        var read = Assert.IsType<TextGuildChannel>(await rest.GetChannelAsync(memes.Id, cancellationToken: Ct));

        Assert.Equal((true, "18+", 7, 15), (read.Nsfw, read.Topic, read.Position, read.Slowmode));
    }

    [Fact]
    [Parity("GET /channels/{channel_id}")]
    public async Task GetChannelAsync_PositionsOfSeveralTypes_CountPerSortingGroup()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var lobby = guild.Category("Lobby");
        var general = lobby.TextChannel("general");
        var news = lobby.AnnouncementChannel("news");
        var voice = lobby.VoiceChannel("Voice");
        var rules = guild.TextChannel("rules");
        using var rest = discord.CreateRestClient();

        Assert.Equal(0, (await rest.GetChannelAsync(lobby.Id, cancellationToken: Ct) as IGuildChannel)!.Position);
        Assert.Equal(0, (await rest.GetChannelAsync(general.Id, cancellationToken: Ct) as IGuildChannel)!.Position);
        Assert.Equal(1, (await rest.GetChannelAsync(news.Id, cancellationToken: Ct) as IGuildChannel)!.Position);
        Assert.Equal(0, (await rest.GetChannelAsync(voice.Id, cancellationToken: Ct) as IGuildChannel)!.Position);
        Assert.Equal(2, (await rest.GetChannelAsync(rules.Id, cancellationToken: Ct) as IGuildChannel)!.Position);
    }

    [Fact]
    [Parity("GET /channels/{channel_id}")]
    [Parity("50001")]
    public async Task GetChannelAsync_EveryTypeOfAGuildWithoutTheBot_Returns403MissingAccess()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Elsewhere", withBot: false);
        var lobby = guild.Category("Lobby");
        ulong[] ids = [lobby.Id, guild.VoiceChannel("v").Id, guild.StageChannel("s").Id, guild.ForumChannel("f").Id, guild.AnnouncementChannel("a").Id];
        using var rest = discord.CreateRestClient();

        foreach (var id in ids)
        {
            var exception = await Assert.ThrowsAsync<RestException>(() => rest.GetChannelAsync(id, cancellationToken: Ct));
            AssertError(exception, HttpStatusCode.Forbidden, 50001);
        }
    }
}
