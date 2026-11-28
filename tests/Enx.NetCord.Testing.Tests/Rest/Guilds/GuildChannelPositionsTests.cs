using System.Net;
using Enx.NetCord.Testing.State;
using NetCord;
using NetCord.Rest;
using Xunit;
using static Enx.NetCord.Testing.Tests.Rest.Guilds.GuildsTestSupport;
using static Enx.NetCord.Testing.Tests.Rest.Users.UsersTestSupport;
using static Enx.NetCord.Testing.Tests.State.StateTestSupport;

namespace Enx.NetCord.Testing.Tests.Rest.Guilds;

/// <summary><c>PATCH /guilds/{guild_id}/channels</c> through the real NetCord <see cref="RestClient"/> (ADR 0013, § 4).</summary>
public sealed class GuildChannelPositionsTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<ChannelSnapshot> ReadAsync(FakeDiscord discord, ulong id) => await Task.FromResult(discord.State.FindChannel(id)!);

    [Fact]
    [Parity("PATCH /guilds/{guild_id}/channels")]
    public async Task ModifyGuildChannelPositionsAsync_Positions_AreStoredWithoutRenumberingTheOthers()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var a = guild.TextChannel("a");
        var b = guild.TextChannel("b");
        var c = guild.TextChannel("c");
        using var rest = discord.CreateRestClient();

        await rest.ModifyGuildChannelPositionsAsync(
            guild.Id,
            [new GuildChannelPositionProperties(c.Id) { Position = 0 }, new GuildChannelPositionProperties(a.Id) { Position = 7 }],
            cancellationToken: Ct);

        var positions = (await rest.GetGuildChannelsAsync(guild.Id, cancellationToken: Ct)).ToDictionary(channel => channel.Id, channel => channel.Position);
        Assert.Equal((7, 1, 0), (positions[a.Id], positions[b.Id], positions[c.Id])); // b keeps its position (no renumbering)
        var attempt = discord.Recorded.Requests.Single(r => r.Method == "PATCH");
        Assert.Equal(HttpStatusCode.NoContent, attempt.StatusCode);
    }

    [Fact]
    [Parity("PATCH /guilds/{guild_id}/channels")]
    public async Task ModifyGuildChannelPositionsAsync_ChangedChannels_PublishOneChannelUpdatedEachInTheOrderOfTheArray()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var a = guild.TextChannel("a");
        var b = guild.TextChannel("b");
        var c = guild.TextChannel("c");
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        await rest.ModifyGuildChannelPositionsAsync(
            guild.Id,
            [
                new GuildChannelPositionProperties(c.Id) { Position = 5 },
                new GuildChannelPositionProperties(b.Id) { Position = 1 }, // already there: no change
                new GuildChannelPositionProperties(a.Id) { Position = 3 },
            ],
            cancellationToken: Ct);

        var changes = Drain(subscription).Cast<ChannelUpdated>().ToList();
        Assert.Equal([c.Id, a.Id], changes.Select(change => change.Channel.Id));
        Assert.Equal([5, 3], changes.Select(change => change.Channel.Position));
    }

    [Fact]
    [Parity("PATCH /guilds/{guild_id}/channels")]
    public async Task ModifyGuildChannelPositionsAsync_ChannelNamedTwice_PublishesOneChangeWithTheFinalState()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var a = guild.TextChannel("a");
        var b = guild.TextChannel("b");
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        await rest.ModifyGuildChannelPositionsAsync(
            guild.Id,
            [new GuildChannelPositionProperties(a.Id) { Position = 4 }, new GuildChannelPositionProperties(b.Id) { Position = 9 }, new GuildChannelPositionProperties(a.Id) { Position = 6 }],
            cancellationToken: Ct);

        var changes = Drain(subscription).Cast<ChannelUpdated>().ToList();
        Assert.Equal([a.Id, b.Id], changes.Select(change => change.Channel.Id));
        Assert.Equal([6, 9], changes.Select(change => change.Channel.Position));
    }

    [Fact]
    [Parity("PATCH /guilds/{guild_id}/channels")]
    public async Task ModifyGuildChannelPositionsAsync_ParentOfOneChannel_MovesItAndKeepsItsPosition()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var archive = guild.Category("Archive");
        var general = guild.TextChannel("general", options: new() { Position = 7 });
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        await rest.ModifyGuildChannelPositionsAsync(
            guild.Id,
            [new GuildChannelPositionProperties(general.Id) { ParentId = archive.Id, LockPermissions = true }],
            cancellationToken: Ct);

        var moved = await ReadAsync(discord, general.Id);
        Assert.Equal((archive.Id, 7), (moved.ParentId, moved.Position));
        Assert.Equal(general.Id, Assert.IsType<ChannelUpdated>(Assert.Single(Drain(subscription))).Channel.Id);
    }

    [Fact]
    [Parity("PATCH /guilds/{guild_id}/channels")]
    public async Task SendRequest_PatchWithNullParent_LeavesTheCategory()
    {
        await using var discord = FakeDiscord.Create();
        var lobby = discord.Seed.Guild("Test").Category("Lobby");
        var general = lobby.TextChannel("general");
        var guildId = discord.State.FindChannel(general.Id)!.GuildId;
        using var rest = discord.CreateRestClient();

        using var response = await rest.SendRequestAsync(
            HttpMethod.Patch,
            Json($$"""[{"id":"{{general.Id}}","parent_id":null}]"""),
            $"/guilds/{guildId}/channels",
            cancellationToken: Ct);

        Assert.Null((await ReadAsync(discord, general.Id)).ParentId);
    }

    [Fact]
    [Parity("PATCH /guilds/{guild_id}/channels")]
    public async Task ModifyGuildChannelPositionsAsync_ParentThatIsAlreadyTheCurrentOne_CountsAsNoParentChangeAndPublishesNothing()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var lobby = guild.Category("Lobby");
        var inside = lobby.TextChannel("inside");
        var other = guild.TextChannel("other");
        var archive = guild.Category("Archive");
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        // `inside` names its current parent (not a change), so `other` is the one channel that moves: the batch is accepted.
        await rest.ModifyGuildChannelPositionsAsync(
            guild.Id,
            [new GuildChannelPositionProperties(inside.Id) { ParentId = lobby.Id }, new GuildChannelPositionProperties(other.Id) { ParentId = archive.Id }],
            cancellationToken: Ct);

        Assert.Equal(other.Id, Assert.IsType<ChannelUpdated>(Assert.Single(Drain(subscription))).Channel.Id);
    }

    [Fact]
    [Parity("PATCH /guilds/{guild_id}/channels")]
    [Parity("40009")]
    public async Task ModifyGuildChannelPositionsAsync_TwoChannelsChangingParent_Returns40009AndChangesNothing()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var archive = guild.Category("Archive");
        var a = guild.TextChannel("a");
        var b = guild.TextChannel("b");
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        var exception = await Assert.ThrowsAsync<RestException>(
            () => rest.ModifyGuildChannelPositionsAsync(
                guild.Id,
                [new GuildChannelPositionProperties(a.Id) { ParentId = archive.Id, Position = 9 }, new GuildChannelPositionProperties(b.Id) { ParentId = archive.Id }],
                cancellationToken: Ct));

        AssertError(exception, HttpStatusCode.BadRequest, 40009);
        Assert.Equal("Only one channel can have a parent_id modified at a time", exception.Error!.Message);
        Assert.Null((await ReadAsync(discord, a.Id)).ParentId);
        Assert.Equal(0, (await ReadAsync(discord, a.Id)).Position);
        Assert.Empty(Drain(subscription));
    }

    [Fact]
    [Parity("PATCH /guilds/{guild_id}/channels")]
    [Parity("50035")]
    public async Task ModifyGuildChannelPositionsAsync_InvalidParent_Returns50035OnParentIdAndChangesNothing()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var rules = guild.TextChannel("rules");
        var general = guild.TextChannel("general");
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        var notCategory = await Assert.ThrowsAsync<RestException>(
            () => rest.ModifyGuildChannelPositionsAsync(
                guild.Id,
                [new GuildChannelPositionProperties(general.Id) { Position = 9, ParentId = rules.Id }],
                cancellationToken: Ct));
        var unknown = await Assert.ThrowsAsync<RestException>(
            () => rest.ModifyGuildChannelPositionsAsync(guild.Id, [new GuildChannelPositionProperties(general.Id) { ParentId = 424242 }], cancellationToken: Ct));

        Assert.Equal("CHANNEL_PARENT_INVALID_TYPE", Assert.Single(FieldErrors(notCategory, "0", "parent_id")).Code); // keyed by the entry's index
        Assert.Equal("CHANNEL_PARENT_INVALID", Assert.Single(FieldErrors(unknown, "0", "parent_id")).Code);
        Assert.Equal(1, (await ReadAsync(discord, general.Id)).Position); // nothing of the refused batch was written
        Assert.Empty(Drain(subscription));
    }

    [Fact]
    [Parity("PATCH /guilds/{guild_id}/channels")]
    [Parity("50035")]
    public async Task ModifyGuildChannelPositionsAsync_InvalidParentOfASecondEntry_IsReportedAtItsIndex()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var a = guild.TextChannel("a");
        var b = guild.TextChannel("b");
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(
            () => rest.ModifyGuildChannelPositionsAsync(
                guild.Id,
                [new GuildChannelPositionProperties(a.Id) { Position = 3 }, new GuildChannelPositionProperties(b.Id) { ParentId = a.Id }],
                cancellationToken: Ct));

        Assert.Equal("CHANNEL_PARENT_INVALID_TYPE", Assert.Single(FieldErrors(exception, "1", "parent_id")).Code);
        Assert.Equal(0, (await ReadAsync(discord, a.Id)).Position); // the whole batch was refused
    }

    [Fact]
    [Parity("PATCH /guilds/{guild_id}/channels")]
    [Parity("10003")]
    public async Task ModifyGuildChannelPositionsAsync_ChannelOfAnotherGuildOrUnknown_Returns10003AndChangesNothing()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var elsewhere = discord.Seed.Guild("Other").TextChannel("elsewhere");
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        var otherGuild = await Assert.ThrowsAsync<RestException>(
            () => rest.ModifyGuildChannelPositionsAsync(
                guild.Id,
                [new GuildChannelPositionProperties(general.Id) { Position = 9 }, new GuildChannelPositionProperties(elsewhere.Id) { Position = 1 }],
                cancellationToken: Ct));
        var unknown = await Assert.ThrowsAsync<RestException>(
            () => rest.ModifyGuildChannelPositionsAsync(guild.Id, [new GuildChannelPositionProperties(424242) { Position = 1 }], cancellationToken: Ct));

        AssertError(otherGuild, HttpStatusCode.NotFound, 10003);
        AssertError(unknown, HttpStatusCode.NotFound, 10003);
        Assert.Equal(0, (await ReadAsync(discord, general.Id)).Position);
        Assert.Empty(Drain(subscription));
    }

    [Fact]
    [Parity("PATCH /guilds/{guild_id}/channels")]
    [Parity("10004")]
    [Parity("50001")]
    public async Task ModifyGuildChannelPositionsAsync_UnknownGuildAndGuildWithoutTheBot_Return10004And50001()
    {
        await using var discord = FakeDiscord.Create();
        var elsewhere = discord.Seed.Guild("Elsewhere", withBot: false);
        var channel = elsewhere.TextChannel("general");
        using var rest = discord.CreateRestClient();

        var unknown = await Assert.ThrowsAsync<RestException>(
            () => rest.ModifyGuildChannelPositionsAsync(424242, [new GuildChannelPositionProperties(channel.Id) { Position = 3 }], cancellationToken: Ct));
        var denied = await Assert.ThrowsAsync<RestException>(
            () => rest.ModifyGuildChannelPositionsAsync(elsewhere.Id, [new GuildChannelPositionProperties(channel.Id) { Position = 3 }], cancellationToken: Ct));

        AssertError(unknown, HttpStatusCode.NotFound, 10004);
        AssertError(denied, HttpStatusCode.Forbidden, 50001);
        Assert.Equal(0, (await ReadAsync(discord, channel.Id)).Position);
    }

    [Fact]
    [Parity("PATCH /guilds/{guild_id}/channels")]
    public async Task ModifyGuildChannelPositionsAsync_EmptyBatch_IsANoOp()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        await rest.ModifyGuildChannelPositionsAsync(guild.Id, [], cancellationToken: Ct);

        Assert.Empty(Drain(subscription));
        Assert.Equal(HttpStatusCode.NoContent, discord.Recorded.Requests[^1].StatusCode);
    }

    [Fact]
    [Parity("PATCH /guilds/{guild_id}/channels")]
    [Parity("50035")]
    public async Task SendRequest_PatchWithInvalidEntries_Returns50035KeyedByArrayIndex()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        using var rest = discord.CreateRestClient();

        var notAnArray = await Assert.ThrowsAsync<RestException>(
            () => rest.SendRequestAsync(HttpMethod.Patch, Json("""{"id":"1"}"""), $"/guilds/{guild.Id}/channels", cancellationToken: Ct));
        var entries = await Assert.ThrowsAsync<RestException>(
            () => rest.SendRequestAsync(
                HttpMethod.Patch,
                Json($$"""[{"id":"{{general.Id}}","position":-1},{"position":2},{"id":"abc"},5,{"id":"{{general.Id}}","lock_permissions":"x"}]"""),
                $"/guilds/{guild.Id}/channels",
                cancellationToken: Ct));

        Assert.Equal(HttpStatusCode.BadRequest, notAnArray.StatusCode);
        Assert.Equal("NUMBER_TYPE_MIN", Assert.Single(FieldErrors(entries, "0", "position")).Code);
        Assert.Equal("BASE_TYPE_REQUIRED", Assert.Single(FieldErrors(entries, "1", "id")).Code);
        Assert.Equal("NUMBER_TYPE_COERCE", Assert.Single(FieldErrors(entries, "2", "id")).Code);
        Assert.Equal("MODEL_TYPE_CONVERT", Assert.Single(FieldErrors(entries, "3")).Code);
        Assert.Equal("BOOLEAN_TYPE_COERCE", Assert.Single(FieldErrors(entries, "4", "lock_permissions")).Code);
        Assert.Equal(0, (await ReadAsync(discord, general.Id)).Position);
    }

    [Fact]
    [Parity("PATCH /guilds/{guild_id}/channels")]
    [Parity("50109")]
    public async Task SendRequest_PatchWithInvalidJson_Returns50109()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(
            () => rest.SendRequestAsync(HttpMethod.Patch, Json("[{"), $"/guilds/{guild.Id}/channels", cancellationToken: Ct));

        AssertError(exception, HttpStatusCode.BadRequest, 50109);
    }
}
