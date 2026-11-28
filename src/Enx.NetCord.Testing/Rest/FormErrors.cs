using Enx.NetCord.Testing.Serialization;

namespace Enx.NetCord.Testing.Rest;

/// <summary>Collects field-level validation errors for an <c>Invalid Form Body</c> (50035) response.</summary>
internal sealed class FormErrors
{
    private readonly FormErrorNode _root = new();
    private readonly FormErrors? _parent;
    private readonly string _prefix = string.Empty;
    private int _scopedCount;

    /// <summary>Initializes a new, empty collector.</summary>
    public FormErrors()
    {
    }

    private FormErrors(FormErrors parent, string prefix)
    {
        _parent = parent;
        _prefix = prefix;
    }

    /// <summary>Gets a value indicating whether no error has been added (through this collector, for a <see cref="Scope"/>).</summary>
    public bool IsEmpty => _parent is null ? _root.Errors.Count == 0 && _root.Children.Count == 0 : _scopedCount == 0;

    internal FormErrorNode Root => _root;

    /// <summary>
    /// Returns a view that adds its errors under <paramref name="prefix"/> in this collector, so the <c>Read*</c> helpers of
    /// <see cref="FormValidation"/> can read a nested object with its own property names (<c>embeds.0</c>, <c>allowed_mentions</c>…).
    /// </summary>
    /// <param name="prefix">The dot-separated path of the nested object.</param>
    /// <returns>The scoped view; its <see cref="IsEmpty"/> only reflects what was added through it.</returns>
    public FormErrors Scope(string prefix)
    {
        ArgumentException.ThrowIfNullOrEmpty(prefix);
        return new FormErrors(this, prefix);
    }

    /// <summary>Adds an error at a dot-separated path of property names and decimal indices.</summary>
    /// <param name="path">The path, such as <c>embeds.0.title</c>; empty for the root.</param>
    /// <param name="code">The Discord text code, such as <c>BASE_TYPE_MAX_LENGTH</c>.</param>
    /// <param name="message">The message.</param>
    /// <returns>This instance, for chaining.</returns>
    public FormErrors Add(string path, string code, string message)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentException.ThrowIfNullOrEmpty(code);
        ArgumentException.ThrowIfNullOrEmpty(message);

        if (_parent is not null)
        {
            _scopedCount++;
            _parent.Add(path.Length == 0 ? _prefix : $"{_prefix}.{path}", code, message);
            return this;
        }

        var node = _root;
        if (path.Length > 0)
        {
            foreach (var part in path.Split('.'))
            {
                ArgumentException.ThrowIfNullOrEmpty(part, nameof(path));
                node = node.GetOrAddChild(part);
            }
        }

        node.Errors.Add(new FieldError(code, message));
        return this;
    }
}
