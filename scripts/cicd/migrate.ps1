# Apply EF Core migrations to the database the API is configured to use.
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\lib.ps1"
Set-Location $script:RepoRoot

$env:ASPNETCORE_ENVIRONMENT = 'Development'
Write-Host 'Applying EF migrations to local Docker Postgres'
dotnet tool restore
if ($LASTEXITCODE -ne 0) {
    throw 'dotnet tool restore failed.'
}

dotnet ef database update --project src/GoodPlays.Infrastructure --startup-project src/GoodPlays.Api
if ($LASTEXITCODE -ne 0) {
    throw 'EF migrations failed.'
}

Write-Host 'RESULT: ok'
Write-Host 'MIGRATIONS: applied'
