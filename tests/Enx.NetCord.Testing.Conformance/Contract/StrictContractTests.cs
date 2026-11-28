using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Enx.NetCord.Testing.Rest;
using Xunit;

namespace Enx.NetCord.Testing.Conformance.Contract;

/// <summary>
/// Guards the strict mode of the validator: a property the spec does not declare is rejected through <c>$ref</c>, <c>allOf</c>,
/// <c>oneOf</c>/<c>anyOf</c>, nested objects and arrays, unless <c>additionalProperties</c> or a registered exception accepts it.
/// </summary>
[Trait("Category", "Conformance")]
public sealed class StrictContractTests
{
    private const string Components = """
        {
          "Base": {"type":"object","properties":{"id":{"type":"string"}},"required":["id"]},
          "Extra": {"type":"object","properties":{"extra":{"type":"integer"}}},
          "Widget": {"allOf":[{"$ref":"#/components/schemas/Base"},{"$ref":"#/components/schemas/Extra"},{"type":"object","properties":{"own":{"type":"boolean"}}}]},
          "Box": {"type":"object","properties":{"widget":{"$ref":"#/components/schemas/Widget"},"widgets":{"type":"array","items":{"$ref":"#/components/schemas/Widget"}}}},
          "Cat": {"type":"object","properties":{"kind":{"const":"cat"},"purrs":{"type":"boolean"}},"required":["kind"]},
          "Dog": {"type":"object","properties":{"kind":{"const":"dog"},"barks":{"type":"boolean"}},"required":["kind"]},
          "Pet": {"oneOf":[{"$ref":"#/components/schemas/Cat"},{"$ref":"#/components/schemas/Dog"}]},
          "Either": {"anyOf":[{"type":"object","properties":{"a":{"type":"integer"}}},{"type":"object","properties":{"b":{"type":"integer"}}}]},
          "Nullable": {"oneOf":[{"type":"null"},{"$ref":"#/components/schemas/Base"}]},
          "NullableObject": {"type":["object","null"],"properties":{"a":{"type":"integer"}}},
          "Holder": {"type":"object","properties":{"any":{},"described":{"description":"annotations only"}}},
          "ClosedEmpty": {"type":"object","properties":{}},
          "Child": {"type":"object","properties":{"a":{"type":"integer"}}},
          "First": {"type":"object","properties":{"kind":{"const":1},"child":{"$ref":"#/components/schemas/Child"}},"required":["kind"]},
          "Second": {"type":"object","properties":{"kind":{"const":2}},"required":["kind"]},
          "Choice": {"oneOf":[{"$ref":"#/components/schemas/First"},{"$ref":"#/components/schemas/Second"}]},
          "Free": {"type":"object","additionalProperties":true},
          "Tags": {"type":"object","additionalProperties":{"type":"string"}}
        }
        """;

    [Theory]
    [InlineData("Base", """{"id":"1"}""")]
    [InlineData("Widget", """{"id":"1","extra":2,"own":true}""")] // allOf: the union of the branches
    [InlineData("Box", """{"widget":{"id":"1","extra":2},"widgets":[{"id":"2","own":false}]}""")]
    [InlineData("Pet", """{"kind":"cat","purrs":true}""")]
    [InlineData("Pet", """{"kind":"dog","barks":true}""")]
    [InlineData("Either", """{"a":1,"b":2}""")] // anyOf: every valid branch declares
    [InlineData("Nullable", "null")]
    [InlineData("Nullable", """{"id":"1"}""")]
    [InlineData("NullableObject", "null")]
    [InlineData("NullableObject", """{"a":1}""")]
    [InlineData("Holder", """{"any":{"x":1,"y":[1]},"described":{"z":1}}""")] // {} and annotation-only schemas are the true schema
    [InlineData("Holder", """{"any":5,"described":"text"}""")]
    [InlineData("Choice", """{"kind":1,"child":{"a":1}}""")]
    [InlineData("Free", """{"whatever":[1,2],"nested":{"x":1}}""")] // additionalProperties: true
    [InlineData("Tags", """{"x":"1","y":"2"}""")] // additionalProperties: schema
    public void Validate_OnlyDeclaredProperties_IsAccepted(string schema, string json)
    {
        Assert.Empty(Validate(schema, json));
    }

    [Theory]
    [InlineData("Base", """{"id":"1","surprise":true}""", "$: undeclared property 'surprise'", "Base")]
    [InlineData("Widget", """{"id":"1","surprise":true}""", "$: undeclared property 'surprise'", "Widget")] // $ref + allOf
    [InlineData("Widget", """{"id":"1","purrs":true}""", "$: undeclared property 'purrs'", "Extra")]
    [InlineData("Box", """{"widget":{"id":"1","surprise":1}}""", "$.widget: undeclared property 'surprise'", "Widget")] // nested
    [InlineData("Box", """{"widgets":[{"id":"1"},{"id":"2","surprise":1}]}""", "$.widgets[1]: undeclared property 'surprise'", "Widget")] // array item
    [InlineData("Pet", """{"kind":"cat","barks":true}""", "$: undeclared property 'barks'", "Cat")] // oneOf: only the branch that validates declares
    [InlineData("Pet", """{"kind":"dog","purrs":true}""", "$: undeclared property 'purrs'", "Dog")]
    [InlineData("Either", """{"a":1,"c":2}""", "$: undeclared property 'c'", "Either")]
    [InlineData("Nullable", """{"id":"1","x":1}""", "$: undeclared property 'x'", "Base")] // oneOf [null, $ref]
    [InlineData("NullableObject", """{"a":1,"x":1}""", "$: undeclared property 'x'", "NullableObject")] // type: [object, null]
    [InlineData("ClosedEmpty", """{"x":1}""", "$: undeclared property 'x'", "ClosedEmpty")] // properties: {} stays closed
    [InlineData("Holder", """{"any":1,"x":1}""", "$: undeclared property 'x'", "Holder")] // only the {} property is open, not its parent
    public void Validate_UndeclaredProperty_IsRejectedWithPathPropertyAndSchema(string schema, string json, string expected, string schemaInMessage)
    {
        var errors = Validate(schema, json);

        var error = Assert.Single(errors, e => e.Contains(expected, StringComparison.Ordinal));
        Assert.Contains(schemaInMessage, error, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_AdditionalPropertiesSchema_StillChecksTheValues()
    {
        Assert.Contains(Validate("Tags", """{"x":1}"""), e => e.Contains("$.x: expected string", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_UndeclaredPropertyOnAnInlineSchema_NamesNoComponent()
    {
        using var schema = JsonDocument.Parse("""{"type":"object","properties":{"a":{"type":"integer"}}}""");
        using var instance = JsonDocument.Parse("""{"a":1,"b":2}""");

        var errors = new JsonSchemaValidator(new Dictionary<string, JsonElement>()).Validate(instance.RootElement, schema.RootElement);

        Assert.Contains(errors, e => e.Contains("$: undeclared property 'b' (the spec schema an inline schema", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_AdditionalPropertiesFalse_ReportsThePropertyOnce()
    {
        using var schema = JsonDocument.Parse("""{"type":"object","properties":{"a":{"type":"integer"}},"additionalProperties":false}""");
        using var instance = JsonDocument.Parse("""{"a":1,"b":2}""");

        var errors = new JsonSchemaValidator(new Dictionary<string, JsonElement>()).Validate(instance.RootElement, schema.RootElement);

        Assert.Single(errors);
    }

    [Fact]
    public void Validate_OneOfWithNoValidBranch_ReportsTheBranchMismatch()
    {
        // purrs has the wrong type, so the Cat branch fails and Dog fails on the kind: the failure is the oneOf one.
        var errors = Validate("Pet", """{"kind":"cat","purrs":"yes"}""");

        Assert.Contains(errors, e => e.Contains("matches none of the oneOf branches", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_RecordedResponseWithAnUndeclaredProperty_IsRejected()
    {
        var attempt = Attempt("""{"id":"1","username":"a","discriminator":"0","avatar":null,"global_name":null,"public_flags":0,"flags":0,"primary_guild":null,"leaked":1}""");

        var errors = OpenApiContract.Current.Validate(attempt);

        Assert.Contains(errors, e => e.Contains("GET /users/{user_id} $: undeclared property 'leaked'", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_NestedUndeclaredObjectInAOneOfBranch_FailsTheSelectionWithTheNestedMessage()
    {
        // The undeclared property sits in a nested object of the only branch whose kind matches: that branch no longer validates.
        var errors = Validate("Choice", """{"kind":1,"child":{"a":1,"zzz":2}}""");

        var error = Assert.Single(errors);
        Assert.Contains("$: matches none of the oneOf branches", error, StringComparison.Ordinal);
        Assert.Contains("$.child: undeclared property 'zzz'", error, StringComparison.Ordinal);
        Assert.Contains("Child", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_FormBodyErrorTreeWithAnUndeclaredProperty_IsRejectedWithItsPath()
    {
        using var body = JsonDocument.Parse("""{"code":50035,"message":"Invalid Form Body","errors":{"name":{"_errors":[{"code":"BASE_TYPE_REQUIRED","message":"This field is required","extra":1}]}}}""");

        var errors = OpenApiContract.Current.Validator.ValidateNamed(body.RootElement, "ErrorResponse");

        Assert.Contains(errors, e => e.Contains("$.errors.name._errors[0]: undeclared property 'extra'", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Validate_EveryChannelFamilyOfTheFakeWithAnInjectedProperty_IsRejectedThroughTheOneOf()
    {
        // Mutation check on real output: a oneOf schema (GuildChannelResponse) must not hide an undeclared property.
        await using var discord = FakeDiscord.Create();
        var lobby = discord.Seed.Guild("Test").Category("Lobby");
        using var rest = discord.CreateRestClient();
        foreach (var id in new[] { lobby.Id, lobby.TextChannel("a").Id, lobby.VoiceChannel("b").Id, lobby.ForumChannel("c").Id })
        {
            _ = await rest.GetChannelAsync(id, cancellationToken: TestContext.Current.CancellationToken);
        }

        var attempts = discord.Recorded.Requests.Where(static r => r.Method == "GET" && r.RouteTemplate == "/channels/{channel_id}").ToList();

        Assert.Equal(4, attempts.Count);
        foreach (var attempt in attempts)
        {
            Assert.Empty(OpenApiContract.Current.Validate(attempt)); // the untouched response of each family conforms

            var node = JsonNode.Parse(attempt.ResponseBody!.Value.GetRawText())!.AsObject();
            node["injected"] = 1;
            using var mutated = JsonDocument.Parse(node.ToJsonString());

            var errors = OpenApiContract.Current.Validator.ValidateNamed(mutated.RootElement, "GuildChannelResponse");

            Assert.Contains(errors, e => e.Contains("$: undeclared property 'injected'", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Validate_ErrorEnvelopeWithAnUndeclaredProperty_IsRejected()
    {
        using var body = JsonDocument.Parse("""{"code":10003,"message":"Unknown Channel","stack":"…"}""");

        var errors = OpenApiContract.Current.Validator.ValidateNamed(body.RootElement, "ErrorResponse");

        Assert.Contains(errors, e => e.Contains("undeclared property 'stack'", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_PropertyWithARegisteredException_IsAcceptedAndRecordedAsUsed()
    {
        var exception = Registered("Base", "legacy");
        var validator = Validator(exception);

        var errors = Validate(validator, "Base", """{"id":"1","legacy":1}""");

        Assert.Empty(errors);
        Assert.Equal([exception], validator.UsedExceptions);
    }

    [Fact]
    public void Validate_ExceptionRegisteredOnAnotherSchema_DoesNotApply()
    {
        var validator = Validator(Registered("Cat", "legacy"));

        var errors = Validate(validator, "Base", """{"id":"1","legacy":1}""");

        Assert.Contains(errors, e => e.Contains("undeclared property 'legacy'", StringComparison.Ordinal));
        Assert.Empty(validator.UsedExceptions);
    }

    [Fact]
    public void Validate_ExceptionOnAnAllOfBranch_AppliesToTheWholeSchema()
    {
        var validator = Validator(Registered("Extra", "legacy"));

        Assert.Empty(Validate(validator, "Widget", """{"id":"1","legacy":1}"""));
    }

    [Fact]
    public void Validate_ExceptionAndAnotherUndeclaredProperty_RejectsOnlyTheOther()
    {
        var validator = Validator(Registered("Base", "legacy"));

        var errors = Validate(validator, "Base", """{"id":"1","legacy":1,"other":2}""");

        Assert.Single(errors);
        Assert.Contains("'other'", errors[0], StringComparison.Ordinal);
    }

    private static IReadOnlyList<string> Validate(string schema, string json) => Validate(Validator(), schema, json);

    private static IReadOnlyList<string> Validate(JsonSchemaValidator validator, string schema, string json)
    {
        using var instance = JsonDocument.Parse(json);
        return validator.ValidateNamed(instance.RootElement, schema);
    }

    private static JsonSchemaValidator Validator(params ContractException[] exceptions)
    {
        var schemas = JsonDocument.Parse(Components).RootElement.EnumerateObject().ToDictionary(static p => p.Name, static p => p.Value, StringComparer.Ordinal);
        return new JsonSchemaValidator(schemas, exceptions);
    }

    private static ContractException Registered(string schema, string property)
        => new(schema, property, "The spec forgot it.", "developers/resources/user.mdx:10", "GET /users/{user_id}", "PAR-SPEC-01");

    private static RecordedRestRequest Attempt(string responseJson) => RecordedRestRequest.From(new RestAttempt
    {
        Method = "GET",
        RouteTemplate = "/users/{user_id}",
        StatusCode = HttpStatusCode.OK,
        ResponseBody = System.Text.Encoding.UTF8.GetBytes(responseJson),
    });
}
