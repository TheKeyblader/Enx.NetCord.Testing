using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Enx.NetCord.Testing.Conformance;

[Trait("Category", "Conformance")]
public sealed class FakeTimeProviderTests
{
    [Fact]
    public void Advance_WithOneSecond_UpdatesUtcNow()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        TimeSpan step = TimeSpan.FromSeconds(1);

        clock.Advance(step);

        Assert.Equal(DateTimeOffset.UnixEpoch + step, clock.GetUtcNow());
    }
}
