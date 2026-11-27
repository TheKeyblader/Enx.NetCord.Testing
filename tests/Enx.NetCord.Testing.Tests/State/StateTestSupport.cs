using Enx.NetCord.Testing.State;
using Microsoft.Extensions.Time.Testing;

namespace Enx.NetCord.Testing.Tests.State;

internal static class StateTestSupport
{
    internal static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    internal static FakeDiscordState NewState(TimeProvider? clock = null, ISnowflakeGenerator? ids = null) =>
        new(clock ?? new FakeTimeProvider(Start), ids ?? new DefaultSnowflakeGenerator());

    internal static ulong IdAt(DateTimeOffset instant, ulong counter = 0) =>
        ((ulong)Snowflake.MillisecondsOf(instant)! << Snowflake.CounterBits) | counter;

    internal static List<StateChange> Drain(ChangeSubscription subscription)
    {
        var changes = new List<StateChange>();
        while (subscription.Reader.TryRead(out var change))
        {
            changes.Add(change);
        }

        return changes;
    }

    /// <summary>A guild with a text channel, a user and that user's membership.</summary>
    internal static (GuildSnapshot Guild, ChannelSnapshot Channel, UserSnapshot User) Seed(FakeDiscordState state)
    {
        var guild = state.CreateGuild(new CreateGuildInput("guild")).Value.Guild;
        var channel = state.CreateTextChannel(new CreateTextChannelInput(guild.Id, "general")).Value;
        var user = state.CreateUser(new CreateUserInput("alice"));
        state.AddMember(new AddMemberInput(guild.Id, user.Id)).ValueOrThrow();
        return (guild, channel, user);
    }
}

/// <summary>Generator driven by a delegate; counts calls.</summary>
internal sealed class ScriptedGenerator(Func<DateTimeOffset, ulong> next) : ISnowflakeGenerator
{
    internal int Calls { get; private set; }

    public ulong Next(DateTimeOffset timestamp)
    {
        Calls++;
        return next(timestamp);
    }
}

/// <summary>Clock that counts reads of <see cref="GetUtcNow"/>.</summary>
internal sealed class CountingTimeProvider(DateTimeOffset now) : TimeProvider
{
    internal int Reads { get; private set; }

    public override DateTimeOffset GetUtcNow()
    {
        Reads++;
        return now;
    }
}

/// <summary>Clock whose time can be set freely, including backwards.</summary>
internal sealed class SettableTimeProvider(DateTimeOffset now) : TimeProvider
{
    internal DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}
