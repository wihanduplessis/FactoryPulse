@description('Azure region for the registry.')
param location string

@description('Tags applied to the registry.')
param tags object

@description('Globally unique registry name (alphanumeric, lowercase).')
param registryName string

@description('Object ID of the identity allowed to push images (the GitHub Actions service principal).')
param pushPrincipalId string

// Built-in role: AcrPush.
var acrPushRoleDefinitionId = '8311e382-0749-4cb8-b61a-304f252e45ec'

resource registry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: registryName
  location: location
  tags: tags
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: false
  }
}

resource acrPush 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: registry
  name: guid(registry.id, pushPrincipalId, acrPushRoleDefinitionId)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', acrPushRoleDefinitionId)
    principalId: pushPrincipalId
    principalType: 'ServicePrincipal'
  }
}

output loginServer string = registry.properties.loginServer
output registryName string = registry.name