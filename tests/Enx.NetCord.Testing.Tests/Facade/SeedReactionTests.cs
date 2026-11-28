using Xunit;

namespace Enx.NetCord.Testing.Tests.Facade;

public sealed class SeedReactionTests
{
    [Fact]
    public async Task Reaction_UnicodeAndCustomEmoji_AreVisibleToTheBotThroughRest()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var alice = guild.Member("alice");
        var message = general.Message("hello", alice);

        general.Reaction(message, "👍", alice);
        general.Reaction(message, "wave:123456789012345678", alice, burst: true);

        using var rest = discord.CreateRestClient();
        var read = await rest.GetMessageAsync(general.Id, message, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(["👍", "wave"], read.Reactions.Select(r => r.Emoji.Name));
        Assert.Equal([1, 1], read.Reactions.Select(r => r.Count));
        Assert.Equal((0, 1), (read.Reactions[1].CountDetails.Normal, read.Reactions[1].CountDetails.Burst));
    }

    [Fact]
    public async Task Reaction_SameUserAndEmojiTwice_IsIdempotent()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var alice = guild.Member("alice");
        var message = general.Message("hello", alice);

        general.Reaction(message, "👍", alice);
        general.Reaction(message, "👍", alice);

        Assert.Equal(1, Assert.Single(discord.State.FindMessage(general.Id, message)!.Reactions).NormalCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("thumbsup")]
    [InlineData("wave:abc")]
    public async Task Reaction_NotAnEmoji_ThrowsArgumentException(string emoji)
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var alice = guild.Member("alice");
        var message = general.Message("hello", alice);

        var exception = Assert.Throws<ArgumentException>(() => general.Reaction(message, emoji, alice));

        Assert.Equal("emoji", exception.ParamName);
        Assert.Empty(discord.State.FindMessage(general.Id, message)!.Reactions);
    }

    [Fact]
    public async Task Reaction_NullEmoji_ThrowsArgumentNullException()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");

        Assert.Throws<ArgumentNullException>(() => general.Reaction(1, null!, 2));
    }

    [Fact]
    public async Task Reaction_UnknownMessageOrUser_ThrowsInvalidOperationException()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var alice = guild.Member("alice");
        var message = general.Message("hello", alice);

        Assert.Throws<InvalidOperationException>(() => general.Reaction(424242, "👍", alice));
        Assert.Throws<InvalidOperationException>(() => general.Reaction(message, "👍", 424242));
        Assert.Empty(discord.State.FindMessage(general.Id, message)!.Reactions);
    }

    [Fact]
    public async Task Reaction_AfterDispose_ThrowsObjectDisposedException()
    {
        var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var alice = guild.Member("alice");
        var message = general.Message("hello", alice);
        await discord.DisposeAsync();

        Assert.Throws<ObjectDisposedException>(() => general.Reaction(message, "👍", alice));
    }
}
