// ============================================================
// Activities Journal - Azure Infrastructure
// Resources: App Service Plan (F1/Free), App Service, Key Vault
// Auth: System-assigned Managed Identity + RBAC (no secrets in config)
// ============================================================

@description('Azure region for all resources')
param location string = resourceGroup().location

@description('Base name for the App Service (must be globally unique - becomes <name>.azurewebsites.net)')
param appName string

@description('Strava API base URL')
param stravaBaseUrl string = 'https://www.strava.com/api/v3'

@description('Object ID of the user/principal running the deployment — gets Key Vault Secrets Officer role so they can manage secrets via CLI. Find with: az ad signed-in-user show --query id -o tsv')
param deployerObjectId string = ''

// Key Vault name: max 24 chars, globally unique
var kvName = 'kv${take(uniqueString(resourceGroup().id, appName), 21)}'
var appServicePlanName = 'plan-${appName}'

// ------------------------------------------------------------
// Log Analytics Workspace (required for workspace-based App Insights)
// Free tier: 5 GB/month ingestion — more than enough for a personal app
// ------------------------------------------------------------
resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: 'log-${appName}'
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

// ------------------------------------------------------------
// Application Insights (workspace-based)
// ------------------------------------------------------------
resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: 'appi-${appName}'
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
  }
}

// ------------------------------------------------------------
// App Service Plan — B1 Basic (Linux)
// ~€11/month, no compute-time quota, supports Always On
// ------------------------------------------------------------
resource appServicePlan 'Microsoft.Web/serverfarms@2022-09-01' = {
  name: appServicePlanName
  location: location
  sku: {
    name: 'B1'
    tier: 'Basic'
  }
  properties: {
    reserved: true // Linux
  }
}

// ------------------------------------------------------------
// App Service
// ------------------------------------------------------------
resource appService 'Microsoft.Web/sites@2022-09-01' = {
  name: appName
  location: location
  identity: {
    type: 'SystemAssigned' // Managed Identity — no credentials needed to access Key Vault
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|8.0'
      alwaysOn: true
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      http20Enabled: true
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
        {
          name: 'Strava__BaseUrl'
          value: stravaBaseUrl
        }
        // Key Vault references — resolved by App Service using Managed Identity at startup.
        // Secret names use '--' because KV doesn't allow ':'.
        // App Service maps them to env vars with '__', which ASP.NET Core reads as nested config.
        {
          name: 'Strava__ClientId'
          value: '@Microsoft.KeyVault(VaultName=${kvName};SecretName=Strava--ClientId)'
        }
        {
          name: 'Strava__ClientSecret'
          value: '@Microsoft.KeyVault(VaultName=${kvName};SecretName=Strava--ClientSecret)'
        }
        {
          name: 'Strava__AccessToken'
          value: '@Microsoft.KeyVault(VaultName=${kvName};SecretName=Strava--AccessToken)'
        }
        {
          name: 'Strava__RefreshToken'
          value: '@Microsoft.KeyVault(VaultName=${kvName};SecretName=Strava--RefreshToken)'
        }
        {
          // Must match the callback URL registered in your Strava API settings
          name: 'Strava__RedirectUri'
          value: 'https://${appName}.azurewebsites.net/Strava/Callback'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsights.properties.ConnectionString
        }
        {
          name: 'ApplicationInsightsAgent_EXTENSION_VERSION'
          value: '~3'
        }
      ]
    }
  }
}

// ------------------------------------------------------------
// Key Vault — Standard, RBAC-based access (modern, no access policies)
// ------------------------------------------------------------
resource keyVault 'Microsoft.KeyVault/vaults@2023-02-01' = {
  name: kvName
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true      // Use Azure RBAC instead of legacy access policies
    enableSoftDelete: true
    softDeleteRetentionInDays: 7       // Minimum — reduces cost/clutter for a personal project
    enabledForDeployment: false
    enabledForDiskEncryption: false
    enabledForTemplateDeployment: false
    publicNetworkAccess: 'Enabled'     // App Service needs to reach KV over public endpoint
  }
}

// ------------------------------------------------------------
// Role assignment: App Service Managed Identity → Key Vault Secrets User
// This role allows reading secret values (but not managing secrets)
// ------------------------------------------------------------
var kvSecretsUserRoleId    = '4633458b-17de-408a-b874-0445c86b69e6' // Key Vault Secrets User (built-in)
var kvSecretsOfficerRoleId = 'b86a8fe4-44ce-4948-aee5-eccb2c155cd7' // Key Vault Secrets Officer (built-in)

resource kvRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, appService.id, kvSecretsUserRoleId)
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', kvSecretsUserRoleId)
    principalId: appService.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// ------------------------------------------------------------
// Role assignment: deployer identity → Key Vault Secrets Officer
// Allows the person running the deploy script to set/update secrets via CLI.
// Skipped when deployerObjectId is empty (e.g. CI/CD with pre-assigned roles).
// ------------------------------------------------------------
resource kvDeployerRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(deployerObjectId)) {
  name: guid(keyVault.id, deployerObjectId, kvSecretsOfficerRoleId)
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', kvSecretsOfficerRoleId)
    principalId: deployerObjectId
    principalType: 'User'
  }
}

// ------------------------------------------------------------
// Outputs
// ------------------------------------------------------------
output appServiceUrl string = 'https://${appService.properties.defaultHostName}'
output keyVaultName string = keyVault.name
output appServiceName string = appService.name
output appInsightsName string = appInsights.name
output appInsightsConnectionString string = appInsights.properties.ConnectionString
