# JetBrains IDE Setup Guide

Since User Secrets has permission issues, use environment variables instead.

## Option 1: Using the Setup Script (Recommended)

1. **Source the script before running**:
   ```bash
   cd ActivitiesJournal
   source set-secrets.sh
   dotnet run
   ```

2. **Or set them manually in your terminal**:
   ```bash
   export Strava__ClientSecret="YOUR_CLIENT_SECRET"
   export Strava__AccessToken="YOUR_ACCESS_TOKEN"
   export Strava__RefreshToken="YOUR_REFRESH_TOKEN"
   export Strava__ClientId="YOUR_CLIENT_ID_HERE"  # You need to get this from Strava
   ```

## Option 2: Configure in JetBrains Rider/IntelliJ

1. Open **Run/Debug Configurations**
2. Edit your configuration (or create a new one)
3. Go to **Environment variables**
4. Add these variables:
   - `Strava__ClientSecret` = `YOUR_CLIENT_SECRET`
   - `Strava__AccessToken` = `YOUR_ACCESS_TOKEN`
   - `Strava__RefreshToken` = `YOUR_REFRESH_TOKEN`
   - `Strava__ClientId` = `YOUR_CLIENT_ID` (get from https://www.strava.com/settings/api)

## Getting Your Client ID

1. Visit https://www.strava.com/settings/api
2. Find your **Client ID** (it's a number, usually 4-5 digits)
3. Add it to the environment variables as `Strava__ClientId`

## Verify Configuration

After setting the environment variables, run:
```bash
dotnet run
```

The application should now be able to connect to Strava API.
