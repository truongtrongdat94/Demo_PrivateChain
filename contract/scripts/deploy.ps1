param(
    [string]$BackendProject = "../backend",
    [switch]$SkipDotnetDeploy
)

$ErrorActionPreference = "Stop"

$contractRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$backendRoot = Resolve-Path (Join-Path $contractRoot $BackendProject)
$artifactSource = Join-Path $contractRoot "artifacts/contracts/HashRegistry.sol/HashRegistry.json"
$artifactTargetDirectory = Join-Path $backendRoot "contract-artifacts"
$artifactTarget = Join-Path $artifactTargetDirectory "HashRegistry.json"

Push-Location $contractRoot
try {
    if (-not (Test-Path -LiteralPath "node_modules")) {
        npm ci
    }

    npm run compile
}
finally {
    Pop-Location
}

if (-not (Test-Path -LiteralPath $artifactSource)) {
    throw "Contract artifact not found: $artifactSource"
}

New-Item -ItemType Directory -Force -Path $artifactTargetDirectory | Out-Null
Copy-Item -LiteralPath $artifactSource -Destination $artifactTarget -Force
Write-Host "Copied contract artifact to $artifactTarget"

if (-not $SkipDotnetDeploy) {
    dotnet run --project $backendRoot -- deploy
}
