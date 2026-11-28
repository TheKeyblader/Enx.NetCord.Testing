using System.Net;
using Enx.NetCord.Testing.Rest;
using Xunit;

namespace Enx.NetCord.Testing.Conformance.Contract;

/// <summary>Collection that runs after every parallel one, so the usage of the exceptions has been recorded by all the scenarios.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ContractExceptionsCollection
{
    /// <summary>The collection name.</summary>
    public const string Name = "Contract exceptions (after all scenarios)";
}

/// <summary>Guards the list of justified exceptions to the strict contract: sound entries, and no dead exception.</summary>
[Collection(ContractExceptionsCollection.Name)]
[Trait("Category", "Conformance")]
public sealed class ContractExceptionsTests
{
    private const string GetMe = "GET /users/{user_id}";

    [Fact]
    public void Default_Exceptions_AreSoundAgainstThePinnedSpec()
    {
        var problems = ContractExceptions.Audit(ContractExceptions.Default, OpenApiContract.Current);

        Assert.True(problems.Count == 0, "Fix or remove: " + string.Join("; ", problems));
    }

    [Fact]
    public void Default_Exceptions_AreAllExercisedByAScenario()
    {
        // Judged only for the operations whose success response was validated in this run, so a filtered `dotnet test` stays green.
        var dead = OpenApiContract.Current.FindDeadExceptions(ContractExceptions.Default);

        Assert.True(
            dead.Count == 0,
            "The fake no longer sends these properties; remove the exceptions: " + string.Join(", ", dead.Select(static e => $"{e.Schema}.{e.Property}")));
    }

    [Fact]
    public void FindDeadExceptions_ExceptionNoResponseNeeded_IsReported()
    {
        var exception = Registered("UserResponse", "legacy_flag");
        var contract = OpenApiContract.Create([exception]);

        Assert.Empty(contract.Validate(Attempt(UserJson)));

        Assert.Equal([exception], contract.FindDeadExceptions([exception]));
    }

    [Fact]
    public void FindDeadExceptions_ExceptionThatAcceptedAProperty_IsNotReported()
    {
        var exception = Registered("UserResponse", "legacy_flag");
        var contract = OpenApiContract.Create([exception]);

        Assert.Empty(contract.Validate(Attempt(UserJson.Replace("}", ""","legacy_flag":1}""", StringComparison.Ordinal))));

        Assert.Empty(contract.FindDeadExceptions([exception]));
        Assert.Equal([exception], contract.UsedExceptions);
    }

    [Fact]
    public void FindDeadExceptions_OperationNeverValidated_IsNotJudged()
    {
        var exception = Registered("UserResponse", "legacy_flag");
        var contract = OpenApiContract.Create([exception]);

        Assert.Empty(contract.FindDeadExceptions([exception]));
    }

    [Fact]
    public void FindDeadExceptions_ErrorResponseOfTheOperation_DoesNotCountAsExercise()
    {
        var exception = Registered("UserResponse", "legacy_flag");
        var contract = OpenApiContract.Create([exception]);

        _ = contract.Validate(Attempt("""{"code":10013,"message":"Unknown User"}""", HttpStatusCode.NotFound));

        Assert.Empty(contract.FindDeadExceptions([exception]));
    }

    [Fact]
    public void Audit_SoundException_HasNoProblem()
    {
        Assert.Empty(ContractExceptions.Audit([Registered("UserResponse", "legacy_flag")], OpenApiContract.Current));
    }

    [Fact]
    public void Audit_PropertyTheSpecDeclares_IsReported()
    {
        var problems = ContractExceptions.Audit([Registered("UserResponse", "username")], OpenApiContract.Current);

        Assert.Contains(problems, p => p.Contains("spec now declares this property", StringComparison.Ordinal));
    }

    [Fact]
    public void Audit_PropertyDeclaredThroughAComposedSchema_IsReported()
    {
        // GuildChannelResponse is a oneOf of channel schemas: a property of one branch is declared by the union.
        var problems = ContractExceptions.Audit([Registered("GuildChannelResponse", "topic")], OpenApiContract.Current);

        Assert.Contains(problems, p => p.Contains("spec now declares this property", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("NoSuchSchema", "x", "GET /users/{user_id}", "developers/resources/user.mdx:10", "Spec has no schema")]
    [InlineData("UserResponse", "x", "GET /nowhere", "developers/resources/user.mdx:10", "not in the contract extract")]
    [InlineData("UserResponse", "x", "GET /users/{user_id}", "", "discord-api-docs file and line")]
    [InlineData("UserResponse", "x", "GET /users/{user_id}", "developers/resources/user.mdx", "discord-api-docs file and line")]
    [InlineData("UserResponse", "x", "GET /users/{user_id}", "https://discord.com/developers/docs", "discord-api-docs file and line")]
    [InlineData("", "x", "GET /users/{user_id}", "developers/resources/user.mdx:10", "schema and property are required")]
    public void Audit_MalformedEntry_IsReported(string schema, string property, string operation, string reference, string expected)
    {
        var entry = new ContractException(schema, property, "The spec forgot it.", reference, operation, "PAR-SPEC-01");

        var problems = ContractExceptions.Audit([entry], OpenApiContract.Current);

        Assert.Contains(problems, p => p.Contains(expected, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Audit_MissingJustificationOrTask_IsReported()
    {
        var entry = new ContractException("UserResponse", "x", " ", "developers/resources/user.mdx:10", GetMe, "");

        var problems = ContractExceptions.Audit([entry], OpenApiContract.Current);

        Assert.Contains(problems, p => p.Contains("justification is required", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.Contains("task or user-review point is required", StringComparison.Ordinal));
    }

    [Fact]
    public void Audit_DuplicateEntry_IsReported()
    {
        var problems = ContractExceptions.Audit([Registered("UserResponse", "x"), Registered("UserResponse", "x")], OpenApiContract.Current);

        Assert.Contains(problems, p => p.Contains("registered more than once", StringComparison.Ordinal));
    }

    private const string UserJson = """{"id":"1","username":"a","avatar":null,"discriminator":"0","public_flags":0,"flags":0,"global_name":null,"primary_guild":null}""";

    private static ContractException Registered(string schema, string property)
        => new(schema, property, "The spec forgot it.", "developers/resources/user.mdx:10", GetMe, "PAR-SPEC-01");

    private static RecordedRestRequest Attempt(string responseJson, HttpStatusCode status = HttpStatusCode.OK) => RecordedRestRequest.From(new RestAttempt
    {
        Method = "GET",
        RouteTemplate = "/users/{user_id}",
        StatusCode = status,
        ResponseBody = System.Text.Encoding.UTF8.GetBytes(responseJson),
    });
}
