# Restart local dev, redeploy GitHub Pages from main, and require Railway /health to be Healthy.
param(
    [switch]$RailwayOnly
)

$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\lib.ps1"
Set-Location $script:RepoRoot

if (-not $RailwayOnly) {
    & "$PSScriptRoot\restart-dev.ps1"
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    Write-Host 'Redeploying GitHub Pages from main'
    $beforeRaw = gh run list --workflow 'Deploy GitHub Pages' --limit 1 --json databaseId
    $beforeId = ''
    if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($beforeRaw)) {
        $before = @($beforeRaw | ConvertFrom-Json | Where-Object { $_ })
        if ($before.Count -gt 0) {
            $beforeId = [string]$before[0].databaseId
        }
    }

    gh workflow run 'Deploy GitHub Pages' --ref main
    if ($LASTEXITCODE -ne 0) {
        throw 'Failed to start the Deploy GitHub Pages workflow.'
    }

    $runId = ''
    $deadline = (Get-Date).AddSeconds(60)
    do {
        Start-Sleep -Seconds 2
        $raw = gh run list --workflow 'Deploy GitHub Pages' --limit 5 --json databaseId,headBranch
        if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($raw)) {
            continue
        }

        $newest = @($raw | ConvertFrom-Json) | Where-Object { $_ } | Select-Object -First 1
        if ($newest -and [string]$newest.databaseId -ne $beforeId -and $newest.headBranch -eq 'main') {
            $runId = [string]$newest.databaseId
        }
    } while ([string]::IsNullOrWhiteSpace($runId) -and (Get-Date) -lt $deadline)

    if ([string]::IsNullOrWhiteSpace($runId)) {
        throw 'Deploy GitHub Pages did not appear in recent workflow runs.'
    }
    Write-Host "Watching GitHub Pages run $runId"
    gh run watch $runId --exit-status
    if ($LASTEXITCODE -ne 0) {
        throw "Deploy GitHub Pages run $runId failed."
    }

    $pagesOk = Wait-Until -TimeoutSec 120 -IntervalSec 5 -Condition { Test-HttpOk $script:PagesUrl }
    if (-not $pagesOk) {
        throw "GitHub Pages did not return HTTP 200 at $($script:PagesUrl)"
    }

    Write-Host "PAGES: $($script:PagesUrl) up (run $runId)"
}

Write-Host "Checking Railway health at $($script:RailwayHealthUrl)"
$railwayOk = Wait-Until -TimeoutSec 300 -IntervalSec 10 -Condition { Test-ServiceHealthy $script:RailwayHealthUrl }
if (-not $railwayOk) {
    Write-Host "RAILWAY_UNHEALTHY $($script:RailwayHealthUrl)"
    Write-Host 'RESULT: railway-unhealthy'
    exit 2
}

Write-Host 'RAILWAY: Healthy'
Write-Host 'RESULT: ok'
