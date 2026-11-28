using System.Net;
using Enx.NetCord.Testing.State;
using NetCord;
using NetCord.Rest;
using Xunit;
using static Enx.NetCord.Testing.Tests.Rest.Guilds.GuildsTestSupport;
using static Enx.NetCord.Testing.Tests.Rest.Users.UsersTestSupport;

namespace Enx.NetCord.Testing.Tests.Rest.Members;

/// <summary>
/// <c>GET /guilds/{guild_id}/members</c>, <c>/members/search</c> and <c>/members/{user_id}</c> (M1-REST-07), through the real NetCord
/// <see cref="RestClient"/>.
/// </summary>
public sealed class ReadMembersTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<List<GuildUser>> CollectAsync(IAsyncEnumerable<GuildUser> members)
    {
        var all = new List<GuildUser>();
        await foreach (var member in members.WithCancellation(Ct))
        {
            all.Add(member);
        }

        return all;
    }

    [Fact]
    [Parity("GET /guilds/{guild_id}/members/{user_id}")]
    public async Task GetGuildUserAsync_SeededMember_ReturnsTheUserAndTheDefaults()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var alice = guild.Member("alice");
        using var rest = discord.CreateRestClient();

        var member = await rest.GetGuildUserAsync(guild.Id, alice, cancellationToken: Ct);

        Assert.Equal((alice, "alice", guild.Id), (member.Id, member.Username, member.GuildId));
        Assert.Null(member.Nickname);
        Assert.Empty(member.RoleIds);
        Assert.Equal((false, false), (member.Muted, member.Deafened));
        Assert.Equal(default, member.GuildFlags);
        Assert.Null(member.TimeOutUntil);
        Assert.Equal(discord.State.FindMember(guild.Id, alice)!.JoinedAt, member.JoinedAt);
        var attempt = Assert.Single(discord.Recorded.Requests);
        Assert.Equal(HttpStatusCode.OK, attempt.StatusCode);
    }

    [Fact]
    [Parity("GET /guilds/{guild_id}/members/{user_id}")]
    public async Task GetGuildUserAsync_MemberWithNicknameAndRoles_ReturnsThemWithoutTheEveryoneRole()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var moderators = guild.Role("Moderators");
        var bob = discord.Seed.User("bob");
        discord.State.AddMember(StateCaller.Fixture, new AddMemberInput(guild.Id, bob, "Bobby", [moderators, guild.Id]));
        using var rest = discord.CreateRestClient();

        var member = await rest.GetGuildUserAsync(guild.Id, bob, cancellationToken: Ct);

        Assert.Equal("Bobby", member.Nickname);
        Assert.Equal([moderators], member.RoleIds);
    }

    [Fact]
    [Parity("GET /guilds/{guild_id}/members/{user_id}")]
    [Parity("10007")]
    [Parity("10013")]
    public async Task GetGuildUserAsync_UserThatIsNotAMemberOrDoesNotExist_Returns10007Or10013()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var stranger = discord.Seed.User("stranger");
        using var rest = discord.CreateRestClient();

        var notMember = await Assert.ThrowsAsync<RestException>(() => rest.GetGuildUserAsync(guild.Id, stranger, cancellationToken: Ct));
        var unknownUser = await Assert.ThrowsAsync<RestException>(() => rest.GetGuildUserAsync(guild.Id, 424242, cancellationToken: Ct));

        AssertError(notMember, HttpStatusCode.NotFound, 10007);
        AssertError(unknownUser, HttpStatusCode.NotFound, 10013);
        Assert.Equal("Unknown Member", notMember.Error!.Message);
    }

    [Fact]
    [Parity("GET /guilds/{guild_id}/members/{user_id}")]
    [Parity("10004")]
    [Parity("50001")]
    public async Task GetGuildUserAsync_UnknownGuildAndGuildWithoutTheBot_Return10004And50001()
    {
        await using var discord = FakeDiscord.Create();
        var elsewhere = discord.Seed.Guild("Elsewhere", withBot: false);
        var carol = elsewhere.Member("carol");
        using var rest = discord.CreateRestClient();

        var unknown = await Assert.ThrowsAsync<RestException>(() => rest.GetGuildUserAsync(424242, carol, cancellationToken: Ct));
        var denied = await Assert.ThrowsAsync<RestException>(() => rest.GetGuildUserAsync(elsewhere.Id, carol, cancellationToken: Ct));
        elsewhere.Member(discord.ApplicationId);

        AssertError(unknown, HttpStatusCode.NotFound, 10004);
        AssertError(denied, HttpStatusCode.Forbidden, 50001);
        Assert.Equal("carol", (await rest.GetGuildUserAsync(elsewhere.Id, carol, cancellationToken: Ct)).Username);
    }

    [Fact]
    [Parity("GET /guilds/{guild_id}/members/{user_id}")]
    [Parity("50035")]
    public async Task SendRequest_UserIdAndGuildIdThatAreNotSnowflakes_Return50035ForBoth()
    {
        await using var discord = FakeDiscord.Create();
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(
            () => rest.SendRequestAsync(HttpMethod.Get, $"/guilds/{"abc"}/members/{"def"}", cancellationToken: Ct));

        Assert.Equal("NUMBER_TYPE_COERCE", Assert.Single(FieldErrors(exception, "guild_id")).Code);
        Assert.Equal("NUMBER_TYPE_COERCE", Assert.Single(FieldErrors(exception, "user_id")).Code);
    }

    [Fact]
    [Parity("GET /guilds/{guild_id}/members")]
    public async Task GetGuildUsersAsync_SeveralMembers_ReturnsThemByAscendingUserId()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var alice = guild.Member("alice");
        var bob = guild.Member("bob");
        using var rest = discord.CreateRestClient();

        var members = await CollectAsync(rest.GetGuildUsersAsync(guild.Id));

        Assert.Equal([discord.ApplicationId, alice, bob], members.Select(member => member.Id)); // the bot was created first
        Assert.Equal(["alice", "bob"], members.Skip(1).Select(member => member.Username));
    }

    [Fact]
    [Parity("GET /guilds/{guild_id}/members")]
    public async Task GetGuildUsersAsync_BatchSizeSmallerThanTheGuild_PagesWithAfterUntilAShortPage()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var ids = new[] { discord.ApplicationId, guild.Member("a"), guild.Member("b"), guild.Member("c") };
        using var rest = discord.CreateRestClient();

        var members = await CollectAsync(rest.GetGuildUsersAsync(guild.Id, new PaginationProperties<ulong> { BatchSize = 2 }));

        Assert.Equal(ids.Order(), members.Select(member => member.Id));
        Assert.Equal(3, discord.Recorded.Requests.Count(attempt => attempt.Method == "GET")); // 2 + 2 + an empty page
    }

    [Fact]
    [Parity("GET /guilds/{guild_id}/members")]
    public async Task GetGuildUsersAsync_StartingFromAUser_ReturnsOnlyTheUsersAfterIt()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var alice = guild.Member("alice");
        var bob = guild.Member("bob");
        using var rest = discord.CreateRestClient();

        var members = await CollectAsync(rest.GetGuildUsersAsync(guild.Id, new PaginationProperties<ulong> { From = alice }));

        Assert.Equal([bob], members.Select(member => member.Id));
    }

    [Fact]
    [Parity("GET /guilds/{guild_id}/members")]
    public async Task SendRequest_ListWithoutLimit_ReturnsOneMemberLikeTheDocumentedDefault()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        guild.Member("alice");
        using var rest = discord.CreateRestClient();

        using var json = await ReadJsonAsync(rest.SendRequestAsync(HttpMethod.Get, $"/guilds/{guild.Id}/members", cancellationToken: Ct), Ct);

        var only = Assert.Single(json.RootElement.EnumerateArray().ToList());
        Assert.Equal(Raw(discord.ApplicationId), only.GetProperty("user").GetProperty("id").GetString());
    }

    [Theory]
    [InlineData("?limit=0", "limit", "NUMBER_TYPE_MIN")]
    [InlineData("?limit=1001", "limit", "NUMBER_TYPE_MAX")]
    [InlineData("?limit=many", "limit", "NUMBER_TYPE_COERCE")]
    [InlineData("?after=abc", "after", "NUMBER_TYPE_COERCE")]
    [Parity("GET /guilds/{guild_id}/members")]
    [Parity("50035")]
    public async Task SendRequest_ListWithInvalidQuery_Returns50035(string query, string field, string code)
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(
            () => rest.SendRequestAsync(HttpMethod.Get, $"/guilds/{guild.Id}/members", query, cancellationToken: Ct));

        Assert.Equal(code, Assert.Single(FieldErrors(exception, field)).Code);
    }

    [Fact]
    [Parity("GET /guilds/{guild_id}/members")]
    [Parity("10004")]
    [Parity("50001")]
    public async Task GetGuildUsersAsync_UnknownGuildAndGuildWithoutTheBot_Return10004And50001()
    {
        await using var discord = FakeDiscord.Create();
        var elsewhere = discord.Seed.Guild("Elsewhere", withBot: false);
        using var rest = discord.CreateRestClient();

        var unknown = await Assert.ThrowsAsync<RestException>(() => CollectAsync(rest.GetGuildUsersAsync(424242)));
        var denied = await Assert.ThrowsAsync<RestException>(() => CollectAsync(rest.GetGuildUsersAsync(elsewhere.Id)));

        AssertError(unknown, HttpStatusCode.NotFound, 10004);
        AssertError(denied, HttpStatusCode.Forbidden, 50001);
    }

    [Fact]
    [Parity("GET /guilds/{guild_id}/members/search")]
    public async Task FindGuildUserAsync_UsernamePrefix_ReturnsTheMatchingMembersIgnoringCase()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var alice = guild.Member("Alice");
        guild.Member("bob");
        var alfred = guild.Member("alfred");
        using var rest = discord.CreateRestClient();

        var matches = await rest.FindGuildUserAsync(guild.Id, "AL", 10, cancellationToken: Ct);

        Assert.Equal([alice, alfred], matches.Select(member => member.Id));
        Assert.Equal("Alice", matches[0].Username);
    }

    [Fact]
    [Parity("GET /guilds/{guild_id}/members/search")]
    public async Task FindGuildUserAsync_NicknamePrefixAndLimit_MatchesNicknamesAndStopsAtTheLimit()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var bob = guild.Member("bob");
        guild.Member("carol");
        guild.Member("dave");
        using var rest = discord.CreateRestClient();
        await rest.ModifyGuildUserAsync(guild.Id, bob, o => o.Nickname = "Captain", cancellationToken: Ct);

        var byNick = await rest.FindGuildUserAsync(guild.Id, "cap", 10, cancellationToken: Ct);
        var limited = await rest.FindGuildUserAsync(guild.Id, "ca", 1, cancellationToken: Ct);
        var nothing = await rest.FindGuildUserAsync(guild.Id, "zzz", 10, cancellationToken: Ct);
        var notAPrefix = await rest.FindGuildUserAsync(guild.Id, "ave", 10, cancellationToken: Ct);

        Assert.Equal([bob], byNick.Select(member => member.Id));
        Assert.Equal([bob], limited.Select(member => member.Id)); // by ascending ID: bob (nickname Captain) before carol
        Assert.Empty(nothing);
        Assert.Empty(notAPrefix); // "dave" contains "ave" but does not start with it
    }

    [Theory]
    [InlineData("", "BASE_TYPE_BAD_LENGTH")]
    [InlineData("?limit=5", "BASE_TYPE_REQUIRED")]
    [Parity("GET /guilds/{guild_id}/members/search")]
    [Parity("50035")]
    public async Task SendRequest_SearchWithoutAUsableQuery_Returns50035(string query, string code)
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(
            () => query.Length == 0
                ? rest.FindGuildUserAsync(guild.Id, string.Empty, 5, cancellationToken: Ct)
                : rest.SendRequestAsync(HttpMethod.Get, $"/guilds/{guild.Id}/members/search", query, cancellationToken: Ct));

        Assert.Equal(code, Assert.Single(FieldErrors(exception, "query")).Code);
    }

    [Fact]
    [Parity("GET /guilds/{guild_id}/members/search")]
    [Parity("50035")]
    public async Task FindGuildUserAsync_QueryLongerThan100AndLimitOutOfRange_Return50035()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        using var rest = discord.CreateRestClient();

        var tooLong = await Assert.ThrowsAsync<RestException>(() => rest.FindGuildUserAsync(guild.Id, new string('a', 101), 5, cancellationToken: Ct));
        var tooMany = await Assert.ThrowsAsync<RestException>(() => rest.FindGuildUserAsync(guild.Id, "a", 1001, cancellationToken: Ct));

        Assert.Equal("BASE_TYPE_BAD_LENGTH", Assert.Single(FieldErrors(tooLong, "query")).Code);
        Assert.Equal("NUMBER_TYPE_MAX", Assert.Single(FieldErrors(tooMany, "limit")).Code);
    }

    [Fact]
    [Parity("GET /guilds/{guild_id}/members/search")]
    [Parity("10004")]
    [Parity("50001")]
    public async Task FindGuildUserAsync_UnknownGuildAndGuildWithoutTheBot_Return10004And50001()
    {
        await using var discord = FakeDiscord.Create();
        var elsewhere = discord.Seed.Guild("Elsewhere", withBot: false);
        using var rest = discord.CreateRestClient();

        var unknown = await Assert.ThrowsAsync<RestException>(() => rest.FindGuildUserAsync(424242, "a", 5, cancellationToken: Ct));
        var denied = await Assert.ThrowsAsync<RestException>(() => rest.FindGuildUserAsync(elsewhere.Id, "a", 5, cancellationToken: Ct));

        AssertError(unknown, HttpStatusCode.NotFound, 10004);
        AssertError(denied, HttpStatusCode.Forbidden, 50001);
    }
}
