# Activities Journal

A personal activities journal web application built with .NET 8 that integrates with the Strava API to retrieve and display your athletic activities with detailed information.

## Features

- 📊 View all your Strava activities in a beautiful, organized interface
- 🔍 Detailed activity view with comprehensive metrics including:
  - Distance, time, elevation gain
  - Speed metrics (average and max)
  - Heart rate data (if available)
  - Power metrics (for cycling activities)
  - Best efforts and personal records
  - Activity splits
  - Gear information
  - Photos, kudos, and comments
- 🔐 Secure storage of API credentials using .NET User Secrets
- 🎨 Modern, responsive UI built with Bootstrap 5

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
  - You can check your installed SDK version with: `dotnet --version`
  - Download .NET 8 SDK from: https://dotnet.microsoft.com/download/dotnet/8.0
- A Strava account
- Strava API credentials (Client ID and Client Secret)

## Getting Started

### 1. Clone or Download the Project

```bash
cd ActivitiesJournal
```

### 2. Get Strava API Credentials

1. Go to [Strava API Settings](https://www.strava.com/settings/api)
2. Create a new application
3. Note down your **Client ID** and **Client Secret**
4. Set the **Authorization Callback Domain** to `localhost` (for development)

### 3. Configure User Secrets

User Secrets are used to store sensitive configuration data like API keys. They are stored outside of your project directory and are not committed to Git.

#### On Windows:
```bash
dotnet user-secrets init
dotnet user-secrets set "Strava:ClientId" "YOUR_CLIENT_ID"
dotnet user-secrets set "Strava:ClientSecret" "YOUR_CLIENT_SECRET"
dotnet user-secrets set "Strava:AccessToken" "YOUR_ACCESS_TOKEN"
dotnet user-secrets set "Strava:RefreshToken" "YOUR_REFRESH_TOKEN"
```

#### On macOS/Linux:
```bash
dotnet user-secrets init
dotnet user-secrets set "Strava:ClientId" "YOUR_CLIENT_ID"
dotnet user-secrets set "Strava:ClientSecret" "YOUR_CLIENT_SECRET"
dotnet user-secrets set "Strava:AccessToken" "YOUR_ACCESS_TOKEN"
dotnet user-secrets set "Strava:RefreshToken" "YOUR_REFRESH_TOKEN"
```

### 4. Getting Your Access Token

#### Option A: Using OAuth Flow (Recommended for Production)

1. Run the application
2. Navigate to `/Strava/Authorize` or click "Connect Strava" in the navigation
3. Authorize the application in Strava
4. You'll receive an authorization code in the callback URL
5. Exchange the code for tokens (this step needs to be implemented in the callback handler)

#### Option B: Using Public Access Token (Quick Start for Testing)

1. Go to your [Strava API Settings](https://www.strava.com/settings/api)
2. Scroll down to find your **Personal Access Token**
3. Copy this token and set it as your `AccessToken` in user secrets
4. Note: This token has limited scope and may not show all activity details

### 5. Run the Application

```bash
dotnet run
```

The application will start on `https://localhost:5001` (or `http://localhost:5000`). Navigate to the URL shown in the console.

## Project Structure

```
ActivitiesJournal/
├── Controllers/
│   ├── ActivitiesController.cs    # Handles activity listing and details
│   ├── HomeController.cs           # Home page controller
│   └── StravaController.cs        # Handles Strava OAuth flow
├── Models/
│   ├── StravaActivity.cs          # Complete Strava activity model
│   └── StravaConfig.cs            # Configuration model
├── Services/
│   ├── IStravaService.cs          # Strava service interface
│   └── StravaService.cs           # Strava API integration service
├── Views/
│   ├── Activities/
│   │   ├── Index.cshtml           # Activities list view
│   │   └── Details.cshtml         # Activity detail view
│   └── Shared/
│       └── _Layout.cshtml         # Main layout
├── Program.cs                      # Application entry point and configuration
└── appsettings.json               # Application settings (non-sensitive)
```

## Configuration

### User Secrets Structure

The application expects the following structure in user secrets:

```json
{
  "Strava": {
    "ClientId": "your_client_id",
    "ClientSecret": "your_client_secret",
    "AccessToken": "your_access_token",
    "RefreshToken": "your_refresh_token",
    "BaseUrl": "https://www.strava.com/api/v3"
  }
}
```

### Environment Variables (Alternative)

You can also use environment variables with the same structure:

```bash
export Strava__ClientId="your_client_id"
export Strava__ClientSecret="your_client_secret"
export Strava__AccessToken="your_access_token"
export Strava__RefreshToken="your_refresh_token"
```

## Strava API Scopes

The application requests the following OAuth scopes:
- `activity:read_all` - Read all activity data including private activities

For more information about Strava API scopes, visit the [Strava API Documentation](https://developers.strava.com/docs/authentication/).

## Features in Detail

### Activity List View
- Displays activities in a card-based grid layout
- Shows key metrics: distance, time, elevation, speed
- Pagination support
- Quick access to activity details

### Activity Detail View
- Comprehensive activity information
- Heart rate data visualization
- Power metrics (for cycling)
- Best efforts and personal records
- Activity splits
- Gear information
- Social metrics (kudos, comments, photos)

## Troubleshooting

### "Failed to load activities" Error

1. Verify your access token is valid and not expired
2. Check that your user secrets are configured correctly
3. Ensure your Strava API application has the correct scopes
4. Check the application logs for detailed error messages

### Access Token Expired

The application automatically attempts to refresh expired tokens using the refresh token. If refresh fails:
1. Re-authorize the application through the OAuth flow
2. Update your access and refresh tokens in user secrets

### No Activities Showing

1. Verify you have activities in your Strava account
2. Check that your access token has the `activity:read_all` scope
3. Try refreshing the page or checking the browser console for errors

## Security Notes

- **Never commit user secrets to Git** - They are automatically excluded via `.gitignore`
- **Use User Secrets for development** - For production, use secure configuration providers like Azure Key Vault, AWS Secrets Manager, or similar
- **Rotate tokens regularly** - Keep your API credentials secure and rotate them periodically
- **Limit API scope** - Only request the minimum scopes needed for your application

## Development

### Building the Project

```bash
dotnet build
```

### Running Tests

```bash
dotnet test
```

### Publishing

```bash
dotnet publish -c Release
```

## API Rate Limits

Strava API has rate limits:
- **Default**: 600 requests per 15 minutes
- **Daily**: 30,000 requests per day

The application includes basic error handling for rate limits, but you may want to implement caching or request throttling for production use.

## Future Enhancements

Potential features to add:
- Activity filtering and search
- Data visualization and charts
- Export activities to CSV/JSON
- Activity statistics and trends
- Integration with other fitness platforms
- Activity comparison tools

## License

This project is for personal use. Please respect Strava's API Terms of Service.

## Resources

- [Strava API Documentation](https://developers.strava.com/docs/)
- [.NET 8 Documentation](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-8/)
- [ASP.NET Core Documentation](https://learn.microsoft.com/en-us/aspnet/core/)

## Support

For issues related to:
- **Strava API**: Check the [Strava API Documentation](https://developers.strava.com/docs/)
- **.NET**: Visit [.NET Documentation](https://learn.microsoft.com/dotnet/)
- **This Application**: Review the code and configuration

---

**Note**: This application is designed for personal use. Make sure to comply with Strava's API Terms of Service and rate limits.
