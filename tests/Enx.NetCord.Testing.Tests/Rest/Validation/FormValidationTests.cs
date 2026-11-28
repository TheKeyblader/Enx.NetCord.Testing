using System.Text;
using System.Text.Json;
using Enx.NetCord.Testing.Rest;
using Enx.NetCord.Testing.State;
using Xunit;

namespace Enx.NetCord.Testing.Tests.Rest.Validation;

public sealed class FormValidationTests
{
    private static FakeRestRequest Request(string? body = null, params (string Name, string Value)[] routeParameters)
        => new(
            new RouteOperation("POST", "/x", null, RouteSecurity.Bot),
            routeParameters.ToDictionary(p => p.Name, p => p.Value),
            [],
            body is null ? null : new RestRequestBody(Encoding.UTF8.GetBytes(body), "application/json"),
            RestCaller.Anonymous,
            FakeDiscordState.Create());

    private static JsonElement ErrorTree(DiscordError error)
    {
        using var document = JsonDocument.Parse(error.ToJson());
        return document.RootElement.GetProperty("errors").Clone();
    }

    private static string FirstCode(JsonElement tree, params string[] path)
    {
        var node = tree;
        foreach (var part in path)
        {
            node = node.GetProperty(part);
        }

        return node.GetProperty("_errors")[0].GetProperty("code").GetString()!;
    }

    [Theory]
    [InlineData("0", 0UL)]
    [InlineData("123456789012345678", 123456789012345678UL)]
    [InlineData("18446744073709551615", ulong.MaxValue)]
    public void TryParseSnowflake_Digits_ReturnsTheId(string raw, ulong expected)
    {
        Assert.True(FormValidation.TryParseSnowflake(raw, out var id));
        Assert.Equal(expected, id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("-1")]
    [InlineData("+1")]
    [InlineData(" 1")]
    [InlineData("1.5")]
    [InlineData("18446744073709551616")]
    public void TryParseSnowflake_NotASnowflake_ReturnsFalse(string raw)
    {
        Assert.False(FormValidation.TryParseSnowflake(raw, out _));
    }

    [Fact]
    public void NotSnowflakeMessage_Value_QuotesTheRawText()
    {
        Assert.Equal("Value \"abc\" is not snowflake.", FormValidation.NotSnowflakeMessage("abc"));
    }

    [Fact]
    public void ValidateRouteSnowflake_ValidParameter_CarriesTheParsedId()
    {
        var validation = FormValidation.ValidateRouteSnowflake(Request(null, ("channel_id", "42")), "channel_id");

        Assert.False(validation.IsInvalid);
        Assert.Equal(42UL, validation.Payload);
    }

    [Fact]
    public void ValidateRouteSnowflake_InvalidParameter_Returns50035AtTheParameterPath()
    {
        var validation = FormValidation.ValidateRouteSnowflake(Request(null, ("channel_id", "abc")), "channel_id");

        Assert.True(validation.IsInvalid);
        Assert.Equal(50035, validation.Error.Code);
        var tree = ErrorTree(validation.Error);
        Assert.Equal("NUMBER_TYPE_COERCE", FirstCode(tree, "channel_id"));
        Assert.Equal(
            "Value \"abc\" is not snowflake.",
            tree.GetProperty("channel_id").GetProperty("_errors")[0].GetProperty("message").GetString());
    }

    [Fact]
    public void TryReadJsonObject_Object_ReturnsTheDocument()
    {
        var ok = FormValidation.TryReadJsonObject(Request("{\"a\":1}"), out var document, out var error);

        Assert.True(ok);
        Assert.Null(error);
        using (document)
        {
            Assert.Equal(1, document!.RootElement.GetProperty("a").GetInt32());
        }
    }

    [Fact]
    public void TryReadJsonObject_NoBody_Returns50109()
    {
        var ok = FormValidation.TryReadJsonObject(Request(), out var document, out var error);

        Assert.False(ok);
        Assert.Null(document);
        Assert.Equal(50109, error!.Code);
    }

    [Fact]
    public void TryReadJsonObject_MalformedJson_Returns50109()
    {
        var ok = FormValidation.TryReadJsonObject(Request("{"), out var document, out var error);

        Assert.False(ok);
        Assert.Null(document);
        Assert.Equal(50109, error!.Code);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("\"text\"")]
    [InlineData("12")]
    [InlineData("null")]
    public void TryReadJsonObject_NonObjectRoot_Returns50035ModelTypeConvertAtTheRoot(string body)
    {
        var ok = FormValidation.TryReadJsonObject(Request(body), out var document, out var error);

        Assert.False(ok);
        Assert.Null(document);
        Assert.Equal(50035, error!.Code);
        Assert.Equal("MODEL_TYPE_CONVERT", ErrorTree(error).GetProperty("_errors")[0].GetProperty("code").GetString());
        Assert.Equal(
            "Only dictionaries may be used in a ModelType",
            ErrorTree(error).GetProperty("_errors")[0].GetProperty("message").GetString());
    }

    [Fact]
    public void ReadOptionalString_AbsentOrString_ReturnsTheValueWithoutErrors()
    {
        using var document = JsonDocument.Parse("{\"name\":\"bob\"}");
        var errors = new FormErrors();

        Assert.Equal("bob", FormValidation.ReadOptionalString(document.RootElement, "name", errors));
        Assert.Null(FormValidation.ReadOptionalString(document.RootElement, "other", errors));
        Assert.True(errors.IsEmpty);
    }

    [Theory]
    [InlineData("{\"name\":1}")]
    [InlineData("{\"name\":null}")]
    [InlineData("{\"name\":[]}")]
    public void ReadOptionalString_WrongType_AddsBaseTypeBadType(string body)
    {
        using var document = JsonDocument.Parse(body);
        var errors = new FormErrors();

        Assert.Null(FormValidation.ReadOptionalString(document.RootElement, "name", errors));

        var tree = ErrorTree(DiscordError.InvalidFormBody(errors));
        Assert.Equal("BASE_TYPE_BAD_TYPE", FirstCode(tree, "name"));
        Assert.Equal(
            "Value must be of type string.",
            tree.GetProperty("name").GetProperty("_errors")[0].GetProperty("message").GetString());
    }

    [Theory]
    [InlineData("ab", true)]
    [InlineData("a", false)]
    [InlineData("abcd", false)]
    [InlineData("😀😀", true)]
    [InlineData("😀", false)]
    public void CheckLength_ValueAroundBounds_CountsCodePointsInclusively(string value, bool expected)
    {
        var errors = new FormErrors();

        Assert.Equal(expected, FormValidation.CheckLength(value, "name", 2, 3, errors));

        Assert.Equal(expected, errors.IsEmpty);
        if (!expected)
        {
            var tree = ErrorTree(DiscordError.InvalidFormBody(errors));
            Assert.Equal("BASE_TYPE_BAD_LENGTH", FirstCode(tree, "name"));
            Assert.Equal(
                "Must be between 2 and 3 in length.",
                tree.GetProperty("name").GetProperty("_errors")[0].GetProperty("message").GetString());
        }
    }

    [Theory]
    [InlineData("\"123\"", 123UL)]
    [InlineData("123", 123UL)]
    public void TryReadSnowflake_StringOrNumber_ReturnsTheId(string json, ulong expected)
    {
        using var document = JsonDocument.Parse(json);
        var errors = new FormErrors();

        Assert.True(FormValidation.TryReadSnowflake(document.RootElement, "id", errors, out var id));
        Assert.Equal(expected, id);
        Assert.True(errors.IsEmpty);
    }

    [Theory]
    [InlineData("\"abc\"", "Value \"abc\" is not snowflake.")]
    [InlineData("1.5", "Value \"1.5\" is not snowflake.")]
    [InlineData("true", "Value \"true\" is not snowflake.")]
    [InlineData("[]", "Value \"[]\" is not snowflake.")]
    public void TryReadSnowflake_NotASnowflake_AddsNumberTypeCoerce(string json, string message)
    {
        using var document = JsonDocument.Parse(json);
        var errors = new FormErrors();

        Assert.False(FormValidation.TryReadSnowflake(document.RootElement, "id", errors, out _));

        var tree = ErrorTree(DiscordError.InvalidFormBody(errors));
        Assert.Equal("NUMBER_TYPE_COERCE", FirstCode(tree, "id"));
        Assert.Equal(message, tree.GetProperty("id").GetProperty("_errors")[0].GetProperty("message").GetString());
    }

    [Theory]
    [InlineData("{\"t\":\"x\"}", true, "x")]
    [InlineData("{\"t\":null}", true, null)]
    [InlineData("{}", false, null)]
    public void ReadNullableString_StringNullOrAbsent_DistinguishesClearedFromNotSent(string body, bool expectedPresent, string? expected)
    {
        using var document = JsonDocument.Parse(body);
        var errors = new FormErrors();

        Assert.Equal(expected, FormValidation.ReadNullableString(document.RootElement, "t", errors, out var present));

        Assert.Equal(expectedPresent, present);
        Assert.True(errors.IsEmpty);
    }

    [Fact]
    public void ReadNullableString_WrongType_AddsBaseTypeBadType()
    {
        using var document = JsonDocument.Parse("{\"t\":3}");
        var errors = new FormErrors();

        Assert.Null(FormValidation.ReadNullableString(document.RootElement, "t", errors, out var present));

        Assert.False(present);
        Assert.Equal("BASE_TYPE_BAD_TYPE", FirstCode(ErrorTree(DiscordError.InvalidFormBody(errors)), "t"));
    }

    [Theory]
    [InlineData("{\"n\":5}", 5)]
    [InlineData("{\"n\":\"7\"}", 7)]
    [InlineData("{\"n\":0}", 0)]
    [InlineData("{\"n\":10}", 10)]
    [InlineData("{\"n\":null}", null)]
    [InlineData("{}", null)]
    public void ReadOptionalInteger_ValidOrAbsent_ReturnsTheValue(string body, int? expected)
    {
        using var document = JsonDocument.Parse(body);
        var errors = new FormErrors();

        Assert.Equal(expected, FormValidation.ReadOptionalInteger(document.RootElement, "n", 0, 10, errors));
        Assert.True(errors.IsEmpty);
    }

    [Theory]
    [InlineData("{\"n\":-1}", "NUMBER_TYPE_MIN", "int value should be greater than or equal to 0.")]
    [InlineData("{\"n\":11}", "NUMBER_TYPE_MAX", "int value should be less than or equal to 10.")]
    [InlineData("{\"n\":99999999999}", "NUMBER_TYPE_MAX", "int value should be less than or equal to 10.")]
    [InlineData("{\"n\":1.5}", "NUMBER_TYPE_COERCE", "Value \"1.5\" is not int.")]
    [InlineData("{\"n\":\"abc\"}", "NUMBER_TYPE_COERCE", "Value \"abc\" is not int.")]
    [InlineData("{\"n\":true}", "NUMBER_TYPE_COERCE", "Value \"true\" is not int.")]
    [InlineData("{\"n\":[]}", "NUMBER_TYPE_COERCE", "Value \"[]\" is not int.")]
    public void ReadOptionalInteger_InvalidValue_AddsTheNumberError(string body, string code, string message)
    {
        using var document = JsonDocument.Parse(body);
        var errors = new FormErrors();

        Assert.Null(FormValidation.ReadOptionalInteger(document.RootElement, "n", 0, 10, errors));

        var tree = ErrorTree(DiscordError.InvalidFormBody(errors));
        Assert.Equal(code, FirstCode(tree, "n"));
        Assert.Equal(message, tree.GetProperty("n").GetProperty("_errors")[0].GetProperty("message").GetString());
    }

    [Theory]
    [InlineData("{\"b\":true}", true)]
    [InlineData("{\"b\":false}", false)]
    [InlineData("{\"b\":null}", null)]
    [InlineData("{}", null)]
    public void ReadOptionalBoolean_ValidOrAbsent_ReturnsTheValue(string body, bool? expected)
    {
        using var document = JsonDocument.Parse(body);
        var errors = new FormErrors();

        Assert.Equal(expected, FormValidation.ReadOptionalBoolean(document.RootElement, "b", errors));
        Assert.True(errors.IsEmpty);
    }

    [Theory]
    [InlineData("{\"b\":\"yes\"}", "Value \"yes\" is not boolean.")]
    [InlineData("{\"b\":1}", "Value \"1\" is not boolean.")]
    public void ReadOptionalBoolean_WrongType_AddsBooleanTypeCoerce(string body, string message)
    {
        using var document = JsonDocument.Parse(body);
        var errors = new FormErrors();

        Assert.Null(FormValidation.ReadOptionalBoolean(document.RootElement, "b", errors));

        var tree = ErrorTree(DiscordError.InvalidFormBody(errors));
        Assert.Equal("BOOLEAN_TYPE_COERCE", FirstCode(tree, "b"));
        Assert.Equal(message, tree.GetProperty("b").GetProperty("_errors")[0].GetProperty("message").GetString());
    }

    [Fact]
    public void AddBadType_NestedPath_AddsBaseTypeBadType()
    {
        var errors = new FormErrors();
        FormValidation.AddBadType(errors, "tags.0", "array");

        var tree = ErrorTree(DiscordError.InvalidFormBody(errors));
        Assert.Equal("BASE_TYPE_BAD_TYPE", FirstCode(tree, "tags", "0"));
        Assert.Equal(
            "Value must be of type array.",
            tree.GetProperty("tags").GetProperty("0").GetProperty("_errors")[0].GetProperty("message").GetString());
    }

    [Fact]
    public void AddRequired_Field_AddsBaseTypeRequired()
    {
        var errors = new FormErrors();
        FormValidation.AddRequired(errors, "name");

        var tree = ErrorTree(DiscordError.InvalidFormBody(errors));
        Assert.Equal("BASE_TYPE_REQUIRED", FirstCode(tree, "name"));
        Assert.Equal(
            "This field is required",
            tree.GetProperty("name").GetProperty("_errors")[0].GetProperty("message").GetString());
    }
}
