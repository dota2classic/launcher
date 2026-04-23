using d2c_launcher.Services;

namespace d2c_launcher.ViewModels;

public static class RoomClearPolicy
{
    public static bool ShouldResolveAfterQueueState(ReadyState? myLastState, bool isDeclinePending) =>
        myLastState == ReadyState.Pending && !isDeclinePending;

    public static bool ShouldShowTimeoutModal(ReadyState? myLastState, bool isDeclinePending, bool? inQueueAfterRoomClear) =>
        !isDeclinePending &&
        (myLastState == ReadyState.Timeout ||
         (myLastState == ReadyState.Pending && inQueueAfterRoomClear != true));
}
