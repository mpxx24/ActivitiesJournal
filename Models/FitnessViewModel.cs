namespace ActivitiesJournal.Models;

public class FitnessDayPoint
{
    public DateTime Date { get; set; }
    public double Ctl { get; set; }   // Chronic Training Load (42-day EMA)
    public double Atl { get; set; }   // Acute Training Load (7-day EMA)
    public double Tsb { get; set; }   // Training Stress Balance = CTL - ATL
    public double Load { get; set; }  // Raw training load that day
}

public class FitnessViewModel
{
    public List<FitnessDayPoint> Points { get; set; } = new();
    public int DaysShown { get; set; }
    public double CurrentCtl { get; set; }
    public double CurrentAtl { get; set; }
    public double CurrentTsb { get; set; }
    public string TsbStatus { get; set; } = string.Empty;
}
