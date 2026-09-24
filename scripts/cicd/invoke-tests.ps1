# Run the full CI-equivalent suite, or only the suites touched by the working tree.
param(
    [ValidateSet('full', 'commit')]
    [string]$Mode = 'full'
)

$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\lib.ps1"
Set-Location $script:RepoRoot
Initialize-ResultsDir

$script:checks = New-Object System.Collections.Generic.List[object]
$script:failingTests = New-Object System.Collections.Generic.List[string]

function Add-Check {
    param([string]$Name, [string]$Result, [string]$Detail)
    $script:checks.Add([pscustomobject]@{ Name = $Name; Result = $Result; Detail = $Detail }) | Out-Null
}

function Add-Failure {
    param([string]$Name)
    if (-not [string]::IsNullOrWhiteSpace($Name)) {
        $script:failingTests.Add($Name)
    }
}

function Invoke-DotNetTests {
    $log = Join-Path $script:ResultsDir 'dotnet-test.log'
    $trx = Join-Path $script:ResultsDir 'dotnet.trx'
    if (Test-Path $trx) {
        Remove-Item $trx -Force
    }

    Write-Host 'Running dotnet test'
    dotnet test GoodPlays.sln --configuration Release --verbosity normal `
        --logger "trx;LogFileName=dotnet.trx" `
        --results-directory $script:ResultsDir *> $log
    $code = $LASTEXITCODE

    $names = @()
    if (Test-Path $trx) {
        try {
            $xml = [xml](Get-Content $trx -Raw)
            $results = @($xml.TestRun.Results.UnitTestResult | Where-Object { $_ })
            foreach ($result in $results) {
                if ($result.outcome -ne 'Failed') {
                    continue
                }

                $message = ''
                if ($result.Output -and $result.Output.ErrorInfo -and $result.Output.ErrorInfo.Message) {
                    $message = ([string]$result.Output.ErrorInfo.Message) -replace '\s+', ' '
                    if ($message.Length -gt 240) {
                        $message = $message.Substring(0, 240) + '...'
                    }
                }

                $label = [string]$result.testName
                if (-not [string]::IsNullOrWhiteSpace($message)) {
                    $label = "$label - $message"
                }

                $names += $label
            }
        }
        catch {
            Write-Host "Could not parse TRX: $($_.Exception.Message)"
        }
    }

    if ($names.Count -eq 0 -and $code -ne 0 -and (Test-Path $log)) {
        $names = @(
            Select-String -Path $log -Pattern '^\s*Failed\s+(\S+)' |
                ForEach-Object { $_.Matches[0].Groups[1].Value }
        )
        if ($names.Count -eq 0) {
            $names = @('dotnet test failed before any test name was reported')
        }
    }

    foreach ($name in $names) {
        Add-Failure $name
    }
    $resultLabel = 'passed'
    if ($code -ne 0) {
        $resultLabel = 'failed'
    }

    Add-Check -Name 'dotnet test' -Result $resultLabel -Detail $log
}

function Invoke-WebCheck {
    param([string]$ScriptName)

    $log = Join-Path $script:ResultsDir "web-$ScriptName.log"
    Write-Host "Running apps/web npm run $ScriptName"
    Push-Location (Join-Path $script:RepoRoot 'apps\web')
    try {
        if (-not (Test-Path 'node_modules')) {
            npm ci *> (Join-Path $script:ResultsDir 'web-npm-ci.log')
            if ($LASTEXITCODE -ne 0) {
                Add-Check -Name 'apps/web npm ci' -Result 'failed' -Detail (Join-Path $script:ResultsDir 'web-npm-ci.log')
                Add-Failure 'apps/web npm ci'
                return
            }
        }

        npm run $ScriptName *> $log
        $code = $LASTEXITCODE
    }
    finally {
        Pop-Location
    }

    $checkName = "apps/web $ScriptName"
    if ($code -ne 0) {
        Add-Check -Name $checkName -Result 'failed' -Detail $log
        Add-Failure $checkName
        return
    }

    Add-Check -Name $checkName -Result 'passed' -Detail $log
}

$runDotNet = $Mode -eq 'full'
$runWeb = $Mode -eq 'full'
$changed = @()

if ($Mode -eq 'commit') {
    $changed = @(Get-ChangedPaths)
    foreach ($path in $changed) {
        if ($path -match '^(src/|tests/)' -or $path -match '\.(cs|csproj|sln)$' -or $path -eq 'GoodPlays.sln') {
            $runDotNet = $true
        }

        if ($path -match '^apps/web/') {
            $runWeb = $true
        }
    }
}

if ($runDotNet) {
    Invoke-DotNetTests
}
elseif ($Mode -eq 'commit') {
    Add-Check -Name 'dotnet test' -Result 'skipped' -Detail 'No C# or test project changes.'
}

if ($runWeb) {
    Invoke-WebCheck -ScriptName 'lint'
    Invoke-WebCheck -ScriptName 'build'
}
elseif ($Mode -eq 'commit') {
    Add-Check -Name 'apps/web lint' -Result 'skipped' -Detail 'No apps/web changes.'
    Add-Check -Name 'apps/web build' -Result 'skipped' -Detail 'No apps/web changes.'
}

$failed = @($script:failingTests | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique)
$outcome = 'passed'
if ($failed.Count -gt 0) {
    $outcome = 'failed'
}

$when = [DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')
$failSection = "None."
if ($failed.Count -gt 0) {
    $failSection = ($failed | ForEach-Object { "- $_" }) -join "`n"
}

$checkRows = @(
    $script:checks | ForEach-Object { "| $($_.Name) | $($_.Result) | $($_.Detail) |" }
) -join "`n"

$changedSection = 'n/a (full suite)'
if ($Mode -eq 'commit') {
    if ($changed.Count -eq 0) {
        $changedSection = 'Working tree clean.'
    }
    else {
        $changedSection = ($changed | ForEach-Object { "- $_" }) -join "`n"
    }
}

$report = @"
# GoodPlays test report

- Mode: $Mode
- Result: $outcome
- When: $when

## Failing tests

$failSection

## Checks

| Check | Result | Log |
| --- | --- | --- |
$checkRows

## Changed paths

$changedSection
"@

$reportPath = Join-Path $script:ResultsDir 'cicd-report.md'
Set-Content -Path $reportPath -Value $report -Encoding utf8

Write-Host "REPORT: $reportPath"
Write-Host "RESULT: $outcome"
Write-Host 'FAILING TESTS:'
if ($failed.Count -eq 0) {
    Write-Host 'None.'
}
else {
    $failed | ForEach-Object { Write-Host $_ }
}

if ($outcome -ne 'passed') {
    exit 1
}

exit 0
