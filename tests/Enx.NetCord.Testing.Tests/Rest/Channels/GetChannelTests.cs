using System.Net;
using System.Text.Json;
using Enx.NetCord.Testing.State;
using NetCord;
using NetCord.Rest;
using Xunit;
using static Enx.NetCord.Testing.Tests.Rest.Channels.ChannelsTestSupport;
using static Enx.NetCord.Testing.Tests.Rest.Users.UsersTestSupport;

namespace Enx.NetCord.Testing.Tests.Rest.Channels;

public sealed class GetChannelTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    [Parity("GET /channels/{channel_id}")]
    public async Task GetChannelAsync_SeededTextChannel_ReturnsItsProperties()
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
        Assert.Null(text.Topic);
        Assert.False(text.Nsfw);
        Assert.Equal(0, text.Slowmode);
        Assert.Equal(0, text.Position);
        Assert.Null(text.ParentId);
        Assert.Null(text.LastMessageId);
        var attempt = Assert.Single(discord.Journal.Snapshot());
        Assert.Equal("/channels/{channel_id}", attempt.RouteTemplate);
    }

    [Fact]
    [Parity("GET /channels/{channel_id}")]
    public async Task GetChannelAsync_ChannelWithMessages_ReportsTheLastMessageId()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        var author = discord.Seed.User("alice");
        general.Message("first", author);
        var last = general.Message("second", author);
        using var rest = discord.CreateRestClient();

        var channel = await rest.GetChannelAsync(general.Id, cancellationToken: Ct);

        Assert.Equal(last, Assert.IsType<TextGuildChannel>(channel).LastMessageId);
    }

    [Fact]
    [Parity("GET /channels/{channel_id}")]
    public async Task GetChannelAsync_Response_HasTheShapeOfGuildChannelResponse()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        using var rest = discord.CreateRestClient();

        using var json = await ReadJsonAsync(rest.SendRequestAsync(HttpMethod.Get, $"/channels/{general.Id}", cancellationToken: Ct), Ct);

        var root = json.RootElement;
        Assert.Equal(Raw(general.Id), root.GetProperty("id").GetString());
        Assert.Equal(0, root.GetProperty("type").GetInt32());
        Assert.Equal(Raw(guild.Id), root.GetProperty("guild_id").GetString());
        Assert.Equal("general", root.GetProperty("name").GetString());
        Assert.Equal(0, root.GetProperty("flags").GetInt32());
        Assert.Equal(0, root.GetProperty("position").GetInt32());
        Assert.Equal(0, root.GetProperty("rate_limit_per_user").GetInt32());
        Assert.False(root.GetProperty("nsfw").GetBoolean());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("topic").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("parent_id").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("last_message_id").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("last_pin_timestamp").ValueKind);
        Assert.Empty(root.GetProperty("permission_overwrites").EnumerateArray());
    }

    [Fact]
    [Parity("GET /channels/{channel_id}")]
    public async Task GetChannelAsync_DmChannel_ReturnsItWithItsRecipient()
    {
        await using var discord = FakeDiscord.Create();
        var aliceId = discord.Seed.User("alice");
        using var rest = discord.CreateRestClient();
        var dm = await rest.GetDMChannelAsync(aliceId, cancellationToken: Ct);

        var channel = await rest.GetChannelAsync(dm.Id, cancellationToken: Ct);

        var read = Assert.IsType<DMChannel>(channel);
        Assert.Equal(dm.Id, read.Id);
        Assert.Equal(aliceId, Assert.Single(read.Users.Values).Id);
    }

    [Fact]
    [Parity("GET /channels/{channel_id}")]
    [Parity("10003")]
    [Parity("http:404")]
    public async Task GetChannelAsync_UnknownChannel_Returns404UnknownChannel()
    {
        await using var discord = FakeDiscord.Create();
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.GetChannelAsync(123456789, cancellationToken: Ct));

        AssertError(exception, HttpStatusCode.NotFound, 10003);
        Assert.Equal("Unknown Channel", exception.Error!.Message);
    }

    [Fact]
    [Parity("GET /channels/{channel_id}")]
    [Parity("50001")]
    [Parity("http:403")]
    public async Task GetChannelAsync_ChannelOfAGuildWithoutTheBot_Returns403MissingAccess()
    {
        await using var discord = FakeDiscord.Create();
        var hidden = discord.Seed.Guild("Elsewhere", withBot: false).TextChannel("secret");
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.GetChannelAsync(hidden.Id, cancellationToken: Ct));

        AssertError(exception, HttpStatusCode.Forbidden, 50001);
        Assert.Equal("Missing Access", exception.Error!.Message);
    }

    [Fact]
    [Parity("GET /channels/{channel_id}")]
    [Parity("50001")]
    public async Task GetChannelAsync_BotJoinsTheGuildLater_BecomesReadable()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Elsewhere", withBot: false);
        var channel = guild.TextChannel("secret");
        using var rest = discord.CreateRestClient();
        await Assert.ThrowsAsync<RestException>(() => rest.GetChannelAsync(channel.Id, cancellationToken: Ct));

        guild.Member(discord.ApplicationId);

        Assert.Equal(channel.Id, (await rest.GetChannelAsync(channel.Id, cancellationToken: Ct)).Id);
    }

    [Fact]
    [Parity("GET /channels/{channel_id}")]
    [Parity("50035")]
    public async Task SendRequest_GetChannelWithANonSnowflakeId_Returns50035OnChannelId()
    {
        await using var discord = FakeDiscord.Create();
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(
            () => rest.SendRequestAsync(HttpMethod.Get, $"/channels/abc", cancellationToken: Ct));

        var error = Assert.Single(FieldErrors(exception, "channel_id"));
        Assert.Equal("NUMBER_TYPE_COERCE", error.Code);
        Assert.Equal("Value \"abc\" is not snowflake.", error.Message);
    }

    [Fact]
    [Parity("GET /channels/{channel_id}")]
    [Parity("PATCH /channels/{channel_id}")]
    [Parity("DELETE /channels/{channel_id}")]
    [Parity("http:401")]
    public async Task ChannelRoutes_WithoutCredentialsOrWithAnUnknownToken_Return401AndChangeNothing()
    {
        using var harness = CreateRealRoutesHarness();
        var guild = harness.Context.State.CreateGuild(new CreateGuildInput("Test", WithBot: true)).Value.Guild;
        var channel = harness.Context.State.CreateTextChannel(StateCaller.Fixture, new CreateTextChannelInput(guild.Id, "general")).Value;
        using var anonymous = harness.CreateClient(rawToken: null);
        using var stranger = harness.CreateClient("NDM.other.token");

        foreach (var client in new[] { anonymous, stranger })
        {
            var attempts = new Func<Task>[]
            {
                () => client.GetChannelAsync(channel.Id, cancellationToken: Ct),
                () => client.ModifyGuildChannelAsync(channel.Id, o => o.Name = "renamed", cancellationToken: Ct),
                () => client.DeleteChannelAsync(channel.Id, cancellationToken: Ct),
            };
            foreach (var attempt in attempts)
            {
                var exception = await Assert.ThrowsAsync<RestException>(attempt);
                AssertError(exception, HttpStatusCode.Unauthorized, 0);
            }
        }

        Assert.Equal("general", harness.Context.State.FindChannel(channel.Id)!.Name);
    }
}
