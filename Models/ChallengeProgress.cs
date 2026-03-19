namespace ActivitiesJournal.Models;

public class ChallengeProgress
{
    public VirtualChallenge Challenge { get; set; } = new();
    public double EarnedKm { get; set; }
    public double Pct => Math.Min(100, Challenge.TargetKm > 0 ? EarnedKm / Challenge.TargetKm * 100 : 0);
    public bool Completed => EarnedKm >= Challenge.TargetKm;
}
