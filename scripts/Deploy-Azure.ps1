param(
    [Parameter(Mandatory = $true)][string]$Prefix,
    [string]$ResourceGroup = "logic-circuit-rg",
    [string]$Location = "westeurope",
    [string]$SqlAdministrator = "circuitadmin",
    [Parameter(Mandatory = $true)][SecureString]$SqlPassword
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path $PSScriptRoot -Parent
& az account show --output none
if ($LASTEXITCODE -ne 0) { throw "Run az login and select the intended subscription first." }

$parameterFile = Join-Path ([System.IO.Path]::GetTempPath()) ([guid]::NewGuid().ToString() + ".json")
$passwordPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($SqlPassword)
try {
    $parameters = @{
        prefix = @{ value = $Prefix }
        location = @{ value = $Location }
        sqlAdministratorLogin = @{ value = $SqlAdministrator }
        sqlAdministratorPassword = @{ value = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($passwordPointer) }
    }
    [System.IO.File]::WriteAllText($parameterFile, (@{ parameters = $parameters } | ConvertTo-Json -Depth 6), (New-Object System.Text.UTF8Encoding($false)))
    & az group create --name $ResourceGroup --location $Location --output none
    if ($LASTEXITCODE -ne 0) { throw "Resource group creation failed." }
    $deployment = & az deployment group create --name "simulator" --resource-group $ResourceGroup --template-file (Join-Path $projectRoot "deployment/main.bicep") --parameters "@$parameterFile" --output json
    if ($LASTEXITCODE -ne 0) { throw "Azure resource deployment failed." }
    $outputs = ($deployment | ConvertFrom-Json).properties.outputs
    $registryName = $outputs.registryName.value
    $appName = $outputs.appName.value
    $appUrl = $outputs.appUrl.value
    & az acr build --registry $registryName --image "logiccircuitsimulator:final" $projectRoot
    if ($LASTEXITCODE -ne 0) { throw "Container build failed. Check the ACR build log." }
    & az webapp restart --name $appName --resource-group $ResourceGroup
    if ($LASTEXITCODE -ne 0) { throw "The web app could not be restarted." }
    $ready = $false
    for ($attempt = 0; $attempt -lt 40; $attempt++) {
        try {
            $health = Invoke-RestMethod -Uri "$appUrl/api/health/database" -TimeoutSec 15
            if ($health.status -eq "ok") { $ready = $true; break }
        } catch { }
        Start-Sleep -Seconds 15
    }
    if (-not $ready) { throw "The database health check is not ready. Open the App Service container logs before using the site." }
    Write-Host "Application ready: $appUrl"
    Write-Host "Test Import HDL and the mobile circuit board before publishing the GitHub release."
} finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($passwordPointer)
    Remove-Item -LiteralPath $parameterFile -ErrorAction SilentlyContinue
}
