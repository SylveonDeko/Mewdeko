using System.IO;
using System.Net.Http;
using System.Text;
using Discord.Interactions;
using Discord.Net;
using Humanizer;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.Modals;
using Mewdeko.Modules.Moderation.Services;
using Mewdeko.Modules.Server_Management.Services;
using Mewdeko.Services.Settings;
using Swan;

namespace Mewdeko.Modules.Server_Management;

/// <summary>
///     Slash commands for managing roles within a guild, including creation, deletion, synchronization, and mass user
///     assignment.
/// </summary>
/// <param name="config">The bot configuration settings.</param>
/// <param name="muteService">The mute service used for timed role assignments.</param>
[Group("rolemanage", "Create, delete, sync and mass assign roles")]
public class SlashRoleManage(BotConfigService config, MuteService muteService)
    : MewdekoSlashModuleBase<RoleCommandsService>
{
    private const string JobsCommand = "`/rolemanage jobs`";

    private async Task<bool> CanManageRole(IRole role)
    {
        var runnerUser = (IGuildUser)ctx.User;
        var currentUser = await ctx.Guild.GetUserAsync(ctx.Client.CurrentUser.Id).ConfigureAwait(false);
        if (ctx.User.Id != ctx.Guild.OwnerId && runnerUser.GetRoles().Max(x => x.Position) <= role.Position)
        {
            await ErrorAsync(Strings.CannotManageRole(ctx.Guild.Id)).ConfigureAwait(false);
            return false;
        }

        if (currentUser.GetRoles().Max(x => x.Position) <= role.Position)
        {
            await ErrorAsync(Strings.BotCannotManageRole(ctx.Guild.Id)).ConfigureAwait(false);
            return false;
        }

        return true;
    }

    private async Task<bool> CanManageRoles(IRole role, IRole role2)
    {
        var runnerUser = (IGuildUser)ctx.User;
        var currentUser = await ctx.Guild.GetUserAsync(ctx.Client.CurrentUser.Id).ConfigureAwait(false);
        var runnerMax = runnerUser.GetRoles().Max(x => x.Position);
        if (ctx.User.Id != ctx.Guild.OwnerId && (runnerMax <= role2.Position || runnerMax <= role.Position))
        {
            await ErrorAsync(Strings.CannotManageRoles(ctx.Guild.Id)).ConfigureAwait(false);
            return false;
        }

        var botMax = currentUser.GetRoles().Max(x => x.Position);
        if (botMax <= role2.Position || botMax <= role.Position)
        {
            await ErrorAsync(Strings.BotCannotManageRoles(ctx.Guild.Id)).ConfigureAwait(false);
            return false;
        }

        return true;
    }

    private async Task<bool> CanManageEachRole(IEnumerable<IRole> roles)
    {
        var runnerUser = (IGuildUser)ctx.User;
        var currentUser = await ctx.Guild.GetUserAsync(ctx.Client.CurrentUser.Id).ConfigureAwait(false);
        var runnerMax = runnerUser.GetRoles().Max(x => x.Position);
        var botMax = currentUser.GetRoles().Max(x => x.Position);
        foreach (var i in roles)
        {
            if (ctx.User.Id != ctx.Guild.OwnerId && runnerMax <= i.Position)
            {
                await ErrorAsync(Strings.CannotManageUser(ctx.Guild.Id, i.Mention)).ConfigureAwait(false);
                return false;
            }

            if (botMax > i.Position) continue;
            await ErrorAsync(Strings.CannotManageRoleMention(ctx.Guild.Id, i.Mention)).ConfigureAwait(false);
            return false;
        }

        return true;
    }

    private async Task<bool> JobLimitReached()
    {
        if (Service.Jobslist.Count < 5) return false;
        await ErrorAsync(Strings.MassroleJobLimit(ctx.Guild.Id, JobsCommand)).ConfigureAwait(false);
        return true;
    }

    private int NextJobId()
    {
        return Service.Jobslist.Count == 0 ? 1 : Service.Jobslist.Max(x => x.JobId) + 1;
    }

    private async Task RunJob(int jobId, IEnumerable<IGuildUser> users, int count, string stoppedTarget,
        Func<IGuildUser, Task> action, Func<int, string> completed)
    {
        var count2 = 0;
        foreach (var i in users)
        {
            try
            {
                var e = Service.JobCheck(ctx.Guild, jobId).FirstOrDefault()?.StoppedOrNot;
                if (e == "Stopped")
                {
                    await Service.RemoveJob(ctx.Guild, jobId).ConfigureAwait(false);
                    await ctx.Channel.SendConfirmAsync(Strings.MassroleStopped(ctx.Guild.Id, stoppedTarget,
                        count2.ToString("N0"), count.ToString("N0"))).ConfigureAwait(false);
                    return;
                }

                await action(i).ConfigureAwait(false);
                await Service.UpdateCount(ctx.Guild, jobId, count2).ConfigureAwait(false);
                count2++;
            }
            catch (HttpException)
            {
            }
        }

        await Service.RemoveJob(ctx.Guild, jobId).ConfigureAwait(false);
        await ctx.Channel.SendConfirmAsync(completed(count2)).ConfigureAwait(false);
    }

    private async Task<OverwritePermissions?> GetCurrentChannelOverwrite(IRole role)
    {
        var ch = ctx.Channel as ITextChannel;
        var perms = ch?.GetPermissionOverwrite(role);
        if (perms is not null) return perms;
        await ErrorAsync(Strings.RoleNoPermsInChannel(ctx.Guild.Id)).ConfigureAwait(false);
        return null;
    }

    /// <summary>
    ///     Opens a modal to create multiple roles within the guild, one role name per line.
    /// </summary>
    [SlashCommand("create-roles", "Creates multiple roles, one name per line")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageRoles)]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    public Task CreateRoles()
    {
        return RespondWithModalAsync<CreateRolesModal>("servermanagement_create_roles");
    }

    /// <summary>
    ///     Handles the create roles modal submission and creates each listed role after confirmation.
    /// </summary>
    /// <param name="modal">The submitted modal containing the role names.</param>
    [ModalInteraction("servermanagement_create_roles", true)]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageRoles)]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    public async Task CreateRolesSubmitted(CreateRolesModal modal)
    {
        var roleList = (modal.RoleNames ?? string.Empty)
            .Split('\n')
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();
        if (roleList.Length == 0)
        {
            await ErrorAsync(Strings.NoRoleNamesProvided(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        if (!await PromptUserConfirmAsync(
                Strings.CreateRolesConfirm(ctx.Guild.Id, roleList.Length, string.Join("\n", roleList)),
                ctx.User.Id).ConfigureAwait(false))
            return;

        var msg = await ctx.Interaction.FollowupAsync(embed: new EmbedBuilder()
            .WithOkColor()
            .WithDescription(Strings.CreatingRoles(ctx.Guild.Id, config.Data.LoadingEmote, roleList.Length))
            .Build()).ConfigureAwait(false);
        foreach (var i in roleList)
        {
            await ctx.Guild.CreateRoleAsync(i, null, null, false, false).ConfigureAwait(false);
        }

        await msg.ModifyAsync(x =>
        {
            x.Embed = new EmbedBuilder()
                .WithOkColor()
                .WithDescription(Strings.RolesCreated(ctx.Guild.Id, config.Data.SuccessEmote, roleList.Length))
                .Build();
        }).ConfigureAwait(false);
    }

    /// <summary>
    ///     Synchronizes a role's permissions from the current channel to all text channels and categories within the guild.
    /// </summary>
    /// <param name="role">The role to synchronize across the guild.</param>
    [SlashCommand("sync-to-all", "Syncs a role's overwrites here to all channels and categories")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    [RequireBotPermission(GuildPermission.ManageChannels)]
    public async Task SyncRoleToAll([Summary("role", "The role to sync")] IRole role)
    {
        var perms = await GetCurrentChannelOverwrite(role).ConfigureAwait(false);
        if (perms is null) return;
        await DeferAsync().ConfigureAwait(false);

        var channels = (await ctx.Guild.GetChannelsAsync().ConfigureAwait(false))
            .Where(x => x is not (SocketThreadChannel or SocketVoiceChannel)).ToList();
        var categories = await ctx.Guild.GetCategoriesAsync().ConfigureAwait(false);
        var textCount = (await ctx.Guild.GetTextChannelsAsync().ConfigureAwait(false))
            .Count(x => x is not SocketThreadChannel);

        var msg = await ctx.Interaction.FollowupAsync(embed: new EmbedBuilder().WithOkColor()
            .WithDescription(Strings.SyncingRolePerms(ctx.Guild.Id, config.Data.LoadingEmote, role.Mention,
                textCount, categories.Count))
            .Build()).ConfigureAwait(false);

        foreach (var i in channels)
        {
            await i.AddPermissionOverwriteAsync(role, perms.Value).ConfigureAwait(false);
        }

        foreach (var i in categories)
        {
            await i.AddPermissionOverwriteAsync(role, perms.Value).ConfigureAwait(false);
        }

        var eb = new EmbedBuilder
        {
            Color = Mewdeko.OkColor,
            Description = Strings.SuccessfullySyncedPermsChannelsCategories(ctx.Guild.Id, role.Mention, textCount,
                categories.Count)
        };
        await msg.ModifyAsync(x => x.Embed = eb.Build()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Synchronizes a role's permissions from the current channel to all text channels within the guild.
    /// </summary>
    /// <param name="role">The role to synchronize across text channels.</param>
    [SlashCommand("sync-to-channels", "Syncs a role's overwrites here to all text channels")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    [RequireBotPermission(GuildPermission.ManageChannels)]
    public async Task SyncRoleToAllChannels([Summary("role", "The role to sync")] IRole role)
    {
        var perms = await GetCurrentChannelOverwrite(role).ConfigureAwait(false);
        if (perms is null) return;
        await DeferAsync().ConfigureAwait(false);

        var channels = (await ctx.Guild.GetTextChannelsAsync().ConfigureAwait(false))
            .Where(x => x is not SocketThreadChannel).ToList();

        var msg = await ctx.Interaction.FollowupAsync(embed: new EmbedBuilder().WithOkColor()
            .WithDescription(Strings.SyncingRolePermsChannels(ctx.Guild.Id, config.Data.LoadingEmote, role.Mention,
                channels.Count))
            .Build()).ConfigureAwait(false);

        foreach (var i in channels)
        {
            await i.AddPermissionOverwriteAsync(role, perms.Value).ConfigureAwait(false);
        }

        var eb = new EmbedBuilder
        {
            Color = Mewdeko.OkColor,
            Description = Strings.SuccessfullySyncedPermsChannels(ctx.Guild.Id, role.Mention, channels.Count)
        };
        await msg.ModifyAsync(x => x.Embed = eb.Build()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Synchronizes a role's permissions from the current channel to all categories within the guild.
    /// </summary>
    /// <param name="role">The role to synchronize across categories.</param>
    [SlashCommand("sync-to-categories", "Syncs a role's overwrites here to all categories")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    [RequireBotPermission(GuildPermission.ManageChannels)]
    public async Task SyncRoleToAllCategories([Summary("role", "The role to sync")] IRole role)
    {
        var perms = await GetCurrentChannelOverwrite(role).ConfigureAwait(false);
        if (perms is null) return;
        await DeferAsync().ConfigureAwait(false);

        var categories = await ctx.Guild.GetCategoriesAsync().ConfigureAwait(false);

        var msg = await ctx.Interaction.FollowupAsync(embed: new EmbedBuilder().WithOkColor()
            .WithDescription(Strings.SyncingRolePermsCategories(ctx.Guild.Id, config.Data.LoadingEmote,
                role.Mention, categories.Count))
            .Build()).ConfigureAwait(false);

        foreach (var i in categories)
        {
            await i.AddPermissionOverwriteAsync(role, perms.Value).ConfigureAwait(false);
        }

        var eb = new EmbedBuilder
        {
            Color = Mewdeko.OkColor,
            Description = Strings.SuccessfullySyncedPermsCategories(ctx.Guild.Id, role.Mention, categories.Count)
        };
        await msg.ModifyAsync(x => x.Embed = eb.Build()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Deletes a list of roles from the guild after confirmation.
    /// </summary>
    /// <param name="roles">The roles to delete, separated by spaces.</param>
    [SlashCommand("delete-roles", "Deletes the given roles")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageRoles)]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    public async Task DeleteRoles([Summary("roles", "Roles to delete, separated by spaces")] IRole[] roles)
    {
        var deletable = roles.Where(x => !x.IsManaged).ToList();
        if (deletable.Count == 0)
        {
            await ErrorAsync(Strings.CannotDeleteManagedRoles(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await DeferAsync().ConfigureAwait(false);
        var secondlist = new List<string>();
        var runnerUser = (IGuildUser)ctx.User;
        var currentUser = await ctx.Guild.GetUserAsync(ctx.Client.CurrentUser.Id).ConfigureAwait(false);
        var guildUsers = await ctx.Guild.GetUsersAsync().ConfigureAwait(false);
        foreach (var i in deletable)
        {
            if (ctx.User.Id != ctx.Guild.OwnerId && runnerUser.GetRoles().Max(x => x.Position) <= i.Position)
            {
                await ErrorAsync(Strings.CannotManageUser(ctx.Guild.Id, i.Mention)).ConfigureAwait(false);
                return;
            }

            if (currentUser.GetRoles().Max(x => x.Position) <= i.Position)
            {
                await ErrorAsync(Strings.CannotManageMention(ctx.Guild.Id, i.Mention)).ConfigureAwait(false);
                return;
            }

            secondlist.Add(Strings.DeleteRolesEntry(ctx.Guild.Id, i.Mention,
                guildUsers.Count(x => x.RoleIds.Contains(i.Id))));
        }

        var embed = new EmbedBuilder
        {
            Title = Strings.DeleteRolesConfirm(ctx.Guild.Id), Description = string.Join("\n", secondlist)
        };
        if (!await PromptUserConfirmAsync(embed, ctx.User.Id).ConfigureAwait(false))
            return;

        var msg = await ctx.Interaction.FollowupAsync(embed: new EmbedBuilder().WithOkColor()
            .WithDescription(Strings.DeletingRolesProgress(ctx.Guild.Id, config.Data.LoadingEmote, deletable.Count))
            .Build()).ConfigureAwait(false);
        foreach (var i in deletable) await i.DeleteAsync().ConfigureAwait(false);
        var newemb = new EmbedBuilder
        {
            Description = Strings.RolesDeleted(ctx.Guild.Id, deletable.Count), Color = Mewdeko.OkColor
        };
        await msg.ModifyAsync(x => x.Embed = newemb.Build()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Stops a mass role operation job by its job number.
    /// </summary>
    /// <param name="jobnum">The job number of the mass role operation to stop.</param>
    [SlashCommand("stop-job", "Stops a running mass role job")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageRoles)]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    public async Task StopJob([Summary("job", "The job number to stop")] int jobnum)
    {
        var list = Service.Jobslist.Find(x => x.JobId == jobnum && x.GuildId == ctx.Guild.Id);
        if (list == null)
        {
            await ErrorAsync(Strings.MassroleJobNotFound(ctx.Guild.Id, JobsCommand)).ConfigureAwait(false);
            return;
        }

        var eb = new EmbedBuilder
        {
            Color = Mewdeko.OkColor, Description = Strings.StopJobConfirm(ctx.Guild.Id)
        };
        eb.AddField(list.JobType,
            Strings.StopJobDetails(ctx.Guild.Id, list.StartedBy.Mention, list.AddedTo, list.TotalUsers));
        if (!await PromptUserConfirmAsync(eb, ctx.User.Id).ConfigureAwait(false))
        {
            await ConfirmAsync(Strings.JobStopCancelled(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await Service.StopJob(ctx.Channel as ITextChannel, jobnum, ctx.Guild).ConfigureAwait(false);
    }

    /// <summary>
    ///     Gives a user one or more roles, optionally for a limited time.
    /// </summary>
    /// <param name="user">The user whose roles will be modified.</param>
    /// <param name="roles">The roles to be added to the user.</param>
    /// <param name="time">Optional duration after which the roles are removed again.</param>
    [SlashCommand("set-roles", "Gives a user the given roles, optionally for a limited time")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageRoles)]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    public async Task SetRoles([Summary("user", "The user to give roles to")] IGuildUser user,
        [Summary("roles", "Roles to add, separated by spaces")]
        IRole[] roles,
        [Summary("time", "How long the roles should last, e.g. 1h30m")]
        TimeSpan? time = null)
    {
        if (!await CanManageEachRole(roles).ConfigureAwait(false)) return;

        if (time is null)
        {
            await user.AddRolesAsync(roles).ConfigureAwait(false);
            await ConfirmAsync(Strings.UserGivenRoles(ctx.Guild.Id, user,
                string.Join<string>("|", roles.Select(x => x.Mention)))).ConfigureAwait(false);
            return;
        }

        foreach (var role in roles)
        {
            await muteService.TimedRole(user, time.Value, $"Timed role assignment by {ctx.User}", role)
                .ConfigureAwait(false);
        }

        await ConfirmAsync(Strings.UserGivenTimedRoles(ctx.Guild.Id, user,
                string.Join<string>("|", roles.Select(x => x.Mention)), time.Value.Humanize()))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Assigns a specific role to a list of users.
    /// </summary>
    /// <param name="role">The role to be added to the users.</param>
    /// <param name="users">The users to whom the role will be added.</param>
    [SlashCommand("add-users-to-role", "Adds a role to the given users")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageRoles)]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    public async Task AddUsersToRole([Summary("role", "The role to add")] IRole role,
        [Summary("users", "Users to add, separated by spaces")]
        IUser[] users)
    {
        if (!await CanManageRole(role).ConfigureAwait(false)) return;
        await DeferAsync().ConfigureAwait(false);

        foreach (var i in users.OfType<IGuildUser>())
        {
            await i.AddRoleAsync(role).ConfigureAwait(false);
        }

        await ConfirmAsync(Strings.RoleUsersAdded(ctx.Guild.Id, role.Mention,
            string.Join<string>("|", users.Select(x => x.Mention)))).ConfigureAwait(false);
    }

    /// <summary>
    ///     Removes a specific role from a list of users.
    /// </summary>
    /// <param name="role">The role to be removed from the users.</param>
    /// <param name="users">The users from whom the role will be removed.</param>
    [SlashCommand("remove-users-from-role", "Removes a role from the given users")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageRoles)]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    public async Task RemoveUsersFromRole([Summary("role", "The role to remove")] IRole role,
        [Summary("users", "Users to remove, separated by spaces")]
        IUser[] users)
    {
        if (!await CanManageRole(role).ConfigureAwait(false)) return;
        await DeferAsync().ConfigureAwait(false);

        foreach (var i in users.OfType<IGuildUser>())
        {
            await i.RemoveRoleAsync(role).ConfigureAwait(false);
        }

        await ConfirmAsync(Strings.RoleUsersRemoved(ctx.Guild.Id, role.Mention,
            string.Join<string>("|", users.Select(x => x.Mention)))).ConfigureAwait(false);
    }

    /// <summary>
    ///     Removes specified roles from a user.
    /// </summary>
    /// <param name="user">The user from whom the roles will be removed.</param>
    /// <param name="roles">The roles to be removed from the user.</param>
    [SlashCommand("remove-roles", "Removes the given roles from a user")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageRoles)]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    public async Task RemoveRoles([Summary("user", "The user to remove roles from")] IGuildUser user,
        [Summary("roles", "Roles to remove, separated by spaces")]
        IRole[] roles)
    {
        if (!await CanManageEachRole(roles).ConfigureAwait(false)) return;

        await user.RemoveRolesAsync(roles).ConfigureAwait(false);
        await ConfirmAsync(
                $"{user} {Strings.RemoveRoles(ctx.Guild.Id)}:\n{string.Join<string>("|", roles.Select(x => x.Mention))}")
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Lists all ongoing mass role operations within the server, providing details about each.
    /// </summary>
    [SlashCommand("jobs", "Lists running mass role operations")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageRoles)]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    public async Task RoleJobs()
    {
        var list = Service.Jobslist.Where(x => x.GuildId == ctx.Guild.Id).ToList();
        if (list.Count == 0)
        {
            await ErrorAsync(Strings.NoMassOperations(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var eb = new EmbedBuilder
        {
            Title = Strings.RoleJobsTitle(ctx.Guild.Id, list.Count), Color = Mewdeko.OkColor
        };
        foreach (var i in list)
        {
            if (i.Role2 is not null && i.JobType != "Adding then Removing a Role")
            {
                eb.AddField(Strings.RoleJobName(ctx.Guild.Id, i.JobId),
                    Strings.RoleJobTwoRoles(ctx.Guild.Id, i.JobType, i.StartedBy.Mention, i.AddedTo, i.TotalUsers,
                        i.Role1?.Mention, i.Role2.Mention));
            }
            else if (i.Role2 is not null)
            {
                eb.AddField(Strings.RoleJobName(ctx.Guild.Id, i.JobId),
                    Strings.RoleJobAddRemove(ctx.Guild.Id, i.JobType, i.StartedBy.Mention, i.AddedTo, i.TotalUsers,
                        i.Role2.Mention, i.Role1?.Mention));
            }
            else
            {
                eb.AddField(Strings.RoleJobName(ctx.Guild.Id, i.JobId),
                    Strings.RoleJobOneRole(ctx.Guild.Id, i.JobType, i.StartedBy.Mention, i.AddedTo, i.TotalUsers,
                        i.Role1?.Mention));
            }
        }

        await ctx.Interaction.RespondAsync(embed: eb.Build()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Adds a specified role to all server members.
    /// </summary>
    /// <param name="role">The role to be added to all server members.</param>
    [SlashCommand("add-to-all", "Adds a role to every member, users and bots")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageRoles)]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    public async Task AddToAll([Summary("role", "The role to add")] IRole role)
    {
        if (!await CanManageRole(role).ConfigureAwait(false)) return;
        if (await JobLimitReached().ConfigureAwait(false)) return;

        var guild = ctx.Guild as SocketGuild;
        var users = guild.Users.Where(c => !c.Roles.Contains(role)).ToList();
        var count = users.Count;
        if (count == 0)
        {
            await ErrorAsync(Strings.AllUsersHaveRole(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await DeferAsync().ConfigureAwait(false);
        var jobId = NextJobId();
        await Service.AddToList(ctx.Guild, ctx.User as IGuildUser, jobId, count, "Adding to Users and Bots", role)
            .ConfigureAwait(false);
        await ConfirmAsync(Strings.MassroleAdding(ctx.Guild.Id, role.Mention, count,
            TimeSpan.FromSeconds(count).Humanize())).ConfigureAwait(false);
        await RunJob(jobId, users, count, role.Mention, i => i.AddRoleAsync(role),
            done => Strings.AppliedRoleToMembers(ctx.Guild.Id, role.Mention, done, count)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Adds a specified role to all bots in the server.
    /// </summary>
    /// <param name="role">The role to be added to all bots.</param>
    [SlashCommand("add-to-all-bots", "Adds a role to every bot")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageRoles)]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    public async Task AddToAllBots([Summary("role", "The role to add")] IRole role)
    {
        if (!await CanManageRole(role).ConfigureAwait(false)) return;
        if (await JobLimitReached().ConfigureAwait(false)) return;

        var guild = ctx.Guild as SocketGuild;
        var users = guild.Users.Where(c => !c.Roles.Contains(role) && c.IsBot).ToList();
        var count = users.Count;
        if (count == 0)
        {
            await ErrorAsync(Strings.AllBotsHaveRole(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await DeferAsync().ConfigureAwait(false);
        var jobId = NextJobId();
        await Service.AddToList(ctx.Guild, ctx.User as IGuildUser, jobId, count, "Adding to Bots Only", role)
            .ConfigureAwait(false);
        await ConfirmAsync(Strings.MassroleAdding(ctx.Guild.Id, role.Mention, count,
            TimeSpan.FromSeconds(count).Humanize())).ConfigureAwait(false);
        await RunJob(jobId, users, count, role.Mention, i => i.AddRoleAsync(role),
            done => Strings.AppliedRoleToBots(ctx.Guild.Id, role.Mention, done, count)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Adds a specified role to all human users in the server, excluding bots.
    /// </summary>
    /// <param name="role">The role to be added to all human users.</param>
    [SlashCommand("add-to-all-users", "Adds a role to every human user")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageRoles)]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    public async Task AddToAllUsers([Summary("role", "The role to add")] IRole role)
    {
        if (!await CanManageRole(role).ConfigureAwait(false)) return;
        if (await JobLimitReached().ConfigureAwait(false)) return;

        var guild = ctx.Guild as SocketGuild;
        var users = guild.Users.Where(c => !c.Roles.Contains(role) && !c.IsBot).ToList();
        var count = users.Count;
        if (count == 0)
        {
            await ErrorAsync(Strings.AllUsersHaveRole(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await DeferAsync().ConfigureAwait(false);
        var jobId = NextJobId();
        await Service.AddToList(ctx.Guild, ctx.User as IGuildUser, jobId, count, "Adding to Users Only", role)
            .ConfigureAwait(false);
        await ConfirmAsync(Strings.MassroleAdding(ctx.Guild.Id, role.Mention, count,
            TimeSpan.FromSeconds(count).Humanize())).ConfigureAwait(false);
        await RunJob(jobId, users, count, role.Mention, i => i.AddRoleAsync(role),
            done => Strings.AppliedRoleToUsers(ctx.Guild.Id, role.Mention, done, count)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Adds a specified role to all users who have been a server member for longer than a specified duration.
    /// </summary>
    /// <param name="time">The minimum duration a user must have been a member of the server to receive the role.</param>
    /// <param name="role">The role to be added to qualifying users.</param>
    [SlashCommand("add-to-users-over", "Adds a role to users who joined longer ago than the given time")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageRoles)]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    public async Task AddToUsersOver([Summary("time", "Minimum membership length, e.g. 30d")] TimeSpan time,
        [Summary("role", "The role to add")] IRole role)
    {
        if (!await CanManageRole(role).ConfigureAwait(false)) return;
        if (await JobLimitReached().ConfigureAwait(false)) return;

        var guild = ctx.Guild as SocketGuild;
        var users = guild.Users.Where(c =>
            !c.Roles.Contains(role) && !c.IsBot && c.JoinedAt.HasValue &&
            DateTimeOffset.Now.Subtract(c.JoinedAt.Value) >= time).ToList();
        var count = users.Count;
        if (count == 0)
        {
            await ErrorAsync(Strings.UsersAtAgeHaveRole(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await DeferAsync().ConfigureAwait(false);
        var jobId = NextJobId();
        await Service.AddToList(ctx.Guild, ctx.User as IGuildUser, jobId, count,
                $"Adding a role to server members that have been here for {time.Humanize()}", role)
            .ConfigureAwait(false);
        await ConfirmAsync(Strings.MassroleAddingOver(ctx.Guild.Id, role.Mention, count, time.Humanize(),
            TimeSpan.FromSeconds(count).Humanize())).ConfigureAwait(false);
        await RunJob(jobId, users, count, role.Mention, i => i.AddRoleAsync(role),
            done => Strings.AppliedRoleToUsers(ctx.Guild.Id, role.Mention, done, count)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Adds a specified role to all users who have been a server member for shorter than a specified duration.
    /// </summary>
    /// <param name="time">The maximum duration a user can have been a member of the server to receive the role.</param>
    /// <param name="role">The role to be added to qualifying users.</param>
    [SlashCommand("add-to-users-under", "Adds a role to users who joined more recently than the given time")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageRoles)]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    public async Task AddToUsersUnder([Summary("time", "Maximum membership length, e.g. 7d")] TimeSpan time,
        [Summary("role", "The role to add")] IRole role)
    {
        if (!await CanManageRole(role).ConfigureAwait(false)) return;
        if (await JobLimitReached().ConfigureAwait(false)) return;

        var guild = ctx.Guild as SocketGuild;
        var users = guild.Users.Where(c =>
            !c.Roles.Contains(role) && !c.IsBot && c.JoinedAt.HasValue &&
            DateTimeOffset.Now.Subtract(c.JoinedAt.Value) < time).ToList();
        var count = users.Count;
        if (count == 0)
        {
            await ErrorAsync(Strings.UsersAtAgeHaveRole(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await DeferAsync().ConfigureAwait(false);
        var jobId = NextJobId();
        await Service.AddToList(ctx.Guild, ctx.User as IGuildUser, jobId, count,
                $"Adding a role to server members that have been here for {time.Humanize()} or less", role)
            .ConfigureAwait(false);
        await ConfirmAsync(Strings.MassroleAddingUnder(ctx.Guild.Id, role.Mention, count, time.Humanize(),
            TimeSpan.FromSeconds(count).Humanize())).ConfigureAwait(false);
        await RunJob(jobId, users, count, role.Mention, i => i.AddRoleAsync(role),
            done => Strings.AppliedRoleToUsers(ctx.Guild.Id, role.Mention, done, count)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Removes a specified role from all server members.
    /// </summary>
    /// <param name="role">The role to be removed from all server members.</param>
    [SlashCommand("remove-from-all", "Removes a role from every member, users and bots")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageRoles)]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    public async Task RemoveFromAll([Summary("role", "The role to remove")] IRole role)
    {
        if (!await CanManageRole(role).ConfigureAwait(false)) return;
        if (await JobLimitReached().ConfigureAwait(false)) return;

        var guild = ctx.Guild as SocketGuild;
        var users = guild.Users.Where(c => c.Roles.Contains(role)).ToList();
        var count = users.Count;
        if (count == 0)
        {
            await ErrorAsync(Strings.NoUsersHaveRole(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await DeferAsync().ConfigureAwait(false);
        var jobId = NextJobId();
        await Service.AddToList(ctx.Guild, ctx.User as IGuildUser, jobId, count,
            "Removing a role from all server members", role).ConfigureAwait(false);
        await ConfirmAsync(Strings.MassroleRemoving(ctx.Guild.Id, role.Mention, count,
            TimeSpan.FromSeconds(count).Humanize())).ConfigureAwait(false);
        await RunJob(jobId, users, count, role.Mention, i => i.RemoveRoleAsync(role),
            done => Strings.RemovedRoleFromMembers(ctx.Guild.Id, role.Mention, done, count)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Removes a specified role from all human users, excluding bots.
    /// </summary>
    /// <param name="role">The role to be removed from all human members.</param>
    [SlashCommand("remove-from-all-users", "Removes a role from every human user")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageRoles)]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    public async Task RemoveFromAllUsers([Summary("role", "The role to remove")] IRole role)
    {
        if (!await CanManageRole(role).ConfigureAwait(false)) return;
        if (await JobLimitReached().ConfigureAwait(false)) return;

        var guild = ctx.Guild as SocketGuild;
        var users = guild.Users.Where(c => c.Roles.Contains(role) && !c.IsBot).ToList();
        var count = users.Count;
        if (count == 0)
        {
            await ErrorAsync(Strings.NoUsersHaveRole(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await DeferAsync().ConfigureAwait(false);
        var jobId = NextJobId();
        await Service.AddToList(ctx.Guild, ctx.User as IGuildUser, jobId, count,
            "Removing a role from only users", role).ConfigureAwait(false);
        await ConfirmAsync(Strings.MassroleRemoving(ctx.Guild.Id, role.Mention, count,
            TimeSpan.FromSeconds(count).Humanize())).ConfigureAwait(false);
        await RunJob(jobId, users, count, role.Mention, i => i.RemoveRoleAsync(role),
                done => Strings.RemovedRoleFromRoleForUsers(ctx.Guild.Id, role.Mention, done, count))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Removes a specified role from all bots in the server.
    /// </summary>
    /// <param name="role">The role to be removed from all bots.</param>
    [SlashCommand("remove-from-all-bots", "Removes a role from every bot")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageRoles)]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    public async Task RemoveFromAllBots([Summary("role", "The role to remove")] IRole role)
    {
        if (!await CanManageRole(role).ConfigureAwait(false)) return;
        if (await JobLimitReached().ConfigureAwait(false)) return;

        var guild = ctx.Guild as SocketGuild;
        var users = guild.Users.Where(c => c.Roles.Contains(role) && c.IsBot).ToList();
        var count = users.Count;
        if (count == 0)
        {
            await ErrorAsync(Strings.NoBotsHaveRole(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await DeferAsync().ConfigureAwait(false);
        var jobId = NextJobId();
        await Service.AddToList(ctx.Guild, ctx.User as IGuildUser, jobId, count,
            "Removing a role from all bots", role).ConfigureAwait(false);
        await ConfirmAsync(Strings.MassroleRemoving(ctx.Guild.Id, role.Mention, count,
            TimeSpan.FromSeconds(count).Humanize())).ConfigureAwait(false);
        await RunJob(jobId, users, count, role.Mention, i => i.RemoveRoleAsync(role),
            done => Strings.RemovedRoleFromBots(ctx.Guild.Id, role.Mention, done, count)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Adds a role to all users who currently have another specified role.
    /// </summary>
    /// <param name="role">The role whose members will receive the new role.</param>
    /// <param name="role2">The role to add to those members.</param>
    [SlashCommand("add-role-to-role", "Adds a role to everyone who has another role")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageRoles)]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    public async Task AddRoleToRole([Summary("source-role", "Members of this role get the new role")] IRole role,
        [Summary("role-to-add", "The role to add to them")]
        IRole role2)
    {
        if (!await CanManageRoles(role, role2).ConfigureAwait(false)) return;
        if (await JobLimitReached().ConfigureAwait(false)) return;

        await DeferAsync().ConfigureAwait(false);
        var users = await ctx.Guild.GetUsersAsync().ConfigureAwait(false);
        var inrole = users.Where(x => x.GetRoles().Contains(role)).ToList();
        var inrole2 = users.Where(x => x.GetRoles().Contains(role2)).ToList();
        if (inrole.Count == inrole2.Count)
        {
            await ErrorAsync(Strings.AllUsersAlreadyHaveRole(ctx.Guild.Id, role.Mention, role2.Mention))
                .ConfigureAwait(false);
            return;
        }

        var jobId = NextJobId();
        await Service.AddToList(ctx.Guild, ctx.User as IGuildUser, jobId, inrole.Count,
            "Adding a role to users within a role", role, role2).ConfigureAwait(false);
        await ConfirmAsync(Strings.MassroleAddingToRole(ctx.Guild.Id, role2.Mention, role.Mention,
            TimeSpan.FromSeconds(inrole.Count).Humanize())).ConfigureAwait(false);
        await RunJob(jobId, inrole, inrole.Count, role2.Mention, i => i.AddRoleAsync(role2),
            done => Strings.AddedRoleToUsers(ctx.Guild.Id, role2.Mention, done)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Removes a role from all users who currently have another specified role.
    /// </summary>
    /// <param name="role">The role whose members will have the other role removed.</param>
    /// <param name="role2">The role to remove from those members.</param>
    [SlashCommand("remove-from-role", "Removes a role from everyone who has another role")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageRoles)]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    public async Task RemoveFromRole(
        [Summary("source-role", "Members of this role lose the other role")]
        IRole role,
        [Summary("role-to-remove", "The role to remove from them")]
        IRole role2)
    {
        if (!await CanManageRoles(role, role2).ConfigureAwait(false)) return;
        if (await JobLimitReached().ConfigureAwait(false)) return;

        await DeferAsync().ConfigureAwait(false);
        var users = await ctx.Guild.GetUsersAsync().ConfigureAwait(false);
        var inrole = users.Where(x => x.GetRoles().Contains(role)).ToList();
        var inrole2 = users.Where(x => x.GetRoles().Contains(role2)).ToList();
        if (inrole2.Count == 0)
        {
            await ErrorAsync(Strings.NoUsersInRoleHaveRole(ctx.Guild.Id, role.Mention, role2.Mention))
                .ConfigureAwait(false);
            return;
        }

        var jobId = NextJobId();
        await Service.AddToList(ctx.Guild, ctx.User as IGuildUser, jobId, inrole.Count,
            "Removing a role from users within a role", role, role2).ConfigureAwait(false);
        await ConfirmAsync(Strings.RemovingRoleFromUsersInRole(ctx.Guild.Id, role2.Mention, role.Mention,
            inrole.Count)).ConfigureAwait(false);
        await RunJob(jobId, inrole, inrole.Count, role2.Mention, i => i.RemoveRoleAsync(role2),
            done => Strings.RemovedRoleFromUsers(ctx.Guild.Id, role2.Mention, done)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Adds a role to users who have another role and then removes that other role from them.
    /// </summary>
    /// <param name="role">The role to add to the users.</param>
    /// <param name="role2">The role whose members are targeted and which is removed from them.</param>
    [SlashCommand("add-then-remove", "Adds a role to members of another role, then removes that role")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageRoles)]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    public async Task AddThenRemove([Summary("role-to-add", "The role to add")] IRole role,
        [Summary("role-to-remove", "Members of this role are targeted and lose it")]
        IRole role2)
    {
        if (!await CanManageRoles(role, role2).ConfigureAwait(false)) return;
        if (await JobLimitReached().ConfigureAwait(false)) return;

        await DeferAsync().ConfigureAwait(false);
        var users = await ctx.Guild.GetUsersAsync().ConfigureAwait(false);
        var inrole = users.Where(x => x.GetRoles().Contains(role2)).ToList();
        if (inrole.Count == 0)
        {
            await ErrorAsync(Strings.NoUsersHaveRole(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var jobId = NextJobId();
        await Service.AddToList(ctx.Guild, ctx.User as IGuildUser, jobId, inrole.Count,
            "Adding then Removing a Role", role, role2).ConfigureAwait(false);
        await ConfirmAsync(Strings.MassroleAddThenRemove(ctx.Guild.Id, role.Mention, role2.Mention,
            TimeSpan.FromSeconds(inrole.Count * 2).Humanize())).ConfigureAwait(false);
        await RunJob(jobId, inrole, inrole.Count, $"{role2.Mention} and removed {role.Mention}", async i =>
                {
                    await i.AddRoleAsync(role).ConfigureAwait(false);
                    await i.RemoveRoleAsync(role2).ConfigureAwait(false);
                },
                done => Strings.RoleAddedRemovedUsers(ctx.Guild.Id, role2.Mention, done, role.Mention))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Commands for exporting and importing role membership lists.
    /// </summary>
    /// <param name="config">The bot configuration settings.</param>
    /// <param name="httpFactory">The http client factory used to download attachments.</param>
    [Group("list", "Export and import role membership lists")]
    public class RoleList(BotConfigService config, IHttpClientFactory httpFactory)
        : MewdekoSlashSubmodule<RoleCommandsService>
    {
        private async Task<bool> JobLimitReached()
        {
            if (Service.Jobslist.Count < 5) return false;
            await ErrorAsync(Strings.MassroleJobLimit(ctx.Guild.Id, JobsCommand)).ConfigureAwait(false);
            return true;
        }

        private int NextJobId()
        {
            return Service.Jobslist.Count == 0 ? 1 : Service.Jobslist.Max(x => x.JobId) + 1;
        }

        /// <summary>
        ///     Exports a list of roles and their associated users to a text file.
        /// </summary>
        [SlashCommand("export", "Exports every role and its members to a text file")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageRoles)]
        [RequireBotPermission(GuildPermission.ManageRoles)]
        public async Task ExportRoleList()
        {
            var roles = ctx.Guild.Roles.Where(x => !x.IsManaged && x.Id != ctx.Guild.Id).ToList();
            if (roles.Count == 0)
            {
                await ErrorAsync(Strings.NoManageableRolesFound(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await DeferAsync().ConfigureAwait(false);
            await ConfirmAsync(Strings.ExportingRoleUserList(ctx.Guild.Id, config.Data.LoadingEmote))
                .ConfigureAwait(false);
            var pair = new List<ExportedRoles>();
            foreach (var i in roles.OrderByDescending(x => x.Position))
            {
                var role = i as SocketRole;
                if (role is not null && role.Members.Any())
                    role.Members.ForEach(x => pair.Add(new ExportedRoles
                    {
                        RoleId = i.Id, UserId = x.Id, RoleName = i.Name
                    }));
                else
                    pair.Add(new ExportedRoles
                    {
                        RoleId = i.Id, UserId = 0, RoleName = i.Name
                    });
            }

            var toExport = string.Join("\n", pair.Select(x => $"{x.RoleId},{x.UserId},{x.RoleName}"));
            var toSend = new MemoryStream(Encoding.UTF8.GetBytes(toExport));
            await ctx.Interaction.FollowupWithFileAsync(toSend, "rolelist.txt").ConfigureAwait(false);
            await toSend.DisposeAsync();
        }

        /// <summary>
        ///     Imports roles and user associations from a text file, applying the specified roles to the listed users.
        /// </summary>
        /// <param name="file">The role list file exported earlier.</param>
        /// <param name="newRoles">Whether new roles should be created based on the import data.</param>
        [SlashCommand("import", "Imports a role list file and applies the roles")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageRoles)]
        [RequireBotPermission(GuildPermission.ManageRoles)]
        public async Task ImportRoleList([Summary("file", "The exported role list file")] IAttachment file,
            [Summary("create-roles", "Create the roles from the file instead of matching ids")]
            bool newRoles = false)
        {
            await DeferAsync().ConfigureAwait(false);
            using var client = httpFactory.CreateClient();
            var guildUsers = (await ctx.Guild.GetUsersAsync().ConfigureAwait(false)).ToList();
            var roles = ctx.Guild.Roles.ToList();
            var content = await client.GetStringAsync(file.Url).ConfigureAwait(false);
            var lines = content.ToLines();
            var toProcess = new List<KeyValuePair<IRole, IGuildUser>>();
            var addedRoles = new List<ulong>();
            if (!newRoles)
                toProcess = (from i in lines
                    select i.Split(",")
                    into split
                    let role = roles.FirstOrDefault(x => x.Id == Convert.ToUInt64(split[0]))
                    where role is not null
                    let user = guildUsers.FirstOrDefault(x => x.Id == Convert.ToUInt64(split[1]))
                    where user is not null
                    select new KeyValuePair<IRole, IGuildUser>(role, user)).ToList();
            else
            {
                foreach (var i in lines)
                {
                    var split = i.Split(",");
                    var roleId = Convert.ToUInt64(split[0]);
                    IRole role;
                    if (!addedRoles.Contains(roleId))
                    {
                        role = await ctx.Guild.CreateRoleAsync(split[2], null, null, false, null)
                            .ConfigureAwait(false);
                        roles.Add(role);
                    }
                    else
                        role = roles.FirstOrDefault(x => x.Name == split[2]);

                    addedRoles.Add(roleId);
                    var user = guildUsers.FirstOrDefault(x => x.Id == Convert.ToUInt64(split[1]));
                    if (user is not null && role is not null)
                        toProcess.Add(new KeyValuePair<IRole, IGuildUser>(role, user));
                }
            }

            if (toProcess.Count == 0)
            {
                await ErrorAsync(Strings.NoRolesUsersInFile(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (await JobLimitReached().ConfigureAwait(false)) return;

            for (var index = toProcess.Count - 1; index >= 0; index--)
            {
                var i = toProcess[index];
                if (i.Value.RoleIds.Any() && i.Value.RoleIds.Contains(i.Key.Id))
                    toProcess.RemoveAt(index);
            }

            if (toProcess.Count == 0)
            {
                await ErrorAsync(Strings.AllRolesAlreadyApplied(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var jobId = NextJobId();
            var count = toProcess.Count;
            var addedCount = 0;
            await Service.AddToList(ctx.Guild, ctx.User as IGuildUser, jobId, count, "Importing User Roles", null)
                .ConfigureAwait(false);
            await ConfirmAsync(Strings.ImportingRoleList(ctx.Guild.Id, config.Data.LoadingEmote, count,
                TimeSpan.FromSeconds(count).Humanize())).ConfigureAwait(false);
            foreach (var i in toProcess)
            {
                try
                {
                    var e = Service.JobCheck(ctx.Guild, jobId).FirstOrDefault()?.StoppedOrNot;
                    if (e == "Stopped")
                    {
                        await Service.RemoveJob(ctx.Guild, jobId).ConfigureAwait(false);
                        await ctx.Channel.SendConfirmAsync(Strings.MassroleStopped(ctx.Guild.Id,
                                Strings.ImportedRolesTarget(ctx.Guild.Id), addedCount.ToString("N0"),
                                count.ToString("N0")))
                            .ConfigureAwait(false);
                        return;
                    }

                    await i.Value.AddRoleAsync(i.Key).ConfigureAwait(false);
                    await Service.UpdateCount(ctx.Guild, jobId, addedCount).ConfigureAwait(false);
                    addedCount++;
                }
                catch (HttpException)
                {
                }
            }

            await Service.RemoveJob(ctx.Guild, jobId).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync(Strings.AppliedRoleUserImports(ctx.Guild.Id, addedCount, count))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Adds a specified role to a list of users defined in an attached file, one username or id per line.
        /// </summary>
        /// <param name="role">The role to be added to the users listed in the file.</param>
        /// <param name="file">A text file with one user id, username or nickname per line.</param>
        [SlashCommand("add", "Adds a role to every user listed in a text file")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageRoles)]
        [RequireBotPermission(GuildPermission.ManageRoles)]
        public async Task AddRoleToList([Summary("role", "The role to add")] IRole role,
            [Summary("file", "Text file with one user id or name per line")]
            IAttachment file)
        {
            var runnerUser = (IGuildUser)ctx.User;
            var currentUser = await ctx.Guild.GetUserAsync(ctx.Client.CurrentUser.Id).ConfigureAwait(false);
            if (ctx.User.Id != ctx.Guild.OwnerId && runnerUser.GetRoles().Max(x => x.Position) <= role.Position)
            {
                await ErrorAsync(Strings.CannotManageRole(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (currentUser.GetRoles().Max(x => x.Position) <= role.Position)
            {
                await ErrorAsync(Strings.BotCannotManageRole(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (await JobLimitReached().ConfigureAwait(false)) return;

            await DeferAsync().ConfigureAwait(false);
            using var client = httpFactory.CreateClient();
            var guildUsers = (await ctx.Guild.GetUsersAsync().ConfigureAwait(false)).ToList();
            var actualUsers = new List<IGuildUser>();
            var content = await client.GetStringAsync(file.Url).ConfigureAwait(false);
            var fileUsers = content.ToLines();
            var ulongIds = new List<ulong>();
            var stringUsers = new List<string>();
            foreach (var i in fileUsers)
            {
                if (ulong.TryParse(i, out var id))
                    ulongIds.Add(id);
                else
                    stringUsers.Add(i);
            }

            foreach (var i in stringUsers)
            {
                var user = guildUsers.FirstOrDefault(x =>
                    x.Username.Equals(i, StringComparison.OrdinalIgnoreCase) ||
                    x.DisplayName.Equals(i, StringComparison.OrdinalIgnoreCase) ||
                    x.Nickname != null && x.Nickname.Equals(i, StringComparison.OrdinalIgnoreCase));
                if (user is null)
                    continue;
                actualUsers.Add(user);
            }

            foreach (var i in ulongIds)
            {
                var user = guildUsers.FirstOrDefault(x => x.Id == i);
                if (user is null)
                    continue;
                actualUsers.Add(user);
            }

            actualUsers = actualUsers.Where(x => !x.GetRoles().Contains(role)).ToList();
            var count = actualUsers.Count;
            if (count == 0)
            {
                await ErrorAsync(Strings.AllUsersHaveRole(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var jobId = NextJobId();
            await Service.AddToList(ctx.Guild, ctx.User as IGuildUser, jobId, count, "Adding to Users in List",
                role).ConfigureAwait(false);
            var count2 = 0;
            await ConfirmAsync(Strings.MassroleAdding(ctx.Guild.Id, role.Mention, count,
                TimeSpan.FromSeconds(count).Humanize())).ConfigureAwait(false);
            foreach (var i in actualUsers)
            {
                try
                {
                    var e = Service.JobCheck(ctx.Guild, jobId).FirstOrDefault()?.StoppedOrNot;
                    if (e == "Stopped")
                    {
                        await Service.RemoveJob(ctx.Guild, jobId).ConfigureAwait(false);
                        await ctx.Channel.SendConfirmAsync(Strings.MassroleStopped(ctx.Guild.Id, role.Mention,
                            count2.ToString("N0"), count.ToString("N0"))).ConfigureAwait(false);
                        return;
                    }

                    await i.AddRoleAsync(role).ConfigureAwait(false);
                    await Service.UpdateCount(ctx.Guild, jobId, count2).ConfigureAwait(false);
                    count2++;
                }
                catch (HttpException)
                {
                }
            }

            await Service.RemoveJob(ctx.Guild, jobId).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync(Strings.AppliedRoleToUsers(ctx.Guild.Id, role.Mention, count2, count))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Represents a structure to hold exported role information.
        /// </summary>
        public record ExportedRoles
        {
            /// <summary>
            ///     Gets or sets the unique identifier for the role.
            /// </summary>
            public ulong RoleId { get; set; }

            /// <summary>
            ///     Gets or sets the unique identifier for the user.
            /// </summary>
            public ulong UserId { get; set; }

            /// <summary>
            ///     Gets or sets the name of the role.
            /// </summary>
            public string RoleName { get; set; }
        }
    }
}