using ActivitiesJournal.Models;

namespace ActivitiesJournal.Services;

public class ActivityStatsService : IActivityStatsService
{
    private readonly IStravaService _stravaService;
    private readonly ILogger<ActivityStatsService> _logger;

    public ActivityStatsService(IStravaService stravaService, ILogger<ActivityStatsService> logger)
    {
        _stravaService = stravaService;
        _logger = logger;
    }

    public async Task<BadgesViewModel> GetBadgesAsync(string type)
    {
        var all = await _stravaService.GetAllActivitiesAsync();

        if (type == "Walk")
        {
            var walks = all.Where(a => SportTypes.IsWalk(a.SportType))
                           .OrderBy(a => a.StartDateLocal).ToList();

            double wDistKm = walks.Sum(a => a.Distance) / 1000.0;
            double wElevM  = walks.Sum(a => (double)a.TotalElevationGain);
            int totalWalks = walks.Count;
            var walkDates = walks.Select(a => a.StartDateLocal.Date).Distinct().OrderBy(d => d).ToList();
            int wLongestStreak = ComputeLongestStreak(walkDates);

            var wHalfMarathons = walks.Where(a => a.Distance >= 21_000).ToList();
            var wMarathons     = walks.Where(a => a.Distance >= 42_000).ToList();
            var wBigClimbs     = walks.Where(a => a.TotalElevationGain >= 2_000).ToList();
            var wEarlyBird     = walks.Where(a => a.StartDateLocal.Hour < 7).ToList();
            var wEveningWalks  = walks.Where(a => a.StartDateLocal.Hour >= 20).ToList();

            var walkBadges = new List<Badge>
            {
                MilestoneBadge("First Walk",   "Complete your first walk or hike",    "bi-person-walking",      1,   totalWalks, walks.FirstOrDefault()),
                MilestoneBadge("10 Walks",     "Complete 10 walks",                   "bi-person-walking",      10,  totalWalks, walks.Count >= 10  ? walks[9]  : null),
                MilestoneBadge("50 Walks",     "Complete 50 walks",                   "bi-person-walking-fill", 50,  totalWalks, walks.Count >= 50  ? walks[49] : null),
                MilestoneBadge("100 Walks",    "Complete 100 walks",                  "bi-person-walking-fill", 100, totalWalks, walks.Count >= 100 ? walks[99] : null),
                MilestoneBadge("500 Walks",    "Complete 500 walks",                  "bi-person-walking-fill", 500, totalWalks, walks.Count >= 500 ? walks[499]: null),

                DistanceBadge("Walker 100 km",   "Walk 100 km total",   "bi-signpost",        100,   wDistKm, walks),
                DistanceBadge("Walker 1,000 km", "Walk 1,000 km total", "bi-signpost-2",      1_000, wDistKm, walks),
                DistanceBadge("Walker 5,000 km", "Walk 5,000 km total", "bi-signpost-2-fill", 5_000, wDistKm, walks),

                ElevationBadge("Hillwalker (500 m)", "Gain 500+ m in one walk",     "bi-triangle",      500,   wElevM, walks),
                ElevationBadge("Everest Walker",     "Climb 8,849 m total walking", "bi-triangle-fill", 8_849, wElevM, walks),

                new Badge { Name = "Half-Marathon Walker", Description = "Walk 21+ km in a single outing", Icon = "bi-person-running",
                    Earned = wHalfMarathons.Any(), EarningActivity = wHalfMarathons.FirstOrDefault(), EarnedOn = wHalfMarathons.FirstOrDefault()?.StartDateLocal,
                    Progress = wHalfMarathons.Any() ? null : $"Longest: {(walks.Any() ? (walks.Max(a => a.Distance)/1000.0).ToString("0.0") : "0")} km" },

                new Badge { Name = "Marathon Walker", Description = "Walk 42+ km in a single outing", Icon = "bi-trophy",
                    Earned = wMarathons.Any(), EarningActivity = wMarathons.FirstOrDefault(), EarnedOn = wMarathons.FirstOrDefault()?.StartDateLocal,
                    Progress = wMarathons.Any() ? null : $"Longest: {(walks.Any() ? (walks.Max(a => a.Distance)/1000.0).ToString("0.0") : "0")} km" },

                new Badge { Name = "Mountain Goat (Walking)", Description = "Gain 2,000+ m elevation in one walk", Icon = "bi-sunrise",
                    Earned = wBigClimbs.Any(), EarningActivity = wBigClimbs.FirstOrDefault(), EarnedOn = wBigClimbs.FirstOrDefault()?.StartDateLocal,
                    Progress = wBigClimbs.Any() ? null : $"Best: {(walks.Any() ? walks.Max(a => a.TotalElevationGain).ToString("0") : "0")} m" },

                new Badge { Name = "Habit Walker", Description = "Walk 7 days in a row", Icon = "bi-fire",
                    Earned = wLongestStreak >= 7, Progress = wLongestStreak >= 7 ? null : $"Best streak: {wLongestStreak} day(s)" },

                new Badge { Name = "Habit Walker Pro", Description = "Walk 30 days in a row", Icon = "bi-stars",
                    Earned = wLongestStreak >= 30, Progress = wLongestStreak >= 30 ? null : $"Best streak: {wLongestStreak} day(s)" },

                new Badge { Name = "Early Bird Walker", Description = "Start a walk before 7 AM", Icon = "bi-sunrise-fill",
                    Earned = wEarlyBird.Any(), EarningActivity = wEarlyBird.FirstOrDefault(), EarnedOn = wEarlyBird.FirstOrDefault()?.StartDateLocal },

                new Badge { Name = "Evening Walker", Description = "Start a walk at or after 8 PM", Icon = "bi-moon-fill",
                    Earned = wEveningWalks.Any(), EarningActivity = wEveningWalks.FirstOrDefault(), EarnedOn = wEveningWalks.FirstOrDefault()?.StartDateLocal },

                new Badge { Name = "Year-Round Walker", Description = "Walk in all 12 calendar months in a year", Icon = "bi-calendar-check",
                    Earned = walks.GroupBy(a => a.StartDateLocal.Year).Any(g => g.Select(a => a.StartDateLocal.Month).Distinct().Count() == 12),
                    Progress = $"Best: {(walks.Any() ? walks.GroupBy(a => a.StartDateLocal.Year).Max(g => g.Select(a => a.StartDateLocal.Month).Distinct().Count()) : 0)} months" },
            };

            return new BadgesViewModel { Badges = walkBadges };
        }

        var rides = all.Where(a => SportTypes.IsRide(a.SportType))
                       .OrderBy(a => a.StartDateLocal).ToList();

        double totalDistKm = rides.Sum(a => a.Distance) / 1000.0;
        double totalElevM = rides.Sum(a => (double)a.TotalElevationGain);
        int totalRides = rides.Count;

        var rideDates = rides.Select(a => a.StartDateLocal.Date).Distinct().OrderBy(d => d).ToList();
        int longestStreak = ComputeLongestStreak(rideDates);

        var centuries = rides.Where(a => a.Distance >= 100_000).ToList();
        var bigClimbs = rides.Where(a => a.TotalElevationGain >= 2000).ToList();
        var earlyBird = rides.Where(a => a.StartDateLocal.Hour < 7).ToList();
        var nightOwl  = rides.Where(a => a.StartDateLocal.Hour >= 20).ToList();
        var fastRides = rides.Where(a => a.Distance >= 40_000 && a.AverageSpeed * 3.6 >= 35).ToList();

        var badges = new List<Badge>
        {
            MilestoneBadge("First Ride", "Complete your first bike ride", "bi-bicycle", 1, totalRides, rides.FirstOrDefault()),
            MilestoneBadge("10 Rides", "Complete 10 rides", "bi-bicycle", 10, totalRides, rides.Count >= 10 ? rides[9] : null),
            MilestoneBadge("50 Rides", "Complete 50 rides", "bi-bicycle-fill", 50, totalRides, rides.Count >= 50 ? rides[49] : null),
            MilestoneBadge("100 Rides", "Complete 100 rides", "bi-bicycle-fill", 100, totalRides, rides.Count >= 100 ? rides[99] : null),
            MilestoneBadge("500 Rides", "Complete 500 rides", "bi-bicycle-fill", 500, totalRides, rides.Count >= 500 ? rides[499] : null),

            DistanceBadge("100 km Club", "Ride 100 km total", "bi-signpost", 100, totalDistKm, rides),
            DistanceBadge("1,000 km Club", "Ride 1,000 km total", "bi-signpost-2", 1_000, totalDistKm, rides),
            DistanceBadge("5,000 km Club", "Ride 5,000 km total", "bi-signpost-2-fill", 5_000, totalDistKm, rides),
            DistanceBadge("10,000 km Club", "Ride 10,000 km total", "bi-globe", 10_000, totalDistKm, rides),
            DistanceBadge("Moon Shot (384,400 km)", "Ride the distance to the Moon", "bi-moon-stars", 384_400, totalDistKm, rides),

            ElevationBadge("Everest (8,849 m)", "Climb as high as Mt Everest in total", "bi-triangle", 8_849, totalElevM, rides),
            ElevationBadge("Triple Everest", "Climb 3× Everest in total", "bi-triangle-fill", 26_547, totalElevM, rides),
            ElevationBadge("10× Everest", "Climb 10× Everest in total", "bi-triangle-fill", 88_490, totalElevM, rides),

            new Badge { Name = "Century Ride", Description = "Complete a 100 km+ ride in a single session", Icon = "bi-c-circle",
                Earned = centuries.Any(), EarningActivity = centuries.FirstOrDefault(),
                EarnedOn = centuries.FirstOrDefault()?.StartDateLocal,
                Progress = centuries.Any() ? null : $"Longest: {(rides.Any() ? (rides.Max(a => a.Distance) / 1000.0).ToString("0.0") : "0")} km" },

            new Badge { Name = "Mountain Goat", Description = "Gain 2,000+ m elevation in a single ride", Icon = "bi-sunrise",
                Earned = bigClimbs.Any(), EarningActivity = bigClimbs.FirstOrDefault(),
                EarnedOn = bigClimbs.FirstOrDefault()?.StartDateLocal,
                Progress = bigClimbs.Any() ? null : $"Best: {(rides.Any() ? rides.Max(a => a.TotalElevationGain).ToString("0") : "0")} m" },

            new Badge { Name = "Speed Demon", Description = "Average 35+ km/h on a 40+ km ride", Icon = "bi-lightning-charge-fill",
                Earned = fastRides.Any(), EarningActivity = fastRides.FirstOrDefault(),
                EarnedOn = fastRides.FirstOrDefault()?.StartDateLocal,
                Progress = fastRides.Any() ? null : "Avg 35 km/h on a 40+ km ride" },

            new Badge { Name = "Week Warrior", Description = "Ride 7 days in a row", Icon = "bi-fire",
                Earned = longestStreak >= 7,
                Progress = longestStreak >= 7 ? null : $"Best streak: {longestStreak} day(s)" },

            new Badge { Name = "Early Bird", Description = "Start a ride before 7 AM", Icon = "bi-sunrise-fill",
                Earned = earlyBird.Any(), EarningActivity = earlyBird.FirstOrDefault(),
                EarnedOn = earlyBird.FirstOrDefault()?.StartDateLocal },

            new Badge { Name = "Night Owl", Description = "Start a ride at or after 8 PM", Icon = "bi-moon-fill",
                Earned = nightOwl.Any(), EarningActivity = nightOwl.FirstOrDefault(),
                EarnedOn = nightOwl.FirstOrDefault()?.StartDateLocal },

            new Badge { Name = "Year-Round Rider", Description = "Ride in all 12 calendar months in a single year", Icon = "bi-calendar-check",
                Earned = rides.GroupBy(a => a.StartDateLocal.Year).Any(g => g.Select(a => a.StartDateLocal.Month).Distinct().Count() == 12),
                Progress = $"Best: {(rides.Any() ? rides.GroupBy(a => a.StartDateLocal.Year).Max(g => g.Select(a => a.StartDateLocal.Month).Distinct().Count()) : 0)} months" },
        };

        return new BadgesViewModel { Badges = badges };
    }

    public async Task<PersonalRecordsViewModel> GetPersonalRecordsAsync(string type)
    {
        var all = await _stravaService.GetAllActivitiesAsync();
        var rides = SportTypes.FilterByType(all, type);

        if (!rides.Any())
            return new PersonalRecordsViewModel();

        var longestByDist = rides.MaxBy(a => a.Distance)!;
        var mostClimbing  = rides.MaxBy(a => a.TotalElevationGain)!;
        var longestTime   = rides.MaxBy(a => a.MovingTime)!;

        var records = new List<PersonalRecord>
        {
            new() { Label = "Longest",        Value = $"{longestByDist.Distance / 1000.0:0.00} km",                       Icon = "bi-rulers",    Activity = longestByDist },
            new() { Label = "Longest Time",   Value = TimeSpan.FromSeconds(longestTime.MovingTime).ToString(@"h\:mm\:ss"), Icon = "bi-clock",     Activity = longestTime },
            new() { Label = "Most Elevation", Value = $"{mostClimbing.TotalElevationGain:0} m",                           Icon = "bi-triangle",  Activity = mostClimbing },
        };

        if (type == "Walk")
        {
            var bestPace = rides.Where(a => a.Distance >= 3000 && a.MovingTime > 0)
                                .MinBy(a => a.MovingTime / (a.Distance / 1000.0));
            if (bestPace != null)
            {
                double secPerKm = bestPace.MovingTime / (bestPace.Distance / 1000.0);
                int min = (int)(secPerKm / 60), sec = (int)(secPerKm % 60);
                records.Add(new() { Label = "Best Pace", Value = $"{min}:{sec:D2} /km", Icon = "bi-lightning-charge", Activity = bestPace });
            }
        }
        else
        {
            var maxSpeed = rides.MaxBy(a => a.MaxSpeed)!;
            records.Add(new() { Label = "Top Speed (max)", Value = $"{maxSpeed.MaxSpeed * 3.6:0.0} km/h", Icon = "bi-lightning-charge", Activity = maxSpeed });
            var fastestAvg = rides.Where(a => a.Distance >= 20_000).MaxBy(a => a.AverageSpeed);
            if (fastestAvg != null)
                records.Add(new() { Label = "Fastest Avg Speed (≥20 km)", Value = $"{fastestAvg.AverageSpeed * 3.6:0.0} km/h", Icon = "bi-speedometer2", Activity = fastestAvg });
        }

        return new PersonalRecordsViewModel
        {
            AllTimeRecords      = records,
            Top10Longest        = rides.OrderByDescending(a => a.Distance).Take(10).ToList(),
            Top10Fastest        = type == "Walk"
                ? rides.Where(a => a.Distance >= 3000 && a.MovingTime > 0)
                       .OrderBy(a => a.MovingTime / (a.Distance / 1000.0)).Take(10).ToList()
                : rides.Where(a => a.Distance >= 20_000).OrderByDescending(a => a.AverageSpeed).Take(10).ToList(),
            Top10MostClimbing   = rides.OrderByDescending(a => a.TotalElevationGain).Take(10).ToList(),
            TotalRides          = rides.Count,
            TotalDistanceKm     = rides.Sum(a => a.Distance) / 1000.0,
        };
    }

    public async Task<(SegmentsViewModel Segments, BestEffortsViewModel BestEfforts)> GetSegmentsAsync(int count)
    {
        var all   = await _stravaService.GetAllActivitiesAsync();
        var rides = all.Where(a => SportTypes.IsRide(a.SportType))
                       .OrderByDescending(a => a.StartDateLocal).ToList();

        int fetchCount = Math.Min(count, rides.Count);
        var toFetch = rides.Take(fetchCount).ToList();

        var semaphore = new SemaphoreSlim(5, 5);
        var details = await Task.WhenAll(toFetch.Select(async r =>
        {
            await semaphore.WaitAsync();
            try { return await _stravaService.GetActivityByIdAsync(r.Id); }
            finally { semaphore.Release(); }
        }));

        var segmentMap = new Dictionary<long, SegmentBestTime>();
        foreach (var detail in details.Where(d => d?.SegmentEfforts != null))
        {
            foreach (var effort in detail!.SegmentEfforts!)
            {
                if (effort.Segment == null) continue;
                if (!segmentMap.TryGetValue(effort.Segment.Id, out var seg))
                {
                    seg = new SegmentBestTime
                    {
                        SegmentId   = effort.Segment.Id,
                        SegmentName = effort.Segment.Name,
                        DistanceM   = effort.Segment.Distance,
                        AverageGrade = effort.Segment.AverageGrade,
                        StartLat = effort.Segment.StartLatlng?.Count >= 2 ? effort.Segment.StartLatlng[0] : null,
                        StartLng = effort.Segment.StartLatlng?.Count >= 2 ? effort.Segment.StartLatlng[1] : null,
                        EndLat   = effort.Segment.EndLatlng?.Count >= 2   ? effort.Segment.EndLatlng[0]   : null,
                        EndLng   = effort.Segment.EndLatlng?.Count >= 2   ? effort.Segment.EndLatlng[1]   : null,
                    };
                    segmentMap[effort.Segment.Id] = seg;
                }
                seg.AllAttempts.Add(new SegmentAttempt
                {
                    Date           = detail.StartDateLocal,
                    ElapsedSeconds = effort.ElapsedTime,
                    ActivityId     = detail.Id,
                    ActivityName   = detail.Name,
                    PrRank         = effort.PrRank,
                });
            }
        }

        foreach (var seg in segmentMap.Values)
        {
            var best = seg.AllAttempts.MinBy(a => a.ElapsedSeconds)!;
            seg.BestElapsedSeconds = best.ElapsedSeconds;
            seg.BestPrRank         = best.PrRank;
            seg.BestDate           = best.Date;
            seg.BestActivityId     = best.ActivityId;
            seg.BestActivityName   = best.ActivityName;
            seg.AttemptCount       = seg.AllAttempts.Count;
            seg.AllAttempts        = seg.AllAttempts.OrderBy(a => a.Date).ToList();
        }

        var effortMap = new Dictionary<string, BestEffortRow>();
        foreach (var detail in details.Where(d => d?.BestEfforts != null))
        {
            foreach (var be in detail!.BestEfforts!)
            {
                if (!effortMap.TryGetValue(be.Name, out var row))
                {
                    row = new BestEffortRow { DistanceName = be.Name, DistanceM = be.Distance };
                    effortMap[be.Name] = row;
                }
                row.History.Add((detail.StartDateLocal, be.ElapsedTime, detail.Id, detail.Name));
            }
        }
        foreach (var row in effortMap.Values)
        {
            var best = row.History.MinBy(h => h.Seconds);
            row.BestElapsedSeconds = best.Seconds;
            row.BestDate           = best.Date;
            row.BestActivityId     = best.ActivityId;
            row.BestActivityName   = best.ActivityName;
            row.History            = row.History.OrderBy(h => h.Date).ToList();
        }

        var polySem = new SemaphoreSlim(5);
        var polyTasks = segmentMap.Values
            .Where(s => s.StartLat.HasValue)
            .Select(async seg =>
            {
                await polySem.WaitAsync();
                try { seg.Polyline = await _stravaService.GetSegmentPolylineAsync(seg.SegmentId); }
                finally { polySem.Release(); }
            });
        await Task.WhenAll(polyTasks);

        var segVm = new SegmentsViewModel
        {
            Segments             = segmentMap.Values.OrderByDescending(s => s.AttemptCount).ThenBy(s => s.SegmentName).ToList(),
            RidesFetched         = fetchCount,
            TotalRidesAvailable  = rides.Count,
        };
        var beVm = new BestEffortsViewModel
        {
            Rows         = effortMap.Values.OrderBy(r => r.DistanceM).ToList(),
            RidesFetched = fetchCount,
        };

        return (segVm, beVm);
    }

    public async Task<FitnessViewModel> GetFitnessAsync(int days)
    {
        var all   = await _stravaService.GetAllActivitiesAsync();
        var rides = all.Where(a => SportTypes.IsRide(a.SportType)).ToList();

        var dailyLoad = rides
            .GroupBy(a => a.StartDateLocal.Date)
            .ToDictionary(g => g.Key, g => g.Sum(a =>
            {
                double hrs = a.MovingTime / 3600.0;
                double spd = a.AverageSpeed * 3.6;
                return hrs * spd / 2.0;
            }));

        var today     = DateTime.Today;
        var startDate = today.AddDays(-days);
        var seedStart = rides.Any() ? rides.Min(a => a.StartDateLocal.Date) : startDate;

        double ctl = 0, atl = 0;
        const double ctlDecay = 1.0 / 42.0;
        const double atlDecay = 1.0 / 7.0;

        for (var d = seedStart; d < startDate; d = d.AddDays(1))
        {
            double load = dailyLoad.GetValueOrDefault(d, 0);
            ctl = ctl + (load - ctl) * ctlDecay;
            atl = atl + (load - atl) * atlDecay;
        }

        var points = new List<FitnessDayPoint>();
        for (var d = startDate; d <= today; d = d.AddDays(1))
        {
            double load = dailyLoad.GetValueOrDefault(d, 0);
            ctl = ctl + (load - ctl) * ctlDecay;
            atl = atl + (load - atl) * atlDecay;
            points.Add(new FitnessDayPoint
            {
                Date = d,
                Load = Math.Round(load, 1),
                Ctl  = Math.Round(ctl, 1),
                Atl  = Math.Round(atl, 1),
                Tsb  = Math.Round(ctl - atl, 1),
            });
        }

        var last   = points.LastOrDefault();
        double tsb = last?.Tsb ?? 0;
        string status = tsb > 5    ? "Fresh — good day to race or go hard"
                      : tsb > -10  ? "Neutral — normal training"
                      : tsb > -30  ? "Tired — accumulated fatigue"
                      :              "Very fatigued — consider rest";

        return new FitnessViewModel
        {
            Points     = points,
            DaysShown  = days,
            CurrentCtl = last?.Ctl ?? 0,
            CurrentAtl = last?.Atl ?? 0,
            CurrentTsb = tsb,
            TsbStatus  = status,
        };
    }

    public async Task<AnalysisViewModel> GetAnalysisAsync(int? year)
    {
        var all   = await _stravaService.GetAllActivitiesAsync();
        var rides = all.Where(a => SportTypes.IsRide(a.SportType)).ToList();

        var availableYears = rides.Select(a => a.StartDateLocal.Year).Distinct().OrderDescending().ToList();
        int selectedYear   = year ?? DateTime.Now.Year;
        var yearRides      = rides.Where(a => a.StartDateLocal.Year == selectedYear).ToList();

        static RideType Classify(StravaActivity a)
        {
            double distKm  = a.Distance / 1000.0;
            double speedKmh = a.AverageSpeed * 3.6;
            if (distKm >= 130) return RideType.Epic;
            if (distKm >= 50 && speedKmh >= 34) return RideType.Race;
            if (speedKmh >= 30 || (distKm >= 40 && speedKmh >= 28)) return RideType.Tempo;
            if (distKm >= 40) return RideType.Endurance;
            return RideType.Recovery;
        }

        var classified = yearRides.Select(a => new ClassifiedRide { Activity = a, RideType = Classify(a) })
                                  .OrderByDescending(r => r.Activity.StartDateLocal).ToList();

        var typeCounts = classified.GroupBy(r => r.RideType)
                                   .ToDictionary(g => g.Key, g => g.Count());

        var zones = new List<SpeedZoneData>
        {
            new() { Label = "< 20 km/h",  MinKmh = 0,  MaxKmh = 20,  Color = "#6c757d" },
            new() { Label = "20–25 km/h", MinKmh = 20, MaxKmh = 25,  Color = "#17a2b8" },
            new() { Label = "25–30 km/h", MinKmh = 25, MaxKmh = 30,  Color = "#28a745" },
            new() { Label = "30–35 km/h", MinKmh = 30, MaxKmh = 35,  Color = "#ffc107" },
            new() { Label = "35–40 km/h", MinKmh = 35, MaxKmh = 40,  Color = "#fd7e14" },
            new() { Label = "> 40 km/h",  MinKmh = 40, MaxKmh = 999, Color = "#dc3545" },
        };

        foreach (var r in yearRides)
        {
            double spd  = r.AverageSpeed * 3.6;
            var zone = zones.FirstOrDefault(z => spd >= z.MinKmh && spd < z.MaxKmh);
            if (zone != null)
            {
                zone.RideCount++;
                zone.TotalDistanceKm += r.Distance / 1000.0;
                zone.TotalTimeHrs    += r.MovingTime / 3600.0;
            }
        }

        return new AnalysisViewModel
        {
            Year           = selectedYear,
            AvailableYears = availableYears,
            ClassifiedRides = classified,
            TypeCounts     = typeCounts,
            SpeedZones     = zones,
        };
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    public static int ComputeLongestStreak(List<DateTime> sortedDates)
    {
        if (!sortedDates.Any()) return 0;
        int longest = 1, current = 1;
        for (int i = 1; i < sortedDates.Count; i++)
        {
            current = (sortedDates[i] - sortedDates[i - 1]).Days == 1 ? current + 1 : 1;
            if (current > longest) longest = current;
        }
        return longest;
    }

    private static Badge MilestoneBadge(string name, string desc, string icon, int target, int actual, StravaActivity? earner)
        => new() { Name = name, Description = desc, Icon = icon, Earned = actual >= target,
            EarningActivity = earner, EarnedOn = earner?.StartDateLocal,
            Progress = actual < target ? $"{actual}/{target} rides" : null };

    private static Badge DistanceBadge(string name, string desc, string icon, double targetKm, double actualKm, List<StravaActivity> rides)
    {
        var earned = actualKm >= targetKm;
        StravaActivity? earner = null;
        if (earned)
        {
            double cum = 0;
            foreach (var r in rides) { cum += r.Distance / 1000.0; if (cum >= targetKm) { earner = r; break; } }
        }
        return new() { Name = name, Description = desc, Icon = icon, Earned = earned,
            EarningActivity = earner, EarnedOn = earner?.StartDateLocal,
            Progress = earned ? null : $"{actualKm:0.0}/{targetKm:0} km" };
    }

    private static Badge ElevationBadge(string name, string desc, string icon, double targetM, double actualM, List<StravaActivity> rides)
    {
        var earned = actualM >= targetM;
        StravaActivity? earner = null;
        if (earned)
        {
            double cum = 0;
            foreach (var r in rides) { cum += r.TotalElevationGain; if (cum >= targetM) { earner = r; break; } }
        }
        return new() { Name = name, Description = desc, Icon = icon, Earned = earned,
            EarningActivity = earner, EarnedOn = earner?.StartDateLocal,
            Progress = earned ? null : $"{actualM:0}/{targetM:0} m" };
    }
}
