using System.Reflection;
using Mewdeko.Controllers;
using Mewdeko.Controllers.Common.DashboardAccess;
using Mewdeko.Controllers.Common.Forms;
using Mewdeko.Database.Enums;
using Mewdeko.Modules.Forms.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Mewdeko.Tests;

/// <summary>
///     Verifies who may reach the forms actions: the shared dashboard access rule, the checks for actions that only
///     know a record id, and that every action of the controller has been classified.
/// </summary>
[TestFixture]
public class FormsAccessTests
{
    private const ulong Caller = 100;
    private const ulong Other = 200;
    private const ulong Guild = 5000;

    private static DashboardGuildMember Member(bool owner = false, bool admin = false)
    {
        return new DashboardGuildMember(owner, admin, [1UL, 2UL]);
    }

    private static Task<DashboardAccessVerdict> Evaluate(
        ulong? user,
        DashboardGuildMember? member,
        string method,
        DashboardAccessLevel granted)
    {
        return DashboardAccessRule.EvaluateAsync(
            user, member, DashboardAccessRule.RequiredLevel(method), _ => Task.FromResult(granted));
    }

    [Test]
    public async Task RuleRequiresAToken()
    {
        var verdict = await Evaluate(null, Member(admin: true), "GET", DashboardAccessLevel.Manage);

        Assert.That(verdict, Is.EqualTo(DashboardAccessVerdict.Unauthorized));
        Assert.That(DashboardAccessRule.ToResult(verdict), Is.TypeOf<UnauthorizedResult>());
    }

    [Test]
    public async Task RuleRefusesANonMember()
    {
        var verdict = await Evaluate(Caller, null, "GET", DashboardAccessLevel.Manage);

        Assert.That(verdict, Is.EqualTo(DashboardAccessVerdict.NotMember));
        Assert.That(((ObjectResult)DashboardAccessRule.ToResult(verdict)!).StatusCode, Is.EqualTo(403));
    }

    [Test]
    public async Task RuleRefusesAMemberWithoutAccess()
    {
        var verdict = await Evaluate(Caller, Member(), "GET", DashboardAccessLevel.None);

        Assert.That(verdict, Is.EqualTo(DashboardAccessVerdict.InsufficientAccess));
        Assert.That(((ObjectResult)DashboardAccessRule.ToResult(verdict)!).StatusCode, Is.EqualTo(403));
    }

    [Test]
    public async Task ViewAccessAllowsReadsOnly()
    {
        Assert.That(await Evaluate(Caller, Member(), "GET", DashboardAccessLevel.View),
            Is.EqualTo(DashboardAccessVerdict.Allowed));
        Assert.That(await Evaluate(Caller, Member(), "HEAD", DashboardAccessLevel.View),
            Is.EqualTo(DashboardAccessVerdict.Allowed));

        foreach (var method in new[] { "POST", "PUT", "PATCH", "DELETE" })
        {
            Assert.That(await Evaluate(Caller, Member(), method, DashboardAccessLevel.View),
                Is.EqualTo(DashboardAccessVerdict.InsufficientAccess), method);
        }
    }

    [Test]
    public async Task ManageAccessAllowsWrites()
    {
        Assert.That(await Evaluate(Caller, Member(), "DELETE", DashboardAccessLevel.Manage),
            Is.EqualTo(DashboardAccessVerdict.Allowed));
    }

    [Test]
    public async Task AdministratorAndOwnerPassWithoutAGrant()
    {
        Assert.That(await Evaluate(Caller, Member(admin: true), "DELETE", DashboardAccessLevel.None),
            Is.EqualTo(DashboardAccessVerdict.Allowed));
        Assert.That(await Evaluate(Caller, Member(owner: true), "DELETE", DashboardAccessLevel.None),
            Is.EqualTo(DashboardAccessVerdict.Allowed));
    }

    private static FormAccessScope Scope(bool draft = false, ulong? responseUser = null)
    {
        return new FormAccessScope
        {
            FormId = 1, GuildId = Guild, IsDraft = draft, ResponseUserId = responseUser
        };
    }

    private static Task<FormAccessOutcome> Decide(
        FormAccessMode mode,
        ulong? caller,
        FormAccessScope? scope,
        DashboardAccessVerdict staff)
    {
        return FormAccessPolicy.DecideAsync(mode, caller, scope, () => Task.FromResult(staff), id => id == 999);
    }

    [Test]
    public async Task StaffModeNeedsATokenBeforeAnythingElse()
    {
        Assert.That(await Decide(FormAccessMode.Staff, null, Scope(), DashboardAccessVerdict.Allowed),
            Is.EqualTo(FormAccessOutcome.Unauthorized));
        Assert.That(await Decide(FormAccessMode.Staff, null, null, DashboardAccessVerdict.Allowed),
            Is.EqualTo(FormAccessOutcome.Unauthorized));
    }

    [Test]
    public async Task StaffModeMapsTheRuleVerdicts()
    {
        Assert.That(await Decide(FormAccessMode.Staff, Caller, Scope(), DashboardAccessVerdict.NotMember),
            Is.EqualTo(FormAccessOutcome.NotMember));
        Assert.That(await Decide(FormAccessMode.Staff, Caller, Scope(), DashboardAccessVerdict.InsufficientAccess),
            Is.EqualTo(FormAccessOutcome.InsufficientAccess));

        var scope = Scope();
        Assert.That(await Decide(FormAccessMode.Staff, Caller, scope, DashboardAccessVerdict.Allowed),
            Is.EqualTo(FormAccessOutcome.Proceed));
        Assert.That(scope.CallerIsStaff, Is.True);
    }

    [Test]
    public async Task AnUnknownIdLetsTheActionGiveItsOwnNotFoundAnswer()
    {
        Assert.That(await Decide(FormAccessMode.Staff, Caller, null, DashboardAccessVerdict.NotMember),
            Is.EqualTo(FormAccessOutcome.Proceed));
        Assert.That(await Decide(FormAccessMode.OwnResponse, Caller, null, DashboardAccessVerdict.NotMember),
            Is.EqualTo(FormAccessOutcome.Proceed));
        Assert.That(await Decide(FormAccessMode.Viewable, null, null, DashboardAccessVerdict.NotMember),
            Is.EqualTo(FormAccessOutcome.Proceed));
    }

    [Test]
    public async Task APublishedFormIsViewableByAnyoneAndOnlyStaffAreMarkedAsStaff()
    {
        var anonymous = Scope();
        Assert.That(await Decide(FormAccessMode.Viewable, null, anonymous, DashboardAccessVerdict.Unauthorized),
            Is.EqualTo(FormAccessOutcome.Proceed));
        Assert.That(anonymous.CallerIsStaff, Is.False);

        var stranger = Scope();
        Assert.That(await Decide(FormAccessMode.Viewable, Caller, stranger, DashboardAccessVerdict.NotMember),
            Is.EqualTo(FormAccessOutcome.Proceed));
        Assert.That(stranger.CallerIsStaff, Is.False);

        var staff = Scope();
        Assert.That(await Decide(FormAccessMode.Viewable, Caller, staff, DashboardAccessVerdict.Allowed),
            Is.EqualTo(FormAccessOutcome.Proceed));
        Assert.That(staff.CallerIsStaff, Is.True);
    }

    [Test]
    public async Task ADraftLooksMissingToEveryoneButStaff()
    {
        Assert.That(await Decide(FormAccessMode.Viewable, null, Scope(true), DashboardAccessVerdict.Unauthorized),
            Is.EqualTo(FormAccessOutcome.NotFound));
        Assert.That(await Decide(FormAccessMode.Viewable, Caller, Scope(true), DashboardAccessVerdict.NotMember),
            Is.EqualTo(FormAccessOutcome.NotFound));
        Assert.That(await Decide(FormAccessMode.Viewable, Caller, Scope(true), DashboardAccessVerdict.Allowed),
            Is.EqualTo(FormAccessOutcome.Proceed));
    }

    [Test]
    public async Task AResponseIsOpenToItsSubmitterAndToStaffOnly()
    {
        Assert.That(
            await Decide(FormAccessMode.OwnResponse, Caller, Scope(responseUser: Caller),
                DashboardAccessVerdict.NotMember), Is.EqualTo(FormAccessOutcome.Proceed));
        Assert.That(
            await Decide(FormAccessMode.OwnResponse, Caller, Scope(responseUser: Other),
                DashboardAccessVerdict.NotMember), Is.EqualTo(FormAccessOutcome.NotMember));
        Assert.That(
            await Decide(FormAccessMode.OwnResponse, Caller, Scope(responseUser: Other),
                DashboardAccessVerdict.Allowed), Is.EqualTo(FormAccessOutcome.Proceed));
        Assert.That(
            await Decide(FormAccessMode.OwnResponse, null, Scope(responseUser: Caller),
                DashboardAccessVerdict.Allowed), Is.EqualTo(FormAccessOutcome.Unauthorized));
        Assert.That(
            await Decide(FormAccessMode.OwnResponse, Caller, Scope(responseUser: null),
                DashboardAccessVerdict.InsufficientAccess), Is.EqualTo(FormAccessOutcome.InsufficientAccess));
    }

    [Test]
    public async Task ABotOwnerMayActForTheSubmitter()
    {
        Assert.That(
            await Decide(FormAccessMode.OwnResponse, 999, Scope(responseUser: Other),
                DashboardAccessVerdict.NotMember), Is.EqualTo(FormAccessOutcome.Proceed));
    }

    [Test]
    public void EveryFormsActionIsClassifiedAndItsIdMatchesItsRoute()
    {
        var actions = typeof(FormsController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any())
            .ToList();

        Assert.That(actions, Has.Count.GreaterThan(30));

        foreach (var action in actions)
        {
            var template = action.GetCustomAttributes<HttpMethodAttribute>().First().Template ?? "";
            var access = action.GetCustomAttribute<FormAccessAttribute>();
            var classified = access != null
                             || action.GetCustomAttribute<FormPublicAttribute>() != null
                             || action.GetCustomAttribute<FormOwnDataAttribute>() != null
                             || action.GetCustomAttribute<RequireOwnerAttribute>() != null
                             || template.Contains("{guildId:ulong}");

            Assert.That(classified, Is.True, $"{action.Name} has no access classification");

            if (access == null)
                continue;

            var kind = (FormIdKind)access.Arguments![0];
            var routeKey = kind switch
            {
                FormIdKind.Form => "{formId:int}",
                FormIdKind.Question => "{questionId:int}",
                FormIdKind.Option => "{optionId:int}",
                FormIdKind.Condition => "{conditionId:int}",
                _ => "{responseId:int}"
            };

            Assert.That(template, Does.Contain(routeKey), $"{action.Name} resolves its guild from the wrong id");
        }
    }

    [Test]
    public void ActionsThatTouchStaffDataAreNotMarkedPublic()
    {
        var staffOnly = new[]
        {
            nameof(FormsController.UpdateForm), nameof(FormsController.DeleteForm),
            nameof(FormsController.GetFormResponses), nameof(FormsController.ExportResponses),
            nameof(FormsController.ApproveResponse), nameof(FormsController.RejectResponse),
            nameof(FormsController.DeleteResponse), nameof(FormsController.GetPendingResponses),
            nameof(FormsController.RestoreFormVersion), nameof(FormsController.GenerateShareLink)
        };

        foreach (var name in staffOnly)
        {
            var method = typeof(FormsController).GetMethod(name)!;
            var access = method.GetCustomAttribute<FormAccessAttribute>();

            Assert.That(access, Is.Not.Null, name);
            Assert.That((FormAccessMode)access!.Arguments![1], Is.EqualTo(FormAccessMode.Staff), name);
            Assert.That(method.GetCustomAttribute<FormPublicAttribute>(), Is.Null, name);
        }
    }
}
