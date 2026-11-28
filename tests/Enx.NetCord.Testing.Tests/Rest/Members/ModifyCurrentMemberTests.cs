using System.Net;
using Enx.NetCord.Testing.State;
using NetCord;
using NetCord.Rest;
using Xunit;
using static Enx.NetCord.Testing.Tests.Rest.Guilds.GuildsTestSupport;
using static Enx.NetCord.Testing.Tests.Rest.Users.UsersTestSupport;
using static Enx.NetCord.Testing.Tests.State.StateTestSupport;

namespace Enx.NetCord.Testing.Tests.Rest.Members;

/// <summary><c>PATCH /guilds/{guild_id}/members/@me</c> (M1-REST-07) through the real NetCord <see cref="RestClient"/>.</summary>
public sealed class ModifyCurrentMemberTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    [Parity("PATCH /guilds/{guild_id}/members/@me")]
    public async Task ModifyCurrentGuildUserAsync_Nickname_ChangesTheBotMemberAndPublishesMemberUpdated()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var alice = guild.Member("alice");
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        var updated = await rest.ModifyCurrentGuildUserAsync(guild.Id, o => o.Nickname = "Helper", cancellationToken: Ct);

        Assert.Equal((discord.ApplicationId, "Helper"), (updated.Id, updated.Nickname));
        Assert.Equal("Helper", (await rest.GetGuildUserAsync(guild.Id, discord.ApplicationId, cancellationToken: Ct)).Nickname);
        Assert.Null(discord.State.FindMember(guild.Id, alice)!.Nick);
        var change = Assert.IsType<MemberUpdated>(Assert.Single(Drain(subscription)));
        Assert.Equal((discord.ApplicationId, "Helper"), (change.Member.UserId, change.Member.Nick));
        var attempt = discord.Recorded.Requests[0];
        Assert.Equal(("PATCH", "/guilds/{guild_id}/members/@me", HttpStatusCode.OK), (attempt.Method, attempt.RouteTemplate, attempt.StatusCode));
    }

    [Fact]
    [Parity("PATCH /guilds/{guild_id}/members/@me")]
    public async Task ModifyCurrentGuildUserAsync_EmptyNickname_ClearsIt()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        using var rest = discord.CreateRestClient();
        await rest.ModifyCurrentGuildUserAsync(guild.Id, o => o.Nickname = "Helper", cancellationToken: Ct);

        var cleared = await rest.ModifyCurrentGuildUserAsync(guild.Id, o => o.Nickname = string.Empty, cancellationToken: Ct);

        Assert.Null(cleared.Nickname);
    }

    [Fact]
    [Parity("PATCH /guilds/{guild_id}/members/@me")]
    public async Task ModifyCurrentGuildUserAsync_AvatarAndBanner_AreStoredAsGuildImageHashes()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        using var rest = discord.CreateRestClient();

        var updated = await rest.ModifyCurrentGuildUserAsync(
            guild.Id,
            o =>
            {
                o.Avatar = new ImageProperties(ImageFormat.Png, new byte[] { 1, 2, 3 });
                o.Banner = new ImageProperties(ImageFormat.Gif, new byte[] { 4, 5, 6 });
            },
            cancellationToken: Ct);

        Assert.NotNull(updated.GuildAvatarHash);
        Assert.StartsWith("a_", updated.GuildBannerHash);
        Assert.True(updated.HasGuildAvatar);
        var removed = await rest.ModifyCurrentGuildUserAsync(guild.Id, o => o.Avatar = ImageProperties.Empty, cancellationToken: Ct);
        Assert.Null(removed.GuildAvatarHash);
        Assert.NotNull(removed.GuildBannerHash);
    }

    [Fact]
    [Parity("PATCH /guilds/{guild_id}/members/@me")]
    public async Task ModifyCurrentGuildUserAsync_Bio_IsValidatedAndNotPartOfTheMember()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        var updated = await rest.ModifyCurrentGuildUserAsync(guild.Id, o => o.Bio = "I help.", cancellationToken: Ct);

        Assert.Equal(discord.ApplicationId, updated.Id);
        Assert.Empty(Drain(subscription));
    }

    [Theory]
    [InlineData("""{"nick":"%NICK33%"}""", "nick", "BASE_TYPE_MAX_LENGTH")]
    [InlineData("""{"bio":"%BIO301%"}""", "bio", "BASE_TYPE_MAX_LENGTH")]
    [InlineData("""{"avatar":"not an image"}""", "avatar", "IMAGE_INVALID")]
    [InlineData("""{"banner":4}""", "banner", "BASE_TYPE_BAD_TYPE")]
    [InlineData("""{"nick":true}""", "nick", "BASE_TYPE_BAD_TYPE")]
    [Parity("PATCH /guilds/{guild_id}/members/@me")]
    [Parity("50035")]
    public async Task SendRequest_PatchMeWithInvalidField_Returns50035(string body, string field, string code)
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        using var rest = discord.CreateRestClient();
        var json = body.Replace("%NICK33%", new string('n', 33), StringComparison.Ordinal).Replace("%BIO301%", new string('b', 301), StringComparison.Ordinal);

        var exception = await Assert.ThrowsAsync<RestException>(
            () => rest.SendRequestAsync(HttpMethod.Patch, Json(json), $"/guilds/{guild.Id}/members/@me", cancellationToken: Ct));

        Assert.Equal(code, Assert.Single(FieldErrors(exception, field)).Code);
    }

    [Fact]
    [Parity("PATCH /guilds/{guild_id}/members/@me")]
    [Parity("10004")]
    [Parity("50001")]
    public async Task ModifyCurrentGuildUserAsync_UnknownGuildAndGuildWithoutTheBot_Return10004And50001()
    {
        await using var discord = FakeDiscord.Create();
        var elsewhere = discord.Seed.Guild("Elsewhere", withBot: false);
        using var rest = discord.CreateRestClient();

        var unknown = await Assert.ThrowsAsync<RestException>(() => rest.ModifyCurrentGuildUserAsync(424242, o => o.Nickname = "x", cancellationToken: Ct));
        var denied = await Assert.ThrowsAsync<RestException>(() => rest.ModifyCurrentGuildUserAsync(elsewhere.Id, o => o.Nickname = "x", cancellationToken: Ct));

        AssertError(unknown, HttpStatusCode.NotFound, 10004);
        AssertError(denied, HttpStatusCode.Forbidden, 50001);
    }
}
