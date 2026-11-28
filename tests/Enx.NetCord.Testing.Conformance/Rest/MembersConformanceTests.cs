using System.Net;
using Enx.NetCord.Testing.Conformance.Contract;
using NetCord;
using NetCord.Rest;
using Xunit;

namespace Enx.NetCord.Testing.Conformance.Rest;

/// <summary>
/// Guild member routes (M1-REST-07) driven through the real NetCord <see cref="RestClient"/>, with every response checked against the
/// OpenAPI contract in strict mode: the member object (<c>GuildMemberResponse</c>, <c>PrivateGuildMemberResponse</c>) and its list.
/// </summary>
[Trait("Category", "Conformance")]
public sealed class MembersConformanceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    [Parity("GET /guilds/{guild_id}/members/{user_id}")]
    public async Task GetGuildUserAsync_MemberWithProfile_ReturnsItAndConformsToGuildMemberResponse()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var alice = guild.Member("alice");
        var moderators = guild.Role("Moderators");
        using var rest = discord.CreateRestClient();
        await rest.ModifyGuildUserAsync(guild.Id, alice, o => { o.Nickname = "Ali"; o.RoleIds = [moderators]; }, cancellationToken: Ct);

        var member = await rest.GetGuildUserAsync(guild.Id, alice, cancellationToken: Ct);

        Assert.Equal("Ali", member.Nickname);
        Assert.Equal([moderators], member.RoleIds);
        ContractAssert.Conforms(discord, "GET /guilds/{guild_id}/members/{user_id}");
    }

    [Fact]
    [Parity("GET /guilds/{guild_id}/members/{user_id}")]
    [Parity("10007")]
    public async Task GetGuildUserAsync_UserThatIsNotAMember_Returns10007WithTheDiscordErrorEnvelope()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.GetGuildUserAsync(guild.Id, 424242, cancellationToken: Ct));

        Assert.Equal((HttpStatusCode.NotFound, 10007), (exception.StatusCode, exception.Error!.Code));
        ContractAssert.Conforms(discord, "GET /guilds/{guild_id}/members/{user_id}");
    }

    [Fact]
    [Parity("GET /guilds/{guild_id}/members")]
    public async Task GetGuildUsersAsync_SeveralMembers_ReturnsThemAndConformsToTheListOfGuildMemberResponse()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        guild.Member("alice");
        guild.Member("bob");
        using var rest = discord.CreateRestClient();

        var ids = new List<ulong>();
        await foreach (var member in rest.GetGuildUsersAsync(guild.Id).WithCancellation(Ct))
        {
            ids.Add(member.Id);
        }

        Assert.Equal(3, ids.Count);
        ContractAssert.Conforms(discord, "GET /guilds/{guild_id}/members");
    }

    [Fact]
    [Parity("GET /guilds/{guild_id}/members/search")]
    public async Task FindGuildUserAsync_Prefix_ReturnsTheMatchesAndConformsToTheListOfGuildMemberResponse()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var alice = guild.Member("alice");
        guild.Member("bob");
        using var rest = discord.CreateRestClient();

        var matches = await rest.FindGuildUserAsync(guild.Id, "al", 5, cancellationToken: Ct);

        Assert.Equal([alice], matches.Select(member => member.Id));
        ContractAssert.Conforms(discord, "GET /guilds/{guild_id}/members/search");
    }

    [Fact]
    [Parity("PATCH /guilds/{guild_id}/members/{user_id}")]
    public async Task ModifyGuildUserAsync_EveryModeledField_ReturnsTheMemberAndConformsToGuildMemberResponse()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var alice = guild.Member("alice");
        var moderators = guild.Role("Moderators");
        using var rest = discord.CreateRestClient();
        var until = discord.State.Clock.GetUtcNow().AddDays(3);

        var member = await rest.ModifyGuildUserAsync(
            guild.Id,
            alice,
            o =>
            {
                o.Nickname = "Ali";
                o.RoleIds = [moderators];
                o.Muted = true;
                o.Deafened = true;
                o.TimeOutUntil = until;
                o.GuildFlags = GuildUserFlags.BypassesVerification;
            },
            cancellationToken: Ct);

        Assert.Equal(until, member.TimeOutUntil);
        ContractAssert.Conforms(discord, "PATCH /guilds/{guild_id}/members/{user_id}");
    }

    [Fact]
    [Parity("PATCH /guilds/{guild_id}/members/{user_id}")]
    [Parity("10011")]
    public async Task ModifyGuildUserAsync_UnknownRole_Returns10011WithTheDiscordErrorEnvelope()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var alice = guild.Member("alice");
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(
            () => rest.ModifyGuildUserAsync(guild.Id, alice, o => o.RoleIds = [424242], cancellationToken: Ct));

        Assert.Equal((HttpStatusCode.NotFound, 10011), (exception.StatusCode, exception.Error!.Code));
        ContractAssert.Conforms(discord, "PATCH /guilds/{guild_id}/members/{user_id}");
    }

    [Fact]
    [Parity("PATCH /guilds/{guild_id}/members/@me")]
    public async Task ModifyCurrentGuildUserAsync_NicknameAndImages_ReturnsTheMemberAndConformsToPrivateGuildMemberResponse()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        using var rest = discord.CreateRestClient();

        var member = await rest.ModifyCurrentGuildUserAsync(
            guild.Id,
            o =>
            {
                o.Nickname = "Helper";
                o.Avatar = new ImageProperties(ImageFormat.Png, new byte[] { 1, 2, 3 });
                o.Banner = new ImageProperties(ImageFormat.Png, new byte[] { 4, 5, 6 });
                o.Bio = "I help.";
            },
            cancellationToken: Ct);

        Assert.Equal("Helper", member.Nickname);
        ContractAssert.Conforms(discord, "PATCH /guilds/{guild_id}/members/@me");
    }

    [Fact]
    [Parity("PUT /guilds/{guild_id}/members/{user_id}")]
    [Parity("50025")]
    public async Task AddGuildUserAsync_AnyAccessToken_Returns50025WithTheDiscordErrorEnvelope()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var stranger = discord.Seed.User("stranger");
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(
            () => rest.AddGuildUserAsync(guild.Id, stranger, new GuildUserProperties("token") { Nickname = "Newbie" }, cancellationToken: Ct));

        Assert.Equal((HttpStatusCode.Forbidden, 50025), (exception.StatusCode, exception.Error!.Code));
        ContractAssert.Conforms(discord, "PUT /guilds/{guild_id}/members/{user_id}");
    }

    [Fact]
    [Parity("DELETE /guilds/{guild_id}/members/{user_id}")]
    public async Task KickGuildUserAsync_Member_ReturnsNoContentAndConforms()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var alice = guild.Member("alice");
        using var rest = discord.CreateRestClient();

        await rest.KickGuildUserAsync(guild.Id, alice, cancellationToken: Ct);

        Assert.Null(discord.State.FindMember(guild.Id, alice));
        ContractAssert.Conforms(discord, "DELETE /guilds/{guild_id}/members/{user_id}");
    }

    [Fact]
    [Parity("PUT /guilds/{guild_id}/members/{user_id}/roles/{role_id}")]
    public async Task AddGuildUserRoleAsync_Role_ReturnsNoContentAndConforms()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var alice = guild.Member("alice");
        var moderators = guild.Role("Moderators");
        using var rest = discord.CreateRestClient();

        await rest.AddGuildUserRoleAsync(guild.Id, alice, moderators, cancellationToken: Ct);

        Assert.Equal([moderators], discord.State.FindMember(guild.Id, alice)!.RoleIds);
        ContractAssert.Conforms(discord, "PUT /guilds/{guild_id}/members/{user_id}/roles/{role_id}");
    }

    [Fact]
    [Parity("DELETE /guilds/{guild_id}/members/{user_id}/roles/{role_id}")]
    [Parity("50028")]
    public async Task RemoveGuildUserRoleAsync_RoleGivenThenTheEveryoneRole_ReturnsNoContentThen50028AndConforms()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var alice = guild.Member("alice");
        var moderators = guild.Role("Moderators");
        using var rest = discord.CreateRestClient();
        await rest.AddGuildUserRoleAsync(guild.Id, alice, moderators, cancellationToken: Ct);

        await rest.RemoveGuildUserRoleAsync(guild.Id, alice, moderators, cancellationToken: Ct);
        var exception = await Assert.ThrowsAsync<RestException>(() => rest.RemoveGuildUserRoleAsync(guild.Id, alice, guild.Id, cancellationToken: Ct));

        Assert.Equal((HttpStatusCode.BadRequest, 50028), (exception.StatusCode, exception.Error!.Code));
        Assert.Empty(discord.State.FindMember(guild.Id, alice)!.RoleIds);
        ContractAssert.Conforms(discord, "DELETE /guilds/{guild_id}/members/{user_id}/roles/{role_id}");
    }
}
