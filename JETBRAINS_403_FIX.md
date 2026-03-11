# Fix 403 Error in JetBrains IDE

If you're getting a 403 error when accessing localhost, follow these steps:

## Quick Fix: Use HTTP Only

The application is now configured to use **HTTP only** in development mode to avoid certificate issues.

### Step 1: Check Your Run Configuration

1. In JetBrains IDE, go to **Run → Edit Configurations**
2. Select your ActivitiesJournal configuration
3. Make sure **Application arguments** or **Environment** shows:
   - `ASPNETCORE_ENVIRONMENT=Development`
4. The app should run on: `http://localhost:5000`

### Step 2: Access via HTTP

**Always use HTTP (not HTTPS):**
```
http://localhost:5000
```

**NOT:**
```
https://localhost:5001  ❌ (will give 403 error)
```

### Step 3: Verify Port

Check the console output when you run the app. You should see:
```
Now listening on: http://localhost:5000
```

### Step 4: If Still Getting 403

1. **Stop the application** completely
2. **Clear browser cache** or try incognito/private mode
3. **Restart the application**
4. Make sure you're accessing `http://localhost:5000` (not https)

## Alternative: Trust Development Certificate

If you really want to use HTTPS:

```bash
dotnet dev-certs https --trust
```

Then restart the app and access `https://localhost:5001`.

## Verify It's Working

Once running, you should be able to:
- Access `http://localhost:5000` without errors
- See the home page
- Navigate to Activities page
- View your Strava activities

If you still see 403, check:
- Browser console for errors
- Application logs in the IDE
- Make sure no firewall is blocking localhost
