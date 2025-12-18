using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.RoleStates.Services;
using Mewdeko.Services.Settings;

namespace Mewdeko.Modules.RoleStates;

/// <summary>
///     Provides slash commands for managing RoleStates in a guild. RoleStates allow the bot to save the roles of users
///     when they leave the server and automatically reassign those roles if they rejoin.
/// </summary>
/// <remarks>
///     This feature is particularly useful for maintaining continuity in user permissions and roles across server leaves
///     and rejoins.
///     Commands in this module allow administrators to enable/disable the feature, manage settings, and manipulate role
///     states for specific users.
/// </remarks>
[Group("rolestates", "Manage roles for users when they leave and rejoin!")]
public class SlashRoleStates(BotConfigService bss, InteractiveService interactivity)
    : MewdekoSlashModuleBase<RoleStatesService>
{
    /// <summary>
    ///     Toggles the RoleStates feature on or off for the guild, allowing or preventing the bot from saving and restoring
    ///     role states.
    /// </summary>
    /// <remarks>
    ///     When enabled, the bot tracks the roles of all users in the guild. If a user leaves and later rejoins the guild,
    ///     their previously assigned roles are automatically reapplied, assuming they haven't been excluded from the feature.
    ///     This command requires administrator permissions to use.
    /// </remarks>
    [SlashCommand("toggle", "Toggle whether RoleStates are enabled")]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task ToggleRoleStates()
    {
        if (await Service.ToggleRoleStates(ctx.Guild.Id))
            await ctx.Interaction.SendConfirmAsync(Strings.RoleStatesEnabled(ctx.Guild.Id, bss.Data.SuccessEmote));
        else
            await ctx.Interaction.SendConfirmAsync(Strings.RoleStatesDisabled(ctx.Guild.Id, bss.Data.SuccessEmote));
    }

    /// <summary>
    ///     Toggles whether Role States will ignore bots when saving and restoring roles.
    /// </summary>
    /// <remarks>
    ///     If enabled, bot users will not have their roles saved or restored. This can help prevent clutter and maintain
    ///     role integrity, especially in servers where bots may frequently join and leave.
    ///     This command requires administrator permissions to use.
    /// </remarks>
    [SlashCommand("toggle-ignore-bots", "Toggle whether Role States will ignore bots")]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task ToggleRoleStatesIgnoreBots()
    {
        var roleStateSettings = await Service.GetRoleStateSettings(ctx.Guild.Id);
        if (roleStateSettings is null)
        {
            await ctx.Interaction.SendErrorAsync(
                Strings.RoleStatesNotEnabledSlash(ctx.Guild.Id, bss.Data.ErrorEmote), Config);
            return;
        }

        if (await Service.ToggleIgnoreBots(roleStateSettings))
            await ctx.Interaction.SendConfirmAsync(Strings.RoleStatesIgnoreBots(ctx.Guild.Id, bss.Data.SuccessEmote));
        else
            await ctx.Interaction.SendConfirmAsync(Strings.RoleStatesNotIgnoreBots(ctx.Guild.Id,
                bss.Data.SuccessEmote));
    }

    /// <summary>
    ///     Toggles whether saved role states should be cleared when a user is banned from the guild.
    /// </summary>
    /// <remarks>
    ///     Enabling this option ensures that users who are banned lose their saved roles, preventing their automatic
    ///     reapplication should they be unbanned and rejoin the server.
    ///     This command requires administrator permissions to use.
    /// </remarks>
    [SlashCommand("toggle-clear-on-ban", "Toggle whether role states get cleared on ban")]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task ToggleRoleStatesClearOnBan()
    {
        var roleStateSettings = await Service.GetRoleStateSettings(ctx.Guild.Id);
        if (roleStateSettings is null)
        {
            await ctx.Interaction.SendErrorAsync(
                Strings.RoleStatesNotEnabledSlash(ctx.Guild.Id, bss.Data.ErrorEmote), Config);
            return;
        }

        if (await Service.ToggleClearOnBan(roleStateSettings))
            await ctx.Interaction.SendConfirmAsync(
                $"{bss.Data.SuccessEmote} {Strings.RoleStatesWillClearOnBan(ctx.Guild.Id)}");
        else
            await ctx.Interaction.SendConfirmAsync(
                $"{bss.Data.SuccessEmote} {Strings.RoleStatesWillNotClearOnBan(ctx.Guild.Id)}");
    }

    /// <summary>
    ///     Toggles whether auto-assign roles should be skipped for users with saved role states.
    /// </summary>
    /// <remarks>
    ///     When enabled, users who have a saved role state will not receive auto-assign roles when they rejoin.
    ///     This prevents duplicate role assignments and allows role states to take priority.
    ///     This command requires administrator permissions to use.
    /// </remarks>
    [SlashCommand("toggle-skip-auto-assign", "Toggle whether auto-assign roles are skipped for users with role states")]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task ToggleRoleStatesSkipAutoAssign()
    {
        var roleStateSettings = await Service.GetRoleStateSettings(ctx.Guild.Id);
        if (roleStateSettings is null)
        {
            await ctx.Interaction.SendErrorAsync(
                Strings.RoleStatesNotEnabledSlash(ctx.Guild.Id, bss.Data.ErrorEmote), Config);
            return;
        }

        if (await Service.ToggleSkipAutoAssignRoles(roleStateSettings))
            await ctx.Interaction.SendConfirmAsync(
                $"{bss.Data.SuccessEmote} {Strings.RoleStatesWillSkipAutoAssign(ctx.Guild.Id)}");
        else
            await ctx.Interaction.SendConfirmAsync(
                $"{bss.Data.SuccessEmote} {Strings.RoleStatesWillNotSkipAutoAssign(ctx.Guild.Id)}");
    }

    /// <summary>
    ///     Adds specified roles to a user's saved RoleState, allowing those roles to be automatically reapplied if the user
    ///     rejoins the guild.
    /// </summary>
    /// <remarks>
    ///     This command can be used to manually adjust the roles that should be restored to a user upon rejoining.
    ///     It is useful for correcting or updating the roles that are saved in the user's RoleState.
    ///     This command requires administrator permissions to use.
    /// </remarks>
    [SlashCommand("viewsettings", "View the current settings for RoleStates")]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task ViewRoleStatesSettings()
    {
        var roleStateSettings = await Service.GetRoleStateSettings(ctx.Guild.Id);
        if (roleStateSettings is null)
            await ctx.Interaction.SendErrorAsync(
                Strings.RoleStatesNotEnabledSlash(ctx.Guild.Id, bss.Data.ErrorEmote), Config);
        else
        {
            var deniedUsers = string.IsNullOrWhiteSpace(roleStateSettings.DeniedUsers)
                ? []
                : roleStateSettings.DeniedUsers.Split(',').Select(ulong.Parse).ToList();

            var deniedRoles = string.IsNullOrWhiteSpace(roleStateSettings.DeniedRoles)
                ? []
                : roleStateSettings.DeniedRoles.Split(',').Select(ulong.Parse).ToList();


            var eb = new EmbedBuilder()
                .WithTitle(Strings.RoleStatesSettingsTitle(ctx.Guild.Id))
                .WithOkColor()
                .WithDescription(Strings.RoleStatesConfigEnabled(ctx.Guild.Id, roleStateSettings.Enabled) +
                                 $"`Clear on ban:` {roleStateSettings.ClearOnBan}\n" +
                                 $"`Ignore bots:` {roleStateSettings.IgnoreBots}\n" +
                                 $"`Skip auto-assign roles:` {roleStateSettings.SkipAutoAssignRoles}\n" +
                                 $"`Denied roles:` {(deniedRoles.Any() ? string.Join("|", deniedRoles.Select(x => $"<@&{x}>")) : "None")}\n" +
                                 $"`Denied users:` {(deniedUsers.Any() ? string.Join("|", deniedUsers.Select(x => $"<@{x}>")) : "None")}\n");
            await ctx.Interaction.RespondAsync(embed: eb.Build());
        }
    }

    /// <summary>
    ///     Lists all users in the server with saved RoleStates, showing the roles that will be automatically reapplied upon
    ///     rejoin.
    /// </summary>
    /// <remarks>
    ///     Use this command to audit the saved role states across all users, ensuring accuracy and relevancy of roles
    ///     that are set to be restored.
    /// </remarks>
    [SlashCommand("viewstates", "Let's you view all active RoleStates for every user!")]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task ViewUserRoleStates()
    {
        var userRoleStates = await Service.GetAllUserRoleStates(ctx.Guild.Id);

        if (!userRoleStates.Any())
        {
            await ctx.Interaction.SendErrorAsync(Strings.NoUserRoleStatesSaved(ctx.Guild.Id, bss.Data.ErrorEmote),
                Config);
        }
        else
        {
            var paginator = new LazyPaginatorBuilder()
                .AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex((userRoleStates.Count - 1) / 3)
                .WithDefaultEmotes()
                .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                .Build();

            await interactivity.SendPaginatorAsync(paginator, ctx.Interaction, TimeSpan.FromMinutes(60))
                .ConfigureAwait(false);

            async Task<PageBuilder> PageFactory(int page)
            {
                await Task.CompletedTask.ConfigureAwait(false);

                var eb = new PageBuilder()
                    .WithTitle(Strings.UserRoleStates(ctx.Guild.Id))
                    .WithOkColor();

                var roleStatesToShow = userRoleStates.Skip(5 * page).Take(3).ToList();

                foreach (var userRoleState in roleStatesToShow)
                {
                    var savedRoles = string.IsNullOrWhiteSpace(userRoleState.SavedRoles)
                        ? []
                        : userRoleState.SavedRoles.Split(',').Select(ulong.Parse).ToList();

                    eb.AddField($"{userRoleState.UserName} ({userRoleState.UserId})",
                        $"`Saved Roles:` {(savedRoles.Any() ? string.Join("|", savedRoles.Select(x => $"<@&{x}>")) : "None")}\n");
                }

                return eb;
            }
        }
    }

    /// <summary>
    ///     Adds specific roles to the list of roles that should not be saved or restored by RoleStates.
    /// </summary>
    /// <remarks>
    ///     This command helps refine RoleStates functionality by excluding roles that should not be automatically managed,
    ///     such as temporary or event-specific roles.
    /// </remarks>
    [SlashCommand("add-deny-roles", "Adds one or more roles to the exluded roles list")]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task RoleStatesAddDenyRole(IRole[] roles)
    {
        var roleStateSettings = await Service.GetRoleStateSettings(ctx.Guild.Id);

        if (roleStateSettings is null)
        {
            await ctx.Interaction.SendErrorAsync(
                Strings.RoleStatesNotEnabledSlash(ctx.Guild.Id, bss.Data.ErrorEmote), Config);
            return;
        }

        var deniedRoles = string.IsNullOrWhiteSpace(roleStateSettings.DeniedRoles)
            ? []
            : roleStateSettings.DeniedRoles.Split(',').Select(ulong.Parse).ToList();

        var addedCount = 0;

        foreach (var role in roles)
        {
            if (deniedRoles.Contains(role.Id)) continue;
            deniedRoles.Add(role.Id);
            addedCount++;
        }

        roleStateSettings.DeniedRoles = string.Join(",", deniedRoles);
        await Service.UpdateRoleStateSettings(roleStateSettings);

        await ctx.Interaction.SendConfirmAsync(
            Strings.RoleStatesRolesAddedToDeny(ctx.Guild.Id, bss.Data.SuccessEmote, addedCount));
    }

    /// <summary>
    ///     Removes specific roles from the exclusion list, allowing them to be saved and restored by RoleStates.
    /// </summary>
    /// <remarks>
    ///     If a role no longer needs to be excluded from RoleStates, use this command to ensure it is again eligible
    ///     for automatic management.
    /// </remarks>
    [SlashCommand("remove-deny-roles", "Removes one or more roles from the exclusion list")]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task RoleStatesRemoveDenyRole(IRole[] roles)
    {
        var roleStateSettings = await Service.GetRoleStateSettings(ctx.Guild.Id);

        if (roleStateSettings is null)
        {
            await ctx.Interaction.SendErrorAsync(
                Strings.RoleStatesNotEnabledSlash(ctx.Guild.Id, bss.Data.ErrorEmote), Config);
            return;
        }

        var deniedRoles = string.IsNullOrWhiteSpace(roleStateSettings.DeniedRoles)
            ? []
            : roleStateSettings.DeniedRoles.Split(',').Select(ulong.Parse).ToList();

        var removedCount = 0;

        foreach (var role in roles)
        {
            if (!deniedRoles.Contains(role.Id)) continue;
            deniedRoles.Remove(role.Id);
            removedCount++;
        }

        roleStateSettings.DeniedRoles = string.Join(",", deniedRoles);
        await Service.UpdateRoleStateSettings(roleStateSettings);

        await ctx.Interaction.SendConfirmAsync(
            Strings.RoleStatesRolesRemovedFromDeny(ctx.Guild.Id, bss.Data.SuccessEmote, removedCount));
    }

    /// <summary>
    ///     Excludes specific users from having their roles saved or restored by RoleStates.
    /// </summary>
    /// <remarks>
    ///     This command is useful for excluding staff, bots, or other special accounts from automatic role management,
    ///     allowing for more granular control over role assignments.
    /// </remarks>
    [SlashCommand("add-deny-users", "Adds one or more users to the excluded users list")]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task RoleStatesAddDenyUser(IUser[] users)
    {
        var roleStateSettings = await Service.GetRoleStateSettings(ctx.Guild.Id);

        if (roleStateSettings is null)
        {
            await ctx.Interaction.SendErrorAsync(
                Strings.RoleStatesNotEnabledSlash(ctx.Guild.Id, bss.Data.ErrorEmote), Config);
            return;
        }

        var deniedUsers = string.IsNullOrWhiteSpace(roleStateSettings.DeniedUsers)
            ? []
            : roleStateSettings.DeniedUsers.Split(',').Select(ulong.Parse).ToList();

        var addedCount = 0;

        foreach (var user in users)
        {
            if (deniedUsers.Contains(user.Id)) continue;
            deniedUsers.Add(user.Id);
            addedCount++;
        }

        roleStateSettings.DeniedUsers = string.Join(",", deniedUsers);
        await Service.UpdateRoleStateSettings(roleStateSettings);

        await ctx.Interaction.SendConfirmAsync(
            Strings.RoleStatesUsersAddedToDeny(ctx.Guild.Id, bss.Data.SuccessEmote, addedCount));
    }

    /// <summary>
    ///     Reincludes specific users, allowing their roles to be managed by RoleStates once again.
    /// </summary>
    /// <remarks>
    ///     If a user no longer needs to be excluded from RoleStates, this command ensures their roles will be saved
    ///     and restored upon leaving and rejoining the server.
    /// </remarks>
    [SlashCommand("remove-deny-users", "Removes one or more users from the users blacklist")]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task RoleStatesRemoveDenyUser(IUser[] users)
    {
        var roleStateSettings = await Service.GetRoleStateSettings(ctx.Guild.Id);

        if (roleStateSettings is null)
        {
            await ctx.Interaction.SendErrorAsync(
                Strings.RoleStatesNotEnabledSlash(ctx.Guild.Id, bss.Data.ErrorEmote), Config);
            return;
        }

        var deniedUsers = string.IsNullOrWhiteSpace(roleStateSettings.DeniedUsers)
            ? []
            : roleStateSettings.DeniedUsers.Split(',').Select(ulong.Parse).ToList();

        var removedCount = 0;

        foreach (var user in users)
        {
            if (!deniedUsers.Contains(user.Id)) continue;
            deniedUsers.Remove(user.Id);
            removedCount++;
        }

        roleStateSettings.DeniedUsers = string.Join(",", deniedUsers);
        await Service.UpdateRoleStateSettings(roleStateSettings);

        await ctx.Interaction.SendConfirmAsync(
            Strings.RoleStatesUsersRemovedFromDeny(ctx.Guild.Id, bss.Data.SuccessEmote, removedCount));
    }

    /// <summary>
    ///     Manually sets a user's RoleState, specifying which roles should be saved and potentially restored.
    /// </summary>
    /// <remarks>
    ///     This command allows administrators to manually adjust the roles included in a user's RoleState,
    ///     overriding the automatic detection and saving process.
    /// </remarks>
    [SlashCommand("set-role-state", "Sets a users role state")]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task SetUserRoleState(IUser user, IRole[] roles)
    {
        var roleIds = roles.Where(x => x.Id != ctx.Guild.Id && !x.IsManaged).Select(x => x.Id);
        if (!roleIds.Any())
            await ctx.Interaction.SendErrorAsync(Strings.RoleStatesNoValidRoles(ctx.Guild.Id, bss.Data.ErrorEmote),
                Config);
        await Service.SetRoleStateManually(user, ctx.Guild.Id, roleIds);
        await ctx.Interaction.SendConfirmAsync(
            $"{bss.Data.SuccessEmote} {Strings.RoleStatesSetSuccess(ctx.Guild.Id, user.Mention)}");
    }

    /// <summary>
    ///     Removes specific roles from a user's saved RoleState, preventing those roles from being automatically reapplied.
    /// </summary>
    /// <remarks>
    ///     Use this command to fine-tune the roles included in a user's RoleState, ensuring only relevant roles are restored.
    /// </remarks>
    [SlashCommand("remove-from-rolestate", "Removes a role from a RoleState")]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task RemoveRolesFromRoleState(IUser user, IRole[] roles)
    {
        var removed = await Service.RemoveRolesFromUserRoleState(ctx.Guild.Id, user.Id, roles.Select(x => x.Id));
        if (!removed.Item1)
            await ctx.Interaction.SendErrorAsync(
                Strings.RoleStateRemoveFailed(ctx.Guild.Id, bss.Data.ErrorEmote, removed.Item2),
                Config);
        else
            await ctx.Interaction.SendConfirmAsync(
                Strings.RoleStateRemovedSuccess(ctx.Guild.Id, bss.Data.SuccessEmote, user));
    }

    /// <summary>
    ///     Adds specific roles to a user's saved RoleState, including them in the set of roles to be automatically reapplied.
    /// </summary>
    /// <remarks>
    ///     This command complements the role removal command, allowing administrators to ensure a user's RoleState
    ///     accurately reflects the roles they should receive upon rejoining the server.
    /// </remarks>
    [SlashCommand("add-to-rolestate", "Adds a role to a RoleState")]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task AddRolesToRoleState(IUser user, IRole[] roles)
    {
        var removed = await Service.AddRolesToUserRoleState(ctx.Guild.Id, user.Id, roles.Select(x => x.Id));
        if (!removed.Item1)
            await ctx.Interaction.SendErrorAsync(
                Strings.RoleStateRemoveFailed(ctx.Guild.Id, bss.Data.ErrorEmote, removed.Item2),
                Config);
        else
            await ctx.Interaction.SendConfirmAsync(
                Strings.RoleStateRemovedSuccess(ctx.Guild.Id, bss.Data.SuccessEmote, user));
    }

    /// <summary>
    ///     Deletes a user's RoleState, removing all saved roles and preventing any from being automatically reapplied upon
    ///     rejoin.
    /// </summary>
    /// <remarks>
    ///     This command is used to clear a user's RoleState entirely, useful in situations where a clean slate is desired,
    ///     or the previously saved roles are no longer applicable.
    /// </remarks>
    [SlashCommand("delete-role-state", "Deletes a users RoleState")]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task DeleteUserRoleState(IUser user)
    {
        var deleted = await Service.DeleteUserRoleState(user.Id, ctx.Guild.Id);
        if (!deleted)
            await ctx.Interaction.SendErrorAsync(Strings.NoRoleStateToDelete(ctx.Guild.Id, bss.Data.ErrorEmote),
                Config);
        else
            await ctx.Interaction.SendConfirmAsync(
                Strings.RoleStateDeletedSuccess(ctx.Guild.Id, bss.Data.SuccessEmote, user));
    }
}