param(
    [string]$RpcUrl = "http://127.0.0.1:28546",
    [string]$ChainId = "1337"
)

$ErrorActionPreference = "Stop"

$contractRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Push-Location $contractRoot
try {
    if (-not (Test-Path -LiteralPath "node_modules")) {
        npm.cmd ci
    }

    npm.cmd run compile
    $env:BESU_RPC_URL = $RpcUrl
    $env:BESU_CHAIN_ID = $ChainId
    npm.cmd run deploy
}
finally {
    Pop-Location
}
