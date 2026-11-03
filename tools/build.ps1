# Builds a player: the Windows development player (with the automation endpoint compiled in) by default,
# the Windows release player with -Release, or a development Android APK / WebGL build with -Target.
#   pwsh tools/build.ps1 [-Release] [-Target Windows|Android|WebGL]
param([switch]$Release, [string]$Target = "Windows")
$method = switch ($Target) {
    "Android" { "Lab.EditorTools.LabBuild.BuildAndroid" }
    "WebGL" { "Lab.EditorTools.LabBuild.BuildWebGL" }
    default { if ($Release) { "Lab.EditorTools.LabBuild.BuildWindowsRelease" } else { "Lab.EditorTools.LabBuild.BuildWindowsDev" } }
}
& "$PSScriptRoot/unity.ps1" -Method $method -Log "build-$Target"
exit $LASTEXITCODE
