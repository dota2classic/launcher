using System.Diagnostics;
using System.IO;

namespace d2c_launcher.Util;

/// <summary>Gets the executable path of a process. May return null if access is denied.</summary>
public static class ProcessPathHelper
{
    public static string? TryGetExecutablePath(Process process)
    {
        if (process == null)
            return null;
        try
        {
            var path = process.MainModule?.FileName;
            if (!string.IsNullOrEmpty(path))
                return Path.GetFullPath(path);
        }
        catch
        {
            // Access denied or process exited
        }
        return null;
    }
}
