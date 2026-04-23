using d2c_launcher.Services;
using d2c_launcher.ViewModels;
using Xunit;

namespace d2c_launcher.Tests;

public sealed class RoomClearPolicyTests
{
    [Fact]
    public void ShouldResolveAfterQueueState_when_my_state_is_pending_and_i_did_not_decline()
    {
        var shouldResolve = RoomClearPolicy.ShouldResolveAfterQueueState(ReadyState.Pending, isDeclinePending: false);

        Assert.True(shouldResolve);
    }

    [Fact]
    public void ShouldShowTimeoutModal_when_my_state_timed_out()
    {
        var shouldShow = RoomClearPolicy.ShouldShowTimeoutModal(ReadyState.Timeout, isDeclinePending: false, inQueueAfterRoomClear: null);

        Assert.True(shouldShow);
    }

    [Fact]
    public void ShouldNotShowTimeoutModal_when_someone_else_declined_and_i_was_requeued()
    {
        var shouldShow = RoomClearPolicy.ShouldShowTimeoutModal(ReadyState.Pending, isDeclinePending: false, inQueueAfterRoomClear: true);

        Assert.False(shouldShow);
    }

    [Fact]
    public void ShouldShowTimeoutModal_when_i_was_pending_and_not_requeued()
    {
        var shouldShow = RoomClearPolicy.ShouldShowTimeoutModal(ReadyState.Pending, isDeclinePending: false, inQueueAfterRoomClear: false);
 
        Assert.True(shouldShow);
    }

    [Fact]
    public void ShouldNotShowTimeoutModal_when_i_am_already_declining()
    {
        var shouldShow = RoomClearPolicy.ShouldShowTimeoutModal(ReadyState.Pending, isDeclinePending: true, inQueueAfterRoomClear: false);

        Assert.False(shouldShow);
    }
}
