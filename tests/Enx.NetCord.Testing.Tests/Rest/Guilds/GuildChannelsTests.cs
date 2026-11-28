using System.Net;
using System.Text.Json;
using Enx.NetCord.Testing.State;
using NetCord;
using NetCord.Rest;
using Xunit;
using static Enx.NetCord.Testing.Tests.Rest.Guilds.GuildsTestSupport;
using static Enx.NetCord.Testing.Tests.Rest.Users.UsersTestSupport;
using static Enx.NetCord.Testing.Tests.State.StateTestSupport;

namespace Enx.NetCord.Testing.Tests.Rest.Guilds;

/// <summary>
/// <c>GET</c> and <c>POST /guilds/{guild_id}/channels</c> through the real NetCord <see cref="RestClient"/>
/// (the <c>PATCH</c> of positions is in <see cref="GuildChannelPositionsTests"/>).
/// </summary>
public sealed class GuildChannelsTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    [Parity("GET /guilds/{guild_id}/channels")]
    public async Task GetGuildChannelsAsync_MixedTypes_ReturnsEveryChannelTypedByAscendingId()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var lobby = guild.Category("Lobby");
        var text = lobby.TextChannel("general", options: new() { Topic = "Hello", Slowmode = 5 });
        var news = lobby.AnnouncementChannel("news");
        var voice = lobby.VoiceChannel("voice");
        var stage = guild.StageChannel("stage");
        var forum = guild.ForumChannel("forum");
        using var rest = discord.CreateRestClient();

        var channels = await rest.GetGuildChannelsAsync(guild.Id, cancellationToken: Ct);

        Assert.Equal([lobby.Id, text.Id, news.Id, voice.Id, stage.Id, forum.Id], channels.Select(c => c.Id));
        Assert.IsType<CategoryGuildChannel>(channels[0]);
        var readText = Assert.IsType<TextGuildChannel>(channels[1]);
        Assert.Equal(("Hello", 5, lobby.Id), (readText.Topic, readText.Slowmode, readText.ParentId));
        Assert.IsType<AnnouncementGuildChannel>(channels[2]);
        Assert.Equal(64000, Assert.IsType<VoiceGuildChannel>(channels[3]).Bitrate);
        Assert.IsType<StageGuildChannel>(channels[4]);
        Assert.IsType<ForumGuildChannel>(channels[5]);
    }

    [Fact]
    [Parity("GET /guilds/{guild_id}/channels")]
    public async Task GetGuildChannelsAsync_OtherGuildsAndDirectMessages_AreNotListed()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        discord.Seed.Guild("Other").TextChannel("elsewhere");
        using var rest = discord.CreateRestClient();
        await rest.GetDMChannelAsync(discord.Seed.User("alice"), cancellationToken: Ct);

        var channels = await rest.GetGuildChannelsAsync(guild.Id, cancellationToken: Ct);

        Assert.Equal([general.Id], channels.Select(c => c.Id));
    }

    [Fact]
    [Parity("GET /guilds/{guild_id}/channels")]
    public async Task GetGuildChannelsAsync_GuildWithoutChannels_ReturnsAnEmptyList()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        using var rest = discord.CreateRestClient();

        Assert.Empty(await rest.GetGuildChannelsAsync(guild.Id, cancellationToken: Ct));
        Assert.Equal(JsonValueKind.Array, discord.Recorded.Requests[^1].ResponseBody!.Value.ValueKind);
    }

    [Fact]
    [Parity("GET /guilds/{guild_id}/channels")]
    [Parity("10004")]
    [Parity("50001")]
    public async Task GetGuildChannelsAsync_UnknownGuildAndGuildWithoutTheBot_Return10004And50001()
    {
        await using var discord = FakeDiscord.Create();
        var elsewhere = discord.Seed.Guild("Elsewhere", withBot: false);
        elsewhere.TextChannel("general");
        using var rest = discord.CreateRestClient();

        var unknown = await Assert.ThrowsAsync<RestException>(() => rest.GetGuildChannelsAsync(424242, cancellationToken: Ct));
        var denied = await Assert.ThrowsAsync<RestException>(() => rest.GetGuildChannelsAsync(elsewhere.Id, cancellationToken: Ct));

        AssertError(unknown, HttpStatusCode.NotFound, 10004);
        AssertError(denied, HttpStatusCode.Forbidden, 50001);
    }

    [Fact]
    [Parity("POST /guilds/{guild_id}/channels")]
    public async Task CreateGuildChannelAsync_TextChannel_ReturnsItRereadsItAndPublishesChannelCreated()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var existing = guild.TextChannel("first");
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        var created = await rest.CreateGuildChannelAsync(
            guild.Id,
            new GuildChannelProperties("general", ChannelType.TextGuildChannel) { Topic = "Hello", Slowmode = 10, Nsfw = true },
            cancellationToken: Ct);

        var text = Assert.IsType<TextGuildChannel>(created);
        Assert.Equal((guild.Id, "general", "Hello", 10, true), (text.GuildId, text.Name, text.Topic, text.Slowmode, text.Nsfw));
        Assert.Equal(1, text.Position); // one more than the first text channel (ADR 0013, § 4)
        Assert.Equal("general", Assert.IsType<TextGuildChannel>(await rest.GetChannelAsync(created.Id, cancellationToken: Ct)).Name);
        Assert.Equal([existing.Id, created.Id], (await rest.GetGuildChannelsAsync(guild.Id, cancellationToken: Ct)).Select(c => c.Id));
        var attempt = discord.Recorded.Requests.Single(r => r.Method == "POST");
        Assert.Equal(HttpStatusCode.Created, attempt.StatusCode);
        var change = Assert.IsType<ChannelCreated>(Assert.Single(Drain(subscription)));
        Assert.Equal((created.Id, guild.Id), (change.Channel.Id, change.Channel.GuildId));
    }

    [Theory]
    [InlineData(ChannelType.TextGuildChannel, 0)]
    [InlineData(ChannelType.VoiceGuildChannel, 2)]
    [InlineData(ChannelType.CategoryChannel, 4)]
    [InlineData(ChannelType.AnnouncementGuildChannel, 5)]
    [InlineData(ChannelType.StageGuildChannel, 13)]
    [InlineData(ChannelType.ForumGuildChannel, 15)]
    [InlineData(ChannelType.MediaForumGuildChannel, 16)]
    [Parity("POST /guilds/{guild_id}/channels")]
    public async Task CreateGuildChannelAsync_EachChannelType_IsCreatedWithTheTypeOfTheRequest(ChannelType type, int expected)
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        using var rest = discord.CreateRestClient();

        var created = await rest.CreateGuildChannelAsync(guild.Id, new GuildChannelProperties("channel", type), cancellationToken: Ct);

        Assert.Equal(expected, (int)discord.State.FindChannel(created.Id)!.Kind);
        Assert.Equal(created.GetType(), (await rest.GetChannelAsync(created.Id, cancellationToken: Ct)).GetType()); // read back as the same NetCord type
    }

    [Fact]
    [Parity("POST /guilds/{guild_id}/channels")]
    public async Task CreateGuildChannelAsync_VoiceChannelWithSettings_StoresThem()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var lobby = guild.Category("Lobby");
        using var rest = discord.CreateRestClient();

        var created = await rest.CreateGuildChannelAsync(
            guild.Id,
            new GuildChannelProperties("voice", ChannelType.VoiceGuildChannel)
            {
                Bitrate = 96000,
                UserLimit = 5,
                RtcRegion = "rotterdam",
                VideoQualityMode = VideoQualityMode.Full,
                ParentId = lobby.Id,
                Position = 3,
                Topic = "ignored: a voice channel has no topic",
            },
            cancellationToken: Ct);

        var voice = Assert.IsType<VoiceGuildChannel>(created);
        Assert.Equal((96000, 5, "rotterdam", lobby.Id, 3), (voice.Bitrate, voice.UserLimit, voice.RtcRegion, voice.ParentId, voice.Position));
        Assert.Equal(VideoQualityMode.Full, voice.VideoQualityMode);
        Assert.Null(discord.State.FindChannel(voice.Id)!.Topic);
    }

    [Fact]
    [Parity("POST /guilds/{guild_id}/channels")]
    public async Task CreateGuildChannelAsync_PositionDefaults_AreCountedPerSortingGroup()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        using var rest = discord.CreateRestClient();

        var text = await rest.CreateGuildChannelAsync(guild.Id, new GuildChannelProperties("t", ChannelType.TextGuildChannel), cancellationToken: Ct);
        var voice = await rest.CreateGuildChannelAsync(guild.Id, new GuildChannelProperties("v", ChannelType.VoiceGuildChannel), cancellationToken: Ct);
        var secondText = await rest.CreateGuildChannelAsync(guild.Id, new GuildChannelProperties("t2", ChannelType.TextGuildChannel), cancellationToken: Ct);
        var category = await rest.CreateGuildChannelAsync(guild.Id, new GuildChannelProperties("c", ChannelType.CategoryChannel), cancellationToken: Ct);

        Assert.Equal((0, 0, 1, 0), (text.Position, voice.Position, secondText.Position, category.Position));
    }

    [Fact]
    [Parity("POST /guilds/{guild_id}/channels")]
    public async Task CreateGuildChannelAsync_ForumWithThreadDefaults_ReadsThemBack()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        using var rest = discord.CreateRestClient();

        var created = await rest.CreateGuildChannelAsync(
            guild.Id,
            new GuildChannelProperties("help", ChannelType.ForumGuildChannel)
            {
                Topic = "Guidelines",
                DefaultAutoArchiveDuration = ThreadArchiveDuration.OneDay,
                DefaultThreadSlowmode = 30,
                DefaultSortOrder = SortOrderType.CreationDate,
                DefaultForumLayout = ForumLayoutType.GalleryView,
            },
            cancellationToken: Ct);

        var forum = Assert.IsType<ForumGuildChannel>(created);
        Assert.Equal(("Guidelines", 30), (forum.Topic, forum.DefaultThreadSlowmode));
        Assert.Equal((SortOrderType.CreationDate, ForumLayoutType.GalleryView), (forum.DefaultSortOrder, forum.DefaultForumLayout));
        Assert.Equal(1440, discord.State.FindChannel(forum.Id)!.DefaultAutoArchiveDuration);
    }

    [Fact]
    [Parity("POST /guilds/{guild_id}/channels")]
    public async Task CreateGuildChannelAsync_AnnouncementWithSlowmode_IgnoresTheSlowmode()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        using var rest = discord.CreateRestClient();

        var created = await rest.CreateGuildChannelAsync(
            guild.Id,
            new GuildChannelProperties("news", ChannelType.AnnouncementGuildChannel) { Slowmode = 30 },
            cancellationToken: Ct);

        Assert.Equal(0, Assert.IsType<AnnouncementGuildChannel>(created).Slowmode);
    }

    [Fact]
    [Parity("POST /guilds/{guild_id}/channels")]
    public async Task CreateGuildChannelAsync_ParentCategory_PutsTheChannelInIt()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var lobby = guild.Category("Lobby");
        using var rest = discord.CreateRestClient();

        var created = await rest.CreateGuildChannelAsync(
            guild.Id,
            new GuildChannelProperties("general", ChannelType.TextGuildChannel) { ParentId = lobby.Id },
            cancellationToken: Ct);

        Assert.Equal(lobby.Id, Assert.IsType<TextGuildChannel>(created).ParentId);
    }

    [Fact]
    [Parity("POST /guilds/{guild_id}/channels")]
    [Parity("50035")]
    public async Task CreateGuildChannelAsync_InvalidParents_Return50035OnParentIdAndCreateNothing()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var rules = guild.TextChannel("rules");
        var lobby = guild.Category("Lobby");
        var otherCategory = discord.Seed.Guild("Other").Category("Elsewhere");
        for (var i = 0; i < FakeDiscordState.MaxChannelsPerCategory; i++)
        {
            lobby.TextChannel($"c{i}");
        }

        using var rest = discord.CreateRestClient();
        var before = discord.State.ListGuildChannels(StateCaller.Fixture, guild.Id).Value.Length;

        var unknown = await Assert.ThrowsAsync<RestException>(() => rest.CreateGuildChannelAsync(guild.Id, new("x", ChannelType.TextGuildChannel) { ParentId = 424242 }, cancellationToken: Ct));
        var notCategory = await Assert.ThrowsAsync<RestException>(() => rest.CreateGuildChannelAsync(guild.Id, new("x", ChannelType.TextGuildChannel) { ParentId = rules.Id }, cancellationToken: Ct));
        var otherGuild = await Assert.ThrowsAsync<RestException>(() => rest.CreateGuildChannelAsync(guild.Id, new("x", ChannelType.TextGuildChannel) { ParentId = otherCategory.Id }, cancellationToken: Ct));
        var categoryInCategory = await Assert.ThrowsAsync<RestException>(() => rest.CreateGuildChannelAsync(guild.Id, new("x", ChannelType.CategoryChannel) { ParentId = lobby.Id }, cancellationToken: Ct));
        var full = await Assert.ThrowsAsync<RestException>(() => rest.CreateGuildChannelAsync(guild.Id, new("x", ChannelType.TextGuildChannel) { ParentId = lobby.Id }, cancellationToken: Ct));

        Assert.Equal("CHANNEL_PARENT_INVALID", Assert.Single(FieldErrors(unknown, "parent_id")).Code);
        Assert.Equal("CHANNEL_PARENT_INVALID_TYPE", Assert.Single(FieldErrors(notCategory, "parent_id")).Code);
        Assert.Equal("CHANNEL_PARENT_INVALID", Assert.Single(FieldErrors(otherGuild, "parent_id")).Code);
        Assert.Equal("CHANNEL_PARENT_INVALID", Assert.Single(FieldErrors(categoryInCategory, "parent_id")).Code);
        Assert.Equal("CHANNEL_PARENT_MAX_CHANNELS", Assert.Single(FieldErrors(full, "parent_id")).Code);
        Assert.Equal(before, discord.State.ListGuildChannels(StateCaller.Fixture, guild.Id).Value.Length);
    }

    [Fact]
    [Parity("POST /guilds/{guild_id}/channels")]
    [Parity("30013")]
    public async Task CreateGuildChannelAsync_GuildWith500Channels_Returns30013AndAllocatesNoId()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");

        // Ten categories hold 490 channels (a category takes at most 50): 500 channels in all.
        var categories = Enumerable.Range(0, 10).Select(i => guild.Category($"cat{i}")).ToList();
        for (var i = 0; i < FakeDiscordState.MaxChannelsPerGuild - categories.Count; i++)
        {
            categories[i / FakeDiscordState.MaxChannelsPerCategory].TextChannel($"c{i}");
        }

        Assert.Equal(FakeDiscordState.MaxChannelsPerGuild, discord.State.ListGuildChannels(StateCaller.Fixture, guild.Id).Value.Length);
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(
            () => rest.CreateGuildChannelAsync(guild.Id, new GuildChannelProperties("one-too-many", ChannelType.TextGuildChannel), cancellationToken: Ct));

        AssertError(exception, HttpStatusCode.BadRequest, 30013);
        Assert.Equal("Maximum number of guild channels reached (500)", exception.Error!.Message);
        Assert.Equal(FakeDiscordState.MaxChannelsPerGuild, discord.State.ListGuildChannels(StateCaller.Fixture, guild.Id).Value.Length);
    }

    [Fact]
    [Parity("POST /guilds/{guild_id}/channels")]
    [Parity("30052")]
    [Parity("50035")]
    public async Task CreateGuildChannelAsync_BoundsOfTheType_AreRefusedWithTheCatalogErrors()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        using var rest = discord.CreateRestClient();

        var bitrate = await Assert.ThrowsAsync<RestException>(() => rest.CreateGuildChannelAsync(guild.Id, new("v", ChannelType.VoiceGuildChannel) { Bitrate = 96001 }, cancellationToken: Ct));
        var stageBitrate = await Assert.ThrowsAsync<RestException>(() => rest.CreateGuildChannelAsync(guild.Id, new("s", ChannelType.StageGuildChannel) { Bitrate = 64001 }, cancellationToken: Ct));
        var voiceLimit = await Assert.ThrowsAsync<RestException>(() => rest.CreateGuildChannelAsync(guild.Id, new("v", ChannelType.VoiceGuildChannel) { UserLimit = 100 }, cancellationToken: Ct));
        var topic = await Assert.ThrowsAsync<RestException>(() => rest.CreateGuildChannelAsync(guild.Id, new("t", ChannelType.TextGuildChannel) { Topic = new string('t', 1025) }, cancellationToken: Ct));

        AssertError(bitrate, HttpStatusCode.BadRequest, 30052);
        AssertError(stageBitrate, HttpStatusCode.BadRequest, 30052);
        Assert.Equal("NUMBER_TYPE_MAX", Assert.Single(FieldErrors(voiceLimit, "user_limit")).Code);
        Assert.Equal("Must be between 0 and 1024 in length.", Assert.Single(FieldErrors(topic, "topic")).Message);
        Assert.Empty(await rest.GetGuildChannelsAsync(guild.Id, cancellationToken: Ct));

        // The forum topic goes up to 4096; a 5000-character one is refused by the structural validator (the spec's widest bound).
        await rest.CreateGuildChannelAsync(guild.Id, new("f", ChannelType.ForumGuildChannel) { Topic = new string('t', 4096) }, cancellationToken: Ct);
        var forumTopic = await Assert.ThrowsAsync<RestException>(() => rest.CreateGuildChannelAsync(guild.Id, new("f2", ChannelType.ForumGuildChannel) { Topic = new string('t', 4097) }, cancellationToken: Ct));
        Assert.Equal("BASE_TYPE_BAD_LENGTH", Assert.Single(FieldErrors(forumTopic, "topic")).Code);
    }

    [Fact]
    [Parity("POST /guilds/{guild_id}/channels")]
    [Parity("50035")]
    public async Task CreateGuildChannelAsync_InvalidNameAndType_Return50035()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        using var rest = discord.CreateRestClient();

        var empty = await Assert.ThrowsAsync<RestException>(() => rest.CreateGuildChannelAsync(guild.Id, new("", ChannelType.TextGuildChannel), cancellationToken: Ct));
        var tooLong = await Assert.ThrowsAsync<RestException>(() => rest.CreateGuildChannelAsync(guild.Id, new(new string('n', 101), ChannelType.TextGuildChannel), cancellationToken: Ct));
        var thread = await Assert.ThrowsAsync<RestException>(() => rest.CreateGuildChannelAsync(guild.Id, new("th", ChannelType.PublicGuildThread), cancellationToken: Ct));
        var directory = await Assert.ThrowsAsync<RestException>(() => rest.CreateGuildChannelAsync(guild.Id, new("dir", ChannelType.DirectoryGuildChannel), cancellationToken: Ct));

        Assert.Equal("BASE_TYPE_BAD_LENGTH", Assert.Single(FieldErrors(empty, "name")).Code);
        Assert.Equal("BASE_TYPE_BAD_LENGTH", Assert.Single(FieldErrors(tooLong, "name")).Code);
        Assert.Equal("BASE_TYPE_CHOICES", Assert.Single(FieldErrors(thread, "type")).Code);
        Assert.Equal("CHANNEL_TYPE_INVALID", Assert.Single(FieldErrors(directory, "type")).Code); // type 14 passes the spec's list, the fake does not model it
    }

    [Fact]
    [Parity("POST /guilds/{guild_id}/channels")]
    [Parity("50035")]
    public async Task SendRequest_PostWithoutNameOrWithWrongTypes_Returns50035()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        using var rest = discord.CreateRestClient();

        var missing = await Assert.ThrowsAsync<RestException>(
            () => rest.SendRequestAsync(HttpMethod.Post, Json("""{"type":0}"""), $"/guilds/{guild.Id}/channels", cancellationToken: Ct));
        var wrongTypes = await Assert.ThrowsAsync<RestException>(
            () => rest.SendRequestAsync(HttpMethod.Post, Json("""{"name":"x","nsfw":"yes","position":-1,"user_limit":"many"}"""), $"/guilds/{guild.Id}/channels", cancellationToken: Ct));

        Assert.Equal("BASE_TYPE_REQUIRED", Assert.Single(FieldErrors(missing, "name")).Code);
        Assert.Equal("BOOLEAN_TYPE_COERCE", Assert.Single(FieldErrors(wrongTypes, "nsfw")).Code);
        Assert.Equal("NUMBER_TYPE_MIN", Assert.Single(FieldErrors(wrongTypes, "position")).Code);
        Assert.Equal("NUMBER_TYPE_COERCE", Assert.Single(FieldErrors(wrongTypes, "user_limit")).Code);
        Assert.Empty(await rest.GetGuildChannelsAsync(guild.Id, cancellationToken: Ct));
    }

    [Fact]
    [Parity("POST /guilds/{guild_id}/channels")]
    [Parity("10004")]
    [Parity("50001")]
    public async Task CreateGuildChannelAsync_UnknownGuildAndGuildWithoutTheBot_Return10004And50001AndCreateNothing()
    {
        await using var discord = FakeDiscord.Create();
        var elsewhere = discord.Seed.Guild("Elsewhere", withBot: false);
        using var rest = discord.CreateRestClient();

        var unknown = await Assert.ThrowsAsync<RestException>(() => rest.CreateGuildChannelAsync(424242, new("x", ChannelType.TextGuildChannel), cancellationToken: Ct));
        var denied = await Assert.ThrowsAsync<RestException>(() => rest.CreateGuildChannelAsync(elsewhere.Id, new("x", ChannelType.TextGuildChannel), cancellationToken: Ct));

        AssertError(unknown, HttpStatusCode.NotFound, 10004);
        AssertError(denied, HttpStatusCode.Forbidden, 50001);
        Assert.Empty(discord.State.ListGuildChannels(StateCaller.Fixture, elsewhere.Id).Value);
    }

    [Fact]
    [Parity("POST /guilds/{guild_id}/channels")]
    public async Task CreateGuildChannelAsync_RefusedRequests_DoNotConsumeASnowflake()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        using var rest = discord.CreateRestClient();

        await Assert.ThrowsAsync<RestException>(() => rest.CreateGuildChannelAsync(guild.Id, new("x", ChannelType.TextGuildChannel) { ParentId = 424242 }, cancellationToken: Ct));
        await Assert.ThrowsAsync<RestException>(() => rest.CreateGuildChannelAsync(guild.Id, new("v", ChannelType.VoiceGuildChannel) { Bitrate = 96001 }, cancellationToken: Ct));
        var created = await rest.CreateGuildChannelAsync(guild.Id, new("ok", ChannelType.TextGuildChannel), cancellationToken: Ct);
        var fresh = FakeDiscord.Create();
        await using (fresh)
        {
            var other = fresh.Seed.Guild("Test");
            using var otherRest = fresh.CreateRestClient();
            var expected = await otherRest.CreateGuildChannelAsync(other.Id, new("ok", ChannelType.TextGuildChannel), cancellationToken: Ct);

            Assert.Equal(expected.Id, created.Id);
        }
    }
}
