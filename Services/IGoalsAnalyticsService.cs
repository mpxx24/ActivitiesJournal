using ActivitiesJournal.Models;

namespace ActivitiesJournal.Services;

public interface IGoalsAnalyticsService
{
    Task<GoalsViewModel> BuildGoalsViewModelAsync();
}
