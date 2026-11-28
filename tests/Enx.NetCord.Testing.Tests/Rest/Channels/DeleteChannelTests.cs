using System.Net;
using Enx.NetCord.Testing.State;
using NetCord;
using NetCord.Rest;
using Xunit;
using static Enx.NetCord.Testing.Tests.Rest.Channels.ChannelsTestSupport;
using static Enx.NetCord.Testing.Tests.State.StateTestSupport;

namespace Enx.NetCord.Testing.Tests.Rest.Channels;

public sealed class DeleteChannelTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    [Parity("DELETE /channels/{channel_id}")]
    public async Task DeleteChannelAsync_GuildChannel_ReturnsTheDeletedChannelAndItIsGone()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var keep = guild.TextChannel("keep");
        using var rest = discord.CreateRestClient();

        var deleted = await rest.DeleteChannelAsync(general.Id, cancellationToken: Ct);

        var text = Assert.IsType<TextGuildChannel>(deleted);
        Assert.Equal(general.Id, text.Id);
        Assert.Equal("general", text.Name);
        var exception = await Assert.ThrowsAsync<RestException>(() => rest.GetChannelAsync(general.Id, cancellationToken: Ct));
        AssertError(exception, HttpStatusCode.NotFound, 10003);
        Assert.Null(discord.State.FindChannel(general.Id));
        var remaining = discord.State.ListGuildChannels(StateCaller.Fixture, guild.Id).Value;
        Assert.Equal(keep.Id, Assert.Single(remaining).Id);
    }

    [Fact]
    [Parity("DELETE /channels/{channel_id}")]
    public async Task DeleteChannelAsync_ChannelWithMessages_DeletesTheirMessagesToo()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        var author = discord.Seed.User("alice");
        var messageId = general.Message("hello", author);
        using var rest = discord.CreateRestClient();

        await rest.DeleteChannelAsync(general.Id, cancellationToken: Ct);

        Assert.Null(discord.State.FindMessage(general.Id, messageId));
    }

    [Fact]
    [Parity("DELETE /channels/{channel_id}")]
    public async Task DeleteChannelAsync_GuildChannel_PublishesOneChannelDeletedWithTheLastState()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        await rest.DeleteChannelAsync(general.Id, cancellationToken: Ct);

        var change = Assert.IsType<ChannelDeleted>(Assert.Single(Drain(subscription)));
        Assert.Equal(general.Id, change.Channel.Id);
        Assert.Equal("general", change.Channel.Name);
    }

    [Fact]
    [Parity("DELETE /channels/{channel_id}")]
    public async Task DeleteChannelAsync_DeletedTwice_Returns404TheSecondTime()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();
        await rest.DeleteChannelAsync(general.Id, cancellationToken: Ct);

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.DeleteChannelAsync(general.Id, cancellationToken: Ct));

        AssertError(exception, HttpStatusCode.NotFound, 10003);
    }

    [Fact]
    [Parity("DELETE /channels/{channel_id}")]
    public async Task DeleteChannelAsync_DmChannel_ClosesItWithoutDestroyingIt()
    {
        await using var discord = FakeDiscord.Create();
        var aliceId = discord.Seed.User("alice");
        using var rest = discord.CreateRestClient();
        var dm = await rest.GetDMChannelAsync(aliceId, cancellationToken: Ct);
        using var subscription = discord.State.SubscribeGateway();

        var closed = await rest.DeleteChannelAsync(dm.Id, cancellationToken: Ct);

        Assert.Equal(dm.Id, Assert.IsType<DMChannel>(closed).Id);
        Assert.Empty(Drain(subscription));
        Assert.Equal(dm.Id, (await rest.GetChannelAsync(dm.Id, cancellationToken: Ct)).Id);
        Assert.Equal(dm.Id, (await rest.GetDMChannelAsync(aliceId, cancellationToken: Ct)).Id); // reopening finds the same channel
    }

    [Fact]
    [Parity("DELETE /channels/{channel_id}")]
    [Parity("10003")]
    public async Task DeleteChannelAsync_UnknownChannel_Returns404UnknownChannel()
    {
        await using var discord = FakeDiscord.Create();
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.DeleteChannelAsync(123456789, cancellationToken: Ct));

        AssertError(exception, HttpStatusCode.NotFound, 10003);
    }

    [Fact]
    [Parity("DELETE /channels/{channel_id}")]
    [Parity("50001")]
    public async Task DeleteChannelAsync_ChannelOfAGuildWithoutTheBot_Returns403AndDeletesNothing()
    {
        await using var discord = FakeDiscord.Create();
        var hidden = discord.Seed.Guild("Elsewhere", withBot: false).TextChannel("secret");
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.DeleteChannelAsync(hidden.Id, cancellationToken: Ct));

        AssertError(exception, HttpStatusCode.Forbidden, 50001);
        Assert.NotNull(discord.State.FindChannel(hidden.Id));
        Assert.Empty(Drain(subscription));
    }

    [Fact]
    [Parity("DELETE /channels/{channel_id}")]
    [Parity("50035")]
    public async Task SendRequest_DeleteWithANonSnowflakeId_Returns50035OnChannelId()
    {
        await using var discord = FakeDiscord.Create();
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(
            () => rest.SendRequestAsync(HttpMethod.Delete, $"/channels/abc", cancellationToken: Ct));

        Assert.Equal("NUMBER_TYPE_COERCE", Assert.Single(Users.UsersTestSupport.FieldErrors(exception, "channel_id")).Code);
    }
}
