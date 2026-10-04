<#
.SYNOPSIS
  Builds the Quest APK headlessly, then installs it if a device is connected over adb.

.EXAMPLE
  .\tools\unity-build.ps1
  .\tools\unity-build.ps1 -ApkPath Builds\Interactive360.apk -NoInstall
#>
param(
    [string]$UnityPath = "C:\Program Files\Unity\Hub\Editor\6000.0.25f1\Editor\Unity.exe",
    [string]$ApkPath = "Builds\Interactive360.apk",
    [switch]$NoInstall
)

$ErrorActionPreference = "Stop"
$project = Split-Path -Parent $PSScriptRoot
$logDir = Join-Path $project "Logs"
New-Item -ItemType Directory -Force $logDir | Out-Null
$log = Join-Path $logDir "build.log"
$apkFull = Join-Path $project $ApkPath

$args = @(
    "-batchmode", "-nographics",
    "-projectPath", $project,
    "-buildTarget", "Android",
    "-executeMethod", "Interactive360.EditorTools.ProjectBootstrap.BuildFromCommandLine",
    "-apkPath", $apkFull,
    "-logFile", $log
)
Write-Host "Building $apkFull (log: $log)"
$p = Start-Process -FilePath $UnityPath -ArgumentList $args -PassThru -Wait -NoNewWindow
if ($p.ExitCode -ne 0) { Write-Host "Build failed (exit $($p.ExitCode)). See $log"; exit $p.ExitCode }
Write-Host "Build OK: $apkFull"

if (-not $NoInstall) {
    $adb = Get-Command adb -ErrorAction SilentlyContinue
    if ($null -eq $adb) { Write-Host "adb not found; skipping install."; exit 0 }
    $devices = & adb devices | Select-String -Pattern "`tdevice$"
    if ($devices.Count -eq 0) { Write-Host "No Quest connected over adb; skipping install."; exit 0 }
    & adb install -r $apkFull
    & adb shell monkey -p com.cavininfotech.interactive360 -c android.intent.category.LAUNCHER 1 | Out-Null
    Write-Host "Installed and launched on headset."
}
