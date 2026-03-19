namespace ActivitiesJournal.Models;

public class GoalsViewModel
{
    public AnnualGoals Goals { get; set; } = new();
    public double CurrentDistanceKm { get; set; }
    public double CurrentElevationM { get; set; }
    public int CurrentRides { get; set; }
    public int Year { get; set; }

    public double DistancePct => Goals.DistanceGoalKm > 0 ? Math.Min(100, CurrentDistanceKm / Goals.DistanceGoalKm!.Value * 100) : 0;
    public double ElevationPct => Goals.ElevationGoalM > 0 ? Math.Min(100, CurrentElevationM / Goals.ElevationGoalM!.Value * 100) : 0;
    public double RidesPct => Goals.RidesGoal > 0 ? Math.Min(100, (double)CurrentRides / Goals.RidesGoal!.Value * 100) : 0;

    public string? DistanceProjection { get; set; }
    public string? ElevationProjection { get; set; }
    public string? RidesProjection { get; set; }

    public List<ChallengeProgress> ChallengeProgressList { get; set; } = new();
}
