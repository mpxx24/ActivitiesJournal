using ActivitiesJournal.Configuration;
using ActivitiesJournal.Controllers;
using ActivitiesJournal.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ActivitiesJournal.Tests;

[TestFixture]
public class StravaControllerTests
{
    private Mock<IStravaService> _stravaService = null!;
    private StravaController _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _stravaService = new Mock<IStravaService>();
        var ownerOptions = Options.Create(new TrackOwnerOptions { OwnerAthleteId = 123, UploadApiKey = "key" });
        _sut = new StravaController(
            _stravaService.Object,
            ownerOptions,
            NullLogger<StravaController>.Instance);

        var httpContext = new DefaultHttpContext();
        var tempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
        _sut.ControllerContext = new ControllerContext { HttpContext = httpContext };
        _sut.TempData = tempData;
        _sut.Url = Mock.Of<IUrlHelper>();
    }

    [TearDown]
    public void TearDown() => _sut.Dispose();

    [Test]
    public void ClearCache_LocalReturnUrl_Redirects()
    {
        _sut.Url = Mock.Of<IUrlHelper>(u => u.IsLocalUrl("/Activities") == true);
        var result = _sut.ClearCache("/Activities") as RedirectResult;

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Url, Is.EqualTo("/Activities"));
    }

    [Test]
    public void ClearCache_ExternalUrl_RedirectsToRoot()
    {
        _sut.Url = Mock.Of<IUrlHelper>(u => u.IsLocalUrl("https://evil.com") == false);
        var result = _sut.ClearCache("https://evil.com") as RedirectResult;

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Url, Is.EqualTo("/"));
    }

    [Test]
    public void ClearCache_NullReturnUrl_RedirectsToRoot()
    {
        _sut.Url = Mock.Of<IUrlHelper>();
        var result = _sut.ClearCache(null) as RedirectResult;

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Url, Is.EqualTo("/"));
    }

    [Test]
    public void Authorize_GeneratesStateAndStoresInTempData()
    {
        _stravaService.Setup(s => s.GetAuthorizationUrl(It.IsAny<string>()))
                      .Returns("https://www.strava.com/oauth/authorize?state=abc");

        var result = _sut.Authorize() as RedirectResult;

        Assert.That(result, Is.Not.Null);
        Assert.That(_sut.TempData["OAuthState"], Is.Not.Null);
        _stravaService.Verify(s => s.GetAuthorizationUrl(It.Is<string>(st => !string.IsNullOrEmpty(st))), Times.Once);
    }

    [Test]
    public async Task Callback_MismatchedState_ReturnsError()
    {
        _sut.TempData["OAuthState"] = "expected-state";

        var result = await _sut.Callback("some-code", "wrong-state") as ViewResult;

        Assert.That(result, Is.Not.Null);
        Assert.That(_sut.ViewBag.Error, Does.Contain("Invalid state"));
    }

    [Test]
    public async Task Callback_MissingState_ReturnsError()
    {
        // No OAuthState in TempData
        var result = await _sut.Callback("some-code", "any-state") as ViewResult;

        Assert.That(result, Is.Not.Null);
        Assert.That(_sut.ViewBag.Error, Does.Contain("Invalid state"));
    }
}
