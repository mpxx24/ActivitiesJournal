# Troubleshooting Guide

## HTTP 403 Forbidden Error

If you're getting a **403 Forbidden** error when trying to access Strava activities, here are the most common causes and solutions:

### 1. Access Token Not Configured

**Symptom**: Error message says "Access token is not configured"

**Solution**: Set up your User Secrets:

```bash
dotnet user-secrets init
dotnet user-secrets set "Strava:AccessToken" "YOUR_ACCESS_TOKEN"
```

### 2. Invalid or Expired Access Token

**Symptom**: 403 error even though token is configured

**Solution**: 
- Get a new access token from [Strava API Settings](https://www.strava.com/settings/api)
- Use the "Personal Access Token" shown on that page for quick testing
- Or complete the OAuth flow to get a proper token with the right scopes

### 3. Insufficient Permissions/Scopes

**Symptom**: 403 error with valid token

**Solution**: 
- Your access token needs the `activity:read_all` scope
- Personal Access Tokens from the settings page may have limited scope
- Use the OAuth flow (`/Strava/Authorize`) to get a token with full permissions

### 4. Token Doesn't Have Required Scope

**Symptom**: 403 when accessing activities

**Solution**:
1. Go to `/Strava/Authorize` in your app
2. Complete the OAuth authorization
3. This will give you a token with `activity:read_all` scope

### 5. Empty or Null Access Token

**Symptom**: Configuration appears set but still getting 403

**Solution**:
```bash
# Verify your secrets are set
dotnet user-secrets list

# If empty, set them again
dotnet user-secrets set "Strava:ClientId" "YOUR_CLIENT_ID"
dotnet user-secrets set "Strava:ClientSecret" "YOUR_CLIENT_SECRET"
dotnet user-secrets set "Strava:AccessToken" "YOUR_ACCESS_TOKEN"
dotnet user-secrets set "Strava:RefreshToken" "YOUR_REFRESH_TOKEN"
```

## Quick Diagnostic Steps

1. **Check if secrets are configured**:
   ```bash
   dotnet user-secrets list
   ```

2. **Verify the access token is not empty**:
   - The token should be a long string (typically 40+ characters)
   - If it's empty or "YOUR_ACCESS_TOKEN", it won't work

3. **Check the application logs**:
   - Look for error messages in the console/IDE output
   - The logs will show the exact Strava API error response

4. **Test with a fresh token**:
   - Go to https://www.strava.com/settings/api
   - Copy the "Personal Access Token" (if available)
   - Update it in user secrets

## Common Error Messages

### "Access token is not configured"
- **Fix**: Run `dotnet user-secrets set "Strava:AccessToken" "YOUR_TOKEN"`

### "Access forbidden (403)"
- **Fix**: Get a new token with proper scopes via OAuth flow

### "Unauthorized (401)"
- **Fix**: Token expired, try refreshing or get a new one

## Getting a Valid Access Token

### Option 1: Personal Access Token (Quick Test)
1. Visit https://www.strava.com/settings/api
2. Scroll to "Personal Access Token"
3. Copy the token
4. Set it: `dotnet user-secrets set "Strava:AccessToken" "YOUR_TOKEN"`

### Option 2: OAuth Flow (Recommended)
1. Configure your Strava app with callback URL: `http://localhost:5000/Strava/Callback`
2. Visit `/Strava/Authorize` in your app
3. Authorize the application
4. The callback will receive an authorization code
5. Exchange the code for tokens (currently needs manual implementation)

## Still Having Issues?

1. Check the console/IDE logs for detailed error messages
2. Verify your Strava API application is set up correctly
3. Ensure your callback URL matches in Strava settings
4. Try using a fresh Personal Access Token from Strava settings
