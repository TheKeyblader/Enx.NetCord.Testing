using Xunit.v3;

namespace Enx.NetCord.Testing;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
internal sealed class ParityAttribute(string key) : Attribute, ITraitAttribute
{
    public IReadOnlyCollection<KeyValuePair<string, string>> GetTraits() =>
        [new KeyValuePair<string, string>("Parity", key)];
}
