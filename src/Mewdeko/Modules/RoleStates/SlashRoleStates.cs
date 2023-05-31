using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.RoleStates.Services;
using Mewdeko.Services.Settings;

namespace Mewdeko.Modules.RoleStates;
[Group("rolestates", "Manage roles for users when they leave and rejoin!")]
public class SlashRoleStates : MewdekoSlashModuleBase<RoleStatesService>
{

    private readonly BotConfigService bss;
    private readonly InteractiveService interactivity;

    public SlashRoleStates(BotConfigService bss, InteractiveService interactivity)
    {
        this.bss = bss;
        this.interactivity = interactivity;
    }

    [SlashCommand("toggle","Toggle whether RoleStates are enabled"), SlashUserPerm(GuildPermission.Administrator)]
    public async Task ToggleRoleStates()
    {
        if (await Service.ToggleRoleStates(ctx.Guild.Id))
            await ctx.Interaction.SendConfirmAsync($"{bss.Data.SuccessEmote} Role States are now enabled!");
        else
            await ctx.Interaction.SendConfirmAsync($"{bss.Data.SuccessEmote} Role States are now disabled!");
    }

    [SlashCommand("toggle-ignore-bots","Toggle whether Role States will ignore bots"), SlashUserPerm(GuildPermission.Administrator)]
    public async Task ToggleRoleStatesIgnoreBots()
    {
        var roleStateSettings = await Service.GetRoleStateSettings(ctx.Guild.Id);
        if (roleStateSettings is null)
        {
            await ctx.Interaction.SendErrorAsync($"{bss.Data.ErrorEmote} Role States are not enabled and have not been configured!");
            return;
        }
        if (await Service.ToggleIgnoreBots(roleStateSettings))
            await ctx.Interaction.SendConfirmAsync($"{bss.Data.SuccessEmote} Role States will ignore bots!");
        else
            await ctx.Interaction.SendConfirmAsync($"{bss.Data.SuccessEmote} Role States will not ignore bots!");
    }

    [SlashCommand("toggle-clear-on-ban","Toggle whether role states get cleared on ban"), SlashUserPerm(GuildPermission.Administrator)]
    public async Task ToggleRoleStatesClearOnBan()
    {
        var roleStateSettings = await Service.GetRoleStateSettings(ctx.Guild.Id);
        if (roleStateSettings is null)
        {
            await ctx.Interaction.SendErrorAsync($"{bss.Data.ErrorEmote} Role States are not enabled and have not been configured!");
            return;
        }
        if (await Service.ToggleClearOnBan(roleStateSettings))
            await ctx.Interaction.SendConfirmAsync($"{bss.Data.SuccessEmote} Role states will clear on ban!");
        else
            await ctx.Interaction.SendConfirmAsync($"{bss.Data.SuccessEmote} Role states will not clear on ban!");
    }


    [SlashCommand("viewsettings","View the current settings for RoleStates"), SlashUserPerm(GuildPermission.Administrator)]
    public async Task ViewRoleStatesSettings()
    {
        var roleStateSettings = await Service.GetRoleStateSettings(ctx.Guild.Id);
        if (roleStateSettings is null)
            await ctx.Interaction.SendErrorAsync($"{bss.Data.ErrorEmote} Role States are not enabled and have not been configured!");
        else
        {
            var deniedUsers = string.IsNullOrWhiteSpace(roleStateSettings.DeniedUsers)
                ? new List<ulong>()
                : roleStateSettings.DeniedUsers.Split(',').Select(ulong.Parse).ToList();

            var deniedRoles = string.IsNullOrWhiteSpace(roleStateSettings.DeniedRoles)
                ? new List<ulong>()
                : roleStateSettings.DeniedRoles.Split(',').Select(ulong.Parse).ToList();


            var eb = new EmbedBuilder()
                .WithTitle("Role States Settings")
                .WithOkColor()
                .WithDescription($"`Enabled:` {roleStateSettings.Enabled}\n" +
                                 $"`Clear on ban:` {roleStateSettings.ClearOnBan}\n" +
                                 $"`Ignore bots:` {roleStateSettings.IgnoreBots}\n" +
                                 $"`Denied roles:` {(deniedRoles.Any() ? string.Join("|", deniedRoles.Select(x => $"<@&{x}>")) : "None")}\n" +
                                 $"`Denied users:` {(deniedUsers.Any() ? string.Join("|", deniedUsers.Select(x => $"<@{x}>")) : "None")}\n");
            await ctx.Interaction.RespondAsync(embed: eb.Build());
        }
    }

    [SlashCommand("viewstates", "Let's you view all active RoleStates for every user!"), SlashUserPerm(GuildPermission.Administrator)]
    public async Task ViewUserRoleStates()
    {
        var userRoleStates = await Service.GetAllUserRoleStates(ctx.Guild.Id);

        if (!userRoleStates.Any())
        {
            await ctx.Interaction.SendErrorAsync($"{bss.Data.ErrorEmote} No user role states have been saved!");
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

            await interactivity.SendPaginatorAsync(paginator, ctx.Interaction, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

            async Task<PageBuilder> PageFactory(int page)
            {
                await Task.CompletedTask.ConfigureAwait(false);

                var eb = new PageBuilder()
                    .WithTitle($"User Role States")
                    .WithOkColor();

                var roleStatesToShow = userRoleStates.Skip(5 * page).Take(3).ToList();

                foreach (var userRoleState in roleStatesToShow)
                {
                    var savedRoles = string.IsNullOrWhiteSpace(userRoleState.SavedRoles)
                        ? new List<ulong>()
                        : userRoleState.SavedRoles.Split(',').Select(ulong.Parse).ToList();

                    eb.AddField($"{userRoleState.UserName} ({userRoleState.UserId})",
                        $"`Saved Roles:` {(savedRoles.Any() ? string.Join("|", savedRoles.Select(x => $"<@&{x}>")) : "None")}\n");
                }

                return eb;
            }
        }
    }

    [SlashCommand("add-deny-roles","Adds one or more roles to the exluded roles list"), SlashUserPerm(GuildPermission.Administrator)]
    public async Task RoleStatesAddDenyRole(IRole[] roles)
    {
        var roleStateSettings = await Service.GetRoleStateSettings(ctx.Guild.Id);

        if (roleStateSettings is null)
        {
            await ctx.Interaction.SendErrorAsync($"{bss.Data.ErrorEmote} Role States are not enabled and have not been configured!");
            return;
        }

        var deniedRoles = string.IsNullOrWhiteSpace(roleStateSettings.DeniedRoles)
            ? new List<ulong>()
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

        await ctx.Interaction.SendConfirmAsync($"{bss.Data.SuccessEmote} Successfully added {addedCount} role(s) to the deny list.");
    }

    [SlashCommand("remove-deny-roles","Removes one or more roles from the exclusion list"), SlashUserPerm(GuildPermission.Administrator)]
    public async Task RoleStatesRemoveDenyRole(IRole[] roles)
    {
        var roleStateSettings = await Service.GetRoleStateSettings(ctx.Guild.Id);

        if (roleStateSettings is null)
        {
            await ctx.Interaction.SendErrorAsync($"{bss.Data.ErrorEmote} Role States are not enabled and have not been configured!");
            return;
        }

        var deniedRoles = string.IsNullOrWhiteSpace(roleStateSettings.DeniedRoles)
            ? new List<ulong>()
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

        await ctx.Interaction.SendConfirmAsync($"{bss.Data.SuccessEmote} Successfully removed {removedCount} role(s) from the deny list.");
    }

    [SlashCommand("add-deny-users","Adds one or more users to the excluded users list"), SlashUserPerm(GuildPermission.Administrator)]
    public async Task RoleStatesAddDenyUser(IUser[] users)
    {
        var roleStateSettings = await Service.GetRoleStateSettings(ctx.Guild.Id);

        if (roleStateSettings is null)
        {
            await ctx.Interaction.SendErrorAsync($"{bss.Data.ErrorEmote} Role States are not enabled and have not been configured!");
            return;
        }

        var deniedUsers = string.IsNullOrWhiteSpace(roleStateSettings.DeniedUsers)
            ? new List<ulong>()
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

        await ctx.Interaction.SendConfirmAsync($"{bss.Data.SuccessEmote} Successfully added {addedCount} user(s) to the deny list.");
    }

    [SlashCommand("remove-deny-users","Removes one or more users from the users blacklist"), SlashUserPerm(GuildPermission.Administrator)]
    public async Task RoleStatesRemoveDenyUser(IUser[] users)
    {
        var roleStateSettings = await Service.GetRoleStateSettings(ctx.Guild.Id);

        if (roleStateSettings is null)
        {
            await ctx.Interaction.SendErrorAsync($"{bss.Data.ErrorEmote} Role States are not enabled and have not been configured!");
            return;
        }

        var deniedUsers = string.IsNullOrWhiteSpace(roleStateSettings.DeniedUsers)
            ? new List<ulong>()
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

        await ctx.Interaction.SendConfirmAsync($"{bss.Data.SuccessEmote} Successfully removed {removedCount} user(s) from the deny list.");
    }

    [SlashCommand("set-role-state","Sets a users role state"), SlashUserPerm(GuildPermission.Administrator)]
    public async Task SetUserRoleState(IUser user, IRole[] roles)
    {
        var roleIds = roles.Where(x => x.Id != ctx.Guild.Id && !x.IsManaged).Select(x => x.Id);
        if (!roleIds.Any())
            await ctx.Interaction.SendErrorAsync($"{bss.Data.ErrorEmote} There are no valid roles specified!");
        await Service.SetRoleStateManually(user, ctx.Guild.Id, roleIds);
        await ctx.Interaction.SendConfirmAsync($"{bss.Data.SuccessEmote} Successfully set the role state for user {user.Mention} with the specified roles.");
    }

    [SlashCommand("remove-from-rolestate","Removes a role from a RoleState"), SlashUserPerm(GuildPermission.Administrator)]
    public async Task RemoveRolesFromRoleState(IUser user, IRole[] roles)
    {
        var removed = await Service.RemoveRolesFromUserRoleState(ctx.Guild.Id, user.Id, roles.Select(x => x.Id));
        if (!removed.Item1)
            await ctx.Interaction.SendErrorAsync($"{bss.Data.ErrorEmote} Remove failed because:\n{removed.Item2}");
        else
            await ctx.Interaction.SendConfirmAsync($"{bss.Data.SuccessEmote} Successfully removed those roles from {user}'s Role State!.");
    }

    [SlashCommand("add-to-rolestate","Adds a role to a RoleState"), SlashUserPerm(GuildPermission.Administrator)]
    public async Task AddRolesToRoleState(IUser user, IRole[] roles)
    {
        var removed = await Service.AddRolesToUserRoleState(ctx.Guild.Id, user.Id, roles.Select(x => x.Id));
        if (!removed.Item1)
            await ctx.Interaction.SendErrorAsync($"{bss.Data.ErrorEmote} Remove failed because:\n{removed.Item2}");
        else
            await ctx.Interaction.SendConfirmAsync($"{bss.Data.SuccessEmote} Successfully removed those roles from {user}'s Role State!.");
    }

    [SlashCommand("delete-role-state","Deletes a users RoleState"), SlashUserPerm(GuildPermission.Administrator)]
    public async Task DeleteUserRoleState(IUser user)
    {
        var deleted = await Service.DeleteUserRoleState(user.Id, ctx.Guild.Id);
        if (!deleted)
            await ctx.Interaction.SendErrorAsync($"{bss.Data.ErrorEmote} No Role State to delete!");
        else
            await ctx.Interaction.SendConfirmAsync($"{bss.Data.SuccessEmote} Successfully deleted {user}'s Role State!");
    }

}