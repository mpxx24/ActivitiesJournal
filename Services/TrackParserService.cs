using System.Xml.Linq;
using ActivitiesJournal.Models;

namespace ActivitiesJournal.Services;

public class TrackParserService : ITrackParserService
{
    public TrackParseResult Parse(Stream gpxStream)
    {
        ArgumentNullException.ThrowIfNull(gpxStream);

        var doc = XDocument.Load(gpxStream);
        var ns = doc.Root?.Name.Namespace ?? XNamespace.None;

        var points = doc.Descendants(ns + "trkpt")
            .Select(pt => new GpxPoint
            {
                Latitude = (double)pt.Attribute("lat")!,
                Longitude = (double)pt.Attribute("lon")!,
                Elevation = pt.Element(ns + "ele") is { } ele
                    ? double.Parse(ele.Value, System.Globalization.CultureInfo.InvariantCulture)
                    : null,
                Time = pt.Element(ns + "time") is { } t
                    ? DateTime.Parse(t.Value, null, System.Globalization.DateTimeStyles.RoundtripKind)
                    : null
            })
            .ToList();

        var result = new TrackParseResult
        {
            Points = points,
            PointCount = points.Count
        };

        if (points.Count == 0)
            return result;

        var firstTime = points.FirstOrDefault(p => p.Time.HasValue)?.Time;
        var lastTime = points.LastOrDefault(p => p.Time.HasValue)?.Time;

        if (firstTime.HasValue)
            result.StartedAt = firstTime.Value;

        if (firstTime.HasValue && lastTime.HasValue && lastTime > firstTime)
            result.Duration = lastTime.Value - firstTime.Value;

        result.DistanceKm = CalculateTotalDistanceKm(points);

        return result;
    }

    private static double CalculateTotalDistanceKm(List<GpxPoint> points)
    {
        double total = 0;
        for (int i = 1; i < points.Count; i++)
            total += HaversineKm(points[i - 1], points[i]);
        return Math.Round(total, 3);
    }

    private static double HaversineKm(GpxPoint a, GpxPoint b)
    {
        const double R = 6371.0;
        var dLat = ToRad(b.Latitude - a.Latitude);
        var dLon = ToRad(b.Longitude - a.Longitude);
        var sinLat = Math.Sin(dLat / 2);
        var sinLon = Math.Sin(dLon / 2);
        var h = sinLat * sinLat + Math.Cos(ToRad(a.Latitude)) * Math.Cos(ToRad(b.Latitude)) * sinLon * sinLon;
        return 2 * R * Math.Asin(Math.Sqrt(h));
    }

    private static double ToRad(double degrees) => degrees * Math.PI / 180.0;
}
