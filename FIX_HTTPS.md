# Fixing HTTPS 403 Error on macOS

If you're getting a 403 error when accessing `https://localhost:5001`, it's likely because the development certificate isn't trusted.

## Solution 1: Use HTTP Instead (Easiest)

Simply access the app via HTTP:
```
http://localhost:5000
```

The app is configured to work on both HTTP and HTTPS in development mode.

## Solution 2: Trust the Development Certificate

If you want to use HTTPS, trust the development certificate:

```bash
dotnet dev-certs https --trust
```

You may need to enter your password. After this, `https://localhost:5001` should work.

## Solution 3: Run with HTTP Profile Only

In JetBrains IDE, select the "http" profile instead of "https" when running:
- Run → Edit Configurations
- Select your configuration
- Change the profile to use "http" profile

## Verify

After applying a solution, restart the app and try accessing:
- HTTP: `http://localhost:5000` 
- HTTPS: `https://localhost:5001` (if you trusted the certificate)
