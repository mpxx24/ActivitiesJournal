using ActivitiesJournal.Models;

namespace ActivitiesJournal.Services;

public interface IGoalsService
{
    Task<GoalsData> LoadAsync();
    Task SaveAsync(GoalsData data);
}
