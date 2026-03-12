namespace ActivitiesJournal.Models;

public class AnnualGoals
{
    public int Year { get; set; }
    public double? DistanceGoalKm { get; set; }
    public double? ElevationGoalM { get; set; }
    public int? RidesGoal { get; set; }
}

public class VirtualChallenge
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public double TargetKm { get; set; }
    public DateTime StartDate { get; set; }
    public bool IsPreset { get; set; }  // built-in challenges can't be deleted
}

public class GoalsData
{
    public List<AnnualGoals> AnnualGoals { get; set; } = new();
    public List<VirtualChallenge> Challenges { get; set; } = new();
}

public class GoalsViewModel
{
    public AnnualGoals Goals { get; set; } = new();
    public double CurrentDistanceKm { get; set; }
    public double CurrentElevationM { get; set; }
    public int CurrentRides { get; set; }
    public int Year { get; set; }

    public double DistancePct => Goals.DistanceGoalKm > 0 ? Math.Min(100, CurrentDistanceKm / Goals.DistanceGoalKm.Value * 100) : 0;
    public double ElevationPct => Goals.ElevationGoalM > 0 ? Math.Min(100, CurrentElevationM / Goals.ElevationGoalM.Value * 100) : 0;
    public double RidesPct => Goals.RidesGoal > 0 ? Math.Min(100, (double)CurrentRides / Goals.RidesGoal.Value * 100) : 0;

    // Project finish based on days elapsed vs total days in year
    public string? DistanceProjection { get; set; }
    public string? ElevationProjection { get; set; }
    public string? RidesProjection { get; set; }

    public List<ChallengeProgress> ChallengeProgressList { get; set; } = new();
}

public class ChallengeProgress
{
    public VirtualChallenge Challenge { get; set; } = new();
    public double EarnedKm { get; set; }
    public double Pct => Math.Min(100, Challenge.TargetKm > 0 ? EarnedKm / Challenge.TargetKm * 100 : 0);
    public bool Completed => EarnedKm >= Challenge.TargetKm;
}
