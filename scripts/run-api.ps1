# Starts GoodPlays.Api after checking whether the configured port is free.
param(
    [int]$Port = 0
)

$ErrorActionPreference = 'Stop'

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
Set-Location $RepoRoot

function Get-ApiPort {
    param([int]$OverridePort)

    if ($OverridePort -gt 0) {
        return $OverridePort
    }

    if ($env:PORT) {
        return [int]$env:PORT
    }

    $launchSettingsPath = Join-Path $RepoRoot 'src/GoodPlays.Api/Properties/launchSettings.json'
    if (Test-Path $launchSettingsPath) {
        $launchSettings = Get-Content $launchSettingsPath -Raw | ConvertFrom-Json
        $applicationUrl = $launchSettings.profiles.http.applicationUrl
        if ($applicationUrl) {
            return ([uri]$applicationUrl).Port
        }
    }

    return 5280
}

function Get-ProcessIdsOnPort {
    param([int]$ListenPort)

    $processIds = @(
        Get-NetTCPConnection -LocalPort $ListenPort -State Listen -ErrorAction SilentlyContinue |
            Select-Object -ExpandProperty OwningProcess -Unique
    )

    if ($processIds.Count -gt 0) {
        return $processIds
    }

    return netstat -ano |
        Select-String ":$ListenPort\s" |
        ForEach-Object {
            if ($_.Line -match '\s(\d+)\s*$') {
                [int]$matches[1]
            }
        } |
        Select-Object -Unique
}

function Stop-PortListeners {
    param([int]$ListenPort)

    $processIds = @(Get-ProcessIdsOnPort -ListenPort $ListenPort | Where-Object { $_ -gt 0 })
    if ($processIds.Count -eq 0) {
        return
    }

    Write-Host "Port $ListenPort is already in use:" -ForegroundColor Yellow
    foreach ($processId in $processIds) {
        $process = Get-Process -Id $processId -ErrorAction SilentlyContinue
        if ($process) {
            Write-Host "  - $($process.ProcessName) (PID $processId)"
            if ($process.Path) {
                Write-Host "    $($process.Path)"
            }
        }
        else {
            Write-Host "  - Unknown process (PID $processId)"
        }
    }

    $answer = Read-Host "Terminate $(@($processIds) -join ', ') and start the API? [y/N]"
    if ($answer -notmatch '^[yY]$') {
        Write-Host 'Aborted.'
        exit 1
    }

    foreach ($processId in $processIds) {
        Stop-Process -Id $processId -Force -ErrorAction SilentlyContinue
    }

    Start-Sleep -Seconds 1

    $remaining = @(Get-ProcessIdsOnPort -ListenPort $ListenPort | Where-Object { $_ -gt 0 })
    if ($remaining.Count -gt 0) {
        Write-Error "Port $ListenPort is still in use after termination attempt."
    }
}

$apiPort = Get-ApiPort -OverridePort $Port
Stop-PortListeners -ListenPort $apiPort

dotnet run --project src/GoodPlays.Api @args
