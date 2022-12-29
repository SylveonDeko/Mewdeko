using System.Threading.Tasks;
using Discord.Commands;
using Discord.Net;
using Humanizer;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.Server_Management.Services;
using Mewdeko.Services.Settings;

namespace Mewdeko.Modules.Server_Management;

public partial class ServerManagement
{
    [Group]
    public class RoleCommands : MewdekoSubmodule<RoleCommandsService>
    {
        private readonly GuildSettingsService guildSettings;
        private readonly BotConfigService config;

        public RoleCommands(GuildSettingsService guildSettings, BotConfigService config)
        {
            this.guildSettings = guildSettings;
            this.config = config;
        }

        [Cmd, Aliases, UserPerm(GuildPermission.ManageRoles), BotPerm(ChannelPermission.ManageRoles)]
        public async Task CreateRoles([Remainder] string roles)
        {
            var roleList = roles.Split(" ");
            if (await PromptUserConfirmAsync($"Are you sure you want to create {roleList.Length} roles with these names?\n{string.Join("\n", roleList)}", ctx.User.Id))
            {
                var msg = await ctx.Channel.SendConfirmAsync($"{config.Data.LoadingEmote} Creating {roleList.Length} roles...");
                foreach (var i in roleList)
                {
                    await ctx.Guild.CreateRoleAsync(i, null, null, false, false);
                }

                await msg.ModifyAsync(x =>
                {
                    x.Embed = new EmbedBuilder()
                        .WithOkColor()
                        .WithDescription($"{config.Data.SuccessEmote} Created {roleList.Length} roles!").Build();
                });
            }
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageChannels), BotPerm(GuildPermission.ManageChannels)]
        public async Task SyncRoleToAll(IRole role)
        {
            var ch = ctx.Channel as ITextChannel;
            var perms = ch.GetPermissionOverwrite(role);
            if (perms is null)
            {
                await ctx.Channel.SendErrorAsync("This role doesnt have perms setup in this channel!").ConfigureAwait(false);
                return;
            }

            var msg = await ctx.Channel.SendConfirmAsync(
                    $"{config.Data.LoadingEmote} Syncing permissions from {role.Mention} to {(await ctx.Guild.GetTextChannelsAsync().ConfigureAwait(false)).Count(x => x is not SocketThreadChannel)} Channels and {(await ctx.Guild.GetTextChannelsAsync().ConfigureAwait(false)).Count(x => x is not SocketThreadChannel)} Categories.....")
                .ConfigureAwait(false);
            foreach (var i in (await ctx.Guild.GetChannelsAsync().ConfigureAwait(false)).Where(x => x is not SocketThreadChannel or SocketVoiceChannel))
            {
                if (perms != null)
                    await i.AddPermissionOverwriteAsync(role, (OverwritePermissions)perms).ConfigureAwait(false);
            }

            foreach (var i in await ctx.Guild.GetCategoriesAsync().ConfigureAwait(false))
            {
                if (perms != null)
                    await i.AddPermissionOverwriteAsync(role, (OverwritePermissions)perms).ConfigureAwait(false);
            }

            var eb = new EmbedBuilder
            {
                Color = Mewdeko.OkColor,
                Description =
                    $"Succesfully synced perms from {role.Mention} to {(await ctx.Guild.GetTextChannelsAsync().ConfigureAwait(false)).Count(x => x is not SocketThreadChannel)} channels and {(await ctx.Guild.GetCategoriesAsync().ConfigureAwait(false)).Count} Categories!!"
            };
            await msg.ModifyAsync(x => x.Embed = eb.Build()).ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageChannels), BotPerm(GuildPermission.ManageChannels)]
        public async Task SyncRoleToAllChannels(IRole role)
        {
            var ch = ctx.Channel as ITextChannel;
            var perms = ch.GetPermissionOverwrite(role);
            if (perms is null)
            {
                await ctx.Channel.SendErrorAsync("This role doesnt have perms setup in this channel!").ConfigureAwait(false);
                return;
            }

            var msg = await ctx.Channel.SendConfirmAsync(
                    $"{config.Data.LoadingEmote} Syncing permissions from {role.Mention} to {(await ctx.Guild.GetTextChannelsAsync().ConfigureAwait(false)).Count(x => x is not SocketThreadChannel)} Channels.....")
                .ConfigureAwait(false);
            foreach (var i in (await ctx.Guild.GetTextChannelsAsync().ConfigureAwait(false)).Where(x => x is not SocketThreadChannel))
            {
                if (perms != null)
                    await i.AddPermissionOverwriteAsync(role, (OverwritePermissions)perms).ConfigureAwait(false);
            }

            var eb = new EmbedBuilder
            {
                Color = Mewdeko.OkColor,
                Description =
                    $"Succesfully synced perms from {role.Mention} to {(await ctx.Guild.GetTextChannelsAsync().ConfigureAwait(false)).Count(x => x is not SocketThreadChannel)} Channels!"
            };
            await msg.ModifyAsync(x => x.Embed = eb.Build()).ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageChannels), BotPerm(GuildPermission.ManageChannels)]
        public async Task SyncRoleToAllCategories(IRole role)
        {
            var ch = ctx.Channel as ITextChannel;
            var perms = ch.GetPermissionOverwrite(role);
            if (perms is null)
            {
                await ctx.Channel.SendErrorAsync("This role doesnt have perms setup in this channel!").ConfigureAwait(false);
                return;
            }

            var msg = await ctx.Channel.SendConfirmAsync(
                    $"{config.Data.LoadingEmote} Syncing permissions from {role.Mention} to {(await ctx.Guild.GetCategoriesAsync().ConfigureAwait(false)).Count} Categories.....")
                .ConfigureAwait(false);
            foreach (var i in await ctx.Guild.GetCategoriesAsync().ConfigureAwait(false))
            {
                if (perms != null)
                    await i.AddPermissionOverwriteAsync(role, (OverwritePermissions)perms).ConfigureAwait(false);
            }

            var eb = new EmbedBuilder
            {
                Color = Mewdeko.OkColor,
                Description =
                    $"Succesfully synced perms from {role.Mention} to {(await ctx.Guild.GetCategoriesAsync().ConfigureAwait(false)).Count} Categories!"
            };
            await msg.ModifyAsync(x => x.Embed = eb.Build()).ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageRoles), BotPerm(GuildPermission.ManageRoles)]
        public async Task DeleteRoles(params IRole[] roles)
        {
            if (roles.Count(x => !x.IsManaged) is 0)
            {
                await ctx.Channel.SendErrorAsync("You cannot delete bot roles or boost roles!").ConfigureAwait(false);
                return;
            }

            var secondlist = new List<string>();
            var runnerUser = (IGuildUser)ctx.User;
            var currentUser = await ctx.Guild.GetUserAsync(ctx.Client.CurrentUser.Id).ConfigureAwait(false);
            foreach (var i in roles.Where(x => !x.IsManaged))
            {
                if (ctx.User.Id != runnerUser.Guild.OwnerId &&
                    runnerUser.GetRoles().Max(x => x.Position) <= i.Position)
                {
                    await ctx.Channel.SendErrorAsync($"You cannot manage {i.Mention}").ConfigureAwait(false);
                    return;
                }

                if (currentUser.GetRoles().Max(x => x.Position) <= i.Position)
                {
                    await ctx.Channel.SendErrorAsync($"I cannot manage {i.Mention}").ConfigureAwait(false);
                    return;
                }

                secondlist.Add(
                    $"{i.Mention} - {(await ctx.Guild.GetUsersAsync().ConfigureAwait(false)).Count(x => x.RoleIds.Contains(i.Id))} Users");
            }

            var embed = new EmbedBuilder
            {
                Title = "Are you sure you want to delete these roles?", Description = $"{string.Join("\n", secondlist)}"
            };
            if (await PromptUserConfirmAsync(embed, ctx.User.Id).ConfigureAwait(false))
            {
                var msg = await ctx.Channel.SendConfirmAsync($"{config.Data.LoadingEmote} Deleting {roles.Length} roles...").ConfigureAwait(false);
                foreach (var i in roles) await i.DeleteAsync().ConfigureAwait(false);
                var newemb = new EmbedBuilder
                {
                    Description = $"Succesfully deleted {roles.Length} roles!", Color = Mewdeko.OkColor
                };
                await msg.ModifyAsync(x => x.Embed = newemb.Build()).ConfigureAwait(false);
            }
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageRoles), BotPerm(GuildPermission.ManageRoles)]
        public async Task StopJob(int jobnum)
        {
            var list = Service.Jobslist
                .Find(x => x.JobId == jobnum && x.GuildId == ctx.Guild.Id);
            if (list == null)
            {
                await ctx.Channel.SendErrorAsync(
                    $"No job with that ID exists, please check the list again with `{await guildSettings.GetPrefix(ctx.Guild)}rolejobs`").ConfigureAwait(false);
                return;
            }

            var eb = new EmbedBuilder
            {
                Color = Mewdeko.OkColor, Description = "Are you sure you want to stop this job?"
            };
            eb.AddField(list.JobType,
                $"Started by {list.StartedBy.Mention}\nProgress: {list.AddedTo}/{list.TotalUsers}");
            if (!await PromptUserConfirmAsync(eb, ctx.User.Id).ConfigureAwait(false))
            {
                var msg = await ctx.Channel.SendConfirmAsync("Job Stop Cancelled.").ConfigureAwait(false);
                msg.DeleteAfter(5);
                return;
            }

            await Service.StopJob(ctx.Channel as ITextChannel, jobnum, ctx.Guild).ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageRoles), BotPerm(GuildPermission.ManageRoles)]
        public async Task SetRoles(IGuildUser user, params IRole[] roles)
        {
            foreach (var i in roles)
            {
                if (ctx.User.Id != ctx.Guild.OwnerId &&
                    ((IGuildUser)ctx.User).GetRoles().Max(x => x.Position) <= i.Position)
                {
                    await ctx.Channel.SendErrorAsync($"You cannot manage the role {i.Mention}").ConfigureAwait(false);
                    return;
                }

                if (((IGuildUser)ctx.User).GetRoles().Max(x => x.Position) > i.Position) continue;
                await ctx.Channel.SendErrorAsync($"I cannot manage the role {i.Mention}!").ConfigureAwait(false);
                return;
            }

            await user.AddRolesAsync(roles).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync(
                $"{user} has been given the roles:\n{string.Join<string>("|", roles.Select(x => x.Mention))}").ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.ManageRoles), BotPerm(GuildPermission.ManageRoles)]
        public async Task AddUsersToRole(IRole role, params IUser[] users)
        {
            if (ctx.User.Id != ctx.Guild.OwnerId &&
                ((IGuildUser)ctx.User).GetRoles().Max(x => x.Position) <= role.Position)
            {
                await ctx.Channel.SendErrorAsync("You cannot manage this role!").ConfigureAwait(false);
                return;
            }

            if (((IGuildUser)ctx.User).GetRoles().Max(x => x.Position) <= role.Position)
            {
                await ctx.Channel.SendErrorAsync("I cannot manage this role!").ConfigureAwait(false);
                return;
            }

            foreach (var i in users.Select(x => x as IGuildUser))
            {
                await i.AddRoleAsync(role).ConfigureAwait(false);
            }

            await ctx.Channel.SendConfirmAsync(
                $"{role.Mention} has had the following users added:\n{string.Join<string>("|", users.Select(x => x.Mention))}").ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.ManageRoles), BotPerm(GuildPermission.ManageRoles)]
        public async Task RemoveUsersFromRole(IRole role, params IUser[] users)
        {
            if (ctx.User.Id != ctx.Guild.OwnerId &&
                ((IGuildUser)ctx.User).GetRoles().Max(x => x.Position) <= role.Position)
            {
                await ctx.Channel.SendErrorAsync("You cannot manage this role!").ConfigureAwait(false);
                return;
            }

            if (((IGuildUser)ctx.User).GetRoles().Max(x => x.Position) <= role.Position)
            {
                await ctx.Channel.SendErrorAsync("I cannot manage this role!").ConfigureAwait(false);
                return;
            }

            foreach (var i in users.Select(x => x as IGuildUser))
            {
                await i.AddRoleAsync(role).ConfigureAwait(false);
            }

            await ctx.Channel.SendConfirmAsync(
                $"{role.Mention} has had the following users removed:\n{string.Join<string>("|", users.Select(x => x.Mention))}").ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageRoles), BotPerm(GuildPermission.ManageRoles)]
        public async Task RemoveRoles(IGuildUser user, params IRole[] roles)
        {
            foreach (var i in roles)
            {
                if (ctx.User.Id != ctx.Guild.OwnerId &&
                    ((IGuildUser)ctx.User).GetRoles().Max(x => x.Position) <= i.Position)
                {
                    await ctx.Channel.SendErrorAsync($"You cannot manage the role {i.Mention}").ConfigureAwait(false);
                    return;
                }

                if (((IGuildUser)ctx.User).GetRoles().Max(x => x.Position) > i.Position) continue;
                await ctx.Channel.SendErrorAsync($"I cannot manage the role {i.Mention}!").ConfigureAwait(false);
                return;
            }

            await user.RemoveRolesAsync(roles).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync(
                $"{user} has had the following roles removed:\n{string.Join<string>("|", roles.Select(x => x.Mention))}").ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageRoles), BotPerm(GuildPermission.ManageRoles)]
        public async Task RoleJobs()
        {
            var list = Service.Jobslist;
            if (list.Count == 0)
            {
                await ctx.Channel.SendErrorAsync("No Mass Role Operations running!").ConfigureAwait(false);
                return;
            }

            var eb = new EmbedBuilder
            {
                Title = $"{list.Count} Mass Role Operations Running", Color = Mewdeko.OkColor
            };
            foreach (var i in list)
            {
                if (i.Role2 is not null && i.JobType != "Adding then Removing a Role")
                {
                    eb.AddField($"Job {i.JobId}",
                        $"Job Type: {i.JobType}\nStarted By: {i.StartedBy.Mention}\nProgress: {i.AddedTo}/{i.TotalUsers}\nFirst Role:{i.Role1.Mention}\nSecond Role:{i.Role2.Mention}");
                }

                if (i.Role2 is not null && i.JobType == "Adding then Removing a Role")
                {
                    eb.AddField($"Job {i.JobId}",
                        $"Job Type: {i.JobType}\nStarted By: {i.StartedBy.Mention}\nProgress: {i.AddedTo}/{i.TotalUsers}\nRemoving Role:{i.Role2.Mention}\nAdding Role:{i.Role1.Mention}");
                }
                else
                {
                    eb.AddField($"Job {i.JobId}",
                        $"Job Type: {i.JobType}\nStarted By: {i.StartedBy.Mention}\nProgress: {i.AddedTo}/{i.TotalUsers}\nRole:{i.Role1.Mention}");
                }
            }

            await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageRoles), BotPerm(GuildPermission.ManageRoles)]
        public async Task AddToAll(IRole role)
        {
            await Task.Delay(500).ConfigureAwait(false);
            var runnerUser = (IGuildUser)ctx.User;
            var currentUser = await ctx.Guild.GetUserAsync(ctx.Client.CurrentUser.Id).ConfigureAwait(false);
            if (ctx.User.Id != runnerUser.Guild.OwnerId &&
                runnerUser.GetRoles().Max(x => x.Position) <= role.Position)
            {
                await ctx.Channel.SendErrorAsync("You cannot manage this role!").ConfigureAwait(false);
                return;
            }

            if (currentUser.GetRoles().Max(x => x.Position) <= role.Position)
            {
                await ctx.Channel.SendErrorAsync("I cannot manage this role!").ConfigureAwait(false);
                return;
            }

            if (Service.Jobslist.Count == 5)
            {
                await ctx.Channel.SendErrorAsync(
                        $"Due to discord rate limits you may only have 5 mass role operations at a time, check your current jobs with `{await guildSettings.GetPrefix(ctx.Guild)}rolejobs`.")
                    .ConfigureAwait(false);
                return;
            }

            var guild = ctx.Guild as SocketGuild;
            var users = guild.Users.Where(c => !c.Roles.Contains(role));
            var count = users.Count();
            if (!users.Any())
            {
                await ctx.Channel.SendErrorAsync("All users already have this role!").ConfigureAwait(false);
                return;
            }

            int jobId;
            if (Service.Jobslist.FirstOrDefault() is null)
                jobId = 1;
            else
                jobId = Service.Jobslist.Count + 1;
            await Service.AddToList(ctx.Guild, ctx.User as IGuildUser, jobId, count, "Adding to Users and Bots",
                role).ConfigureAwait(false);
            var count2 = 0;
            await ctx.Channel.SendConfirmAsync(
                $"Adding {role.Mention} to {count} Members.\nThis will take about {TimeSpan.FromSeconds(users.Count()).Humanize()}.").ConfigureAwait(false);
            using (ctx.Channel.EnterTypingState())
            {
                foreach (var i in users)
                {
                    try
                    {
                        var e = Service.JobCheck(ctx.Guild, jobId).FirstOrDefault().StoppedOrNot;
                        var t = e == "Stopped";
                        if (t)
                        {
                            await Service.RemoveJob(ctx.Guild, jobId).ConfigureAwait(false);
                            await ctx.Channel.SendConfirmAsync(
                                $"Massrole Stopped.\nApplied {role.Mention} to {count2} out of {count} members before stopped.").ConfigureAwait(false);
                            return;
                        }

                        await i.AddRoleAsync(role).ConfigureAwait(false);
                        await Service.UpdateCount(ctx.Guild, jobId, count2).ConfigureAwait(false);
                        count2++;
                    }
                    catch (HttpException)
                    {
                        //ignored
                    }
                }
            }

            await Service.RemoveJob(ctx.Guild, jobId).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync($"Applied {role.Mention} to {count2} out of {count} members!").ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageRoles), BotPerm(GuildPermission.ManageRoles)]
        public async Task AddToAllBots(IRole role)
        {
            var runnerUser = (IGuildUser)ctx.User;
            var currentUser = await ctx.Guild.GetUserAsync(ctx.Client.CurrentUser.Id).ConfigureAwait(false);
            if (ctx.User.Id != runnerUser.Guild.OwnerId &&
                runnerUser.GetRoles().Max(x => x.Position) <= role.Position)
            {
                await ctx.Channel.SendErrorAsync("You cannot manage this role!").ConfigureAwait(false);
                return;
            }

            if (currentUser.GetRoles().Max(x => x.Position) <= role.Position)
            {
                await ctx.Channel.SendErrorAsync("I cannot manage this role!").ConfigureAwait(false);
                return;
            }

            if (Service.Jobslist.Count == 5)
            {
                await ctx.Channel.SendErrorAsync(
                        $"Due to discord rate limits you may only have 5 mass role operations at a time, check your current jobs with `{await guildSettings.GetPrefix(ctx.Guild)}rolejobs`.")
                    .ConfigureAwait(false);
                return;
            }

            var guild = ctx.Guild as SocketGuild;
            var users = guild.Users.Where(c => !c.Roles.Contains(role) && c.IsBot);
            var count = users.Count();
            if (!users.Any())
            {
                await ctx.Channel.SendErrorAsync("All bots already have this role!").ConfigureAwait(false);
                return;
            }

            int jobId;
            if (Service.Jobslist.FirstOrDefault() is null)
                jobId = 1;
            else
                jobId = Service.Jobslist.FirstOrDefault().JobId + 1;
            await Service.AddToList(ctx.Guild, ctx.User as IGuildUser, jobId, count, "Adding to Bots Only", role).ConfigureAwait(false);
            var count2 = 0;
            await ctx.Channel.SendConfirmAsync(
                $"Adding {role.Mention} to {count} Members.\nThis will take about {TimeSpan.FromSeconds(users.Count()).Humanize()}.").ConfigureAwait(false);
            using (ctx.Channel.EnterTypingState())
            {
                foreach (var i in users)
                {
                    try
                    {
                        var e = Service.JobCheck(ctx.Guild, jobId).FirstOrDefault().StoppedOrNot;
                        var t = e == "Stopped";
                        if (t)
                        {
                            await Service.RemoveJob(ctx.Guild, jobId).ConfigureAwait(false);
                            await ctx.Channel.SendConfirmAsync(
                                $"Massrole Stopped.\nApplied {role.Mention} to {count2} out of {count} bots before stopped.").ConfigureAwait(false);
                            return;
                        }

                        await i.AddRoleAsync(role).ConfigureAwait(false);
                        await Service.UpdateCount(ctx.Guild, jobId, count2).ConfigureAwait(false);
                        count2++;
                    }
                    catch (HttpException)
                    {
                        //ignored
                    }
                }
            }

            await Service.RemoveJob(ctx.Guild, jobId).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync($"Applied {role.Mention} to {count2} out of {count} bots!").ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageRoles), BotPerm(GuildPermission.ManageRoles)]
        public async Task AddToAllUsers(IRole role)
        {
            var runnerUser = (IGuildUser)ctx.User;
            var currentUser = await ctx.Guild.GetUserAsync(ctx.Client.CurrentUser.Id).ConfigureAwait(false);
            if (ctx.User.Id != runnerUser.Guild.OwnerId &&
                runnerUser.GetRoles().Max(x => x.Position) <= role.Position)
            {
                await ctx.Channel.SendErrorAsync("You cannot manage this role!").ConfigureAwait(false);
                return;
            }

            if (currentUser.GetRoles().Max(x => x.Position) <= role.Position)
            {
                await ctx.Channel.SendErrorAsync("I cannot manage this role!").ConfigureAwait(false);
                return;
            }

            if (Service.Jobslist.Count == 5)
            {
                await ctx.Channel.SendErrorAsync(
                        $"Due to discord rate limits you may only have 5 mass role operations at a time, check your current jobs with `{await guildSettings.GetPrefix(ctx.Guild)}rolejobs`.")
                    .ConfigureAwait(false);
                return;
            }

            int jobId;
            if (Service.Jobslist.FirstOrDefault() is null)
                jobId = 1;
            else
                jobId = Service.Jobslist.FirstOrDefault().JobId + 1;
            var guild = ctx.Guild as SocketGuild;
            var users = guild.Users.Where(c => !c.Roles.Contains(role) && !c.IsBot);
            var count = users.Count();
            if (!users.Any())
            {
                await ctx.Channel.SendErrorAsync("All users already have this role!").ConfigureAwait(false);
                return;
            }

            await Service.AddToList(ctx.Guild, ctx.User as IGuildUser, jobId, count, "Adding to Users Only", role).ConfigureAwait(false);
            var count2 = 0;
            await ctx.Channel.SendConfirmAsync(
                $"Adding {role.Mention} to {count} users.\n + This will take about {TimeSpan.FromSeconds(users.Count()).Humanize()}.").ConfigureAwait(false);
            using (ctx.Channel.EnterTypingState())
            {
                foreach (var i in users)
                {
                    try
                    {
                        var e = Service.JobCheck(ctx.Guild, jobId).FirstOrDefault().StoppedOrNot;
                        var t = e == "Stopped";
                        if (t)
                        {
                            await Service.RemoveJob(ctx.Guild, jobId).ConfigureAwait(false);
                            await ctx.Channel.SendConfirmAsync(
                                $"Massrole Stopped.\nApplied {role.Mention} to {count2} out of {count} users before stopped.").ConfigureAwait(false);
                            return;
                        }

                        await i.AddRoleAsync(role).ConfigureAwait(false);
                        await Service.UpdateCount(ctx.Guild, jobId, count2).ConfigureAwait(false);
                        count2++;
                    }
                    catch (HttpException)
                    {
                        //ignored
                    }
                }
            }

            await Service.RemoveJob(ctx.Guild, jobId).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync($"Applied {role.Mention} to {count2} out of {count} users!").ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageRoles), BotPerm(GuildPermission.ManageRoles)]
        public async Task AddToUsersOver(StoopidTime time, IRole role)
        {
            var runnerUser = (IGuildUser)ctx.User;
            var currentUser = await ctx.Guild.GetUserAsync(ctx.Client.CurrentUser.Id).ConfigureAwait(false);
            if (ctx.User.Id != runnerUser.Guild.OwnerId &&
                runnerUser.GetRoles().Max(x => x.Position) <= role.Position)
            {
                await ctx.Channel.SendErrorAsync("You cannot manage this role!").ConfigureAwait(false);
                return;
            }

            if (currentUser.GetRoles().Max(x => x.Position) <= role.Position)
            {
                await ctx.Channel.SendErrorAsync("I cannot manage this role!").ConfigureAwait(false);
                return;
            }

            if (Service.Jobslist.Count == 5)
            {
                await ctx.Channel.SendErrorAsync(
                        $"Due to discord rate limits you may only have 5 mass role operations at a time, check your current jobs with `{await guildSettings.GetPrefix(ctx.Guild)}rolejobs`.")
                    .ConfigureAwait(false);
                return;
            }

            var guild = ctx.Guild as SocketGuild;
            var users = guild.Users.Where(c =>
                !c.Roles.Contains(role) && !c.IsBot &&
                DateTimeOffset.Now.Subtract(c.JoinedAt.Value) >= time.Time);
            var count = users.Count();
            if (!users.Any())
            {
                await ctx.Channel.SendErrorAsync("All users at this account age already have this role!").ConfigureAwait(false);
                return;
            }

            int jobId;
            if (Service.Jobslist.FirstOrDefault() is null)
                jobId = 1;
            else
                jobId = Service.Jobslist.FirstOrDefault().JobId + 1;
            await Service.AddToList(ctx.Guild, ctx.User as IGuildUser, jobId, count,
                $"Adding a role to server members that have been here for {time.Time.Humanize()}", role).ConfigureAwait(false);
            var count2 = 0;
            await ctx.Channel.SendConfirmAsync(
                    $"Adding {role.Mention} to {count} users who have acounts that are equal to or older than {time.Time.Humanize()} old..\nThis will take about {TimeSpan.FromSeconds(users.Count()).Humanize()}.")
                .ConfigureAwait(false);
            using (ctx.Channel.EnterTypingState())
            {
                foreach (var i in users)
                {
                    try
                    {
                        var e = Service.JobCheck(ctx.Guild, jobId).FirstOrDefault().StoppedOrNot;
                        var t = e == "Stopped";
                        if (t)
                        {
                            await Service.RemoveJob(ctx.Guild, jobId).ConfigureAwait(false);
                            await ctx.Channel.SendConfirmAsync(
                                $"Massrole Stopped.\nApplied {role.Mention} to {count2} out of {count} users before stopped.").ConfigureAwait(false);
                            return;
                        }

                        await i.AddRoleAsync(role).ConfigureAwait(false);
                        await Service.UpdateCount(ctx.Guild, jobId, count2).ConfigureAwait(false);
                        count2++;
                    }
                    catch (HttpException)
                    {
                        //ignored
                    }
                }
            }

            await Service.RemoveJob(ctx.Guild, jobId).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync($"Applied {role.Mention} to {count2} out of {count} users!").ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageRoles), BotPerm(GuildPermission.ManageRoles)]
        public async Task AddToUsersUnder(StoopidTime time, IRole role)
        {
            var runnerUser = (IGuildUser)ctx.User;
            var currentUser = await ctx.Guild.GetUserAsync(ctx.Client.CurrentUser.Id).ConfigureAwait(false);
            if (ctx.User.Id != runnerUser.Guild.OwnerId &&
                runnerUser.GetRoles().Max(x => x.Position) <= role.Position)
            {
                await ctx.Channel.SendErrorAsync("You cannot manage this role!").ConfigureAwait(false);
                return;
            }

            if (currentUser.GetRoles().Max(x => x.Position) <= role.Position)
            {
                await ctx.Channel.SendErrorAsync("I cannot manage this role!").ConfigureAwait(false);
                return;
            }

            if (Service.Jobslist.Count == 5)
            {
                await ctx.Channel.SendErrorAsync(
                        $"Due to discord rate limits you may only have 5 mass role operations at a time, check your current jobs with `{await guildSettings.GetPrefix(ctx.Guild)}rolejobs`.")
                    .ConfigureAwait(false);
                return;
            }

            var guild = ctx.Guild as SocketGuild;
            var users = guild.Users.Where(c =>
                !c.Roles.Contains(role) && !c.IsBot &&
                DateTimeOffset.Now.Subtract(c.JoinedAt.Value) < time.Time);
            var count = users.Count();
            if (!users.Any())
            {
                await ctx.Channel.SendErrorAsync("All users at this account age already have this role!").ConfigureAwait(false);
                return;
            }

            int jobId;
            if (Service.Jobslist.FirstOrDefault() is null)
                jobId = 1;
            else
                jobId = Service.Jobslist.FirstOrDefault().JobId + 1;
            await Service.AddToList(ctx.Guild, ctx.User as IGuildUser, jobId, count,
                $"Adding a role to server members that have been here for {time.Time.Humanize()} or less", role).ConfigureAwait(false);
            var count2 = 0;
            await ctx.Channel.SendConfirmAsync(
                    $"Adding {role.Mention} to {count} users who have acounts that are less than {time.Time.Humanize()} old..\nThis will take about {TimeSpan.FromSeconds(users.Count()).Humanize()}.")
                .ConfigureAwait(false);
            using (ctx.Channel.EnterTypingState())
            {
                foreach (var i in users)
                {
                    try
                    {
                        var e = Service.JobCheck(ctx.Guild, jobId).FirstOrDefault().StoppedOrNot;
                        var t = e == "Stopped";
                        if (t)
                        {
                            await Service.RemoveJob(ctx.Guild, jobId).ConfigureAwait(false);
                            await ctx.Channel.SendConfirmAsync(
                                $"Massrole Stopped.\nApplied {role.Mention} to {count2} out of {count} users before stopped.").ConfigureAwait(false);
                            return;
                        }

                        await i.AddRoleAsync(role).ConfigureAwait(false);
                        await Service.UpdateCount(ctx.Guild, jobId, count2).ConfigureAwait(false);
                        count2++;
                    }
                    catch (HttpException)
                    {
                        //ignored
                    }
                }
            }

            await ctx.Channel.SendConfirmAsync($"Applied {role.Mention} to {count2} out of {count} users!").ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageRoles), BotPerm(GuildPermission.ManageRoles)]
        public async Task RemoveFromAll(IRole role)
        {
            var runnerUser = (IGuildUser)ctx.User;
            var currentUser = await ctx.Guild.GetUserAsync(ctx.Client.CurrentUser.Id).ConfigureAwait(false);
            if (ctx.User.Id != runnerUser.Guild.OwnerId &&
                runnerUser.GetRoles().Max(x => x.Position) <= role.Position)
            {
                await ctx.Channel.SendErrorAsync("You cannot manage this role!").ConfigureAwait(false);
                return;
            }

            if (currentUser.GetRoles().Max(x => x.Position) <= role.Position)
            {
                await ctx.Channel.SendErrorAsync("I cannot manage this role!").ConfigureAwait(false);
                return;
            }

            if (Service.Jobslist.Count == 5)
            {
                await ctx.Channel.SendErrorAsync(
                        $"Due to discord rate limits you may only have 5 mass role operations at a time, check your current jobs with `{await guildSettings.GetPrefix(ctx.Guild)}rolejobs`.")
                    .ConfigureAwait(false);
                return;
            }

            var guild = ctx.Guild as SocketGuild;
            var users = guild.Users.Where(c => c.Roles.Contains(role));
            var count = users.Count();
            if (!users.Any())
            {
                await ctx.Channel.SendErrorAsync("No users have this role!").ConfigureAwait(false);
                return;
            }

            int jobId;
            if (Service.Jobslist.FirstOrDefault() is null)
                jobId = 1;
            else
                jobId = Service.Jobslist.FirstOrDefault().JobId + 1;
            await Service.AddToList(ctx.Guild, ctx.User as IGuildUser, jobId, count,
                "Removing a role from all server members", role).ConfigureAwait(false);
            var count2 = 0;
            await ctx.Channel.SendConfirmAsync(
                $"Removing {role.Mention} from {count} Members.\n + This will take about {TimeSpan.FromSeconds(users.Count()).Humanize()}.").ConfigureAwait(false);
            using (ctx.Channel.EnterTypingState())
            {
                foreach (var i in users)
                {
                    try
                    {
                        var e = Service.JobCheck(ctx.Guild, jobId).FirstOrDefault().StoppedOrNot;
                        var t = e == "Stopped";
                        if (t)
                        {
                            await Service.RemoveJob(ctx.Guild, jobId).ConfigureAwait(false);
                            await ctx.Channel.SendConfirmAsync(
                                $"Massrole Stopped.\nRemoved {role.Mention} from {count2} out of {count} server members before stopped.").ConfigureAwait(false);
                            return;
                        }

                        await i.RemoveRoleAsync(role).ConfigureAwait(false);
                        await Service.UpdateCount(ctx.Guild, jobId, count2).ConfigureAwait(false);
                        count2++;
                    }
                    catch (HttpException)
                    {
                        //ignored
                    }
                }
            }

            await Service.RemoveJob(ctx.Guild, jobId).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync($"Removed {role.Mention} from {count2} out of {count} members!").ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageRoles), BotPerm(GuildPermission.ManageRoles)]
        public async Task RemoveFromAllUsers(IRole role)
        {
            var runnerUser = (IGuildUser)ctx.User;
            var currentUser = await ctx.Guild.GetUserAsync(ctx.Client.CurrentUser.Id).ConfigureAwait(false);
            if (ctx.User.Id != runnerUser.Guild.OwnerId &&
                runnerUser.GetRoles().Max(x => x.Position) <= role.Position)
            {
                await ctx.Channel.SendErrorAsync("You cannot manage this role!").ConfigureAwait(false);
                return;
            }

            if (currentUser.GetRoles().Max(x => x.Position) <= role.Position)
            {
                await ctx.Channel.SendErrorAsync("I cannot manage this role!").ConfigureAwait(false);
                return;
            }

            if (Service.Jobslist.Count == 5)
            {
                await ctx.Channel.SendErrorAsync(
                        $"Due to discord rate limits you may only have 5 mass role operations at a time, check your current jobs with `{await guildSettings.GetPrefix(ctx.Guild)}rolejobs`.")
                    .ConfigureAwait(false);
                return;
            }

            var guild = ctx.Guild as SocketGuild;
            var users = guild.Users.Where(c => c.Roles.Contains(role) && !c.IsBot);
            var count = users.Count();
            if (!users.Any())
            {
                await ctx.Channel.SendErrorAsync("No users have this role!").ConfigureAwait(false);
                return;
            }

            int jobId;
            if (Service.Jobslist.FirstOrDefault() is null)
                jobId = 1;
            else
                jobId = Service.Jobslist.FirstOrDefault().JobId + 1;
            await Service.AddToList(ctx.Guild, ctx.User as IGuildUser, jobId, count,
                "Removing a role from only users", role).ConfigureAwait(false);
            var count2 = 0;
            await ctx.Channel.SendConfirmAsync(
                $"Removing {role.Mention} from {count} users.\n + This will take about {TimeSpan.FromSeconds(users.Count()).Humanize()}.").ConfigureAwait(false);
            using (ctx.Channel.EnterTypingState())
            {
                foreach (var i in users)
                {
                    try
                    {
                        var e = Service.JobCheck(ctx.Guild, jobId).FirstOrDefault().StoppedOrNot;
                        var t = e == "Stopped";
                        if (t)
                        {
                            await Service.RemoveJob(ctx.Guild, jobId).ConfigureAwait(false);
                            await ctx.Channel.SendConfirmAsync(
                                $"Massrole Stopped.\nRemoved {role.Mention} from {count2} out of {count} users before stopped.").ConfigureAwait(false);
                            return;
                        }

                        await i.RemoveRoleAsync(role).ConfigureAwait(false);
                        await Service.UpdateCount(ctx.Guild, jobId, count2).ConfigureAwait(false);
                        count2++;
                    }
                    catch (HttpException)
                    {
                        //ignored
                    }
                }
            }

            await Service.RemoveJob(ctx.Guild, jobId).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync($"Removed {role.Mention} from {count2} out of {count} users!").ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageRoles), BotPerm(GuildPermission.ManageRoles)]
        public async Task RemoveFromAllBots(IRole role)
        {
            var runnerUser = (IGuildUser)ctx.User;
            var currentUser = await ctx.Guild.GetUserAsync(ctx.Client.CurrentUser.Id).ConfigureAwait(false);
            if (ctx.User.Id != runnerUser.Guild.OwnerId &&
                runnerUser.GetRoles().Max(x => x.Position) <= role.Position)
            {
                await ctx.Channel.SendErrorAsync("You cannot manage this role!").ConfigureAwait(false);
                return;
            }

            if (currentUser.GetRoles().Max(x => x.Position) <= role.Position)
            {
                await ctx.Channel.SendErrorAsync("I cannot manage this role!").ConfigureAwait(false);
                return;
            }

            if (Service.Jobslist.Count == 5)
            {
                await ctx.Channel.SendErrorAsync(
                        $"Due to discord rate limits you may only have 5 mass role operations at a time, check your current jobs with `{await guildSettings.GetPrefix(ctx.Guild)}rolejobs`.")
                    .ConfigureAwait(false);
                return;
            }

            var guild = ctx.Guild as SocketGuild;
            var users = guild.Users.Where(c => c.Roles.Contains(role) && c.IsBot);
            var count = users.Count();
            if (!users.Any())
            {
                await ctx.Channel.SendErrorAsync("No bots have this role!").ConfigureAwait(false);
                return;
            }

            int jobId;
            if (Service.Jobslist.FirstOrDefault() is null)
                jobId = 1;
            else
                jobId = Service.Jobslist.FirstOrDefault().JobId + 1;
            await Service.AddToList(ctx.Guild, ctx.User as IGuildUser, jobId, count,
                "Removing a role from all bots", role).ConfigureAwait(false);
            var count2 = 0;
            await ctx.Channel.SendConfirmAsync(
                $"Removing {role.Mention} from {count} bots.\n + This will take about {TimeSpan.FromSeconds(users.Count()).Humanize()}.").ConfigureAwait(false);
            using (ctx.Channel.EnterTypingState())
            {
                foreach (var i in users)
                {
                    try
                    {
                        var e = Service.JobCheck(ctx.Guild, jobId).FirstOrDefault().StoppedOrNot;
                        var t = e == "Stopped";
                        if (t)
                        {
                            await Service.RemoveJob(ctx.Guild, jobId).ConfigureAwait(false);
                            await ctx.Channel.SendConfirmAsync(
                                $"Massrole Stopped.\nRemoved {role.Mention} from {count2} out of {count} bots before stopped.").ConfigureAwait(false);
                            return;
                        }

                        await i.RemoveRoleAsync(role).ConfigureAwait(false);
                        await Service.UpdateCount(ctx.Guild, jobId, count2).ConfigureAwait(false);
                        count2++;
                    }
                    catch (HttpException)
                    {
                        //ignored
                    }
                }
            }

            await Service.RemoveJob(ctx.Guild, jobId).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync($"Removed {role.Mention} from {count2} out of {count} bots!").ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageRoles), BotPerm(GuildPermission.ManageRoles)]
        public async Task AddRoleToRole(IRole role, IRole role2)
        {
            var runnerUser = (IGuildUser)ctx.User;
            var client = await ctx.Guild.GetUserAsync(ctx.Client.CurrentUser.Id).ConfigureAwait(false);
            if ((ctx.User.Id != runnerUser.Guild.OwnerId &&
                 runnerUser.GetRoles().Max(x => x.Position) <= role2.Position) ||
                runnerUser.GetRoles().Max(x => x.Position) <= role.Position)
            {
                await ctx.Channel.SendErrorAsync("You cannot manage these roles!").ConfigureAwait(false);
                return;
            }

            if (client.GetRoles().Max(x => x.Position) <= role2.Position ||
                client.GetRoles().Max(x => x.Position) <= role.Position)
            {
                await ctx.Channel.SendErrorAsync("I cannot manage these roles!").ConfigureAwait(false);
                return;
            }

            if (Service.Jobslist.Count == 5)
            {
                await ctx.Channel.SendErrorAsync(
                        $"Due to discord rate limits you may only have 5 mass role operations at a time, check your current jobs with `{await guildSettings.GetPrefix(ctx.Guild)}rolejobs`.")
                    .ConfigureAwait(false);
                return;
            }

            var users = await ctx.Guild.GetUsersAsync().ConfigureAwait(false);
            var inrole = users.Where(x => x.GetRoles().Contains(role));
            var inrole2 = users.Where(x => x.GetRoles().Contains(role2));
            if (inrole.Count() == inrole2.Count())
            {
                await ctx.Channel.SendErrorAsync($"All users in {role.Mention} already have {role2.Mention}!").ConfigureAwait(false);
                return;
            }

            int jobId;
            if (Service.Jobslist.FirstOrDefault() is null)
                jobId = 1;
            else
                jobId = Service.Jobslist.FirstOrDefault().JobId + 1;
            await Service.AddToList(ctx.Guild, ctx.User as IGuildUser, jobId, inrole.Count(),
                "Adding a role to users within a role", role, role2).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync(
                $"Adding {role2.Mention} to users in {role.Mention}.\nThis will take about {inrole.Count()}s.").ConfigureAwait(false);
            var count2 = 0;
            using (ctx.Channel.EnterTypingState())
            {
                foreach (var i in inrole)
                {
                    try
                    {
                        var e = Service.JobCheck(ctx.Guild, jobId).FirstOrDefault().StoppedOrNot;
                        var t = e == "Stopped";
                        if (t)
                        {
                            await Service.RemoveJob(ctx.Guild, jobId).ConfigureAwait(false);
                            await ctx.Channel.SendConfirmAsync(
                                $"Massrole Stopped.\nAdded {role2.Mention} to {count2} out of {inrole.Count()} users before stopped.").ConfigureAwait(false);
                            return;
                        }

                        await i.AddRoleAsync(role2).ConfigureAwait(false);
                        await Service.UpdateCount(ctx.Guild, jobId, count2).ConfigureAwait(false);
                        count2++;
                    }
                    catch (HttpException)
                    {
                        //ignored
                    }
                }
            }

            await Service.RemoveJob(ctx.Guild, jobId).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync($"Added {role2.Mention} to {count2} users.").ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageRoles), BotPerm(GuildPermission.ManageRoles)]
        public async Task RemoveFromRole(IRole role, IRole role2)
        {
            var runnerUser = (IGuildUser)ctx.User;
            var client = await ctx.Guild.GetUserAsync(ctx.Client.CurrentUser.Id).ConfigureAwait(false);
            if ((ctx.User.Id != runnerUser.Guild.OwnerId &&
                 runnerUser.GetRoles().Max(x => x.Position) <= role2.Position) ||
                runnerUser.GetRoles().Max(x => x.Position) <= role.Position)
            {
                await ctx.Channel.SendErrorAsync("You cannot manage these roles!").ConfigureAwait(false);
                return;
            }

            if (client.GetRoles().Max(x => x.Position) <= role2.Position ||
                client.GetRoles().Max(x => x.Position) <= role.Position)
            {
                await ctx.Channel.SendErrorAsync("I cannot manage these roles!").ConfigureAwait(false);
                return;
            }

            var users = await ctx.Guild.GetUsersAsync().ConfigureAwait(false);
            var inrole = users.Where(x => x.GetRoles().Contains(role));
            var inrole2 = users.Where(x => x.GetRoles().Contains(role2));
            if (!inrole2.Any())
            {
                await ctx.Channel.SendErrorAsync($"No users in {role.Mention} have {role2.Mention}!").ConfigureAwait(false);
                return;
            }

            int jobId;
            if (Service.Jobslist.FirstOrDefault() is null)
                jobId = 1;
            else
                jobId = Service.Jobslist.FirstOrDefault().JobId + 1;
            await Service.AddToList(ctx.Guild, ctx.User as IGuildUser, jobId, inrole.Count(),
                "Removing a role from users within a role", role, role2).ConfigureAwait(false);
            var guildUsers = inrole as IGuildUser[] ?? inrole.ToArray();
            await ctx.Channel.SendConfirmAsync(
                $"Removing {role2.Mention} from users in {role.Mention}.\nThis will take about {guildUsers.Length}s.").ConfigureAwait(false);
            var count2 = 0;
            using (ctx.Channel.EnterTypingState())
            {
                foreach (var i in inrole)
                {
                    try
                    {
                        var e = Service.JobCheck(ctx.Guild, jobId).FirstOrDefault().StoppedOrNot;
                        var t = e == "Stopped";
                        if (t)
                        {
                            await Service.RemoveJob(ctx.Guild, jobId).ConfigureAwait(false);
                            await ctx.Channel.SendConfirmAsync(
                                $"Massrole Stopped.\nRemoved {role2.Mention} from {count2} out of {inrole.Count()} users before stopped.").ConfigureAwait(false);
                            return;
                        }

                        await i.RemoveRoleAsync(role2).ConfigureAwait(false);
                        await Service.UpdateCount(ctx.Guild, jobId, count2).ConfigureAwait(false);
                        count2++;
                    }
                    catch (HttpException)
                    {
                        //ignored
                    }
                }
            }

            await Service.RemoveJob(ctx.Guild, jobId).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync($"Removed {role2.Mention} from {count2} users.").ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageRoles), BotPerm(GuildPermission.ManageRoles)]
        public async Task AddThenRemove(IRole role, IRole role2)
        {
            await Task.Delay(500).ConfigureAwait(false);
            var client = await ctx.Guild.GetUserAsync(ctx.Client.CurrentUser.Id).ConfigureAwait(false);
            var runnerUser = (IGuildUser)ctx.User;
            if ((ctx.User.Id != runnerUser.Guild.OwnerId &&
                 runnerUser.GetRoles().Max(x => x.Position) <= role2.Position) ||
                runnerUser.GetRoles().Max(x => x.Position) <= role.Position)
            {
                await ctx.Channel.SendErrorAsync("You cannot manage these roles!").ConfigureAwait(false);
                return;
            }

            if (client.GetRoles().Max(x => x.Position) <= role2.Position ||
                client.GetRoles().Max(x => x.Position) <= role.Position)
            {
                await ctx.Channel.SendErrorAsync("I cannot manage these roles!").ConfigureAwait(false);
                return;
            }

            var users = await ctx.Guild.GetUsersAsync().ConfigureAwait(false);
            var inrole = users.Where(x => x.GetRoles().Contains(role2));
            if (!inrole.Any())
            {
                await ctx.Channel.SendErrorAsync("No users have the role you are trying to remove!").ConfigureAwait(false);
                return;
            }

            int jobId;
            if (Service.Jobslist.FirstOrDefault() is null)
                jobId = 1;
            else
                jobId = Service.Jobslist.FirstOrDefault().JobId + 1;
            await Service.AddToList(ctx.Guild, ctx.User as IGuildUser, jobId, inrole.Count(),
                "Adding then Removing a Role", role, role2).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync(
                $"Adding {role.Mention} to users in {role2.Mention} and removing {role2.Mention}.\nThis will take about {inrole.Count() * 2}s.").ConfigureAwait(false);
            var count2 = 0;
            using (ctx.Channel.EnterTypingState())
            {
                foreach (var i in inrole)
                {
                    try
                    {
                        var e = Service.JobCheck(ctx.Guild, jobId).FirstOrDefault().StoppedOrNot;
                        var t = e == "Stopped";
                        if (t)
                        {
                            await Service.RemoveJob(ctx.Guild, jobId).ConfigureAwait(false);
                            await ctx.Channel.SendConfirmAsync(
                                    $"Massrole Stopped.\nAdded {role2.Mention} and removed {role.Mention} from {count2} users out of {inrole.Count()} users before stopped.")
                                .ConfigureAwait(false);
                            return;
                        }

                        await i.AddRoleAsync(role).ConfigureAwait(false);
                        await i.RemoveRoleAsync(role2).ConfigureAwait(false);
                        await Service.UpdateCount(ctx.Guild, jobId, count2).ConfigureAwait(false);
                        count2++;
                    }
                    catch (HttpException)
                    {
                        //ignored
                    }
                }
            }

            await Service.RemoveJob(ctx.Guild, jobId).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync(
                $"Added {role2.Mention} to {count2} users and removed {role.Mention}.").ConfigureAwait(false);
        }
    }
}