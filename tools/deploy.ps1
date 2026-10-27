#Requires -Version 5.1
<#
.SYNOPSIS
    Shuts down any running TownWalker, clears the release build output, rebuilds the Windows
    release player (Lab.EditorTools.LabBuild.BuildWindowsRelease), and publishes a standalone copy
    to C:\Apps\TownWalker\ — same process Unity.Eve/UnitySolarSystem/Automata/KdpPublish/
    IdiotProof use to always have a fresh, independent deployed copy instead of a possibly-stale
    dev build.

.DESCRIPTION
    1. Stops any running TownWalker.exe process (dev copy or deployed copy).
    2. Removes Build\WindowsRelease — the build OUTPUT only, never Library\ (Unity's asset
       import / script-compile cache, which survives exactly as it does for a normal Editor
       rebuild) — so the player build below is never a stale incremental copy.
    3. Runs Unity in batch mode via tools\unity.ps1 (-executeMethod
       Lab.EditorTools.LabBuild.BuildWindowsRelease, requires the Editor closed)
       -> Build\WindowsRelease\TownWalker.exe.
    4. Copies Build\WindowsRelease\* -> C:\Apps\TownWalker\.
    5. Writes/refreshes a Desktop shortcut pointing at the deployed exe.

.PARAMETER Launch
    After publishing, immediately run the deployed TownWalker.exe.

.NOTES
    This script never prompts, so agents and /deploy can run it unattended. The repo-root
    deploy.bat is the interactive entry point: it asks 1) Deploy & Run (this script with -Launch;
    every run rebuilds, there is no up-to-date skip) or 2) Run (starts the copy already in
    C:\Apps\TownWalker without calling this script, deploying first if none is there).

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools\deploy.ps1
    powershell -ExecutionPolicy Bypass -File tools\deploy.ps1 -Launch
#>
param([switch]$Launch)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoDir  = Split-Path $PSScriptRoot   # tools\ -> TownWalker\
$out      = 'C:\Apps\TownWalker'
$exeName  = 'TownWalker.exe'
$buildDir = Join-Path $repoDir 'Build\WindowsRelease'

# ── Stop running instance ──────────────────────────────────────────────────
Write-Host ''
Write-Host '  Stopping running instance...' -ForegroundColor Yellow
$procs = Get-Process 'TownWalker' -ErrorAction SilentlyContinue
if ($procs) {
    $procs | Stop-Process -Force
    Write-Host "    Stopped ($(@($procs).Count) process(es))" -ForegroundColor DarkYellow
    Start-Sleep -Milliseconds 800
} else {
    Write-Host '    Nothing running.' -ForegroundColor DarkGray
}

# ── Clear build output ───────────────────────────────────────────────────────
# Build\WindowsRelease only — never Library\, which is Unity's import/compile cache, not
# build output, and must survive a redeploy exactly as it survives a normal rebuild.
Write-Host ''
Write-Host '  Clearing build output (Build\WindowsRelease)...' -ForegroundColor Cyan
if (Test-Path $buildDir) {
    Remove-Item -Recurse -Force $buildDir
}

# ── Build ────────────────────────────────────────────────────────────────────
Write-Host ''
Write-Host '  Building Windows release player (Unity batch mode)...' -ForegroundColor Cyan
& "$PSScriptRoot/unity.ps1" -Method Lab.EditorTools.LabBuild.BuildWindowsRelease -Log 'deploy-build'
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed (exit $LASTEXITCODE). See Logs\deploy-build.log." -ForegroundColor Red
    exit $LASTEXITCODE
}

$builtExe = Join-Path $buildDir $exeName
if (-not (Test-Path $builtExe)) {
    Write-Host "Build completed but $exeName not found at $builtExe." -ForegroundColor Red
    exit 1
}

# ── Publish ─────────────────────────────────────────────────────────────────
Write-Host ''
Write-Host "  Publishing $builtExe" -ForegroundColor Cyan
Write-Host "    -> $out\$exeName"

if (Test-Path $out) { Remove-Item -Recurse -Force $out }
New-Item -ItemType Directory -Force $out | Out-Null
Copy-Item -Recurse -Force (Join-Path $buildDir '*') $out

$exe = Join-Path $out $exeName
if (-not (Test-Path $exe)) {
    Write-Host "Publish completed but $exeName not found at $exe." -ForegroundColor Red
    exit 1
}

# ── Desktop shortcut ─────────────────────────────────────────────────────────
# Points at the published copy, so double-clicking it (or /run) always plays the last deploy.
# GetFolderPath, not $HOME\Desktop: OneDrive may have moved the Desktop folder.
$shortcut = Join-Path ([Environment]::GetFolderPath('Desktop')) 'TownWalker.lnk'
$link = (New-Object -ComObject WScript.Shell).CreateShortcut($shortcut)
$link.TargetPath       = $exe
$link.WorkingDirectory = $out
$link.IconLocation     = "$exe,0"
$link.Description      = 'TownWalker (deployed copy in C:\Apps)'
$link.Save()
Write-Host "    Shortcut -> $shortcut" -ForegroundColor Gray

# ── Summary ────────────────────────────────────────────────────────────────
Write-Host ''
Write-Host '  Published successfully.' -ForegroundColor Green
Write-Host "    Exe : $exe ($([math]::Round(((Get-Item $exe).Length / 1MB), 1)) MB)" -ForegroundColor Gray
Write-Host ''

if ($Launch) {
    Write-Host '  Launching...' -ForegroundColor Cyan
    Start-Process $exe
}

# An explicit success code: Unity runs via Start-Process inside unity.ps1, which already exits
# with Unity's own code, but set it explicitly here too so a caller checking $LASTEXITCODE after
# everything below (shortcut creation, etc.) doesn't read a stale/empty value as a failure.
exit 0
