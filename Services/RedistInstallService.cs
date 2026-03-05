using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using d2c_launcher.Util;

namespace d2c_launcher.Services;

/// <summary>
/// Finds and silently installs DirectX and Visual C++ redistributable packages
/// from the game's _CommonRedist directory.
/// </summary>
public class RedistInstallService
{
    /// <summary>
    /// Runs all redistributable installers found under <c>{gameDirectory}\_CommonRedist</c>.
    /// Each installer is run with silent/quiet flags.
    /// </summary>
    /// <returns>True if at least one installer was found and launched; false if nothing to do.</returns>
    public async Task<bool> InstallAsync(
        string gameDirectory,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var redistDir = Path.Combine(gameDirectory, "_CommonRedist");
        if (!Directory.Exists(redistDir))
            return false;

        var installers = FindInstallers(redistDir);
        if (installers.Count == 0)
            return false;

        foreach (var path in installers)
        {
            ct.ThrowIfCancellationRequested();

            var name = Path.GetFileName(path);

            if (IsAlreadyInstalled(path))
            {
                AppLog.Info($"[Redist] Skipping {name} — already installed");
                continue;
            }

            progress?.Report(name);
            AppLog.Info($"[Redist] Running {path}");

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true,
                };

                using var proc = Process.Start(psi);
                if (proc != null)
                    await proc.WaitForExitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Don't block the user from launching — log and continue
                AppLog.Info($"[Redist] Failed to run {name}: {ex.Message}");
            }
        }

        return true;
    }

    private static bool IsAlreadyInstalled(string installerPath)
    {
        var name = Path.GetFileName(installerPath);

        if (name.Equals("DXSETUP.exe", StringComparison.OrdinalIgnoreCase))
            return IsDxSetupInstalled();

        if (name.StartsWith("vcredist_", StringComparison.OrdinalIgnoreCase))
            return IsVcRedistInstalled(installerPath);

        return false;
    }

    /// <summary>
    /// Checks for the runtime DLL installed by the given vcredist exe.
    /// Reads the installer's file version to determine which DLL to look for,
    /// so this works regardless of which VC++ year the installer is for.
    /// </summary>
    private static bool IsVcRedistInstalled(string installerPath)
    {
        try
        {
            var info = FileVersionInfo.GetVersionInfo(installerPath);
            var dllName = info.FileMajorPart switch
            {
                10 => "msvcr100.dll",  // VC++ 2010
                11 => "msvcr110.dll",  // VC++ 2012
                12 => "msvcr120.dll",  // VC++ 2013
                >= 14 => "vcruntime140.dll", // VC++ 2015–2022
                _ => null
            };
            if (dllName == null) return false;

            var isX64 = installerPath.Contains("x64", StringComparison.OrdinalIgnoreCase);
            var sysDir = isX64
                ? Environment.GetFolderPath(Environment.SpecialFolder.System)
                : Environment.GetFolderPath(Environment.SpecialFolder.SystemX86);

            return File.Exists(Path.Combine(sysDir, dllName));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks for d3dx9_43.dll — the latest D3DX9 helper DLL required by Source 1 engine.
    /// It is not part of Windows and is only present after running DXSETUP.
    /// </summary>
    private static bool IsDxSetupInstalled()
    {
        var sys32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
        return File.Exists(Path.Combine(sys32, "d3dx9_43.dll"));
    }

    private static List<string> FindInstallers(string redistDir)
    {
        var result = new List<string>();

        // DirectX
        var dxSetup = Path.Combine(redistDir, "DirectX", "DXSETUP.exe");
        if (File.Exists(dxSetup))
            result.Add(dxSetup);

        // Visual C++ redistributables — search all version subdirectories
        var vcredistDir = Path.Combine(redistDir, "vcredist");
        if (Directory.Exists(vcredistDir))
        {
            result.AddRange(Directory.EnumerateFiles(vcredistDir, "vcredist_x64.exe", SearchOption.AllDirectories));
            result.AddRange(Directory.EnumerateFiles(vcredistDir, "vcredist_x86.exe", SearchOption.AllDirectories));
        }

        return result;
    }
}
