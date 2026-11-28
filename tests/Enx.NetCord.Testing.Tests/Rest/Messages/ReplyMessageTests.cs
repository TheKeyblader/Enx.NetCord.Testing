using System.Text.Json;
using NetCord;
using NetCord.Rest;
using Xunit;
using static Enx.NetCord.Testing.Tests.Rest.Messages.MessagesTestSupport;

namespace Enx.NetCord.Testing.Tests.Rest.Messages;

public sealed class ReplyMessageTests
{
    private static async Task<(FakeDiscord Discord, RestClient Client, ulong ChannelId, ulong AuthorId, ulong OriginalId, ulong ReplyId)> ReplyAsync()
    {
        var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var aliceId = guild.Member("alice");
        var original = general.Message("question", aliceId);
        var rest = discord.CreateRestClient();
        var reply = await rest.SendMessageAsync(
            general.Id,
            new MessageProperties { Content = "answer", MessageReference = MessageReferenceProperties.Reply(original) },
            cancellationToken: Ct);
        return (discord, rest, general.Id, aliceId, original, reply.Id);
    }

    [Fact]
    [Parity("GET /channels/{channel_id}/messages/{message_id}")]
    [Parity("GET /channels/{channel_id}/messages")]
    public async Task GetMessage_ReplyWhoseTargetWasDeleted_WritesAnExplicitNullReferencedMessage()
    {
        var (discord, rest, channelId, _, originalId, replyId) = await ReplyAsync();
        await using var disposableDiscord = discord;
        using var disposableRest = rest;
        await rest.DeleteMessageAsync(channelId, originalId, cancellationToken: Ct);

        using var single = await GetRawAsync(rest, $"/channels/{channelId}/messages/{replyId}");
        using var list = await GetRawAsync(rest, $"/channels/{channelId}/messages");

        Assert.True(single.RootElement.TryGetProperty("referenced_message", out var referenced));
        Assert.Equal(JsonValueKind.Null, referenced.ValueKind);
        Assert.Equal(originalId.ToString(), single.RootElement.GetProperty("message_reference").GetProperty("message_id").GetString());
        var item = Assert.Single(list.RootElement.EnumerateArray());
        Assert.True(item.TryGetProperty("referenced_message", out var listed));
        Assert.Equal(JsonValueKind.Null, listed.ValueKind);
        Assert.Null((await rest.GetMessageAsync(channelId, replyId, cancellationToken: Ct)).ReferencedMessage);
    }

    [Fact]
    [Parity("GET /channels/{channel_id}/messages/{message_id}")]
    public async Task GetMessage_ReplyWhoseTargetExists_WritesTheReferencedMessageAndAPlainMessageOmitsIt()
    {
        var (discord, rest, channelId, _, originalId, replyId) = await ReplyAsync();
        await using var disposableDiscord = discord;
        using var disposableRest = rest;

        using var reply = await GetRawAsync(rest, $"/channels/{channelId}/messages/{replyId}");
        using var original = await GetRawAsync(rest, $"/channels/{channelId}/messages/{originalId}");

        Assert.Equal("question", reply.RootElement.GetProperty("referenced_message").GetProperty("content").GetString());
        Assert.False(original.RootElement.TryGetProperty("referenced_message", out _));
        Assert.False(original.RootElement.TryGetProperty("message_reference", out _));
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}/messages/{message_id}")]
    public async Task ModifyMessageAsync_ContentOfAReply_KeepsMentioningTheRepliedAuthor()
    {
        var (discord, rest, channelId, aliceId, _, replyId) = await ReplyAsync();
        await using var disposableDiscord = discord;
        using var disposableRest = rest;

        var edited = await rest.ModifyMessageAsync(channelId, replyId, o => o.Content = "better answer", cancellationToken: Ct);

        Assert.Equal(aliceId, Assert.Single(edited.MentionedUsers).Id);
        Assert.Equal(MessageType.Reply, edited.Type);
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}/messages/{message_id}")]
    public async Task ModifyMessageAsync_ContentOfAReplyWithRepliedUserDisallowed_DoesNotMentionTheAuthor()
    {
        var (discord, rest, channelId, _, _, replyId) = await ReplyAsync();
        await using var disposableDiscord = discord;
        using var disposableRest = rest;

        var edited = await rest.ModifyMessageAsync(
            channelId,
            replyId,
            o =>
            {
                o.Content = "better answer";
                o.AllowedMentions = new AllowedMentionsProperties { ReplyMention = false };
            },
            cancellationToken: Ct);

        Assert.Empty(edited.MentionedUsers);
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}/messages/{message_id}")]
    public async Task ModifyMessageAsync_ContentOfAReplyWhoseTargetWasDeleted_MentionsNoOne()
    {
        var (discord, rest, channelId, _, originalId, replyId) = await ReplyAsync();
        await using var disposableDiscord = discord;
        using var disposableRest = rest;
        await rest.DeleteMessageAsync(channelId, originalId, cancellationToken: Ct);

        var edited = await rest.ModifyMessageAsync(channelId, replyId, o => o.Content = "still here", cancellationToken: Ct);

        Assert.Empty(edited.MentionedUsers);
    }
}
