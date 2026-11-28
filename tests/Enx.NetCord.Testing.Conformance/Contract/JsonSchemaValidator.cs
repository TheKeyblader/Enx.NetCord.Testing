using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Enx.NetCord.Testing.Conformance.Contract;

/// <summary>
/// A small JSON Schema validator for the subset of OpenAPI 3.1 used by the Discord spec responses:
/// <c>$ref</c> (to <c>#/components/schemas/*</c>), <c>type</c> (string or array), <c>properties</c>, <c>required</c>,
/// <c>additionalProperties</c>, <c>items</c>, <c>uniqueItems</c>, <c>enum</c>, <c>const</c>, <c>allOf</c>, <c>oneOf</c>,
/// <c>anyOf</c>, <c>pattern</c>, <c>minimum</c>/<c>maximum</c>, <c>maxLength</c> and the formats
/// <c>snowflake</c>, <c>date-time</c>, <c>int32</c> and <c>int64</c>.
/// </summary>
/// <remarks>
/// A keyword outside this subset throws instead of being ignored, so a spec sync that introduces a new keyword cannot
/// silently weaken the contract. Unknown formats and annotations (<c>description</c>, <c>title</c>, <c>default</c>…)
/// are accepted. Undeclared properties are not rejected unless <c>additionalProperties</c> says so, as in the spec.
/// </remarks>
internal sealed class JsonSchemaValidator(IReadOnlyDictionary<string, JsonElement> schemas)
{
    private const string SchemaPrefix = "#/components/schemas/";

    private static readonly HashSet<string> Annotations =
    [
        "description", "title", "default", "format", "example", "examples", "deprecated", "readOnly", "writeOnly",
    ];

    private static readonly HashSet<string> Supported =
    [
        "$ref", "type", "properties", "required", "additionalProperties", "items", "uniqueItems", "enum", "const",
        "allOf", "oneOf", "anyOf", "pattern", "minimum", "maximum", "maxLength", "minLength",
    ];

    private static readonly Regex DateTimePattern = new(
        @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?(Z|[+-]\d{2}:\d{2})$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>Validates an instance against a schema node.</summary>
    /// <param name="instance">The JSON instance.</param>
    /// <param name="schema">The schema (inline or a <c>$ref</c>).</param>
    /// <returns>The violations, as <c>$.path: message</c>; empty when the instance conforms.</returns>
    public IReadOnlyList<string> Validate(JsonElement instance, JsonElement schema)
    {
        var errors = new List<string>();
        Check(instance, schema, "$", errors);
        return errors;
    }

    /// <summary>Validates an instance against a named component schema.</summary>
    /// <param name="instance">The JSON instance.</param>
    /// <param name="schemaName">The component schema name.</param>
    /// <returns>The violations; empty when the instance conforms.</returns>
    public IReadOnlyList<string> ValidateNamed(JsonElement instance, string schemaName)
    {
        var errors = new List<string>();
        Check(instance, Resolve(SchemaPrefix + schemaName), "$", errors);
        return errors;
    }

    private static string Describe(JsonElement instance)
    {
        var raw = instance.GetRawText();
        return raw.Length > 60 ? raw[..60] + "…" : raw;
    }

    private static bool TypeMatches(JsonElement instance, string type) => type switch
    {
        "object" => instance.ValueKind == JsonValueKind.Object,
        "array" => instance.ValueKind == JsonValueKind.Array,
        "string" => instance.ValueKind == JsonValueKind.String,
        "boolean" => instance.ValueKind is JsonValueKind.True or JsonValueKind.False,
        "null" => instance.ValueKind == JsonValueKind.Null,
        "number" => instance.ValueKind == JsonValueKind.Number,
        "integer" => instance.ValueKind == JsonValueKind.Number && IsIntegral(instance),
        _ => throw new NotSupportedException($"Unsupported schema type '{type}'."),
    };

    private static bool IsIntegral(JsonElement number)
        => number.TryGetInt64(out _) || (number.TryGetDecimal(out var d) && d == decimal.Truncate(d));

    private static bool SameJson(JsonElement a, JsonElement b)
        => JsonNode.DeepEquals(JsonNode.Parse(a.GetRawText()), JsonNode.Parse(b.GetRawText()));

    private JsonElement Resolve(string reference)
    {
        if (!reference.StartsWith(SchemaPrefix, StringComparison.Ordinal))
        {
            throw new NotSupportedException($"Unsupported reference '{reference}'.");
        }

        var name = reference[SchemaPrefix.Length..];
        return schemas.TryGetValue(name, out var schema)
            ? schema
            : throw new InvalidOperationException($"The contract extract has no schema '{name}'; re-run the extraction script.");
    }

    private void Check(JsonElement instance, JsonElement schema, string path, List<string> errors)
    {
        if (schema.ValueKind == JsonValueKind.True)
        {
            return;
        }

        if (schema.ValueKind == JsonValueKind.False)
        {
            errors.Add($"{path}: no value is allowed here");
            return;
        }

        foreach (var keyword in schema.EnumerateObject())
        {
            if (!Supported.Contains(keyword.Name) && !Annotations.Contains(keyword.Name))
            {
                throw new NotSupportedException($"Unsupported schema keyword '{keyword.Name}' at {path}.");
            }
        }

        if (schema.TryGetProperty("$ref", out var reference))
        {
            Check(instance, Resolve(reference.GetString()!), path, errors);
        }

        if (schema.TryGetProperty("type", out var type) && !CheckType(instance, type, path, errors))
        {
            return; // The other keywords are meaningless on a value of the wrong type.
        }

        CheckValue(instance, schema, path, errors);
        CheckCombinators(instance, schema, path, errors);

        switch (instance.ValueKind)
        {
            case JsonValueKind.Object:
                CheckObject(instance, schema, path, errors);
                break;
            case JsonValueKind.Array:
                CheckArray(instance, schema, path, errors);
                break;
            case JsonValueKind.String:
                CheckString(instance, schema, path, errors);
                break;
            case JsonValueKind.Number:
                CheckNumber(instance, schema, path, errors);
                break;
        }
    }

    private bool CheckType(JsonElement instance, JsonElement type, string path, List<string> errors)
    {
        var allowed = type.ValueKind == JsonValueKind.Array
            ? type.EnumerateArray().Select(static t => t.GetString()!).ToArray()
            : [type.GetString()!];

        if (allowed.Any(t => TypeMatches(instance, t)))
        {
            return true;
        }

        errors.Add($"{path}: expected {string.Join(" or ", allowed)}, found {instance.ValueKind} {Describe(instance)}");
        return false;
    }

    private static void CheckValue(JsonElement instance, JsonElement schema, string path, List<string> errors)
    {
        if (schema.TryGetProperty("enum", out var values) && !values.EnumerateArray().Any(v => SameJson(v, instance)))
        {
            errors.Add($"{path}: {Describe(instance)} is not one of {values.GetRawText()}");
        }

        if (schema.TryGetProperty("const", out var constant) && !SameJson(constant, instance))
        {
            errors.Add($"{path}: expected {constant.GetRawText()}, found {Describe(instance)}");
        }
    }

    private void CheckCombinators(JsonElement instance, JsonElement schema, string path, List<string> errors)
    {
        if (schema.TryGetProperty("allOf", out var allOf))
        {
            foreach (var branch in allOf.EnumerateArray())
            {
                Check(instance, branch, path, errors);
            }
        }

        if (schema.TryGetProperty("oneOf", out var oneOf))
        {
            var failures = new List<List<string>>();
            var passed = 0;
            foreach (var branch in oneOf.EnumerateArray())
            {
                var branchErrors = new List<string>();
                Check(instance, branch, path, branchErrors);
                if (branchErrors.Count == 0)
                {
                    passed++;
                }
                else
                {
                    failures.Add(branchErrors);
                }
            }

            if (passed == 0)
            {
                errors.Add($"{path}: matches none of the oneOf branches: {Summarize(failures)}");
            }
            else if (passed > 1)
            {
                errors.Add($"{path}: matches {passed} oneOf branches, expected exactly one");
            }
        }

        if (schema.TryGetProperty("anyOf", out var anyOf))
        {
            var failures = new List<List<string>>();
            var passed = false;
            foreach (var branch in anyOf.EnumerateArray())
            {
                var branchErrors = new List<string>();
                Check(instance, branch, path, branchErrors);
                if (branchErrors.Count == 0)
                {
                    passed = true;
                    break;
                }

                failures.Add(branchErrors);
            }

            if (!passed)
            {
                errors.Add($"{path}: matches none of the anyOf branches: {Summarize(failures)}");
            }
        }
    }

    private static string Summarize(List<List<string>> failures)
        => string.Join(" | ", failures.Select(static f => f[0]));

    private void CheckObject(JsonElement instance, JsonElement schema, string path, List<string> errors)
    {
        JsonElement properties = default;
        var hasProperties = schema.TryGetProperty("properties", out properties);

        if (schema.TryGetProperty("required", out var required))
        {
            foreach (var name in required.EnumerateArray().Select(static r => r.GetString()!))
            {
                if (!instance.TryGetProperty(name, out _))
                {
                    errors.Add($"{path}: missing required property '{name}'");
                }
            }
        }

        foreach (var property in instance.EnumerateObject())
        {
            if (hasProperties && properties.TryGetProperty(property.Name, out var propertySchema))
            {
                Check(property.Value, propertySchema, $"{path}.{property.Name}", errors);
            }
            else if (schema.TryGetProperty("additionalProperties", out var additional))
            {
                if (additional.ValueKind == JsonValueKind.False)
                {
                    errors.Add($"{path}: unexpected property '{property.Name}'");
                }
                else if (additional.ValueKind == JsonValueKind.Object)
                {
                    Check(property.Value, additional, $"{path}.{property.Name}", errors);
                }
            }
        }
    }

    private void CheckArray(JsonElement instance, JsonElement schema, string path, List<string> errors)
    {
        var index = 0;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var unique = schema.TryGetProperty("uniqueItems", out var u) && u.ValueKind == JsonValueKind.True;
        foreach (var item in instance.EnumerateArray())
        {
            if (schema.TryGetProperty("items", out var items))
            {
                Check(item, items, $"{path}[{index}]", errors);
            }

            if (unique && !seen.Add(item.GetRawText()))
            {
                errors.Add($"{path}[{index}]: duplicate item {Describe(item)}");
            }

            index++;
        }
    }

    private static void CheckString(JsonElement instance, JsonElement schema, string path, List<string> errors)
    {
        var value = instance.GetString()!;

        if (schema.TryGetProperty("pattern", out var pattern)
            && !Regex.IsMatch(value, pattern.GetString()!, RegexOptions.CultureInvariant))
        {
            errors.Add($"{path}: '{value}' does not match {pattern.GetString()}");
        }

        if (schema.TryGetProperty("maxLength", out var max) && new StringInfo(value).LengthInTextElements > max.GetInt32())
        {
            errors.Add($"{path}: longer than {max.GetInt32()} characters");
        }

        if (schema.TryGetProperty("minLength", out var min) && new StringInfo(value).LengthInTextElements < min.GetInt32())
        {
            errors.Add($"{path}: shorter than {min.GetInt32()} characters");
        }

        if (schema.TryGetProperty("format", out var format))
        {
            switch (format.GetString())
            {
                case "snowflake":
                    if (!ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out _))
                    {
                        errors.Add($"{path}: '{value}' is not an unsigned 64-bit snowflake");
                    }

                    break;
                case "date-time":
                    if (!DateTimePattern.IsMatch(value) || !DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                    {
                        errors.Add($"{path}: '{value}' is not an RFC 3339 date-time");
                    }

                    break;
            }
        }
    }

    private static void CheckNumber(JsonElement instance, JsonElement schema, string path, List<string> errors)
    {
        var value = instance.GetDecimal();
        if (schema.TryGetProperty("minimum", out var min) && value < min.GetDecimal())
        {
            errors.Add($"{path}: {value} is below the minimum {min.GetDecimal()}");
        }

        if (schema.TryGetProperty("maximum", out var max) && value > max.GetDecimal())
        {
            errors.Add($"{path}: {value} is above the maximum {max.GetDecimal()}");
        }

        if (schema.TryGetProperty("format", out var format))
        {
            switch (format.GetString())
            {
                case "int32" when !instance.TryGetInt32(out _):
                    errors.Add($"{path}: {value} does not fit in an int32");
                    break;
                case "int64" when !instance.TryGetInt64(out _):
                    errors.Add($"{path}: {value} does not fit in an int64");
                    break;
            }
        }
    }
}
