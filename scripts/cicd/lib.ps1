# Shared helpers for GoodPlays CI/CD commands. Dot-source from the other scripts.
$ErrorActionPreference = 'Stop'

$script:RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$script:ApiPort = 5280
$script:WebPort = 5180
$script:ApiHealthUrl = 'http://localhost:5280/health'
$script:WebUrl = 'http://localhost:5180/'
$script:RailwayHealthUrl = 'https://goodplays-production.up.railway.app/health'
$script:PagesUrl = 'https://sentinemodo.github.io/GoodPlays/'
$script:ResultsDir = Join-Path $script:RepoRoot 'TestResults'

function Initialize-ResultsDir {
    if (-not (Test-Path $script:ResultsDir)) {
        New-Item -ItemType Directory -Path $script:ResultsDir | Out-Null
    }
}

function Get-ListenerPids {
    param([int]$Port)

    $pids = @()
    try {
        $pids = @(
            Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue |
                Select-Object -ExpandProperty OwningProcess -Unique
        )
    }
    catch {
        $pids = @()
    }

    if ($pids.Count -eq 0) {
        $pids = @(
            netstat -ano |
                Select-String ":$Port\s" |
                Where-Object { $_.Line -match 'LISTENING' } |
                ForEach-Object {
                    if ($_.Line -match '\s(\d+)\s*$') { [int]$Matches[1] }
                }
        )
    }

    return @($pids | Where-Object { $_ -gt 0 } | Select-Object -Unique)
}

function Stop-Listeners {
    param([int]$Port)

    $pids = @(Get-ListenerPids -Port $Port)
    foreach ($procId in $pids) {
        Write-Host "Stopping PID $procId on port $Port"
        Stop-Process -Id $procId -Force -ErrorAction SilentlyContinue
    }

    if ($pids.Count -gt 0) {
        Start-Sleep -Seconds 1
    }

    $remaining = @(Get-ListenerPids -Port $Port)
    if ($remaining.Count -gt 0) {
        throw "Port $Port is still in use by PID $($remaining -join ', ')."
    }
}

function Test-RemoteDatabaseConfigured {
    if (-not [string]::IsNullOrWhiteSpace($env:DATABASE_URL)) {
        return $true
    }

    $envFile = Join-Path $script:RepoRoot '.env'
    if (-not (Test-Path $envFile)) {
        return $false
    }

    foreach ($rawLine in Get-Content $envFile) {
        $line = $rawLine.Trim()
        if ($line.Length -eq 0 -or $line.StartsWith('#')) {
            continue
        }

        if ($line -match '^DATABASE_URL\s*=\s*(.*)$') {
            $value = $Matches[1].Trim().Trim('"').Trim("'")
            return -not [string]::IsNullOrWhiteSpace($value)
        }
    }

    return $false
}

function Get-ContainerHealth {
    param([string]$Name)

    $previousErrorAction = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    $status = docker inspect --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}missing{{end}}' $Name 2>$null
    $code = $LASTEXITCODE
    $ErrorActionPreference = $previousErrorAction
    if ($code -ne 0 -or [string]::IsNullOrWhiteSpace($status)) {
        return 'missing'
    }

    return $status.Trim()
}

function Wait-Until {
    param(
        [scriptblock]$Condition,
        [int]$TimeoutSec,
        [int]$IntervalSec = 2
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    do {
        if (& $Condition) {
            return $true
        }

        Start-Sleep -Seconds $IntervalSec
    } while ((Get-Date) -lt $deadline)

    return $false
}

function Test-HttpOk {
    param([string]$Url)

    try {
        $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 15
        return $response.StatusCode -ge 200 -and $response.StatusCode -lt 400
    }
    catch {
        return $false
    }
}

function Test-ServiceHealthy {
    param([string]$Url)

    try {
        $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 15
        $body = [string]$response.Content
        if ($body -match 'Unhealthy') {
            return $false
        }

        return $response.StatusCode -eq 200 -and $body -match 'Healthy'
    }
    catch {
        return $false
    }
}

function Get-ChangedPaths {
    $lines = @(git status --porcelain=v1 -uall)
    $paths = @()
    foreach ($line in $lines) {
        if ([string]::IsNullOrWhiteSpace($line) -or $line.Length -lt 4) {
            continue
        }

        $path = $line.Substring(3).Trim()
        if ($path -match ' -> ') {
            $path = ($path -split ' -> ')[-1].Trim()
        }

        $path = $path.Trim('"').Replace('\', '/')
        if (-not [string]::IsNullOrWhiteSpace($path)) {
            $paths += $path
        }
    }

    return @($paths | Select-Object -Unique)
}
