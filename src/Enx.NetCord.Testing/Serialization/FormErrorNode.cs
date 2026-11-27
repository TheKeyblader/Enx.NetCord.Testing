using System.Text.Json;
using System.Text.Json.Serialization;

namespace Enx.NetCord.Testing.Serialization;

/// <summary>One validation message of a field: an upper-case Discord code and a human message.</summary>
/// <param name="Code">The text code, such as <c>BASE_TYPE_MAX_LENGTH</c>.</param>
/// <param name="Message">The message.</param>
internal sealed record FieldError(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("message")] string Message);

/// <summary>
/// A node of the <c>errors</c> tree of an <c>Invalid Form Body</c> error: child objects keyed by property name
/// or decimal index, and an optional <c>_errors</c> list at this node.
/// </summary>
[JsonConverter(typeof(FormErrorNodeConverter))]
internal sealed class FormErrorNode
{
    /// <summary>Gets the messages attached to this node, written as <c>_errors</c>.</summary>
    public List<FieldError> Errors { get; } = [];

    /// <summary>Gets the child nodes in insertion order.</summary>
    public List<KeyValuePair<string, FormErrorNode>> Children { get; } = [];

    /// <summary>Gets a child node, creating it when absent.</summary>
    /// <param name="name">The property name or decimal index.</param>
    /// <returns>The child node.</returns>
    public FormErrorNode GetOrAddChild(string name)
    {
        foreach (var child in Children)
        {
            if (string.Equals(child.Key, name, StringComparison.Ordinal))
            {
                return child.Value;
            }
        }

        var node = new FormErrorNode();
        Children.Add(new KeyValuePair<string, FormErrorNode>(name, node));
        return node;
    }
}

/// <summary>Writes a <see cref="FormErrorNode"/> in the Discord wire shape; reading is not needed by the server.</summary>
internal sealed class FormErrorNodeConverter : JsonConverter<FormErrorNode>
{
    /// <inheritdoc />
    public override FormErrorNode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => throw new NotSupportedException("The fake never reads form error trees.");

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, FormErrorNode value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        if (value.Errors.Count > 0)
        {
            writer.WriteStartArray("_errors");
            foreach (var error in value.Errors)
            {
                writer.WriteStartObject();
                writer.WriteString("code", error.Code);
                writer.WriteString("message", error.Message);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        foreach (var child in value.Children)
        {
            writer.WritePropertyName(child.Key);
            Write(writer, child.Value, options);
        }

        writer.WriteEndObject();
    }
}
