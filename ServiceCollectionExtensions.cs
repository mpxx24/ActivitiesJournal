using ActivitiesJournal.Configuration;
using ActivitiesJournal.Services;

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

        services.AddHttpClient<IStravaService, StravaService>();
        services.AddHttpClient("weather", c =>
        {
            c.BaseAddress = new Uri("https://archive-api.open-meteo.com/");
            c.Timeout = TimeSpan.FromSeconds(10);
        });

        services.AddSingleton<IGoalsService, GoalsService>();
        services.AddScoped<IGoalsAnalyticsService, GoalsAnalyticsService>();
        services.AddScoped<IDashboardService, DashboardService>();

        return services;
    }
}
