using System.Net;
using Enx.NetCord.Testing.State;
using NetCord;
using NetCord.Rest;
using Xunit;
using static Enx.NetCord.Testing.Tests.Rest.Guilds.GuildsTestSupport;
using static Enx.NetCord.Testing.Tests.Rest.Users.UsersTestSupport;
using static Enx.NetCord.Testing.Tests.State.StateTestSupport;

namespace Enx.NetCord.Testing.Tests.Rest.Guilds;

/// <summary><c>PATCH /guilds/{guild_id}</c> through the real NetCord <see cref="RestClient"/>.</summary>
public sealed class ModifyGuildTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ImageProperties Image(ImageFormat format, params byte[] data) => new(format, data);

    [Fact]
    [Parity("PATCH /guilds/{guild_id}")]
    public async Task ModifyGuildAsync_NameAndDescription_ReturnsTheGuildAndRereadsIt()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        using var rest = discord.CreateRestClient();

        var updated = await rest.ModifyGuildAsync(guild.Id, o => { o.Name = "Renamed"; o.Description = "About us"; }, cancellationToken: Ct);

        Assert.Equal(("Renamed", "About us"), (updated.Name, updated.Description));
        var read = await rest.GetGuildAsync(guild.Id, cancellationToken: Ct);
        Assert.Equal(("Renamed", "About us"), (read.Name, read.Description));
        discord.Recorded.ShouldHaveRequestedOnce($"PATCH /guilds/{guild.Id}");
    }

    [Fact]
    [Parity("PATCH /guilds/{guild_id}")]
    public async Task ModifyGuildAsync_SettingsOfTheSpec_AreStoredAndReturned()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var afk = guild.VoiceChannel("afk");
        var system = guild.TextChannel("system");
        var rules = guild.AnnouncementChannel("rules");
        using var rest = discord.CreateRestClient();

        var updated = await rest.ModifyGuildAsync(
            guild.Id,
            o =>
            {
                o.VerificationLevel = VerificationLevel.High;
                o.DefaultMessageNotificationLevel = DefaultMessageNotificationLevel.OnlyMentions;
                o.ContentFilter = ContentFilter.AllUsers;
                o.PreferredLocale = "fr";
                o.AfkTimeout = 900;
                o.AfkChannelId = afk.Id;
                o.SystemChannelId = system.Id;
                o.RulesChannelId = rules.Id;
                o.PublicUpdatesChannelId = system.Id;
                o.SafetyAlertsChannelId = system.Id;
                o.SystemChannelFlags = (SystemChannelFlags)3;
                o.PremiumProgressBarEnabled = true;
            },
            cancellationToken: Ct);

        var read = await rest.GetGuildAsync(guild.Id, cancellationToken: Ct);
        foreach (var guildRead in new[] { updated, read })
        {
            Assert.Equal(VerificationLevel.High, guildRead.VerificationLevel);
            Assert.Equal(DefaultMessageNotificationLevel.OnlyMentions, guildRead.DefaultMessageNotificationLevel);
            Assert.Equal(ContentFilter.AllUsers, guildRead.ContentFilter);
            Assert.Equal(("fr", 900), (guildRead.PreferredLocale, guildRead.AfkTimeout));
            Assert.Equal((afk.Id, system.Id, rules.Id), (guildRead.AfkChannelId, guildRead.SystemChannelId, guildRead.RulesChannelId));
            Assert.Equal((system.Id, system.Id), (guildRead.PublicUpdatesChannelId, guildRead.SafetyAlertsChannelId));
            Assert.Equal((SystemChannelFlags)3, guildRead.SystemChannelFlags);
            Assert.True(guildRead.PremiumProgressBarEnabled);
        }
    }

    [Fact]
    [Parity("PATCH /guilds/{guild_id}")]
    public async Task ModifyGuildAsync_ImagesAndEmptyImage_SetThenClearTheHash()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        using var rest = discord.CreateRestClient();

        var updated = await rest.ModifyGuildAsync(
            guild.Id,
            o =>
            {
                o.Icon = Image(ImageFormat.Png, 1, 2, 3);
                o.Splash = Image(ImageFormat.Jpeg, 4);
                o.Banner = Image(ImageFormat.Gif, 5);
            },
            cancellationToken: Ct);

        Assert.Matches("^[0-9a-f]{32}$", updated.IconHash!);
        Assert.Matches("^[0-9a-f]{32}$", updated.SplashHash!);
        Assert.Matches("^a_[0-9a-f]{32}$", updated.BannerHash!);

        var cleared = await rest.ModifyGuildAsync(guild.Id, o => o.Icon = ImageProperties.Empty, cancellationToken: Ct);

        Assert.Null(cleared.IconHash);
        Assert.Equal(updated.SplashHash, cleared.SplashHash); // untouched
    }

    [Fact]
    [Parity("PATCH /guilds/{guild_id}")]
    public async Task ModifyGuildAsync_ChangedValues_PublishOneGuildUpdatedAfterTheChange()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        await rest.ModifyGuildAsync(guild.Id, o => { o.Name = "Renamed"; o.VerificationLevel = VerificationLevel.Low; }, cancellationToken: Ct);

        var change = Assert.IsType<GuildUpdated>(Assert.Single(Drain(subscription)));
        Assert.Equal((guild.Id, "Renamed", 1), (change.Guild.Id, change.Guild.Name, change.Guild.Settings.VerificationLevel));
    }

    [Fact]
    [Parity("PATCH /guilds/{guild_id}")]
    public async Task ModifyGuildAsync_SameValuesOrNothing_ReturnsTheGuildAndPublishesNothing()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        var same = await rest.ModifyGuildAsync(guild.Id, o => { o.Name = "Test"; o.AfkTimeout = 300; }, cancellationToken: Ct);
        var none = await rest.ModifyGuildAsync(guild.Id, _ => { }, cancellationToken: Ct);

        Assert.Equal("Test", same.Name);
        Assert.Equal("Test", none.Name);
        Assert.Empty(Drain(subscription));
    }

    [Fact]
    [Parity("PATCH /guilds/{guild_id}")]
    public async Task ModifyGuildAsync_FeaturesRegionAndUnknownFields_AreValidatedAndIgnored()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var alice = discord.Seed.User("alice");
        using var rest = discord.CreateRestClient();

        var updated = await rest.ModifyGuildAsync(
            guild.Id,
            o =>
            {
                o.Features = ["COMMUNITY"];
                o.OwnerId = alice;
            },
            cancellationToken: Ct);
        var read = await rest.GetGuildAsync(guild.Id, cancellationToken: Ct);

        Assert.Empty(updated.Features);
        Assert.Empty(read.Features);
        Assert.Equal(discord.ApplicationId, read.OwnerId); // owner_id is not in the spec's schema: ignored
    }

    [Fact]
    [Parity("PATCH /guilds/{guild_id}")]
    public async Task SendRequest_PatchWithNullDescriptionAndChannels_ClearsThem()
    {
        // NetCord never sends null (every property of GuildOptions is WhenWritingNull): the request is built by hand.
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var system = guild.TextChannel("system");
        using var rest = discord.CreateRestClient();
        await rest.ModifyGuildAsync(guild.Id, o => { o.Description = "About"; o.SystemChannelId = system.Id; }, cancellationToken: Ct);

        using var json = await ReadJsonAsync(
            rest.SendRequestAsync(
                HttpMethod.Patch,
                Json("""{"description":null,"system_channel_id":null,"icon":null}"""),
                $"/guilds/{guild.Id}",
                cancellationToken: Ct),
            Ct);

        Assert.Equal(System.Text.Json.JsonValueKind.Null, json.RootElement.GetProperty("description").ValueKind);
        var read = await rest.GetGuildAsync(guild.Id, cancellationToken: Ct);
        Assert.Null(read.Description);
        Assert.Null(read.SystemChannelId);
    }

    [Fact]
    [Parity("PATCH /guilds/{guild_id}")]
    [Parity("50035")]
    public async Task ModifyGuildAsync_NameOutOfBounds_Returns50035AndChangesNothing()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        var tooShort = await Assert.ThrowsAsync<RestException>(() => rest.ModifyGuildAsync(guild.Id, o => o.Name = "x", cancellationToken: Ct));
        var tooLong = await Assert.ThrowsAsync<RestException>(() => rest.ModifyGuildAsync(guild.Id, o => o.Name = new string('x', 101), cancellationToken: Ct));

        foreach (var exception in new[] { tooShort, tooLong })
        {
            var error = Assert.Single(FieldErrors(exception, "name"));
            Assert.Equal(("BASE_TYPE_BAD_LENGTH", "Must be between 2 and 100 in length."), (error.Code, error.Message));
        }

        Assert.Equal("Test", discord.State.FindGuild(guild.Id)!.Name);
        Assert.Empty(Drain(subscription));
    }

    [Fact]
    [Parity("PATCH /guilds/{guild_id}")]
    [Parity("50035")]
    public async Task ModifyGuildAsync_ValuesOutsideTheEnumerationsOfTheSpec_Return50035()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        using var rest = discord.CreateRestClient();

        var level = await Assert.ThrowsAsync<RestException>(() => rest.ModifyGuildAsync(guild.Id, o => o.VerificationLevel = (VerificationLevel)9, cancellationToken: Ct));
        var timeout = await Assert.ThrowsAsync<RestException>(() => rest.ModifyGuildAsync(guild.Id, o => o.AfkTimeout = 61, cancellationToken: Ct));
        var locale = await Assert.ThrowsAsync<RestException>(() => rest.ModifyGuildAsync(guild.Id, o => o.PreferredLocale = "xx", cancellationToken: Ct));
        var description = await Assert.ThrowsAsync<RestException>(() => rest.ModifyGuildAsync(guild.Id, o => o.Description = new string('d', 301), cancellationToken: Ct));
        var icon = await Assert.ThrowsAsync<RestException>(
            () => rest.SendRequestAsync(HttpMethod.Patch, Json("""{"icon":"not an image"}"""), $"/guilds/{guild.Id}", cancellationToken: Ct));

        Assert.Equal("BASE_TYPE_CHOICES", Assert.Single(FieldErrors(level, "verification_level")).Code);
        Assert.Equal("Value must be one of (60, 300, 900, 1800, 3600).", Assert.Single(FieldErrors(timeout, "afk_timeout")).Message);
        Assert.Equal("BASE_TYPE_CHOICES", Assert.Single(FieldErrors(locale, "preferred_locale")).Code);
        Assert.Equal("BASE_TYPE_BAD_LENGTH", Assert.Single(FieldErrors(description, "description")).Code);
        Assert.Equal("IMAGE_INVALID", Assert.Single(FieldErrors(icon, "icon")).Code);
    }

    [Fact]
    [Parity("PATCH /guilds/{guild_id}")]
    [Parity("50035")]
    public async Task ModifyGuildAsync_ChannelSettingNamingTheWrongChannel_Returns50035OnThatFieldAndChangesNothing()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var elsewhere = discord.Seed.Guild("Elsewhere").TextChannel("other");
        var general = guild.TextChannel("general");
        var voice = guild.VoiceChannel("voice");
        using var rest = discord.CreateRestClient();
        using var subscription = discord.State.SubscribeGateway();

        var unknown = await Assert.ThrowsAsync<RestException>(() => rest.ModifyGuildAsync(guild.Id, o => { o.Name = "Renamed"; o.SystemChannelId = 424242; }, cancellationToken: Ct));
        var otherGuild = await Assert.ThrowsAsync<RestException>(() => rest.ModifyGuildAsync(guild.Id, o => o.RulesChannelId = elsewhere.Id, cancellationToken: Ct));
        var textAsAfk = await Assert.ThrowsAsync<RestException>(() => rest.ModifyGuildAsync(guild.Id, o => o.AfkChannelId = general.Id, cancellationToken: Ct));
        var voiceAsSystem = await Assert.ThrowsAsync<RestException>(() => rest.ModifyGuildAsync(guild.Id, o => o.SystemChannelId = voice.Id, cancellationToken: Ct));

        Assert.Equal("GUILD_CHANNEL_INVALID", Assert.Single(FieldErrors(unknown, "system_channel_id")).Code);
        Assert.Single(FieldErrors(otherGuild, "rules_channel_id"));
        Assert.Single(FieldErrors(textAsAfk, "afk_channel_id"));
        Assert.Single(FieldErrors(voiceAsSystem, "system_channel_id"));
        Assert.Equal("Test", discord.State.FindGuild(guild.Id)!.Name); // the whole update was refused
        Assert.Empty(Drain(subscription));
    }

    [Fact]
    [Parity("PATCH /guilds/{guild_id}")]
    [Parity("10004")]
    public async Task ModifyGuildAsync_UnknownGuildAndGuildWithoutTheBot_Return10004AndChangeNothing()
    {
        await using var discord = FakeDiscord.Create();
        var elsewhere = discord.Seed.Guild("Elsewhere", withBot: false);
        using var rest = discord.CreateRestClient();

        var unknown = await Assert.ThrowsAsync<RestException>(() => rest.ModifyGuildAsync(424242, o => o.Name = "x2", cancellationToken: Ct));
        var hidden = await Assert.ThrowsAsync<RestException>(() => rest.ModifyGuildAsync(elsewhere.Id, o => o.Name = "Hijacked", cancellationToken: Ct));

        AssertError(unknown, HttpStatusCode.NotFound, 10004);
        AssertError(hidden, HttpStatusCode.NotFound, 10004);
        Assert.Equal("Elsewhere", discord.State.FindGuild(elsewhere.Id)!.Name);
    }

    [Fact]
    [Parity("PATCH /guilds/{guild_id}")]
    [Parity("50109")]
    public async Task SendRequest_PatchWithInvalidJson_Returns50109()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        using var rest = discord.CreateRestClient();

        var exception = await Assert.ThrowsAsync<RestException>(
            () => rest.SendRequestAsync(HttpMethod.Patch, Json("{nope"), $"/guilds/{guild.Id}", cancellationToken: Ct));

        AssertError(exception, HttpStatusCode.BadRequest, 50109);
    }

    [Fact]
    [Parity("PATCH /guilds/{guild_id}")]
    public async Task DeleteChannelAsync_ChannelThatAGuildSettingPointsTo_ClearsTheSettingAndPublishesGuildUpdated()
    {
        await using var discord = FakeDiscord.Create();
        var guild = discord.Seed.Guild("Test");
        var system = guild.TextChannel("system");
        using var rest = discord.CreateRestClient();
        await rest.ModifyGuildAsync(guild.Id, o => o.SystemChannelId = system.Id, cancellationToken: Ct);
        using var subscription = discord.State.SubscribeGateway();

        await rest.DeleteChannelAsync(system.Id, cancellationToken: Ct);

        Assert.Null((await rest.GetGuildAsync(guild.Id, cancellationToken: Ct)).SystemChannelId);
        var changes = Drain(subscription);
        Assert.IsType<ChannelDeleted>(changes[0]);
        Assert.Null(Assert.IsType<GuildUpdated>(Assert.Single(changes.Skip(1))).Guild.Settings.SystemChannelId);
    }
}
