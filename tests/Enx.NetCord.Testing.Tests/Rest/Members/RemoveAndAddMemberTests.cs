using System.Net;
using Enx.NetCord.Testing.State;
using NetCord.Rest;
using Xunit;
using static Enx.NetCord.Testing.Tests.Rest.Guilds.GuildsTestSupport;
using static Enx.NetCord.Testing.Tests.Rest.Users.UsersTestSupport;
using static Enx.NetCord.Testing.Tests.State.StateTestSupport;

namespace Enx.NetCord.Testing.Tests.Rest.Members;

/// <summary>
/// <c>DELETE</c> and <c>PUT /guilds/{guild_id}/members/{user_id}</c> (M1-REST-07) through the real NetCord <see cref="RestClient"/>.
/// </summary>
public sealed class RemoveAndAddMemberTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    [Parity("DELETE /guilds/{guild_id}/members/{user_id}")]
    public async Task KickGuildUserAsync_Member_IsRemovedAndPublishedAsMemberRemoved()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var alice = guild.Member("alice");
        var bob = guild.Member("bob");
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        await rest.KickGuildUserAsync(guild.Id, alice, cancellationToken: Ct);

        var gone = await Assert.ThrowsAsync<RestException>(() => rest.GetGuildUserAsync(guild.Id, alice, cancellationToken: Ct));
        AssertError(gone, HttpStatusCode.NotFound, 10007);
        var remaining = new List<ulong>();
        await foreach (var member in rest.GetGuildUsersAsync(guild.Id).WithCancellation(Ct))
        {
            remaining.Add(member.Id);
        }

        Assert.Equal([discord.ApplicationId, bob], remaining);
        var removed = Assert.IsType<MemberRemoved>(Assert.Single(Drain(subscription)));
        Assert.Equal((alice, "alice"), (removed.Member.UserId, removed.User.Username));
        Assert.NotNull(discord.State.FindUser(alice)); // the user stays, only the membership goes
        Assert.Null(discord.State.FindMember(guild.Id, alice));
        Assert.Equal(HttpStatusCode.NoContent, discord.Recorded.Requests[0].StatusCode);
    }

    [Fact]
    [Parity("DELETE /guilds/{guild_id}/members/{user_id}")]
    public async Task KickGuildUserAsync_MemberOfSeveralGuilds_LeavesTheOtherMemberships()
    {
        await using var discord = FakeDiscord.Create();
        var first = discord.Seed.Guild("First");
        var second = discord.Seed.Guild("Second");
        var alice = first.Member("alice");
        second.Member(alice);
        using var rest = discord.CreateRestClient();

        await rest.KickGuildUserAsync(first.Id, alice, cancellationToken: Ct);

        Assert.Null(discord.State.FindMember(first.Id, alice));
        Assert.NotNull(discord.State.FindMember(second.Id, alice));
    }

    [Fact]
    [Parity("DELETE /guilds/{guild_id}/members/{user_id}")]
    [Parity("10007")]
    public async Task KickGuildUserAsync_UserThatIsNotAMember_Returns10007()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var stranger = discord.Seed.User("stranger");
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.KickGuildUserAsync(guild.Id, stranger, cancellationToken: Ct));

        AssertError(exception, HttpStatusCode.NotFound, 10007);
        Assert.Empty(Drain(subscription));
    }

    [Fact]
    [Parity("DELETE /guilds/{guild_id}/members/{user_id}")]
    [Parity("50013")]
    public async Task KickGuildUserAsync_TheBotItselfOrTheGuildOwner_Returns50013AndKeepsTheMember()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var alice = discord.Seed.User("alice");
        var owned = discord.State.CreateGuild(new CreateGuildInput("Owned", OwnerId: alice, WithBot: true)).Value.Guild;
        discord.State.AddMember(StateCaller.Fixture, new AddMemberInput(owned.Id, alice));
        using var rest = discord.CreateRestClient();

        var itself = await Assert.ThrowsAsync<RestException>(() => rest.KickGuildUserAsync(guild.Id, discord.ApplicationId, cancellationToken: Ct));
        var owner = await Assert.ThrowsAsync<RestException>(() => rest.KickGuildUserAsync(owned.Id, alice, cancellationToken: Ct));

        AssertError(itself, HttpStatusCode.Forbidden, 50013);
        AssertError(owner, HttpStatusCode.Forbidden, 50013);
        Assert.NotNull(discord.State.FindMember(guild.Id, discord.ApplicationId));
        Assert.NotNull(discord.State.FindMember(owned.Id, alice));
    }

    [Fact]
    [Parity("DELETE /guilds/{guild_id}/members/{user_id}")]
    [Parity("10004")]
    [Parity("50001")]
    public async Task KickGuildUserAsync_UnknownGuildAndGuildWithoutTheBot_Return10004And50001()
    {
        await using var discord = FakeDiscord.Create();
        var elsewhere = discord.Seed.Guild("Elsewhere", withBot: false);
        var carol = elsewhere.Member("carol");
        using var rest = discord.CreateRestClient();

        var unknown = await Assert.ThrowsAsync<RestException>(() => rest.KickGuildUserAsync(424242, carol, cancellationToken: Ct));
        var denied = await Assert.ThrowsAsync<RestException>(() => rest.KickGuildUserAsync(elsewhere.Id, carol, cancellationToken: Ct));

        AssertError(unknown, HttpStatusCode.NotFound, 10004);
        AssertError(denied, HttpStatusCode.Forbidden, 50001);
        Assert.NotNull(discord.State.FindMember(elsewhere.Id, carol));
    }

    [Fact]
    [Parity("PUT /guilds/{guild_id}/members/{user_id}")]
    [Parity("50025")]
    public async Task AddGuildUserAsync_AnyAccessToken_Returns50025BecauseTheFakeHasNoOAuth2Grants()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var stranger = discord.Seed.User("stranger");
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(
            () => rest.AddGuildUserAsync(guild.Id, stranger, new GuildUserProperties("token") { Nickname = "Newbie" }, cancellationToken: Ct));

        AssertError(exception, HttpStatusCode.Forbidden, 50025);
        Assert.Null(discord.State.FindMember(guild.Id, stranger));
    }

    [Fact]
    [Parity("PUT /guilds/{guild_id}/members/{user_id}")]
    [Parity("10004")]
    [Parity("50001")]
    public async Task AddGuildUserAsync_UnknownGuildAndGuildWithoutTheBot_Return10004And50001()
    {
        await using var discord = FakeDiscord.Create();
        var elsewhere = discord.Seed.Guild("Elsewhere", withBot: false);
        var stranger = discord.Seed.User("stranger");
        using var rest = discord.CreateRestClient();

        var unknown = await Assert.ThrowsAsync<RestException>(() => rest.AddGuildUserAsync(424242, stranger, new GuildUserProperties("token"), cancellationToken: Ct));
        var denied = await Assert.ThrowsAsync<RestException>(() => rest.AddGuildUserAsync(elsewhere.Id, stranger, new GuildUserProperties("token"), cancellationToken: Ct));

        AssertError(unknown, HttpStatusCode.NotFound, 10004);
        AssertError(denied, HttpStatusCode.Forbidden, 50001);
    }

    [Theory]
    [InlineData("""{}""", "access_token", "BASE_TYPE_REQUIRED")]
    [InlineData("""{"access_token":5}""", "access_token", "BASE_TYPE_BAD_TYPE")]
    [InlineData("""{"access_token":"t","nick":"%NICK33%"}""", "nick", "BASE_TYPE_MAX_LENGTH")]
    [InlineData("""{"access_token":"t","roles":"x"}""", "roles", "BASE_TYPE_BAD_TYPE")]
    [InlineData("""{"access_token":"t","mute":"yes"}""", "mute", "BOOLEAN_TYPE_COERCE")]
    [Parity("PUT /guilds/{guild_id}/members/{user_id}")]
    [Parity("50035")]
    public async Task SendRequest_PutWithInvalidBody_Returns50035(string body, string field, string code)
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var stranger = discord.Seed.User("stranger");
        using var rest = discord.CreateRestClient();
        var json = body.Replace("%NICK33%", new string('n', 33), StringComparison.Ordinal);

        var exception = await Assert.ThrowsAsync<RestException>(
            () => rest.SendRequestAsync(HttpMethod.Put, Json(json), $"/guilds/{guild.Id}/members/{stranger}", cancellationToken: Ct));

        Assert.Equal(code, Assert.Single(FieldErrors(exception, field)).Code);
    }
}
