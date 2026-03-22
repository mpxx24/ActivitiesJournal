using ActivitiesJournal.Models;

namespace ActivitiesJournal.Services;

public interface IGoalsService
{
    Task<GoalsData> LoadAsync(long athleteId);
    Task SaveAsync(GoalsData data, long athleteId);
}
