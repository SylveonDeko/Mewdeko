using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace Mewdeko.Middleware;

/// <inheritdoc />
public class ValidateModelAttribute : ActionFilterAttribute
{
    private readonly ILogger<ValidateModelAttribute> _logger;

    /// <inheritdoc />
    public ValidateModelAttribute(ILogger<ValidateModelAttribute> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.ModelState.IsValid) return;
        var errors = context.ModelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage)
            .ToList();

        _logger.LogWarning($"Validation Errors: {string.Join(", ", errors)}");

        context.Result = new BadRequestObjectResult(context.ModelState);
    }
}