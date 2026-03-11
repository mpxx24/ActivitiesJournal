using './main.bicep'

// App Service name — must be globally unique (becomes <name>.azurewebsites.net)
// Allowed characters: letters, numbers, hyphens. Length: 2–60.
param appName = 'myactivitiesjournal'

// Azure region — westeurope or northeurope are closest for Poland
param location = 'westeurope'
