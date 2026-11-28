using Enx.NetCord.Testing.State;
using NetCord;
using Xunit;
using static Enx.NetCord.Testing.Tests.Rest.Messages.MessagesTestSupport;
using static Enx.NetCord.Testing.Tests.State.StateTestSupport;

namespace Enx.NetCord.Testing.Tests.Rest.Messages;

/// <summary>The success path of <c>crosspost</c>, now that an announcement channel can be seeded (ADR 0013).</summary>
public sealed class CrosspostMessageTests
{
    [Fact]
    [Parity("POST /channels/{channel_id}/messages/{message_id}/crosspost")]
    public async Task CrosspostMessageAsync_MessageOfASeededAnnouncementChannel_SetsCrosspostedAndIsReadBack()
    {
        await using var discord = FakeDiscord.Create();
        var news = discord.Seed.Guild("Test").AnnouncementChannel("news");
        var alice = discord.Seed.User("alice");
        var messageId = news.Message("release 1.0", alice);
        using var rest = discord.CreateRestClient();

        var crossposted = await rest.CrosspostMessageAsync(news.Id, messageId, cancellationToken: Ct);

        Assert.Equal(messageId, crossposted.Id);
        Assert.True(crossposted.Flags.HasFlag(MessageFlags.Crossposted));
        var read = await rest.GetMessageAsync(news.Id, messageId, cancellationToken: Ct);
        Assert.True(read.Flags.HasFlag(MessageFlags.Crossposted));
        Assert.Equal("release 1.0", read.Content);
    }

    [Fact]
    [Parity("POST /channels/{channel_id}/messages/{message_id}/crosspost")]
    public async Task CrosspostMessageAsync_MessageOfAnAnnouncementChannel_PublishesOneMessageEdited()
    {
        await using var discord = FakeDiscord.Create();
        var news = discord.Seed.Guild("Test").AnnouncementChannel("news");
        var messageId = news.Message("release", discord.Seed.User("alice"));
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        await rest.CrosspostMessageAsync(news.Id, messageId, cancellationToken: Ct);

        var change = Assert.IsType<MessageEdited>(Assert.Single(Drain(subscription)));
        Assert.Equal(messageId, change.Message.Id);
        Assert.Equal(1, change.Message.Flags & 1);
    }

    [Fact]
    [Parity("POST /channels/{channel_id}/messages/{message_id}/crosspost")]
    public async Task CrosspostMessageAsync_AnnouncementChannelInACategory_Works()
    {
        await using var discord = FakeDiscord.Create();
        var news = discord.Seed.Guild("Test").Category("Lobby").AnnouncementChannel("news");
        var messageId = news.Message("release", discord.Seed.User("alice"));
        using var rest = discord.CreateRestClient();

        var crossposted = await rest.CrosspostMessageAsync(news.Id, messageId, cancellationToken: Ct);

        Assert.Equal(messageId, crossposted.Id);
    }
}
