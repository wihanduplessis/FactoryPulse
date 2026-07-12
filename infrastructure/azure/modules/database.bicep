@description('Azure region.')
param location string

@description('Tags applied to the server and database.')
param tags object

@description('Globally unique SQL logical server name.')
param serverName string

@description('Database name.')
param databaseName string

@description('Entra object ID of the server administrator.')
param administratorObjectId string

@description('Entra login (UPN) of the server administrator.')
param administratorLogin string

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: serverName
  location: location
  tags: tags
  properties: {
    administrators: {
      administratorType: 'ActiveDirectory'
      principalType: 'User'
      login: administratorLogin
      sid: administratorObjectId
      tenantId: subscription().tenantId

      // No SQL username/password exists on this server at all. Every connection —
      // yours, the app's, the migration runner's — authenticates as an Entra identity.
      azureADOnlyAuthentication: true
    }
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

resource database 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: databaseName
  location: location
  tags: tags
  sku: {
    name: 'GP_S_Gen5_2'
    tier: 'GeneralPurpose'
    family: 'Gen5'
    capacity: 2
  }
  properties: {
    // Serverless: scales down to 0.5 vCores and pauses entirely after an hour idle.
    autoPauseDelay: 60
    minCapacity: json('0.5')
    maxSizeBytes: 34359738368

    // The free offer: 100k vCore-seconds and 32 GB a month, at no cost. When the
    // monthly allowance runs out the database pauses rather than starting to bill.
    useFreeLimit: true
    freeLimitExhaustionBehavior: 'AutoPause'

    zoneRedundant: false
  }
}

// Container Apps on the Consumption plan has no fixed outbound IP, so the app cannot
// be allow-listed by address. This rule (the 0.0.0.0 sentinel) permits connections
// from Azure services. It is the weakest link in the design and is accepted knowingly:
// the server has *no* password to guess, so reaching it buys an attacker nothing
// without an Entra identity that already holds a role.
resource allowAzureServices 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAllWindowsAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

output serverName string = sqlServer.name
output fullyQualifiedDomainName string = sqlServer.properties.fullyQualifiedDomainName
output databaseName string = database.name