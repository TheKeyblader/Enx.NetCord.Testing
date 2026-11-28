using System.Net;
using Enx.NetCord.Testing.State;
using NetCord.Rest;
using Xunit;
using static Enx.NetCord.Testing.Tests.Rest.Guilds.GuildsTestSupport;
using static Enx.NetCord.Testing.Tests.Rest.Users.UsersTestSupport;
using static Enx.NetCord.Testing.Tests.State.StateTestSupport;

namespace Enx.NetCord.Testing.Tests.Rest.Members;

/// <summary>
/// <c>PUT</c> and <c>DELETE /guilds/{guild_id}/members/{user_id}/roles/{role_id}</c> (M1-REST-07) through the real NetCord
/// <see cref="RestClient"/>.
/// </summary>
public sealed class MemberRolesTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    [Parity("PUT /guilds/{guild_id}/members/{user_id}/roles/{role_id}")]
    public async Task AddGuildUserRoleAsync_Role_IsGivenReadBackAndPublishedAsMemberUpdated()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var alice = guild.Member("alice");
        var moderators = guild.Role("Moderators");
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        await rest.AddGuildUserRoleAsync(guild.Id, alice, moderators, cancellationToken: Ct);

        Assert.Equal([moderators], (await rest.GetGuildUserAsync(guild.Id, alice, cancellationToken: Ct)).RoleIds);
        var change = Assert.IsType<MemberUpdated>(Assert.Single(Drain(subscription)));
        Assert.Equal([moderators], change.Member.RoleIds);
        Assert.Equal(HttpStatusCode.NoContent, discord.Recorded.Requests[0].StatusCode);
    }

    [Fact]
    [Parity("PUT /guilds/{guild_id}/members/{user_id}/roles/{role_id}")]
    public async Task AddGuildUserRoleAsync_RoleAlreadyGiven_SucceedsWithoutAChange()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var alice = guild.Member("alice");
        var moderators = guild.Role("Moderators");
        using var rest = discord.CreateRestClient();
        await rest.AddGuildUserRoleAsync(guild.Id, alice, moderators, cancellationToken: Ct);
        using var subscription = discord.State.SubscribeGateway();

        await rest.AddGuildUserRoleAsync(guild.Id, alice, moderators, cancellationToken: Ct);

        Assert.Empty(Drain(subscription));
        Assert.Equal([moderators], discord.State.FindMember(guild.Id, alice)!.RoleIds);
    }

    [Fact]
    [Parity("PUT /guilds/{guild_id}/members/{user_id}/roles/{role_id}")]
    public async Task AddGuildUserRoleAsync_SeveralRoles_KeepTheOrderTheyWereGiven()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var alice = guild.Member("alice");
        var first = guild.Role("First");
        var second = guild.Role("Second");
        using var rest = discord.CreateRestClient();

        await rest.AddGuildUserRoleAsync(guild.Id, alice, second, cancellationToken: Ct);
        await rest.AddGuildUserRoleAsync(guild.Id, alice, first, cancellationToken: Ct);

        Assert.Equal([second, first], discord.State.FindMember(guild.Id, alice)!.RoleIds);
    }

    [Fact]
    [Parity("PUT /guilds/{guild_id}/members/{user_id}/roles/{role_id}")]
    [Parity("50028")]
    public async Task AddGuildUserRoleAsync_TheEveryoneRole_Returns50028()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var alice = guild.Member("alice");
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.AddGuildUserRoleAsync(guild.Id, alice, guild.Id, cancellationToken: Ct));

        AssertError(exception, HttpStatusCode.BadRequest, 50028);
        Assert.Empty(discord.State.FindMember(guild.Id, alice)!.RoleIds);
    }

    [Fact]
    [Parity("PUT /guilds/{guild_id}/members/{user_id}/roles/{role_id}")]
    [Parity("10011")]
    [Parity("10007")]
    public async Task AddGuildUserRoleAsync_UnknownRoleOrUnknownMember_Returns10011Or10007()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var other = discord.Seed.Guild("Other");
        var alice = guild.Member("alice");
        var moderators = guild.Role("Moderators");
        var foreignRole = other.Role("Foreign");
        var stranger = discord.Seed.User("stranger");
        using var rest = discord.CreateRestClient();

        var unknownRole = await Assert.ThrowsAsync<RestException>(() => rest.AddGuildUserRoleAsync(guild.Id, alice, 424242, cancellationToken: Ct));
        var roleOfAnotherGuild = await Assert.ThrowsAsync<RestException>(() => rest.AddGuildUserRoleAsync(guild.Id, alice, foreignRole, cancellationToken: Ct));
        var unknownMember = await Assert.ThrowsAsync<RestException>(() => rest.AddGuildUserRoleAsync(guild.Id, stranger, moderators, cancellationToken: Ct));

        AssertError(unknownRole, HttpStatusCode.NotFound, 10011);
        AssertError(roleOfAnotherGuild, HttpStatusCode.NotFound, 10011);
        AssertError(unknownMember, HttpStatusCode.NotFound, 10007);
    }

    [Fact]
    [Parity("PUT /guilds/{guild_id}/members/{user_id}/roles/{role_id}")]
    [Parity("10004")]
    [Parity("50001")]
    public async Task AddGuildUserRoleAsync_UnknownGuildAndGuildWithoutTheBot_Return10004And50001()
    {
        await using var discord = FakeDiscord.Create();
        var elsewhere = discord.Seed.Guild("Elsewhere", withBot: false);
        var carol = elsewhere.Member("carol");
        var role = elsewhere.Role("Role");
        using var rest = discord.CreateRestClient();

        var unknown = await Assert.ThrowsAsync<RestException>(() => rest.AddGuildUserRoleAsync(424242, carol, role, cancellationToken: Ct));
        var denied = await Assert.ThrowsAsync<RestException>(() => rest.AddGuildUserRoleAsync(elsewhere.Id, carol, role, cancellationToken: Ct));

        AssertError(unknown, HttpStatusCode.NotFound, 10004);
        AssertError(denied, HttpStatusCode.Forbidden, 50001);
        Assert.Empty(discord.State.FindMember(elsewhere.Id, carol)!.RoleIds);
    }

    [Fact]
    [Parity("PUT /guilds/{guild_id}/members/{user_id}/roles/{role_id}")]
    [Parity("50035")]
    public async Task SendRequest_RoleRouteWithIdsThatAreNotSnowflakes_Returns50035ForEach()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(
            () => rest.SendRequestAsync(HttpMethod.Put, $"/guilds/{guild.Id}/members/{"abc"}/roles/{"def"}", cancellationToken: Ct));

        Assert.Single(FieldErrors(exception, "user_id"));
        Assert.Single(FieldErrors(exception, "role_id"));
    }

    [Fact]
    [Parity("DELETE /guilds/{guild_id}/members/{user_id}/roles/{role_id}")]
    public async Task RemoveGuildUserRoleAsync_RoleGiven_IsTakenAwayAndPublishedAsMemberUpdated()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var alice = guild.Member("alice");
        var moderators = guild.Role("Moderators");
        var vips = guild.Role("VIPs");
        using var rest = discord.CreateRestClient();
        await rest.AddGuildUserRoleAsync(guild.Id, alice, moderators, cancellationToken: Ct);
        await rest.AddGuildUserRoleAsync(guild.Id, alice, vips, cancellationToken: Ct);
        using var subscription = discord.State.SubscribeGateway();

        await rest.RemoveGuildUserRoleAsync(guild.Id, alice, moderators, cancellationToken: Ct);

        Assert.Equal([vips], (await rest.GetGuildUserAsync(guild.Id, alice, cancellationToken: Ct)).RoleIds);
        Assert.Equal([vips], Assert.IsType<MemberUpdated>(Assert.Single(Drain(subscription))).Member.RoleIds);
        Assert.Equal(HttpStatusCode.NoContent, discord.Recorded.Requests[2].StatusCode);
    }

    [Fact]
    [Parity("DELETE /guilds/{guild_id}/members/{user_id}/roles/{role_id}")]
    public async Task RemoveGuildUserRoleAsync_RoleNotGiven_SucceedsWithoutAChange()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var alice = guild.Member("alice");
        var moderators = guild.Role("Moderators");
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        await rest.RemoveGuildUserRoleAsync(guild.Id, alice, moderators, cancellationToken: Ct);

        Assert.Empty(Drain(subscription));
    }

    [Fact]
    [Parity("DELETE /guilds/{guild_id}/members/{user_id}/roles/{role_id}")]
    [Parity("50028")]
    [Parity("10011")]
    [Parity("10007")]
    public async Task RemoveGuildUserRoleAsync_EveryoneRoleUnknownRoleOrUnknownMember_Return50028And10011And10007()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var alice = guild.Member("alice");
        var moderators = guild.Role("Moderators");
        var stranger = discord.Seed.User("stranger");
        using var rest = discord.CreateRestClient();

        var everyone = await Assert.ThrowsAsync<RestException>(() => rest.RemoveGuildUserRoleAsync(guild.Id, alice, guild.Id, cancellationToken: Ct));
        var unknownRole = await Assert.ThrowsAsync<RestException>(() => rest.RemoveGuildUserRoleAsync(guild.Id, alice, 424242, cancellationToken: Ct));
        var unknownMember = await Assert.ThrowsAsync<RestException>(() => rest.RemoveGuildUserRoleAsync(guild.Id, stranger, moderators, cancellationToken: Ct));

        AssertError(everyone, HttpStatusCode.BadRequest, 50028);
        AssertError(unknownRole, HttpStatusCode.NotFound, 10011);
        AssertError(unknownMember, HttpStatusCode.NotFound, 10007);
    }

    [Fact]
    [Parity("DELETE /guilds/{guild_id}/members/{user_id}/roles/{role_id}")]
    [Parity("10004")]
    [Parity("50001")]
    public async Task RemoveGuildUserRoleAsync_UnknownGuildAndGuildWithoutTheBot_Return10004And50001()
    {
        await using var discord = FakeDiscord.Create();
        var elsewhere = discord.Seed.Guild("Elsewhere", withBot: false);
        var carol = elsewhere.Member("carol");
        var role = elsewhere.Role("Role");
        using var rest = discord.CreateRestClient();

        var unknown = await Assert.ThrowsAsync<RestException>(() => rest.RemoveGuildUserRoleAsync(424242, carol, role, cancellationToken: Ct));
        var denied = await Assert.ThrowsAsync<RestException>(() => rest.RemoveGuildUserRoleAsync(elsewhere.Id, carol, role, cancellationToken: Ct));

        AssertError(unknown, HttpStatusCode.NotFound, 10004);
        AssertError(denied, HttpStatusCode.Forbidden, 50001);
    }
}
