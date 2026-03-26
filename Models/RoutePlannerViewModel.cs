namespace ActivitiesJournal.Models;

public class RoutePlannerIndexViewModel
{
    public IReadOnlyList<PlannedRoute> Routes { get; set; } = [];
}

public class RoutePlannerDetailViewModel
{
    public PlannedRoute Route { get; set; } = null!;
}
