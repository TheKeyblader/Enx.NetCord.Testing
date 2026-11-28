using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Enx.NetCord.Testing.Conformance.Contract;

/// <summary>
/// A small JSON Schema validator for the subset of OpenAPI 3.1 used by the Discord spec responses:
/// <c>$ref</c> (to <c>#/components/schemas/*</c>), <c>type</c> (string or array), <c>properties</c>, <c>required</c>,
/// <c>additionalProperties</c>, <c>items</c>, <c>uniqueItems</c>, <c>enum</c>, <c>const</c>, <c>allOf</c>, <c>oneOf</c>,
/// <c>anyOf</c>, <c>pattern</c>, <c>minimum</c>/<c>maximum</c>, <c>minLength</c>/<c>maxLength</c> (in code points) and the formats
/// <c>snowflake</c>, <c>date-time</c>, <c>uri</c>, <c>int32</c> and <c>int64</c> (<c>double</c> and <c>nonce</c> are accepted as plain values).
/// </summary>
/// <remarks>
/// A keyword outside this subset throws instead of being ignored, so a spec sync that introduces a new keyword cannot
/// silently weaken the contract. Unknown formats and annotations (<c>description</c>, <c>title</c>, <c>default</c>…)
/// are accepted.
/// <para>
/// The validation is <b>strict</b>: a property the schema does not declare is rejected, like <c>unevaluatedProperties: false</c> would.
/// A property is declared by <c>properties</c> of the schema, of a <c>$ref</c> target, of any <c>allOf</c> branch (union), or of a
/// <c>oneOf</c>/<c>anyOf</c> branch <i>that validates</i>; <c>additionalProperties</c> (<see langword="true"/> or a schema) declares all the others.
/// The only way to accept a property the spec does not declare is a justified entry of <see cref="ContractException"/>.
/// </para>
/// </remarks>
internal sealed class JsonSchemaValidator(
    IReadOnlyDictionary<string, JsonElement> schemas,
    IReadOnlyList<ContractException>? exceptions = null)
{
    private readonly IReadOnlyList<ContractException> _exceptions = exceptions ?? [];
    private readonly ConcurrentDictionary<ContractException, int> _hits = new();
    private readonly ConcurrentDictionary<(string Operation, string Schema), byte> _evaluated = new();

    // The operation whose success response is being validated; validation is synchronous, so a thread-static scope is enough.
    [ThreadStatic]
    private static string? _currentOperation;

    private const string SchemaPrefix = "#/components/schemas/";

    // A property name no schema declares: DeclaresProperty then only succeeds through additionalProperties.
    private const string NoSuchProperty = "\u0000";

    private static readonly HashSet<string> Annotations =
    [
        "description", "title", "default", "format", "example", "examples", "deprecated", "readOnly", "writeOnly",
    ];

    // "nonce" and "double" carry no constraint beyond their type; the others are checked in CheckString/CheckNumber.
    private static readonly HashSet<string> KnownFormats = ["snowflake", "date-time", "int32", "int64", "double", "uri", "nonce"];

    private static readonly HashSet<string> Supported =
    [
        "$ref", "type", "properties", "required", "additionalProperties", "items", "uniqueItems", "enum", "const",
        "allOf", "oneOf", "anyOf", "pattern", "minimum", "maximum", "maxLength", "minLength",
    ];

    private static readonly Regex DateTimePattern = new(
        "^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}(\\.[0-9]+)?(Z|[+-][0-9]{2}:[0-9]{2})$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>Validates an instance against a schema node.</summary>
    /// <param name="instance">The JSON instance.</param>
    /// <param name="schema">The schema (inline or a <c>$ref</c>).</param>
    /// <returns>The violations, as <c>$.path: message</c>; empty when the instance conforms.</returns>
    public IReadOnlyList<string> Validate(JsonElement instance, JsonElement schema) => Validate(instance, schema, null);

    /// <summary>Validates an instance against a schema node, recording which component schemas its objects were evaluated against.</summary>
    /// <param name="instance">The JSON instance.</param>
    /// <param name="schema">The schema (inline or a <c>$ref</c>).</param>
    /// <param name="operation">The <c>METHOD /template</c> key of the success response being validated, or <see langword="null"/>.</param>
    /// <returns>The violations, as <c>$.path: message</c>; empty when the instance conforms.</returns>
    public IReadOnlyList<string> Validate(JsonElement instance, JsonElement schema, string? operation)
    {
        var errors = new List<string>();
        var previous = _currentOperation;
        _currentOperation = operation;
        try
        {
            Check(instance, schema, "$", errors);
        }
        finally
        {
            _currentOperation = previous;
        }

        return errors;
    }

    /// <summary>Validates an instance against a named component schema.</summary>
    /// <param name="instance">The JSON instance.</param>
    /// <param name="schemaName">The component schema name.</param>
    /// <returns>The violations; empty when the instance conforms.</returns>
    public IReadOnlyList<string> ValidateNamed(JsonElement instance, string schemaName) => ValidateNamed(instance, schemaName, null);

    /// <summary>Validates an instance against a named component schema, recording the evaluations like <see cref="Validate(JsonElement, JsonElement, string?)"/>.</summary>
    /// <param name="instance">The JSON instance.</param>
    /// <param name="schemaName">The component schema name.</param>
    /// <param name="operation">The <c>METHOD /template</c> key of the success response being validated, or <see langword="null"/>.</param>
    /// <returns>The violations; empty when the instance conforms.</returns>
    public IReadOnlyList<string> ValidateNamed(JsonElement instance, string schemaName, string? operation)
    {
        Resolve(SchemaPrefix + schemaName); // Fails early on an unknown name.
        using var reference = JsonDocument.Parse(new JsonObject { ["$ref"] = SchemaPrefix + schemaName }.ToJsonString());
        return Validate(instance, reference.RootElement, operation); // Through the $ref, so the schema name is part of the messages and of the exception scope.
    }

    /// <summary>
    /// Returns whether a validated success response of an operation contained an object evaluated against a component schema
    /// (directly, through <c>$ref</c>, <c>allOf</c> or a branch): the only situation where an exception on that schema could have been needed.
    /// </summary>
    /// <param name="operation">The <c>METHOD /template</c> key.</param>
    /// <param name="schemaName">The component schema name.</param>
    /// <returns><see langword="true"/> when such an object was evaluated.</returns>
    public bool WasEvaluated(string operation, string schemaName) => _evaluated.ContainsKey((operation, schemaName));

    /// <summary>Gets the exceptions that accepted at least one undeclared property since this validator was created.</summary>
    public IReadOnlyCollection<ContractException> UsedExceptions => [.. _hits.Keys];

    /// <summary>Returns whether the extract has a component schema.</summary>
    /// <param name="schemaName">The component schema name.</param>
    /// <returns><see langword="true"/> when the schema exists.</returns>
    public bool HasSchema(string schemaName) => schemas.ContainsKey(schemaName);

    /// <summary>Returns whether the spec itself declares a property on a component schema, directly or through <c>$ref</c>, <c>allOf</c>, <c>oneOf</c> or <c>anyOf</c>.</summary>
    /// <param name="schemaName">The component schema name.</param>
    /// <param name="property">The JSON property name.</param>
    /// <returns><see langword="true"/> when some branch of the schema lists the property.</returns>
    public bool Declares(string schemaName, string property)
        => DeclaresProperty(Resolve(SchemaPrefix + schemaName), property, [SchemaPrefix + schemaName]);

    /// <summary>Returns whether the spec lets a component schema carry any property (<c>additionalProperties</c> <see langword="true"/> or a schema, or an empty schema).</summary>
    /// <param name="schemaName">The component schema name.</param>
    /// <returns><see langword="true"/> when some branch accepts arbitrary properties.</returns>
    public bool AcceptsAnyProperty(string schemaName)
        => DeclaresProperty(Resolve(SchemaPrefix + schemaName), NoSuchProperty, [SchemaPrefix + schemaName]);

    private bool DeclaresProperty(JsonElement schema, string property, HashSet<string> visited)
    {
        if (schema.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (schema.TryGetProperty("properties", out var properties) && properties.TryGetProperty(property, out _))
        {
            return true;
        }

        if (schema.TryGetProperty("additionalProperties", out var additional) && additional.ValueKind is not JsonValueKind.False)
        {
            return true; // The spec accepts any property.
        }

        if (schema.TryGetProperty("$ref", out var reference))
        {
            var target = reference.GetString()!;
            if (visited.Add(target) && DeclaresProperty(Resolve(target), property, visited))
            {
                return true;
            }
        }

        foreach (var combinator in new[] { "allOf", "oneOf", "anyOf" })
        {
            if (schema.TryGetProperty(combinator, out var branches)
                && branches.EnumerateArray().Any(branch => DeclaresProperty(branch, property, visited)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Walks every schema of the extract, including nested ones, and lists what the validator would reject up front:
    /// keywords outside the supported subset, types it does not know, and <c>$ref</c>s that point nowhere.
    /// </summary>
    /// <returns>One line per problem; empty when the whole extract can be validated.</returns>
    public IReadOnlyList<string> FindUnsupported()
    {
        var problems = new List<string>();
        foreach (var (name, schema) in schemas)
        {
            Scan(schema, name, problems);
        }

        return problems;
    }

    /// <summary>Scans one inline schema (for example an operation response) the way <see cref="FindUnsupported()"/> scans components.</summary>
    /// <param name="schema">The schema.</param>
    /// <param name="path">A label used to locate problems.</param>
    /// <returns>One line per problem; empty when the schema can be validated.</returns>
    public IReadOnlyList<string> FindUnsupported(JsonElement schema, string path)
    {
        var problems = new List<string>();
        Scan(schema, path, problems);
        return problems;
    }

    private void Scan(JsonElement schema, string path, List<string> problems)
    {
        if (schema.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var keyword in schema.EnumerateObject())
        {
            var here = $"{path}.{keyword.Name}";
            switch (keyword.Name)
            {
                case "properties":
                    foreach (var property in keyword.Value.EnumerateObject())
                    {
                        Scan(property.Value, $"{path}.{property.Name}", problems);
                    }

                    break;
                case "items" or "additionalProperties":
                    Scan(keyword.Value, here, problems);
                    break;
                case "allOf" or "oneOf" or "anyOf":
                    foreach (var branch in keyword.Value.EnumerateArray())
                    {
                        Scan(branch, here, problems);
                    }

                    break;
                case "$ref":
                    var target = keyword.Value.GetString()!;
                    if (!target.StartsWith(SchemaPrefix, StringComparison.Ordinal) || !schemas.ContainsKey(target[SchemaPrefix.Length..]))
                    {
                        problems.Add($"{here}: unresolved reference '{target}'");
                    }

                    break;
                case "format":
                    if (keyword.Value.ValueKind != JsonValueKind.String || !KnownFormats.Contains(keyword.Value.GetString()!))
                    {
                        problems.Add($"{here}: unsupported format {keyword.Value.GetRawText()}");
                    }

                    break;
                case "type":
                    var types = keyword.Value.ValueKind == JsonValueKind.Array
                        ? keyword.Value.EnumerateArray().Select(static t => t.GetString()!)
                        : [keyword.Value.GetString()!];
                    foreach (var type in types.Where(static t => t is not ("object" or "array" or "string" or "boolean" or "null" or "number" or "integer")))
                    {
                        problems.Add($"{here}: unsupported type '{type}'");
                    }

                    break;
                default:
                    if (!Supported.Contains(keyword.Name) && !Annotations.Contains(keyword.Name))
                    {
                        problems.Add($"{here}: unsupported keyword '{keyword.Name}'");
                    }

                    break;
            }
        }
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

    /// <summary>What a schema (with its <c>$ref</c>s and combinator branches) declared about the properties of one object instance.</summary>
    private sealed class Evaluation
    {
        public HashSet<string> Names { get; } = new(StringComparer.Ordinal);

        public List<string> Schemas { get; } = [];

        public bool Open { get; set; }

        public void Merge(Evaluation other)
        {
            Names.UnionWith(other.Names);
            Schemas.AddRange(other.Schemas);
            Open |= other.Open;
        }
    }

    /// <summary>Checks an instance against the schema of one JSON node, then rejects the properties nothing declared.</summary>
    private void Check(JsonElement instance, JsonElement schema, string path, List<string> errors)
    {
        var evaluation = new Evaluation();
        Evaluate(instance, schema, path, errors, evaluation);
        if (IsUnconstrained(schema, []))
        {
            evaluation.Open = true; // {} / annotations only / true, as the schema of the node itself: any value, so any property.
        }

        if (instance.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (_currentOperation is { } operation)
        {
            foreach (var name in evaluation.Schemas)
            {
                _evaluated[(operation, name)] = 0;
            }
        }

        if (!evaluation.Open)
        {
            ReportUndeclared(instance, path, errors, evaluation);
        }
    }

    private bool IsUnconstrained(JsonElement schema, HashSet<string> visited)
    {
        if (schema.ValueKind == JsonValueKind.True)
        {
            return true;
        }

        if (schema.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var reference = default(string);
        foreach (var keyword in schema.EnumerateObject())
        {
            if (keyword.Name == "$ref")
            {
                reference = keyword.Value.GetString();
            }
            else if (!Annotations.Contains(keyword.Name))
            {
                return false;
            }
        }

        // A bare $ref to an unconstrained component is itself unconstrained.
        return reference is null || (visited.Add(reference) && IsUnconstrained(Resolve(reference), visited));
    }

    private void ReportUndeclared(JsonElement instance, string path, List<string> errors, Evaluation evaluation)
    {
        foreach (var property in instance.EnumerateObject())
        {
            if (evaluation.Names.Contains(property.Name))
            {
                continue;
            }

            var exception = _exceptions.FirstOrDefault(e => e.Property == property.Name && evaluation.Schemas.Contains(e.Schema, StringComparer.Ordinal));
            if (exception is not null)
            {
                _hits.AddOrUpdate(exception, 1, static (_, n) => n + 1);
                continue;
            }

            var schemaNames = evaluation.Schemas.Count == 0 ? "an inline schema" : string.Join(", ", evaluation.Schemas.Distinct(StringComparer.Ordinal));
            errors.Add($"{path}: undeclared property '{property.Name}' (the spec schema {schemaNames} does not declare it; "
                + "fix the fake, or register a justified entry in ContractExceptions)");
        }
    }

    private void Evaluate(JsonElement instance, JsonElement schema, string path, List<string> errors, Evaluation evaluation)
    {
        if (schema.ValueKind == JsonValueKind.True)
        {
            return; // As a branch, the true schema declares no property (it is open only as the schema of a node, see Check).
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
            var target = reference.GetString()!;
            evaluation.Schemas.Add(target[SchemaPrefix.Length..]);
            Evaluate(instance, Resolve(target), path, errors, evaluation);
        }

        if (schema.TryGetProperty("type", out var type) && !CheckType(instance, type, path, errors))
        {
            return; // The other keywords are meaningless on a value of the wrong type.
        }

        CheckValue(instance, schema, path, errors);
        CheckCombinators(instance, schema, path, errors, evaluation);

        switch (instance.ValueKind)
        {
            case JsonValueKind.Object:
                CheckObject(instance, schema, path, errors, evaluation);
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

    private void CheckCombinators(JsonElement instance, JsonElement schema, string path, List<string> errors, Evaluation evaluation)
    {
        if (schema.TryGetProperty("allOf", out var allOf))
        {
            foreach (var branch in allOf.EnumerateArray())
            {
                Evaluate(instance, branch, path, errors, evaluation); // every branch must hold; what they declare is the union
            }
        }

        if (schema.TryGetProperty("oneOf", out var oneOf))
        {
            var failures = new List<List<string>>();
            var passed = 0;
            foreach (var branch in oneOf.EnumerateArray())
            {
                var branchErrors = new List<string>();
                var branchEvaluation = new Evaluation();
                Evaluate(instance, branch, path, branchErrors, branchEvaluation);
                if (branchErrors.Count == 0)
                {
                    passed++;
                    evaluation.Merge(branchEvaluation);
                }
                else
                {
                    failures.Add(branchErrors);
                }
            }

            if (passed == 0)
            {
                evaluation.Open = true; // No branch declares anything: the failure is reported once, not again as every property undeclared.
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
                var branchEvaluation = new Evaluation();
                Evaluate(instance, branch, path, branchErrors, branchEvaluation);
                if (branchErrors.Count == 0)
                {
                    passed = true;
                    evaluation.Merge(branchEvaluation); // every valid branch declares, not only the first
                }
                else
                {
                    failures.Add(branchErrors);
                }
            }

            if (!passed)
            {
                evaluation.Open = true; // As for oneOf: one failure, not one per property.
                errors.Add($"{path}: matches none of the anyOf branches: {Summarize(failures)}");
            }
        }
    }

    private static string Summarize(List<List<string>> failures)
        => string.Join(" | ", failures.Select(static f => f[0]));

    private void CheckObject(JsonElement instance, JsonElement schema, string path, List<string> errors, Evaluation evaluation)
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
                evaluation.Names.Add(property.Name);
                Check(property.Value, propertySchema, $"{path}.{property.Name}", errors);
            }
            else if (schema.TryGetProperty("additionalProperties", out var additional))
            {
                if (additional.ValueKind == JsonValueKind.False)
                {
                    evaluation.Names.Add(property.Name); // reported here, not a second time as undeclared
                    errors.Add($"{path}: unexpected property '{property.Name}'");
                }
                else if (additional.ValueKind == JsonValueKind.Object)
                {
                    evaluation.Names.Add(property.Name);
                    Check(property.Value, additional, $"{path}.{property.Name}", errors);
                }
                else
                {
                    evaluation.Names.Add(property.Name); // true: the spec accepts any value
                }
            }
        }
    }

    private void CheckArray(JsonElement instance, JsonElement schema, string path, List<string> errors)
    {
        var index = 0;
        var seen = new List<JsonElement>();
        var unique = schema.TryGetProperty("uniqueItems", out var u) && u.ValueKind == JsonValueKind.True;
        foreach (var item in instance.EnumerateArray())
        {
            if (schema.TryGetProperty("items", out var items))
            {
                Check(item, items, $"{path}[{index}]", errors);
            }

            if (unique)
            {
                // Structural equality (number 1 equals 1.0, object key order is irrelevant); quadratic, which is fine for responses.
                if (seen.Any(other => SameJson(other, item)))
                {
                    errors.Add($"{path}[{index}]: duplicate item {Describe(item)}");
                }

                seen.Add(item);
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

        if (schema.TryGetProperty("maxLength", out var max) && value.EnumerateRunes().Count() > max.GetInt32())
        {
            errors.Add($"{path}: longer than {max.GetInt32()} code points");
        }

        if (schema.TryGetProperty("minLength", out var min) && value.EnumerateRunes().Count() < min.GetInt32())
        {
            errors.Add($"{path}: shorter than {min.GetInt32()} code points");
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
                case "uri":
                    if (!Uri.TryCreate(value, UriKind.Absolute, out _))
                    {
                        errors.Add($"{path}: '{value}' is not an absolute URI");
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
