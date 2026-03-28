using ActivitiesJournal.Models;

namespace ActivitiesJournal.Services;

public interface IActivityStatsService
{
    Task<BadgesViewModel> GetBadgesAsync(string type);
    Task<PersonalRecordsViewModel> GetPersonalRecordsAsync(string type);
    Task<(SegmentsViewModel Segments, BestEffortsViewModel BestEfforts)> GetSegmentsAsync(int count);
    Task<FitnessViewModel> GetFitnessAsync(int days);
    Task<AnalysisViewModel> GetAnalysisAsync(int? year);
}
