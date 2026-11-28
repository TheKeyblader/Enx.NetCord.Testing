using System.Text;
using System.Text.Json;
using Enx.NetCord.Testing.Rest;
using Enx.NetCord.Testing.State;
using Xunit;

namespace Enx.NetCord.Testing.Tests.Rest.Validation;

/// <summary>The typed-array, nested-object and query helpers of <see cref="FormValidation"/>, and <see cref="FormErrors.Scope"/>.</summary>
public sealed class FormValidationArrayTests
{
    private static JsonElement Root(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static FakeRestRequest Request(params (string Name, string Value)[] query)
        => new(
            new RouteOperation("GET", "/x", null, RouteSecurity.Bot),
            new Dictionary<string, string>(),
            [.. query.Select(q => new KeyValuePair<string, string>(q.Name, q.Value))],
            null,
            RestCaller.Anonymous,
            FakeDiscordState.Create());

    private static JsonElement Tree(FormErrors errors)
    {
        using var document = JsonDocument.Parse(DiscordError.InvalidFormBody(errors).ToJson());
        return document.RootElement.GetProperty("errors").Clone();
    }

    private static string CodeAt(JsonElement tree, params string[] path)
    {
        foreach (var part in path)
        {
            tree = tree.GetProperty(part);
        }

        return tree.GetProperty("_errors")[0].GetProperty("code").GetString()!;
    }

    [Fact]
    public void ReadSnowflakeArray_StringsAndNumbers_ReturnsTheIdsInOrder()
    {
        var errors = new FormErrors();

        var ids = FormValidation.ReadSnowflakeArray(Root("""{"ids":["3",2,1]}"""), "ids", 0, 5, true, errors);

        Assert.Equal([3UL, 2, 1], ids);
        Assert.True(errors.IsEmpty);
    }

    [Theory]
    [InlineData("""{}""")]
    [InlineData("""{"ids":null}""")]
    public void ReadSnowflakeArray_AbsentOrNull_ReturnsNullWithoutError(string json)
    {
        var errors = new FormErrors();

        Assert.Null(FormValidation.ReadSnowflakeArray(Root(json), "ids", 2, 5, true, errors));
        Assert.True(errors.IsEmpty);
    }

    [Fact]
    public void ReadSnowflakeArray_NotAnArray_IsBadType()
    {
        var errors = new FormErrors();

        Assert.Null(FormValidation.ReadSnowflakeArray(Root("""{"ids":"1"}"""), "ids", 0, 5, true, errors));

        Assert.Equal("BASE_TYPE_BAD_TYPE", CodeAt(Tree(errors), "ids"));
    }

    [Theory]
    [InlineData("""{"ids":["1"]}""", 2, 5, "BASE_TYPE_BAD_LENGTH")]
    [InlineData("""{"ids":["1","2","3"]}""", 2, 2, "BASE_TYPE_BAD_LENGTH")]
    [InlineData("""{"ids":["1","2","3"]}""", 0, 2, "BASE_TYPE_MAX_LENGTH")]
    public void ReadSnowflakeArray_SizeOutOfRange_IsALengthError(string json, int min, int max, string code)
    {
        var errors = new FormErrors();

        Assert.Null(FormValidation.ReadSnowflakeArray(Root(json), "ids", min, max, true, errors));

        Assert.Equal(code, CodeAt(Tree(errors), "ids"));
    }

    [Fact]
    public void ReadSnowflakeArray_BadItem_IsReportedAtTheItemIndexAndTheOthersStillChecked()
    {
        var errors = new FormErrors();

        var ids = FormValidation.ReadSnowflakeArray(Root("""{"ids":["1","x",true]}"""), "ids", 0, 5, true, errors);

        Assert.Null(ids);
        var tree = Tree(errors);
        Assert.Equal("NUMBER_TYPE_COERCE", CodeAt(tree, "ids", "1"));
        Assert.Equal("NUMBER_TYPE_COERCE", CodeAt(tree, "ids", "2"));
        Assert.False(tree.GetProperty("ids").TryGetProperty("0", out _));
    }

    [Fact]
    public void ReadSnowflakeArray_Duplicates_AreRefusedOnlyWhenUniqueIsRequested()
    {
        var unique = new FormErrors();
        var anything = new FormErrors();
        var root = Root("""{"ids":["1","1"]}""");

        Assert.Null(FormValidation.ReadSnowflakeArray(root, "ids", 0, 5, true, unique));
        Assert.Equal([1UL, 1], FormValidation.ReadSnowflakeArray(root, "ids", 0, 5, false, anything));

        Assert.Equal("BASE_TYPE_ARRAY_UNIQUE", CodeAt(Tree(unique), "ids"));
        Assert.True(anything.IsEmpty);
    }

    [Fact]
    public void TryReadArray_Array_ReturnsTheElement()
    {
        var errors = new FormErrors();

        Assert.True(FormValidation.TryReadArray(Root("""{"a":[1,2]}"""), "a", errors, out var array));

        Assert.Equal(2, array.GetArrayLength());
        Assert.True(errors.IsEmpty);
    }

    [Fact]
    public void TryReadObject_ObjectNullAbsentAndOtherTypes_BehaveAsDocumented()
    {
        var errors = new FormErrors();

        Assert.True(FormValidation.TryReadObject(Root("""{"o":{"x":1}}"""), "o", errors, out var value));
        Assert.Equal(1, value.GetProperty("x").GetInt32());
        Assert.False(FormValidation.TryReadObject(Root("""{"o":null}"""), "o", errors, out _));
        Assert.False(FormValidation.TryReadObject(Root("""{}"""), "o", errors, out _));
        Assert.True(errors.IsEmpty);

        Assert.False(FormValidation.TryReadObject(Root("""{"o":[]}"""), "o", errors, out _));
        Assert.Equal("MODEL_TYPE_CONVERT", CodeAt(Tree(errors), "o"));
    }

    [Fact]
    public void CheckMaxLength_CountsCodePointsNotUtf16Units()
    {
        var errors = new FormErrors();
        var twoEmoji = "😀😀";

        Assert.True(FormValidation.CheckMaxLength(twoEmoji, "s", 2, errors));
        Assert.False(FormValidation.CheckMaxLength(twoEmoji, "s", 1, errors));

        var error = Tree(errors).GetProperty("s").GetProperty("_errors")[0];
        Assert.Equal("BASE_TYPE_MAX_LENGTH", error.GetProperty("code").GetString());
        Assert.Equal("Must be 1 or fewer in length.", error.GetProperty("message").GetString());
    }

    [Fact]
    public void CheckMaxCount_AtAndAboveTheBound_OnlyAboveIsAnError()
    {
        var errors = new FormErrors();

        Assert.True(FormValidation.CheckMaxCount(10, "embeds", 10, errors));
        Assert.True(errors.IsEmpty);
        Assert.False(FormValidation.CheckMaxCount(11, "embeds", 10, errors));
        Assert.Equal("BASE_TYPE_MAX_LENGTH", CodeAt(Tree(errors), "embeds"));
    }

    [Fact]
    public void ReadQueryInteger_AbsentValidAndInvalid_FollowTheBodyCoercionRules()
    {
        var errors = new FormErrors();

        Assert.Null(FormValidation.ReadQueryInteger(Request(), "limit", 1, 100, errors));
        Assert.Equal(25, FormValidation.ReadQueryInteger(Request(("limit", "25")), "limit", 1, 100, errors));
        Assert.True(errors.IsEmpty);

        Assert.Null(FormValidation.ReadQueryInteger(Request(("limit", "0")), "limit", 1, 100, errors));
        Assert.Equal("NUMBER_TYPE_MIN", CodeAt(Tree(errors), "limit"));
        var coerce = new FormErrors();
        Assert.Null(FormValidation.ReadQueryInteger(Request(("limit", "")), "limit", 1, 100, coerce));
        Assert.Equal("NUMBER_TYPE_COERCE", CodeAt(Tree(coerce), "limit"));
    }

    [Fact]
    public void ReadQuerySnowflake_AbsentValidAndInvalid_ReturnTheIdNullOrAnError()
    {
        var errors = new FormErrors();

        Assert.Null(FormValidation.ReadQuerySnowflake(Request(), "before", errors));
        Assert.Equal(123UL, FormValidation.ReadQuerySnowflake(Request(("before", "123")), "before", errors));
        Assert.True(errors.IsEmpty);

        Assert.Null(FormValidation.ReadQuerySnowflake(Request(("before", "-1")), "before", errors));
        Assert.Equal("NUMBER_TYPE_COERCE", CodeAt(Tree(errors), "before"));
    }

    [Fact]
    public void Scope_NestedErrors_LandUnderThePrefixAndOnlyScopedIsEmptyReflectsTheView()
    {
        var errors = new FormErrors();
        var embed = errors.Scope("embeds.0");
        var footer = embed.Scope("footer");

        Assert.True(embed.IsEmpty);
        footer.Add("text", "BASE_TYPE_MAX_LENGTH", "Must be 2048 or fewer in length.");
        embed.Add(string.Empty, "MODEL_TYPE_CONVERT", "Only dictionaries may be used in a ModelType");

        Assert.False(embed.IsEmpty);
        Assert.False(footer.IsEmpty);
        Assert.False(errors.IsEmpty);
        var tree = Tree(errors);
        Assert.Equal("BASE_TYPE_MAX_LENGTH", CodeAt(tree, "embeds", "0", "footer", "text"));
        Assert.Equal("MODEL_TYPE_CONVERT", CodeAt(tree, "embeds", "0"));
        Assert.True(errors.Scope("other").IsEmpty);
    }
}
