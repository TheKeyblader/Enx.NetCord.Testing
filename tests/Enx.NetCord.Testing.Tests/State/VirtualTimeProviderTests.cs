using Enx.NetCord.Testing.State;
using Xunit;

namespace Enx.NetCord.Testing.Tests.State;

public sealed class VirtualTimeProviderTests
{
    [Fact]
    public void GetUtcNow_Always_ReturnsTheFixedDefaultInstant()
    {
        var clock = new VirtualTimeProvider();

        Assert.Equal(new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero), clock.GetUtcNow());
        Assert.Equal(clock.GetUtcNow(), clock.GetUtcNow());
    }

    [Fact]
    public async Task CreateTimer_WithDueTime_NeverFires()
    {
        var clock = new VirtualTimeProvider();
        var fired = false;

        await using var timer = clock.CreateTimer(_ => fired = true, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(1));

        Assert.True(timer.Change(TimeSpan.Zero, TimeSpan.Zero));
        Assert.False(fired);
        Assert.Equal(0, clock.GetTimestamp());
    }
}
