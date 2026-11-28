using System.Text.RegularExpressions;

namespace Enx.NetCord.Testing.Conformance.Contract;

/// <summary>
/// The single list of properties the fake may send although the pinned spec does not declare them.
/// </summary>
/// <remarks>
/// Rule: an undeclared property is a bug of the fake unless the official documentation (<c>discord-api-docs</c>) declares it, in which
/// case the spec is incomplete and an entry is added here. Each entry needs a verifiable <see cref="ContractException.Reference"/>
/// (file and line in <c>discord-api-docs</c>), the <see cref="ContractException.Operation"/> that exercises it and a
/// <see cref="ContractException.Task"/>. <c>ContractExceptionsTests</c> fails for an entry the spec now declares, one nothing
/// exercises any more, or one without justification: there are no dead exceptions.
/// </remarks>
internal static partial class ContractExceptions
{
    /// <summary>Gets the exceptions applied to every conformance scenario.</summary>
    public static IReadOnlyList<ContractException> Default { get; } = [];

    /// <summary>
    /// Statically audits a list of exceptions against the contract: every field filled in, the reference pointing at a line of
    /// <c>discord-api-docs</c>, the schema and operation present in the extract, no duplicate, and the property really undeclared by the spec.
    /// </summary>
    /// <param name="exceptions">The list to audit.</param>
    /// <param name="contract">The contract the exceptions apply to.</param>
    /// <returns>One line per problem; empty when the list is sound.</returns>
    public static IReadOnlyList<string> Audit(IReadOnlyList<ContractException> exceptions, OpenApiContract contract)
    {
        ArgumentNullException.ThrowIfNull(exceptions);
        ArgumentNullException.ThrowIfNull(contract);

        var problems = new List<string>();
        foreach (var (schema, property, justification, reference, operation, task) in exceptions)
        {
            var label = $"{schema}.{property}";
            if (string.IsNullOrWhiteSpace(schema) || string.IsNullOrWhiteSpace(property))
            {
                problems.Add($"{label}: schema and property are required");
                continue;
            }

            if (string.IsNullOrWhiteSpace(justification))
            {
                problems.Add($"{label}: a justification is required");
            }

            if (string.IsNullOrWhiteSpace(task))
            {
                problems.Add($"{label}: a task or user-review point is required");
            }

            if (!DocsReference().IsMatch(reference ?? string.Empty))
            {
                problems.Add($"{label}: the reference '{reference}' must be a discord-api-docs file and line (developers/….mdx:123)");
            }

            if (!contract.Covers(operation ?? string.Empty))
            {
                problems.Add($"{label}: the operation '{operation}' is not in the contract extract");
            }

            if (!contract.Validator.HasSchema(schema))
            {
                problems.Add($"{label}: the spec has no schema '{schema}'");
            }
            else if (contract.Validator.Declares(schema, property))
            {
                problems.Add($"{label}: the spec now declares this property, remove the exception");
            }
        }

        problems.AddRange(exceptions.GroupBy(static e => (e.Schema, e.Property)).Where(static g => g.Count() > 1).Select(static g => $"{g.Key.Schema}.{g.Key.Property}: registered more than once"));
        return problems;
    }

    [GeneratedRegex(@"^developers/.+\.mdx?:[1-9][0-9]*$", RegexOptions.CultureInvariant)]
    private static partial Regex DocsReference();
}
