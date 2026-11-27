using Enx.NetCord.Testing.Serialization;

namespace Enx.NetCord.Testing.Rest;

/// <summary>Collects field-level validation errors for an <c>Invalid Form Body</c> (50035) response.</summary>
internal sealed class FormErrors
{
    private readonly FormErrorNode _root = new();

    /// <summary>Gets a value indicating whether no error has been added.</summary>
    public bool IsEmpty => _root.Errors.Count == 0 && _root.Children.Count == 0;

    internal FormErrorNode Root => _root;

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
