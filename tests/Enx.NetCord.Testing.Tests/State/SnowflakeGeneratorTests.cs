using Enx.NetCord.Testing.State;
using Xunit;

namespace Enx.NetCord.Testing.Tests.State;

public sealed class SnowflakeGeneratorTests
{
    private static readonly DateTimeOffset Epoch = DateTimeOffset.FromUnixTimeMilliseconds(Snowflake.EpochUnixMilliseconds);
    private static readonly DateTimeOffset Instant = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Next_AtDiscordEpoch_ReturnsOneNotZero()
    {
        var generator = new DefaultSnowflakeGenerator();

        Assert.Equal(1UL, generator.Next(Epoch));
        Assert.Equal(2UL, generator.Next(Epoch));
    }

    [Fact]
    public void Next_FirstOfNewMillisecond_StartsCounterAtZero()
    {
        var generator = new DefaultSnowflakeGenerator();

        var first = generator.Next(Instant);
        var second = generator.Next(Instant);
        var next = generator.Next(Instant.AddMilliseconds(1));

        Assert.Equal(StateTestSupport.IdAt(Instant, 0), first);
        Assert.Equal(StateTestSupport.IdAt(Instant, 1), second);
        Assert.Equal(StateTestSupport.IdAt(Instant.AddMilliseconds(1), 0), next);
    }

    [Fact]
    public void Next_4097thAllocationInOneMillisecond_CarriesIntoProcessField()
    {
        var generator = new DefaultSnowflakeGenerator();
        ulong last = 0;

        for (var i = 0; i < 4097; i++)
        {
            last = generator.Next(Instant);
        }

        var counter = last & Snowflake.MaxCounter;
        Assert.Equal(4096UL, counter);
        Assert.Equal(0UL, counter & 0xFFF); // increment field wrapped
        Assert.Equal(1UL, (counter >> 12) & 0x1F); // carry reached the process field
        Assert.Equal(0UL, counter >> 17); // worker untouched
        Assert.Equal(Instant, Snowflake.CreatedAt(last));
    }

    [Fact]
    public void Next_FullCounterRangeInOneMillisecond_ThenExhausted_Throws()
    {
        var generator = new DefaultSnowflakeGenerator();
        ulong previous = 0;
        for (var i = 0; i <= (int)Snowflake.MaxCounter; i++)
        {
            var id = generator.Next(Instant);
            Assert.True(id > previous);
            previous = id;
        }

        Assert.Equal(Snowflake.MaxCounter, previous & Snowflake.MaxCounter);
        Assert.Throws<InvalidOperationException>(() => generator.Next(Instant));
        Assert.Throws<InvalidOperationException>(() => generator.Next(Instant));

        // A later millisecond is unaffected by the failed attempts.
        Assert.Equal(StateTestSupport.IdAt(Instant.AddMilliseconds(1), 0), generator.Next(Instant.AddMilliseconds(1)));
    }

    [Fact]
    public void Next_MillisecondGoesBack_ThrowsWithoutCorruptingTheGenerator()
    {
        var generator = new DefaultSnowflakeGenerator();
        generator.Next(Instant);

        Assert.Throws<InvalidOperationException>(() => generator.Next(Instant.AddMilliseconds(-1)));

        Assert.Equal(StateTestSupport.IdAt(Instant, 1), generator.Next(Instant));
    }

    [Fact]
    public void Next_BeforeEpoch_Throws()
    {
        var generator = new DefaultSnowflakeGenerator();

        Assert.Throws<InvalidOperationException>(() => generator.Next(Epoch.AddMilliseconds(-1)));
    }

    [Fact]
    public void Next_AfterTheLast42BitMillisecond_Throws()
    {
        var generator = new DefaultSnowflakeGenerator();
        var lastValid = Epoch.AddMilliseconds(Snowflake.MaxMilliseconds);

        var id = generator.Next(lastValid);

        Assert.Equal(ulong.MaxValue & ~Snowflake.MaxCounter, id & ~Snowflake.MaxCounter);
        Assert.Throws<InvalidOperationException>(() => generator.Next(lastValid.AddMilliseconds(1)));
    }

    [Fact]
    public void CreatedAt_OfGeneratedId_ReturnsTheRequestedInstant()
    {
        var generator = new DefaultSnowflakeGenerator();

        Assert.Equal(Instant, Snowflake.CreatedAt(generator.Next(Instant)));
    }
}
