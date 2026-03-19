using ActivitiesJournal.Models;

namespace ActivitiesJournal.Services;

public interface IDashboardService
{
    Task<DashboardViewModel> BuildDashboardAsync();
}
