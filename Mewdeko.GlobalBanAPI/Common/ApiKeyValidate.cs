using Mewdeko.GlobalBanAPI.DbStuff;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Mewdeko.GlobalBanAPI.Common;

public class ApiKeyAuthorizeFilter(DbService dbContext) : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        using var uow = dbContext.GetDbContext();
        var isAttributeApplied = context.ActionDescriptor.EndpointMetadata
            .Any(meta => meta is ApiKeyAuthorizeAttribute);

        if (!isAttributeApplied) return;

        if (!context.HttpContext.Request.Headers.TryGetValue("ApiKey", out var extractedApiKey))
        {
            context.Result = new ContentResult()
            {
                StatusCode = 401, Content = "No API key provided.", ContentType = "text/plain"
            };
            return;
        }

        var keyExists = uow.Keys.Any(k => k.Key == extractedApiKey.ToString());
        if (!keyExists)
        {
            context.Result = new ContentResult()
            {
                StatusCode = 403, Content = "Invalid API key.", ContentType = "text/plain"
            };
        }
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
        // unused
    }
}