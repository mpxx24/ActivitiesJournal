using System;
using System.Collections.Generic;
using System.Linq;

namespace ActivitiesJournal.Models;

public class ActivityTypeSummary
{
    public string SportType { get; set; } = string.Empty;
    public int Count { get; set; }
    public double TotalDistanceKm { get; set; }
    public TimeSpan TotalMovingTime { get; set; }
    public double TotalElevationGain { get; set; }
    public StravaActivity? LongestByDistance { get; set; }
}

public class YearSummaryViewModel
{
    public int Year { get; set; }
    public List<StravaActivity> Activities { get; set; } = new();
    public List<ActivityTypeSummary> ByType { get; set; } = new();

    public double TotalDistanceKm => Activities.Sum(a => a.Distance) / 1000.0;
    public TimeSpan TotalMovingTime => TimeSpan.FromSeconds(Activities.Sum(a => a.MovingTime));
    public double TotalElevationGain => Activities.Sum(a => a.TotalElevationGain);
}

