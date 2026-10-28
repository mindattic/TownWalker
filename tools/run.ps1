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
    0. First, writes/refreshes a Desktop shortcut pointing at the repo-root run.bat (the menu).

.PARAMETER Launch
    After publishing, immediately run the deployed TownWalker.exe.

.NOTES
    This script never prompts, so agents and /deploy can run it unattended. The repo-root
    run.bat is the interactive entry point: it asks 1) Deploy & Run (this script with -Launch;
    every run rebuilds, there is no up-to-date skip) or 2) Run (starts the copy already in
    C:\Apps\TownWalker without calling this script, deploying first if none is there).

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools\run.ps1
    powershell -ExecutionPolicy Bypass -File tools\run.ps1 -Launch
#>
param([switch]$Launch)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoDir  = Split-Path $PSScriptRoot   # tools\ -> TownWalker\
$out      = 'C:\Apps\TownWalker'
$exeName  = 'TownWalker.exe'
$buildDir = Join-Path $repoDir 'Build\WindowsRelease'

# ── Desktop shortcut ─────────────────────────────────────────────────────────
# Points at the repo-root run.bat, so a double-click asks Deploy & Run or Run first. Refreshed
# before the build so it's right even if the build fails. GetFolderPath, not $HOME\Desktop:
# OneDrive may have moved the Desktop folder.
#
# IconLocation is special-cased: $out\$exeName is this run's TARGET (it may not exist yet, e.g.
# right after a repo/product rename, before the first successful deploy under the new name —
# see GGC-T26, where TownWalker was renamed from GridGameCaravan but the shortcut's icon kept
# pointing at the not-yet-deployed C:\Apps\TownWalker\TownWalker.exe for days because nothing
# re-pointed it at the still-working pre-rename copy). Only point the icon at it if it already
# exists; otherwise leave whatever icon the shortcut already had (a working icon beats a broken
# one) and let the post-publish block below re-aim it once the new exe is actually on disk.
$shortcut = Join-Path ([Environment]::GetFolderPath('Desktop')) 'TownWalker.lnk'
$link = (New-Object -ComObject WScript.Shell).CreateShortcut($shortcut)
$link.TargetPath       = Join-Path $repoDir 'run.bat'
$link.WorkingDirectory = $repoDir
if (Test-Path (Join-Path $out $exeName)) {
    $link.IconLocation = "$out\$exeName,0"
}
$link.Description      = 'TownWalker: Deploy & Run, or Run the copy in C:\Apps'
$link.WindowStyle      = 1
$link.Save()
Write-Host "    Shortcut -> $shortcut" -ForegroundColor Gray

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

# ── Re-aim shortcut icon ──────────────────────────────────────────────────────
# $exe is guaranteed to exist at this point (checked above), so this is the one place the icon
# can be safely pointed at the freshly published copy even right after a rename (GGC-T26).
try {
    $link = (New-Object -ComObject WScript.Shell).CreateShortcut($shortcut)
    $link.IconLocation = "$out\$exeName,0"
    $link.Save()
} catch {
    Write-Host "    Warning: could not refresh shortcut icon ($_)." -ForegroundColor DarkYellow
}

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
