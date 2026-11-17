using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Mewdeko.Controllers.Common;

/// <summary>
///     Rejects a request whose required JSON body is missing or could not be read, before the action runs.
///     Model state validation is turned off for this API, so without this a missing body reaches the action as
///     <c>null</c>.
/// </summary>
public sealed class RequiredBodyFilter : IActionFilter
{
    private static readonly NullabilityInfoContext Nullability = new();

    /// <inheritdoc />
    public void OnActionExecuting(ActionExecutingContext context)
    {
        foreach (var parameter in context.ActionDescriptor.Parameters)
        {
            if (parameter.BindingInfo?.BindingSource != BindingSource.Body)
                continue;

            if (context.ActionArguments.TryGetValue(parameter.Name, out var value) && value is not null)
                continue;

            if (parameter is ControllerParameterDescriptor { ParameterInfo: var info } && IsOptional(info))
                continue;

            context.Result = new BadRequestObjectResult("The request body is missing or is not valid JSON.");
            return;
        }
    }

    /// <inheritdoc />
    public void OnActionExecuted(ActionExecutedContext context)
    {
    }

    private static bool IsOptional(ParameterInfo info)
    {
        if (info.HasDefaultValue || Nullable.GetUnderlyingType(info.ParameterType) is not null)
            return true;

        lock (Nullability)
        {
            return Nullability.Create(info).ReadState == NullabilityState.Nullable;
        }
    }
}
