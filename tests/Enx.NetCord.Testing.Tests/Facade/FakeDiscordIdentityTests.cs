using Enx.NetCord.Testing.Tests.State;
using Microsoft.Extensions.Time.Testing;
using NetCord;
using Xunit;

namespace Enx.NetCord.Testing.Tests.Facade;

public sealed class FakeDiscordIdentityTests
{
    [Fact]
    public async Task Create_NoOptions_BotAndApplicationShareOneAllocatedId()
    {
        await using var discord = FakeDiscord.Create();

        var identity = discord.State.FindBotIdentity()!;

        Assert.NotEqual(0UL, identity.User.Id);
        Assert.Equal(identity.User.Id, identity.Application.Id);
        Assert.True(identity.User.IsBot);
        Assert.Equal(identity.User.Id, discord.Token.Id);
    }

    [Fact]
    public async Task Create_NoOptions_SynthesizesATokenAcceptedByNetCord()
    {
        await using var discord = FakeDiscord.Create();

        var token = new BotToken(discord.Token.RawToken);

        Assert.EndsWith(".fake.test", token.RawToken, StringComparison.Ordinal);
        Assert.Equal(discord.State.FindBotIdentity()!.User.Id, token.Id);
    }

    [Fact]
    public async Task Create_ApplicationIdOnly_SynthesizesTheTokenFromThatId()
    {
        await using var discord = FakeDiscord.Create(new FakeDiscordOptions { ApplicationId = 42 });

        Assert.Equal("NDI.fake.test", discord.Token.RawToken);
        Assert.Equal(42UL, discord.State.FindBotIdentity()!.Application.Id);
        Assert.Equal(42UL, discord.State.FindUser(42)!.Id);
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(1234567890123456789UL)]
    [InlineData(ulong.MaxValue)]
    public async Task Create_ExplicitApplicationId_SyntheticTokenRoundTripsThroughBotToken(ulong id)
    {
        await using var discord = FakeDiscord.Create(new FakeDiscordOptions { ApplicationId = id });

        Assert.Equal(id, new BotToken(discord.Token.RawToken).Id);
    }

    [Fact]
    public async Task Create_TokenOnly_TokenIdIsTheBotAndApplicationId()
    {
        await using var discord = FakeDiscord.Create(new FakeDiscordOptions { BotToken = "NDI.fake.test" });

        var identity = discord.State.FindBotIdentity()!;
        Assert.Equal(42UL, identity.User.Id);
        Assert.Equal(42UL, identity.Application.Id);
        Assert.Equal("NDI.fake.test", discord.Token.RawToken);
    }

    [Fact]
    public async Task Create_TokenAndMatchingApplicationId_IsAccepted()
    {
        await using var discord = FakeDiscord.Create(new FakeDiscordOptions { BotToken = "NDI.fake.test", ApplicationId = 42 });

        Assert.Equal(42UL, discord.State.FindBotIdentity()!.User.Id);
    }

    [Fact]
    public void Create_ApplicationIdDisagreesWithToken_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => FakeDiscord.Create(new FakeDiscordOptions { BotToken = "NDI.fake.test", ApplicationId = 43 }));

        Assert.Contains("43", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_TokenEncodingAZeroId_ThrowsArgumentException()
    {
        // "MA" is base64("0"): NetCord may refuse it itself, otherwise the fake does; both are argument errors.
        Assert.ThrowsAny<ArgumentException>(() => FakeDiscord.Create(new FakeDiscordOptions { BotToken = "MA.fake.test" }));
    }

    [Theory]
    [InlineData("not-a-token")]
    [InlineData("")]
    public void Create_TokenRejectedByBotToken_ThrowsArgumentException(string token)
    {
        Assert.ThrowsAny<ArgumentException>(() => FakeDiscord.Create(new FakeDiscordOptions { BotToken = token }));
    }

    [Fact]
    public void Create_ZeroApplicationId_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FakeDiscord.Create(new FakeDiscordOptions { ApplicationId = 0 }));
    }

    [Fact]
    public void Create_UndefinedUnimplementedRouteMode_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => FakeDiscord.Create(new FakeDiscordOptions { UnimplementedRoutes = (UnimplementedRouteMode)42 }));
    }

    [Fact]
    public async Task Create_ExplicitIdentity_ReservesTheIdOnceForBotAndApplication()
    {
        await using var discord = FakeDiscord.Create(new FakeDiscordOptions { ApplicationId = 42 });

        // Neither a user nor a guild may take the bot's ID again, and creation itself did not reserve it twice.
        Assert.Throws<InvalidOperationException>(() => discord.Seed.User("impostor", id: 42));
        Assert.Throws<InvalidOperationException>(() => discord.Seed.Guild("impostor", id: 42));
        Assert.Equal("FakeBot", discord.State.FindUser(42)!.Username);
        Assert.Null(discord.State.FindGuild(42));
    }

    [Fact]
    public async Task Create_InjectedClockAndGenerator_AreUsedForTheIdentityAndTheSeed()
    {
        var clock = new FakeTimeProvider(StateTestSupport.Start);
        await using var discord = FakeDiscord.Create(new FakeDiscordOptions { TimeProvider = clock });

        var userId = discord.Seed.User("alice");

        Assert.Equal(StateTestSupport.Start, discord.State.FindUser(userId)!.CreatedAt);
        Assert.Equal(StateTestSupport.Start, discord.State.FindBotIdentity()!.User.CreatedAt);
    }

    [Fact]
    public async Task Create_NoClock_UsesTheFixedVirtualClock()
    {
        await using var first = FakeDiscord.Create();
        await using var second = FakeDiscord.Create();

        var a = first.Seed.User("alice");
        var b = second.Seed.User("alice");

        Assert.Equal(a, b);
        Assert.Equal(new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero), first.State.FindUser(a)!.CreatedAt);
    }
}
