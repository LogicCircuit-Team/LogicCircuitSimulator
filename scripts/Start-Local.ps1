param(
    [ValidateSet("Sqlite", "SqlServer")]
    [string]$Database = "Sqlite",
    [switch]$Lan
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path $PSScriptRoot -Parent
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:Database__Provider = $Database
$env:Database__ApplyMigrationsOnStartup = "true"

if ($Database -eq "Sqlite") {
    $dataPath = Join-Path $projectRoot "App_Data"
    New-Item -ItemType Directory -Force -Path $dataPath | Out-Null
    $databasePath = Join-Path $dataPath "circuits.db"
    $env:ConnectionStrings__CircuitDatabase = "Data Source=$databasePath"
} elseif ($env:ConnectionStrings__CircuitDatabase -like "Data Source=*.db") {
    $settings = Get-Content (Join-Path $projectRoot "CircuitApi/appsettings.json") -Raw | ConvertFrom-Json
    $env:ConnectionStrings__CircuitDatabase = $settings.ConnectionStrings.CircuitDatabase
}

if ($Lan) {
    $applicationUrl = "http://0.0.0.0:5210"
    Write-Host "Open http://YOUR-PC-IP:5210 on a phone connected to the same Wi-Fi."
} else {
    $applicationUrl = "https://localhost:7178;http://localhost:5210"
    Write-Host "Open https://localhost:7178"
}

& dotnet run --project (Join-Path $projectRoot "CircuitApi/CircuitApi.csproj") --no-launch-profile --urls $applicationUrl
if ($LASTEXITCODE -ne 0) { throw "The API stopped with exit code $LASTEXITCODE." }
