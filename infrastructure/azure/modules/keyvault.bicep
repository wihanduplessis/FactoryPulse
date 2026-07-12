@description('Azure region.')
param location string

@description('Tags applied to the vault.')
param tags object

@description('Globally unique key vault name.')
param vaultName string

@description('Object ID of the application identity that reads secrets at runtime.')
param readerPrincipalId string

@description('Object ID of the human administrator who writes the secrets.')
param administratorObjectId string

// Built-in roles.
var secretsUserRoleDefinitionId = '4633458b-17de-408a-b874-0445c86b69e6'    // Key Vault Secrets User (read)
var secretsOfficerRoleDefinitionId = 'b86a8fe4-44ce-4948-aee5-eccb2c155cd7' // Key Vault Secrets Officer (read/write)

resource vault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: vaultName
  location: location
  tags: tags
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId

    // RBAC rather than the legacy access-policy model: permissions are Azure role
    // assignments like everything else, instead of a separate parallel system.
    enableRbacAuthorization: true

    enableSoftDelete: true
    softDeleteRetentionInDays: 7
  }
}

// The app can read secrets, and only read them.
resource secretsUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: vault
  name: guid(vault.id, readerPrincipalId, secretsUserRoleDefinitionId)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', secretsUserRoleDefinitionId)
    principalId: readerPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// The human operator can write them.
resource secretsOfficer 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: vault
  name: guid(vault.id, administratorObjectId, secretsOfficerRoleDefinitionId)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', secretsOfficerRoleDefinitionId)
    principalId: administratorObjectId
    principalType: 'User'
  }
}

output vaultName string = vault.name
output vaultUri string = vault.properties.vaultUri