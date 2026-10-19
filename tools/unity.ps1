# Shared helper: resolves the Unity editor for this project and runs a batchmode method.
#   pwsh tools/unity.ps1 -Method Lab.EditorTools.LabBuild.CompileCheck [-Log name]
param(
    [Parameter(Mandatory = $true)][string]$Method,
    [string]$Log = "unity",
    [switch]$NoGraphics,
    [string[]]$Extra = @()
)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$ver = (Select-String -Path "$root/ProjectSettings/ProjectVersion.txt" -Pattern "m_EditorVersion: (.+)").Matches[0].Groups[1].Value.Trim()
$unity = "C:\Program Files\Unity\Hub\Editor\$ver\Editor\Unity.exe"
if (-not (Test-Path $unity)) { throw "Unity $ver not installed at $unity" }
if (Get-Process -Name Unity -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowTitle -like "*TownWalker*" }) {
    throw "TownWalker is open in the Unity Editor. Close it (File > Quit) and re-run."
}
New-Item -ItemType Directory -Force "$root/Logs" | Out-Null
$logFile = "$root/Logs/$Log.log"
$uargs = @("-batchmode", "-quit", "-projectPath", $root, "-executeMethod", $Method, "-logFile", $logFile)
if ($NoGraphics) { $uargs = @("-nographics") + $uargs }
$uargs += $Extra
$p = Start-Process -FilePath $unity -ArgumentList $uargs -Wait -PassThru -NoNewWindow
$errors = Select-String -Path $logFile -Pattern "error CS\d+" | Select-Object -First 40
if ($errors) { $errors | ForEach-Object { Write-Host $_.Line -ForegroundColor Red } }
Select-String -Path $logFile -Pattern "\[Build\]" | ForEach-Object { Write-Host $_.Line }
Write-Host "Unity exit code: $($p.ExitCode)  (log: $logFile)"
exit $p.ExitCode
