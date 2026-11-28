using System.Net;
using System.Reflection;
using System.Text.Json;
using Enx.NetCord.Testing.Rest;
using Xunit;

namespace Enx.NetCord.Testing.Conformance.Contract;

/// <summary>Guards the contract infrastructure itself: pinned source, validator strictness, and one scenario per implemented operation.</summary>
[Trait("Category", "Conformance")]
public sealed class ContractInfrastructureTests
{
    [Fact]
    public void Contract_Extract_IsProducedFromThePinnedSpecCommit()
    {
        Assert.Equal(RouteInventory.SpecificationCommit, OpenApiContract.Current.Commit);
    }

    [Fact]
    public void Contract_Extract_AndRouteInventory_DescribeTheSameSpecOperations()
    {
        var inventory = RouteTable.Default.Entries.Where(static e => e.Operation.InSpecification).Select(static e => e.Operation.Key).ToHashSet(StringComparer.Ordinal);
        var extract = OpenApiContract.Current.OperationKeys.ToHashSet(StringComparer.Ordinal);

        var notExtracted = inventory.Except(extract).Order(StringComparer.Ordinal).ToList();
        var notInventoried = extract.Except(inventory).Order(StringComparer.Ordinal).ToList();

        Assert.True(
            notExtracted.Count == 0 && notInventoried.Count == 0,
            "The inventory and the contract extract are out of sync (after a /parity/sync, re-run tools/generate-route-inventory.py and "
            + "Contract/extract-openapi-contract.py). Not extracted: " + string.Join(", ", notExtracted)
            + ". Not inventoried: " + string.Join(", ", notInventoried));
    }

    [Fact]
    public void Contract_Extract_UsesOnlyKeywordsTheValidatorSupports()
    {
        var problems = OpenApiContract.Current.FindUnsupported();

        Assert.True(problems.Count == 0, "Extend JsonSchemaValidator or document the strategy for: " + string.Join("; ", problems.Take(20)));
    }

    [Fact]
    public void ConformanceTests_EveryImplementedOperation_HasAScenarioTaggedWithItsKey()
    {
        var tagged = typeof(ContractInfrastructureTests).Assembly.GetTypes()
            .SelectMany(static t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            .Where(static m => !m.GetCustomAttributes<FactAttribute>().Any(static f => f.Skip is not null)) // a skipped test proves nothing
            .SelectMany(static m => m.GetCustomAttributes<ParityAttribute>())
            .SelectMany(static a => a.GetTraits())
            .Select(static trait => trait.Value)
            .ToHashSet(StringComparer.Ordinal);
        var implemented = RouteTable.Default.Entries.Where(static e => e.Handler is not null).Select(static e => e.Operation.Key);

        var missing = implemented.Where(key => !tagged.Contains(key)).Order(StringComparer.Ordinal).ToList();

        Assert.True(
            missing.Count == 0,
            "Write a conformance scenario with [Parity(\"<key>\")] for: " + string.Join(", ", missing));
    }

    [Fact]
    public void Validate_DocumentedResponse_AcceptsAMinimalUser()
    {
        using var body = JsonDocument.Parse("""{"id":"1","username":"a","avatar":null,"discriminator":"0","public_flags":0,"flags":0,"global_name":null,"primary_guild":null}""");

        var errors = OpenApiContract.Current.Validator.ValidateNamed(body.RootElement, "UserResponse");

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("""{"username":"a","discriminator":"0"}""", "missing required property 'id'")]
    [InlineData("""{"id":1,"username":"a","discriminator":"0"}""", "expected string")]
    [InlineData("""{"id":"-1","username":"a","discriminator":"0"}""", "does not match")]
    [InlineData("""{"id":"1","username":null,"discriminator":"0"}""", "expected string")]
    [InlineData("""{"id":"1","username":"a","discriminator":"0","bot":"yes"}""", "expected boolean")]
    [InlineData("""{"id":"1","username":"a","discriminator":"0","public_flags":1.5}""", "expected integer")]
    public void Validate_NonConformingUser_ReportsTheViolation(string json, string expected)
    {
        using var body = JsonDocument.Parse(json);

        var errors = OpenApiContract.Current.Validator.ValidateNamed(body.RootElement, "UserResponse");

        Assert.Contains(errors, e => e.Contains(expected, StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_OneOfWithNoMatchingBranch_ReportsTheViolation()
    {
        using var body = JsonDocument.Parse("""{"id":"1","type":99,"flags":0}""");
        var operation = OpenApiContract.Current;

        var errors = operation.Validator.ValidateNamed(body.RootElement, "PrivateChannelResponse");

        Assert.NotEmpty(errors);
    }

    [Fact]
    public void Validate_ErrorEnvelope_AcceptsFieldErrorsAndRejectsAMissingMessage()
    {
        using var good = JsonDocument.Parse("""{"code":50035,"message":"Invalid Form Body","errors":{"name":{"_errors":[{"code":"BASE_TYPE_REQUIRED","message":"This field is required"}]}}}""");
        using var bad = JsonDocument.Parse("""{"code":10003}""");

        Assert.Empty(OpenApiContract.Current.Validator.ValidateNamed(good.RootElement, "ErrorResponse"));
        Assert.Contains(
            OpenApiContract.Current.Validator.ValidateNamed(bad.RootElement, "ErrorResponse"),
            e => e.Contains("missing required property 'message'", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_KeywordOutsideTheSupportedSubset_Throws()
    {
        using var schema = JsonDocument.Parse("""{"not":{"type":"string"}}""");
        using var instance = JsonDocument.Parse("1");
        var validator = new JsonSchemaValidator(new Dictionary<string, JsonElement>());

        Assert.Throws<NotSupportedException>(() => validator.Validate(instance.RootElement, schema.RootElement));
    }

    // ---- Validator oracle: each keyword must reject what it forbids (a validator that accepts everything would make every conformance test vacuous).

    [Theory]
    [InlineData("""{"oneOf":[{"type":"string"},{"type":"string","maxLength":5}]}""", "\"abc\"", "matches 2 oneOf branches")]
    [InlineData("""{"oneOf":[{"type":"integer"},{"type":"boolean"}]}""", "\"x\"", "matches none of the oneOf branches")]
    [InlineData("""{"anyOf":[{"type":"integer"},{"type":"boolean"}]}""", "\"x\"", "matches none of the anyOf branches")]
    [InlineData("""{"allOf":[{"type":"object","required":["a"]},{"type":"object","required":["b"]}]}""", """{"a":1}""", "missing required property 'b'")]
    [InlineData("""{"type":"string","format":"date-time"}""", "\"2026-13-45T00:00:00Z\"", "RFC 3339")]
    [InlineData("""{"type":"string","format":"date-time"}""", "\"2026-01-01 00:00:00\"", "RFC 3339")]
    [InlineData("""{"type":"string","format":"date-time"}""", "\"2026-01-01T00:00:00\"", "RFC 3339")]
    [InlineData("""{"type":"string","format":"snowflake"}""", "\"18446744073709551616\"", "64-bit snowflake")]
    [InlineData("""{"type":"string","format":"snowflake"}""", "\"-1\"", "64-bit snowflake")]
    [InlineData("""{"type":"string","format":"uri"}""", "\"not a uri\"", "absolute URI")]
    [InlineData("""{"type":"integer","format":"int32"}""", "2147483648", "does not fit in an int32")]
    [InlineData("""{"type":"integer","format":"int32"}""", "-2147483649", "does not fit in an int32")]
    [InlineData("""{"type":"integer","format":"int64"}""", "9223372036854775808", "does not fit in an int64")]
    [InlineData("""{"type":"integer","minimum":1}""", "0", "below the minimum")]
    [InlineData("""{"type":"integer","maximum":10}""", "11", "above the maximum")]
    [InlineData("""{"type":"integer"}""", "1.5", "expected integer")]
    [InlineData("""{"type":"string","enum":["a","b"]}""", "\"c\"", "is not one of")]
    [InlineData("""{"const":3}""", "4", "expected 3")]
    [InlineData("""{"type":"object","properties":{"a":{"type":"integer"}},"additionalProperties":false}""", """{"a":1,"b":2}""", "unexpected property 'b'")]
    [InlineData("""{"type":"object","additionalProperties":{"type":"integer"}}""", """{"a":"x"}""", "expected integer")]
    [InlineData("""{"type":"object","required":["a"]}""", "{}", "missing required property 'a'")]
    [InlineData("""{"type":"array","items":{"type":"integer"}}""", """[1,"x"]""", "$[1]: expected integer")]
    [InlineData("""{"type":"array","uniqueItems":true}""", "[1,2,1]", "duplicate item")]
    [InlineData("""{"type":"array","uniqueItems":true}""", "[1,1.0]", "duplicate item")]
    [InlineData("""{"type":"array","uniqueItems":true}""", """[{"a":1,"b":2},{"b":2,"a":1}]""", "duplicate item")]
    [InlineData("""{"type":"string","maxLength":2}""", "\"abc\"", "longer than 2 code points")]
    [InlineData("""{"type":"string","maxLength":2}""", "\"\ud83d\ude00\ud83d\ude00\ud83d\ude00\"", "longer than 2 code points")]
    [InlineData("""{"type":"string","minLength":2}""", "\"a\"", "shorter than 2 code points")]
    [InlineData("""{"type":"string","pattern":"^[0-9]+$"}""", "\"12a\"", "does not match")]
    [InlineData("""{"type":["string","null"]}""", "1", "expected string or null")]
    public void Validate_ViolatingInstance_IsRejected(string schemaJson, string instanceJson, string expected)
    {
        var errors = ValidateInline(schemaJson, instanceJson);

        Assert.Contains(errors, e => e.Contains(expected, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("""{"oneOf":[{"type":"integer"},{"type":"string"}]}""", "\"x\"")]
    [InlineData("""{"anyOf":[{"type":"integer"},{"type":"string"}]}""", "\"x\"")]
    [InlineData("""{"type":"string","format":"date-time"}""", "\"2026-01-01T00:00:00.123+02:00\"")]
    [InlineData("""{"type":"string","format":"snowflake"}""", "\"18446744073709551615\"")]
    [InlineData("""{"type":"string","format":"uri"}""", "\"https://example.com/a?b=c\"")]
    [InlineData("""{"type":"integer","format":"int32"}""", "2147483647")]
    [InlineData("""{"type":"integer","format":"int64"}""", "9223372036854775807")]
    [InlineData("""{"type":"object","properties":{"a":{"type":"integer"}},"additionalProperties":false}""", """{"a":1}""")]
    [InlineData("""{"type":"object","properties":{"a":{"type":"integer"}}}""", """{"a":1,"undeclared":true}""")]
    [InlineData("""{"type":"array","uniqueItems":true}""", "[1,2,3]")]
    [InlineData("""{"type":"string","maxLength":2}""", "\"\ud83d\ude00\ud83d\ude00\"")]
    [InlineData("""{"type":["string","null"],"enum":["a",null]}""", "null")]
    [InlineData("""{"const":3}""", "3")]
    public void Validate_ConformingInstance_IsAccepted(string schemaJson, string instanceJson)
    {
        Assert.Empty(ValidateInline(schemaJson, instanceJson));
    }

    [Theory]
    [InlineData("""{"type":"string","format":"mystery"}""", "unsupported format")]
    [InlineData("""{"type":"mystery"}""", "unsupported type")]
    [InlineData("""{"not":{"type":"string"}}""", "unsupported keyword 'not'")]
    [InlineData("""{"$ref":"#/components/schemas/Nowhere"}""", "unresolved reference")]
    [InlineData("""{"type":"object","properties":{"a":{"patternProperties":{}}}}""", "unsupported keyword 'patternProperties'")]
    public void FindUnsupported_SchemaOutsideTheSubset_IsReported(string schemaJson, string expected)
    {
        using var schema = JsonDocument.Parse(schemaJson);
        var validator = new JsonSchemaValidator(new Dictionary<string, JsonElement>());

        Assert.Contains(validator.FindUnsupported(schema.RootElement, "inline"), p => p.Contains(expected, StringComparison.Ordinal));
    }

    // ---- OpenApiContract.Validate: the status and body rules of a recorded attempt.

    private const string DeleteMessage = "DELETE /channels/{channel_id}/messages/{message_id}";
    private const string GetMe = "GET /users/@me";

    [Fact]
    public void Validate_Documented204WithoutABody_Conforms()
    {
        Assert.Empty(OpenApiContract.Current.Validate(Attempt(DeleteMessage, HttpStatusCode.NoContent)));
    }

    [Fact]
    public void Validate_204WithABody_IsRejected()
    {
        var errors = OpenApiContract.Current.Validate(Attempt(DeleteMessage, HttpStatusCode.NoContent, """{"unexpected":true}"""));

        Assert.Contains(errors, e => e.Contains("without a body", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.Created)]
    [InlineData(HttpStatusCode.Accepted)]
    public void Validate_UndocumentedSuccessStatus_IsRejected(HttpStatusCode status)
    {
        var errors = OpenApiContract.Current.Validate(Attempt(DeleteMessage, status, "{}"));

        Assert.Contains(errors, e => e.Contains("is not documented", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_201WhereTheSpecDocumentsOnly200_IsRejected()
    {
        var errors = OpenApiContract.Current.Validate(Attempt(GetMe, HttpStatusCode.Created, """{"id":"1"}"""));

        Assert.Contains(errors, e => e.Contains("status 201 is not documented", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_200WithoutTheDocumentedJsonBody_IsRejected()
    {
        var errors = OpenApiContract.Current.Validate(Attempt(GetMe, HttpStatusCode.OK));

        Assert.Contains(errors, e => e.Contains("sent none", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_200WithABodyMissingARequiredProperty_IsRejected()
    {
        var errors = OpenApiContract.Current.Validate(Attempt(GetMe, HttpStatusCode.OK, """{"username":"bot"}"""));

        Assert.Contains(errors, e => e.Contains("missing required property 'id'", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_ClientError_MustMatchTheErrorEnvelope()
    {
        var good = OpenApiContract.Current.Validate(Attempt(GetMe, HttpStatusCode.NotFound, """{"code":10003,"message":"Unknown Channel"}"""));
        var bad = OpenApiContract.Current.Validate(Attempt(GetMe, HttpStatusCode.NotFound, """{"code":"10003"}"""));
        var none = OpenApiContract.Current.Validate(Attempt(GetMe, HttpStatusCode.NotFound));

        Assert.Empty(good);
        Assert.NotEmpty(bad);
        Assert.NotEmpty(none);
    }

    [Fact]
    public void Validate_TooManyRequests_MustMatchTheRatelimitEnvelopeNotTheGenericError()
    {
        var good = OpenApiContract.Current.Validate(
            Attempt(GetMe, HttpStatusCode.TooManyRequests, """{"code":0,"message":"You are being rate limited.","retry_after":1.5,"global":false}"""));
        var genericOnly = OpenApiContract.Current.Validate(
            Attempt(GetMe, HttpStatusCode.TooManyRequests, """{"code":0,"message":"You are being rate limited."}"""));

        Assert.Empty(good);
        Assert.Contains(genericOnly, e => e.Contains("missing required property 'retry_after'", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_AttemptWithoutAResponse_IsRejected()
    {
        var errors = OpenApiContract.Current.Validate(Attempt(GetMe, status: null));

        Assert.Contains(errors, e => e.Contains("no response", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_SpecOperationAbsentFromTheExtract_IsRejected()
    {
        var errors = OpenApiContract.Current.Validate(Attempt("GET /not/in/the/spec", HttpStatusCode.OK, "{}"));

        Assert.Contains(errors, e => e.Contains("not in the contract extract", StringComparison.Ordinal));
    }

    private static IReadOnlyList<string> ValidateInline(string schemaJson, string instanceJson)
    {
        using var schema = JsonDocument.Parse(schemaJson);
        using var instance = JsonDocument.Parse(instanceJson);
        return new JsonSchemaValidator(new Dictionary<string, JsonElement>()).Validate(instance.RootElement, schema.RootElement);
    }

    private static RecordedRestRequest Attempt(string key, HttpStatusCode? status, string? responseJson = null)
    {
        var space = key.IndexOf(' ', StringComparison.Ordinal);
        return RecordedRestRequest.From(new RestAttempt
        {
            Method = key[..space],
            RouteTemplate = key[(space + 1)..],
            StatusCode = status,
            ResponseBody = responseJson is null ? null : System.Text.Encoding.UTF8.GetBytes(responseJson),
        });
    }
}
