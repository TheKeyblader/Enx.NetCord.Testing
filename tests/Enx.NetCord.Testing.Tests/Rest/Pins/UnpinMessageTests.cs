using System.Net;
using Enx.NetCord.Testing.State;
using NetCord.Rest;
using Xunit;
using static Enx.NetCord.Testing.Tests.Rest.Messages.MessagesTestSupport;
using static Enx.NetCord.Testing.Tests.Rest.Pins.PinsTestSupport;
using static Enx.NetCord.Testing.Tests.State.StateTestSupport;

namespace Enx.NetCord.Testing.Tests.Rest.Pins;

public sealed class UnpinMessageTests
{
    private static readonly DateTimeOffset Now = VirtualTimeProvider.DefaultNow;

    [Fact]
    [Parity("DELETE /channels/{channel_id}/messages/pins/{message_id}")]
    [Parity("http:204")]
    public async Task UnpinMessageAsync_PinnedMessage_ClearsTheFlagAndPublishesTheFacts()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var message = general.Message("pin me", guild.Member("alice"));
        using var rest = discord.CreateRestClient();
        await rest.PinMessageAsync(general.Id, message, cancellationToken: Ct);
        using var subscription = discord.State.SubscribeGateway();

        await rest.UnpinMessageAsync(general.Id, message, cancellationToken: Ct);

        Assert.False((await rest.GetMessageAsync(general.Id, message, cancellationToken: Ct)).IsPinned);
        Assert.Empty(await rest.GetChannelPinsAsync(general.Id, cancellationToken: Ct).ToListAsync(Ct));
        var changes = Drain(subscription);
        Assert.Equal(2, changes.Count);
        var edited = Assert.IsType<MessageEdited>(changes[0]);
        Assert.Equal((message, false), (edited.Message.Id, edited.Message.Pinned));
        var pins = Assert.IsType<ChannelPinsUpdated>(changes[1]);
        Assert.Equal((guild.Id, general.Id, null), (pins.GuildId, pins.ChannelId, pins.LastPinTimestamp));
        discord.Recorded.ShouldHaveRequestedOnce($"DELETE /channels/{general.Id}/messages/pins/{message}");
        Assert.Equal(HttpStatusCode.NoContent, discord.Recorded.Requests.First(r => r.Method == "DELETE").StatusCode);
    }

    [Fact]
    [Parity("DELETE /channels/{channel_id}/messages/pins/{message_id}")]
    public async Task UnpinMessageAsync_NewestOfTwoPins_ReportsTheRemainingOneAsLastPin()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var alice = guild.Member("alice");
        var older = general.Message("older", alice);
        var newer = general.Message("newer", alice);
        using var rest = discord.CreateRestClient();
        await rest.PinMessageAsync(general.Id, older, cancellationToken: Ct);
        await rest.PinMessageAsync(general.Id, newer, cancellationToken: Ct);
        using var subscription = discord.State.SubscribeGateway();

        await rest.UnpinMessageAsync(general.Id, newer, cancellationToken: Ct);

        var pins = Assert.Single(Drain(subscription).OfType<ChannelPinsUpdated>());
        Assert.Equal(Now, pins.LastPinTimestamp);
        Assert.Equal([older], (await rest.GetChannelPinsAsync(general.Id, cancellationToken: Ct).ToListAsync(Ct)).Select(pin => pin.Message.Id));
    }

    [Fact]
    [Parity("DELETE /channels/{channel_id}/messages/pins/{message_id}")]
    public async Task UnpinMessageAsync_NotPinned_Returns204WithoutAnyChange()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var message = general.Message("never pinned", guild.Member("alice"));
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        await rest.UnpinMessageAsync(general.Id, message, cancellationToken: Ct);

        Assert.Empty(Drain(subscription));
        Assert.Equal(HttpStatusCode.NoContent, discord.Recorded.Requests.Single().StatusCode);
    }

    [Fact]
    [Parity("DELETE /channels/{channel_id}/messages/pins/{message_id}")]
    public async Task UnpinMessageAsync_UnknownChannelMessageOrNotMember_FailsWithTheCatalogErrors()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        var secret = discord.Seed.Guild("Elsewhere", withBot: false).TextChannel("secret");
        var hidden = SeedPinned(discord, secret.Id, discord.Seed.User("alice"), "hidden");
        using var rest = discord.CreateRestClient();

        var unknownChannel = await Assert.ThrowsAsync<RestException>(() => rest.UnpinMessageAsync(123456789, 1, cancellationToken: Ct));
        var unknownMessage = await Assert.ThrowsAsync<RestException>(() => rest.UnpinMessageAsync(general.Id, 424242, cancellationToken: Ct));
        var forbidden = await Assert.ThrowsAsync<RestException>(() => rest.UnpinMessageAsync(secret.Id, hidden, cancellationToken: Ct));

        AssertError(unknownChannel, HttpStatusCode.NotFound, 10003);
        AssertError(unknownMessage, HttpStatusCode.NotFound, 10008);
        AssertError(forbidden, HttpStatusCode.Forbidden, 50001);
        Assert.True(discord.State.FindMessage(secret.Id, hidden)!.Pinned);
    }

    [Fact]
    [Parity("DELETE /channels/{channel_id}/messages/pins/{message_id}")]
    public async Task UnpinMessageAsync_MalformedIds_Fails50035OnEachPathParameter()
    {
        await using var discord = FakeDiscord.Create();
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(async () =>
        {
            await using var stream = await rest.SendRequestAsync(HttpMethod.Delete, $"/channels/{"abc"}/messages/pins/{"def"}", cancellationToken: Ct);
        });

        AssertError(exception, HttpStatusCode.BadRequest, 50035);
        Assert.Equal("NUMBER_TYPE_COERCE", Assert.Single(FieldErrors(exception, "channel_id")).Code);
        Assert.Equal("NUMBER_TYPE_COERCE", Assert.Single(FieldErrors(exception, "message_id")).Code);
    }
}
