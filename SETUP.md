# Quick Setup Guide

## Prerequisites Check

First, verify you have .NET 8 SDK installed:

```bash
dotnet --version
```

You should see version 8.x. If you have a lower version, install .NET 8 SDK from https://dotnet.microsoft.com/download/dotnet/8.0

## Step 1: Initialize Git (if needed)

If Git initialization failed due to permissions, you can initialize it manually:

```bash
cd ActivitiesJournal
git init
git add .
git commit -m "Initial commit: Activities Journal app"
```

## Step 2: Configure User Secrets

Run these commands in the `ActivitiesJournal` directory:

```bash
# Initialize user secrets
dotnet user-secrets init

# Set your Strava API credentials
dotnet user-secrets set "Strava:ClientId" "YOUR_CLIENT_ID_HERE"
dotnet user-secrets set "Strava:ClientSecret" "YOUR_CLIENT_SECRET_HERE"
dotnet user-secrets set "Strava:AccessToken" "YOUR_ACCESS_TOKEN_HERE"
dotnet user-secrets set "Strava:RefreshToken" "YOUR_REFRESH_TOKEN_HERE"
```

## Step 3: Get Your Strava Credentials

1. Visit https://www.strava.com/settings/api
2. Create a new application
3. Copy your Client ID and Client Secret
4. For quick testing, you can use the "Personal Access Token" shown on that page as your AccessToken

## Step 4: Run the Application

```bash
dotnet run
```

Navigate to the URL shown in the console (typically https://localhost:5001)

## Verify User Secrets

To verify your secrets are set correctly:

```bash
dotnet user-secrets list
```

You should see your Strava configuration keys listed.
