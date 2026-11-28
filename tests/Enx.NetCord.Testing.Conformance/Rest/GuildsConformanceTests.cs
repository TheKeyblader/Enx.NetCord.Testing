using System.Net;
using Enx.NetCord.Testing.Conformance.Contract;
using NetCord;
using NetCord.Rest;
using Xunit;

namespace Enx.NetCord.Testing.Conformance.Rest;

/// <summary>
/// Guild routes (M1-REST-06) driven through the real NetCord <see cref="RestClient"/>, with every response checked against the OpenAPI
/// contract in strict mode: the guild objects, the preview, and the mixed list of channels (one branch of the spec's oneOf per element).
/// </summary>
[Trait("Category", "Conformance")]
public sealed class GuildsConformanceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    [Parity("GET /guilds/{guild_id}")]
    public async Task GetGuildAsync_SeededGuildWithARole_ReturnsItAndConformsToGuildWithCountsResponse()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var moderators = guild.Role("Moderators");
        using var rest = discord.CreateRestClient();

        var read = await rest.GetGuildAsync(guild.Id, cancellationToken: Ct);

        Assert.Equal((guild.Id, "Test"), (read.Id, read.Name));
        Assert.Contains(moderators, read.Roles.Keys);
        ContractAssert.Conforms(discord, "GET /guilds/{guild_id}");
    }

    [Fact]
    [Parity("GET /guilds/{guild_id}")]
    public async Task GetGuildAsync_WithCounts_ConformsToGuildWithCountsResponse()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        guild.Member("alice");
        using var rest = discord.CreateRestClient();

        var read = await rest.GetGuildAsync(guild.Id, withCounts: true, cancellationToken: Ct);

        Assert.Equal(2, read.ApproximateUserCount);
        ContractAssert.Conforms(discord, "GET /guilds/{guild_id}");
    }

    [Fact]
    [Parity("GET /guilds/{guild_id}")]
    [Parity("10004")]
    public async Task GetGuildAsync_GuildWithoutTheBot_Returns10004WithTheDiscordErrorEnvelope()
    {
        await using var discord = FakeDiscord.Create();
        var elsewhere = discord.Seed.Guild("Elsewhere", withBot: false);
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.GetGuildAsync(elsewhere.Id, cancellationToken: Ct));

        Assert.Equal((HttpStatusCode.NotFound, 10004), (exception.StatusCode, exception.Error!.Code));
        ContractAssert.Conforms(discord, "GET /guilds/{guild_id}");
    }

    [Fact]
    [Parity("PATCH /guilds/{guild_id}")]
    public async Task ModifyGuildAsync_SettingsAndImages_ReturnTheGuildAndConformToGuildResponse()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var afk = guild.VoiceChannel("afk");
        var system = guild.TextChannel("system");
        using var rest = discord.CreateRestClient();

        var updated = await rest.ModifyGuildAsync(
            guild.Id,
            o =>
            {
                o.Name = "Renamed";
                o.Description = "About us";
                o.Icon = new ImageProperties(ImageFormat.Png, new byte[] { 1, 2, 3 });
                o.VerificationLevel = VerificationLevel.Medium;
                o.AfkTimeout = 900;
                o.AfkChannelId = afk.Id;
                o.SystemChannelId = system.Id;
                o.PreferredLocale = "fr";
            },
            cancellationToken: Ct);

        Assert.Equal(("Renamed", "About us", afk.Id), (updated.Name, updated.Description, updated.AfkChannelId));
        ContractAssert.Conforms(discord, "PATCH /guilds/{guild_id}");
    }

    [Fact]
    [Parity("PATCH /guilds/{guild_id}")]
    [Parity("50035")]
    public async Task ModifyGuildAsync_NameTooShort_Returns50035WithTheFieldErrorTree()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.ModifyGuildAsync(guild.Id, o => o.Name = "x", cancellationToken: Ct));

        Assert.Equal((HttpStatusCode.BadRequest, 50035), (exception.StatusCode, exception.Error!.Code));
        ContractAssert.Conforms(discord, "PATCH /guilds/{guild_id}");
        var fieldError = discord.Recorded.Requests[^1].ResponseBody!.Value.GetProperty("errors").GetProperty("name").GetProperty("_errors")[0];
        Assert.Equal("BASE_TYPE_BAD_LENGTH", fieldError.GetProperty("code").GetString());
    }

    [Fact]
    [Parity("GET /guilds/{guild_id}/preview")]
    public async Task GetGuildPreviewAsync_SeededGuild_ReturnsItAndConformsToGuildPreviewResponse()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        guild.Member("alice");
        using var rest = discord.CreateRestClient();

        var preview = await rest.GetGuildPreviewAsync(guild.Id, cancellationToken: Ct);

        Assert.Equal((guild.Id, 2), (preview.Id, preview.ApproximateUserCount));
        ContractAssert.Conforms(discord, "GET /guilds/{guild_id}/preview");
    }

    [Fact]
    [Parity("GET /guilds/{guild_id}/preview")]
    [Parity("10004")]
    public async Task GetGuildPreviewAsync_UnknownGuild_Returns10004WithTheDiscordErrorEnvelope()
    {
        await using var discord = FakeDiscord.Create();
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.GetGuildPreviewAsync(123456789, cancellationToken: Ct));

        Assert.Equal(10004, exception.Error!.Code);
        ContractAssert.Conforms(discord, "GET /guilds/{guild_id}/preview");
    }

    [Fact]
    [Parity("GET /guilds/{guild_id}/channels")]
    public async Task GetGuildChannelsAsync_EachSeedableFamily_ConformsToTheOneOfBranchOfEachElement()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var lobby = guild.Category("Lobby");
        lobby.TextChannel("general", options: new SeededChannelOptions { Topic = "Hello", Slowmode = 5 });
        lobby.AnnouncementChannel("news");
        lobby.VoiceChannel("voice");
        guild.StageChannel("stage");
        guild.ForumChannel("forum");
        using var rest = discord.CreateRestClient();

        var channels = await rest.GetGuildChannelsAsync(guild.Id, cancellationToken: Ct);

        Assert.Equal(6, channels.Count);
        Assert.IsType<CategoryGuildChannel>(channels[0]);
        Assert.IsType<ForumGuildChannel>(channels[5]);
        ContractAssert.Conforms(discord, "GET /guilds/{guild_id}/channels");
    }

    [Fact]
    [Parity("GET /guilds/{guild_id}/channels")]
    [Parity("50001")]
    public async Task GetGuildChannelsAsync_GuildWithoutTheBot_Returns50001WithTheDiscordErrorEnvelope()
    {
        await using var discord = FakeDiscord.Create();
        var elsewhere = discord.Seed.Guild("Elsewhere", withBot: false);
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.GetGuildChannelsAsync(elsewhere.Id, cancellationToken: Ct));

        Assert.Equal((HttpStatusCode.Forbidden, 50001), (exception.StatusCode, exception.Error!.Code));
        ContractAssert.Conforms(discord, "GET /guilds/{guild_id}/channels");
    }

    [Fact]
    [Parity("POST /guilds/{guild_id}/channels")]
    public async Task CreateGuildChannelAsync_EachFamily_ReturnsTheChannelAndConformsToGuildChannelResponse()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var lobby = guild.Category("Lobby");
        using var rest = discord.CreateRestClient();

        var text = await rest.CreateGuildChannelAsync(guild.Id, new GuildChannelProperties("general", ChannelType.TextGuildChannel) { Topic = "Hi", ParentId = lobby.Id }, cancellationToken: Ct);
        var voice = await rest.CreateGuildChannelAsync(guild.Id, new GuildChannelProperties("voice", ChannelType.VoiceGuildChannel) { Bitrate = 96000, UserLimit = 5 }, cancellationToken: Ct);
        var category = await rest.CreateGuildChannelAsync(guild.Id, new GuildChannelProperties("Archive", ChannelType.CategoryChannel), cancellationToken: Ct);
        var news = await rest.CreateGuildChannelAsync(guild.Id, new GuildChannelProperties("news", ChannelType.AnnouncementGuildChannel), cancellationToken: Ct);
        var stage = await rest.CreateGuildChannelAsync(guild.Id, new GuildChannelProperties("stage", ChannelType.StageGuildChannel), cancellationToken: Ct);
        var forum = await rest.CreateGuildChannelAsync(guild.Id, new GuildChannelProperties("help", ChannelType.ForumGuildChannel) { DefaultThreadSlowmode = 30 }, cancellationToken: Ct);

        // Type 16 (media) is not exercised here: the pinned spec's enumeration of channel types lacks it (ADR 0013, § 1), so its response
        // cannot conform; it is covered by CreateGuildChannelAsync_EachChannelType_IsCreatedWithTheTypeOfTheRequest.
        Assert.IsType<TextGuildChannel>(text);
        Assert.IsType<VoiceGuildChannel>(voice);
        Assert.IsType<CategoryGuildChannel>(category);
        Assert.IsType<AnnouncementGuildChannel>(news);
        Assert.IsType<StageGuildChannel>(stage);
        Assert.IsType<ForumGuildChannel>(forum);
        ContractAssert.Conforms(discord, "POST /guilds/{guild_id}/channels");
    }

    [Fact]
    [Parity("POST /guilds/{guild_id}/channels")]
    [Parity("30013")]
    public async Task CreateGuildChannelAsync_GuildWith500Channels_Returns30013WithTheDiscordErrorEnvelope()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var categories = Enumerable.Range(0, 10).Select(i => guild.Category($"cat{i}")).ToList();
        for (var i = 0; i < 490; i++)
        {
            categories[i / 50].TextChannel($"c{i}");
        }

        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(
            () => rest.CreateGuildChannelAsync(guild.Id, new GuildChannelProperties("one-too-many", ChannelType.TextGuildChannel), cancellationToken: Ct));

        Assert.Equal((HttpStatusCode.BadRequest, 30013), (exception.StatusCode, exception.Error!.Code));
        ContractAssert.Conforms(discord, "POST /guilds/{guild_id}/channels");
    }

    [Fact]
    [Parity("POST /guilds/{guild_id}/channels")]
    [Parity("50035")]
    public async Task CreateGuildChannelAsync_ParentThatIsNotACategory_Returns50035OnParentId()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var rules = guild.TextChannel("rules");
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(
            () => rest.CreateGuildChannelAsync(guild.Id, new GuildChannelProperties("x", ChannelType.TextGuildChannel) { ParentId = rules.Id }, cancellationToken: Ct));

        Assert.Equal((HttpStatusCode.BadRequest, 50035), (exception.StatusCode, exception.Error!.Code));
        ContractAssert.Conforms(discord, "POST /guilds/{guild_id}/channels");
        var fieldError = discord.Recorded.Requests[^1].ResponseBody!.Value.GetProperty("errors").GetProperty("parent_id").GetProperty("_errors")[0];
        Assert.Equal("CHANNEL_PARENT_INVALID_TYPE", fieldError.GetProperty("code").GetString());
    }

    [Fact]
    [Parity("PATCH /guilds/{guild_id}/channels")]
    public async Task ModifyGuildChannelPositionsAsync_PositionsAndOneParent_Return204AndConform()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var archive = guild.Category("Archive");
        var a = guild.TextChannel("a");
        var b = guild.TextChannel("b");
        using var rest = discord.CreateRestClient();

        await rest.ModifyGuildChannelPositionsAsync(
            guild.Id,
            [new GuildChannelPositionProperties(a.Id) { Position = 4, ParentId = archive.Id, LockPermissions = true }, new GuildChannelPositionProperties(b.Id) { Position = 1 }],
            cancellationToken: Ct);

        Assert.Equal(HttpStatusCode.NoContent, discord.Recorded.Requests[^1].StatusCode);
        ContractAssert.Conforms(discord, "PATCH /guilds/{guild_id}/channels");
    }

    [Fact]
    [Parity("PATCH /guilds/{guild_id}/channels")]
    [Parity("40009")]
    public async Task ModifyGuildChannelPositionsAsync_TwoParentChanges_Returns40009WithTheDiscordErrorEnvelope()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var archive = guild.Category("Archive");
        var a = guild.TextChannel("a");
        var b = guild.TextChannel("b");
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(
            () => rest.ModifyGuildChannelPositionsAsync(
                guild.Id,
                [new GuildChannelPositionProperties(a.Id) { ParentId = archive.Id }, new GuildChannelPositionProperties(b.Id) { ParentId = archive.Id }],
                cancellationToken: Ct));

        Assert.Equal((HttpStatusCode.BadRequest, 40009), (exception.StatusCode, exception.Error!.Code));
        ContractAssert.Conforms(discord, "PATCH /guilds/{guild_id}/channels");
    }

    [Fact]
    [Parity("PATCH /guilds/{guild_id}/channels")]
    [Parity("10003")]
    public async Task ModifyGuildChannelPositionsAsync_UnknownChannel_Returns10003WithTheDiscordErrorEnvelope()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(
            () => rest.ModifyGuildChannelPositionsAsync(guild.Id, [new GuildChannelPositionProperties(123456789) { Position = 1 }], cancellationToken: Ct));

        Assert.Equal((HttpStatusCode.NotFound, 10003), (exception.StatusCode, exception.Error!.Code));
        ContractAssert.Conforms(discord, "PATCH /guilds/{guild_id}/channels");
    }
}
