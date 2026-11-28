using System.Net;
using System.Text.Json;
using Enx.NetCord.Testing.State;
using NetCord;
using NetCord.Rest;
using Xunit;
using static Enx.NetCord.Testing.Tests.Rest.Channels.ChannelsTestSupport;
using static Enx.NetCord.Testing.Tests.Rest.Users.UsersTestSupport;
using static Enx.NetCord.Testing.Tests.State.StateTestSupport;

namespace Enx.NetCord.Testing.Tests.Rest.Channels;

/// <summary><c>PATCH /channels/{channel_id}</c>: the text &lt;-&gt; announcement conversion (ADR 0013, § 5) and the fields and bounds that depend on the type (§ 1).</summary>
public sealed class ModifyChannelTypeTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Task<JsonDocument> PatchAsync(RestClient rest, ulong channelId, string body)
        => ReadJsonAsync(rest.SendRequestAsync(HttpMethod.Patch, Json(body), $"/channels/{channelId}", cancellationToken: Ct), Ct);

    private static Task<JsonDocument> GetAsync(RestClient rest, ulong channelId)
        => ReadJsonAsync(rest.SendRequestAsync(HttpMethod.Get, $"/channels/{channelId}", cancellationToken: Ct), Ct);

    private static Task<RestException> RefusedAsync(RestClient rest, ulong channelId, string body)
        => Assert.ThrowsAsync<RestException>(
            () => rest.SendRequestAsync(HttpMethod.Patch, Json(body), $"/channels/{channelId}", cancellationToken: Ct));

    // ---- conversion ----

    [Fact]
    [Parity("PATCH /channels/{channel_id}")]
    public async Task ModifyGuildChannelAsync_TextToAnnouncement_KeepsTheChannelAndItsMessages()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var lobby = guild.Category("Lobby");
        var general = lobby.TextChannel("general", options: new() { Topic = "hi", Nsfw = true, Slowmode = 30, Position = 4 });
        var message = general.Message("hello", discord.Seed.User("alice"));
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        var converted = await rest.ModifyGuildChannelAsync(general.Id, o => o.ChannelType = ChannelType.AnnouncementGuildChannel, cancellationToken: Ct);

        var news = Assert.IsType<AnnouncementGuildChannel>(converted);
        Assert.Equal((general.Id, "general", lobby.Id, 4, "hi", true), (news.Id, news.Name, news.ParentId, news.Position, news.Topic, news.Nsfw));
        Assert.Equal(0, news.Slowmode); // an announcement channel has no slowmode (hypothesis, ADR 0013 lot C)
        Assert.IsType<AnnouncementGuildChannel>(await rest.GetChannelAsync(general.Id, cancellationToken: Ct));
        Assert.Equal("hello", (await rest.GetMessageAsync(general.Id, message, cancellationToken: Ct)).Content);
        var change = Assert.IsType<ChannelUpdated>(Assert.Single(Drain(subscription)));
        Assert.Equal((ChannelKind.GuildAnnouncement, 0), (change.Channel.Kind, change.Channel.RateLimitPerUser));
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}")]
    public async Task ModifyGuildChannelAsync_AnnouncementToText_KeepsTheChannel()
    {
        await using var discord = FakeDiscord.Create();
        var news = discord.Seed.Guild("Test").AnnouncementChannel("news", options: new() { Topic = "hi" });
        using var rest = discord.CreateRestClient();

        var converted = await rest.ModifyGuildChannelAsync(news.Id, o => o.ChannelType = ChannelType.TextGuildChannel, cancellationToken: Ct);

        var text = Assert.IsType<TextGuildChannel>(converted);
        Assert.Equal((news.Id, "news", "hi", 0), (text.Id, text.Name, text.Topic, text.Slowmode));
        Assert.IsType<TextGuildChannel>(await rest.GetChannelAsync(news.Id, cancellationToken: Ct));
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}")]
    public async Task ModifyGuildChannelAsync_ConversionWithOtherFields_PublishesOneChannelUpdated()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        await rest.ModifyGuildChannelAsync(
            general.Id,
            o =>
            {
                o.ChannelType = ChannelType.AnnouncementGuildChannel;
                o.Name = "news";
                o.Topic = "headlines";
            },
            cancellationToken: Ct);

        var change = Assert.IsType<ChannelUpdated>(Assert.Single(Drain(subscription)));
        Assert.Equal((ChannelKind.GuildAnnouncement, "news", "headlines"), (change.Channel.Kind, change.Channel.Name, change.Channel.Topic));
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}")]
    public async Task ModifyGuildChannelAsync_TextToAnnouncementWithSlowmode_AppliesTheNewTypeAndIgnoresTheSlowmode()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();

        var converted = await rest.ModifyGuildChannelAsync(
            general.Id, o => { o.ChannelType = ChannelType.AnnouncementGuildChannel; o.Slowmode = 30; }, cancellationToken: Ct);

        Assert.Equal(0, Assert.IsType<AnnouncementGuildChannel>(converted).Slowmode);
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}")]
    public async Task ModifyGuildChannelAsync_AnnouncementToTextWithSlowmode_AppliesTheSlowmodeToTheText()
    {
        await using var discord = FakeDiscord.Create();
        var news = discord.Seed.Guild("Test").AnnouncementChannel("news");
        using var rest = discord.CreateRestClient();

        var converted = await rest.ModifyGuildChannelAsync(
            news.Id, o => { o.ChannelType = ChannelType.TextGuildChannel; o.Slowmode = 30; }, cancellationToken: Ct);

        Assert.Equal(30, Assert.IsType<TextGuildChannel>(converted).Slowmode);
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}")]
    public async Task ModifyGuildChannelAsync_ConversionToTheCurrentType_ChangesNothing()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        var news = discord.Seed.Guild("Other").AnnouncementChannel("news");
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        await rest.ModifyGuildChannelAsync(general.Id, o => o.ChannelType = ChannelType.TextGuildChannel, cancellationToken: Ct);
        await rest.ModifyGuildChannelAsync(news.Id, o => o.ChannelType = ChannelType.AnnouncementGuildChannel, cancellationToken: Ct);

        Assert.Empty(Drain(subscription));
    }

    [Theory]
    [Parity("PATCH /channels/{channel_id}")]
    [Parity("50035")]
    [InlineData(0, 2)]
    [InlineData(0, 4)]
    [InlineData(0, 13)]
    [InlineData(0, 14)]
    [InlineData(0, 15)]
    [InlineData(0, 16)]
    [InlineData(5, 2)]
    [InlineData(5, 15)]
    [InlineData(2, 0)]
    [InlineData(2, 13)]
    [InlineData(13, 2)]
    [InlineData(4, 5)]
    [InlineData(15, 0)]
    [InlineData(15, 16)]
    [InlineData(16, 15)]
    [InlineData(2, 14)]
    public async Task SendRequest_PatchWithATypeThatIsNotATextAnnouncementConversion_Returns50035AndChangesNothing(int from, int to)
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var channel = discord.State.CreateGuildChannel(StateCaller.Fixture, new CreateGuildChannelInput(guild.Id, (ChannelKind)from, "subject")).Value;
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        var exception = await RefusedAsync(rest, channel.Id, $$"""{"type":{{to}},"name":"renamed"}""");

        var error = Assert.Single(FieldErrors(exception, "type"));
        Assert.Equal(("CHANNEL_TYPE_INVALID", "Invalid channel type."), (error.Code, error.Message));
        var stored = discord.State.FindChannel(channel.Id)!;
        Assert.Equal(((ChannelKind)from, "subject"), (stored.Kind, stored.Name));
        Assert.Empty(Drain(subscription));
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}")]
    [Parity("50035")]
    public async Task SendRequest_PatchWithATypeOutsideTheEnumeration_StillReturnsBaseTypeChoices()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();

        var exception = await RefusedAsync(rest, general.Id, """{"type":1}""");

        Assert.Equal("BASE_TYPE_CHOICES", Assert.Single(FieldErrors(exception, "type")).Code);
    }

    // ---- topic ----

    [Theory]
    [Parity("PATCH /channels/{channel_id}")]
    [InlineData("text", 1024)]
    [InlineData("announcement", 1024)]
    [InlineData("forum", 4096)]
    public async Task SendRequest_PatchWithATopicAtTheBoundOfTheType_IsAccepted(string family, int length)
    {
        await using var discord = FakeDiscord.Create();
        var channelId = SeedFamily(discord, family);
        using var rest = discord.CreateRestClient();

        using var json = await PatchAsync(rest, channelId, $$"""{"topic":"{{new string('t', length)}}"}""");

        Assert.Equal(length, json.RootElement.GetProperty("topic").GetString()!.Length);
    }

    [Theory]
    [Parity("PATCH /channels/{channel_id}")]
    [Parity("50035")]
    [InlineData("text", 1025, "Must be between 0 and 1024 in length.")]
    [InlineData("announcement", 1025, "Must be between 0 and 1024 in length.")]
    [InlineData("forum", 4097, "Must be between 0 and 4096 in length.")]
    public async Task SendRequest_PatchWithATopicAboveTheBoundOfTheType_Returns50035AndChangesNothing(string family, int length, string message)
    {
        await using var discord = FakeDiscord.Create();
        var channelId = SeedFamily(discord, family);
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        var exception = await RefusedAsync(rest, channelId, $$"""{"topic":"{{new string('t', length)}}","name":"renamed"}""");

        var error = Assert.Single(FieldErrors(exception, "topic"));
        Assert.Equal(("BASE_TYPE_BAD_LENGTH", message), (error.Code, error.Message));
        Assert.NotEqual("renamed", discord.State.FindChannel(channelId)!.Name);
        Assert.Empty(Drain(subscription));
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}")]
    public async Task SendRequest_PatchWithALongTopicOnAVoiceChannel_IsIgnored()
    {
        await using var discord = FakeDiscord.Create();
        var voice = discord.Seed.Guild("Test").VoiceChannel("voice");
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        using var json = await PatchAsync(rest, voice.Id, $$"""{"topic":"{{new string('t', 2000)}}"}""");

        Assert.False(json.RootElement.TryGetProperty("topic", out _));
        Assert.Empty(Drain(subscription));
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}")]
    public async Task SendRequest_PatchConvertingAnAnnouncementToTextWithALongTopic_IsRefusedByTheTypeBound()
    {
        await using var discord = FakeDiscord.Create();
        var news = discord.Seed.Guild("Test").AnnouncementChannel("news");
        using var rest = discord.CreateRestClient();

        var exception = await RefusedAsync(rest, news.Id, $$"""{"type":0,"topic":"{{new string('t', 1025)}}"}""");

        Assert.Equal("BASE_TYPE_BAD_LENGTH", Assert.Single(FieldErrors(exception, "topic")).Code);
        Assert.Equal(ChannelKind.GuildAnnouncement, discord.State.FindChannel(news.Id)!.Kind); // nothing was written, the conversion included
    }

    // ---- voice ----

    [Fact]
    [Parity("PATCH /channels/{channel_id}")]
    public async Task ModifyGuildChannelAsync_VoiceSettings_AreAppliedAndReadBack()
    {
        await using var discord = FakeDiscord.Create();
        var voice = discord.Seed.Guild("Test").VoiceChannel("voice");
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        var updated = await rest.ModifyGuildChannelAsync(
            voice.Id,
            o =>
            {
                o.Bitrate = 96000;
                o.UserLimit = 99;
                o.RtcRegion = "rotterdam";
                o.VideoQualityMode = VideoQualityMode.Full;
                o.Nsfw = true;
                o.Slowmode = 10;
            },
            cancellationToken: Ct);

        var read = Assert.IsType<VoiceGuildChannel>(await rest.GetChannelAsync(voice.Id, cancellationToken: Ct));
        Assert.Equal((96000, 99, "rotterdam", VideoQualityMode.Full, true, 10), (read.Bitrate, read.UserLimit, read.RtcRegion, read.VideoQualityMode, read.Nsfw, read.Slowmode));
        Assert.Equal(96000, Assert.IsType<VoiceGuildChannel>(updated).Bitrate);
        Assert.IsType<ChannelUpdated>(Assert.Single(Drain(subscription)));
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}")]
    public async Task SendRequest_PatchWithANullRtcRegion_ReturnsToAutomatic()
    {
        await using var discord = FakeDiscord.Create();
        var voice = discord.Seed.Guild("Test").VoiceChannel("voice");
        using var rest = discord.CreateRestClient();
        await rest.ModifyGuildChannelAsync(voice.Id, o => o.RtcRegion = "rotterdam", cancellationToken: Ct);

        using var json = await PatchAsync(rest, voice.Id, """{"rtc_region":null}""");

        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("rtc_region").ValueKind);
        Assert.Null(discord.State.FindChannel(voice.Id)!.Voice!.RtcRegion);
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}")]
    public async Task ModifyGuildChannelAsync_StageSettings_AcceptTheStageBounds()
    {
        await using var discord = FakeDiscord.Create();
        var stage = discord.Seed.Guild("Test").StageChannel("stage");
        using var rest = discord.CreateRestClient();

        var updated = await rest.ModifyGuildChannelAsync(stage.Id, o => { o.Bitrate = 64000; o.UserLimit = 10000; }, cancellationToken: Ct);

        var read = Assert.IsType<StageGuildChannel>(updated);
        Assert.Equal((64000, 10000), (read.Bitrate, read.UserLimit));
    }

    [Theory]
    [Parity("PATCH /channels/{channel_id}")]
    [Parity("30052")]
    [InlineData("voice", 96001)]
    [InlineData("stage", 64001)]
    public async Task ModifyGuildChannelAsync_BitrateAboveTheTypeMaximum_Returns30052AndChangesNothing(string family, int bitrate)
    {
        await using var discord = FakeDiscord.Create();
        var channelId = SeedFamily(discord, family);
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        var exception = await Assert.ThrowsAsync<RestException>(
            () => rest.ModifyGuildChannelAsync(channelId, o => { o.Bitrate = bitrate; o.Name = "renamed"; }, cancellationToken: Ct));

        AssertError(exception, HttpStatusCode.BadRequest, 30052);
        Assert.NotEqual("renamed", discord.State.FindChannel(channelId)!.Name);
        Assert.Empty(Drain(subscription));
    }

    [Theory]
    [Parity("PATCH /channels/{channel_id}")]
    [Parity("50035")]
    [InlineData("voice", 100, 99)]
    [InlineData("stage", 10001, 10000)]
    public async Task ModifyGuildChannelAsync_UserLimitAboveTheTypeMaximum_Returns50035AndChangesNothing(string family, int limit, int maximum)
    {
        await using var discord = FakeDiscord.Create();
        var channelId = SeedFamily(discord, family);
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        var exception = await Assert.ThrowsAsync<RestException>(
            () => rest.ModifyGuildChannelAsync(channelId, o => o.UserLimit = limit, cancellationToken: Ct));

        var error = Assert.Single(FieldErrors(exception, "user_limit"));
        Assert.Equal(("NUMBER_TYPE_MAX", $"int value should be less than or equal to {maximum}."), (error.Code, error.Message));
        Assert.Equal(0, discord.State.FindChannel(channelId)!.Voice!.UserLimit);
        Assert.Empty(Drain(subscription));
    }

    [Theory]
    [Parity("PATCH /channels/{channel_id}")]
    [Parity("50035")]
    [InlineData("""{"bitrate":7999}""", "bitrate", "NUMBER_TYPE_MIN", "int value should be greater than or equal to 8000.")]
    [InlineData("""{"user_limit":-1}""", "user_limit", "NUMBER_TYPE_MIN", "int value should be greater than or equal to 0.")]
    [InlineData("""{"bitrate":"abc"}""", "bitrate", "NUMBER_TYPE_COERCE", "Value \"abc\" is not int.")]
    [InlineData("""{"rtc_region":5}""", "rtc_region", "BASE_TYPE_BAD_TYPE", "Value must be of type string.")]
    [InlineData("""{"video_quality_mode":3}""", "video_quality_mode", "BASE_TYPE_CHOICES", "Value must be one of (1, 2).")]
    [InlineData("""{"default_auto_archive_duration":61}""", "default_auto_archive_duration", "BASE_TYPE_CHOICES", "Value must be one of (60, 1440, 4320, 10080).")]
    [InlineData("""{"default_thread_rate_limit_per_user":21601}""", "default_thread_rate_limit_per_user", "NUMBER_TYPE_MAX", "int value should be less than or equal to 21600.")]
    [InlineData("""{"default_sort_order":2}""", "default_sort_order", "BASE_TYPE_CHOICES", "Value must be one of (0, 1).")]
    [InlineData("""{"default_forum_layout":3}""", "default_forum_layout", "BASE_TYPE_CHOICES", "Value must be one of (0, 1, 2).")]
    public async Task SendRequest_PatchWithAnInvalidTypedField_Returns50035OnAnyChannelType(string body, string field, string code, string message)
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();

        var exception = await RefusedAsync(rest, general.Id, body);

        var error = Assert.Single(FieldErrors(exception, field));
        Assert.Equal((code, message), (error.Code, error.Message));
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}")]
    public async Task SendRequest_PatchWithVoiceFieldsOnATextChannel_IsAcceptedAndIgnored()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        // Even a value above any voice maximum: the text channel has no use for the field (UR-010).
        using var json = await PatchAsync(rest, general.Id, """{"bitrate":999999,"user_limit":500,"rtc_region":"x","video_quality_mode":2}""");

        Assert.False(json.RootElement.TryGetProperty("bitrate", out _));
        Assert.Null(discord.State.FindChannel(general.Id)!.Voice);
        Assert.Empty(Drain(subscription));
    }

    // ---- thread defaults ----

    [Theory]
    [Parity("PATCH /channels/{channel_id}")]
    [InlineData("text")]
    [InlineData("announcement")]
    [InlineData("forum")]
    public async Task ModifyGuildChannelAsync_ThreadDefaults_AreAppliedAndReadBack(string family)
    {
        await using var discord = FakeDiscord.Create();
        var channelId = SeedFamily(discord, family);
        using var rest = discord.CreateRestClient();

        await rest.ModifyGuildChannelAsync(
            channelId, o => { o.DefaultAutoArchiveDuration = ThreadArchiveDuration.OneDay; o.DefaultThreadSlowmode = 30; }, cancellationToken: Ct);

        using var json = await GetAsync(rest, channelId);
        Assert.Equal((1440, 30), (json.RootElement.GetProperty("default_auto_archive_duration").GetInt32(), json.RootElement.GetProperty("default_thread_rate_limit_per_user").GetInt32()));
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}")]
    public async Task SendRequest_PatchWithThreadDefaultsOnAVoiceChannel_IsIgnored()
    {
        await using var discord = FakeDiscord.Create();
        var voice = discord.Seed.Guild("Test").VoiceChannel("voice");
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        using var json = await PatchAsync(rest, voice.Id, """{"default_auto_archive_duration":1440,"default_thread_rate_limit_per_user":30}""");

        Assert.False(json.RootElement.TryGetProperty("default_auto_archive_duration", out _));
        Assert.Empty(Drain(subscription));
    }

    // ---- forum ----

    [Fact]
    [Parity("PATCH /channels/{channel_id}")]
    public async Task ModifyGuildChannelAsync_ForumSettings_AreAppliedAndReadBack()
    {
        await using var discord = FakeDiscord.Create();
        var forum = discord.Seed.Guild("Test").ForumChannel("ideas");
        using var rest = discord.CreateRestClient();

        await rest.ModifyGuildChannelAsync(
            forum.Id,
            o =>
            {
                o.DefaultSortOrder = SortOrderType.CreationDate;
                o.DefaultForumLayout = ForumLayoutType.GalleryView;
                o.Topic = "Post your ideas";
                o.Slowmode = 60;
            },
            cancellationToken: Ct);

        var read = Assert.IsType<ForumGuildChannel>(await rest.GetChannelAsync(forum.Id, cancellationToken: Ct));
        Assert.Equal((SortOrderType.CreationDate, ForumLayoutType.GalleryView, "Post your ideas", 60), (read.DefaultSortOrder, read.DefaultForumLayout, read.Topic, read.Slowmode));
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}")]
    public async Task SendRequest_PatchWithANullDefaultSortOrder_ClearsIt()
    {
        await using var discord = FakeDiscord.Create();
        var forum = discord.Seed.Guild("Test").ForumChannel("ideas");
        using var rest = discord.CreateRestClient();
        await rest.ModifyGuildChannelAsync(forum.Id, o => o.DefaultSortOrder = SortOrderType.CreationDate, cancellationToken: Ct);

        using var json = await PatchAsync(rest, forum.Id, """{"default_sort_order":null}""");

        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("default_sort_order").ValueKind);
        Assert.Null(discord.State.FindChannel(forum.Id)!.Forum!.DefaultSortOrder);
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}")]
    public async Task SendRequest_PatchWithAForumLayoutOnAMediaChannel_IsIgnored()
    {
        // default_forum_layout is for GUILD_FORUM only (channel.mdx); a media channel has no such field.
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var media = discord.State.CreateGuildChannel(StateCaller.Fixture, new CreateGuildChannelInput(guild.Id, ChannelKind.GuildMedia, "gallery")).Value;
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        using var json = await PatchAsync(rest, media.Id, """{"default_forum_layout":2}""");

        Assert.False(json.RootElement.TryGetProperty("default_forum_layout", out _));
        Assert.Equal(0, discord.State.FindChannel(media.Id)!.Forum!.DefaultForumLayout);
        Assert.Empty(Drain(subscription));
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}")]
    public async Task SendRequest_PatchWithTagsFlagsAndReactionEmoji_IsValidatedThenIgnored()
    {
        await using var discord = FakeDiscord.Create();
        var forum = discord.Seed.Guild("Test").ForumChannel("ideas");
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        using var json = await PatchAsync(
            rest,
            forum.Id,
            """
            {"available_tags":[{"name":"bug","emoji_name":"x","moderated":true},{"id":"5","name":"idea","emoji_id":"9"}],
             "default_reaction_emoji":{"emoji_name":"y"},"default_tag_setting":"match_all","flags":16}
            """);

        Assert.Equal(0, json.RootElement.GetProperty("available_tags").GetArrayLength()); // tags wait for M4-REST-01 (UR-007)
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("default_reaction_emoji").ValueKind);
        Assert.Empty(Drain(subscription));
    }

    [Theory]
    [Parity("PATCH /channels/{channel_id}")]
    [Parity("50035")]
    [InlineData("""{"available_tags":[{"emoji_name":"x"}]}""", "available_tags.0.name", "BASE_TYPE_REQUIRED")]
    [InlineData("""{"available_tags":[{"name":"%LONG%"}]}""", "available_tags.0.name", "BASE_TYPE_BAD_LENGTH")]
    [InlineData("""{"available_tags":[5]}""", "available_tags.0", "MODEL_TYPE_CONVERT")]
    [InlineData("""{"available_tags":"x"}""", "available_tags", "BASE_TYPE_BAD_TYPE")]
    [InlineData("""{"available_tags":[{"name":"a","emoji_id":"abc"}]}""", "available_tags.0.emoji_id", "NUMBER_TYPE_COERCE")]
    [InlineData("""{"default_reaction_emoji":{"emoji_id":"abc"}}""", "default_reaction_emoji.emoji_id", "NUMBER_TYPE_COERCE")]
    [InlineData("""{"default_reaction_emoji":5}""", "default_reaction_emoji", "MODEL_TYPE_CONVERT")]
    [InlineData("""{"default_tag_setting":"all"}""", "default_tag_setting", "BASE_TYPE_CHOICES")]
    [InlineData("""{"flags":"x"}""", "flags", "NUMBER_TYPE_COERCE")]
    public async Task SendRequest_PatchWithAnInvalidTagFlagOrEmoji_Returns50035(string body, string path, string code)
    {
        await using var discord = FakeDiscord.Create();
        var forum = discord.Seed.Guild("Test").ForumChannel("ideas");
        using var rest = discord.CreateRestClient();

        var exception = await RefusedAsync(rest, forum.Id, body.Replace("%LONG%", new string('n', 51), StringComparison.Ordinal));

        Assert.Equal(code, Assert.Single(FieldErrors(exception, path.Split('.'))).Code);
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}")]
    [Parity("50035")]
    public async Task SendRequest_PatchWithMoreThanTwentyTags_Returns50035MaxLength()
    {
        await using var discord = FakeDiscord.Create();
        var forum = discord.Seed.Guild("Test").ForumChannel("ideas");
        using var rest = discord.CreateRestClient();
        var tags = string.Join(",", Enumerable.Range(0, 21).Select(i => $$"""{"name":"t{{i}}"}"""));

        var exception = await RefusedAsync(rest, forum.Id, $$"""{"available_tags":[{{tags}}]}""");

        var error = Assert.Single(FieldErrors(exception, "available_tags"));
        Assert.Equal(("BASE_TYPE_MAX_LENGTH", "Must be 20 or fewer in length."), (error.Code, error.Message));
    }

    private static ulong SeedFamily(FakeDiscord discord, string family)
    {
        var guild = discord.Seed.Guild($"g-{family}");
        return family switch
        {
            "text" => guild.TextChannel("subject").Id,
            "announcement" => guild.AnnouncementChannel("subject").Id,
            "voice" => guild.VoiceChannel("subject").Id,
            "stage" => guild.StageChannel("subject").Id,
            "forum" => guild.ForumChannel("subject").Id,
            _ => throw new ArgumentOutOfRangeException(nameof(family), family, null),
        };
    }
}
