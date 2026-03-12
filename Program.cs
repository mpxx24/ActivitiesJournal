var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddMemoryCache();

// App Insights: only enable when connection string is configured (set via APPLICATIONINSIGHTS_CONNECTION_STRING app setting)
var appInsightsConnStr = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
if (!string.IsNullOrEmpty(appInsightsConnStr))
    builder.Services.AddApplicationInsightsTelemetry();

// Ensure environment variables are loaded (they should be by default, but let's be explicit)
// The configuration system automatically reads:
// 1. appsettings.json
// 2. appsettings.{Environment}.json
// 3. Environment variables (with __ separator)
// 4. User Secrets (if available)

// Configure Strava settings from configuration (User Secrets, appsettings.json, environment variables, etc.)
builder.Services.Configure<ActivitiesJournal.Models.StravaConfig>(
    builder.Configuration.GetSection("Strava"));

// Validate Strava configuration at startup
var stravaConfig = builder.Configuration.GetSection("Strava").Get<ActivitiesJournal.Models.StravaConfig>();
var startupLogger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger<Program>();

if (stravaConfig == null)
{
    startupLogger.LogWarning("⚠️  Strava configuration section not found!");
}
else
{
    startupLogger.LogInformation("Strava Configuration Check:");
    startupLogger.LogInformation("  ClientId: {Status}", string.IsNullOrEmpty(stravaConfig.ClientId) ? "❌ NOT SET" : "✓ Set");
    startupLogger.LogInformation("  ClientSecret: {Status}", string.IsNullOrEmpty(stravaConfig.ClientSecret) ? "❌ NOT SET" : "✓ Set");
    startupLogger.LogInformation("  AccessToken: {Status}", string.IsNullOrEmpty(stravaConfig.AccessToken) ? "❌ NOT SET" : "✓ Set");
    startupLogger.LogInformation("  RefreshToken: {Status}", string.IsNullOrEmpty(stravaConfig.RefreshToken) ? "❌ NOT SET" : "✓ Set");
    
    if (string.IsNullOrEmpty(stravaConfig.AccessToken))
    {
        startupLogger.LogWarning("⚠️  Strava AccessToken is not configured!");
        startupLogger.LogWarning("   Set environment variables:");
        startupLogger.LogWarning("     export Strava__AccessToken=\"YOUR_TOKEN\"");
        startupLogger.LogWarning("     export Strava__ClientId=\"YOUR_CLIENT_ID\"");
        startupLogger.LogWarning("     export Strava__ClientSecret=\"YOUR_SECRET\"");
        startupLogger.LogWarning("     export Strava__RefreshToken=\"YOUR_REFRESH_TOKEN\"");
    }
}

// Register Strava service
builder.Services.AddHttpClient<ActivitiesJournal.Services.IStravaService, ActivitiesJournal.Services.StravaService>();
builder.Services.AddSingleton<ActivitiesJournal.Services.GoalsService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// In development, disable HTTPS redirection to avoid certificate issues
// Access the app via HTTP: http://localhost:5000
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
