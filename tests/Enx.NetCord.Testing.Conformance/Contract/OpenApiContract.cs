using System.Globalization;
using System.Text.Json;
using Enx.NetCord.Testing.Rest;

namespace Enx.NetCord.Testing.Conformance.Contract;

/// <summary>
/// The response contract of the covered operations, read from the embedded extract of the pinned Discord OpenAPI spec
/// (<c>Contract/openapi-contract.json</c>, produced by <c>extract-openapi-contract.py</c>).
/// </summary>
internal sealed class OpenApiContract
{
    private const string ResourceName = "Enx.NetCord.Testing.Conformance.openapi-contract.json";

    private static readonly Lazy<OpenApiContract> Instance = new(Load);

    private readonly JsonDocument _document;
    private readonly JsonSchemaValidator _validator;

    private OpenApiContract(JsonDocument document)
    {
        _document = document;
        var schemas = document.RootElement.GetProperty("schemas")
            .EnumerateObject()
            .ToDictionary(static p => p.Name, static p => p.Value, StringComparer.Ordinal);
        _validator = new JsonSchemaValidator(schemas);
    }

    /// <summary>Gets the contract embedded in the test assembly.</summary>
    public static OpenApiContract Current => Instance.Value;

    /// <summary>Gets the <c>discord-api-spec</c> commit the extract was produced from.</summary>
    public string Commit => _document.RootElement.GetProperty("source").GetProperty("commit").GetString()!;

    /// <summary>Gets the keys (<c>METHOD /template</c>) of the operations whose responses the extract describes.</summary>
    public IEnumerable<string> OperationKeys
        => _document.RootElement.GetProperty("operations").EnumerateObject().Select(static p => p.Name);

    /// <summary>Gets the validator over the extracted component schemas.</summary>
    public JsonSchemaValidator Validator => _validator;

    /// <summary>Lists what the validator could not handle in the extract: component schemas and inline operation responses.</summary>
    /// <returns>One line per problem; empty when everything can be validated.</returns>
    public IReadOnlyList<string> FindUnsupported()
    {
        var problems = new List<string>(_validator.FindUnsupported());
        foreach (var operation in _document.RootElement.GetProperty("operations").EnumerateObject())
        {
            foreach (var response in operation.Value.GetProperty("responses").EnumerateObject())
            {
                problems.AddRange(_validator.FindUnsupported(response.Value, $"{operation.Name} {response.Name}"));
            }
        }

        return problems;
    }

    /// <summary>Returns whether the extract describes an operation.</summary>
    /// <param name="key">The <c>METHOD /template</c> key.</param>
    /// <returns><see langword="true"/> when the operation is in the extract.</returns>
    public bool Covers(string key) => _document.RootElement.GetProperty("operations").TryGetProperty(key, out _);

    /// <summary>Validates one completed REST attempt against the contract.</summary>
    /// <param name="request">The recorded attempt.</param>
    /// <returns>The violations; empty when the attempt conforms.</returns>
    /// <remarks>
    /// A success status must be documented for the operation and its body must match the schema (no body for a response
    /// the spec documents without content). A 429 body must match <c>RatelimitedResponse</c>, any other 4xx/5xx body the Discord error envelope.
    /// </remarks>
    public IReadOnlyList<string> Validate(RecordedRestRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var key = $"{request.Method} {request.RouteTemplate}";
        if (request.StatusCode is not { } status)
        {
            return [$"{key}: the attempt produced no response"];
        }

        var code = (int)status;
        if (code >= 400)
        {
            // The spec documents 429 with its own envelope (retry_after, global); every other 4xx with ErrorResponse.
            var envelope = code == 429 ? "RatelimitedResponse" : "ErrorResponse";
            return request.ResponseBody is { } error
                ? Prefix(key, _validator.ValidateNamed(error, envelope))
                : [$"{key}: {code} response has no JSON error body"];
        }

        if (!_document.RootElement.GetProperty("operations").TryGetProperty(key, out var operation))
        {
            // A route NetCord calls but the spec does not document has no contract to check.
            return RouteTable.Default.TryGetEntry(key, out var entry) && !entry.Operation.InSpecification
                ? []
                : [$"{key}: the operation is not in the contract extract"];
        }

        var responses = operation.GetProperty("responses");
        var statusText = code.ToString(CultureInfo.InvariantCulture);
        if (!responses.TryGetProperty(statusText, out var schema))
        {
            var documented = string.Join(", ", responses.EnumerateObject().Select(static p => p.Name));
            return [$"{key}: status {statusText} is not documented (spec: {documented})"];
        }

        if (schema.ValueKind == JsonValueKind.String)
        {
            return []; // A non-JSON body (an image): only the status is part of the contract.
        }

        if (schema.ValueKind == JsonValueKind.Null)
        {
            return request.ResponseBody is null ? [] : [$"{key}: the spec documents {statusText} without a body, the fake sent one"];
        }

        return request.ResponseBody is { } body
            ? Prefix(key, _validator.Validate(body, schema))
            : [$"{key}: the spec documents a JSON body for {statusText}, the fake sent none"];
    }

    private static List<string> Prefix(string key, IReadOnlyList<string> violations)
        => [.. violations.Select(v => $"{key} {v}")];

    private static OpenApiContract Load()
    {
        using var stream = typeof(OpenApiContract).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' is missing.");
        return new OpenApiContract(JsonDocument.Parse(stream));
    }
}
