using Enx.NetCord.Testing.State;
using Xunit;
using static Enx.NetCord.Testing.Tests.State.StateTestSupport;

namespace Enx.NetCord.Testing.Tests.State;

public sealed class UserUpdateTests
{
    [Fact]
    public void UpdateUser_UnknownUser_FailsWithoutACommit()
    {
        var state = NewState();
        using var subscription = state.SubscribeGateway();

        var result = state.UpdateUser(new UpdateUserInput(999, "x"));

        Assert.Equal(new StateFailure(StateFailureKind.UnknownUser, 999), result.Failure);
        Assert.Empty(Drain(subscription));
    }

    [Fact]
    public void UpdateUser_ChangedValues_CommitsOneUserUpdatedWithTheNewSnapshot()
    {
        var state = NewState();
        var user = state.CreateUser(new CreateUserInput("alice"));
        using var subscription = state.SubscribeGateway();

        var updated = state.UpdateUser(new UpdateUserInput(user.Id, "alicia", ChangeAvatar: true, AvatarHash: "h", ChangeBanner: true, BannerHash: "b")).Value;

        Assert.Equal(("alicia", "h", "b"), (updated.Username, updated.AvatarHash, updated.BannerHash));
        Assert.Equal(updated, state.FindUser(user.Id));
        Assert.Equal(updated, Assert.IsType<UserUpdated>(Assert.Single(Drain(subscription))).User);
    }

    [Fact]
    public void UpdateUser_SameValuesOrNothing_CommitsNothing()
    {
        var state = NewState();
        var user = state.CreateUser(new CreateUserInput("alice", AvatarHash: "h"));
        using var subscription = state.SubscribeGateway();

        state.UpdateUser(new UpdateUserInput(user.Id));
        state.UpdateUser(new UpdateUserInput(user.Id, "alice", ChangeAvatar: true, AvatarHash: "h", ChangeBanner: true, BannerHash: null));

        Assert.Empty(Drain(subscription));
    }
}
