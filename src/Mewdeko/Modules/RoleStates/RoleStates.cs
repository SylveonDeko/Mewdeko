using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.RoleStates.Services;
using Mewdeko.Services.Settings;

namespace Mewdeko.Modules.RoleStates;

/// <summary>
/// </summary>
/// <param name="bss">The BotConfigService instance.</param>
/// <param name="interactivity">The InteractiveService instance.</param>
/// <param name="client">The Discord client instance.</param>
public class RoleStates(BotConfigService bss, InteractiveService interactivity, DiscordShardedClient client)
    : MewdekoModuleBase<RoleStatesService>
{
    /// <summary>
    ///     Toggles the role states feature on or off.
    /// </summary>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public async Task ToggleRoleStates()
    {
        if (await Service.ToggleRoleStates(ctx.Guild.Id))
            await ctx.Channel.SendConfirmAsync(Strings.RoleStatesEnabled(ctx.Guild.Id, bss.Data.SuccessEmote));
        else
            await ctx.Channel.SendConfirmAsync(Strings.RoleStatesDisabled(ctx.Guild.Id, bss.Data.SuccessEmote));
    }

    /// <summary>
    ///     Saves role states for all users in the server.
    /// </summary>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public async Task SaveAllRoleStates()
    {
        var roleStateSettings = await Service.GetRoleStateSettings(ctx.Guild.Id);
        if (roleStateSettings is null || !roleStateSettings.Enabled)
        {
            await ctx.Channel.SendErrorAsync(
                Strings.RoleStatesNotEnabled(ctx.Guild.Id, bss.Data.ErrorEmote), Config);
            return;
        }

        await ctx.Channel.SendConfirmAsync(
            $"{bss.Data.LoadingEmote} {Strings.SavingAllRoleStates(ctx.Guild.Id)}");

        var result = await Service.SaveAllUserRoleStates(ctx.Guild);

        if (result.SavedCount > 0)
            await ctx.Channel.SendConfirmAsync(
                $"{bss.Data.SuccessEmote} {Strings.RoleStatesSaved(ctx.Guild.Id, result.SavedCount)}");
        else
            await ctx.Channel.SendErrorAsync(
                $"{bss.Data.ErrorEmote} {Strings.NoRoleStatesSaved(ctx.Guild.Id)}", Config);
    }

    /// <summary>
    ///     Transfers role states from the current server to another server.
    /// </summary>
    /// <param name="serverId">The ID of the target server.</param>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public async Task TransferRoleStates(ulong serverId)
    {
        var targetGuild = client.GetGuild(serverId);
        if (targetGuild == null)
        {
            await ReplyErrorAsync(Strings.NotInServer(serverId));
            return;
        }

        var roleStateSettings = await Service.GetRoleStateSettings(ctx.Guild.Id);
        if (roleStateSettings is null || !roleStateSettings.Enabled)
        {
            await ctx.Channel.SendErrorAsync(
                Strings.RoleStatesNotEnabled(ctx.Guild.Id, bss.Data.ErrorEmote), Config);
            return;
        }

        await ctx.Channel.SendConfirmAsync(
            $"{bss.Data.LoadingEmote} {Strings.TransferringRoleStates(ctx.Guild.Id, targetGuild.Name)}");

        var result = await Service.TransferRoleStates(ctx.Guild, targetGuild);

        if (result.TransferCount > 0)
            await ctx.Channel.SendConfirmAsync(
                $"{bss.Data.SuccessEmote} {Strings.RoleStatesTransferred(ctx.Guild.Id, result.TransferCount, targetGuild.Name)}");
        else
            await ctx.Channel.SendErrorAsync(
                $"{bss.Data.ErrorEmote} {Strings.RoleStatesTransferFailed(ctx.Guild.Id)}\n{result.ErrorMessage}",
                Config);
    }

    /// <summary>
    ///     Toggles whether bots should be ignored by the role states feature.
    /// </summary>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public async Task ToggleRoleStatesIgnoreBots()
    {
        var roleStateSettings = await Service.GetRoleStateSettings(ctx.Guild.Id);
        if (roleStateSettings is null)
        {
            await ctx.Channel.SendErrorAsync(
                Strings.RoleStatesNotEnabled(ctx.Guild.Id, bss.Data.ErrorEmote), Config);
            return;
        }

        if (await Service.ToggleIgnoreBots(roleStateSettings))
            await ctx.Channel.SendConfirmAsync(Strings.RoleStatesIgnoreBots(ctx.Guild.Id, bss.Data.SuccessEmote));
        else
            await ctx.Channel.SendConfirmAsync(Strings.RoleStatesNotIgnoreBots(ctx.Guild.Id, bss.Data.SuccessEmote));
    }


    /// <summary>
    ///     Toggles whether role states should be cleared when a user is banned.
    /// </summary>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public async Task ToggleRoleStatesClearOnBan()
    {
        var roleStateSettings = await Service.GetRoleStateSettings(ctx.Guild.Id);
        if (roleStateSettings is null)
        {
            await ctx.Channel.SendErrorAsync(
                Strings.RoleStatesNotEnabled(ctx.Guild.Id, bss.Data.ErrorEmote), Config);
            return;
        }

        if (await Service.ToggleClearOnBan(roleStateSettings))
            await ctx.Channel.SendConfirmAsync(
                $"{bss.Data.SuccessEmote} {Strings.RoleStatesWillClearOnBan(ctx.Guild.Id)}");
        else
            await ctx.Channel.SendConfirmAsync(
                $"{bss.Data.SuccessEmote} {Strings.RoleStatesWillNotClearOnBan(ctx.Guild.Id)}");
    }

    /// <summary>
    ///     Toggles whether auto-assign roles should be skipped for users with saved role states.
    /// </summary>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public async Task ToggleRoleStatesSkipAutoAssign()
    {
        var roleStateSettings = await Service.GetRoleStateSettings(ctx.Guild.Id);
        if (roleStateSettings is null)
        {
            await ctx.Channel.SendErrorAsync(
                Strings.RoleStatesNotEnabled(ctx.Guild.Id, bss.Data.ErrorEmote), Config);
            return;
        }

        if (await Service.ToggleSkipAutoAssignRoles(roleStateSettings))
            await ctx.Channel.SendConfirmAsync(
                $"{bss.Data.SuccessEmote} {Strings.RoleStatesWillSkipAutoAssign(ctx.Guild.Id)}");
        else
            await ctx.Channel.SendConfirmAsync(
                $"{bss.Data.SuccessEmote} {Strings.RoleStatesWillNotSkipAutoAssign(ctx.Guild.Id)}");
    }

    /// <summary>
    ///     Displays the current settings for the role states feature.
    /// </summary>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public async Task ViewRoleStatesSettings()
    {
        var roleStateSettings = await Service.GetRoleStateSettings(ctx.Guild.Id);
        if (roleStateSettings is null)
            await ctx.Channel.SendErrorAsync(
                Strings.RoleStatesNotEnabled(ctx.Guild.Id, bss.Data.ErrorEmote), Config);
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
                .WithDescription(Strings.RoleStatesConfigEnabled(ctx.Guild.Id, roleStateSettings.Enabled) + "\n" +
                                 $"`Clear on ban:` {roleStateSettings.ClearOnBan}\n" +
                                 $"`Ignore bots:` {roleStateSettings.IgnoreBots}\n" +
                                 $"`Skip auto-assign roles:` {roleStateSettings.SkipAutoAssignRoles}\n" +
                                 $"`Denied roles:` {(deniedRoles.Any() ? string.Join("|", deniedRoles.Select(x => $"<@&{x}>")) : "None")}\n" +
                                 $"`Denied users:` {(deniedUsers.Any() ? string.Join("|", deniedUsers.Select(x => $"<@{x}>")) : "None")}\n");
            await ctx.Channel.SendMessageAsync(embed: eb.Build());
        }
    }

    /// <summary>
    ///     Displays the role states for all users.
    /// </summary>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public async Task ViewUserRoleStates()
    {
        var userRoleStates = await Service.GetAllUserRoleStates(ctx.Guild.Id);

        if (!userRoleStates.Any())
        {
            await ctx.Channel.SendErrorAsync($"{bss.Data.ErrorEmote} {Strings.RoleStatesNoSaved(ctx.Guild.Id)}",
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

            await interactivity.SendPaginatorAsync(paginator, ctx.Channel, TimeSpan.FromMinutes(60))
                .ConfigureAwait(false);

            async Task<PageBuilder> PageFactory(int page)
            {
                await Task.CompletedTask.ConfigureAwait(false);

                var eb = new PageBuilder()
                    .WithTitle(Strings.RoleStatesUserTitle(ctx.Guild.Id))
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
    ///     Deletes the role state for a specific user.
    /// </summary>
    /// <param name="user">The user whose role state should be deleted.</param>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public async Task DeleteUserRoleState(IGuildUser user)
    {
        var userRoleStates = await Service.DeleteUserRoleState(ctx.Guild.Id, user.Id);
        if (!userRoleStates)
            await ctx.Channel.SendErrorAsync(
                $"{bss.Data.ErrorEmote} {Strings.RoleStatesNoStateForUser(ctx.Guild.Id, user)}!", Config);
        else
        {
            await ctx.Channel.SendConfirmAsync(
                $"{bss.Data.SuccessEmote} {Strings.RoleStatesUserDeleted(ctx.Guild.Id, user)}");
        }
    }

    /// <summary>
    ///     Adds roles to the deny list for the role states feature.
    /// </summary>
    /// <param name="roles">The roles to be added to the deny list.</param>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public async Task RoleStatesAddDenyRole(params IRole[] roles)
    {
        var roleStateSettings = await Service.GetRoleStateSettings(ctx.Guild.Id);

        if (roleStateSettings is null)
        {
            await ctx.Channel.SendErrorAsync(
                Strings.RoleStatesNotEnabled(ctx.Guild.Id, bss.Data.ErrorEmote), Config);
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

        await ctx.Channel.SendConfirmAsync(
            Strings.RoleStatesRolesAddedToDeny(ctx.Guild.Id, bss.Data.SuccessEmote, addedCount));
    }

    /// <summary>
    ///     Removes roles from the deny list for the role states feature.
    /// </summary>
    /// <param name="roles">The roles to be removed from the deny list.</param>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public async Task RoleStatesRemoveDenyRole(params IRole[] roles)
    {
        var roleStateSettings = await Service.GetRoleStateSettings(ctx.Guild.Id);

        if (roleStateSettings is null)
        {
            await ctx.Channel.SendErrorAsync(
                Strings.RoleStatesNotEnabled(ctx.Guild.Id, bss.Data.ErrorEmote), Config);
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

        await ctx.Channel.SendConfirmAsync(
            Strings.RoleStatesRolesRemovedFromDeny(ctx.Guild.Id, bss.Data.SuccessEmote, removedCount));
    }

    /// <summary>
    ///     Adds users to the deny list for the role states feature.
    /// </summary>
    /// <param name="users">The users to be added to the deny list.</param>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public async Task RoleStatesAddDenyUser(params IGuildUser[] users)
    {
        var roleStateSettings = await Service.GetRoleStateSettings(ctx.Guild.Id);

        if (roleStateSettings is null)
        {
            await ctx.Channel.SendErrorAsync(
                Strings.RoleStatesNotEnabled(ctx.Guild.Id, bss.Data.ErrorEmote), Config);
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

        await ctx.Channel.SendConfirmAsync(
            Strings.RoleStatesUsersAddedToDeny(ctx.Guild.Id, bss.Data.SuccessEmote, addedCount));
    }

    /// <summary>
    ///     Removes users from the deny list for the role states feature.
    /// </summary>
    /// <param name="users">The users to be removed from the deny list.</param>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public async Task RoleStatesRemoveDenyUser(params IGuildUser[] users)
    {
        var roleStateSettings = await Service.GetRoleStateSettings(ctx.Guild.Id);

        if (roleStateSettings is null)
        {
            await ctx.Channel.SendErrorAsync(
                Strings.RoleStatesNotEnabled(ctx.Guild.Id, bss.Data.ErrorEmote), Config);
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

        await ctx.Channel.SendConfirmAsync(
            Strings.RoleStatesUsersRemovedFromDeny(ctx.Guild.Id, bss.Data.SuccessEmote, removedCount));
    }

    /// <summary>
    ///     Sets the role state for a specific user.
    /// </summary>
    /// <param name="user">The user whose role state should be set.</param>
    /// <param name="roles">The roles to be included in the user's role state.</param>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public async Task SetUserRoleState(IGuildUser user, params IRole[] roles)
    {
        var roleIds = roles.Where(x => x.Id != ctx.Guild.Id && !x.IsManaged).Select(x => x.Id);
        if (!roleIds.Any())
            await ctx.Channel.SendErrorAsync(Strings.RoleStatesNoValidRoles(ctx.Guild.Id, bss.Data.ErrorEmote),
                Config);
        await Service.SetRoleStateManually(user, ctx.Guild.Id, roleIds);
        await ctx.Channel.SendConfirmAsync(
            $"{bss.Data.SuccessEmote} {Strings.RoleStatesSetSuccess(ctx.Guild.Id, user.Mention)}");
    }

    /// <summary>
    ///     Removes roles from a user's role state.
    /// </summary>
    /// <param name="user">The user whose role state should be modified.</param>
    /// <param name="roles">The roles to be removed from the user's role state.</param>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public async Task RemoveRolesFromRoleState(IUser user, params IRole[] roles)
    {
        var removed = await Service.RemoveRolesFromUserRoleState(ctx.Guild.Id, user.Id, roles.Select(x => x.Id));
        if (!removed.Item1)
            await ctx.Channel.SendErrorAsync(
                $"{bss.Data.ErrorEmote} {Strings.RoleStatesRemoveFailed(ctx.Guild.Id, removed.Item2)}", Config);
        else
            await ctx.Channel.SendConfirmAsync(
                $"{bss.Data.SuccessEmote} {Strings.RoleStatesRolesRemovedSuccess(ctx.Guild.Id, user)}");
    }

    /// <summary>
    ///     Adds roles to a user's role state.
    /// </summary>
    /// <param name="user">The user whose role state should be modified.</param>
    /// <param name="roles">The roles to be added to the user's role state.</param>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public async Task AddRolesToRoleState(IUser user, params IRole[] roles)
    {
        var removed = await Service.AddRolesToUserRoleState(ctx.Guild.Id, user.Id, roles.Select(x => x.Id));
        if (!removed.Item1)
            await ctx.Channel.SendErrorAsync(
                $"{bss.Data.ErrorEmote} {Strings.RoleStatesRemoveFailed(ctx.Guild.Id, removed.Item2)}", Config);
        else
            await ctx.Channel.SendConfirmAsync(
                $"{bss.Data.SuccessEmote} {Strings.RoleStatesRolesRemovedSuccess(ctx.Guild.Id, user)}");
    }

    /// <summary>
    ///     Deletes the role state for a specific user.
    /// </summary>
    /// <param name="user">The user whose role state should be deleted.</param>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public async Task DeleteUserRoleState(IUser user)
    {
        var deleted = await Service.DeleteUserRoleState(user.Id, ctx.Guild.Id);
        if (!deleted)
            await ctx.Channel.SendErrorAsync($"{bss.Data.ErrorEmote} {Strings.RoleStatesNoStateToDelete(ctx.Guild.Id)}",
                Config);
        else
            await ctx.Channel.SendConfirmAsync(
                Strings.RoleStatesDeletedSuccessFinal(ctx.Guild.Id, bss.Data.SuccessEmote, user));
    }
}