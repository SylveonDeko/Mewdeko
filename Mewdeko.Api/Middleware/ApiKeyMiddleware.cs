using System.Net;
using Mewdeko.Api.Common;
using Mewdeko.Api.Services;

namespace Mewdeko.Api.Middleware;

public class ApiKeyMiddleware(RequestDelegate next, IApiKeyValidation apiKeyValidation)
{
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