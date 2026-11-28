using System.Net;
using System.Text.Json;
using Enx.NetCord.Testing.State;
using NetCord;
using NetCord.Rest;
using Xunit;
using static Enx.NetCord.Testing.Tests.Rest.Guilds.GuildsTestSupport;
using static Enx.NetCord.Testing.Tests.Rest.Users.UsersTestSupport;

namespace Enx.NetCord.Testing.Tests.Rest.Guilds;

/// <summary><c>GET /guilds/{guild_id}</c> and <c>GET /guilds/{guild_id}/preview</c>, through the real NetCord <see cref="RestClient"/>.</summary>
public sealed class GetGuildTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    [Parity("GET /guilds/{guild_id}")]
    public async Task GetGuildAsync_SeededGuild_ReturnsItsFieldsAndRoles()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var moderators = guild.Role("Moderators");
        using var rest = discord.CreateRestClient();

        var read = await rest.GetGuildAsync(guild.Id, cancellationToken: Ct);

        Assert.Equal((guild.Id, "Test"), (read.Id, read.Name));
        Assert.Null(read.IconHash);
        Assert.Null(read.Description);
        Assert.Equal(discord.ApplicationId, read.OwnerId); // a guild without owner in the state belongs to the bot that reads it
        Assert.Equal((300, VerificationLevel.None, "en-US"), (read.AfkTimeout, read.VerificationLevel, read.PreferredLocale));
        Assert.Equal([guild.Id, moderators], read.Roles.Keys.Order());
        Assert.Equal("@everyone", read.EveryoneRole!.Name);
        Assert.Null(read.ApproximateUserCount); // not requested
        var attempt = Assert.Single(discord.Recorded.Requests);
        Assert.Equal(("GET", "/guilds/{guild_id}", HttpStatusCode.OK), (attempt.Method, attempt.RouteTemplate, attempt.StatusCode));
    }

    [Fact]
    [Parity("GET /guilds/{guild_id}")]
    public async Task GetGuildAsync_WithOwnerSeededByTheState_ReportsThatOwner()
    {
        await using var discord = FakeDiscord.Create();
        var alice = discord.Seed.User("alice");
        var guild = discord.State.CreateGuild(new CreateGuildInput("Owned", OwnerId: alice, WithBot: true)).Value.Guild;
        using var rest = discord.CreateRestClient();

        var read = await rest.GetGuildAsync(guild.Id, cancellationToken: Ct);

        Assert.Equal(alice, read.OwnerId);
    }

    [Fact]
    [Parity("GET /guilds/{guild_id}")]
    public async Task GetGuildAsync_WithCounts_ReturnsTheMemberCountAndNoPresence()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        guild.Member("alice");
        guild.Member("bob");
        using var rest = discord.CreateRestClient();

        var read = await rest.GetGuildAsync(guild.Id, withCounts: true, cancellationToken: Ct);
        var without = await rest.GetGuildAsync(guild.Id, withCounts: false, cancellationToken: Ct);

        Assert.Equal((3, 0), (read.ApproximateUserCount, read.ApproximatePresenceCount)); // the bot and two members
        Assert.Null(without.ApproximateUserCount);
        Assert.Null(without.ApproximatePresenceCount);
    }

    [Theory]
    [InlineData("1", true)]
    [InlineData("true", true)]
    [InlineData("0", false)]
    [InlineData("False", false)]
    [Parity("GET /guilds/{guild_id}")]
    public async Task SendRequest_WithCountsSpelledLikeDiscordAccepts_IsRead(string raw, bool expected)
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        using var rest = discord.CreateRestClient();

        using var json = await ReadJsonAsync(rest.SendRequestAsync(HttpMethod.Get, $"/guilds/{guild.Id}", $"?with_counts={raw}", cancellationToken: Ct), Ct);

        Assert.Equal(expected, json.RootElement.TryGetProperty("approximate_member_count", out _));
    }

    [Fact]
    [Parity("GET /guilds/{guild_id}")]
    [Parity("50035")]
    public async Task SendRequest_WithCountsThatIsNotABoolean_Returns50035()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(
            () => rest.SendRequestAsync(HttpMethod.Get, $"/guilds/{guild.Id}", "?with_counts=maybe", cancellationToken: Ct));

        var error = Assert.Single(FieldErrors(exception, "with_counts"));
        Assert.Equal(("BOOLEAN_TYPE_COERCE", "Value \"maybe\" is not boolean."), (error.Code, error.Message));
    }

    [Fact]
    [Parity("GET /guilds/{guild_id}")]
    [Parity("10004")]
    public async Task GetGuildAsync_UnknownGuild_Returns10004()
    {
        await using var discord = FakeDiscord.Create();
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.GetGuildAsync(424242, cancellationToken: Ct));

        AssertError(exception, HttpStatusCode.NotFound, 10004);
        Assert.Equal("Unknown Guild", exception.Error!.Message);
    }

    [Fact]
    [Parity("GET /guilds/{guild_id}")]
    [Parity("10004")]
    public async Task GetGuildAsync_GuildWithoutTheBot_IsHiddenAsUnknown10004ThenReadableOnceTheBotJoins()
    {
        await using var discord = FakeDiscord.Create();
        var elsewhere = discord.Seed.Guild("Elsewhere", withBot: false);
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.GetGuildAsync(elsewhere.Id, cancellationToken: Ct));
        elsewhere.Member(discord.ApplicationId);

        AssertError(exception, HttpStatusCode.NotFound, 10004);
        Assert.Equal("Elsewhere", (await rest.GetGuildAsync(elsewhere.Id, cancellationToken: Ct)).Name);
    }

    [Fact]
    [Parity("GET /guilds/{guild_id}")]
    [Parity("50035")]
    public async Task SendRequest_GuildIdThatIsNotASnowflake_Returns50035()
    {
        await using var discord = FakeDiscord.Create();
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(
            () => rest.SendRequestAsync(HttpMethod.Get, $"/guilds/{"abc"}", cancellationToken: Ct));

        Assert.Equal("NUMBER_TYPE_COERCE", Assert.Single(FieldErrors(exception, "guild_id")).Code);
    }

    [Fact]
    [Parity("GET /guilds/{guild_id}/preview")]
    public async Task GetGuildPreviewAsync_SeededGuild_ReturnsThePreviewWithCounts()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        guild.Member("alice");
        using var rest = discord.CreateRestClient();

        var preview = await rest.GetGuildPreviewAsync(guild.Id, cancellationToken: Ct);

        Assert.Equal((guild.Id, "Test"), (preview.Id, preview.Name));
        Assert.Equal((2, 0), (preview.ApproximateUserCount, preview.ApproximatePresenceCount));
        Assert.Empty(preview.Features);
        Assert.Empty(preview.Emojis);
        Assert.Empty(preview.Stickers);
    }

    [Fact]
    [Parity("GET /guilds/{guild_id}/preview")]
    [Parity("10004")]
    public async Task GetGuildPreviewAsync_UnknownGuildAndGuildWithoutTheBot_Return10004()
    {
        await using var discord = FakeDiscord.Create();
        var elsewhere = discord.Seed.Guild("Elsewhere", withBot: false);
        using var rest = discord.CreateRestClient();

        var unknown = await Assert.ThrowsAsync<RestException>(() => rest.GetGuildPreviewAsync(424242, cancellationToken: Ct));
        var hidden = await Assert.ThrowsAsync<RestException>(() => rest.GetGuildPreviewAsync(elsewhere.Id, cancellationToken: Ct));

        AssertError(unknown, HttpStatusCode.NotFound, 10004);
        AssertError(hidden, HttpStatusCode.NotFound, 10004);
    }

    [Fact]
    [Parity("GET /guilds/{guild_id}/preview")]
    public async Task GetGuildPreviewAsync_AfterTheGuildWasRenamed_ReflectsTheCurrentName()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        using var rest = discord.CreateRestClient();
        await rest.ModifyGuildAsync(guild.Id, o => o.Name = "Renamed", cancellationToken: Ct);

        var preview = await rest.GetGuildPreviewAsync(guild.Id, cancellationToken: Ct);

        Assert.Equal("Renamed", preview.Name);
    }

    [Fact]
    [Parity("GET /guilds/{guild_id}")]
    public async Task GetGuildAsync_Response_CarriesEveryRequiredPropertyOfGuildResponse()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        using var rest = discord.CreateRestClient();
        await rest.GetGuildAsync(guild.Id, cancellationToken: Ct);

        var body = discord.Recorded.Requests[^1].ResponseBody!.Value;

        Assert.Equal(Raw(guild.Id), body.GetProperty("id").GetString());
        Assert.Equal(Raw(discord.ApplicationId), body.GetProperty("owner_id").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("icon").ValueKind);
        Assert.Equal(JsonValueKind.Array, body.GetProperty("features").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("incidents_data").ValueKind);
        Assert.Equal(Raw(guild.Id), body.GetProperty("roles")[0].GetProperty("id").GetString());
        Assert.Equal("1071698660929", body.GetProperty("roles")[0].GetProperty("permissions").GetString());
        Assert.False(body.TryGetProperty("approximate_member_count", out _));
    }
}
