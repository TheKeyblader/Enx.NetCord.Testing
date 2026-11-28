using Enx.NetCord.Testing.State;
using Xunit;

namespace Enx.NetCord.Testing.Tests.Facade;

/// <summary>M1-DEBT-06: a seeded channel name has 1 to 100 code points, like the name Discord accepts.</summary>
public sealed class SeedChannelNameTests
{
    [Fact]
    public async Task SeedChannels_EmptyOrTooLongName_ThrowsArgumentOutOfRangeAndCreatesNothing()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var lobby = guild.Category("Lobby");
        var tooLong = new string('n', 101);

        Assert.Throws<ArgumentOutOfRangeException>(() => guild.TextChannel(string.Empty));
        Assert.Throws<ArgumentOutOfRangeException>(() => guild.TextChannel(tooLong));
        Assert.Throws<ArgumentOutOfRangeException>(() => guild.AnnouncementChannel(tooLong));
        Assert.Throws<ArgumentOutOfRangeException>(() => guild.VoiceChannel(string.Empty));
        Assert.Throws<ArgumentOutOfRangeException>(() => guild.StageChannel(tooLong));
        Assert.Throws<ArgumentOutOfRangeException>(() => guild.ForumChannel(string.Empty));
        Assert.Throws<ArgumentOutOfRangeException>(() => guild.Category(tooLong));
        Assert.Throws<ArgumentOutOfRangeException>(() => lobby.TextChannel(string.Empty));
        Assert.Equal([lobby.Id], discord.State.ListGuildChannels(StateCaller.Fixture, guild.Id).Value.Select(channel => channel.Id));
    }

    [Fact]
    public async Task SeedChannels_NameOfOneAndOfHundredCodePoints_IsAccepted()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var emojis = string.Concat(Enumerable.Repeat("😀", 100)); // 100 code points, 200 UTF-16 units

        var one = guild.TextChannel("a");
        var hundred = guild.TextChannel(emojis);

        Assert.Equal(["a", emojis], new[] { discord.State.FindChannel(one.Id)!.Name, discord.State.FindChannel(hundred.Id)!.Name });
    }

    [Fact]
    public async Task SeedChannels_RefusedName_DoesNotConsumeASnowflake()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        Assert.Throws<ArgumentOutOfRangeException>(() => guild.TextChannel(string.Empty));
        var created = guild.TextChannel("ok");
        await using var fresh = FakeDiscord.Create();
        var expected = fresh.Seed.Guild("Test").TextChannel("ok");

        Assert.Equal(expected.Id, created.Id);
    }
}
