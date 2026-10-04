<#
.SYNOPSIS
  Runs the Unity project bootstrap headlessly: imports packages, configures Quest/Android settings,
  OpenXR, and writes Assets/Scenes/Main.unity.

.EXAMPLE
  .\tools\unity-setup.ps1
  .\tools\unity-setup.ps1 -UnityPath "C:\Program Files\Unity\Hub\Editor\6000.0.25f1\Editor\Unity.exe"
#>
param(
    [string]$UnityPath = "C:\Program Files\Unity\Hub\Editor\6000.0.25f1\Editor\Unity.exe",
    [string]$Method = "Interactive360.EditorTools.ProjectBootstrap.SetupFromCommandLine"
)

$ErrorActionPreference = "Stop"
$project = Split-Path -Parent $PSScriptRoot
$logDir = Join-Path $project "Logs"
New-Item -ItemType Directory -Force $logDir | Out-Null
$log = Join-Path $logDir "setup.log"

Write-Host "Unity : $UnityPath"
Write-Host "Proj  : $project"
Write-Host "Log   : $log"

$args = @(
    "-batchmode", "-nographics",
    "-projectPath", $project,
    "-buildTarget", "Android",
    "-executeMethod", $Method,
    "-logFile", $log
)
$p = Start-Process -FilePath $UnityPath -ArgumentList $args -PassThru -Wait -NoNewWindow
Write-Host "Unity exited with code $($p.ExitCode)"
exit $p.ExitCode
