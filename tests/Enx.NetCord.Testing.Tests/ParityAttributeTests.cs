using Xunit;

namespace Enx.NetCord.Testing.Tests;

public sealed class ParityAttributeTests
{
    private const string Key = "bootstrap:placeholder";

    [Fact]
    [Parity(Key)]
    public void GetTraits_OnDiscoveredTest_ExposesParityTrait()
    {
        Assert.Contains(Key, TestContext.Current.Test!.Traits["Parity"]);
    }
}
