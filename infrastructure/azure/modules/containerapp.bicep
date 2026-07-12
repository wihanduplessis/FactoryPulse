@description('Azure region.')
param location string

@description('Tags applied to the resources.')
param tags object

@description('Name of the Container Apps managed environment.')
param managedEnvironmentName string

@description('Name of the container app.')
param containerAppName string

@description('Name of the Log Analytics workspace the environment logs to.')
param workspaceName string

@description('Resource ID of the user-assigned managed identity the app runs as.')
param identityId string

@description('Client ID of that identity, so DefaultAzureCredential picks the right one.')
param identityClientId string

@description('Login server of the container registry, e.g. myregistry.azurecr.io')
param registryLoginServer string

@description('Image tag to deploy — the commit SHA, not "latest".')
param imageTag string

@description('URI of the key vault holding the JWT signing key and seed admin password.')
param keyVaultUri string

@description('Application Insights connection string.')
param appInsightsConnectionString string

@description('Passwordless SQL connection string (Entra authentication).')
param sqlConnectionString string

@description('Commit SHA, stamped onto every log line.')
param buildSha string

resource workspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' existing = {
  name: workspaceName
}

resource managedEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: managedEnvironmentName
  location: location
  tags: tags
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: workspace.properties.customerId
        sharedKey: workspace.listKeys().primarySharedKey
      }
    }
  }
}

resource containerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: containerAppName
  location: location
  tags: tags

  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${identityId}': {}
    }
  }

  properties: {
    managedEnvironmentId: managedEnvironment.id

    configuration: {
      activeRevisionsMode: 'Single'

      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
      }

      registries: [
        {
          server: registryLoginServer
          identity: identityId
        }
      ]
    }

    template: {
      containers: [
        {
          name: 'api'
          image: '${registryLoginServer}/factorypulse-api:${imageTag}'
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }

            { name: 'AZURE_CLIENT_ID', value: identityClientId }

            { name: 'KeyVaultUri', value: keyVaultUri }
            { name: 'ConnectionStrings__FactoryPulseDatabase', value: sqlConnectionString }
            { name: 'ApplicationInsights__ConnectionString', value: appInsightsConnectionString }

            { name: 'UseHttpsRedirection', value: 'false' }

            { name: 'UseForwardedHeaders', value: 'true' }

            { name: 'ApplyMigrationsOnStartup', value: 'false' }
            { name: 'SeedIdentityOnStartup', value: 'true' }

            { name: 'EnableSwagger', value: 'true' }
            { name: 'BuildSha', value: buildSha }
          ]
        }
      ]

      scale: {

        minReplicas: 0
        maxReplicas: 1
      }
    }
  }
}

output fqdn string = containerApp.properties.configuration.ingress.fqdn
output containerAppName string = containerApp.name