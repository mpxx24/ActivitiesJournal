namespace ActivitiesJournal.Models;

public class GoalsData
{
    public List<AnnualGoals> AnnualGoals { get; set; } = new();
    public List<VirtualChallenge> Challenges { get; set; } = new();
}
