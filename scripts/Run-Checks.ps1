param(
    [string]$ApiBaseUrl = ""
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path $PSScriptRoot -Parent
& dotnet run --project (Join-Path $projectRoot "tests/LogicCoreChecks/LogicCoreChecks.csproj")
if ($LASTEXITCODE -ne 0) { throw "Core checks failed." }

if (-not [string]::IsNullOrWhiteSpace($ApiBaseUrl)) {
    & dotnet run --project (Join-Path $projectRoot "tests/ApiChecks/ApiChecks.csproj") -- $ApiBaseUrl
    if ($LASTEXITCODE -ne 0) { throw "API checks failed." }
}
