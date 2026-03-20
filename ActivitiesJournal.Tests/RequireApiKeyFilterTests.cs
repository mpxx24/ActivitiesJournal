using ActivitiesJournal.Configuration;
using ActivitiesJournal.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace ActivitiesJournal.Tests;

[TestFixture]
public class RequireApiKeyFilterTests
{
    private const string ValidKey = "test-api-key-abc123";

    private static ActionExecutingContext BuildContext(string? apiKeyHeader)
    {
        var httpContext = new DefaultHttpContext();
        if (apiKeyHeader != null)
            httpContext.Request.Headers["X-Api-Key"] = apiKeyHeader;

        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor());

        return new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            new object());
    }

    private static RequireApiKeyFilter CreateFilter(string configuredKey) =>
        new(Options.Create(new TrackOwnerOptions { UploadApiKey = configuredKey }));

    [Test]
    public void OnActionExecuting_ValidApiKey_PassesThrough()
    {
        var filter = CreateFilter(ValidKey);
        var context = BuildContext(ValidKey);

        filter.OnActionExecuting(context);

        Assert.That(context.Result, Is.Null);
    }

    [Test]
    public void OnActionExecuting_WrongApiKey_ReturnsUnauthorized()
    {
        var filter = CreateFilter(ValidKey);
        var context = BuildContext("wrong-key");

        filter.OnActionExecuting(context);

        Assert.That(context.Result, Is.InstanceOf<UnauthorizedResult>());
    }

    [Test]
    public void OnActionExecuting_MissingHeader_ReturnsUnauthorized()
    {
        var filter = CreateFilter(ValidKey);
        var context = BuildContext(null);

        filter.OnActionExecuting(context);

        Assert.That(context.Result, Is.InstanceOf<UnauthorizedResult>());
    }

    [Test]
    public void OnActionExecuting_EmptyHeader_ReturnsUnauthorized()
    {
        var filter = CreateFilter(ValidKey);
        var context = BuildContext(string.Empty);

        filter.OnActionExecuting(context);

        Assert.That(context.Result, Is.InstanceOf<UnauthorizedResult>());
    }
}
