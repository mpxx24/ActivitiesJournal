namespace ActivitiesJournal.Models;

public class DayInHistoryViewModel
{
    public DateTime Today { get; set; }
    // Activities on this same calendar day in prior years
    public List<(int Year, List<StravaActivity> Activities)> ByYear { get; set; } = new();

    // Streak info
    public int CurrentStreakDays { get; set; }
    public int LongestStreakDays { get; set; }
    public DateTime LongestStreakStart { get; set; }
    public DateTime LongestStreakEnd { get; set; }
}
