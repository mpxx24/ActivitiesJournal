using ActivitiesJournal.Models;
using ActivitiesJournal.Services;

namespace ActivitiesJournal.Tests;

[TestFixture]
public class GoalsServiceTests
{
    [Test]
    public void NormalizePresetStartDates_PresetChallenge_FixesStartDateToJanFirst()
    {
        var data = new GoalsData
        {
            Challenges = new List<VirtualChallenge>
            {
                new() { Name = "Tour de France", TargetKm = 3406, StartDate = new DateTime(2026, 2, 15), IsPreset = true }
            }
        };

        GoalsService.NormalizePresetStartDates(data);

        Assert.That(data.Challenges[0].StartDate, Is.EqualTo(new DateTime(2026, 1, 1)));
    }

    [Test]
    public void NormalizePresetStartDates_CustomChallenge_LeavesStartDateUntouched()
    {
        var customStart = new DateTime(2026, 3, 10);
        var data = new GoalsData
        {
            Challenges = new List<VirtualChallenge>
            {
                new() { Name = "My Custom Challenge", TargetKm = 500, StartDate = customStart, IsPreset = false }
            }
        };

        GoalsService.NormalizePresetStartDates(data);

        Assert.That(data.Challenges[0].StartDate, Is.EqualTo(customStart));
    }

    [Test]
    public void NormalizePresetStartDates_PresetChallenge_AlreadyJanFirst_NoChange()
    {
        var janFirst = new DateTime(2026, 1, 1);
        var data = new GoalsData
        {
            Challenges = new List<VirtualChallenge>
            {
                new() { Name = "Giro d'Italia", TargetKm = 3497, StartDate = janFirst, IsPreset = true }
            }
        };

        GoalsService.NormalizePresetStartDates(data);

        Assert.That(data.Challenges[0].StartDate, Is.EqualTo(janFirst));
    }

    [Test]
    public void NormalizePresetStartDates_MixedChallenges_OnlyFixesPresets()
    {
        var customStart = new DateTime(2026, 5, 20);
        var data = new GoalsData
        {
            Challenges = new List<VirtualChallenge>
            {
                new() { Name = "Preset", TargetKm = 1000, StartDate = new DateTime(2026, 3, 1), IsPreset = true },
                new() { Name = "Custom", TargetKm = 500,  StartDate = customStart,              IsPreset = false }
            }
        };

        GoalsService.NormalizePresetStartDates(data);

        Assert.That(data.Challenges[0].StartDate, Is.EqualTo(new DateTime(2026, 1, 1)));
        Assert.That(data.Challenges[1].StartDate, Is.EqualTo(customStart));
    }
}
