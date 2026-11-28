using System.Net;
using Enx.NetCord.Testing.State;
using NetCord.Rest;
using Xunit;
using static Enx.NetCord.Testing.Tests.Rest.Messages.MessagesTestSupport;
using static Enx.NetCord.Testing.Tests.Rest.Pins.PinsTestSupport;

namespace Enx.NetCord.Testing.Tests.Rest.Pins;

public sealed class GetChannelPinsTests
{
    private static readonly DateTimeOffset Now = VirtualTimeProvider.DefaultNow;

    [Fact]
    [Parity("GET /channels/{channel_id}/messages/pins")]
    public async Task GetChannelPinsAsync_PinnedMessages_ReturnsThemNewestPinFirstWithTheirPinTime()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var alice = guild.Member("alice");
        var first = general.Message("first", alice);
        var second = general.Message("second", alice);
        var unpinned = general.Message("not pinned", alice);
        using var rest = discord.CreateRestClient();
        await rest.PinMessageAsync(general.Id, second, cancellationToken: Ct);
        await rest.PinMessageAsync(general.Id, first, cancellationToken: Ct);

        var pins = await rest.GetChannelPinsAsync(general.Id, cancellationToken: Ct).ToListAsync(Ct);

        Assert.Equal([first, second], pins.Select(pin => pin.Message.Id));
        Assert.Equal([Now.AddSeconds(1), Now], pins.Select(pin => pin.PinnedAt));
        Assert.All(pins, pin => Assert.True(pin.Message.IsPinned));
        Assert.DoesNotContain(pins, pin => pin.Message.Id == unpinned);
        Assert.Equal("first", pins[0].Message.Content);
        discord.Recorded.ShouldHaveRequestedOnce($"GET /channels/{general.Id}/messages/pins");
    }

    [Fact]
    [Parity("GET /channels/{channel_id}/messages/pins")]
    public async Task GetChannelPinsAsync_NoPins_ReturnsAnEmptyPage()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();

        Assert.Empty(await rest.GetChannelPinsAsync(general.Id, cancellationToken: Ct).ToListAsync(Ct));

        using var raw = await GetRawAsync(rest, $"/channels/{general.Id}/messages/pins");
        Assert.Equal(0, raw.RootElement.GetProperty("items").GetArrayLength());
        Assert.False(raw.RootElement.GetProperty("has_more").GetBoolean());
    }

    [Fact]
    [Parity("GET /channels/{channel_id}/messages/pins")]
    public async Task GetChannelPinsAsync_MoreThanOneBatch_PagesWithBeforeAndLosesNone()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var alice = guild.Member("alice");
        var pinned = Enumerable.Range(0, 5).Select(i => SeedPinned(discord, general.Id, alice, $"pin {i}")).ToList();
        using var rest = discord.CreateRestClient();

        var pins = await rest.GetChannelPinsAsync(general.Id, new PaginationProperties<DateTimeOffset> { BatchSize = 2 }, cancellationToken: Ct).ToListAsync(Ct);

        Assert.Equal(Enumerable.Reverse(pinned), pins.Select(pin => pin.Message.Id));
        Assert.Equal(3, discord.Recorded.Requests.Count(r => r.Method == "GET"));
    }

    [Fact]
    [Parity("GET /channels/{channel_id}/messages/pins")]
    public async Task GetChannelPinsAsync_LimitSmallerThanThePins_SetsHasMore()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var alice = guild.Member("alice");
        SeedPinned(discord, general.Id, alice, "a");
        SeedPinned(discord, general.Id, alice, "b");
        using var rest = discord.CreateRestClient();

        using var limited = await GetRawAsync(rest, $"/channels/{general.Id}/messages/pins", "?limit=1");
        using var all = await GetRawAsync(rest, $"/channels/{general.Id}/messages/pins", "?limit=2");

        Assert.Equal((1, true), (limited.RootElement.GetProperty("items").GetArrayLength(), limited.RootElement.GetProperty("has_more").GetBoolean()));
        Assert.Equal((2, false), (all.RootElement.GetProperty("items").GetArrayLength(), all.RootElement.GetProperty("has_more").GetBoolean()));
    }

    [Fact]
    [Parity("GET /channels/{channel_id}/messages/pins")]
    public async Task GetChannelPinsAsync_DirectMessageChannel_ListsItsPins()
    {
        await using var discord = FakeDiscord.Create();
        var alice = discord.Seed.User("alice");
        var dm = OpenDm(discord, alice);
        var message = SeedPinned(discord, dm, alice, "keep");
        using var rest = discord.CreateRestClient();

        Assert.Equal(message, Assert.Single(await rest.GetChannelPinsAsync(dm, cancellationToken: Ct).ToListAsync(Ct)).Message.Id);
    }

    [Fact]
    [Parity("GET /channels/{channel_id}/messages/pins")]
    public async Task GetChannelPinsAsync_UnknownChannelOrNotMember_FailsWithTheCatalogErrors()
    {
        await using var discord = FakeDiscord.Create();
        var secret = discord.Seed.Guild("Elsewhere", withBot: false).TextChannel("secret");
        using var rest = discord.CreateRestClient();

        var unknown = await Assert.ThrowsAsync<RestException>(() => rest.GetChannelPinsAsync(123456789, cancellationToken: Ct).ToListAsync(Ct).AsTask());
        var forbidden = await Assert.ThrowsAsync<RestException>(() => rest.GetChannelPinsAsync(secret.Id, cancellationToken: Ct).ToListAsync(Ct).AsTask());

        AssertError(unknown, HttpStatusCode.NotFound, 10003);
        AssertError(forbidden, HttpStatusCode.Forbidden, 50001);
    }

    [Fact]
    [Parity("GET /channels/{channel_id}/messages/pins")]
    public async Task GetChannelPinsAsync_CategoryChannel_Returns50024()
    {
        await using var discord = FakeDiscord.Create();
        var lobby = discord.Seed.Guild("Test").Category("Lobby");
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.GetChannelPinsAsync(lobby.Id, cancellationToken: Ct).ToListAsync(Ct).AsTask());

        AssertError(exception, HttpStatusCode.BadRequest, 50024);
    }

    [Theory]
    [InlineData("limit=0", "limit")]
    [InlineData("limit=51", "limit")]
    [InlineData("limit=many", "limit")]
    [InlineData("before=yesterday", "before")]
    [Parity("GET /channels/{channel_id}/messages/pins")]
    public async Task GetChannelPinsAsync_InvalidQuery_Fails50035OnTheParameter(string query, string field)
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(async () =>
        {
            await using var stream = await rest.SendRequestAsync(HttpMethod.Get, $"/channels/{general.Id}/messages/pins", $"?{query}", cancellationToken: Ct);
        });

        AssertError(exception, HttpStatusCode.BadRequest, 50035);
        Assert.Single(FieldErrors(exception, field));
    }

    [Fact]
    [Parity("GET /channels/{channel_id}/messages/pins")]
    public async Task GetChannelPinsAsync_MalformedChannelId_Fails50035()
    {
        await using var discord = FakeDiscord.Create();
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(async () =>
        {
            await using var stream = await rest.SendRequestAsync(HttpMethod.Get, $"/channels/{"abc"}/messages/pins", cancellationToken: Ct);
        });

        AssertError(exception, HttpStatusCode.BadRequest, 50035);
        Assert.Equal("NUMBER_TYPE_COERCE", Assert.Single(FieldErrors(exception, "channel_id")).Code);
    }
}
