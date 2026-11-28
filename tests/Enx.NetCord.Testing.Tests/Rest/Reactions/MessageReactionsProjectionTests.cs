using System.Text.Json;
using NetCord.Rest;
using Xunit;
using static Enx.NetCord.Testing.Tests.Rest.Messages.MessagesTestSupport;

namespace Enx.NetCord.Testing.Tests.Rest.Reactions;

/// <summary>The <c>reactions</c> of the messages the message routes return (the shape is the one of Discord's reaction object).</summary>
public sealed class MessageReactionsProjectionTests
{
    [Fact]
    [Parity("GET /channels/{channel_id}/messages/{message_id}")]
    public async Task GetMessageAsync_NoReaction_OmitsTheReactionsProperty()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var message = general.Message("plain", guild.Member("alice"));
        using var rest = discord.CreateRestClient();

        using var document = await GetRawAsync(rest, $"/channels/{general.Id}/messages/{message}");

        Assert.False(document.RootElement.TryGetProperty("reactions", out _));
        Assert.Empty((await rest.GetMessageAsync(general.Id, message, cancellationToken: Ct)).Reactions);
    }

    [Fact]
    [Parity("GET /channels/{channel_id}/messages/{message_id}")]
    public async Task GetMessageAsync_NormalAndBurstReactions_CarriesCountsDetailsAndMeFlags()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var alice = guild.Member("alice");
        var bob = guild.Member("bob");
        var message = general.Message("hello", alice);
        general.Reaction(message, "👍", alice);
        general.Reaction(message, "👍", bob, burst: true);
        general.Reaction(message, "wave:123456789012345678", bob);
        using var rest = discord.CreateRestClient();
        await rest.AddMessageReactionAsync(general.Id, message, "👍", cancellationToken: Ct);

        using var document = await GetRawAsync(rest, $"/channels/{general.Id}/messages/{message}");

        var reactions = document.RootElement.GetProperty("reactions");
        Assert.Equal(2, reactions.GetArrayLength());
        var thumbs = reactions[0];
        Assert.Equal(JsonValueKind.Null, thumbs.GetProperty("emoji").GetProperty("id").ValueKind);
        Assert.Equal("👍", thumbs.GetProperty("emoji").GetProperty("name").GetString());
        Assert.Equal(3, thumbs.GetProperty("count").GetInt32());
        Assert.Equal(2, thumbs.GetProperty("count_details").GetProperty("normal").GetInt32());
        Assert.Equal(1, thumbs.GetProperty("count_details").GetProperty("burst").GetInt32());
        Assert.True(thumbs.GetProperty("me").GetBoolean());
        Assert.False(thumbs.GetProperty("me_burst").GetBoolean());
        Assert.Equal(0, thumbs.GetProperty("burst_colors").GetArrayLength());
        var wave = reactions[1];
        Assert.Equal("123456789012345678", wave.GetProperty("emoji").GetProperty("id").GetString());
        Assert.Equal("wave", wave.GetProperty("emoji").GetProperty("name").GetString());
        Assert.False(wave.GetProperty("me").GetBoolean());
    }

    [Fact]
    [Parity("GET /channels/{channel_id}/messages")]
    public async Task GetMessagesAsync_ReactedMessage_ListsItsReactionsInTheOrderOfTheFirstReaction()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var alice = guild.Member("alice");
        var message = general.Message("hello", alice);
        general.Reaction(message, "🔥", alice);
        general.Reaction(message, "👍", alice);
        general.Reaction(message, "🔥", guild.Member("bob"));
        using var rest = discord.CreateRestClient();

        var listed = Assert.Single(await rest.GetMessagesAsync(general.Id).ToListAsync(Ct));

        Assert.Equal(["🔥", "👍"], listed.Reactions.Select(r => r.Emoji.Name));
        Assert.Equal([2, 1], listed.Reactions.Select(r => r.Count));
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}/messages/{message_id}")]
    public async Task ModifyMessageAsync_ReactedMessage_KeepsItsReactions()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();
        var message = await rest.SendMessageAsync(general.Id, "before", cancellationToken: Ct);
        await rest.AddMessageReactionAsync(general.Id, message.Id, "👍", cancellationToken: Ct);

        var edited = await rest.ModifyMessageAsync(general.Id, message.Id, o => o.Content = "after", cancellationToken: Ct);

        Assert.Equal("after", edited.Content);
        Assert.Equal("👍", Assert.Single(edited.Reactions).Emoji.Name);
    }

    [Fact]
    [Parity("DELETE /channels/{channel_id}/messages/{message_id}")]
    public async Task DeleteMessageAsync_ReactedMessage_TakesItsReactionsAway()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();
        var message = await rest.SendMessageAsync(general.Id, "bye", cancellationToken: Ct);
        await rest.AddMessageReactionAsync(general.Id, message.Id, "👍", cancellationToken: Ct);

        await rest.DeleteMessageAsync(general.Id, message.Id, cancellationToken: Ct);

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.GetMessageReactionsAsync(general.Id, message.Id, "👍").ToListAsync(Ct).AsTask());
        AssertError(exception, System.Net.HttpStatusCode.NotFound, 10008);
    }
}
