namespace Enx.NetCord.Testing.Rest;

/// <summary>
/// The immutable inventory of REST operations and their explicitly registered handlers (ADR 0005).
/// Resolution is an ordinal, whole-path match: a literal segment beats <c>{parameter}</c> at the first
/// position that distinguishes two templates, and the method is only looked up on the winning template.
/// </summary>
internal sealed class RouteTable
{
    private static readonly Lazy<RouteTable> DefaultTable = new(
        static () => Create(RouteInventory.All, []));

    private readonly Dictionary<string, RouteEntry> _byKey;
    private readonly Dictionary<int, List<TemplateNode>> _bySegmentCount;

    private RouteTable(Dictionary<string, RouteEntry> byKey, Dictionary<int, List<TemplateNode>> bySegmentCount)
    {
        _byKey = byKey;
        _bySegmentCount = bySegmentCount;
    }

    /// <summary>Gets the table built from the embedded inventory and the handlers registered so far.</summary>
    public static RouteTable Default => DefaultTable.Value;

    /// <summary>Gets every inventoried entry.</summary>
    public IEnumerable<RouteEntry> Entries => _byKey.Values;

    /// <summary>Checks the inventory and bindings without building a table.</summary>
    /// <param name="operations">The inventoried operations.</param>
    /// <param name="bindings">The handler bindings.</param>
    /// <returns>The problems found; empty when the table is valid.</returns>
    public static IReadOnlyList<string> Validate(IEnumerable<RouteOperation> operations, IEnumerable<RouteBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentNullException.ThrowIfNull(bindings);

        var problems = new List<string>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var shapes = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var operation in operations)
        {
            if (string.IsNullOrEmpty(operation.Method) || operation.Method != operation.Method.ToUpperInvariant())
            {
                problems.Add($"operation '{operation.Key}' has a method that is not upper-case");
            }

            if (!TryParseTemplate(operation.Template, out var segments, out var syntaxProblem))
            {
                problems.Add($"operation '{operation.Key}' has an invalid template: {syntaxProblem}");
                continue;
            }

            if (!keys.Add(operation.Key))
            {
                problems.Add($"duplicate operation '{operation.Key}'");
            }

            var shape = string.Join('/', segments.Select(static s => s.IsParameter ? "{}" : s.Value));
            if (shapes.TryGetValue(shape, out var existing))
            {
                if (!string.Equals(existing, operation.Template, StringComparison.Ordinal))
                {
                    problems.Add($"ambiguous templates '{existing}' and '{operation.Template}' differ only by parameter names");
                }
            }
            else
            {
                shapes[shape] = operation.Template;
            }
        }

        var bound = new HashSet<string>(StringComparer.Ordinal);
        foreach (var binding in bindings)
        {
            if (!keys.Contains(binding.Key))
            {
                problems.Add($"binding '{binding.Key}' has no inventory entry");
            }
            else if (!bound.Add(binding.Key))
            {
                problems.Add($"duplicate binding '{binding.Key}'");
            }
        }

        return problems;
    }

    /// <summary>Builds a table, failing when <see cref="Validate"/> reports a problem.</summary>
    /// <param name="operations">The inventoried operations.</param>
    /// <param name="bindings">The handler bindings.</param>
    /// <returns>The table.</returns>
    /// <exception cref="RouteTableException">The check failed.</exception>
    public static RouteTable Create(IEnumerable<RouteOperation> operations, IEnumerable<RouteBinding> bindings)
    {
        var operationList = operations.ToList();
        var bindingList = bindings.ToList();
        var problems = Validate(operationList, bindingList);
        if (problems.Count > 0)
        {
            throw new RouteTableException(problems);
        }

        var handlers = bindingList.ToDictionary(static b => b.Key, static b => b.Handler, StringComparer.Ordinal);
        var byKey = new Dictionary<string, RouteEntry>(StringComparer.Ordinal);
        var nodes = new Dictionary<string, TemplateNode>(StringComparer.Ordinal);
        foreach (var operation in operationList)
        {
            handlers.TryGetValue(operation.Key, out var handler);
            var entry = new RouteEntry(operation, handler);
            byKey[operation.Key] = entry;

            if (!nodes.TryGetValue(operation.Template, out var node))
            {
                TryParseTemplate(operation.Template, out var segments, out _);
                node = new TemplateNode(operation.Template, segments);
                nodes[operation.Template] = node;
            }

            node.Methods[operation.Method] = entry;
        }

        var bySegmentCount = new Dictionary<int, List<TemplateNode>>();
        foreach (var node in nodes.Values)
        {
            node.Seal();
            if (!bySegmentCount.TryGetValue(node.Segments.Length, out var list))
            {
                list = [];
                bySegmentCount[node.Segments.Length] = list;
            }

            list.Add(node);
        }

        return new RouteTable(byKey, bySegmentCount);
    }

    /// <summary>Gets an entry by its exact <c>METHOD /template</c> key.</summary>
    /// <param name="key">The key.</param>
    /// <param name="entry">The entry when found.</param>
    /// <returns><see langword="true"/> when the key is inventoried.</returns>
    public bool TryGetEntry(string key, out RouteEntry entry)
    {
        if (_byKey.TryGetValue(key, out var found))
        {
            entry = found;
            return true;
        }

        entry = null!;
        return false;
    }

    /// <summary>Resolves a request against the table.</summary>
    /// <param name="method">The upper-case HTTP method.</param>
    /// <param name="segments">The decoded path segments, after the API prefix.</param>
    /// <returns>The resolution.</returns>
    public RouteResolution Resolve(string method, IReadOnlyList<string> segments)
    {
        if (!_bySegmentCount.TryGetValue(segments.Count, out var candidates))
        {
            return RouteResolution.NotFound();
        }

        TemplateNode? best = null;
        foreach (var candidate in candidates)
        {
            if (!candidate.Matches(segments))
            {
                continue;
            }

            if (best is null || candidate.IsMoreSpecificThan(best))
            {
                best = candidate;
            }
        }

        if (best is null)
        {
            return RouteResolution.NotFound();
        }

        var parameters = best.Extract(segments);
        return best.Methods.TryGetValue(method, out var entry)
            ? RouteResolution.Found(entry, parameters, best.AllowedMethods)
            : RouteResolution.MethodNotAllowed(best.Template, parameters, best.AllowedMethods);
    }

    private static bool TryParseTemplate(string template, out Segment[] segments, out string problem)
    {
        segments = [];
        problem = string.Empty;
        if (string.IsNullOrEmpty(template) || template[0] != '/')
        {
            problem = "a template must start with '/'";
            return false;
        }

        var parts = template.Split('/')[1..];
        var parsed = new List<Segment>(parts.Length);
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var part in parts)
        {
            if (part.Length == 0)
            {
                problem = "empty segment";
                return false;
            }

            if (part.Contains('{') || part.Contains('}'))
            {
                if (part.Length < 3 || part[0] != '{' || part[^1] != '}' || part.AsSpan(1, part.Length - 2).ContainsAny('{', '}'))
                {
                    problem = $"segment '{part}' mixes literal text and a parameter";
                    return false;
                }

                var name = part[1..^1];
                if (!names.Add(name))
                {
                    problem = $"parameter '{name}' is repeated";
                    return false;
                }

                parsed.Add(new Segment(true, name));
            }
            else
            {
                parsed.Add(new Segment(false, part));
            }
        }

        segments = [.. parsed];
        return true;
    }

    private readonly record struct Segment(bool IsParameter, string Value);

    private sealed class TemplateNode(string template, Segment[] segments)
    {
        private string[] _allowed = [];

        public string Template { get; } = template;

        public Segment[] Segments { get; } = segments;

        public Dictionary<string, RouteEntry> Methods { get; } = new(StringComparer.Ordinal);

        public IReadOnlyList<string> AllowedMethods => _allowed;

        public void Seal()
        {
            _allowed = [.. Methods.Keys.Order(StringComparer.Ordinal)];
        }

        public bool Matches(IReadOnlyList<string> path)
        {
            for (var i = 0; i < Segments.Length; i++)
            {
                var segment = Segments[i];
                if (segment.IsParameter)
                {
                    if (path[i].Length == 0)
                    {
                        return false;
                    }
                }
                else if (!string.Equals(segment.Value, path[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        public bool IsMoreSpecificThan(TemplateNode other)
        {
            for (var i = 0; i < Segments.Length; i++)
            {
                var mine = Segments[i].IsParameter;
                var theirs = other.Segments[i].IsParameter;
                if (mine != theirs)
                {
                    return !mine;
                }
            }

            return false;
        }

        public IReadOnlyDictionary<string, string> Extract(IReadOnlyList<string> path)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var i = 0; i < Segments.Length; i++)
            {
                if (Segments[i].IsParameter)
                {
                    result[Segments[i].Value] = path[i];
                }
            }

            return result;
        }
    }
}
