# Restart local Postgres, Redis, the API, and the Vite site. Exit 0 only when all are healthy.
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\lib.ps1"
Set-Location $script:RepoRoot
Initialize-ResultsDir

Write-Host 'Starting local Postgres and Redis'
$previousErrorAction = $ErrorActionPreference
$ErrorActionPreference = 'Continue'
docker info *> $null
$dockerCode = $LASTEXITCODE
$ErrorActionPreference = $previousErrorAction
if ($dockerCode -ne 0) {
    throw 'Docker is not running. Start Docker Desktop, then run restart dev again.'
}

docker compose up -d
if ($LASTEXITCODE -ne 0) {
    throw 'docker compose up -d failed.'
}

$infraOk = Wait-Until -TimeoutSec 180 -IntervalSec 3 -Condition {
    $postgres = Get-ContainerHealth 'goodplays-postgres'
    $redis = Get-ContainerHealth 'goodplays-redis'
    Write-Host "postgres=$postgres redis=$redis"
    ($postgres -eq 'healthy' -and $redis -eq 'healthy')
}

if (-not $infraOk) {
    $postgres = Get-ContainerHealth 'goodplays-postgres'
    $redis = Get-ContainerHealth 'goodplays-redis'
    throw "Local database is not healthy (postgres=$postgres, redis=$redis)."
}

Write-Host 'Stopping existing API and website processes'
Stop-Listeners -Port $script:ApiPort
Stop-Listeners -Port $script:WebPort

Write-Host 'Applying EF migrations to local Docker Postgres'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet tool restore
if ($LASTEXITCODE -ne 0) {
    throw 'dotnet tool restore failed.'
}

dotnet ef database update --project src/GoodPlays.Infrastructure --startup-project src/GoodPlays.Api
if ($LASTEXITCODE -ne 0) {
    throw 'EF migrations failed, so the local database is not healthy.'
}

$apiOut = Join-Path $script:ResultsDir 'api.log'
$apiErr = Join-Path $script:ResultsDir 'api.err.log'
$webOut = Join-Path $script:ResultsDir 'web.log'
$webErr = Join-Path $script:ResultsDir 'web.err.log'
foreach ($log in @($apiOut, $apiErr, $webOut, $webErr)) {
    if (Test-Path $log) {
        Remove-Item $log -Force -ErrorAction SilentlyContinue
    }
}

Write-Host 'Starting API on http://localhost:5280'
$apiProc = Start-Process -FilePath 'powershell.exe' `
    -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $PSScriptRoot 'start-api.ps1')) `
    -WorkingDirectory $script:RepoRoot `
    -RedirectStandardOutput $apiOut `
    -RedirectStandardError $apiErr `
    -WindowStyle Hidden `
    -PassThru
Set-Content -Path (Join-Path $script:ResultsDir 'api.pid') -Value $apiProc.Id

Write-Host "Starting website on $($script:WebUrl)"
$webProc = Start-Process -FilePath 'powershell.exe' `
    -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $PSScriptRoot 'start-web.ps1')) `
    -WorkingDirectory $script:RepoRoot `
    -RedirectStandardOutput $webOut `
    -RedirectStandardError $webErr `
    -WindowStyle Hidden `
    -PassThru
Set-Content -Path (Join-Path $script:ResultsDir 'web.pid') -Value $webProc.Id

$apiOk = Wait-Until -TimeoutSec 180 -IntervalSec 3 -Condition { Test-ServiceHealthy $script:ApiHealthUrl }
$webOk = Wait-Until -TimeoutSec 90 -IntervalSec 2 -Condition { Test-HttpOk $script:WebUrl }

if (-not $apiOk -or -not $webOk) {
    Write-Host '--- api.err.log ---'
    if (Test-Path $apiErr) { Get-Content $apiErr -Tail 40 }
    Write-Host '--- web.err.log ---'
    if (Test-Path $webErr) { Get-Content $webErr -Tail 40 }
    throw "Dev restart failed (api=$apiOk, web=$webOk)."
}

Write-Host 'RESULT: ok'
Write-Host "POSTGRES: healthy"
Write-Host "REDIS: healthy"
Write-Host "API: $($script:ApiHealthUrl) Healthy"
Write-Host "WEB: $($script:WebUrl) up"
