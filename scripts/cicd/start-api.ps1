# Long-running local API. Started by restart-dev.ps1; do not invoke for a one-shot command.
$ErrorActionPreference = 'Stop'
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location $RepoRoot
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project src/GoodPlays.Api --launch-profile http
exit $LASTEXITCODE
