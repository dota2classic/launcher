<#
.SYNOPSIS
    Build and screenshot a single Avalonia component in isolation (Storybook-like).

.PARAMETER Component
    Name of the component to preview. Run without arguments to see available names.

.PARAMETER WaitSeconds
    Seconds to wait for the window to appear (default: 10).

.EXAMPLE
    pwsh tools/preview.ps1 PartyPanel
    pwsh tools/preview.ps1 GameSearchPanel
    pwsh tools/preview.ps1 AcceptGameModal -WaitSeconds 15
#>
param(
    [Parameter(Position = 0)]
    [string]$Component = "",

    [int]$WaitSeconds = 10
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path $PSScriptRoot -Parent
$exePath  = Join-Path $repoRoot "bin\Debug\net10.0-windows10.0.17763.0\d2c-launcher.exe"
$outDir   = Join-Path $PSScriptRoot "screenshots"

# --- Clean up old screenshots ---
if (Test-Path $outDir) {
    Get-ChildItem -Path $outDir -Filter "*.png" | Remove-Item -Force
}

# --- Kill any existing instance ---
Get-Process -Name "d2c-launcher" -ErrorAction SilentlyContinue | Stop-Process -Force

# --- Build (incremental) ---
Write-Host "Building..." -ForegroundColor Cyan
$buildStart = Get-Date
& dotnet build "$repoRoot\d2c-launcher.csproj" -c Debug --nologo -v quiet
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed."
    exit 1
}
$buildMs = [int]((Get-Date) - $buildStart).TotalMilliseconds
Write-Host "Build: $($buildMs)ms" -ForegroundColor Green

# --- Launch in preview mode ---
$proc = Start-Process -FilePath $exePath `
    -ArgumentList "--preview", $Component `
    -PassThru

# --- Wait for window ---
$hwnd     = [IntPtr]::Zero
$deadline = (Get-Date).AddSeconds($WaitSeconds)

while ((Get-Date) -lt $deadline) {
    Start-Sleep -Milliseconds 400
    try {
        $p = Get-Process -Id $proc.Id -ErrorAction Stop
        if ($p.MainWindowHandle -ne [IntPtr]::Zero) {
            $hwnd = $p.MainWindowHandle
            break
        }
    } catch { break }
}

if ($hwnd -eq [IntPtr]::Zero) {
    $proc | Stop-Process -Force -ErrorAction SilentlyContinue
    Write-Error "Window did not appear within $WaitSeconds seconds."
    exit 1
}

# --- Screenshot ---
Add-Type -AssemblyName System.Drawing

Add-Type @"
using System;
using System.Runtime.InteropServices;
public class PreviewWinApi {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }
}
"@

[PreviewWinApi]::SetForegroundWindow($hwnd) | Out-Null
Start-Sleep -Milliseconds 3000  # let Avalonia finish rendering (and async images load)

$rect = New-Object PreviewWinApi+RECT
[PreviewWinApi]::GetWindowRect($hwnd, [ref]$rect) | Out-Null

$w = $rect.Right  - $rect.Left
$h = $rect.Bottom - $rect.Top

if ($w -le 0 -or $h -le 0) {
    $proc | Stop-Process -Force -ErrorAction SilentlyContinue
    Write-Error "Invalid window dimensions: ${w}x${h}"
    exit 1
}

$bmp = New-Object System.Drawing.Bitmap($w, $h)
$gfx = [System.Drawing.Graphics]::FromImage($bmp)
$gfx.CopyFromScreen($rect.Left, $rect.Top, 0, 0, [System.Drawing.Size]::new($w, $h))

if (-not (Test-Path $outDir)) {
    New-Item -ItemType Directory -Path $outDir | Out-Null
}

$ts  = Get-Date -Format "yyyyMMdd_HHmmss"
$out = Join-Path $outDir "${ts}.png"
$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
$gfx.Dispose()
$bmp.Dispose()

# --- Cleanup ---
$proc | Stop-Process -Force -ErrorAction SilentlyContinue

Write-Output $out
