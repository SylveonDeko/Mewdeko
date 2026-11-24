using Mewdeko.Controllers.Common.DashboardAccess;
using Mewdeko.Modules.Forms.Common;
using Mewdeko.Modules.Forms.Services;
using Mewdeko.Services;
using Mewdeko.Services.Impl;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Mewdeko.Controllers.Common.Forms;

/// <summary>
///     Who may reach a form action that is addressed by a record id rather than by a guild.
/// </summary>
public enum FormAccessMode
{
    /// <summary>
    ///     Only people with access to the form's guild section, by the same rule as every guild route.
    /// </summary>
    Staff,

    /// <summary>
    ///     Anyone may read a form that is no longer a draft. A draft is only visible to staff and looks missing to
    ///     everyone else.
    /// </summary>
    Viewable,

    /// <summary>
    ///     The person who submitted the response, or staff of the form's guild.
    /// </summary>
    OwnResponse
}

/// <summary>
///     What a form access check decided.
/// </summary>
public enum FormAccessOutcome
{
    /// <summary>
    ///     The action runs.
    /// </summary>
    Proceed,

    /// <summary>
    ///     The request carries no valid dashboard user token.
    /// </summary>
    Unauthorized,

    /// <summary>
    ///     The action answers as if the record did not exist.
    /// </summary>
    NotFound,

    /// <summary>
    ///     The caller is not a member of the form's guild.
    /// </summary>
    NotMember,

    /// <summary>
    ///     The caller is a member without the access level the request needs.
    /// </summary>
    InsufficientAccess
}

/// <summary>
///     The decision logic behind <see cref="FormAccessAttribute" />, kept apart from the request pipeline so it can
///     be tested on its own.
/// </summary>
public static class FormAccessPolicy
{
    /// <summary>
    ///     Decides whether a caller reaches a form action.
    /// </summary>
    /// <param name="mode">Who the action is for.</param>
    /// <param name="caller">The user from the dashboard token, or null when there is none.</param>
    /// <param name="scope">The form the id belongs to, or null when no such record exists.</param>
    /// <param name="staffCheck">Applies the dashboard access rule to the scope's guild.</param>
    /// <param name="isBotOwner">Tells whether a user id belongs to a bot owner.</param>
    /// <returns>The outcome. The scope is marked with whether the caller is staff.</returns>
    public static async Task<FormAccessOutcome> DecideAsync(
        FormAccessMode mode,
        ulong? caller,
        FormAccessScope? scope,
        Func<Task<DashboardAccessVerdict>> staffCheck,
        Func<ulong, bool> isBotOwner)
    {
        if (mode == FormAccessMode.Viewable)
        {
            if (scope is null)
                return FormAccessOutcome.Proceed;

            if (caller is not null)
                scope.CallerIsStaff = await staffCheck() == DashboardAccessVerdict.Allowed;

            return !scope.IsDraft || scope.CallerIsStaff ? FormAccessOutcome.Proceed : FormAccessOutcome.NotFound;
        }

        if (caller is null)
            return FormAccessOutcome.Unauthorized;

        if (scope is null)
            return FormAccessOutcome.Proceed;

        if (mode == FormAccessMode.OwnResponse && scope.ResponseUserId is { } owner &&
            DashboardCaller.CanActFor(caller, owner, isBotOwner))
            return FormAccessOutcome.Proceed;

        var verdict = await staffCheck();
        scope.CallerIsStaff = verdict == DashboardAccessVerdict.Allowed;

        return verdict switch
        {
            DashboardAccessVerdict.Allowed => FormAccessOutcome.Proceed,
            DashboardAccessVerdict.Unauthorized => FormAccessOutcome.Unauthorized,
            DashboardAccessVerdict.NotMember => FormAccessOutcome.NotMember,
            _ => FormAccessOutcome.InsufficientAccess
        };
    }
}

/// <summary>
///     Protects a form action whose route names a form, question, option, condition or response but no guild. The
///     guild is found from the record, then the same access rule as every guild route is applied. An id that does
///     not exist lets the action run so it gives its usual not found answer.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class FormAccessAttribute : TypeFilterAttribute
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="FormAccessAttribute" /> class.
    /// </summary>
    /// <param name="kind">What the route id names.</param>
    /// <param name="mode">Who the action is for.</param>
    public FormAccessAttribute(FormIdKind kind, FormAccessMode mode = FormAccessMode.Staff)
        : base(typeof(FormAccessFilter))
    {
        Arguments = [kind, mode];
    }
}

/// <summary>
///     Marks a form action that people filling in forms reach, so it is deliberately open to any signed in user.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class FormPublicAttribute : Attribute;

/// <summary>
///     Marks a form action that acts on one person's own data, whose binding to the signed in user is done by
///     <see cref="UserRouteBindingFilter" /> or inside the action.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class FormOwnDataAttribute : Attribute;

/// <summary>
///     Runs <see cref="FormAccessPolicy" /> for an action marked with <see cref="FormAccessAttribute" />.
/// </summary>
public sealed class FormAccessFilter(
    FormsService forms,
    DiscordShardedClient client,
    DashboardAccessService access,
    BotCredentials creds,
    FormIdKind kind,
    FormAccessMode mode) : IAsyncActionFilter
{
    private const string ScopeKey = "Mewdeko.FormAccessScope";

    /// <inheritdoc />
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var routeKey = kind switch
        {
            FormIdKind.Form => "formId",
            FormIdKind.Question => "questionId",
            FormIdKind.Option => "optionId",
            FormIdKind.Condition => "conditionId",
            _ => "responseId"
        };

        if (!context.RouteData.Values.TryGetValue(routeKey, out var raw) || !int.TryParse(raw?.ToString(), out var id))
        {
            context.Result = new NotFoundResult();
            return;
        }

        var http = context.HttpContext;
        var caller = await DashboardAccessRule.ReadUserIdAsync(http);
        var scope = await forms.ResolveAccessScopeAsync(kind, id);
        var section = ((ControllerActionDescriptor)context.ActionDescriptor).ControllerName;

        var outcome = await FormAccessPolicy.DecideAsync(
            mode,
            caller,
            scope,
            () => DashboardAccessRule.EvaluateGuildAsync(
                client, access, caller, http.Request.Method, scope!.GuildId, section),
            creds.IsOwner);

        context.Result = outcome switch
        {
            FormAccessOutcome.Proceed => null,
            FormAccessOutcome.NotFound => new NotFoundObjectResult(new
            {
                message = "Form not found"
            }),
            FormAccessOutcome.Unauthorized => DashboardAccessRule.ToResult(DashboardAccessVerdict.Unauthorized),
            FormAccessOutcome.NotMember => DashboardAccessRule.ToResult(DashboardAccessVerdict.NotMember),
            _ => DashboardAccessRule.ToResult(DashboardAccessVerdict.InsufficientAccess)
        };

        if (context.Result is not null)
            return;

        if (scope is not null)
            http.Items[ScopeKey] = scope;

        await next();
    }

    /// <summary>
    ///     Gets the scope an access check resolved for the current request.
    /// </summary>
    /// <param name="http">The current request.</param>
    /// <returns>The scope, or null when the id named no record.</returns>
    public static FormAccessScope? ScopeOf(HttpContext http)
    {
        return http.Items.TryGetValue(ScopeKey, out var scope) ? scope as FormAccessScope : null;
    }
}
