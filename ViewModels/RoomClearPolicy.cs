using d2c_launcher.Services;

namespace d2c_launcher.ViewModels;

public static class RoomClearPolicy
{
    public static bool ShouldShowTimeoutModal(ReadyState? myLastState, bool isDeclinePending) =>
        myLastState == ReadyState.Timeout && !isDeclinePending;
}
