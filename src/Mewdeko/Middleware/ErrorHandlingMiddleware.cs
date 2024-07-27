using System.Net;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace Mewdeko.Middleware;

/// <summary>
/// a
/// </summary>
public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate next;

    /// <summary>
    /// Stupidity
    /// </summary>
    /// <param name="next"></param>
    public ErrorHandlingMiddleware(RequestDelegate next)
    {
        this.next = next;
    }

    /// <summary>
    /// e
    /// </summary>
    /// <param name="context"></param>
    public async Task Invoke(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var code = HttpStatusCode.InternalServerError; // 500 if unexpected

        var result = JsonConvert.SerializeObject(new { error = exception.Message });
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)code;
        return context.Response.WriteAsync(result);
    }
}