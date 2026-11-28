using System.Net;
using Enx.NetCord.Testing.Conformance.Contract;
using NetCord.Rest;
using Xunit;

namespace Enx.NetCord.Testing.Conformance.Rest;

/// <summary>Pin and typing routes driven through the real NetCord <see cref="RestClient"/>, with every response checked against the OpenAPI contract.</summary>
[Trait("Category", "Conformance")]
public sealed class PinsConformanceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    [Parity("PUT /channels/{channel_id}/messages/pins/{message_id}")]
    public async Task PinMessageAsync_Message_Returns204AndConformsToTheContract()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var message = general.Message("pin me", guild.Member("alice"));
        using var rest = discord.CreateRestClient();

        await rest.PinMessageAsync(general.Id, message, cancellationToken: Ct);

        Assert.True((await rest.GetMessageAsync(general.Id, message, cancellationToken: Ct)).IsPinned);
        ContractAssert.Conforms(discord, "PUT /channels/{channel_id}/messages/pins/{message_id}");
    }

    [Fact]
    [Parity("PUT /channels/{channel_id}/messages/pins/{message_id}")]
    [Parity("10008")]
    public async Task PinMessageAsync_UnknownMessage_Returns10008WithTheDiscordErrorEnvelope()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.PinMessageAsync(general.Id, 424242, cancellationToken: Ct));

        Assert.Equal((HttpStatusCode.NotFound, 10008), (exception.StatusCode, exception.Error!.Code));
        ContractAssert.Conforms(discord, "PUT /channels/{channel_id}/messages/pins/{message_id}");
    }

    [Fact]
    [Parity("DELETE /channels/{channel_id}/messages/pins/{message_id}")]
    public async Task UnpinMessageAsync_PinnedMessage_Returns204AndConformsToTheContract()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var message = general.Message("pin me", guild.Member("alice"));
        using var rest = discord.CreateRestClient();
        await rest.PinMessageAsync(general.Id, message, cancellationToken: Ct);

        await rest.UnpinMessageAsync(general.Id, message, cancellationToken: Ct);

        Assert.False((await rest.GetMessageAsync(general.Id, message, cancellationToken: Ct)).IsPinned);
        ContractAssert.Conforms(discord, "DELETE /channels/{channel_id}/messages/pins/{message_id}");
    }

    [Fact]
    [Parity("GET /channels/{channel_id}/messages/pins")]
    public async Task GetChannelPinsAsync_PinnedMessages_ReturnsThemAndConformsToPinnedMessagesResponse()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var alice = guild.Member("alice");
        var first = general.Message("first", alice);
        var second = general.Message("second", alice);
        using var rest = discord.CreateRestClient();
        await rest.PinMessageAsync(general.Id, first, cancellationToken: Ct);
        await rest.PinMessageAsync(general.Id, second, cancellationToken: Ct);

        var pins = await rest.GetChannelPinsAsync(general.Id, cancellationToken: Ct).ToListAsync(Ct);

        Assert.Equal([second, first], pins.Select(pin => pin.Message.Id));
        ContractAssert.Conforms(discord, "GET /channels/{channel_id}/messages/pins");
    }

    [Fact]
    [Parity("GET /channels/{channel_id}/messages/pins")]
    [Parity("10003")]
    public async Task GetChannelPinsAsync_UnknownChannel_Returns10003WithTheDiscordErrorEnvelope()
    {
        await using var discord = FakeDiscord.Create();
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.GetChannelPinsAsync(123456789, cancellationToken: Ct).ToListAsync(Ct).AsTask());

        Assert.Equal((HttpStatusCode.NotFound, 10003), (exception.StatusCode, exception.Error!.Code));
        ContractAssert.Conforms(discord, "GET /channels/{channel_id}/messages/pins");
    }

    [Fact]
    [Parity("POST /channels/{channel_id}/typing")]
    public async Task TriggerTypingAsync_GuildChannel_Returns204AndConformsToTheContract()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();

        await rest.TriggerTypingAsync(general.Id, cancellationToken: Ct);

        ContractAssert.Conforms(discord, "POST /channels/{channel_id}/typing");
    }

    [Fact]
    [Parity("POST /channels/{channel_id}/typing")]
    [Parity("10003")]
    public async Task TriggerTypingAsync_UnknownChannel_Returns10003WithTheDiscordErrorEnvelope()
    {
        await using var discord = FakeDiscord.Create();
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.TriggerTypingAsync(123456789, cancellationToken: Ct));

        Assert.Equal((HttpStatusCode.NotFound, 10003), (exception.StatusCode, exception.Error!.Code));
        ContractAssert.Conforms(discord, "POST /channels/{channel_id}/typing");
    }
}
