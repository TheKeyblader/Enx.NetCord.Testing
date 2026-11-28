using System.Text.Json;
using Enx.NetCord.Testing.State;
using NetCord;
using NetCord.Rest;
using Xunit;
using static Enx.NetCord.Testing.Tests.Rest.Users.UsersTestSupport;
using static Enx.NetCord.Testing.Tests.State.StateTestSupport;

namespace Enx.NetCord.Testing.Tests.Rest.Channels;

/// <summary><c>PATCH /channels/{channel_id}</c> and <c>parent_id</c>: the three states and the four invariants of ADR 0013, § 2.</summary>
public sealed class ModifyChannelParentTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<ulong?> ReadParentAsync(RestClient rest, ulong channelId)
    {
        using var json = await ReadJsonAsync(rest.SendRequestAsync(HttpMethod.Get, $"/channels/{channelId}", cancellationToken: Ct), Ct);
        var parent = json.RootElement.GetProperty("parent_id");
        return parent.ValueKind == JsonValueKind.Null ? null : ulong.Parse(parent.GetString()!, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string FieldMessage(RestException exception, string field)
        => Assert.Single(FieldErrors(exception, field)).Message;

    [Fact]
    [Parity("PATCH /channels/{channel_id}")]
    public async Task ModifyGuildChannelAsync_ParentCategory_MovesTheChannelAndOnlyItsParent()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var archive = guild.Category("Archive");
        var first = guild.TextChannel("first");
        var general = guild.TextChannel("general", options: new() { Topic = "kept", Position = 7 });
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        var moved = Assert.IsType<TextGuildChannel>(
            await rest.ModifyGuildChannelAsync(general.Id, o => o.ParentId = archive.Id, cancellationToken: Ct));

        Assert.Equal(archive.Id, moved.ParentId);
        Assert.Equal((7, "kept"), (moved.Position, moved.Topic)); // a move changes only the parent (ADR 0013, § 2)
        Assert.Equal(archive.Id, Assert.IsType<TextGuildChannel>(await rest.GetChannelAsync(general.Id, cancellationToken: Ct)).ParentId);
        Assert.Null(Assert.IsType<TextGuildChannel>(await rest.GetChannelAsync(first.Id, cancellationToken: Ct)).ParentId);
        var change = Assert.IsType<ChannelUpdated>(Assert.Single(Drain(subscription)));
        Assert.Equal((general.Id, archive.Id), (change.Channel.Id, change.Channel.ParentId));
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}")]
    public async Task ModifyGuildChannelAsync_ParentInAnotherCategory_MovesThere()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var lobby = guild.Category("Lobby");
        var archive = guild.Category("Archive");
        var general = lobby.TextChannel("general");
        using var rest = discord.CreateRestClient();

        await rest.ModifyGuildChannelAsync(general.Id, o => o.ParentId = archive.Id, cancellationToken: Ct);

        Assert.Equal(archive.Id, await ReadParentAsync(rest, general.Id));
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}")]
    public async Task ModifyGuildChannelAsync_EveryChannelFamily_CanMoveIntoACategory()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var lobby = guild.Category("Lobby");
        var ids = new[] { guild.TextChannel("t").Id, guild.AnnouncementChannel("n").Id, guild.VoiceChannel("v").Id, guild.StageChannel("s").Id, guild.ForumChannel("f").Id };
        using var rest = discord.CreateRestClient();

        foreach (var id in ids)
        {
            await rest.ModifyGuildChannelAsync(id, o => o.ParentId = lobby.Id, cancellationToken: Ct);
        }

        foreach (var id in ids)
        {
            Assert.Equal(lobby.Id, await ReadParentAsync(rest, id));
        }
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}")]
    public async Task SendRequest_PatchWithANullParent_LeavesTheCategory()
    {
        // NetCord never sends null (every property of GuildChannelOptions is WhenWritingNull): the request is built by hand.
        await using var discord = FakeDiscord.Create();
        var lobby = discord.Seed.Guild("Test").Category("Lobby");
        var general = lobby.TextChannel("general");
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        using var json = await ReadJsonAsync(
            rest.SendRequestAsync(HttpMethod.Patch, Json("""{"parent_id":null}"""), $"/channels/{general.Id}", cancellationToken: Ct),
            Ct);

        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("parent_id").ValueKind);
        Assert.Null(await ReadParentAsync(rest, general.Id));
        Assert.Equal(general.Id, Assert.IsType<ChannelUpdated>(Assert.Single(Drain(subscription))).Channel.Id);
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}")]
    public async Task SendRequest_PatchWithAKeyAbsent_LeavesTheParent()
    {
        await using var discord = FakeDiscord.Create();
        var lobby = discord.Seed.Guild("Test").Category("Lobby");
        var general = lobby.TextChannel("general");
        using var rest = discord.CreateRestClient();

        await rest.ModifyGuildChannelAsync(general.Id, o => o.Name = "renamed", cancellationToken: Ct);

        Assert.Equal(lobby.Id, await ReadParentAsync(rest, general.Id));
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}")]
    public async Task ModifyGuildChannelAsync_CurrentParentOrNullWithoutParent_ChangesNothingAndPublishesNothing()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var lobby = guild.Category("Lobby");
        var inside = lobby.TextChannel("inside");
        var outside = guild.TextChannel("outside");
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        await rest.ModifyGuildChannelAsync(inside.Id, o => o.ParentId = lobby.Id, cancellationToken: Ct);
        using var cleared = await ReadJsonAsync(
            rest.SendRequestAsync(HttpMethod.Patch, Json("""{"parent_id":null}"""), $"/channels/{outside.Id}", cancellationToken: Ct),
            Ct);

        Assert.Empty(Drain(subscription));
        Assert.Equal(JsonValueKind.Null, cleared.RootElement.GetProperty("parent_id").ValueKind);
        Assert.Equal(lobby.Id, await ReadParentAsync(rest, inside.Id));
        Assert.Null(await ReadParentAsync(rest, outside.Id));
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}")]
    public async Task ModifyGuildChannelAsync_CurrentParentOfAFullCategory_IsNotRefused()
    {
        await using var discord = FakeDiscord.Create();
        var lobby = discord.Seed.Guild("Test").Category("Lobby");
        var children = Enumerable.Range(0, FakeDiscordState.MaxChannelsPerCategory)
            .Select(i => lobby.TextChannel($"c{i}").Id)
            .ToList();
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        await rest.ModifyGuildChannelAsync(children[0], o => o.ParentId = lobby.Id, cancellationToken: Ct);

        Assert.Empty(Drain(subscription));
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}")]
    public async Task ModifyGuildChannelAsync_ParentAndOtherFields_PublishOneChannelUpdated()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var archive = guild.Category("Archive");
        var general = guild.TextChannel("general");
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        await rest.ModifyGuildChannelAsync(general.Id, o => { o.ParentId = archive.Id; o.Name = "old-general"; o.Topic = "archived"; }, cancellationToken: Ct);

        var change = Assert.IsType<ChannelUpdated>(Assert.Single(Drain(subscription)));
        Assert.Equal((archive.Id, "old-general", "archived"), (change.Channel.ParentId, change.Channel.Name, change.Channel.Topic));
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}")]
    [Parity("50035")]
    public async Task ModifyGuildChannelAsync_ParentThatDoesNotExist_Returns50035AndChangesNothing()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        var exception = await Assert.ThrowsAsync<RestException>(
            () => rest.ModifyGuildChannelAsync(general.Id, o => { o.ParentId = 424242; o.Name = "renamed"; }, cancellationToken: Ct));

        var error = Assert.Single(FieldErrors(exception, "parent_id"));
        Assert.Equal(("CHANNEL_PARENT_INVALID", "Category does not exist"), (error.Code, error.Message));
        Assert.Equal("general", discord.State.FindChannel(general.Id)!.Name);
        Assert.Empty(Drain(subscription));
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}")]
    [Parity("50035")]
    public async Task ModifyGuildChannelAsync_CategoryOfAnotherGuild_Returns50035()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        var foreign = discord.Seed.Guild("Elsewhere").Category("Foreign");
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(
            () => rest.ModifyGuildChannelAsync(general.Id, o => o.ParentId = foreign.Id, cancellationToken: Ct));

        Assert.Equal("CHANNEL_PARENT_INVALID", Assert.Single(FieldErrors(exception, "parent_id")).Code);
        Assert.Null(discord.State.FindChannel(general.Id)!.ParentId);
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}")]
    [Parity("50035")]
    public async Task ModifyGuildChannelAsync_ParentThatIsNotACategory_Returns50035NotACategory()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var general = guild.TextChannel("general");
        var rules = guild.TextChannel("rules");
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(
            () => rest.ModifyGuildChannelAsync(general.Id, o => o.ParentId = rules.Id, cancellationToken: Ct));

        var error = Assert.Single(FieldErrors(exception, "parent_id"));
        Assert.Equal(("CHANNEL_PARENT_INVALID_TYPE", "Not a category"), (error.Code, error.Message));
        Assert.Null(discord.State.FindChannel(general.Id)!.ParentId);
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}")]
    [Parity("50035")]
    public async Task ModifyGuildChannelAsync_CategoryGivenACategoryParent_Returns50035NoSubcategories()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var lobby = guild.Category("Lobby");
        var archive = guild.Category("Archive");
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(
            () => rest.ModifyGuildChannelAsync(archive.Id, o => o.ParentId = lobby.Id, cancellationToken: Ct));

        var error = Assert.Single(FieldErrors(exception, "parent_id"));
        Assert.Equal(("CHANNEL_PARENT_INVALID", "Categories cannot have subcategories"), (error.Code, error.Message));
        Assert.Null(discord.State.FindChannel(archive.Id)!.ParentId);
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}")]
    [Parity("50035")]
    public async Task ModifyGuildChannelAsync_CategoryGivenANonCategoryParent_ChecksTheParentFirst()
    {
        // The invariants are checked in order (ADR 0013, § 2): the parent is not a category before a category cannot have a parent.
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var lobby = guild.Category("Lobby");
        var rules = guild.TextChannel("rules");
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(
            () => rest.ModifyGuildChannelAsync(lobby.Id, o => o.ParentId = rules.Id, cancellationToken: Ct));

        Assert.Equal("CHANNEL_PARENT_INVALID_TYPE", Assert.Single(FieldErrors(exception, "parent_id")).Code);
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}")]
    [Parity("50035")]
    public async Task ModifyGuildChannelAsync_FullCategory_Returns50035MaximumReached()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var lobby = guild.Category("Lobby");
        for (var i = 0; i < FakeDiscordState.MaxChannelsPerCategory; i++)
        {
            lobby.TextChannel($"c{i}");
        }

        var outsider = guild.VoiceChannel("outsider");
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        var exception = await Assert.ThrowsAsync<RestException>(
            () => rest.ModifyGuildChannelAsync(outsider.Id, o => o.ParentId = lobby.Id, cancellationToken: Ct));

        var error = Assert.Single(FieldErrors(exception, "parent_id"));
        Assert.Equal(("CHANNEL_PARENT_MAX_CHANNELS", "Maximum number of channels in category reached (50)"), (error.Code, error.Message));
        Assert.Null(discord.State.FindChannel(outsider.Id)!.ParentId);
        Assert.Empty(Drain(subscription));
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}")]
    [Parity("50035")]
    public async Task ModifyGuildChannelAsync_RefusedParentWithOtherValidFields_WritesNothing()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();

        await Assert.ThrowsAsync<RestException>(
            () => rest.ModifyGuildChannelAsync(general.Id, o => { o.ParentId = 424242; o.Topic = "new"; o.Nsfw = true; o.Position = 9; }, cancellationToken: Ct));

        var stored = discord.State.FindChannel(general.Id)!;
        Assert.Equal((null, false, 0), (stored.Topic, stored.Nsfw, stored.Position));
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}")]
    [Parity("50035")]
    public async Task SendRequest_PatchWithANonSnowflakeParent_Returns50035Coerce()
    {
        await using var discord = FakeDiscord.Create();
        var general = discord.Seed.Guild("Test").TextChannel("general");
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(
            () => rest.SendRequestAsync(HttpMethod.Patch, Json("""{"parent_id":"abc"}"""), $"/channels/{general.Id}", cancellationToken: Ct));

        Assert.Equal("Value \"abc\" is not snowflake.", FieldMessage(exception, "parent_id"));
    }

    [Fact]
    [Parity("PATCH /channels/{channel_id}")]
    public async Task ModifyGuildChannelAsync_DeletedCategoryThenParentedChannel_AcceptsTheNewParent()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var lobby = guild.Category("Lobby");
        var archive = guild.Category("Archive");
        var general = lobby.TextChannel("general");
        using var rest = discord.CreateRestClient();
        await rest.DeleteChannelAsync(lobby.Id, cancellationToken: Ct);

        await rest.ModifyGuildChannelAsync(general.Id, o => o.ParentId = archive.Id, cancellationToken: Ct);

        Assert.Equal(archive.Id, await ReadParentAsync(rest, general.Id));
    }
}
