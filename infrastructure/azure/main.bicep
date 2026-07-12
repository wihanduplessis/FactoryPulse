@description('Azure region for all resources. Defaults to the resource group location.')
param location string = resourceGroup().location

@description('Short application name, used to build resource names.')
@minLength(3)
@maxLength(12)
param applicationName string = 'factorypulse'

@description('Object ID of the GitHub Actions service principal that pushes images.')
param githubPrincipalId string

@description('Object ID of the human administrator (you) — Key Vault writer and SQL Entra admin.')
param administratorObjectId string

@description('User principal name of the human administrator (you).')
param administratorLogin string

@description('Deployment environment, used in resource names and tags.')
@allowed([
  'dev'
  'prod'
])
param environmentName string = 'prod'

// Several Azure resources (container registries, key vaults) need a name that is
// unique across the whole of Azure, not just this subscription. This gives us a
// short, deterministic suffix derived from the subscription and resource group,
// so re-deploying produces the same names rather than a new set of resources.
var resourceToken = uniqueString(subscription().subscriptionId, resourceGroup().id)

var tags = {
  application: applicationName
  environment: environmentName
  managedBy: 'bicep'
}

var registryName = 'cr${applicationName}${resourceToken}'

var identityName = 'id-${applicationName}'
var vaultName = 'kv-${applicationName}-${take(resourceToken, 6)}'

module identity 'modules/identity.bicep' = {
  name: 'identity'
  params: {
    location: location
    tags: tags
    identityName: identityName
  }
}

module registry 'modules/registry.bicep' = {
  name: 'registry'
  params: {
    location: location
    tags: tags
    registryName: registryName
    pushPrincipalId: githubPrincipalId
    pullPrincipalId: identity.outputs.principalId
  }
}

module keyVault 'modules/keyvault.bicep' = {
  name: 'keyvault'
  params: {
    location: location
    tags: tags
    vaultName: vaultName
    readerPrincipalId: identity.outputs.principalId
    administratorObjectId: administratorObjectId
  }
}

var sqlServerName = 'sql-${applicationName}-${resourceToken}'
var sqlDatabaseName = 'FactoryPulseDb'

module database 'modules/database.bicep' = {
  name: 'database'
  params: {
    location: location
    tags: tags
    serverName: sqlServerName
    databaseName: sqlDatabaseName
    administratorObjectId: administratorObjectId
    administratorLogin: administratorLogin
  }
}


output sqlServerFqdn string = database.outputs.fullyQualifiedDomainName
output sqlDatabaseName string = database.outputs.databaseName
output sqlConnectionString string = 'Server=tcp:${database.outputs.fullyQualifiedDomainName},1433;Database=${database.outputs.databaseName};Authentication=Active Directory Default;Encrypt=True;'

output managedIdentityName string = identity.outputs.name
output managedIdentityClientId string = identity.outputs.clientId
output keyVaultUri string = keyVault.outputs.vaultUri

output containerRegistryLoginServer string = registry.outputs.loginServer
output containerRegistryName string = registry.outputs.registryName

output location string = location
output resourceToken string = resourceToken
output tags object = tags