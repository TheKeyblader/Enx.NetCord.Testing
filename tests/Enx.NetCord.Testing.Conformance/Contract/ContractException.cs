namespace Enx.NetCord.Testing.Conformance.Contract;

/// <summary>
/// A property the fake may send although the pinned OpenAPI spec does not declare it on <see cref="Schema"/>, because Discord
/// documents it elsewhere. Registered in <see cref="ContractExceptions"/>; the strict validator rejects every other undeclared property.
/// </summary>
/// <param name="Schema">The component schema the spec leaves incomplete (for example <c>GuildMemberResponse</c>); matches any schema reached through a <c>$ref</c>, <c>allOf</c> or a valid <c>oneOf</c> branch at the node that carries the property.</param>
/// <param name="Property">The JSON property name.</param>
/// <param name="Justification">Why the property is legitimate and the spec incomplete, in a sentence.</param>
/// <param name="Reference">The official documentation that declares the property: <c>discord-api-docs</c> path and line (<c>developers/…/resource.mdx:123</c>).</param>
/// <param name="Operation">A route key (<c>METHOD /template</c>) whose 2xx response carries the property in the fake; proves the exception is exercised.</param>
/// <param name="Task">The roadmap task or <c>docs/user-review.md</c> point that tracks the gap (for example <c>PAR-SPEC-01</c>).</param>
internal sealed record ContractException(
    string Schema,
    string Property,
    string Justification,
    string Reference,
    string Operation,
    string Task);
