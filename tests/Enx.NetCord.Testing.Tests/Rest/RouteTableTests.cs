using System.Security.Cryptography;
using System.Text;
using Enx.NetCord.Testing.Rest;
using Xunit;

namespace Enx.NetCord.Testing.Tests.Rest;

public sealed class RouteTableTests
{
    private static RouteOperation Op(string method, string template, RouteSecurity security = RouteSecurity.Bot)
        => new(method, template, null, security);

    private static RouteBinding Binding(string key)
        => RouteBinding.Of(key, _ => RestResult.NoContent());

    [Fact]
    public void Default_EmbeddedInventory_PassesTheTableCheck()
    {
        var problems = RouteTable.Validate(RouteInventory.All, []);

        Assert.Empty(problems);
        Assert.NotNull(RouteTable.Default);
    }

    [Fact]
    public void Inventory_PinnedSpecification_HasTwoHundredFiftyOperationsAndEightNetCordOnlyRoutes()
    {
        Assert.Equal(250, RouteInventory.Specification.Length);
        Assert.Equal(8, NetCordOnlyRoutes.All.Length);
        Assert.Equal(258, RouteInventory.All.Count);
        Assert.All(RouteInventory.Specification, o => Assert.NotNull(o.OperationId));
        Assert.All(NetCordOnlyRoutes.All, o => Assert.False(o.InSpecification));
        Assert.Empty(NetCordOnlyRoutes.All.Select(o => o.Key).Intersect(RouteInventory.Specification.Select(o => o.Key)));
    }

    [Fact]
    public void Inventory_InteractionCallback_AcceptsAnonymousCallers()
    {
        var callback = RouteInventory.Specification.Single(o => o.OperationId == "create_interaction_response");

        Assert.True(callback.Security.HasFlag(RouteSecurity.Anonymous));
        Assert.Equal("POST /interactions/{interaction_id}/{interaction_token}/callback", callback.Key);
    }

    [Fact]
    public void Validate_DuplicateOperation_ReportsIt()
    {
        var problems = RouteTable.Validate([Op("GET", "/a"), Op("GET", "/a")], []);

        Assert.Contains(problems, p => p.Contains("duplicate operation 'GET /a'", StringComparison.Ordinal));
        Assert.Throws<RouteTableException>(() => RouteTable.Create([Op("GET", "/a"), Op("GET", "/a")], []));
    }

    [Fact]
    public void Validate_TemplatesDifferingOnlyByParameterNames_AreAmbiguous()
    {
        var problems = RouteTable.Validate([Op("GET", "/things/{thing_id}"), Op("POST", "/things/{id}")], []);

        Assert.Contains(problems, p => p.Contains("ambiguous", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_BindingWithoutInventoryEntry_Fails()
    {
        var exception = Assert.Throws<RouteTableException>(
            () => RouteTable.Create([Op("GET", "/a")], [Binding("GET /missing")]));

        Assert.Contains("binding 'GET /missing' has no inventory entry", exception.Problems);
    }

    [Fact]
    public void Validate_DuplicateBinding_Fails()
    {
        var problems = RouteTable.Validate([Op("GET", "/a")], [Binding("GET /a"), Binding("GET /a")]);

        Assert.Contains(problems, p => p.Contains("duplicate binding", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("a")]
    [InlineData("/a//b")]
    [InlineData("/a/x{id}")]
    [InlineData("/a/{id}/{id}")]
    public void Validate_MalformedTemplate_Fails(string template)
    {
        var problems = RouteTable.Validate([Op("GET", template)], []);

        Assert.Single(problems);
    }

    [Fact]
    public void Resolve_LiteralAndParameter_LiteralWinsAtFirstDistinguishingPosition()
    {
        var table = RouteTable.Create(
            [Op("GET", "/users/@me"), Op("GET", "/users/{user_id}"), Op("GET", "/g/templates/{code}"), Op("GET", "/g/{guild_id}/members")],
            []);

        Assert.Equal("/users/@me", table.Resolve("GET", ["users", "@me"]).Template);
        Assert.Equal("/users/{user_id}", table.Resolve("GET", ["users", "123"]).Template);
        Assert.Equal("/g/templates/{code}", table.Resolve("GET", ["g", "templates", "members"]).Template);
        Assert.Equal("/g/{guild_id}/members", table.Resolve("GET", ["g", "1", "members"]).Template);
    }

    [Fact]
    public void Resolve_EmptySegmentOrUnknownPath_IsNotFound()
    {
        var table = RouteTable.Create([Op("GET", "/channels/{channel_id}")], []);

        Assert.Equal(RouteResolutionKind.NotFound, table.Resolve("GET", ["channels", ""]).Kind);
        Assert.Equal(RouteResolutionKind.NotFound, table.Resolve("GET", ["channels"]).Kind);
        Assert.Equal(RouteResolutionKind.NotFound, table.Resolve("GET", ["Channels", "1"]).Kind);
        Assert.Equal(RouteResolutionKind.NotFound, table.Resolve("GET", []).Kind);
    }

    [Fact]
    public void Resolve_KnownPathUnknownMethod_IsMethodNotAllowedWithSortedAllow()
    {
        var table = RouteTable.Create([Op("PATCH", "/users/@me"), Op("GET", "/users/@me"), Op("GET", "/users/{user_id}")], []);

        var resolution = table.Resolve("PUT", ["users", "@me"]);

        Assert.Equal(RouteResolutionKind.MethodNotAllowed, resolution.Kind);
        Assert.Equal(["GET", "PATCH"], resolution.AllowedMethods);
        Assert.Equal(RouteResolutionKind.MethodNotAllowed, table.Resolve("HEAD", ["users", "1"]).Kind);
    }

    [Fact]
    public void Resolve_ParameterValue_IsExtractedUnchanged()
    {
        var table = RouteTable.Create([Op("GET", "/w/{webhook_id}/{webhook_token}")], []);

        var resolution = table.Resolve("GET", ["w", "1", "a/b%40"]);

        Assert.Equal("1", resolution.Parameters["webhook_id"]);
        Assert.Equal("a/b%40", resolution.Parameters["webhook_token"]);
    }

    [Fact]
    public void Entry_Bucket_IsLowerHexSha256OfMethodAndTemplate()
    {
        var entry = RouteTable.Default.Resolve("GET", ["users", "@me"]).Entry!;
        var expected = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("GET /users/@me"))).ToLowerInvariant();

        Assert.Equal(expected, entry.Bucket);
        Assert.Equal(64, entry.Bucket.Length);
    }

    [Theory]
    [InlineData("https://fake.discord.test/api/v10/users/%40me", "users|@me")]
    [InlineData("https://fake.discord.test/api/v10/users/@me", "users|@me")]
    [InlineData("https://fake.discord.test/api/v10/users/a%2Fb", "users|a/b")]
    [InlineData("https://fake.discord.test/api/v10/users/%2540me", "users|%40me")]
    [InlineData("https://fake.discord.test/api/v10/channels//messages", "channels||messages")]
    [InlineData("https://FAKE.discord.test/api/v10", "")]
    public void Parse_EncodedSegments_AreSplitBeforeDecodingOnce(string uri, string expected)
    {
        var target = RestRequestParser.Parse(new Uri(uri), RestPipelineContext.DefaultHostname);

        Assert.Equal(expected, string.Join('|', target.Segments));
    }

    [Fact]
    public void Parse_Query_PreservesRepeatedValuesInWireOrder()
    {
        var target = RestRequestParser.Parse(
            new Uri("https://fake.discord.test/api/v10/guilds/1/members?limit=5&after=2&limit=7&q=a%20b%2Bc"),
            RestPipelineContext.DefaultHostname);

        Assert.Equal(
            [("limit", "5"), ("after", "2"), ("limit", "7"), ("q", "a b+c")],
            target.Query.Select(p => (p.Key, p.Value)));
    }

    [Theory]
    [InlineData("https://example.com/api/v10/users/@me")]
    [InlineData("https://fake.discord.test/api/v9/users/@me")]
    [InlineData("https://fake.discord.test/users/@me")]
    [InlineData("https://fake.discord.test/api/v100/users/@me")]
    public void Parse_OtherHostOrPrefix_IsALocalConfigurationError(string uri)
    {
        Assert.Throws<InvalidOperationException>(() => RestRequestParser.Parse(new Uri(uri), RestPipelineContext.DefaultHostname));
    }
}
