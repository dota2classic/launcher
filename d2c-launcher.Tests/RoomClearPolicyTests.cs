using d2c_launcher.Services;
using d2c_launcher.ViewModels;
using Xunit;

namespace d2c_launcher.Tests;

public sealed class RoomClearPolicyTests
{
    [Fact]
    public void ShouldShowTimeoutModal_when_my_state_timed_out()
    {
        var shouldShow = RoomClearPolicy.ShouldShowTimeoutModal(ReadyState.Timeout, isDeclinePending: false);

        Assert.True(shouldShow);
    }

    [Fact]
    public void ShouldNotShowTimeoutModal_when_someone_else_declined_while_i_was_pending()
    {
        var shouldShow = RoomClearPolicy.ShouldShowTimeoutModal(ReadyState.Pending, isDeclinePending: false);

        Assert.False(shouldShow);
    }

    [Fact]
    public void ShouldNotShowTimeoutModal_when_i_am_already_declining()
    {
        var shouldShow = RoomClearPolicy.ShouldShowTimeoutModal(ReadyState.Timeout, isDeclinePending: true);

        Assert.False(shouldShow);
    }
}
