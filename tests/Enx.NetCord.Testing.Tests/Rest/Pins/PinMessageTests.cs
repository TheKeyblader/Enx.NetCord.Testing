using System.Net;
using Enx.NetCord.Testing.State;
using Microsoft.Extensions.Time.Testing;
using NetCord.Rest;
using Xunit;
using static Enx.NetCord.Testing.Tests.Rest.Messages.MessagesTestSupport;
using static Enx.NetCord.Testing.Tests.Rest.Pins.PinsTestSupport;
using static Enx.NetCord.Testing.Tests.State.StateTestSupport;

namespace Enx.NetCord.Testing.Tests.Rest.Pins;

public sealed class PinMessageTests
{
    private static readonly DateTimeOffset Now = VirtualTimeProvider.DefaultNow;

    [Fact]
    [Parity("PUT /channels/{channel_id}/messages/pins/{message_id}")]
    [Parity("http:204")]
    public async Task PinMessageAsync_Message_MarksItPinnedAndPublishesTheFacts()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var alice = guild.Member("alice");
        var message = general.Message("remember this", alice);
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        await rest.PinMessageAsync(general.Id, message, cancellationToken: Ct);

        Assert.True((await rest.GetMessageAsync(general.Id, message, cancellationToken: Ct)).IsPinned);
        var changes = Drain(subscription);
        Assert.Equal(3, changes.Count);
        var edited = Assert.IsType<MessageEdited>(changes[0]);
        Assert.Equal((message, true, null), (edited.Message.Id, edited.Message.Pinned, edited.Message.EditedTimestamp));
        var pins = Assert.IsType<ChannelPinsUpdated>(changes[1]);
        Assert.Equal((guild.Id, general.Id, Now), (pins.GuildId, pins.ChannelId, pins.LastPinTimestamp));
        var system = Assert.IsType<MessageCreated>(changes[2]).Message;
        Assert.Equal((6, discord.ApplicationId, string.Empty), (system.Type, system.AuthorId, system.Content));
        Assert.Equal(new MessageReferenceValue(message, general.Id, guild.Id), system.Reference);
        Assert.Equal(system.Id, discord.State.FindChannel(general.Id)?.LastMessageId);
        discord.Recorded.ShouldHaveRequestedOnce($"PUT /channels/{general.Id}/messages/pins/{message}");
        Assert.Equal(HttpStatusCode.NoContent, discord.Recorded.Requests.First(r => r.Method == "PUT").StatusCode);
    }

    [Fact]
    [Parity("PUT /channels/{channel_id}/messages/pins/{message_id}")]
    public async Task PinMessageAsync_Message_SetsTheLastPinTimestampOfTheChannel()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var message = general.Message("pin me", guild.Member("alice"));
        using var rest = discord.CreateRestClient();

        await rest.PinMessageAsync(general.Id, message, cancellationToken: Ct);

        using var channel = await GetRawAsync(rest, $"/channels/{general.Id}");
        Assert.Equal(Now, channel.RootElement.GetProperty("last_pin_timestamp").GetDateTimeOffset());
    }

    [Fact]
    [Parity("PUT /channels/{channel_id}/messages/pins/{message_id}")]
    public async Task PinMessageAsync_AlreadyPinned_Returns204WithoutAnyChange()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var message = general.Message("pin me", guild.Member("alice"));
        using var rest = discord.CreateRestClient();
        await rest.PinMessageAsync(general.Id, message, cancellationToken: Ct);
        var lastMessageId = discord.State.FindChannel(general.Id)?.LastMessageId;
        using var subscription = discord.State.SubscribeGateway();

        await rest.PinMessageAsync(general.Id, message, cancellationToken: Ct);

        Assert.Empty(Drain(subscription));
        Assert.Equal(lastMessageId, discord.State.FindChannel(general.Id)?.LastMessageId);
        Assert.All(discord.Recorded.Requests.Where(r => r.Method == "PUT"), r => Assert.Equal(HttpStatusCode.NoContent, r.StatusCode));
    }

    [Fact]
    [Parity("PUT /channels/{channel_id}/messages/pins/{message_id}")]
    public async Task PinMessageAsync_SeveralMessagesWithAFrozenClock_GetStrictlyIncreasingPinTimes()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var alice = guild.Member("alice");
        var first = general.Message("one", alice);
        var second = general.Message("two", alice);
        using var rest = discord.CreateRestClient();

        await rest.PinMessageAsync(general.Id, first, cancellationToken: Ct);
        await rest.PinMessageAsync(general.Id, second, cancellationToken: Ct);

        var pins = await rest.GetChannelPinsAsync(general.Id, cancellationToken: Ct).ToListAsync(Ct);
        Assert.Equal([second, first], pins.Select(pin => pin.Message.Id));
        Assert.Equal([Now.AddSeconds(1), Now], pins.Select(pin => pin.PinnedAt));
    }

    [Fact]
    [Parity("PUT /channels/{channel_id}/messages/pins/{message_id}")]
    public async Task PinMessageAsync_ClockAdvanced_UsesTheClockSecond()
    {
        var clock = new FakeTimeProvider(Start);
        await using var discord = FakeDiscord.Create(new FakeDiscordOptions { TimeProvider = clock });
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var message = general.Message("pin me", guild.Member("alice"));
        using var rest = discord.CreateRestClient();
        clock.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromMilliseconds(700));

        await rest.PinMessageAsync(general.Id, message, cancellationToken: Ct);

        var pin = Assert.Single(await rest.GetChannelPinsAsync(general.Id, cancellationToken: Ct).ToListAsync(Ct));
        Assert.Equal(Start.AddMinutes(5), pin.PinnedAt);
    }

    [Fact]
    [Parity("PUT /channels/{channel_id}/messages/pins/{message_id}")]
    public async Task PinMessageAsync_DirectMessageChannel_IsAccepted()
    {
        await using var discord = FakeDiscord.Create();
        var alice = discord.Seed.User("alice");
        var dm = OpenDm(discord, alice);
        var message = discord.State.CreateMessage(StateCaller.Fixture, new CreateMessageInput(dm, alice, "hi bot")).Value.Id;
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        await rest.PinMessageAsync(dm, message, cancellationToken: Ct);

        var pin = Assert.Single(await rest.GetChannelPinsAsync(dm, cancellationToken: Ct).ToListAsync(Ct));
        Assert.Equal(message, pin.Message.Id);
        var pins = Assert.Single(Drain(subscription).OfType<ChannelPinsUpdated>());
        Assert.Equal((null, dm), (pins.GuildId, pins.ChannelId));
    }

    [Fact]
    [Parity("PUT /channels/{channel_id}/messages/pins/{message_id}")]
    [Parity("30003")]
    public async Task PinMessageAsync_ChannelAlreadyHas250Pins_Returns30003AndChangesNothing()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var alice = guild.Member("alice");
        for (var i = 0; i < FakeDiscordState.MaxPinsPerChannel; i++)
        {
            SeedPinned(discord, general.Id, alice, $"pinned {i}");
        }

        var extra = general.Message("one too many", alice);
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.PinMessageAsync(general.Id, extra, cancellationToken: Ct));

        AssertError(exception, HttpStatusCode.BadRequest, 30003);
        Assert.False((await rest.GetMessageAsync(general.Id, extra, cancellationToken: Ct)).IsPinned);
        Assert.Empty(Drain(subscription));
    }

    [Fact]
    [Parity("PUT /channels/{channel_id}/messages/pins/{message_id}")]
    public async Task PinMessageAsync_AlreadyPinnedWhileChannelIsFull_StillSucceeds()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var alice = guild.Member("alice");
        var first = SeedPinned(discord, general.Id, alice, "first");
        for (var i = 1; i < FakeDiscordState.MaxPinsPerChannel; i++)
        {
            SeedPinned(discord, general.Id, alice, $"pinned {i}");
        }

        using var rest = discord.CreateRestClient();

        await rest.PinMessageAsync(general.Id, first, cancellationToken: Ct);
    }

    [Fact]
    [Parity("PUT /channels/{channel_id}/messages/pins/{message_id}")]
    public async Task PinMessageAsync_UnknownChannelMessageOrNotMember_FailsWithTheCatalogErrors()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        var secret = discord.Seed.Guild("Elsewhere", withBot: false).TextChannel("secret");
        var alice = discord.Seed.User("alice");
        var hidden = secret.Message("hidden", alice);
        var elsewhere = general.Message("in general", alice);
        using var rest = discord.CreateRestClient();

        var unknownChannel = await Assert.ThrowsAsync<RestException>(() => rest.PinMessageAsync(123456789, 1, cancellationToken: Ct));
        var unknownMessage = await Assert.ThrowsAsync<RestException>(() => rest.PinMessageAsync(general.Id, 424242, cancellationToken: Ct));
        var forbidden = await Assert.ThrowsAsync<RestException>(() => rest.PinMessageAsync(secret.Id, hidden, cancellationToken: Ct));
        var foreignMessageInHiddenChannel = await Assert.ThrowsAsync<RestException>(() => rest.PinMessageAsync(secret.Id, elsewhere, cancellationToken: Ct));

        AssertError(unknownChannel, HttpStatusCode.NotFound, 10003);
        AssertError(unknownMessage, HttpStatusCode.NotFound, 10008);
        AssertError(forbidden, HttpStatusCode.Forbidden, 50001);
        AssertError(foreignMessageInHiddenChannel, HttpStatusCode.Forbidden, 50001);
        Assert.False(discord.State.FindMessage(secret.Id, hidden)!.Pinned);
    }

    [Fact]
    [Parity("PUT /channels/{channel_id}/messages/pins/{message_id}")]
    [Parity("50021")]
    public async Task PinMessageAsync_SystemMessage_Returns50021AndChangesNothing()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var message = general.Message("pin me", guild.Member("alice"));
        using var rest = discord.CreateRestClient();
        await rest.PinMessageAsync(general.Id, message, cancellationToken: Ct);
        var systemMessage = discord.State.FindChannel(general.Id)!.LastMessageId!.Value;
        Assert.Equal(6, discord.State.FindMessage(general.Id, systemMessage)!.Type);
        using var subscription = discord.State.SubscribeGateway();

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.PinMessageAsync(general.Id, systemMessage, cancellationToken: Ct));

        AssertError(exception, HttpStatusCode.BadRequest, 50021);
        Assert.False(discord.State.FindMessage(general.Id, systemMessage)!.Pinned);
        Assert.Empty(Drain(subscription));
    }

    [Fact]
    [Parity("PUT /channels/{channel_id}/messages/pins/{message_id}")]
    public async Task PinMessageAsync_Reply_IsAccepted()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var original = general.Message("question", guild.Member("alice"));
        using var rest = discord.CreateRestClient();
        var reply = await rest.SendMessageAsync(general.Id, new MessageProperties { Content = "answer", MessageReference = MessageReferenceProperties.Reply(original) }, cancellationToken: Ct);

        await rest.PinMessageAsync(general.Id, reply.Id, cancellationToken: Ct);

        Assert.True((await rest.GetMessageAsync(general.Id, reply.Id, cancellationToken: Ct)).IsPinned);
    }

    [Fact]
    [Parity("PUT /channels/{channel_id}/messages/pins/{message_id}")]
    public async Task PinMessageAsync_MessageOfAnotherChannel_Returns10008()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var random = guild.TextChannel("random");
        var message = random.Message("elsewhere", guild.Member("alice"));
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.PinMessageAsync(general.Id, message, cancellationToken: Ct));

        AssertError(exception, HttpStatusCode.NotFound, 10008);
    }

    [Fact]
    [Parity("PUT /channels/{channel_id}/messages/pins/{message_id}")]
    public async Task PinMessageAsync_CategoryChannel_Returns10008BecauseNoMessageExistsThere()
    {
        await using var discord = FakeDiscord.Create();
        var lobby = discord.Seed.Guild("Test").Category("Lobby");
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.PinMessageAsync(lobby.Id, 424242, cancellationToken: Ct));

        AssertError(exception, HttpStatusCode.NotFound, 10008);
    }

    [Fact]
    [Parity("PUT /channels/{channel_id}/messages/pins/{message_id}")]
    public async Task PinMessageAsync_MalformedIds_Fails50035OnEachPathParameter()
    {
        await using var discord = FakeDiscord.Create();
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(async () =>
        {
            await using var stream = await rest.SendRequestAsync(HttpMethod.Put, $"/channels/{"abc"}/messages/pins/{"def"}", cancellationToken: Ct);
        });

        AssertError(exception, HttpStatusCode.BadRequest, 50035);
        Assert.Equal("NUMBER_TYPE_COERCE", Assert.Single(FieldErrors(exception, "channel_id")).Code);
        Assert.Equal("NUMBER_TYPE_COERCE", Assert.Single(FieldErrors(exception, "message_id")).Code);
    }

    [Fact]
    [Parity("DELETE /channels/{channel_id}/messages/{message_id}")]
    [Parity("GET /channels/{channel_id}/messages/pins")]
    public async Task DeleteMessageAsync_PinnedMessage_LeavesThePinsWithoutAChannelPinsUpdate()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var alice = guild.Member("alice");
        var doomed = SeedPinned(discord, general.Id, alice, "doomed");
        var kept = SeedPinned(discord, general.Id, alice, "kept");
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        await rest.DeleteMessageAsync(general.Id, doomed, cancellationToken: Ct);

        Assert.Equal([kept], (await rest.GetChannelPinsAsync(general.Id, cancellationToken: Ct).ToListAsync(Ct)).Select(pin => pin.Message.Id));
        Assert.Empty(Drain(subscription).OfType<ChannelPinsUpdated>());
    }

    [Fact]
    [Parity("GET /channels/{channel_id}/messages/{message_id}")]
    public async Task GetMessageAsync_SeededPinnedMessage_ReportsItPinnedAndSetsTheLastPinTimestamp()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var message = SeedPinned(discord, general.Id, guild.Member("alice"), "already pinned");
        using var rest = discord.CreateRestClient();

        Assert.True((await rest.GetMessageAsync(general.Id, message, cancellationToken: Ct)).IsPinned);
        Assert.Equal(Now, discord.State.FindChannel(general.Id)?.LastPinTimestamp);
    }
}
