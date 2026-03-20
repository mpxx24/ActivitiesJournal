using ActivitiesJournal.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace ActivitiesJournal.Filters;

public class RequireApiKeyFilter : IActionFilter
{
    private const string ApiKeyHeader = "X-Api-Key";
    private readonly TrackOwnerOptions _options;

    public RequireApiKeyFilter(IOptions<TrackOwnerOptions> options)
    {
        _options = options.Value;
    }

    public void OnActionExecuting(ActionExecutingContext context)
    {
        var provided = context.HttpContext.Request.Headers[ApiKeyHeader].FirstOrDefault();
        if (string.IsNullOrEmpty(provided) ||
            !string.Equals(provided, _options.UploadApiKey, StringComparison.Ordinal))
        {
            context.Result = new UnauthorizedResult();
        }
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
}
