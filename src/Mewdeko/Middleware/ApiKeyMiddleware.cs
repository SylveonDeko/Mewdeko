using System.Net;
using Mewdeko.Api.Services;
using Microsoft.AspNetCore.Http;

namespace Mewdeko.Middleware;

/// <summary>
/// Api Key middlewarte for mewdekos dashboard
/// </summary>
/// <param name="next"></param>
/// <param name="apiKeyValidation"></param>
public class ApiKeyMiddleware(RequestDelegate next, IApiKeyValidation apiKeyValidation)
{
    /// <summary>
    /// Checks if the given api key is valid
    /// </summary>
    /// <param name="context"></param>
    public async Task InvokeAsync(HttpContext context)
    {
        if (string.IsNullOrWhiteSpace(context.Request.Headers[ApiConstants.HeaderName]))
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            return;
        }

        string? userApiKey = context.Request.Headers[ApiConstants.HeaderName];

        if (!apiKeyValidation.IsValidApiKey(userApiKey!))
        {
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            return;
        }

        await next(context);
    }
}