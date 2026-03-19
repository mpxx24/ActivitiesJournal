using ActivitiesJournal.Models;

namespace ActivitiesJournal.Services;

public class GoalsAnalyticsService : IGoalsAnalyticsService
{
    private readonly IStravaService _strava;
    private readonly IGoalsService _goals;

    public GoalsAnalyticsService(IStravaService strava, IGoalsService goals)
    {
        _strava = strava;
        _goals = goals;
    }

    public async Task<GoalsViewModel> BuildGoalsViewModelAsync()
    {
        var all = await _strava.GetAllActivitiesAsync();
        var rides = all.Where(a => SportTypes.IsRide(a.SportType)).ToList();

        int year = DateTime.Now.Year;
        var yearRides = rides.Where(a => a.StartDateLocal.Year == year).ToList();
        double distKm = yearRides.Sum(a => a.Distance) / 1000.0;
        double elevM = yearRides.Sum(a => (double)a.TotalElevationGain);
        int rideCount = yearRides.Count;

        var data = await _goals.LoadAsync();
        var goals = data.AnnualGoals.FirstOrDefault(g => g.Year == year)
            ?? new AnnualGoals { Year = year };

        int dayOfYear = DateTime.Now.DayOfYear;
        int daysInYear = DateTime.IsLeapYear(year) ? 366 : 365;
        double fraction = dayOfYear / (double)daysInYear;

        var progressList = data.Challenges.Select(c =>
        {
            var km = rides.Where(a => a.StartDateLocal.Date >= c.StartDate.Date)
                          .Sum(a => a.Distance) / 1000.0;
            return new ChallengeProgress { Challenge = c, EarnedKm = Math.Round(km, 1) };
        }).ToList();

        return new GoalsViewModel
        {
            Year = year,
            Goals = goals,
            CurrentDistanceKm = Math.Round(distKm, 1),
            CurrentElevationM = Math.Round(elevM, 0),
            CurrentRides = rideCount,
            DistanceProjection = Project(distKm, goals.DistanceGoalKm, fraction, daysInYear, dayOfYear),
            ElevationProjection = Project(elevM, goals.ElevationGoalM, fraction, daysInYear, dayOfYear),
            RidesProjection = Project(rideCount, goals.RidesGoal, fraction, daysInYear, dayOfYear),
            ChallengeProgressList = progressList,
        };
    }

    internal static string? Project(double current, double? target, double fraction, int daysInYear, int dayOfYear)
    {
        if (target == null || fraction <= 0) return null;
        double projected = current / fraction;
        if (projected >= target) return "On track!";
        double needed = target.Value - current;
        double daysLeft = daysInYear - dayOfYear;
        return $"On pace for {projected:0} — need {needed:0} more in {daysLeft:0} days";
    }
}
