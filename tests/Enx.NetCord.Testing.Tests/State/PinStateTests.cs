using Enx.NetCord.Testing.State;
using Xunit;
using static Enx.NetCord.Testing.Tests.State.StateTestSupport;

namespace Enx.NetCord.Testing.Tests.State;

public sealed class PinStateTests
{
    [Fact]
    public void CreateMessage_Pinned251stInAChannel_FailsWithMaxPinsAndAllocatesNothing()
    {
        var state = NewState();
        var (_, channel, user) = Seed(state);
        for (var i = 0; i < FakeDiscordState.MaxPinsPerChannel; i++)
        {
            state.CreateMessage(StateCaller.Fixture, new CreateMessageInput(channel.Id, user.Id, "p", Pinned: true)).ValueOrThrow();
        }

        var refused = state.CreateMessage(StateCaller.Fixture, new CreateMessageInput(channel.Id, user.Id, "p", Pinned: true));

        Assert.Equal(StateFailureKind.MaxPins, refused.Failure.Kind);
        Assert.Equal(FakeDiscordState.MaxPinsPerChannel, state.ListPins(StateCaller.Fixture, channel.Id, before: null, limit: 1000).Value.Items.Length);
    }

    [Fact]
    public void PinMessage_UnknownPinner_FailsWithUnknownUserAndCommitsNothing()
    {
        var state = NewState();
        var (_, channel, user) = Seed(state);
        var message = state.CreateMessage(StateCaller.Fixture, new CreateMessageInput(channel.Id, user.Id, "x")).Value;
        using var subscription = state.SubscribeGateway();

        var refused = state.PinMessage(StateCaller.Fixture, channel.Id, message.Id, userId: 999);

        Assert.Equal(StateFailureKind.UnknownUser, refused.Failure.Kind);
        Assert.Empty(Drain(subscription));
        Assert.False(state.FindMessage(channel.Id, message.Id)!.Pinned);
    }

    [Fact]
    public void ListPins_BeforeTheOldestPin_ReturnsNothing()
    {
        var state = NewState();
        var (_, channel, user) = Seed(state);
        var message = state.CreateMessage(StateCaller.Fixture, new CreateMessageInput(channel.Id, user.Id, "x")).Value;
        state.PinMessage(StateCaller.Fixture, channel.Id, message.Id, user.Id).ValueOrThrow();
        var pinnedAt = state.FindChannel(channel.Id)!.LastPinTimestamp!.Value;

        var page = state.ListPins(StateCaller.Fixture, channel.Id, before: pinnedAt, limit: 50).Value;

        Assert.Empty(page.Items);
        Assert.False(page.HasMore);
    }
}
