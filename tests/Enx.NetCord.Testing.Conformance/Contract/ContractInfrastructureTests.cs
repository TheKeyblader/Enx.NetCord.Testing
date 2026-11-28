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
    public void Contract_Extract_CoversEveryOperationTheFakeImplements()
    {
        var implemented = RouteTable.Default.Entries.Where(static e => e.Handler is not null).Select(static e => e.Operation.Key);

        var missing = implemented.Where(key => !OpenApiContract.Current.Covers(key)).Order(StringComparer.Ordinal).ToList();

        Assert.True(
            missing.Count == 0,
            "Add these operations to Contract/contract-operations.txt and re-run extract-openapi-contract.py: " + string.Join(", ", missing));
    }

    [Fact]
    public void ConformanceTests_EveryImplementedOperation_HasAScenarioTaggedWithItsKey()
    {
        var tagged = typeof(ContractInfrastructureTests).Assembly.GetTypes()
            .SelectMany(static t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
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
}
