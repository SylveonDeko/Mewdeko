using DataModel;
using Discord.Rest;
using LinqToDB.Async;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Database.Enums;

namespace Mewdeko.Modules.Forms.Services;

/// <summary>
///     Whether a given person may submit a given form right now, and why not when they may not.
/// </summary>
/// <remarks>
///     This is one gate rather than several, because a check that only some callers run is a check
///     that does not exist. Everything that can turn a submission away lives here, and both the
///     public form page and the submit endpoint go through it.
/// </remarks>
public partial class FormsService
{
    /// <summary>
    ///     Checks whether someone may submit a form.
    /// </summary>
    /// <param name="formId">The form.</param>
    /// <param name="userId">The prospective submitter.</param>
    /// <returns>Whether they may, and why not when they may not.</returns>
    public async Task<Eligibility> CheckFormEligibilityAsync(int formId, ulong userId)
    {
        var form = await GetFormAsync(formId);

        return form == null
            ? Eligibility.No("Form not found")
            : await CheckFormEligibilityAsync(form, userId);
    }

    /// <summary>
    ///     Checks whether someone may submit a form.
    /// </summary>
    /// <param name="form">The form.</param>
    /// <param name="userId">The prospective submitter.</param>
    /// <returns>Whether they may, and why not when they may not.</returns>
    public async Task<Eligibility> CheckFormEligibilityAsync(Form form, ulong userId)
    {
        var now = DateTime.UtcNow;

        if (form.IsDraft)
            return Eligibility.No("This form has not been published yet");

        if (!form.IsActive)
            return Eligibility.No("This form is no longer accepting responses");

        if (form.OpensAt is { } opensAt && opensAt > now)
            return Eligibility.NotYet("This form is not open yet", opensAt);

        if (form.ExpiresAt is { } expiresAt && expiresAt <= now)
            return Eligibility.No("This form has closed and is no longer accepting responses");

        var guild = client.GetGuild(form.GuildId);
        if (guild == null)
            return Eligibility.No("This server is not reachable right now, try again shortly");

        if (form.MinAccountAgeDays is > 0)
        {
            var createdAt = SnowflakeUtils.FromSnowflake(userId).UtcDateTime;
            var eligibleAt = createdAt.AddDays(form.MinAccountAgeDays.Value);

            if (eligibleAt > now)
            {
                return Eligibility.NotYet(
                    $"Your Discord account must be at least {form.MinAccountAgeDays.Value} days old to submit this form",
                    eligibleAt);
            }
        }

        await using var db = await dbFactory.CreateConnectionAsync();

        if (form.MaxResponses.HasValue)
        {
            var total = await db.FormResponses.CountAsync(r => r.FormId == form.Id);

            if (total >= form.MaxResponses.Value)
                return Eligibility.No("This form has reached its maximum number of responses");
        }

        var history = await GetSubmissionHistoryAsync(db, form.Id, userId);

        if (!form.AllowMultipleSubmissions)
        {
            // A rejected response can be replaced when the form allows it, so someone told to fix
            // something is not locked out by the very response they were asked to correct.
            var blocking = form.AllowResubmitAfterRejection
                ? history.Where(h => h.Status != ResponseStatus.Rejected).ToList()
                : history;

            if (blocking.Count > 0)
                return Eligibility.No("You have already submitted a response to this form");
        }

        var member = guild.GetUser(userId);

        if (form.RequiredRoleId is { } requiredRoleId)
        {
            if (member == null)
                return Eligibility.No("You must be a member of this server to submit this form");

            if (!member.Roles.Any(r => r.Id == requiredRoleId))
            {
                var roleName = guild.GetRole(requiredRoleId)?.Name ?? "the required";
                return Eligibility.No($"You must have the {roleName} role to submit this form");
            }
        }

        return (FormType)form.FormType switch
        {
            FormType.BanAppeal => await CheckAppealEligibilityAsync(form, userId, guild, history),
            FormType.JoinApplication => member == null
                ? Eligibility.Allowed
                : Eligibility.No("You are already a member of this server"),
            _ => !form.AllowExternalUsers && member == null
                ? Eligibility.No("You must be a member of this server to submit this form")
                : Eligibility.Allowed
        };
    }

    /// <summary>
    ///     Applies the appeal policy of a ban appeal form, which is what stops a banned user filing
    ///     the same appeal every day until somebody gives in.
    /// </summary>
    private async Task<Eligibility> CheckAppealEligibilityAsync(
        Form form,
        ulong userId,
        IGuild guild,
        IReadOnlyList<SubmissionRecord> history)
    {
        IBan? ban;

        try
        {
            ban = await guild.GetBanAsync(userId);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not read ban for user {UserId} in guild {GuildId}", userId, guild.Id);
            ban = null;
        }

        if (ban == null)
            return Eligibility.No("You are not banned from this server");

        if (form.AppealDelayDays is > 0)
        {
            // Discord does not record when a ban was placed, so the audit log is the only source
            // for it. When it has aged out there is nothing to measure the delay against, and the
            // appeal is allowed rather than blocked on a date nobody can produce.
            var bannedAt = await FindBanDateAsync(guild, userId);

            if (bannedAt is { } placedAt)
            {
                var appealableAt = placedAt.AddDays(form.AppealDelayDays.Value);

                if (appealableAt > DateTime.UtcNow)
                {
                    return Eligibility.NotYet(
                        $"You must wait {form.AppealDelayDays.Value} days after your ban before appealing",
                        appealableAt);
                }
            }
        }

        if (history.Any(h => h.Status is ResponseStatus.Pending or ResponseStatus.UnderReview))
            return Eligibility.No("You already have an appeal awaiting review");

        var rejections = history.Where(h => h.Status == ResponseStatus.Rejected).ToList();

        if (rejections.Count == 0)
            return Eligibility.Allowed;

        if (form.BlockReappealAfterRejection)
            return Eligibility.No("Your appeal was rejected and this server does not accept further appeals");

        if (form.MaxAppealAttempts is { } maxAttempts && rejections.Count >= maxAttempts)
            return Eligibility.No($"You have used all {maxAttempts} of your appeals");

        if (form.ReappealCooldownDays is > 0)
        {
            var lastReviewedAt = rejections.Max(r => r.ReviewedAt);

            if (lastReviewedAt is { } reviewedAt)
            {
                var reappealableAt = reviewedAt.AddDays(form.ReappealCooldownDays.Value);

                if (reappealableAt > DateTime.UtcNow)
                    return Eligibility.NotYet("You cannot appeal again yet", reappealableAt);
            }
        }

        return Eligibility.Allowed;
    }

    /// <summary>
    ///     Finds when a ban was placed, by looking for it in the guild's audit log.
    /// </summary>
    /// <returns>When the ban was placed, or null when it is no longer in the log.</returns>
    private async Task<DateTime?> FindBanDateAsync(IGuild guild, ulong userId)
    {
        try
        {
            var entries = await guild.GetAuditLogsAsync(actionType: ActionType.Ban);

            var entry = entries.FirstOrDefault(e => e.Data is BanAuditLogData data && data.Target.Id == userId);

            return entry?.CreatedAt.UtcDateTime;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not read audit log for guild {GuildId}", guild.Id);
            return null;
        }
    }

    /// <summary>
    ///     Reads what someone has already submitted to a form, along with how each submission was
    ///     decided.
    /// </summary>
    private static async Task<List<SubmissionRecord>> GetSubmissionHistoryAsync(
        MewdekoDb db,
        int formId,
        ulong userId)
    {
        return await (
            from response in db.FormResponses
            where response.FormId == formId && response.UserId == userId
            from workflow in db.FormResponseWorkflows
                .Where(w => w.ResponseId == response.Id)
                .DefaultIfEmpty()
            select new SubmissionRecord(
                response.Id,
                workflow == null ? ResponseStatus.Pending : (ResponseStatus)workflow.Status,
                workflow == null ? null : workflow.ReviewedAt)
        ).ToListAsync();
    }

    /// <summary>
    ///     Why a form is not accepting a response from someone, together with when they may try again.
    /// </summary>
    /// <param name="IsEligible">Whether the person may submit.</param>
    /// <param name="Reason">Why not, or null when they may.</param>
    /// <param name="RetryAt">
    ///     When the refusal lifts, for the refusals that lift on their own. Null when the refusal is
    ///     permanent, or when nothing is waiting on a clock.
    /// </param>
    public record Eligibility(bool IsEligible, string? Reason, DateTime? RetryAt = null)
    {
        /// <summary>
        ///     An acceptance, with nothing to explain.
        /// </summary>
        public static Eligibility Allowed { get; } = new(true, null);

        /// <summary>
        ///     A refusal that will never lift.
        /// </summary>
        public static Eligibility No(string reason)
        {
            return new Eligibility(false, reason);
        }

        /// <summary>
        ///     A refusal that lifts at a known moment, so the page can count down to it rather than
        ///     leaving the reader to guess.
        /// </summary>
        public static Eligibility NotYet(string reason, DateTime retryAt)
        {
            return new Eligibility(false, reason, retryAt);
        }
    }

    /// <summary>
    ///     One of someone's earlier submissions to a form, and how it was decided.
    /// </summary>
    /// <param name="ResponseId">The response.</param>
    /// <param name="Status">Where it stands, treated as pending when it has no workflow row.</param>
    /// <param name="ReviewedAt">When it was decided, or null while it awaits review.</param>
    private record SubmissionRecord(int ResponseId, ResponseStatus Status, DateTime? ReviewedAt);
}