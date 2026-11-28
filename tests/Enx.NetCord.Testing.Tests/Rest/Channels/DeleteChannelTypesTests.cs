using Enx.NetCord.Testing.State;
using NetCord;
using NetCord.Rest;
using Xunit;
using static Enx.NetCord.Testing.Tests.State.StateTestSupport;

namespace Enx.NetCord.Testing.Tests.Rest.Channels;

/// <summary>Deleting the channel types of ADR 0013 through the real <c>RestClient</c>, categories and their children first.</summary>
public sealed class DeleteChannelTypesTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    [Parity("DELETE /channels/{channel_id}")]
    public async Task DeleteChannelAsync_Category_ReturnsItAndKeepsItsChildrenWithoutParent()
    {
        await using var discord = FakeDiscord.Create();
        var lobby = discord.Seed.Guild("Test").Category("Lobby");
        var general = lobby.TextChannel("general");
        var voice = lobby.VoiceChannel("Voice");
        var alice = discord.Seed.User("alice");
        var message = general.Message("hello", alice);
        using var rest = discord.CreateRestClient();

        var deleted = await rest.DeleteChannelAsync(lobby.Id, cancellationToken: Ct);

        Assert.Equal((lobby.Id, "Lobby"), (Assert.IsType<CategoryGuildChannel>(deleted).Id, ((IGuildChannel)deleted).Name));
        await Assert.ThrowsAsync<RestException>(() => rest.GetChannelAsync(lobby.Id, cancellationToken: Ct));
        Assert.Null(Assert.IsType<TextGuildChannel>(await rest.GetChannelAsync(general.Id, cancellationToken: Ct)).ParentId);
        Assert.Null(Assert.IsType<VoiceGuildChannel>(await rest.GetChannelAsync(voice.Id, cancellationToken: Ct)).ParentId);
        Assert.Equal("hello", (await rest.GetMessageAsync(general.Id, message, cancellationToken: Ct)).Content);
    }

    [Fact]
    [Parity("DELETE /channels/{channel_id}")]
    public async Task DeleteChannelAsync_Category_PublishesAChannelUpdatedPerChildThenChannelDeleted()
    {
        await using var discord = FakeDiscord.Create();
        var lobby = discord.Seed.Guild("Test").Category("Lobby");
        var general = lobby.TextChannel("general");
        var forum = lobby.ForumChannel("Forum");
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        await rest.DeleteChannelAsync(lobby.Id, cancellationToken: Ct);

        Assert.Collection(
            Drain(subscription),
            change =>
            {
                var updated = Assert.IsType<ChannelUpdated>(change);
                Assert.Equal((general.Id, null), (updated.Channel.Id, updated.Channel.ParentId));
            },
            change =>
            {
                var updated = Assert.IsType<ChannelUpdated>(change);
                Assert.Equal((forum.Id, null), (updated.Channel.Id, updated.Channel.ParentId));
            },
            change => Assert.Equal(lobby.Id, Assert.IsType<ChannelDeleted>(change).Channel.Id));
    }

    [Fact]
    [Parity("DELETE /channels/{channel_id}")]
    public async Task DeleteChannelAsync_Category_ItsIdAndTheCategoryAreGoneForTheSeed()
    {
        await using var discord = FakeDiscord.Create();
        var lobby = discord.Seed.Guild("Test").Category("Lobby");
        using var rest = discord.CreateRestClient();
        await rest.DeleteChannelAsync(lobby.Id, cancellationToken: Ct);

        Assert.Throws<InvalidOperationException>(() => lobby.TextChannel("late"));
        Assert.Null(discord.State.FindChannel(lobby.Id));
    }

    [Fact]
    [Parity("DELETE /channels/{channel_id}")]
    public async Task DeleteChannelAsync_VoiceStageAndForum_ReturnEachChannelOfItsType()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var voice = guild.VoiceChannel("Voice");
        var stage = guild.StageChannel("Stage");
        var forum = guild.ForumChannel("Forum");
        var news = guild.AnnouncementChannel("news");
        using var rest = discord.CreateRestClient();

        Assert.IsType<VoiceGuildChannel>(await rest.DeleteChannelAsync(voice.Id, cancellationToken: Ct));
        Assert.IsType<StageGuildChannel>(await rest.DeleteChannelAsync(stage.Id, cancellationToken: Ct));
        Assert.IsType<ForumGuildChannel>(await rest.DeleteChannelAsync(forum.Id, cancellationToken: Ct));
        Assert.IsType<AnnouncementGuildChannel>(await rest.DeleteChannelAsync(news.Id, cancellationToken: Ct));

        Assert.Empty(discord.State.ListGuildChannels(StateCaller.Fixture, guild.Id).Value);
    }

    [Fact]
    [Parity("DELETE /channels/{channel_id}")]
    [Parity("50001")]
    public async Task DeleteChannelAsync_CategoryOfAGuildWithoutTheBot_Returns403AndKeepsItsChildren()
    {
        await using var discord = FakeDiscord.Create();
        var lobby = discord.Seed.Guild("Elsewhere", withBot: false).Category("Lobby");
        var general = lobby.TextChannel("general");
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        await Assert.ThrowsAsync<RestException>(() => rest.DeleteChannelAsync(lobby.Id, cancellationToken: Ct));

        Assert.Equal(lobby.Id, discord.State.FindChannel(general.Id)!.ParentId);
        Assert.Empty(Drain(subscription));
    }
}
