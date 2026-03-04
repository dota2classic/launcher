<#
.SYNOPSIS
    Screenshot an HTML file using headless Chrome.

.PARAMETER HtmlFile
    Path to the HTML file to render (absolute or relative to CWD).

.PARAMETER Width
    Viewport width in pixels (default: 1000).

.PARAMETER Height
    Viewport height in pixels (default: 800).

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools/screenshot-html.ps1 tools/screenshots/d2c-launcher-views.html
    powershell -ExecutionPolicy Bypass -File tools/screenshot-html.ps1 tools/screenshots/d2c-launcher-views.html -Width 1200 -Height 900
#>
param(
    [Parameter(Position = 0, Mandatory = $true)]
    [string]$HtmlFile,

    [int]$Width  = 1000,
    [int]$Height = 800
)

$ErrorActionPreference = "Stop"
$outDir = Join-Path $PSScriptRoot "screenshots"

# --- Resolve HTML path ---
$resolved = Resolve-Path $HtmlFile
$fileUrl  = "file:///" + ($resolved.Path -replace '\\', '/')

# --- Find Chrome ---
$chromeCandidates = @(
    "$env:LOCALAPPDATA\Google\Chrome\Application\chrome.exe",
    "C:\Program Files\Google\Chrome\Application\chrome.exe",
    "C:\Program Files (x86)\Google\Chrome\Application\chrome.exe"
)
$chromePath = $chromeCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $chromePath) {
    Write-Error "Google Chrome not found."
    exit 1
}

# --- Clean up old screenshots ---
if (Test-Path $outDir) {
    Get-ChildItem -Path $outDir -Filter "*.png" | Remove-Item -Force
}
if (-not (Test-Path $outDir)) {
    New-Item -ItemType Directory -Path $outDir | Out-Null
}

$ts  = Get-Date -Format "yyyyMMdd_HHmmss"
$out = Join-Path $outDir "${ts}.png"

Write-Host "Rendering: $($resolved.Path)" -ForegroundColor Cyan

# Chrome headless screenshot — Start-Process with array ensures correct arg passing
$chromeArgs = @(
    "--headless",
    "--screenshot=$out",
    "--window-size=$Width,$Height",
    "--allow-file-access-from-files",
    "--disable-gpu",
    "--no-sandbox",
    $fileUrl
)
$proc = Start-Process -FilePath $chromePath -ArgumentList $chromeArgs -Wait -PassThru

if (-not (Test-Path $out)) {
    Write-Error "Chrome did not produce a screenshot (exit code $($proc.ExitCode))."
    exit 1
}

Write-Output $out
