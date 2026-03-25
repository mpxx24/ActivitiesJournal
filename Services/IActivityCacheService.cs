using ActivitiesJournal.Models;

namespace ActivitiesJournal.Services;

public interface IActivityCacheService
{
    Task<List<StravaActivity>> GetAsync(long athleteId);
    Task SetAsync(long athleteId, List<StravaActivity> activities);
}
