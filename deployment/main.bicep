@minLength(3)
@maxLength(24)
param prefix string
param location string = resourceGroup().location
param sqlAdministratorLogin string = 'circuitadmin'
@secure()
param sqlAdministratorPassword string

var registryName = 'lcs${uniqueString(resourceGroup().id)}'
var siteName = '${toLower(prefix)}-${uniqueString(resourceGroup().id)}'
var sqlName = '${toLower(prefix)}-sql-${uniqueString(resourceGroup().id)}'

resource registry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: registryName
  location: location
  sku: { name: 'Basic' }
  properties: { adminUserEnabled: false }
}

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: sqlName
  location: location
  properties: {
    administratorLogin: sqlAdministratorLogin
    administratorLoginPassword: sqlAdministratorPassword
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

resource database 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: 'CircuitDb'
  location: location
  sku: { name: 'Basic', tier: 'Basic', capacity: 5 }
  properties: { maxSizeBytes: 2147483648 }
}

resource firewall 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: { startIpAddress: '0.0.0.0', endIpAddress: '0.0.0.0' }
}

resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: '${siteName}-plan'
  location: location
  kind: 'linux'
  sku: { name: 'B1', tier: 'Basic', capacity: 1 }
  properties: { reserved: true }
}

resource site 'Microsoft.Web/sites@2023-12-01' = {
  name: siteName
  location: location
  kind: 'app,linux,container'
  identity: { type: 'SystemAssigned' }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOCKER|${registry.properties.loginServer}/logiccircuitsimulator:final'
      acrUseManagedIdentityCreds: true
      alwaysOn: true
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      healthCheckPath: '/api/health'
      appSettings: [
        { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
        { name: 'ASPNETCORE_FORWARDEDHEADERS_ENABLED', value: 'true' }
        { name: 'WEBSITES_PORT', value: '8080' }
        { name: 'WEBSITES_ENABLE_APP_SERVICE_STORAGE', value: 'true' }
        { name: 'WEBSITES_CONTAINER_START_TIME_LIMIT', value: '1800' }
        { name: 'DataProtection__KeyPath', value: '/home/data-protection-keys' }
        { name: 'Database__Provider', value: 'SqlServer' }
        { name: 'Database__ApplyMigrationsOnStartup', value: 'true' }
        { name: 'HdlSynthesis__EnvironmentScript', value: '' }
        { name: 'HdlSynthesis__YosysExecutable', value: '/opt/oss-cad-suite/bin/yosys' }
      ]
      connectionStrings: [
        {
          name: 'CircuitDatabase'
          type: 'SQLAzure'
          connectionString: 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Initial Catalog=${database.name};User ID=${sqlAdministratorLogin};Password=${sqlAdministratorPassword};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
        }
      ]
    }
  }
}

resource registryPull 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(registry.id, site.id, 'AcrPull')
  scope: registry
  properties: {
    principalId: site.identity.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')
  }
}

output appName string = site.name
output appUrl string = 'https://${site.properties.defaultHostName}'
output registryName string = registry.name
output sqlServerName string = sqlServer.name
