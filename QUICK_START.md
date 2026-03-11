# Quick Start - Your Credentials Are Ready!

Your Strava credentials have been configured. Here's how to run the app:

## For JetBrains IDE (Rider/IntelliJ)

### Method 1: Set Environment Variables in IDE (Easiest)

1. **Open Run/Debug Configurations** (Run → Edit Configurations)
2. Find or create your configuration for the ActivitiesJournal project
3. Click on **Environment variables**
4. Add these variables (click the + icon for each):
   - `Strava__ClientSecret` = `YOUR_CLIENT_SECRET`
   - `Strava__AccessToken` = `YOUR_ACCESS_TOKEN`
   - `Strava__RefreshToken` = `YOUR_REFRESH_TOKEN`
   - `Strava__ClientId` = `YOUR_CLIENT_ID` ← **You need to get this from Strava**

5. **Get your Client ID**:
   - Go to https://www.strava.com/settings/api
   - Find your **Client ID** (it's a number)
   - Add it as `Strava__ClientId` in the environment variables

6. **Run the application** from the IDE

### Method 2: Use Terminal in IDE

1. Open the terminal in JetBrains IDE
2. Run:
   ```bash
   cd ActivitiesJournal
   source set-secrets.sh
   export Strava__ClientId="YOUR_CLIENT_ID"  # Add your Client ID
   dotnet run
   ```

## Important: You Still Need Your Client ID

Your Client ID is different from the Client Secret. To get it:
1. Visit https://www.strava.com/settings/api
2. Look for **Client ID** (usually a 4-5 digit number)
3. Add it to environment variables as `Strava__ClientId`

## Test the Application

Once you've set all the environment variables (including ClientId), run the app and:
1. Navigate to http://localhost:5000 or https://localhost:5001
2. Click on "Activities" in the navigation
3. You should see your Strava activities!

## Troubleshooting

If you still get a 403 error:
- Make sure all 4 environment variables are set (ClientId, ClientSecret, AccessToken, RefreshToken)
- Check the IDE console/logs for detailed error messages
- Verify your access token is still valid at https://www.strava.com/settings/api
