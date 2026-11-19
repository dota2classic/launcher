using System.Reflection;

namespace d2c_launcher.Util;

public static class AppVersion
{
    public static string Current { get; } = Resolve();

    private static string Resolve()
    {
        var asm = typeof(AppVersion).Assembly;
        return asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion?.Split('+')[0]
               ?? asm.GetName().Version?.ToString(3)
               ?? "0.0.0";
    }
}
