using System.Net;
using Enx.NetCord.Testing.State;
using NetCord.Rest;
using Xunit;
using static Enx.NetCord.Testing.Tests.Rest.Messages.MessagesTestSupport;
using static Enx.NetCord.Testing.Tests.Rest.Pins.PinsTestSupport;
using static Enx.NetCord.Testing.Tests.State.StateTestSupport;

namespace Enx.NetCord.Testing.Tests.Rest.Typing;

public sealed class TriggerTypingTests
{
    [Fact]
    [Parity("POST /channels/{channel_id}/typing")]
    [Parity("http:204")]
    public async Task TriggerTypingAsync_GuildChannel_Returns204AndPublishesTypingStarted()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        await rest.TriggerTypingAsync(general.Id, cancellationToken: Ct);

        var typing = Assert.IsType<TypingStarted>(Assert.Single(Drain(subscription)));
        Assert.Equal((guild.Id, general.Id, discord.ApplicationId, VirtualTimeProvider.DefaultNow), (typing.GuildId, typing.ChannelId, typing.UserId, typing.Timestamp));
        Assert.Equal(discord.ApplicationId, typing.Member?.UserId);
        discord.Recorded.ShouldHaveRequestedOnce($"POST /channels/{general.Id}/typing");
        Assert.Equal(HttpStatusCode.NoContent, discord.Recorded.Requests.Single().StatusCode);
    }

    [Fact]
    [Parity("POST /channels/{channel_id}/typing")]
    public async Task TriggerTypingAsync_CalledTwice_PublishesAFactEachTime()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        await rest.TriggerTypingAsync(general.Id, cancellationToken: Ct);
        await rest.TriggerTypingAsync(general.Id, cancellationToken: Ct);

        Assert.Equal(2, Drain(subscription).OfType<TypingStarted>().Count());
    }

    [Fact]
    [Parity("POST /channels/{channel_id}/typing")]
    public async Task TriggerTypingAsync_DirectMessageChannel_IsAcceptedWithoutAGuild()
    {
        await using var discord = FakeDiscord.Create();
        var dm = OpenDm(discord, discord.Seed.User("alice"));
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        await rest.TriggerTypingAsync(dm, cancellationToken: Ct);

        var typing = Assert.IsType<TypingStarted>(Assert.Single(Drain(subscription)));
        Assert.Equal((null, dm, null), (typing.GuildId, typing.ChannelId, typing.Member));
    }

    [Fact]
    [Parity("POST /channels/{channel_id}/typing")]
    public async Task TriggerTypingAsync_VoiceChannel_IsAccepted()
    {
        await using var discord = FakeDiscord.Create();
        var lounge = discord.Seed.Guild("Test").VoiceChannel("lounge");
        using var rest = discord.CreateRestClient();

        await rest.TriggerTypingAsync(lounge.Id, cancellationToken: Ct);

        Assert.Equal(HttpStatusCode.NoContent, discord.Recorded.Requests.Single().StatusCode);
    }

    [Fact]
    [Parity("POST /channels/{channel_id}/typing")]
    public async Task TriggerTypingAsync_UnknownChannelOrNotMember_FailsWithTheCatalogErrors()
    {
        await using var discord = FakeDiscord.Create();
        var secret = discord.Seed.Guild("Elsewhere", withBot: false).TextChannel("secret");
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        var unknown = await Assert.ThrowsAsync<RestException>(() => rest.TriggerTypingAsync(123456789, cancellationToken: Ct));
        var forbidden = await Assert.ThrowsAsync<RestException>(() => rest.TriggerTypingAsync(secret.Id, cancellationToken: Ct));

        AssertError(unknown, HttpStatusCode.NotFound, 10003);
        AssertError(forbidden, HttpStatusCode.Forbidden, 50001);
        Assert.Empty(Drain(subscription));
    }

    [Fact]
    [Parity("POST /channels/{channel_id}/typing")]
    public async Task TriggerTypingAsync_CategoryChannel_Returns50024()
    {
        await using var discord = FakeDiscord.Create();
        var lobby = discord.Seed.Guild("Test").Category("Lobby");
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.TriggerTypingAsync(lobby.Id, cancellationToken: Ct));

        AssertError(exception, HttpStatusCode.BadRequest, 50024);
    }

    [Fact]
    [Parity("POST /channels/{channel_id}/typing")]
    public async Task TriggerTypingAsync_MalformedChannelId_Fails50035()
    {
        await using var discord = FakeDiscord.Create();
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(async () =>
        {
            await using var stream = await rest.SendRequestAsync(HttpMethod.Post, $"/channels/{"abc"}/typing", cancellationToken: Ct);
        });

        AssertError(exception, HttpStatusCode.BadRequest, 50035);
        Assert.Equal("NUMBER_TYPE_COERCE", Assert.Single(FieldErrors(exception, "channel_id")).Code);
    }
}
