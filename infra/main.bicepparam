using './main.bicep'

// All parameter values are passed via the deploy script (deploy-activities-journal.sh)
// so that no real resource names or environment-specific values are committed to git.
//
// Required params:
//   appName             — App Service name (becomes <name>.azurewebsites.net)
//   storageAccountName  — Production storage account name
//
// Optional params (have defaults in main.bicep):
//   location            — Azure region (default: resource group location)
//   stravaBaseUrl       — Strava API base URL
//   deployerObjectId    — passed by script at deploy time
