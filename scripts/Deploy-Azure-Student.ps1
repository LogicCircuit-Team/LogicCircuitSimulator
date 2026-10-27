param(
    [Parameter(Mandatory = $true)][ValidatePattern('^[a-z][a-z0-9]{2,23}$')][string]$Prefix,
    [string]$ResourceGroup = "logic-circuit-rg",
    [string]$Location = "westeurope",
    [ValidatePattern('^[a-zA-Z][a-zA-Z0-9]{2,30}$')][string]$SqlAdministrator = "circuitadmin",
    [SecureString]$SqlPassword,
    [switch]$CheckOnly
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path $PSScriptRoot -Parent
$template = Join-Path $projectRoot "deployment/main.bicep"
foreach ($command in @("az", "docker")) {
    if (-not (Get-Command $command -ErrorAction SilentlyContinue)) {
        throw "Install $command and reopen PowerShell before running this script."
    }
}
$accountJson = & az account show --output json
if ($LASTEXITCODE -ne 0) { throw "Run az login and select your Azure subscription first." }
$account = $accountJson | ConvertFrom-Json
if ($account.state -ne "Enabled") { throw "The selected subscription is not enabled." }
Write-Host "Subscription: $($account.name) ($($account.id))"
$dockerOs = & docker info --format '{{.OSType}}'
if ($LASTEXITCODE -ne 0 -or "$dockerOs".Trim() -ne "linux") {
    throw "Start Docker Desktop with Linux containers, then try again."
}
& az bicep build --file $template --stdout | Out-Null
if ($LASTEXITCODE -ne 0) { throw "The Bicep template could not be compiled." }
if ($CheckOnly) {
    Write-Host "Local tools and template are ready. No Azure resources were created."
    Write-Host "Subscription policies, regional quotas and resource permissions are checked during deployment."
    return
}
if (-not $SqlPassword) { $SqlPassword = Read-Host "Azure SQL administrator password" -AsSecureString }
$parameterFile = Join-Path ([System.IO.Path]::GetTempPath()) ([guid]::NewGuid().ToString("N") + ".json")
$passwordPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($SqlPassword)
try {
    $password = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($passwordPointer)
    if ($password.Length -lt 12 -or $password.Contains(';') -or $password.Contains('"') -or $password.Contains("`r") -or $password.Contains("`n")) {
        throw "Use a password of at least 12 characters without semicolons, double quotes or line breaks. Azure SQL also enforces its password policy."
    }
    $parameters = @{
        prefix = @{ value = $Prefix }
        location = @{ value = $Location }
        sqlAdministratorLogin = @{ value = $SqlAdministrator }
        sqlAdministratorPassword = @{ value = $password }
    }
    $password = $null
    [System.IO.File]::WriteAllText($parameterFile, (@{ parameters = $parameters } | ConvertTo-Json -Depth 6), (New-Object System.Text.UTF8Encoding($false)))
    $localImage = "logiccircuitsimulator:final"
    & docker build --platform linux/amd64 --tag $localImage $projectRoot
    if ($LASTEXITCODE -ne 0) { throw "The local container build failed. No new Azure resources were requested by this script." }
    Write-Host "Creating paid App Service B1, Azure SQL Basic and ACR Basic resources in $ResourceGroup."
    & az group create --name $ResourceGroup --location $Location --output none
    if ($LASTEXITCODE -ne 0) { throw "Resource group creation failed. Check subscription policy and region permissions." }
    $deploymentJson = & az deployment group create --name "simulator" --resource-group $ResourceGroup --template-file $template --parameters "@$parameterFile" --output json
    if ($LASTEXITCODE -ne 0) { throw "Azure deployment failed. Inspect the deployment errors and any resources already created in $ResourceGroup before retrying." }
    $outputs = ($deploymentJson | ConvertFrom-Json).properties.outputs
    $registryName = $outputs.registryName.value
    $appName = $outputs.appName.value
    $appUrl = $outputs.appUrl.value
    $registryHost = & az acr show --name $registryName --resource-group $ResourceGroup --query loginServer --output tsv
    if ($LASTEXITCODE -ne 0) { throw "The container registry address could not be read." }
    & az acr login --name $registryName
    if ($LASTEXITCODE -ne 0) { throw "Registry sign-in failed. The signed-in account needs permission to push images." }
    $remoteImage = "$($registryHost.Trim())/logiccircuitsimulator:final"
    & docker tag $localImage $remoteImage
    if ($LASTEXITCODE -ne 0) { throw "The container image could not be tagged." }
    & docker push $remoteImage
    if ($LASTEXITCODE -ne 0) { throw "Container upload failed. The Azure resources remain provisioned; inspect the error before retrying." }
    & az webapp log config --name $appName --resource-group $ResourceGroup --docker-container-logging filesystem --output none
    if ($LASTEXITCODE -ne 0) { throw "Container logging could not be enabled." }
    & az webapp restart --name $appName --resource-group $ResourceGroup
    if ($LASTEXITCODE -ne 0) { throw "The web app could not be restarted." }
    $ready = $false
    for ($attempt = 0; $attempt -lt 40; $attempt++) {
        try {
            $health = Invoke-RestMethod -Uri "$appUrl/api/health/database" -TimeoutSec 15
            if ($health.status -eq "ok") { $ready = $true; break }
        } catch { }
        Write-Host "Waiting for the HTTPS application and database ($($attempt + 1)/40)..."
        Start-Sleep -Seconds 15
    }
    if (-not $ready) {
        throw "Database health is not ready. Run: az webapp log tail --name $appName --resource-group $ResourceGroup"
    }
    Write-Host "Application ready: $appUrl"
    Write-Host "App name: $appName"
    Write-Host "Resource group: $ResourceGroup"
    Write-Host "Registry: $registryName"
    Write-Host "Run: .\scripts\Run-Checks.ps1 -ApiBaseUrl $appUrl"
    Write-Host "Then test Save, Load, Community and examples/full_adder.sv on a physical phone."
} finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($passwordPointer)
    $password = $null
    $parameters = $null
    Remove-Item -LiteralPath $parameterFile -ErrorAction SilentlyContinue
}
