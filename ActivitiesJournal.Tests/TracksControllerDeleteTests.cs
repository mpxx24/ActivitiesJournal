using ActivitiesJournal.Controllers;
using ActivitiesJournal.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ActivitiesJournal.Tests;

[TestFixture]
public class TracksControllerDeleteTests
{
    private Mock<ITrackStorageService> _storage = null!;
    private TracksController _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _storage = new Mock<ITrackStorageService>();
        _sut = new TracksController(
            _storage.Object,
            new Mock<ITrackParserService>().Object,
            NullLogger<TracksController>.Instance);
    }

    [TearDown]
    public void TearDown() => _sut.Dispose();

    [Test]
    public async Task Delete_ValidId_DeletesTrackAndRedirectsToIndex()
    {
        _storage.Setup(s => s.DeleteTrackAsync("abc123", It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

        var result = await _sut.Delete("abc123", CancellationToken.None);

        _storage.Verify(s => s.DeleteTrackAsync("abc123", It.IsAny<CancellationToken>()), Times.Once);
        var redirect = result as RedirectToActionResult;
        Assert.That(redirect, Is.Not.Null);
        Assert.That(redirect!.ActionName, Is.EqualTo("Index"));
    }

    [Test]
    public async Task Delete_EmptyId_ReturnsBadRequest()
    {
        var result = await _sut.Delete("", CancellationToken.None);

        Assert.That(result, Is.InstanceOf<BadRequestResult>());
        _storage.Verify(s => s.DeleteTrackAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Delete_NullId_ReturnsBadRequest()
    {
        var result = await _sut.Delete(null!, CancellationToken.None);

        Assert.That(result, Is.InstanceOf<BadRequestResult>());
        _storage.Verify(s => s.DeleteTrackAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
