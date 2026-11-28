using System.Net;
using System.Text.Json;
using Enx.NetCord.Testing.State;
using NetCord;
using NetCord.Rest;
using Xunit;
using static Enx.NetCord.Testing.Tests.Rest.Channels.ChannelsTestSupport;
using static Enx.NetCord.Testing.Tests.Rest.Users.UsersTestSupport;
using static Enx.NetCord.Testing.Tests.State.StateTestSupport;

namespace Enx.NetCord.Testing.Tests.Rest.Channels;

public sealed class ModifyChannelTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    [Parity("PATCH /channels/{channel_id}")]
    public async Task ModifyGuildChannelAsync_AllFields_ReturnsTheUpdatedChannelAndIsReadBack()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();

        var updated = await rest.ModifyGuildChannelAsync(
            general.Id,
            o =>
            {
                o.Name = "announcements";
                o.Topic = "Read me";
                o.Nsfw = true;
                o.Slowmode = 30;
                o.Position = 3;
            },
            cancellationToken: Ct);

        var text = Assert.IsType<TextGuildChannel>(updated);
        Assert.Equal("announcements", text.Name);
        Assert.Equal("Read me", text.Topic);
        Assert.True(text.Nsfw);
        Assert.Equal(30, text.Slowmode);
        Assert.Equal(3, text.Position);
        var read = Assert.IsType<TextGuildChannel>(await rest.GetChannelAsync(general.Id, cancellationToken: Ct));
        Assert.Equal("announcements", read.Name);
        Assert.Equal("Read me", read.Topic);
        Assert.True(read.Nsfw);
        Assert.Equal(30, read.Slowmode);
        Assert.Equal(3, read.Position);
        var stored = discord.State.FindChannel(general.Id)!;
        Assert.Equal(("announcements", "Read me", true, 30, 3), (stored.Name, stored.Topic, stored.Nsfw, stored.RateLimitPerUser, stored.Position));
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}")]
    public async Task ModifyGuildChannelAsync_OneField_LeavesTheOthersUntouched()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();
        await rest.ModifyGuildChannelAsync(general.Id, o => { o.Topic = "kept"; o.Slowmode = 5; }, cancellationToken: Ct);

        var updated = Assert.IsType<TextGuildChannel>(
            await rest.ModifyGuildChannelAsync(general.Id, o => o.Name = "renamed", cancellationToken: Ct));

        Assert.Equal("renamed", updated.Name);
        Assert.Equal("kept", updated.Topic);
        Assert.Equal(5, updated.Slowmode);
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}")]
    public async Task ModifyGuildChannelAsync_Update_PublishesOneChannelUpdatedWithTheNewState()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        await rest.ModifyGuildChannelAsync(general.Id, o => o.Name = "renamed", cancellationToken: Ct);

        var change = Assert.IsType<ChannelUpdated>(Assert.Single(Drain(subscription)));
        Assert.Equal(general.Id, change.Channel.Id);
        Assert.Equal("renamed", change.Channel.Name);
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}")]
    public async Task ModifyGuildChannelAsync_ValuesAlreadySet_CommitsNoChange()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        var unchanged = await rest.ModifyGuildChannelAsync(general.Id, o => { o.Name = "general"; o.Nsfw = false; }, cancellationToken: Ct);
        var empty = await rest.ModifyGuildChannelAsync(general.Id, _ => { }, cancellationToken: Ct);

        Assert.Equal("general", Assert.IsType<TextGuildChannel>(unchanged).Name);
        Assert.Equal("general", Assert.IsType<TextGuildChannel>(empty).Name);
        Assert.Empty(Drain(subscription));
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}")]
    public async Task SendRequest_PatchWithANullTopic_ClearsTheTopic()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();
        await rest.ModifyGuildChannelAsync(general.Id, o => o.Topic = "temporary", cancellationToken: Ct);

        using var json = await ReadJsonAsync(
            rest.SendRequestAsync(HttpMethod.Patch, Json("""{"topic":null}"""), $"/channels/{general.Id}", cancellationToken: Ct),
            Ct);

        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("topic").ValueKind);
        Assert.Null(discord.State.FindChannel(general.Id)!.Topic);
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}")]
    public async Task SendRequest_PatchWithTypeZeroAndFieldsOfOtherChannelKinds_IsAcceptedAndIgnored()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();

        using var json = await ReadJsonAsync(
            rest.SendRequestAsync(
                HttpMethod.Patch,
                Json("""{"type":0,"bitrate":64000,"user_limit":5,"parent_id":null,"rate_limit_per_user":"12","name":"kept"}"""),
                $"/channels/{general.Id}",
                cancellationToken: Ct),
            Ct);

        Assert.Equal("kept", json.RootElement.GetProperty("name").GetString());
        Assert.Equal(12, json.RootElement.GetProperty("rate_limit_per_user").GetInt32()); // a numeric string is coerced, as Discord does
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}")]
    public async Task ModifyGuildChannelAsync_DmChannel_ReturnsItUnchanged()
    {
        await using var discord = FakeDiscord.Create();
        var aliceId = discord.Seed.User("alice");
        using var rest = discord.CreateRestClient();
        var dm = await rest.GetDMChannelAsync(aliceId, cancellationToken: Ct);
        using var subscription = discord.State.SubscribeGateway();

        var result = await rest.ModifyGuildChannelAsync(dm.Id, o => o.Name = "ignored", cancellationToken: Ct);

        Assert.Equal(dm.Id, Assert.IsType<DMChannel>(result).Id);
        Assert.Null(discord.State.FindChannel(dm.Id)!.Name);
        Assert.Empty(Drain(subscription));
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}")]
    public async Task SendRequest_PatchWithAnEmptyOrNullNameOnADm_IsAcceptedAndLeavesTheDmUnchanged()
    {
        await using var discord = FakeDiscord.Create();
        var aliceId = discord.Seed.User("alice");
        using var rest = discord.CreateRestClient();
        var dm = await rest.GetDMChannelAsync(aliceId, cancellationToken: Ct); // UpdateDMRequestPartial: name is 0-100 and nullable

        using var empty = await ReadJsonAsync(
            rest.SendRequestAsync(HttpMethod.Patch, Json("""{"name":""}"""), $"/channels/{dm.Id}", cancellationToken: Ct),
            Ct);
        using var cleared = await ReadJsonAsync(
            rest.SendRequestAsync(HttpMethod.Patch, Json("""{"name":null}"""), $"/channels/{dm.Id}", cancellationToken: Ct),
            Ct);

        Assert.Equal(Raw(dm.Id), empty.RootElement.GetProperty("id").GetString());
        Assert.Equal(Raw(dm.Id), cleared.RootElement.GetProperty("id").GetString());
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}")]
    public async Task SendRequest_PatchWithANullName_LeavesTheGuildChannelName()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();

        using var json = await ReadJsonAsync(
            rest.SendRequestAsync(HttpMethod.Patch, Json("""{"name":null}"""), $"/channels/{general.Id}", cancellationToken: Ct),
            Ct);

        Assert.Equal("general", json.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}")]
    [Parity("50035")]
    public async Task SendRequest_PatchWithAnInvalidBodyOnAnUnknownChannel_Returns50035()
    {
        // ADR 0005: structural validation (step 3) comes before the state lookup, so the body is judged before the channel exists.
        await using var discord = FakeDiscord.Create();
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(
            () => rest.SendRequestAsync(HttpMethod.Patch, Json("""{"nsfw":"x"}"""), $"/channels/123456789", cancellationToken: Ct));

        Assert.Equal("BOOLEAN_TYPE_COERCE", Assert.Single(FieldErrors(exception, "nsfw")).Code);
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}")]
    [Parity("50035")]
    public async Task SendRequest_PatchWithAnInvalidBodyOnAChannelOfAGuildWithoutTheBot_Returns50035Not50001()
    {
        await using var discord = FakeDiscord.Create();
        var hidden = discord.Seed.Guild("Elsewhere", withBot: false).TextChannel("secret");
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(
            () => rest.SendRequestAsync(HttpMethod.Patch, Json("""{"nsfw":"x"}"""), $"/channels/{hidden.Id}", cancellationToken: Ct));

        Assert.Equal("BOOLEAN_TYPE_COERCE", Assert.Single(FieldErrors(exception, "nsfw")).Code);
    }

    [Theory]
    [Parity("PATCH /channels/{channel_id}")]
    [Parity("50035")]
    [InlineData("""{"name":""}""", "name", "BASE_TYPE_BAD_LENGTH", "Must be between 1 and 100 in length.")]
    [InlineData("""{"name":"%NAME101%"}""", "name", "BASE_TYPE_BAD_LENGTH", "Must be between 1 and 100 in length.")]
    [InlineData("""{"name":5}""", "name", "BASE_TYPE_BAD_TYPE", "Value must be of type string.")]
    [InlineData("""{"topic":"%TOPIC1025%"}""", "topic", "BASE_TYPE_BAD_LENGTH", "Must be between 0 and 1024 in length.")]
    [InlineData("""{"topic":5}""", "topic", "BASE_TYPE_BAD_TYPE", "Value must be of type string.")]
    [InlineData("""{"nsfw":"yes"}""", "nsfw", "BOOLEAN_TYPE_COERCE", "Value \"yes\" is not boolean.")]
    [InlineData("""{"nsfw":1}""", "nsfw", "BOOLEAN_TYPE_COERCE", "Value \"1\" is not boolean.")]
    [InlineData("""{"rate_limit_per_user":21601}""", "rate_limit_per_user", "NUMBER_TYPE_MAX", "int value should be less than or equal to 21600.")]
    [InlineData("""{"rate_limit_per_user":-1}""", "rate_limit_per_user", "NUMBER_TYPE_MIN", "int value should be greater than or equal to 0.")]
    [InlineData("""{"rate_limit_per_user":1.5}""", "rate_limit_per_user", "NUMBER_TYPE_COERCE", "Value \"1.5\" is not int.")]
    [InlineData("""{"rate_limit_per_user":"abc"}""", "rate_limit_per_user", "NUMBER_TYPE_COERCE", "Value \"abc\" is not int.")]
    [InlineData("""{"rate_limit_per_user":true}""", "rate_limit_per_user", "NUMBER_TYPE_COERCE", "Value \"true\" is not int.")]
    [InlineData("""{"position":-1}""", "position", "NUMBER_TYPE_MIN", "int value should be greater than or equal to 0.")]
    [InlineData("""{"type":99}""", "type", "BASE_TYPE_CHOICES", "Value must be one of (0, 2, 4, 5, 13, 14, 15).")]
    [InlineData("""{"parent_id":"abc"}""", "parent_id", "NUMBER_TYPE_COERCE", "Value \"abc\" is not snowflake.")]
    public async Task SendRequest_PatchWithAnInvalidField_Returns50035AndChangesNothing(string body, string field, string code, string message)
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();
        body = body.Replace("%NAME101%", new string('n', 101), StringComparison.Ordinal)
            .Replace("%TOPIC1025%", new string('t', 1025), StringComparison.Ordinal);

        var exception = await Assert.ThrowsAsync<RestException>(
            () => rest.SendRequestAsync(HttpMethod.Patch, Json(body), $"/channels/{general.Id}", cancellationToken: Ct));

        var error = Assert.Single(FieldErrors(exception, field));
        Assert.Equal(code, error.Code);
        Assert.Equal(message, error.Message);
        Assert.Equal("general", discord.State.FindChannel(general.Id)!.Name);
        Assert.Empty(Drain(subscription));
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}")]
    [Parity("50035")]
    public async Task SendRequest_PatchWithSeveralInvalidFields_ReportsEachOfThem()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(
            () => rest.SendRequestAsync(
                HttpMethod.Patch,
                Json("""{"name":5,"nsfw":"x","rate_limit_per_user":-5}"""),
                $"/channels/{general.Id}",
                cancellationToken: Ct));

        Assert.Equal("BASE_TYPE_BAD_TYPE", Assert.Single(FieldErrors(exception, "name")).Code);
        Assert.Equal("BOOLEAN_TYPE_COERCE", Assert.Single(FieldErrors(exception, "nsfw")).Code);
        Assert.Equal("NUMBER_TYPE_MIN", Assert.Single(FieldErrors(exception, "rate_limit_per_user")).Code);
    }

    [Theory]
    [Parity("PATCH /channels/{channel_id}")]
    [InlineData("""{"name":"a"}""", 1)]
    [InlineData("""{"name":"%NAME100%"}""", 100)]
    public async Task SendRequest_PatchWithANameAtTheBounds_IsAccepted(string body, int length)
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();

        using var json = await ReadJsonAsync(
            rest.SendRequestAsync(
                HttpMethod.Patch,
                Json(body.Replace("%NAME100%", new string('n', 100), StringComparison.Ordinal)),
                $"/channels/{general.Id}",
                cancellationToken: Ct),
            Ct);

        Assert.Equal(length, json.RootElement.GetProperty("name").GetString()!.Length);
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}")]
    [Parity("50035")]
    public async Task ModifyGuildChannelAsync_ConversionToAnotherType_Returns50035OnType()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(
            () => rest.ModifyGuildChannelAsync(
                general.Id,
                o =>
                {
                    o.Name = "renamed";
                    o.ChannelType = ChannelType.AnnouncementGuildChannel;
                },
                cancellationToken: Ct));

        Assert.Equal("CHANNEL_TYPE_INVALID", Assert.Single(FieldErrors(exception, "type")).Code);
        var stored = discord.State.FindChannel(general.Id)!;
        Assert.Equal(ChannelKind.GuildText, stored.Kind);
        Assert.Equal("general", stored.Name); // the refusal is decided before any field is written
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}")]
    [Parity("50035")]
    public async Task ModifyGuildChannelAsync_UnknownParentCategory_Returns50035OnParentId()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(
            () => rest.ModifyGuildChannelAsync(general.Id, o => o.ParentId = 999, cancellationToken: Ct));

        Assert.Equal("CHANNEL_PARENT_INVALID", Assert.Single(FieldErrors(exception, "parent_id")).Code);
    }

    [Theory]
    [Parity("PATCH /channels/{channel_id}")]
    [Parity("50035")]
    [InlineData("[]")]
    [InlineData("\"text\"")]
    public async Task SendRequest_PatchWithANonObjectBody_Returns50035ModelTypeConvert(string body)
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(
            () => rest.SendRequestAsync(HttpMethod.Patch, Json(body), $"/channels/{general.Id}", cancellationToken: Ct));

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Equal(50035, exception.Error!.Code);
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}")]
    [Parity("50109")]
    public async Task SendRequest_PatchWithMalformedJson_Returns50109()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(
            () => rest.SendRequestAsync(HttpMethod.Patch, Json("{"), $"/channels/{general.Id}", cancellationToken: Ct));

        AssertError(exception, HttpStatusCode.BadRequest, 50109);
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}")]
    [Parity("50035")]
    public async Task SendRequest_PatchWithANonSnowflakeId_Returns50035OnChannelId()
    {
        await using var discord = FakeDiscord.Create();
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(
            () => rest.SendRequestAsync(HttpMethod.Patch, Json("{}"), $"/channels/abc", cancellationToken: Ct));

        Assert.Equal("NUMBER_TYPE_COERCE", Assert.Single(FieldErrors(exception, "channel_id")).Code);
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}")]
    [Parity("10003")]
    public async Task ModifyGuildChannelAsync_UnknownChannel_Returns404UnknownChannel()
    {
        await using var discord = FakeDiscord.Create();
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(
            () => rest.ModifyGuildChannelAsync(123456789, o => o.Name = "x", cancellationToken: Ct));

        AssertError(exception, HttpStatusCode.NotFound, 10003);
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}")]
    [Parity("50001")]
    public async Task ModifyGuildChannelAsync_ChannelOfAGuildWithoutTheBot_Returns403AndChangesNothing()
    {
        await using var discord = FakeDiscord.Create();
        var hidden = discord.Seed.Guild("Elsewhere", withBot: false).TextChannel("secret");
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        var exception = await Assert.ThrowsAsync<RestException>(
            () => rest.ModifyGuildChannelAsync(hidden.Id, o => o.Name = "renamed", cancellationToken: Ct));

        AssertError(exception, HttpStatusCode.Forbidden, 50001);
        Assert.Equal("secret", discord.State.FindChannel(hidden.Id)!.Name);
        Assert.Empty(Drain(subscription));
    }
}
