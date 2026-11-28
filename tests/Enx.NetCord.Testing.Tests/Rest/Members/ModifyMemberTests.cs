using System.Net;
using Enx.NetCord.Testing.State;
using NetCord;
using NetCord.Rest;
using Xunit;
using static Enx.NetCord.Testing.Tests.Rest.Guilds.GuildsTestSupport;
using static Enx.NetCord.Testing.Tests.Rest.Users.UsersTestSupport;
using static Enx.NetCord.Testing.Tests.State.StateTestSupport;

namespace Enx.NetCord.Testing.Tests.Rest.Members;

/// <summary><c>PATCH /guilds/{guild_id}/members/{user_id}</c> (M1-REST-07) through the real NetCord <see cref="RestClient"/>.</summary>
public sealed class ModifyMemberTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    [Parity("PATCH /guilds/{guild_id}/members/{user_id}")]
    public async Task ModifyGuildUserAsync_Nickname_IsStoredReadBackAndPublishedAsMemberUpdated()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var alice = guild.Member("alice");
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        var updated = await rest.ModifyGuildUserAsync(guild.Id, alice, o => o.Nickname = "Ali", cancellationToken: Ct);

        Assert.Equal("Ali", updated.Nickname);
        Assert.Equal("Ali", (await rest.GetGuildUserAsync(guild.Id, alice, cancellationToken: Ct)).Nickname);
        var change = Assert.IsType<MemberUpdated>(Assert.Single(Drain(subscription)));
        Assert.Equal(("Ali", alice, "alice"), (change.Member.Nick, change.Member.UserId, change.User.Username));
        Assert.Equal(HttpStatusCode.OK, discord.Recorded.Requests[0].StatusCode);
    }

    [Fact]
    [Parity("PATCH /guilds/{guild_id}/members/{user_id}")]
    public async Task ModifyGuildUserAsync_EmptyNicknameOrSameValue_ClearsItOrChangesNothing()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var alice = guild.Member("alice");
        using var rest = discord.CreateRestClient();
        await rest.ModifyGuildUserAsync(guild.Id, alice, o => o.Nickname = "Ali", cancellationToken: Ct);
        using var subscription = discord.State.SubscribeGateway();

        await rest.ModifyGuildUserAsync(guild.Id, alice, o => o.Nickname = "Ali", cancellationToken: Ct); // unchanged
        Assert.Empty(Drain(subscription));
        var cleared = await rest.ModifyGuildUserAsync(guild.Id, alice, o => o.Nickname = string.Empty, cancellationToken: Ct);

        Assert.Null(cleared.Nickname);
        Assert.IsType<MemberUpdated>(Assert.Single(Drain(subscription)));
    }

    [Fact]
    [Parity("PATCH /guilds/{guild_id}/members/{user_id}")]
    public async Task SendRequest_NullNickname_ClearsIt()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var alice = guild.Member("alice");
        using var rest = discord.CreateRestClient();
        await rest.ModifyGuildUserAsync(guild.Id, alice, o => o.Nickname = "Ali", cancellationToken: Ct);

        using var json = await ReadJsonAsync(
            rest.SendRequestAsync(HttpMethod.Patch, Json("""{"nick":null}"""), $"/guilds/{guild.Id}/members/{alice}", cancellationToken: Ct), Ct);

        Assert.Equal(System.Text.Json.JsonValueKind.Null, json.RootElement.GetProperty("nick").ValueKind);
        Assert.Null(discord.State.FindMember(guild.Id, alice)!.Nick);
    }

    [Fact]
    [Parity("PATCH /guilds/{guild_id}/members/{user_id}")]
    public async Task ModifyGuildUserAsync_Roles_ReplaceTheListAndIgnoreTheEveryoneRole()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var alice = guild.Member("alice");
        var moderators = guild.Role("Moderators");
        var vips = guild.Role("VIPs");
        using var rest = discord.CreateRestClient();

        await rest.ModifyGuildUserAsync(guild.Id, alice, o => o.RoleIds = [moderators], cancellationToken: Ct);
        var updated = await rest.ModifyGuildUserAsync(guild.Id, alice, o => o.RoleIds = [vips, guild.Id], cancellationToken: Ct);

        Assert.Equal([vips], updated.RoleIds);
        Assert.Equal([vips], (await rest.GetGuildUserAsync(guild.Id, alice, cancellationToken: Ct)).RoleIds);
    }

    [Fact]
    [Parity("PATCH /guilds/{guild_id}/members/{user_id}")]
    public async Task ModifyGuildUserAsync_EmptyRoleList_RemovesEveryRole()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var alice = guild.Member("alice");
        var moderators = guild.Role("Moderators");
        using var rest = discord.CreateRestClient();
        await rest.ModifyGuildUserAsync(guild.Id, alice, o => o.RoleIds = [moderators], cancellationToken: Ct);

        var updated = await rest.ModifyGuildUserAsync(guild.Id, alice, o => o.RoleIds = [], cancellationToken: Ct);

        Assert.Empty(updated.RoleIds);
    }

    [Fact]
    [Parity("PATCH /guilds/{guild_id}/members/{user_id}")]
    [Parity("10011")]
    public async Task ModifyGuildUserAsync_UnknownRole_Returns10011AndChangesNothing()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var other = discord.Seed.Guild("Other");
        var alice = guild.Member("alice");
        var foreignRole = other.Role("Foreign");
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        var unknown = await Assert.ThrowsAsync<RestException>(
            () => rest.ModifyGuildUserAsync(guild.Id, alice, o => { o.Nickname = "Ali"; o.RoleIds = [424242]; }, cancellationToken: Ct));
        var otherGuild = await Assert.ThrowsAsync<RestException>(
            () => rest.ModifyGuildUserAsync(guild.Id, alice, o => o.RoleIds = [foreignRole], cancellationToken: Ct));

        AssertError(unknown, HttpStatusCode.NotFound, 10011);
        AssertError(otherGuild, HttpStatusCode.NotFound, 10011);
        Assert.Equal("Unknown Role", unknown.Error!.Message);
        Assert.Null(discord.State.FindMember(guild.Id, alice)!.Nick); // validated in full before the first write
        Assert.Empty(Drain(subscription));
    }

    [Fact]
    [Parity("PATCH /guilds/{guild_id}/members/{user_id}")]
    public async Task ModifyGuildUserAsync_MuteDeafAndFlags_AreStoredAndRead()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var alice = guild.Member("alice");
        using var rest = discord.CreateRestClient();

        var updated = await rest.ModifyGuildUserAsync(
            guild.Id,
            alice,
            o => { o.Muted = true; o.Deafened = true; o.GuildFlags = GuildUserFlags.BypassesVerification; },
            cancellationToken: Ct);

        Assert.Equal((true, true, GuildUserFlags.BypassesVerification), (updated.Muted, updated.Deafened, updated.GuildFlags));
        var stored = discord.State.FindMember(guild.Id, alice)!;
        Assert.Equal((true, true, 4), (stored.Mute, stored.Deaf, stored.Flags));
    }

    [Fact]
    [Parity("PATCH /guilds/{guild_id}/members/{user_id}")]
    public async Task ModifyGuildUserAsync_TimeOutThenClear_SetsAndRemovesTheTimeout()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var alice = guild.Member("alice");
        using var rest = discord.CreateRestClient();
        var until = discord.State.Clock.GetUtcNow().AddDays(7);

        var timedOut = await rest.ModifyGuildUserAsync(guild.Id, alice, o => o.TimeOutUntil = until, cancellationToken: Ct);
        var cleared = await rest.ModifyGuildUserAsync(guild.Id, alice, o => o.TimeOutUntil = default(DateTimeOffset), cancellationToken: Ct);

        Assert.Equal(until, timedOut.TimeOutUntil);
        Assert.Null(cleared.TimeOutUntil);
        Assert.Null(discord.State.FindMember(guild.Id, alice)!.CommunicationDisabledUntil);
    }

    [Fact]
    [Parity("PATCH /guilds/{guild_id}/members/{user_id}")]
    [Parity("50035")]
    public async Task ModifyGuildUserAsync_TimeoutMoreThan28DaysAway_Returns50035()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var alice = guild.Member("alice");
        using var rest = discord.CreateRestClient();
        var until = discord.State.Clock.GetUtcNow().AddDays(29);

        var exception = await Assert.ThrowsAsync<RestException>(
            () => rest.ModifyGuildUserAsync(guild.Id, alice, o => o.TimeOutUntil = until, cancellationToken: Ct));

        Assert.Single(FieldErrors(exception, "communication_disabled_until"));
        Assert.Null(discord.State.FindMember(guild.Id, alice)!.CommunicationDisabledUntil);
    }

    [Fact]
    [Parity("PATCH /guilds/{guild_id}/members/{user_id}")]
    [Parity("50013")]
    public async Task ModifyGuildUserAsync_TimeoutOfTheOwnerOrAnAdministrator_Returns50013()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var admins = discord.State.CreateRole(StateCaller.Fixture, new CreateRoleInput(guild.Id, "Admins", Permissions: 1UL << 3)).Value.Id;
        var admin = guild.Member("admin");
        using var rest = discord.CreateRestClient();
        await rest.AddGuildUserRoleAsync(guild.Id, admin, admins, cancellationToken: Ct);
        var until = discord.State.Clock.GetUtcNow().AddDays(1);

        var owner = await Assert.ThrowsAsync<RestException>(
            () => rest.ModifyGuildUserAsync(guild.Id, discord.ApplicationId, o => o.TimeOutUntil = until, cancellationToken: Ct));
        var administrator = await Assert.ThrowsAsync<RestException>(
            () => rest.ModifyGuildUserAsync(guild.Id, admin, o => o.TimeOutUntil = until, cancellationToken: Ct));

        AssertError(owner, HttpStatusCode.Forbidden, 50013);
        AssertError(administrator, HttpStatusCode.Forbidden, 50013);
        Assert.Equal("Missing Permissions", owner.Error!.Message);
    }

    [Fact]
    [Parity("PATCH /guilds/{guild_id}/members/{user_id}")]
    public async Task ModifyGuildUserAsync_TimeoutAlreadyPassed_IsStoredAsSent()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var alice = guild.Member("alice");
        using var rest = discord.CreateRestClient();
        var past = discord.State.Clock.GetUtcNow().AddDays(-1);

        var updated = await rest.ModifyGuildUserAsync(guild.Id, alice, o => o.TimeOutUntil = past, cancellationToken: Ct);

        Assert.Equal(past, updated.TimeOutUntil);
    }

    [Fact]
    [Parity("PATCH /guilds/{guild_id}/members/{user_id}")]
    [Parity("10007")]
    public async Task ModifyGuildUserAsync_UserThatIsNotAMember_Returns10007()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var stranger = discord.Seed.User("stranger");
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(
            () => rest.ModifyGuildUserAsync(guild.Id, stranger, o => o.Nickname = "x", cancellationToken: Ct));

        AssertError(exception, HttpStatusCode.NotFound, 10007);
    }

    [Fact]
    [Parity("PATCH /guilds/{guild_id}/members/{user_id}")]
    [Parity("10004")]
    [Parity("50001")]
    public async Task ModifyGuildUserAsync_UnknownGuildAndGuildWithoutTheBot_Return10004And50001()
    {
        await using var discord = FakeDiscord.Create();
        var elsewhere = discord.Seed.Guild("Elsewhere", withBot: false);
        var carol = elsewhere.Member("carol");
        using var rest = discord.CreateRestClient();

        var unknown = await Assert.ThrowsAsync<RestException>(() => rest.ModifyGuildUserAsync(424242, carol, o => o.Nickname = "x", cancellationToken: Ct));
        var denied = await Assert.ThrowsAsync<RestException>(() => rest.ModifyGuildUserAsync(elsewhere.Id, carol, o => o.Nickname = "x", cancellationToken: Ct));

        AssertError(unknown, HttpStatusCode.NotFound, 10004);
        AssertError(denied, HttpStatusCode.Forbidden, 50001);
        Assert.Null(discord.State.FindMember(elsewhere.Id, carol)!.Nick);
    }

    [Fact]
    [Parity("PATCH /guilds/{guild_id}/members/{user_id}")]
    [Parity("50035")]
    public async Task ModifyGuildUserAsync_NicknameLongerThan32_Returns50035()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var alice = guild.Member("alice");
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(
            () => rest.ModifyGuildUserAsync(guild.Id, alice, o => o.Nickname = new string('n', 33), cancellationToken: Ct));

        Assert.Equal("BASE_TYPE_MAX_LENGTH", Assert.Single(FieldErrors(exception, "nick")).Code);
    }

    [Theory]
    [InlineData("""{"mute":"yes"}""", "mute", "BOOLEAN_TYPE_COERCE")]
    [InlineData("""{"deaf":3}""", "deaf", "BOOLEAN_TYPE_COERCE")]
    [InlineData("""{"nick":5}""", "nick", "BASE_TYPE_BAD_TYPE")]
    [InlineData("""{"roles":"mods"}""", "roles", "BASE_TYPE_BAD_TYPE")]
    [InlineData("""{"roles":["abc"]}""", "roles.0", "NUMBER_TYPE_COERCE")]
    [InlineData("""{"channel_id":"abc"}""", "channel_id", "NUMBER_TYPE_COERCE")]
    [InlineData("""{"flags":"x"}""", "flags", "NUMBER_TYPE_COERCE")]
    [InlineData("""{"communication_disabled_until":"tomorrow"}""", "communication_disabled_until", "DATE_TIME_TYPE_PARSE")]
    [InlineData("""{"communication_disabled_until":12}""", "communication_disabled_until", "BASE_TYPE_BAD_TYPE")]
    [Parity("PATCH /guilds/{guild_id}/members/{user_id}")]
    [Parity("50035")]
    public async Task SendRequest_PatchWithInvalidField_Returns50035(string body, string path, string code)
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var alice = guild.Member("alice");
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(
            () => rest.SendRequestAsync(HttpMethod.Patch, Json(body), $"/guilds/{guild.Id}/members/{alice}", cancellationToken: Ct));

        Assert.Equal(code, Assert.Single(FieldErrors(exception, path.Split('.'))).Code);
    }

    [Fact]
    [Parity("PATCH /guilds/{guild_id}/members/{user_id}")]
    [Parity("50109")]
    public async Task SendRequest_PatchWithABodyThatIsNotJson_Returns50109()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var alice = guild.Member("alice");
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(
            () => rest.SendRequestAsync(HttpMethod.Patch, Json("{nick:"), $"/guilds/{guild.Id}/members/{alice}", cancellationToken: Ct));

        AssertError(exception, HttpStatusCode.BadRequest, 50109);
    }

    [Fact]
    [Parity("PATCH /guilds/{guild_id}/members/{user_id}")]
    [Parity("50035")]
    public async Task SendRequest_PatchWithABadIdAndABadField_ReportsBoth()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(
            () => rest.SendRequestAsync(HttpMethod.Patch, Json("""{"mute":"yes"}"""), $"/guilds/{guild.Id}/members/{"abc"}", cancellationToken: Ct));

        Assert.Single(FieldErrors(exception, "user_id"));
        Assert.Single(FieldErrors(exception, "mute"));
    }

    [Fact]
    [Parity("PATCH /guilds/{guild_id}/members/{user_id}")]
    public async Task ModifyGuildUserAsync_MemberOfSeveralGuilds_ChangesOnlyTheMembershipOfThatGuild()
    {
        await using var discord = FakeDiscord.Create();
        var first = discord.Seed.Guild("First");
        var second = discord.Seed.Guild("Second");
        var alice = first.Member("alice");
        second.Member(alice);
        using var rest = discord.CreateRestClient();

        await rest.ModifyGuildUserAsync(first.Id, alice, o => o.Nickname = "Ali", cancellationToken: Ct);

        Assert.Equal("Ali", discord.State.FindMember(first.Id, alice)!.Nick);
        Assert.Null(discord.State.FindMember(second.Id, alice)!.Nick);
    }
}
