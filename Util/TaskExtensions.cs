using System;
using System.Threading.Tasks;

namespace d2c_launcher.Util;

internal static class TaskExtensions
{
    internal static async void FireAndForget(this Task task, string context)
    {
        try { await task; }
        catch (Exception ex) { AppLog.Error($"FireAndForget({context})", ex); }
    }
}
