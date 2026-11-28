using System.Net;
using System.Text;
using Enx.NetCord.Testing.State;
using NetCord;
using NetCord.Rest;
using Xunit;
using static Enx.NetCord.Testing.Tests.Rest.Messages.MessagesTestSupport;
using static Enx.NetCord.Testing.Tests.State.StateTestSupport;

namespace Enx.NetCord.Testing.Tests.Rest.Messages;

public sealed class SendMessageTests
{
    [Fact]
    [Parity("POST /channels/{channel_id}/messages")]
    public async Task SendMessageAsync_Content_ReturnsTheMessageAndItIsReadBack()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();

        var message = await rest.SendMessageAsync(general.Id, "hello", cancellationToken: Ct);

        Assert.Equal("hello", message.Content);
        Assert.Equal(general.Id, message.ChannelId);
        Assert.Equal(discord.ApplicationId, message.Author.Id);
        Assert.True(message.Author.IsBot);
        Assert.Null(message.EditedAt);
        Assert.Equal(MessageType.Default, message.Type);
        Assert.Empty(message.Embeds);
        Assert.Empty(message.Attachments);
        var read = await rest.GetMessageAsync(general.Id, message.Id, cancellationToken: Ct);
        Assert.Equal("hello", read.Content);
        Assert.Equal(message.Id, Assert.IsType<TextGuildChannel>(await rest.GetChannelAsync(general.Id, cancellationToken: Ct)).LastMessageId);
        discord.Recorded.ShouldHaveRequestedOnce($"POST /channels/{general.Id}/messages");
    }

    [Fact]
    [Parity("POST /channels/{channel_id}/messages")]
    public async Task SendMessageAsync_Message_PublishesOneMessageCreatedWithAuthorAndMembership()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        var message = await rest.SendMessageAsync(general.Id, "hi", cancellationToken: Ct);

        var created = Assert.IsType<MessageCreated>(Assert.Single(Drain(subscription)));
        Assert.Equal(message.Id, created.Message.Id);
        Assert.Equal("hi", created.Message.Content);
        Assert.Equal(discord.ApplicationId, created.Author.Id);
        Assert.Equal(guild.Id, created.AuthorMember!.GuildId);
    }

    [Fact]
    [Parity("POST /channels/{channel_id}/messages")]
    public async Task SendMessageAsync_DirectMessageChannel_AcceptsTheMessage()
    {
        await using var discord = FakeDiscord.Create();
        var aliceId = discord.Seed.User("alice");
        using var rest = discord.CreateRestClient();
        var dm = await rest.GetDMChannelAsync(aliceId, cancellationToken: Ct);

        var message = await rest.SendMessageAsync(dm.Id, "psst", cancellationToken: Ct);

        Assert.Equal("psst", (await rest.GetMessageAsync(dm.Id, message.Id, cancellationToken: Ct)).Content);
        Assert.Equal(message.Id, (await rest.GetDMChannelAsync(aliceId, cancellationToken: Ct)).LastMessageId);
    }

    [Fact]
    [Parity("POST /channels/{channel_id}/messages")]
    public async Task SendMessageAsync_Embeds_AreReturnedAsRichEmbeds()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();

        var message = await rest.SendMessageAsync(
            general.Id,
            new MessageProperties
            {
                Embeds =
                [
                    new EmbedProperties
                    {
                        Title = "Title",
                        Description = "Body",
                        Url = "https://example.test/page",
                        Color = new Color(0x336699),
                        Timestamp = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero),
                        Footer = new EmbedFooterProperties { Text = "footer", IconUrl = "https://example.test/f.png" },
                        Author = new EmbedAuthorProperties { Name = "someone" },
                        Image = "https://example.test/i.png",
                        Fields = [new EmbedFieldProperties { Name = "n", Value = "v", Inline = true }],
                    },
                ],
            },
            cancellationToken: Ct);

        var embed = Assert.Single(message.Embeds);
        Assert.Equal(EmbedType.Rich, embed.Type);
        Assert.Equal("Title", embed.Title);
        Assert.Equal("Body", embed.Description);
        Assert.Equal("https://example.test/page", embed.Url);
        Assert.Equal(0x336699, embed.Color!.Value.RawValue);
        Assert.Equal(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero), embed.Timestamp);
        Assert.Equal("footer", embed.Footer!.Text);
        Assert.Equal("someone", embed.Author!.Name);
        Assert.Equal("https://example.test/i.png", embed.Image!.Url);
        var field = Assert.Single(embed.Fields);
        Assert.Equal(("n", "v", true), (field.Name, field.Value, field.Inline));
    }

    [Fact]
    [Parity("POST /channels/{channel_id}/messages")]
    public async Task SendMessageAsync_EmbedOnly_IsNotEmpty()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();

        var message = await rest.SendMessageAsync(
            general.Id,
            new MessageProperties { Embeds = [new EmbedProperties { Title = "only" }] },
            cancellationToken: Ct);

        Assert.Equal(string.Empty, message.Content);
        Assert.Equal("only", Assert.Single(message.Embeds).Title);
    }

    [Fact]
    [Parity("POST /channels/{channel_id}/messages")]
    public async Task SendMessageAsync_FlagsAndTts_KeepsTheCreatableBitsOnly()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();

        var message = await rest.SendMessageAsync(
            general.Id,
            new MessageProperties
            {
                Content = "quiet",
                Tts = true,
                Flags = MessageFlags.SuppressEmbeds | MessageFlags.SuppressNotifications | MessageFlags.Crossposted,
            },
            cancellationToken: Ct);

        Assert.Equal(MessageFlags.SuppressEmbeds | MessageFlags.SuppressNotifications, message.Flags);
        Assert.True(message.IsTts);
    }

    [Fact]
    [Parity("POST /channels/{channel_id}/messages")]
    public async Task SendMessageAsync_Mentions_ReportsUsersRolesAndEveryone()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var aliceId = guild.Member("alice");
        var roleId = guild.Role("mods");
        using var rest = discord.CreateRestClient();

        var message = await rest.SendMessageAsync(general.Id, $"<@{aliceId}> <@&{roleId}> <@!{aliceId}> @everyone", cancellationToken: Ct);

        Assert.Equal(aliceId, Assert.Single(message.MentionedUsers).Id);
        Assert.Equal([roleId], message.MentionedRoleIds);
        Assert.True(message.MentionEveryone);
    }

    [Fact]
    [Parity("POST /channels/{channel_id}/messages")]
    public async Task SendMessageAsync_AllowedMentionsNone_MentionsNothing()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var aliceId = guild.Member("alice");
        using var rest = discord.CreateRestClient();

        var message = await rest.SendMessageAsync(
            general.Id,
            new MessageProperties { Content = $"<@{aliceId}> @everyone", AllowedMentions = AllowedMentionsProperties.None },
            cancellationToken: Ct);

        Assert.Empty(message.MentionedUsers);
        Assert.False(message.MentionEveryone);
    }

    [Fact]
    [Parity("POST /channels/{channel_id}/messages")]
    public async Task SendMessageAsync_AllowedMentionsWithAUserList_MentionsOnlyThatUser()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var aliceId = guild.Member("alice");
        var bobId = guild.Member("bob");
        using var rest = discord.CreateRestClient();

        var message = await rest.SendMessageAsync(
            general.Id,
            new MessageProperties
            {
                Content = $"<@{aliceId}> <@{bobId}>",
                AllowedMentions = new AllowedMentionsProperties { AllowedUsers = [bobId], AllowedRoles = [], Everyone = false },
            },
            cancellationToken: Ct);

        Assert.Equal(bobId, Assert.Single(message.MentionedUsers).Id);
    }

    [Fact]
    [Parity("POST /channels/{channel_id}/messages")]
    public async Task SendMessageAsync_Reply_SetsTheReferenceTheTypeAndMentionsTheRepliedAuthor()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var aliceId = guild.Member("alice");
        var original = general.Message("question", aliceId);
        using var rest = discord.CreateRestClient();

        var reply = await rest.SendMessageAsync(
            general.Id,
            new MessageProperties { Content = "answer", MessageReference = MessageReferenceProperties.Reply(original) },
            cancellationToken: Ct);

        Assert.Equal(MessageType.Reply, reply.Type);
        Assert.Equal(original, reply.MessageReference!.MessageId);
        Assert.Equal(general.Id, reply.MessageReference.ChannelId);
        Assert.Equal(guild.Id, reply.MessageReference.GuildId);
        Assert.Equal("question", reply.ReferencedMessage!.Content);
        Assert.Equal(aliceId, Assert.Single(reply.MentionedUsers).Id);
    }

    [Fact]
    [Parity("POST /channels/{channel_id}/messages")]
    public async Task SendMessageAsync_ReplyWithRepliedUserDisallowed_DoesNotMentionTheAuthor()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var original = general.Message("question", guild.Member("alice"));
        using var rest = discord.CreateRestClient();

        var reply = await rest.SendMessageAsync(
            general.Id,
            new MessageProperties
            {
                Content = "answer",
                MessageReference = MessageReferenceProperties.Reply(original),
                AllowedMentions = new AllowedMentionsProperties { ReplyMention = false },
            },
            cancellationToken: Ct);

        Assert.Empty(reply.MentionedUsers);
        Assert.Equal(MessageType.Reply, reply.Type);
    }

    [Fact]
    [Parity("POST /channels/{channel_id}/messages")]
    public async Task SendMessageAsync_ReplyToAnUnknownMessage_Returns50035OnMessageReferenceAndCreatesNothing()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.SendMessageAsync(
            general.Id,
            new MessageProperties { Content = "answer", MessageReference = MessageReferenceProperties.Reply(999) },
            cancellationToken: Ct));

        Assert.Equal("MESSAGE_REFERENCE_UNKNOWN_MESSAGE", Assert.Single(FieldErrors(exception, "message_reference")).Code);
        Assert.Empty(discord.State.ListMessages(StateCaller.Fixture, general.Id, 10).Value);
    }

    [Fact]
    [Parity("POST /channels/{channel_id}/messages")]
    public async Task SendMessageAsync_ReplyToAnUnknownMessageWithoutFailing_SendsANormalMessage()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();

        var message = await rest.SendMessageAsync(
            general.Id,
            new MessageProperties { Content = "answer", MessageReference = MessageReferenceProperties.Reply(999, failIfNotExists: false) },
            cancellationToken: Ct);

        Assert.Equal(MessageType.Default, message.Type);
        Assert.Null(message.MessageReference);
    }

    [Fact]
    [Parity("POST /channels/{channel_id}/messages")]
    public async Task SendMessageAsync_ReplyToAMessageOfAnotherChannel_IsRefused()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var other = guild.TextChannel("other");
        var elsewhere = other.Message("there", guild.Member("alice"));
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.SendMessageAsync(
            general.Id,
            new MessageProperties { Content = "answer", MessageReference = MessageReferenceProperties.Reply(elsewhere) },
            cancellationToken: Ct));

        AssertError(exception, HttpStatusCode.BadRequest, 50035);
    }

    [Fact]
    [Parity("POST /channels/{channel_id}/messages")]
    public async Task SendMessageAsync_NonceWithoutEnforcement_IsEchoedAndDoesNotDeduplicate()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();

        var first = await rest.SendMessageAsync(general.Id, new MessageProperties { Content = "a", Nonce = new NonceProperties("n-1") { Unique = false } }, cancellationToken: Ct);
        var second = await rest.SendMessageAsync(general.Id, new MessageProperties { Content = "a", Nonce = new NonceProperties("n-1") { Unique = false } }, cancellationToken: Ct);

        Assert.Equal("n-1", first.Nonce);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    [Parity("POST /channels/{channel_id}/messages")]
    public async Task SendMessageAsync_EnforcedNonceSentTwice_ReturnsTheFirstMessageAndCreatesOne()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        var first = await rest.SendMessageAsync(general.Id, new MessageProperties { Content = "a", Nonce = "same" }, cancellationToken: Ct);
        var second = await rest.SendMessageAsync(general.Id, new MessageProperties { Content = "b", Nonce = "same" }, cancellationToken: Ct);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal("a", second.Content);
        Assert.Single(Drain(subscription));
    }

    [Fact]
    [Parity("POST /channels/{channel_id}/messages")]
    [Parity("10003")]
    public async Task SendMessageAsync_UnknownChannel_Returns404UnknownChannel()
    {
        await using var discord = FakeDiscord.Create();
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.SendMessageAsync(123456789, "hi", cancellationToken: Ct));

        AssertError(exception, HttpStatusCode.NotFound, 10003);
        Assert.Empty(Drain(subscription));
    }

    [Fact]
    [Parity("POST /channels/{channel_id}/messages")]
    [Parity("50001")]
    public async Task SendMessageAsync_ChannelOfAGuildTheBotIsNotIn_Returns403MissingAccess()
    {
        await using var discord = FakeDiscord.Create();
        var secret = discord.Seed.Guild("Elsewhere", withBot: false).TextChannel("secret");
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.SendMessageAsync(secret.Id, "hi", cancellationToken: Ct));

        AssertError(exception, HttpStatusCode.Forbidden, 50001);
        Assert.Empty(discord.State.ListMessages(StateCaller.Fixture, secret.Id, 10).Value);
    }

    [Fact]
    [Parity("GET /channels/{channel_id}/messages")]
    [Parity("POST /channels/{channel_id}/messages")]
    [Parity("POST /channels/{channel_id}/messages/bulk-delete")]
    [Parity("GET /channels/{channel_id}/messages/{message_id}")]
    [Parity("PATCH /channels/{channel_id}/messages/{message_id}")]
    [Parity("DELETE /channels/{channel_id}/messages/{message_id}")]
    [Parity("POST /channels/{channel_id}/messages/{message_id}/crosspost")]
    [Parity("http:401")]
    public async Task MessageRoutes_WithoutCredentialsOrWithAnUnknownToken_Return401AndChangeNothing()
    {
        using var harness = Users.UsersTestSupport.CreateRealRoutesHarness();
        var guild = harness.Context.State.CreateGuild(new CreateGuildInput("Test", WithBot: true)).Value.Guild;
        var channel = harness.Context.State.CreateGuildChannel(StateCaller.Fixture, new CreateGuildChannelInput(guild.Id, ChannelKind.GuildText, "general")).Value;
        var author = harness.Context.State.CreateUser(new CreateUserInput("alice"));
        var existing = harness.Context.State.CreateMessage(StateCaller.Fixture, new CreateMessageInput(channel.Id, author.Id, "kept")).Value;
        using var anonymous = harness.CreateClient(rawToken: null);
        using var stranger = harness.CreateClient("NDM.other.token");

        foreach (var client in new[] { anonymous, stranger })
        {
            var attempts = new Func<Task>[]
            {
                async () => await client.GetMessagesAroundAsync(channel.Id, existing.Id, cancellationToken: Ct),
                () => client.SendMessageAsync(channel.Id, "x", cancellationToken: Ct),
                () => client.DeleteMessagesAsync(channel.Id, [existing.Id, existing.Id + 1], cancellationToken: Ct),
                () => client.GetMessageAsync(channel.Id, existing.Id, cancellationToken: Ct),
                () => client.ModifyMessageAsync(channel.Id, existing.Id, o => o.Content = "y", cancellationToken: Ct),
                () => client.DeleteMessageAsync(channel.Id, existing.Id, cancellationToken: Ct),
                () => client.CrosspostMessageAsync(channel.Id, existing.Id, cancellationToken: Ct),
            };
            foreach (var attempt in attempts)
            {
                var exception = await Assert.ThrowsAsync<RestException>(attempt);
                AssertError(exception, HttpStatusCode.Unauthorized, 0);
            }
        }

        var stored = harness.Context.State.ListMessages(StateCaller.Fixture, channel.Id, 10).Value;
        Assert.Equal("kept", Assert.Single(stored).Content);
    }

    [Fact]
    [Parity("POST /channels/{channel_id}/messages")]
    [Parity("50006")]
    public async Task SendMessageAsync_NothingToSend_Returns50006AndCreatesNothing()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();

        var exception = await SendRawFailingAsync(rest, HttpMethod.Post, $"/channels/{general.Id}/messages", "{}");

        AssertError(exception, HttpStatusCode.BadRequest, 50006);
        Assert.Empty(discord.State.ListMessages(StateCaller.Fixture, general.Id, 10).Value);
    }

    [Theory]
    [Parity("POST /channels/{channel_id}/messages")]
    [Parity("50006")]
    [InlineData("""{"content":""}""")]
    [InlineData("""{"content":null,"embeds":[]}""")]
    [InlineData("""{"content":null,"embeds":null,"components":[]}""")]
    public async Task SendRequest_EmptyContentVariants_Return50006(string body)
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();

        var exception = await SendRawFailingAsync(rest, HttpMethod.Post, $"/channels/{general.Id}/messages", body);

        AssertError(exception, HttpStatusCode.BadRequest, 50006);
    }

    [Theory]
    [Parity("POST /channels/{channel_id}/messages")]
    [Parity("50035")]
    [InlineData("""{"content":5}""", "content", "BASE_TYPE_BAD_TYPE")]
    [InlineData("""{"content":"hi","tts":"yes"}""", "tts", "BOOLEAN_TYPE_COERCE")]
    [InlineData("""{"content":"hi","embeds":"x"}""", "embeds", "BASE_TYPE_BAD_TYPE")]
    [InlineData("""{"content":"hi","embeds":[5]}""", "embeds.0", "MODEL_TYPE_CONVERT")]
    [InlineData("""{"content":"hi","flags":"x"}""", "flags", "NUMBER_TYPE_COERCE")]
    [InlineData("""{"content":"hi","allowed_mentions":5}""", "allowed_mentions", "MODEL_TYPE_CONVERT")]
    [InlineData("""{"content":"hi","allowed_mentions":{"parse":["bots"]}}""", "allowed_mentions.parse.0", "BASE_TYPE_CHOICES")]
    [InlineData("""{"content":"hi","allowed_mentions":{"parse":["users"],"users":["1"]}}""", "allowed_mentions.users", "MESSAGE_ALLOWED_MENTIONS_PARSE_EXCLUSIVE")]
    [InlineData("""{"content":"hi","allowed_mentions":{"users":["abc"]}}""", "allowed_mentions.users.0", "NUMBER_TYPE_COERCE")]
    [InlineData("""{"content":"hi","message_reference":{}}""", "message_reference.message_id", "BASE_TYPE_REQUIRED")]
    [InlineData("""{"content":"hi","message_reference":{"message_id":"abc"}}""", "message_reference.message_id", "NUMBER_TYPE_COERCE")]
    [InlineData("""{"content":"hi","message_reference":{"message_id":"1","type":1}}""", "message_reference.type", "BASE_TYPE_CHOICES")]
    [InlineData("""{"content":"hi","nonce":true}""", "nonce", "NUMBER_TYPE_COERCE")]
    [InlineData("""{"content":"hi","sticker_ids":["1","2","3","4"]}""", "sticker_ids", "BASE_TYPE_MAX_LENGTH")]
    public async Task SendRequest_InvalidField_Returns50035AtTheFieldPath(string body, string path, string code)
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();

        var exception = await SendRawFailingAsync(rest, HttpMethod.Post, $"/channels/{general.Id}/messages", body);

        Assert.Equal(code, FieldErrors(exception, path.Split('.')).First().Code);
        Assert.Empty(discord.State.ListMessages(StateCaller.Fixture, general.Id, 10).Value);
    }

    [Fact]
    [Parity("POST /channels/{channel_id}/messages")]
    [Parity("50035")]
    public async Task SendMessageAsync_ContentOver2000_Returns50035MaxLengthOnContent()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.SendMessageAsync(general.Id, new string('x', 2001), cancellationToken: Ct));
        var accepted = await rest.SendMessageAsync(general.Id, new string('x', 2000), cancellationToken: Ct);

        var error = Assert.Single(FieldErrors(exception, "content"));
        Assert.Equal("BASE_TYPE_MAX_LENGTH", error.Code);
        Assert.Equal("Must be 2000 or fewer in length.", error.Message);
        Assert.Equal(2000, accepted.Content.Length);
    }

    [Fact]
    [Parity("POST /channels/{channel_id}/messages")]
    [Parity("50035")]
    public async Task SendMessageAsync_EmbedLimits_ReturnOneErrorPerOffendingPath()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();

        var tooMany = await Assert.ThrowsAsync<RestException>(() => rest.SendMessageAsync(
            general.Id,
            new MessageProperties { Embeds = Enumerable.Range(0, 11).Select(i => new EmbedProperties { Title = $"e{i}" }) },
            cancellationToken: Ct));
        var longTitle = await Assert.ThrowsAsync<RestException>(() => rest.SendMessageAsync(
            general.Id,
            new MessageProperties { Embeds = [new EmbedProperties { Title = new string('t', 257), Description = new string('d', 4097) }] },
            cancellationToken: Ct));
        var tooManyFields = await Assert.ThrowsAsync<RestException>(() => rest.SendMessageAsync(
            general.Id,
            new MessageProperties { Embeds = [new EmbedProperties { Fields = Enumerable.Range(0, 26).Select(i => new EmbedFieldProperties { Name = "n", Value = "v" }) }] },
            cancellationToken: Ct));

        Assert.Equal("BASE_TYPE_MAX_LENGTH", Assert.Single(FieldErrors(tooMany, "embeds")).Code);
        Assert.Equal("BASE_TYPE_MAX_LENGTH", Assert.Single(FieldErrors(longTitle, "embeds", "0", "title")).Code);
        Assert.Equal("BASE_TYPE_MAX_LENGTH", Assert.Single(FieldErrors(longTitle, "embeds", "0", "description")).Code);
        Assert.Equal("BASE_TYPE_MAX_LENGTH", Assert.Single(FieldErrors(tooManyFields, "embeds", "0", "fields")).Code);
    }

    [Fact]
    [Parity("POST /channels/{channel_id}/messages")]
    [Parity("50035")]
    public async Task SendMessageAsync_EmbedsOverTheSixThousandCharacterBudget_Return50035OnEmbeds()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();
        EmbedProperties Big() => new() { Description = new string('d', 3500) };

        var exception = await Assert.ThrowsAsync<RestException>(
            () => rest.SendMessageAsync(general.Id, new MessageProperties { Embeds = [Big(), Big()] }, cancellationToken: Ct));

        Assert.Equal("MAX_EMBED_SIZE_EXCEEDED", Assert.Single(FieldErrors(exception, "embeds")).Code);
    }

    [Theory]
    [Parity("POST /channels/{channel_id}/messages")]
    [Parity("50035")]
    [InlineData("""{"embeds":[{"url":"not a url"}]}""", "embeds.0.url", "URL_TYPE_INVALID_URL")]
    [InlineData("""{"embeds":[{"color":16777216}]}""", "embeds.0.color", "NUMBER_TYPE_MAX")]
    [InlineData("""{"embeds":[{"fields":[{"name":"n"}]}]}""", "embeds.0.fields.0.value", "BASE_TYPE_REQUIRED")]
    [InlineData("""{"embeds":[{"fields":[{"name":"","value":"v"}]}]}""", "embeds.0.fields.0.name", "BASE_TYPE_BAD_LENGTH")]
    [InlineData("""{"embeds":[{"footer":{"text":5}}]}""", "embeds.0.footer.text", "BASE_TYPE_BAD_TYPE")]
    [InlineData("""{"embeds":[{"author":"x"}]}""", "embeds.0.author", "MODEL_TYPE_CONVERT")]
    [InlineData("""{"embeds":[{"image":{"url":"ftp://x/y.png"}}]}""", "embeds.0.image.url", "URL_TYPE_INVALID_URL")]
    public async Task SendRequest_InvalidEmbed_Returns50035AtTheNestedPath(string body, string path, string code)
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();

        var exception = await SendRawFailingAsync(rest, HttpMethod.Post, $"/channels/{general.Id}/messages", body);

        Assert.Equal(code, FieldErrors(exception, path.Split('.')).First().Code);
    }

    [Fact]
    [Parity("POST /channels/{channel_id}/messages")]
    [Parity("50035")]
    public async Task SendRequest_InvalidChannelId_Returns50035OnChannelId()
    {
        await using var discord = FakeDiscord.Create();
        using var rest = discord.CreateRestClient();

        var exception = await SendRawFailingAsync(rest, HttpMethod.Post, $"/channels/{"abc"}/messages", """{"content":"x"}""");

        Assert.Equal("NUMBER_TYPE_COERCE", Assert.Single(FieldErrors(exception, "channel_id")).Code);
    }

    [Fact]
    [Parity("POST /channels/{channel_id}/messages")]
    [Parity("50109")]
    public async Task SendRequest_BodyThatIsNotJson_Returns50109()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();
        using var content = new StringContent("{nope", Encoding.UTF8, "application/json");

        var exception = await Assert.ThrowsAsync<RestException>(async () =>
        {
            await using var stream = await rest.SendRequestAsync(HttpMethod.Post, content, $"/channels/{general.Id}/messages", cancellationToken: Ct);
        });

        AssertError(exception, HttpStatusCode.BadRequest, 50109);
    }

    [Fact]
    [Parity("POST /channels/{channel_id}/messages")]
    public async Task SendMessageAsync_Multipart_StoresTheAttachmentsAndReturnsTheirMetadata()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();
        var bytes = "hello, file"u8.ToArray();

        var message = await rest.SendMessageAsync(
            general.Id,
            new MessageProperties
            {
                Content = "with a file",
                Attachments =
                [
                    new AttachmentProperties("hello.txt", new MemoryStream(bytes)) { Description = "a greeting" },
                    new AttachmentProperties("second.bin", new MemoryStream(new byte[300])),
                ],
            },
            cancellationToken: Ct);

        Assert.Equal("with a file", message.Content);
        Assert.Equal(2, message.Attachments.Count);
        var first = message.Attachments[0];
        Assert.Equal("hello.txt", first.FileName);
        Assert.Equal(bytes.Length, first.Size);
        Assert.Equal("a greeting", first.Description);
        Assert.Equal($"https://cdn.discordapp.com/attachments/{general.Id}/{first.Id}/hello.txt", first.Url);
        Assert.StartsWith("https://media.discordapp.net/attachments/", first.ProxyUrl, StringComparison.Ordinal);
        Assert.Equal(300, message.Attachments[1].Size);
        Assert.NotEqual(first.Id, message.Attachments[1].Id);
        var read = await rest.GetMessageAsync(general.Id, message.Id, cancellationToken: Ct);
        Assert.Equal(["hello.txt", "second.bin"], read.Attachments.Select(a => a.FileName));
    }

    [Fact]
    [Parity("POST /channels/{channel_id}/messages")]
    public async Task SendMessageAsync_MultipartWithoutContent_IsNotEmpty()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();

        var message = await rest.SendMessageAsync(
            general.Id,
            new MessageProperties { Attachments = [new AttachmentProperties("only.txt", new MemoryStream([1, 2, 3]))] },
            cancellationToken: Ct);

        Assert.Equal(string.Empty, message.Content);
        Assert.Equal(3, Assert.Single(message.Attachments).Size);
    }

    [Fact]
    [Parity("POST /channels/{channel_id}/messages")]
    public async Task SendMessageAsync_MultipartWithANonAsciiFileName_KeepsTheName()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();

        var message = await rest.SendMessageAsync(
            general.Id,
            new MessageProperties { Attachments = [new AttachmentProperties("é à.txt", new MemoryStream([1]))] },
            cancellationToken: Ct);

        Assert.Equal("é à.txt", Assert.Single(message.Attachments).FileName);
    }

    [Fact]
    [Parity("POST /channels/{channel_id}/messages")]
    public async Task SendRequest_MultipartWithNonContiguousFileIndexes_PairsEachFileWithItsMetadata()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();
        using var content = new MultipartFormDataContent
        {
            { new StringContent("""{"content":"gaps","attachments":[{"id":5,"filename":"five.txt","description":"the sixth"}]}""", Encoding.UTF8, "application/json"), "payload_json" },
            { new ByteArrayContent([1]), "files[0]", "zero.txt" },
            { new ByteArrayContent([1, 2, 3, 4, 5]), "files[5]", "ignored-name.txt" },
        };

        using var json = await SendMultipartAsync(rest, content, general.Id);

        var attachments = json.RootElement.GetProperty("attachments").EnumerateArray().ToList();
        Assert.Equal(["zero.txt", "five.txt"], attachments.Select(a => a.GetProperty("filename").GetString()));
        Assert.Equal(5, attachments[1].GetProperty("size").GetInt32());
        Assert.Equal("the sixth", attachments[1].GetProperty("description").GetString());
        Assert.False(attachments[0].TryGetProperty("description", out _));
    }

    private static async Task<System.Text.Json.JsonDocument> SendMultipartAsync(RestClient rest, HttpContent content, ulong channelId)
    {
        await using var stream = await rest.SendRequestAsync(HttpMethod.Post, content, $"/channels/{channelId}/messages", cancellationToken: Ct);
        return await System.Text.Json.JsonDocument.ParseAsync(stream, cancellationToken: Ct);
    }

    [Fact]
    [Parity("POST /channels/{channel_id}/messages")]
    public async Task SendMessageAsync_Multipart_JournalKeepsThePayloadJsonWithoutTheFileBytes()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();

        await rest.SendMessageAsync(
            general.Id,
            new MessageProperties { Content = "c", Attachments = [new AttachmentProperties("f.txt", new MemoryStream([9, 9]))] },
            cancellationToken: Ct);

        var recorded = Assert.Single(discord.Recorded.Requests);
        Assert.Equal("c", recorded.RequestBody!.Value.GetProperty("content").GetString());
        Assert.Equal(HttpStatusCode.OK, recorded.StatusCode);
    }

    [Fact]
    [Parity("POST /channels/{channel_id}/messages")]
    [Parity("50035")]
    public async Task SendMessageAsync_ElevenFiles_Returns50035OnAttachments()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(() => rest.SendMessageAsync(
            general.Id,
            new MessageProperties
            {
                Content = "many",
                Attachments = Enumerable.Range(0, 11).Select(i => new AttachmentProperties($"f{i}.txt", new MemoryStream([1]))).ToArray(),
            },
            cancellationToken: Ct));

        Assert.Equal("BASE_TYPE_MAX_LENGTH", Assert.Single(FieldErrors(exception, "attachments")).Code);
        Assert.Empty(discord.State.ListMessages(StateCaller.Fixture, general.Id, 10).Value);
    }
}
