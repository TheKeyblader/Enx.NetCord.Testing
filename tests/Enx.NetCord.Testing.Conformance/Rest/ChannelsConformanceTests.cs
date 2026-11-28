using System.Net;
using System.Text.Json;
using Enx.NetCord.Testing.Conformance.Contract;
using NetCord;
using NetCord.Rest;
using Xunit;

namespace Enx.NetCord.Testing.Conformance.Rest;

/// <summary>
/// Channel routes driven through the real NetCord <see cref="RestClient"/>, with every response checked against the OpenAPI contract.
/// Guild channels are covered per seedable family on <c>GET</c> (category, text, announcement, voice, stage, forum), the other verbs on text channels and DMs.
/// </summary>
[Trait("Category", "Conformance")]
public sealed class ChannelsConformanceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    [Parity("GET /channels/{channel_id}")]
    public async Task GetChannelAsync_SeededTextChannel_ReturnsItAndConformsToGuildChannelResponse()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        using var rest = discord.CreateRestClient();

        var channel = await rest.GetChannelAsync(general.Id, cancellationToken: Ct);

        var text = Assert.IsType<TextGuildChannel>(channel);
        Assert.Equal(general.Id, text.Id);
        Assert.Equal(guild.Id, text.GuildId);
        Assert.Equal("general", text.Name);
        ContractAssert.Conforms(discord, "GET /channels/{channel_id}");
    }

    [Fact]
    [Parity("GET /channels/{channel_id}")]
    public async Task GetChannelAsync_EachSeedableGuildChannelFamily_ConformsToGuildChannelResponse()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var lobby = guild.Category("Lobby");
        var text = lobby.TextChannel("general", options: new SeededChannelOptions { Topic = "Hello", Slowmode = 5 });
        var news = lobby.AnnouncementChannel("news");
        var voice = lobby.VoiceChannel("voice");
        var stage = lobby.StageChannel("stage");
        var forum = lobby.ForumChannel("forum");
        using var rest = discord.CreateRestClient();

        Assert.IsType<CategoryGuildChannel>(await rest.GetChannelAsync(lobby.Id, cancellationToken: Ct));
        Assert.IsType<TextGuildChannel>(await rest.GetChannelAsync(text.Id, cancellationToken: Ct));
        Assert.IsType<AnnouncementGuildChannel>(await rest.GetChannelAsync(news.Id, cancellationToken: Ct));
        Assert.IsType<VoiceGuildChannel>(await rest.GetChannelAsync(voice.Id, cancellationToken: Ct));
        Assert.IsType<StageGuildChannel>(await rest.GetChannelAsync(stage.Id, cancellationToken: Ct));
        Assert.IsType<ForumGuildChannel>(await rest.GetChannelAsync(forum.Id, cancellationToken: Ct));

        // One journal entry per family: each response must match exactly one branch of the spec's oneOf.
        Assert.Equal(6, discord.Recorded.Requests.Count(r => r.Method == "GET" && r.RouteTemplate == "/channels/{channel_id}"));
        ContractAssert.Conforms(discord, "GET /channels/{channel_id}");
    }

    [Fact]
    [Parity("GET /channels/{channel_id}")]
    public async Task GetChannelAsync_DirectMessageChannel_ReturnsItAndConformsToPrivateChannelResponse()
    {
        await using var discord = FakeDiscord.Create();
        var aliceId = discord.Seed.User("alice");
        using var rest = discord.CreateRestClient();
        var dm = await rest.GetDMChannelAsync(aliceId, cancellationToken: Ct);

        var channel = await rest.GetChannelAsync(dm.Id, cancellationToken: Ct);

        Assert.IsType<DMChannel>(channel);
        Assert.Equal(dm.Id, channel.Id);
        ContractAssert.Conforms(discord, "GET /channels/{channel_id}", "POST /users/@me/channels");
    }

    [Fact]
    [Parity("GET /channels/{channel_id}")]
    [Parity("10003")]
    public async Task GetChannelAsync_UnknownChannel_Returns10003WithTheDiscordErrorEnvelope()
    {
        await using var discord = FakeDiscord.Create();
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.GetChannelAsync(123456789, cancellationToken: Ct));

        Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
        Assert.Equal(10003, exception.Error!.Code);
        ContractAssert.Conforms(discord, "GET /channels/{channel_id}");
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}")]
    public async Task ModifyGuildChannelAsync_NameAndTopic_ReturnsTheUpdatedChannelAndConformsToGuildChannelResponse()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();

        var channel = await rest.ModifyGuildChannelAsync(general.Id, o => { o.Name = "announcements"; o.Topic = "Read me"; }, cancellationToken: Ct);

        var text = Assert.IsType<TextGuildChannel>(channel);
        Assert.Equal("announcements", text.Name);
        Assert.Equal("Read me", text.Topic);
        ContractAssert.Conforms(discord, "PATCH /channels/{channel_id}");
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}")]
    public async Task ModifyGuildChannelAsync_MoveIntoACategory_ReturnsTheMovedChannelAndConformsToGuildChannelResponse()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var archive = guild.Category("Archive");
        var general = guild.TextChannel("general");
        using var rest = discord.CreateRestClient();

        var channel = await rest.ModifyGuildChannelAsync(general.Id, o => o.ParentId = archive.Id, cancellationToken: Ct);

        Assert.Equal(archive.Id, Assert.IsType<TextGuildChannel>(channel).ParentId);
        ContractAssert.Conforms(discord, "PATCH /channels/{channel_id}");
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}")]
    public async Task ModifyGuildChannelAsync_TextToAnnouncement_ReturnsTheConvertedChannelAndConformsToGuildChannelResponse()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general", options: new SeededChannelOptions { Slowmode = 30 });
        using var rest = discord.CreateRestClient();

        var channel = await rest.ModifyGuildChannelAsync(general.Id, o => o.ChannelType = ChannelType.AnnouncementGuildChannel, cancellationToken: Ct);

        Assert.IsType<AnnouncementGuildChannel>(channel);
        ContractAssert.Conforms(discord, "PATCH /channels/{channel_id}");
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}")]
    public async Task ModifyGuildChannelAsync_VoiceSettingsAndNullParent_ConformToGuildChannelResponse()
    {
        await using var discord = FakeDiscord.Create();
        var voice = discord.Seed.Guild("Test").Category("Lobby").VoiceChannel("voice");
        using var rest = discord.CreateRestClient();

        var channel = await rest.ModifyGuildChannelAsync(voice.Id, o => { o.Bitrate = 96000; o.UserLimit = 5; o.RtcRegion = "rotterdam"; }, cancellationToken: Ct);
        using var left = await rest.SendRequestAsync(
            HttpMethod.Patch, new StringContent("""{"parent_id":null}""", System.Text.Encoding.UTF8, "application/json"), $"/channels/{voice.Id}", cancellationToken: Ct);

        Assert.Equal((96000, 5), (Assert.IsType<VoiceGuildChannel>(channel).Bitrate, Assert.IsType<VoiceGuildChannel>(channel).UserLimit));
        ContractAssert.Conforms(discord, "PATCH /channels/{channel_id}");
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}")]
    [Parity("50035")]
    public async Task ModifyGuildChannelAsync_ParentThatIsNotACategory_Returns50035OnParentId()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var rules = guild.TextChannel("rules");
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(
            () => rest.ModifyGuildChannelAsync(general.Id, o => o.ParentId = rules.Id, cancellationToken: Ct));

        Assert.Equal((HttpStatusCode.BadRequest, 50035), (exception.StatusCode, exception.Error!.Code));
        ContractAssert.Conforms(discord, "PATCH /channels/{channel_id}");
        var fieldError = discord.Recorded.Requests[^1].ResponseBody!.Value.GetProperty("errors").GetProperty("parent_id").GetProperty("_errors")[0];
        Assert.Equal("CHANNEL_PARENT_INVALID_TYPE", fieldError.GetProperty("code").GetString());
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}")]
    [Parity("30052")]
    public async Task ModifyGuildChannelAsync_BitrateAboveTheMaximum_Returns30052WithTheDiscordErrorEnvelope()
    {
        await using var discord = FakeDiscord.Create();
        var voice = discord.Seed.Guild("Test").VoiceChannel("voice");
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(
            () => rest.ModifyGuildChannelAsync(voice.Id, o => o.Bitrate = 96001, cancellationToken: Ct));

        Assert.Equal((HttpStatusCode.BadRequest, 30052), (exception.StatusCode, exception.Error!.Code));
        ContractAssert.Conforms(discord, "PATCH /channels/{channel_id}");
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}")]
    [Parity("10003")]
    public async Task ModifyGuildChannelAsync_UnknownChannel_Returns10003WithTheDiscordErrorEnvelope()
    {
        await using var discord = FakeDiscord.Create();
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(
            () => rest.ModifyGuildChannelAsync(123456789, o => o.Name = "x", cancellationToken: Ct));

        Assert.Equal(10003, exception.Error!.Code);
        ContractAssert.Conforms(discord, "PATCH /channels/{channel_id}");
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}")]
    [Parity("50035")]
    public async Task ModifyGuildChannelAsync_InvalidName_Returns50035WithTheFieldErrorTree()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(
            () => rest.ModifyGuildChannelAsync(general.Id, o => o.Name = new string('x', 101), cancellationToken: Ct));

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Equal(50035, exception.Error!.Code);
        ContractAssert.Conforms(discord, "PATCH /channels/{channel_id}");

        // The contract only types the tree loosely: check the field error itself (string code under errors.name._errors).
        var body = discord.Recorded.Requests[^1].ResponseBody!.Value;
        var fieldError = body.GetProperty("errors").GetProperty("name").GetProperty("_errors")[0];
        Assert.Equal(JsonValueKind.String, fieldError.GetProperty("code").ValueKind);
        Assert.Equal("BASE_TYPE_BAD_LENGTH", fieldError.GetProperty("code").GetString());
    }

    [Fact]
    [Parity("DELETE /channels/{channel_id}")]
    public async Task DeleteChannelAsync_GuildChannel_ReturnsTheDeletedChannelAndConformsToGuildChannelResponse()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();

        var channel = await rest.DeleteChannelAsync(general.Id, cancellationToken: Ct);

        Assert.Equal(general.Id, channel.Id);
        ContractAssert.Conforms(discord, "DELETE /channels/{channel_id}");
    }

    [Fact]
    [Parity("DELETE /channels/{channel_id}")]
    public async Task DeleteChannelAsync_DirectMessageChannel_ReturnsTheClosedChannelAndConformsToPrivateChannelResponse()
    {
        await using var discord = FakeDiscord.Create();
        var aliceId = discord.Seed.User("alice");
        using var rest = discord.CreateRestClient();
        var dm = await rest.GetDMChannelAsync(aliceId, cancellationToken: Ct);

        var closed = await rest.DeleteChannelAsync(dm.Id, cancellationToken: Ct);

        Assert.IsType<DMChannel>(closed);
        ContractAssert.Conforms(discord, "DELETE /channels/{channel_id}");
    }

    [Fact]
    [Parity("DELETE /channels/{channel_id}")]
    [Parity("10003")]
    public async Task DeleteChannelAsync_UnknownChannel_Returns10003WithTheDiscordErrorEnvelope()
    {
        await using var discord = FakeDiscord.Create();
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.DeleteChannelAsync(123456789, cancellationToken: Ct));

        Assert.Equal(10003, exception.Error!.Code);
        ContractAssert.Conforms(discord, "DELETE /channels/{channel_id}");
    }
}
