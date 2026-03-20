using System.Text;
using ActivitiesJournal.Services;

namespace ActivitiesJournal.Tests;

[TestFixture]
public class TrackParserServiceTests
{
    private TrackParserService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _sut = new TrackParserService();
    }

    private static Stream ToStream(string xml) =>
        new MemoryStream(Encoding.UTF8.GetBytes(xml));

    private const string ThreePointGpx = """
        <?xml version="1.0"?>
        <gpx xmlns="http://www.topografix.com/GPX/1/1">
          <trk><trkseg>
            <trkpt lat="52.2297" lon="21.0122"><time>2024-06-01T08:00:00Z</time></trkpt>
            <trkpt lat="52.2300" lon="21.0150"><time>2024-06-01T08:05:00Z</time></trkpt>
            <trkpt lat="52.2310" lon="21.0200"><time>2024-06-01T08:10:00Z</time></trkpt>
          </trkseg></trk>
        </gpx>
        """;

    [Test]
    public void Parse_ValidGpx_ReturnsCorrectPointCount()
    {
        var result = _sut.Parse(ToStream(ThreePointGpx));
        Assert.That(result.PointCount, Is.EqualTo(3));
    }

    [Test]
    public void Parse_ValidGpx_ReturnsCorrectStartTime()
    {
        var result = _sut.Parse(ToStream(ThreePointGpx));
        Assert.That(result.StartedAt, Is.EqualTo(new DateTime(2024, 6, 1, 8, 0, 0, DateTimeKind.Utc)));
    }

    [Test]
    public void Parse_ValidGpx_ReturnsCorrectDuration()
    {
        var result = _sut.Parse(ToStream(ThreePointGpx));
        Assert.That(result.Duration, Is.EqualTo(TimeSpan.FromMinutes(10)));
    }

    [Test]
    public void Parse_ValidGpx_ReturnsPositiveDistance()
    {
        var result = _sut.Parse(ToStream(ThreePointGpx));
        Assert.That(result.DistanceKm, Is.GreaterThan(0));
    }

    [Test]
    public void Parse_TwoKnownPoints_ReturnsApproximateDistance()
    {
        // Warsaw (52.2297°N, 21.0122°E) → Kraków (50.0647°N, 19.9450°E) ≈ 252 km
        const string gpx = """
            <?xml version="1.0"?>
            <gpx xmlns="http://www.topografix.com/GPX/1/1">
              <trk><trkseg>
                <trkpt lat="52.2297" lon="21.0122"><time>2024-06-01T08:00:00Z</time></trkpt>
                <trkpt lat="50.0647" lon="19.9450"><time>2024-06-01T10:00:00Z</time></trkpt>
              </trkseg></trk>
            </gpx>
            """;

        var result = _sut.Parse(ToStream(gpx));

        Assert.That(result.DistanceKm, Is.EqualTo(252.0).Within(5.0));
    }

    [Test]
    public void Parse_SinglePoint_ReturnsZeroDistanceAndZeroDuration()
    {
        const string gpx = """
            <?xml version="1.0"?>
            <gpx xmlns="http://www.topografix.com/GPX/1/1">
              <trk><trkseg>
                <trkpt lat="52.2297" lon="21.0122"><time>2024-06-01T08:00:00Z</time></trkpt>
              </trkseg></trk>
            </gpx>
            """;

        var result = _sut.Parse(ToStream(gpx));

        Assert.That(result.DistanceKm, Is.EqualTo(0));
        Assert.That(result.Duration, Is.EqualTo(TimeSpan.Zero));
        Assert.That(result.PointCount, Is.EqualTo(1));
    }

    [Test]
    public void Parse_PointsMissingTimestamps_ReturnsDurationZero()
    {
        const string gpx = """
            <?xml version="1.0"?>
            <gpx xmlns="http://www.topografix.com/GPX/1/1">
              <trk><trkseg>
                <trkpt lat="52.2297" lon="21.0122"></trkpt>
                <trkpt lat="52.2300" lon="21.0150"></trkpt>
              </trkseg></trk>
            </gpx>
            """;

        var result = _sut.Parse(ToStream(gpx));

        Assert.That(result.Duration, Is.EqualTo(TimeSpan.Zero));
        Assert.That(result.PointCount, Is.EqualTo(2));
    }

    [Test]
    public void Parse_EmptyTrack_ReturnsEmptyResult()
    {
        const string gpx = """
            <?xml version="1.0"?>
            <gpx xmlns="http://www.topografix.com/GPX/1/1">
              <trk><trkseg></trkseg></trk>
            </gpx>
            """;

        var result = _sut.Parse(ToStream(gpx));

        Assert.That(result.PointCount, Is.EqualTo(0));
        Assert.That(result.DistanceKm, Is.EqualTo(0));
        Assert.That(result.Duration, Is.EqualTo(TimeSpan.Zero));
    }

    [Test]
    public void Parse_NullStream_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _sut.Parse(null!));
    }
}
