using DataModel;
using Mewdeko.Database.Enums;

namespace Mewdeko.Modules.Forms.Services;

/// <summary>
///     The roles a form hands out and takes away as a response moves through review, and the direct
///     message telling the submitter what was decided.
/// </summary>
public partial class FormsService
{
    /// <summary>
    ///     Reads a stored comma separated list of role identifiers.
    /// </summary>
    /// <param name="stored">The stored list, which may be null or empty.</param>
    /// <returns>The identifiers, ignoring anything that is not one.</returns>
    public static List<ulong> ReadRoleIds(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
            return [];

        return stored
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(entry => ulong.TryParse(entry, out var id) ? id : 0)
            .Where(id => id > 0)
            .Distinct()
            .ToList();
    }

    /// <summary>
    ///     Grants the roles a form hands out the moment a response arrives, before anyone has looked
    ///     at it. Used for things like a "submitted" tag that unlocks a waiting room channel.
    /// </summary>
    /// <param name="form">The form that was submitted.</param>
    /// <param name="userId">The submitter.</param>
    public async Task ApplySubmitRolesAsync(Form form, ulong userId)
    {
        var roleIds = ReadRoleIds(form.SubmitRoleIds);

        if (form.PendingRoleId is { } pendingRoleId)
            roleIds.Add(pendingRoleId);

        if (roleIds.Count == 0)
            return;

        await ChangeRolesAsync(form, userId, roleIds, [], "Form submitted");
    }

    /// <summary>
    ///     Applies what a form does to a submitter's roles when their response is decided.
    /// </summary>
    /// <remarks>
    ///     A decision may both add and remove roles, which is what an application usually wants: the
    ///     applicant loses the pending tag and gains the role they applied for in one step, rather
    ///     than in two decisions that can half fail. The older single list and action type are still
    ///     honoured for forms configured before the split existed.
    /// </remarks>
    /// <param name="form">The form the response belongs to.</param>
    /// <param name="userId">The submitter.</param>
    /// <param name="isApproval">Whether the response was approved.</param>
    /// <returns>True when any role actually changed.</returns>
    public async Task<bool> ApplyDecisionRolesAsync(Form form, ulong userId, bool isApproval)
    {
        var add = ReadRoleIds(isApproval ? form.ApprovalAddRoleIds : form.RejectionAddRoleIds);
        var remove = ReadRoleIds(isApproval ? form.ApprovalRemoveRoleIds : form.RejectionRemoveRoleIds);

        // Forms built before the add and remove lists existed carry one list and a separate flag
        // saying which way it points.
        var legacyList = ReadRoleIds(isApproval ? form.ApprovalRoleIds : form.RejectionRoleIds);
        var legacyAction = (RoleActionType)(isApproval ? form.ApprovalActionType : form.RejectionActionType);

        if (legacyList.Count > 0)
        {
            switch (legacyAction)
            {
                case RoleActionType.AddRoles:
                    add.AddRange(legacyList);
                    break;

                case RoleActionType.RemoveRoles:
                    remove.AddRange(legacyList);
                    break;
            }
        }

        if (form.PendingRoleId is { } pendingRoleId)
            remove.Add(pendingRoleId);

        add = add.Distinct().ToList();

        // A role named on both sides is contradictory. Removal wins, because taking access away is
        // the safer reading of an ambiguous configuration.
        remove = remove.Distinct().ToList();
        add.RemoveAll(remove.Contains);

        if (add.Count == 0 && remove.Count == 0)
            return false;

        var reason = isApproval ? "Form response approved" : "Form response rejected";

        return await ChangeRolesAsync(form, userId, add, remove, reason);
    }

    /// <summary>
    ///     Adds and removes roles in one operation, skipping any the bot cannot manage.
    /// </summary>
    /// <returns>True when at least one role changed.</returns>
    private async Task<bool> ChangeRolesAsync(
        Form form,
        ulong userId,
        IReadOnlyCollection<ulong> add,
        IReadOnlyCollection<ulong> remove,
        string reason)
    {
        // An anonymous form records no submitter, so there is nobody whose roles could be changed.
        if (form.AllowAnonymous)
        {
            logger.LogWarning("Form {FormId} is anonymous, so its role actions were skipped", form.Id);
            return false;
        }

        var guild = client.GetGuild(form.GuildId);
        var member = guild?.GetUser(userId);

        if (guild == null || member == null)
        {
            logger.LogWarning("User {UserId} is not in guild {GuildId}, so form role actions were skipped",
                userId, form.GuildId);
            return false;
        }

        if (!guild.CurrentUser.GuildPermissions.ManageRoles)
        {
            logger.LogError("Missing Manage Roles in guild {GuildId}, so form {FormId} could not change roles",
                form.GuildId, form.Id);
            return false;
        }

        var options = new RequestOptions
        {
            AuditLogReason = $"{reason} (form #{form.Id}: {form.Name.TrimTo(50)})"
        };

        var changed = false;

        try
        {
            var toAdd = Manageable(guild, add).Where(r => member.Roles.All(existing => existing.Id != r.Id))
                .ToList();

            var toRemove = Manageable(guild, remove).Where(r => member.Roles.Any(existing => existing.Id == r.Id))
                .ToList();

            if (toAdd.Count > 0)
            {
                await member.AddRolesAsync(toAdd, options);
                changed = true;
            }

            if (toRemove.Count > 0)
            {
                await member.RemoveRolesAsync(toRemove, options);
                changed = true;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to change roles for user {UserId} on form {FormId}", userId, form.Id);
            return false;
        }

        return changed;
    }

    /// <summary>
    ///     Filters a list of role identifiers down to the roles that exist and that the bot sits
    ///     above in the hierarchy, so an unmanageable role is skipped rather than failing the batch.
    /// </summary>
    private List<IRole> Manageable(SocketGuild guild, IReadOnlyCollection<ulong> roleIds)
    {
        if (roleIds.Count == 0)
            return [];

        var botMember = guild.CurrentUser;
        var highest = botMember.Roles.Max(r => r.Position);

        var manageable = new List<IRole>();

        foreach (var roleId in roleIds)
        {
            var role = guild.GetRole(roleId);

            if (role == null)
            {
                logger.LogWarning("Role {RoleId} no longer exists in guild {GuildId}", roleId, guild.Id);
                continue;
            }

            if (role.IsEveryone)
                continue;

            if (role.Position >= highest || role.IsManaged)
            {
                logger.LogWarning("Role {RoleName} in guild {GuildId} is above the bot or externally managed",
                    role.Name, guild.Id);
                continue;
            }

            manageable.Add(role);
        }

        return manageable;
    }

    /// <summary>
    ///     Tells a submitter what was decided about their response.
    /// </summary>
    /// <remarks>
    ///     People routinely have direct messages closed, and a decision the submitter never hears
    ///     about looks to them like no decision at all. The failure is reported back so it can be
    ///     recorded against the response and shown to the reviewer, rather than swallowed.
    /// </remarks>
    /// <param name="form">The form the response belongs to.</param>
    /// <param name="userId">The submitter.</param>
    /// <param name="approved">Whether the response was approved.</param>
    /// <param name="notes">The reviewer's notes, shown to the submitter when there are any.</param>
    /// <param name="inviteCode">An invite generated by the decision, if any.</param>
    /// <returns>True when the message was delivered.</returns>
    public async Task<bool> NotifySubmitterAsync(
        Form form,
        ulong userId,
        bool approved,
        string? notes,
        string? inviteCode)
    {
        try
        {
            var user = await client.Rest.GetUserAsync(userId);
            if (user == null)
                return false;

            var guild = client.GetGuild(form.GuildId);

            var embed = new EmbedBuilder()
                .WithTitle(approved ? "Your response was approved" : "Your response was not accepted")
                .WithDescription(form.Name)
                .WithColor(approved ? Mewdeko.OkColor : Mewdeko.ErrorColor)
                .WithCurrentTimestamp();

            if (guild != null)
                embed.WithFooter(guild.Name, guild.IconUrl);

            if (!string.IsNullOrWhiteSpace(notes))
                embed.AddField("Reviewer notes", notes.TrimTo(1024));

            if (approved && !string.IsNullOrWhiteSpace(form.SuccessMessage))
                embed.AddField("Message", form.SuccessMessage.TrimTo(1024));

            if (!string.IsNullOrWhiteSpace(inviteCode))
                embed.AddField("Invite", $"https://discord.gg/{inviteCode}");

            var channel = await user.CreateDMChannelAsync();
            await channel.SendMessageAsync(embed: embed.Build());

            return true;
        }
        catch (Exception ex)
        {
            logger.LogInformation(ex, "Could not tell user {UserId} about their response to form {FormId}",
                userId, form.Id);
            return false;
        }
    }
}