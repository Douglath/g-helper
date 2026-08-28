param(
    [string]$DotNet = 'dotnet',
    [switch]$RunChecks
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'app/GHelper.csproj'
$output = Join-Path $repoRoot 'artifacts/claymore'

# GITHUB_ACTIONS disables upstream's build target that exits running G-Helper.
# This script never starts, installs or registers the application.
& $DotNet publish $project --configuration Release --runtime win-x64 --no-self-contained `
    -p:PublishSingleFile=true -p:GITHUB_ACTIONS=true `
    -p:InformationalVersion=0.279.0-claymore-spatial-mapfix `
    --output $output --disable-build-servers
if ($LASTEXITCODE -ne 0) { throw "G-Helper publish failed ($LASTEXITCODE)." }

if ($RunChecks) {
    $testProject = Join-Path $repoRoot 'tests/ClaymoreSpatialSync/ClaymoreSpatialSync.csproj'
    & $DotNet run --project $testProject --configuration Release
    if ($LASTEXITCODE -ne 0) { throw "Spatial sync checks failed ($LASTEXITCODE)." }
}

Write-Output "Built $output. Not installed or launched."
