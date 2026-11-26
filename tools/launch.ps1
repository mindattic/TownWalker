<#
.SYNOPSIS
  Deploys the current build to R:\MindAttic\Apps\TownWalker, then offers Normal Play, Demo +
  Capture, or Quit, run from the Release copy.
.NOTES
  Always redeploys from source first (the repo-root run.ps1 -Redeploy), so this never runs a stale build.
  Requires the Unity Editor closed for this project.
#>
$ErrorActionPreference = "Stop"

try {
    & (Join-Path $PSScriptRoot "..\run.ps1") -Redeploy
} catch {
    Write-Host "Deploy failed ($_); not launching." -ForegroundColor Red
    exit 1
}

$exe = "R:\MindAttic\Apps\TownWalker\Release\TownWalker.exe"
if (-not (Test-Path $exe)) {
    Write-Host "Deploy reported success but $exe is missing." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "  TownWalker" -ForegroundColor Cyan
Write-Host "  [1] Normal Play"
Write-Host "  [2] Demo + Capture"
Write-Host "  [3] Quit"
Write-Host ""
$choice = Read-Host "Select (default 1)"
if ([string]::IsNullOrWhiteSpace($choice)) { $choice = "1" }

switch ($choice) {
    "1" { Start-Process -FilePath $exe }
    "2" { Start-Process -FilePath $exe -ArgumentList @("-demo", "-capture", "video", "screen", "5") }
    "3" { exit 0 }
    default {
        Write-Host "Unrecognized choice, launching Normal Play." -ForegroundColor Yellow
        Start-Process -FilePath $exe
    }
}
