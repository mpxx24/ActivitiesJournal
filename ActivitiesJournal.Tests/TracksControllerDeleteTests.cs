using System.Security.Claims;
using ActivitiesJournal.Configuration;
using ActivitiesJournal.Controllers;
using ActivitiesJournal.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ActivitiesJournal.Tests;

[TestFixture]
public class TracksControllerDeleteTests
{
    private const long TestAthleteId = 12345;
    private Mock<ITrackStorageService> _storage = null!;
    private TracksController _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _storage = new Mock<ITrackStorageService>();
        var ownerOptions = Options.Create(new TrackOwnerOptions { OwnerAthleteId = TestAthleteId, UploadApiKey = "key" });
        _sut = new TracksController(
            _storage.Object,
            new Mock<ITrackParserService>().Object,
            ownerOptions,
            NullLogger<TracksController>.Instance);

        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, TestAthleteId.ToString()) };
        var identity = new ClaimsIdentity(claims, "Test");
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
    }

    [TearDown]
    public void TearDown() => _sut.Dispose();

    [Test]
    public async Task Delete_ValidId_DeletesTrackAndRedirectsToIndex()
    {
        _storage.Setup(s => s.DeleteTrackAsync(TestAthleteId, "abc123", It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

        var result = await _sut.Delete("abc123", CancellationToken.None);

        _storage.Verify(s => s.DeleteTrackAsync(TestAthleteId, "abc123", It.IsAny<CancellationToken>()), Times.Once);
        var redirect = result as RedirectToActionResult;
        Assert.That(redirect, Is.Not.Null);
        Assert.That(redirect!.ActionName, Is.EqualTo("Index"));
    }

    [Test]
    public async Task Delete_EmptyId_ReturnsBadRequest()
    {
        var result = await _sut.Delete("", CancellationToken.None);

        Assert.That(result, Is.InstanceOf<BadRequestResult>());
        _storage.Verify(s => s.DeleteTrackAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Delete_NullId_ReturnsBadRequest()
    {
        var result = await _sut.Delete(null!, CancellationToken.None);

        Assert.That(result, Is.InstanceOf<BadRequestResult>());
        _storage.Verify(s => s.DeleteTrackAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Index_ListsTracksForCurrentAthlete()
    {
        _storage.Setup(s => s.ListTracksAsync(TestAthleteId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<Models.TrackSummary>());

        var result = await _sut.Index(CancellationToken.None);

        _storage.Verify(s => s.ListTracksAsync(TestAthleteId, It.IsAny<CancellationToken>()), Times.Once);
        Assert.That(result, Is.InstanceOf<ViewResult>());
    }

    [Test]
    public async Task Detail_PassesAthleteIdToStorage()
    {
        var summary = new Models.TrackSummary { Id = "abc", AthleteId = TestAthleteId };
        _storage.Setup(s => s.GetTrackSummaryAsync(TestAthleteId, "abc", It.IsAny<CancellationToken>()))
                .ReturnsAsync(summary);

        var result = await _sut.Detail("abc", CancellationToken.None);

        _storage.Verify(s => s.GetTrackSummaryAsync(TestAthleteId, "abc", It.IsAny<CancellationToken>()), Times.Once);
        Assert.That(result, Is.InstanceOf<ViewResult>());
    }
}
