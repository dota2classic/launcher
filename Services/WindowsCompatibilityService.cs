using System;
using System.Runtime.Versioning;
using Microsoft.Win32;
using d2c_launcher.Util;

namespace d2c_launcher.Services;

/// <summary>
/// Manages the Windows Vista compatibility layer for dota.exe via the
/// AppCompatFlags\Layers registry key.
///
/// Why Vista compat fixes rendering on AMD + Windows 10 ≥ 14393:
/// 1. DISABLEDXMAXIMIZEDWINDOWEDMODE shim — suppresses Fullscreen Optimizations (FSO).
///    Windows 10 FSO silently converts D3D9 exclusive fullscreen to a DWM-composited
///    borderless window. On AMD RDNA, this causes the swap chain to never present,
///    leaving the game window transparent (shows desktop). Disabling FSO restores true
///    exclusive fullscreen where the AMD D3D9 driver works correctly.
/// 2. VistaRTMVersionLie shim — GetVersionEx returns Vista 6.0. Source 1 engine's
///    version-gated code then selects the hardware vertex processing path (WDDM 1.0
///    path) instead of D3DCREATE_MIXED_VERTEXPROCESSING, which Windows 10 build 14393+
///    changed to force software vertex processing (causing 1–2 FPS on AMD).
///
/// Risk: not safe for all users — disabling FSO removes G-Sync on NVIDIA monitors.
/// Expose as an explicit opt-in setting; auto-suggest only for AMD + build ≥ 14393.
/// </summary>
[SupportedOSPlatform("windows")]
public static class WindowsCompatibilityService
{
    private const string LayersKeyPath =
        @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers";

    /// <summary>Windows Vista RTM compatibility layer token.</summary>
    private const string VistaCompatValue = "~ VISTARTM";

    /// <summary>
    /// Returns true if dota.exe at <paramref name="exePath"/> has the Vista
    /// compatibility layer set in the current user's AppCompatFlags registry.
    /// </summary>
    public static bool IsVistaCompatEnabled(string exePath)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(LayersKeyPath, writable: false);
            if (key == null) return false;
            var value = key.GetValue(exePath) as string;
            return value != null && value.Contains("VISTA", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            AppLog.Error("[Compat] Failed to read AppCompatFlags", ex);
            return false;
        }
    }

    /// <summary>
    /// Sets or clears the Vista compatibility layer for dota.exe at <paramref name="exePath"/>.
    /// </summary>
    public static void SetVistaCompat(string exePath, bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(LayersKeyPath, writable: true)
                ?? Registry.CurrentUser.CreateSubKey(LayersKeyPath);

            if (enabled)
                key.SetValue(exePath, VistaCompatValue, RegistryValueKind.String);
            else
                key.DeleteValue(exePath, throwOnMissingValue: false);

            AppLog.Info($"[Compat] Vista compat {(enabled ? "enabled" : "disabled")} for {exePath}");
        }
        catch (Exception ex)
        {
            AppLog.Error("[Compat] Failed to write AppCompatFlags", ex);
        }
    }

    /// <summary>
    /// Returns true if the current system is likely affected by the AMD + Windows 10
    /// D3DCREATE_MIXED_VERTEXPROCESSING / FSO rendering bug.
    ///
    /// Heuristic: AMD/Radeon GPU present AND Windows build ≥ 14393 (Anniversary Update —
    /// the build that changed D3DCREATE_MIXED_VERTEXPROCESSING to force software VP).
    /// Source: Microsoft Compatibility Cookbook, "Changes in DX9 legacy support".
    /// </summary>
    public static bool DetectNeedsCompat(HardwareSnapshot hw)
    {
        if (!int.TryParse(hw.OsBuild, out var build) || build < 14393)
            return false;

        foreach (var gpu in hw.Gpus)
        {
            if (gpu.Contains("AMD", StringComparison.OrdinalIgnoreCase) ||
                gpu.Contains("Radeon", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
