using ActivitiesJournal.Configuration;
using ActivitiesJournal.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;

namespace ActivitiesJournal;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddActivitiesJournalServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllersWithViews();
        services.AddMemoryCache();

        var appInsightsConnStr = configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
        if (!string.IsNullOrEmpty(appInsightsConnStr))
            services.AddApplicationInsightsTelemetry();

        services.Configure<StravaOptions>(configuration.GetSection("Strava"));
        services.Configure<StorageOptions>(configuration.GetSection("Storage"));
        services.Configure<TrackOwnerOptions>(configuration.GetSection("TrackOwner"));

        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath = "/";
                options.AccessDeniedPath = "/";
                options.ExpireTimeSpan = TimeSpan.FromDays(30);
                options.SlidingExpiration = true;
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.Cookie.SameSite = SameSiteMode.Lax;
            });

        services.AddHttpContextAccessor();
        services.AddSingleton<ITokenStore, TokenStore>();

        services.AddHttpClient<IStravaService, StravaService>((sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<StravaOptions>>().Value;
            client.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/");
        });
        services.AddHttpClient("weather", c =>
        {
            c.BaseAddress = new Uri("https://archive-api.open-meteo.com/");
            c.Timeout = TimeSpan.FromSeconds(10);
        });

        services.AddHostedService<AzureBlobStorageInitializer>();
        services.AddSingleton<ISegmentPolylineCacheService, SegmentPolylineCacheService>();
        services.AddSingleton<IGoalsService, GoalsService>();
        services.AddScoped<IGoalsAnalyticsService, GoalsAnalyticsService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddSingleton<ITrackStorageService, TrackStorageService>();
        services.AddSingleton<ITrackParserService, TrackParserService>();
        services.AddHostedService<TrackMigrationService>();

        return services;
    }
}
