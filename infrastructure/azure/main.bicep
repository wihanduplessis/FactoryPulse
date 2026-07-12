@description('Azure region for all resources. Defaults to the resource group location.')
param location string = resourceGroup().location

@description('Short application name, used to build resource names.')
@minLength(3)
@maxLength(12)
param applicationName string = 'factorypulse'

@description('Object ID of the GitHub Actions service principal that pushes images.')
param githubPrincipalId string

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

module registry 'modules/registry.bicep' = {
  name: 'registry'
  params: {
    location: location
    tags: tags
    registryName: registryName
    pushPrincipalId: githubPrincipalId
  }
}

output containerRegistryLoginServer string = registry.outputs.loginServer
output containerRegistryName string = registry.outputs.registryName

output location string = location
output resourceToken string = resourceToken
output tags object = tags