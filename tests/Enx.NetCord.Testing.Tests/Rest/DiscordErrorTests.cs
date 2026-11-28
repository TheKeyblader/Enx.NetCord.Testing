using System.Net;
using System.Text;
using System.Text.Json;
using Enx.NetCord.Testing.Rest;
using Enx.NetCord.Testing.State;
using NetCord.Rest;
using Xunit;

namespace Enx.NetCord.Testing.Tests.Rest;

public sealed class DiscordErrorTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string Json(DiscordError error) => Encoding.UTF8.GetString(error.ToJson());

    [Fact]
    [Parity("0")]
    public void ToJson_SimpleErrors_HaveMessageAndCodeAndNoErrorsProperty()
    {
        Assert.Equal("{\"message\":\"401: Unauthorized\",\"code\":0}", Json(DiscordError.Unauthorized()));
        Assert.Equal("{\"message\":\"404: Not Found\",\"code\":0}", Json(DiscordError.NotFound()));
        Assert.Equal("{\"message\":\"405: Method Not Allowed\",\"code\":0}", Json(DiscordError.MethodNotAllowed()));
        Assert.Equal("{\"message\":\"Unknown Channel\",\"code\":10003}", Json(DiscordError.UnknownChannel()));
        Assert.Equal("{\"message\":\"Unknown Message\",\"code\":10008}", Json(DiscordError.UnknownMessage()));
        Assert.Equal("{\"message\":\"Unknown User\",\"code\":10013}", Json(DiscordError.UnknownUser()));
        Assert.Equal("{\"message\":\"Invalid OAuth2 access token provided\",\"code\":50025}", Json(DiscordError.InvalidOAuth2AccessToken()));
        Assert.Equal("{\"message\":\"Missing Permissions\",\"code\":50013}", Json(DiscordError.MissingPermissions()));
        Assert.Equal("{\"message\":\"The request body contains invalid JSON.\",\"code\":50109}", Json(DiscordError.InvalidJson()));
    }

    [Fact]
    [Parity("50035")]
    public void ToJson_InvalidFormBody_BuildsObjectsPerPropertyOrIndexWithErrorsAtTheLeaves()
    {
        var errors = new FormErrors()
            .Add("content", "BASE_TYPE_MAX_LENGTH", "Must be 2000 or fewer in length.")
            .Add("embeds.0.title", "BASE_TYPE_REQUIRED", "This field is required")
            .Add("embeds.0.title", "BASE_TYPE_MAX_LENGTH", "Must be 256 or fewer in length.")
            .Add(string.Empty, "MODEL_TYPE_CONVERT", "Root problem");

        using var document = JsonDocument.Parse(DiscordError.InvalidFormBody(errors).ToJson());
        var root = document.RootElement;

        Assert.Equal(50035, root.GetProperty("code").GetInt32());
        Assert.Equal("Invalid Form Body", root.GetProperty("message").GetString());
        var tree = root.GetProperty("errors");
        Assert.Equal("MODEL_TYPE_CONVERT", tree.GetProperty("_errors")[0].GetProperty("code").GetString());
        var content = tree.GetProperty("content").GetProperty("_errors");
        Assert.Equal("BASE_TYPE_MAX_LENGTH", content[0].GetProperty("code").GetString());
        Assert.Equal("Must be 2000 or fewer in length.", content[0].GetProperty("message").GetString());
        var title = tree.GetProperty("embeds").GetProperty("0").GetProperty("title").GetProperty("_errors");
        Assert.Equal(2, title.GetArrayLength());
        Assert.False(tree.GetProperty("content").TryGetProperty("errors", out _));
    }

    [Fact]
    public void InvalidFormBody_WithoutDetails_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => DiscordError.InvalidFormBody(new FormErrors()));
    }

    [Fact]
    public void FormErrors_EmptyCodeOrSegment_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => new FormErrors().Add("a..b", "C", "m"));
        Assert.Throws<ArgumentException>(() => new FormErrors().Add("a", "", "m"));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, 0)]
    [InlineData(HttpStatusCode.Forbidden, 50013)]
    [InlineData(HttpStatusCode.NotFound, 0)]
    [InlineData(HttpStatusCode.NotFound, 10003)]
    [InlineData(HttpStatusCode.NotFound, 10004)]
    [InlineData(HttpStatusCode.Forbidden, 50001)]
    [InlineData(HttpStatusCode.NotFound, 10008)]
    [InlineData(HttpStatusCode.NotFound, 10013)]
    [InlineData(HttpStatusCode.Forbidden, 50025)]
    [InlineData(HttpStatusCode.InternalServerError, 0)]
    [InlineData(HttpStatusCode.BadGateway, 0)]
    public void TryCreateSimple_CatalogPair_ReturnsMatchingError(HttpStatusCode status, int code)
    {
        Assert.True(DiscordError.TryCreateSimple(status, code, out var error));
        Assert.Equal(status, error.Status);
        Assert.Equal(code, error.Code);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, 0)]
    [InlineData(HttpStatusCode.BadRequest, 50035)]
    [InlineData(HttpStatusCode.TooManyRequests, 0)]
    [InlineData((HttpStatusCode)418, 0)]
    public void TryCreateSimple_UnknownPair_ReturnsFalse(HttpStatusCode status, int code)
    {
        Assert.False(DiscordError.TryCreateSimple(status, code, out _));
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, 10004, "Unknown Guild")]
    [InlineData(HttpStatusCode.Forbidden, 50001, "Missing Access")]
    [Parity("10004")]
    [Parity("50001")]
    public void GuildAccessErrors_Factories_ProduceTheCatalogStatusCodeAndMessage(HttpStatusCode status, int code, string message)
    {
        var error = code == 10004 ? DiscordError.UnknownGuild() : DiscordError.MissingAccess();

        Assert.Equal((status, code, message), (error.Status, error.Code, error.Message));
        using var json = System.Text.Json.JsonDocument.Parse(error.ToJson());
        Assert.Equal(code, json.RootElement.GetProperty("code").GetInt32());
        Assert.Equal(message, json.RootElement.GetProperty("message").GetString());
    }

    [Theory]
    [InlineData((int)StateFailureKind.UnknownGuild, 10004)]
    [InlineData((int)StateFailureKind.MissingAccess, 50001)]
    [InlineData((int)StateFailureKind.UnknownChannel, 10003)]
    [InlineData((int)StateFailureKind.UnknownMessage, 10008)]
    [InlineData((int)StateFailureKind.UnknownUser, 10013)]
    public void FromStateFailure_KindWithACatalogError_MapsToIt(int kind, int code)
    {
        Assert.Equal(code, DiscordError.FromStateFailure(new StateFailure((StateFailureKind)kind, 1)).Code);
    }

    [Fact]
    public void FromStateFailure_KindWithoutACatalogError_IsRejected()
    {
        Assert.Throws<NotSupportedException>(() => DiscordError.FromStateFailure(new StateFailure(StateFailureKind.UnknownRole, 1)));
    }

    [Fact]
    [Parity("10008")]
    [Parity("http:404")]
    public async Task GetMessage_HandlerReturnsUnknownMessage_NetCordSees404With10008()
    {
        using var harness = new RestTestHarness();
        using var client = harness.CreateClient();

        var exception = await Assert.ThrowsAsync<RestException>(() => client.GetMessageAsync(1, 2, cancellationToken: Ct));

        Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
        Assert.Equal(10008, exception.Error!.Code);
        Assert.Equal("Unknown Message", exception.Error.Message);
    }

    [Fact]
    [Parity("50013")]
    [Parity("http:403")]
    public async Task ModifyChannel_HandlerReturnsMissingPermissions_NetCordSees403With50013()
    {
        using var harness = new RestTestHarness();
        using var client = harness.CreateClient();

        var exception = await Assert.ThrowsAsync<RestException>(() => client.SendRequestAsync(
            HttpMethod.Patch, new StringContent("{}", Encoding.UTF8, "application/json"), $"/channels/{1UL}", cancellationToken: Ct));

        Assert.Equal(HttpStatusCode.Forbidden, exception.StatusCode);
        Assert.Equal(50013, exception.Error!.Code);
    }

    [Fact]
    [Parity("50035")]
    [Parity("http:400")]
    public async Task SendMessage_ValidatorReportsAMissingField_NetCordSees400With50035AndTheErrorTree()
    {
        using var harness = new RestTestHarness();
        using var client = harness.CreateClient();

        var exception = await Assert.ThrowsAsync<RestException>(() => client.SendRequestAsync(
            HttpMethod.Post, new StringContent("{}", Encoding.UTF8, "application/json"), $"/channels/{1UL}/messages", cancellationToken: Ct));

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Equal(50035, exception.Error!.Code);
        Assert.Equal("Invalid Form Body", exception.Error.Message);
        var group = Assert.IsType<RestErrorGroup>(exception.Error.Error);
        var content = Assert.IsType<RestErrorDetailGroup>(group.Errors["content"]);
        var detail = Assert.Single(content.Errors);
        Assert.Equal("BASE_TYPE_REQUIRED", detail.Code);
        Assert.Equal("This field is required", detail.Message);
    }

    [Fact]
    [Parity("50109")]
    [Parity("http:400")]
    public async Task SendMessage_MalformedJson_ReturnsInvalidJson50109()
    {
        using var harness = new RestTestHarness();
        using var client = harness.CreateClient();

        var exception = await Assert.ThrowsAsync<RestException>(() => client.SendRequestAsync(
            HttpMethod.Post, new StringContent("{\"content\":", Encoding.UTF8, "application/json"), $"/channels/{1UL}/messages", cancellationToken: Ct));

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Equal(50109, exception.Error!.Code);
        Assert.Equal("The request body contains invalid JSON.", exception.Error.Message);
    }

    [Fact]
    public async Task SendMessage_ValidJson_ReachesTheHandlerWithTheValidatedPayloadAndTheBodyBytes()
    {
        using var harness = new RestTestHarness();
        using var client = harness.CreateClient();

        await using var stream = await client.SendRequestAsync(
            HttpMethod.Post, new StringContent("{\"content\":\"hi\"}", Encoding.UTF8, "application/json"), $"/channels/{1UL}/messages", cancellationToken: Ct);

        var attempt = Assert.Single(harness.Journal.Attempts);
        Assert.True(attempt.RequestBodyRead);
        Assert.Equal("{\"content\":\"hi\"}", Encoding.UTF8.GetString(attempt.RequestBody!));
        Assert.Equal("application/json", attempt.RequestContentType);
    }
}
