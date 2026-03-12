namespace ActivitiesJournal.Models;

public class Badge
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public bool Earned { get; set; }
    public string? Progress { get; set; }   // shown when not yet earned
    public DateTime? EarnedOn { get; set; }
    public StravaActivity? EarningActivity { get; set; }
}

public class BadgesViewModel
{
    public List<Badge> Badges { get; set; } = new();
    public int EarnedCount => Badges.Count(b => b.Earned);
}
