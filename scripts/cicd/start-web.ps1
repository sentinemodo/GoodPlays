# Long-running Vite dev server. Started by restart-dev.ps1.
$ErrorActionPreference = 'Stop'
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location (Join-Path $RepoRoot 'apps\web')
npm run dev -- --host 127.0.0.1 --port 5180 --strictPort
exit $LASTEXITCODE
