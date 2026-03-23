using System.Security.Cryptography;
using System.Text;
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
        if (string.IsNullOrEmpty(provided) || string.IsNullOrEmpty(_options.UploadApiKey))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(provided));
        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(_options.UploadApiKey));
        if (!CryptographicOperations.FixedTimeEquals(providedHash, expectedHash))
        {
            context.Result = new UnauthorizedResult();
        }
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
}
