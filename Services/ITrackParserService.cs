using ActivitiesJournal.Models;

namespace ActivitiesJournal.Services;

public interface ITrackParserService
{
    TrackParseResult Parse(Stream gpxStream);
}
