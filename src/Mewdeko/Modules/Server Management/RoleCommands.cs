using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Net;
using Discord.WebSocket;
using Humanizer;
using Mewdeko.Common.Attributes;
using Mewdeko.Core.Common.TypeReaders.Models;
using Mewdeko.Extensions;
using Mewdeko.Modules.ServerManagement.Services;

namespace Mewdeko.Modules.ServerManagement
{
    public partial class ServerManagement
    {
        [Group]
        public class RoleCommands : MewdekoSubmodule<RoleCommandsService>
        {
            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageChannels)]
            [BotPerm(GuildPerm.ManageChannels)]
            public async Task SyncRoleToAll(IRole role)
            {
                var ch = ctx.Channel as ITextChannel;
                var perms = ch.GetPermissionOverwrite(role);
                if (perms is null)
                {
                    await ctx.Channel.SendErrorAsync("This role doesnt have perms setup in this channel!");
                    return;
                }

                var msg = await ctx.Channel.SendConfirmAsync(
                    $"<a:loading:847706744741691402> Syncing permissions from {role.Mention} to {(await ctx.Guild.GetTextChannelsAsync()).Count()} Channels and {(await ctx.Guild.GetTextChannelsAsync()).Count()} Categories.....");
                foreach (var i in await ctx.Guild.GetTextChannelsAsync())
                    if (perms != null)
                        await i.AddPermissionOverwriteAsync(role, (OverwritePermissions)perms);
                foreach (var i in await ctx.Guild.GetCategoriesAsync())
                    if (perms != null)
                        await i.AddPermissionOverwriteAsync(role, (OverwritePermissions)perms);
                var eb = new EmbedBuilder
                {
                    Color = Mewdeko.OkColor,
                    Description =
                        $"Succesfully synced perms from {role.Mention} to {(await ctx.Guild.GetTextChannelsAsync()).Count()} channels and {(await ctx.Guild.GetTextChannelsAsync()).Count()} Categories!!"
                };
                await msg.ModifyAsync(x => x.Embed = eb.Build());
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageChannels)]
            [BotPerm(GuildPerm.ManageChannels)]
            public async Task SyncRoleToAllChannels(IRole role)
            {
                var ch = ctx.Channel as ITextChannel;
                var perms = ch.GetPermissionOverwrite(role);
                if (perms is null)
                {
                    await ctx.Channel.SendErrorAsync("This role doesnt have perms setup in this channel!");
                    return;
                }

                var msg = await ctx.Channel.SendConfirmAsync(
                    $"<a:loading:847706744741691402> Syncing permissions from {role.Mention} to {(await ctx.Guild.GetTextChannelsAsync()).Count()} Channels.....");
                foreach (var i in await ctx.Guild.GetTextChannelsAsync())
                    if (perms != null)
                        await i.AddPermissionOverwriteAsync(role, (OverwritePermissions)perms);
                var eb = new EmbedBuilder
                {
                    Color = Mewdeko.OkColor,
                    Description =
                        $"Succesfully synced perms from {role.Mention} to {(await ctx.Guild.GetTextChannelsAsync()).Count()} Channels!"
                };
                await msg.ModifyAsync(x => x.Embed = eb.Build());
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageChannels)]
            [BotPerm(GuildPerm.ManageChannels)]
            public async Task SyncRoleToAllCategories(IRole role)
            {
                var ch = ctx.Channel as ITextChannel;
                var perms = ch.GetPermissionOverwrite(role);
                if (perms is null)
                {
                    await ctx.Channel.SendErrorAsync("This role doesnt have perms setup in this channel!");
                    return;
                }

                var msg = await ctx.Channel.SendConfirmAsync(
                    $"<a:loading:847706744741691402> Syncing permissions from {role.Mention} to {(await ctx.Guild.GetCategoriesAsync()).Count()} Categories.....");
                foreach (var i in await ctx.Guild.GetCategoriesAsync())
                    if (perms != null)
                        await i.AddPermissionOverwriteAsync(role, (OverwritePermissions)perms);
                var eb = new EmbedBuilder
                {
                    Color = Mewdeko.OkColor,
                    Description =
                        $"Succesfully synced perms from {role.Mention} to {(await ctx.Guild.GetCategoriesAsync()).Count()} Categories!"
                };
                await msg.ModifyAsync(x => x.Embed = eb.Build());
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageRoles)]
            [BotPerm(GuildPerm.ManageRoles)]
            public async Task DeleteRoles(params IRole[] roles)
            {
                if (roles.Where(x => !x.IsManaged).Count() is 0)
                {
                    await ctx.Channel.SendErrorAsync("You cannot delete bot roles or boost roles!");
                    return;
                }

                var secondlist = new List<string>();
                var runnerUser = (IGuildUser)ctx.User;
                var currentUser = await ctx.Guild.GetUserAsync(ctx.Client.CurrentUser.Id);
                foreach (var i in roles.Where(x => !x.IsManaged))
                {
                    if (ctx.User.Id != runnerUser.Guild.OwnerId &&
                        runnerUser.GetRoles().Max(x => x.Position) <= i.Position)
                    {
                        await ctx.Channel.SendErrorAsync($"You cannot manage {i.Mention}");
                        return;
                    }

                    if (currentUser.GetRoles().Max(x => x.Position) <= i.Position)
                    {
                        await ctx.Channel.SendErrorAsync($"I cannot manage {i.Mention}");
                        return;
                    }

                    secondlist.Add(
                        $"{i.Mention} - {ctx.Guild.GetUsersAsync().Result.Where(x => x.RoleIds.Contains(i.Id)).Count()} Users");
                }

                ;
                var embed = new EmbedBuilder
                {
                    Title = "Are you sure you want to delete these roles?",
                    Description = $"{string.Join("\n", secondlist)}"
                };
                if (await PromptUserConfirmAsync(embed))
                {
                    var msg = await ctx.Channel.SendConfirmAsync($"Deleting {roles.Count()} roles...");
                    var emb = msg.Embeds.First();
                    foreach (var i in roles) await i.DeleteAsync();
                    var newemb = new EmbedBuilder
                    {
                        Description = $"Succesfully deleted {roles.Count()} roles!",
                        Color = Mewdeko.OkColor
                    };
                    await msg.ModifyAsync(x => x.Embed = newemb.Build());
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageRoles)]
            [BotPerm(GuildPerm.ManageRoles)]
            public async Task StopJob(int jobnum)
            {
                var list = _service.jobslist.Where(x => x.JobId == jobnum && x.GuildId == ctx.Guild.Id)
                    .FirstOrDefault();
                if (list == null)
                {
                    await ctx.Channel.SendErrorAsync(
                        $"No job with that ID exists, please check the list again with `{Prefix}rolejobs`");
                    return;
                }

                var eb = new EmbedBuilder
                {
                    Color = Mewdeko.OkColor,
                    Description = "Are you sure you want to stop this job?"
                };
                eb.AddField(list.JobType,
                    $"Started by {list.StartedBy.Mention}\nProgress: {list.AddedTo}/{list.TotalUsers}");
                if (!await PromptUserConfirmAsync(eb).ConfigureAwait(false))
                {
                    var msg = await ctx.Channel.SendConfirmAsync("Job Stop Cancelled.");
                    msg.DeleteAfter(5);
                    return;
                }

                await _service.StopJob(ctx.Channel as ITextChannel, jobnum, ctx.Guild);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageRoles)]
            [BotPerm(GuildPerm.ManageRoles)]
            public async Task SetMultipleRoles(IGuildUser user, params IRole[] roles)
            {
                await user.AddRolesAsync(roles);
                await ctx.Channel.SendConfirmAsync(
                    $"{user} has been given the roles:\n{string.Join<string>("|", roles.Select(x => x.Mention))}");
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageRoles)]
            [BotPerm(GuildPerm.ManageRoles)]
            public async Task RoleJobs()
            {
                var list = _service.jobslist;
                if (!list.Any())
                {
                    await ctx.Channel.SendErrorAsync("No Mass Role Operations running!");
                    return;
                }

                var eb = new EmbedBuilder
                {
                    Title = $"{list.Count} Mass Role Operations Running",
                    Color = Mewdeko.OkColor
                };
                foreach (var i in list)
                {
                    if (i.Role2 is not null && i.JobType != "Adding then Removing a Role")
                        eb.AddField($"Job {i.JobId}",
                            $"Job Type: {i.JobType}\nStarted By: {i.StartedBy.Mention}\nProgress: {i.AddedTo}/{i.TotalUsers}\nFirst Role:{i.Role1.Mention}\nSecond Role:{i.Role2.Mention}");
                    if (i.Role2 is not null && i.JobType == "Adding then Removing a Role")
                        eb.AddField($"Job {i.JobId}",
                            $"Job Type: {i.JobType}\nStarted By: {i.StartedBy.Mention}\nProgress: {i.AddedTo}/{i.TotalUsers}\nRemoving Role:{i.Role2.Mention}\nAdding Role:{i.Role1.Mention}");
                    else
                        eb.AddField($"Job {i.JobId}",
                            $"Job Type: {i.JobType}\nStarted By: {i.StartedBy.Mention}\nProgress: {i.AddedTo}/{i.TotalUsers}\nRole:{i.Role1.Mention}");
                }

                await ctx.Channel.SendMessageAsync(embed: eb.Build());
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageRoles)]
            [BotPerm(GuildPerm.ManageRoles)]
            public async Task AddToAll(IRole role)
            {
                var runnerUser = (IGuildUser)ctx.User;
                var currentUser = await ctx.Guild.GetUserAsync(ctx.Client.CurrentUser.Id);
                if (ctx.User.Id != runnerUser.Guild.OwnerId &&
                    runnerUser.GetRoles().Max(x => x.Position) <= role.Position)
                {
                    await ctx.Channel.SendErrorAsync("You cannot manage this role!");
                    return;
                }

                if (currentUser.GetRoles().Max(x => x.Position) <= role.Position)
                {
                    await ctx.Channel.SendErrorAsync("I cannot manage this role!");
                    return;
                }

                if (_service.jobslist.Count() == 5)
                {
                    await ctx.Channel.SendErrorAsync(
                        $"Due to discord rate limits you may only have 5 mass role operations at a time, check your current jobs with `{Prefix}rolejobs`.");
                    return;
                }

                var guild = ctx.Guild as SocketGuild;
                var users = guild.Users.Where(c => !c.Roles.Contains(role));
                var count = users.Count();
                if (!users.Any())
                {
                    await ctx.Channel.SendErrorAsync("All users already have this role!");
                    return;
                }

                var JobId = 0;
                if (_service.jobslist.FirstOrDefault() is null)
                    JobId = 1;
                else
                    JobId = _service.jobslist.FirstOrDefault().JobId + 1;
                await _service.AddToList(ctx.Guild, ctx.User as IGuildUser, JobId, count, "Adding to Users and Bots",
                    role);
                var count2 = 0;
                await ctx.Channel.SendConfirmAsync(
                    $"Adding {role.Mention} to {count} Members.\nThis will take about {users.Count()}s.");
                using (ctx.Channel.EnterTypingState())
                {
                    foreach (var i in users)
                        try
                        {
                            var e = _service.JobCheck(ctx.Guild, JobId).FirstOrDefault().StoppedOrNot;
                            var t = e == "Stopped";
                            if (t)
                            {
                                await _service.RemoveJob(ctx.Guild, JobId);
                                await ctx.Channel.SendConfirmAsync(
                                    $"Massrole Stopped.\nApplied {role.Mention} to {count2} out of {count} members before stopped.");
                                return;
                            }

                            await i.AddRoleAsync(role);
                            await _service.UpdateCount(ctx.Guild, JobId, count2);
                            count2++;
                        }
                        catch (HttpException e)
                        {
                            if (e.HttpCode == HttpStatusCode.NotFound) continue;
                        }
                }

                await _service.RemoveJob(ctx.Guild, JobId);
                await ctx.Channel.SendConfirmAsync($"Applied {role.Mention} to {count2} out of {count} members!");
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageRoles)]
            [BotPerm(GuildPerm.ManageRoles)]
            public async Task AddRoleToList(IRole role)
            {
                if (!ctx.Message.Attachments.Any())
                {
                    await ctx.Channel.SendErrorAsync("You must attach a file for this to work.");
                    return;
                }

                var file = ctx.Message.Attachments.First();
                if (!file.Filename.EndsWith(".txt"))
                {
                    await ctx.Channel.SendErrorAsync("The file attached must be a txt file!");
                    return;
                }

                var users = new List<IGuildUser>();
                using (var client = new WebClient())
                {
                    var content = client.DownloadData(file.Url);
                    using (var stream = new MemoryStream(content))
                    {
                        var file1 = new StreamReader(stream);
                        string line;
                        while ((line = file1.ReadLine()) != null)
                        {
                            var user = await ctx.Guild.GetUserAsync(Convert.ToUInt64(line));
                            users.Add(user);
                        }
                    }
                }

                var runnerUser = (IGuildUser)ctx.User;
                var currentUser = await ctx.Guild.GetUserAsync(ctx.Client.CurrentUser.Id);
                if (ctx.User.Id != runnerUser.Guild.OwnerId &&
                    runnerUser.GetRoles().Max(x => x.Position) <= role.Position)
                {
                    await ctx.Channel.SendErrorAsync("You cannot manage this role!");
                    return;
                }

                if (currentUser.GetRoles().Max(x => x.Position) <= role.Position)
                {
                    await ctx.Channel.SendErrorAsync("I cannot manage this role!");
                    return;
                }

                if (_service.jobslist.Count() == 5)
                {
                    await ctx.Channel.SendErrorAsync(
                        $"Due to discord rate limits you may only have 5 mass role operations at a time, check your current jobs with `{Prefix}rolejobs`.");
                    return;
                }

                var guild = ctx.Guild as SocketGuild;
                var count = users.Count();
                if (users.Count() == 0)
                {
                    await ctx.Channel.SendErrorAsync("All users already have this role!");
                    return;
                }

                var JobId = 0;
                if (_service.jobslist.FirstOrDefault() is null)
                    JobId = 1;
                else
                    JobId = _service.jobslist.FirstOrDefault().JobId + 1;
                await _service.AddToList(ctx.Guild, ctx.User as IGuildUser, JobId, count,
                    "Adding a role to a provided user list",
                    role);
                var count2 = 0;
                await ctx.Channel.SendConfirmAsync(
                    $"Adding {role.Mention} to {count} Members.\nThis will take about {users.Count()}s.");
                using (ctx.Channel.EnterTypingState())
                {
                    foreach (var i in users)
                        try
                        {
                            var e = _service.JobCheck(ctx.Guild, JobId).FirstOrDefault().StoppedOrNot;
                            var t = e == "Stopped";
                            if (t)
                            {
                                await _service.RemoveJob(ctx.Guild, JobId);
                                await ctx.Channel.SendConfirmAsync(
                                    $"Massrole Stopped.\nApplied {role.Mention} to {count2} out of {count} members before stopped.");
                                return;
                            }

                            await i.AddRoleAsync(role);
                            await _service.UpdateCount(ctx.Guild, JobId, count2);
                            count2++;
                        }
                        catch (HttpException e)
                        {
                            if (e.HttpCode == HttpStatusCode.NotFound) continue;
                        }
                }

                await _service.RemoveJob(ctx.Guild, JobId);
                await ctx.Channel.SendConfirmAsync($"Applied {role.Mention} to {count2} out of {count} members!");
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageRoles)]
            [BotPerm(GuildPerm.ManageRoles)]
            public async Task AddToAllBots(IRole role)
            {
                var runnerUser = (IGuildUser)ctx.User;
                var currentUser = await ctx.Guild.GetUserAsync(ctx.Client.CurrentUser.Id);
                if (ctx.User.Id != runnerUser.Guild.OwnerId &&
                    runnerUser.GetRoles().Max(x => x.Position) <= role.Position)
                {
                    await ctx.Channel.SendErrorAsync("You cannot manage this role!");
                    return;
                }

                if (currentUser.GetRoles().Max(x => x.Position) <= role.Position)
                {
                    await ctx.Channel.SendErrorAsync("I cannot manage this role!");
                    return;
                }

                if (_service.jobslist.Count() == 5)
                {
                    await ctx.Channel.SendErrorAsync(
                        $"Due to discord rate limits you may only have 5 mass role operations at a time, check your current jobs with `{Prefix}rolejobs`.");
                    return;
                }

                var guild = ctx.Guild as SocketGuild;
                var users = guild.Users.Where(c => !c.Roles.Contains(role) && c.IsBot);
                var count = users.Count();
                if (users.Count() == 0)
                {
                    await ctx.Channel.SendErrorAsync("All bots already have this role!");
                    return;
                }

                var JobId = 0;
                if (_service.jobslist.FirstOrDefault() is null)
                    JobId = 1;
                else
                    JobId = _service.jobslist.FirstOrDefault().JobId + 1;
                await _service.AddToList(ctx.Guild, ctx.User as IGuildUser, JobId, count, "Adding to Bots Only", role);
                var count2 = 0;
                await ctx.Channel.SendConfirmAsync(
                    $"Adding {role.Mention} to {count} Members.\nThis will take about {users.Count()}s.");
                using (ctx.Channel.EnterTypingState())
                {
                    foreach (var i in users)
                        try
                        {
                            var e = _service.JobCheck(ctx.Guild, JobId).FirstOrDefault().StoppedOrNot;
                            var t = e == "Stopped";
                            if (t)
                            {
                                await _service.RemoveJob(ctx.Guild, JobId);
                                await ctx.Channel.SendConfirmAsync(
                                    $"Massrole Stopped.\nApplied {role.Mention} to {count2} out of {count} bots before stopped.");
                                return;
                            }

                            await i.AddRoleAsync(role);
                            await _service.UpdateCount(ctx.Guild, JobId, count2);
                            count2++;
                        }
                        catch (HttpException e)
                        {
                            if (e.HttpCode != HttpStatusCode.NotFound) continue;
                        }
                }

                await _service.RemoveJob(ctx.Guild, JobId);
                await ctx.Channel.SendConfirmAsync($"Applied {role.Mention} to {count2} out of {count} bots!");
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageRoles)]
            [BotPerm(GuildPerm.ManageRoles)]
            public async Task AddToAllUsers(IRole role)
            {
                var runnerUser = (IGuildUser)ctx.User;
                var currentUser = await ctx.Guild.GetUserAsync(ctx.Client.CurrentUser.Id);
                if (ctx.User.Id != runnerUser.Guild.OwnerId &&
                    runnerUser.GetRoles().Max(x => x.Position) <= role.Position)
                {
                    await ctx.Channel.SendErrorAsync("You cannot manage this role!");
                    return;
                }

                if (currentUser.GetRoles().Max(x => x.Position) <= role.Position)
                {
                    await ctx.Channel.SendErrorAsync("I cannot manage this role!");
                    return;
                }

                if (_service.jobslist.Count() == 5)
                {
                    await ctx.Channel.SendErrorAsync(
                        $"Due to discord rate limits you may only have 5 mass role operations at a time, check your current jobs with `{Prefix}rolejobs`.");
                    return;
                }

                var JobId = 0;
                if (_service.jobslist.FirstOrDefault() is null)
                    JobId = 1;
                else
                    JobId = _service.jobslist.FirstOrDefault().JobId + 1;
                var guild = ctx.Guild as SocketGuild;
                var users = guild.Users.Where(c => !c.Roles.Contains(role) && !c.IsBot);
                var count = users.Count();
                if (users.Count() == 0)
                {
                    await ctx.Channel.SendErrorAsync("All users already have this role!");
                    return;
                }

                await _service.AddToList(ctx.Guild, ctx.User as IGuildUser, JobId, count, "Adding to Users Only", role);
                var count2 = 0;
                await ctx.Channel.SendConfirmAsync(
                    $"Adding {role.Mention} to {count} users.\n + This will take about {users.Count()}s.");
                using (ctx.Channel.EnterTypingState())
                {
                    foreach (var i in users)
                        try
                        {
                            var e = _service.JobCheck(ctx.Guild, JobId).FirstOrDefault().StoppedOrNot;
                            var t = e == "Stopped";
                            if (t)
                            {
                                await _service.RemoveJob(ctx.Guild, JobId);
                                await ctx.Channel.SendConfirmAsync(
                                    $"Massrole Stopped.\nApplied {role.Mention} to {count2} out of {count} users before stopped.");
                                return;
                            }

                            await i.AddRoleAsync(role);
                            await _service.UpdateCount(ctx.Guild, JobId, count2);
                            count2++;
                        }
                        catch (HttpException e)
                        {
                            if (e.HttpCode != HttpStatusCode.NotFound) continue;
                        }
                }

                await _service.RemoveJob(ctx.Guild, JobId);
                await ctx.Channel.SendConfirmAsync($"Applied {role.Mention} to {count2} out of {count} users!");
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageRoles)]
            [BotPerm(GuildPerm.ManageRoles)]
            public async Task AddToUsersOver(StoopidTime time, IRole role)
            {
                var runnerUser = (IGuildUser)ctx.User;
                var currentUser = await ctx.Guild.GetUserAsync(ctx.Client.CurrentUser.Id);
                if (ctx.User.Id != runnerUser.Guild.OwnerId &&
                    runnerUser.GetRoles().Max(x => x.Position) <= role.Position)
                {
                    await ctx.Channel.SendErrorAsync("You cannot manage this role!");
                    return;
                }

                if (currentUser.GetRoles().Max(x => x.Position) <= role.Position)
                {
                    await ctx.Channel.SendErrorAsync("I cannot manage this role!");
                    return;
                }

                if (_service.jobslist.Count() == 5)
                {
                    await ctx.Channel.SendErrorAsync(
                        $"Due to discord rate limits you may only have 5 mass role operations at a time, check your current jobs with `{Prefix}rolejobs`.");
                    return;
                }

                var guild = ctx.Guild as SocketGuild;
                var users = guild.Users.Where(c =>
                    !c.Roles.Contains(role) && !c.IsBot &&
                    DateTimeOffset.Now.Subtract(c.JoinedAt.Value) >= time.Time);
                var count = users.Count();
                if (users.Count() == 0)
                {
                    await ctx.Channel.SendErrorAsync("All users at this account age already have this role!");
                    return;
                }

                var JobId = 0;
                if (_service.jobslist.FirstOrDefault() is null)
                    JobId = 1;
                else
                    JobId = _service.jobslist.FirstOrDefault().JobId + 1;
                await _service.AddToList(ctx.Guild, ctx.User as IGuildUser, JobId, count,
                    $"Adding a role to server members that have been here for {time.Time.Humanize()}", role);
                var count2 = 0;
                await ctx.Channel.SendConfirmAsync(
                    $"Adding {role.Mention} to {count} users who have acounts that are equal to or older than {time.Time.Humanize()} old..\nThis will take about {users.Count()}s.");
                using (ctx.Channel.EnterTypingState())
                {
                    foreach (var i in users)
                        try
                        {
                            var e = _service.JobCheck(ctx.Guild, JobId).FirstOrDefault().StoppedOrNot;
                            var t = e == "Stopped";
                            if (t)
                            {
                                await _service.RemoveJob(ctx.Guild, JobId);
                                await ctx.Channel.SendConfirmAsync(
                                    $"Massrole Stopped.\nApplied {role.Mention} to {count2} out of {count} users before stopped.");
                                return;
                            }

                            await i.AddRoleAsync(role);
                            await _service.UpdateCount(ctx.Guild, JobId, count2);
                            count2++;
                        }
                        catch (HttpException e)
                        {
                            if (e.HttpCode == HttpStatusCode.NotFound) continue;
                        }
                }

                await _service.RemoveJob(ctx.Guild, JobId);
                await ctx.Channel.SendConfirmAsync($"Applied {role.Mention} to {count2} out of {count} users!");
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageRoles)]
            [BotPerm(GuildPerm.ManageRoles)]
            public async Task AddToUsersUnder(StoopidTime time, IRole role)
            {
                var runnerUser = (IGuildUser)ctx.User;
                var currentUser = await ctx.Guild.GetUserAsync(ctx.Client.CurrentUser.Id);
                if (ctx.User.Id != runnerUser.Guild.OwnerId &&
                    runnerUser.GetRoles().Max(x => x.Position) <= role.Position)
                {
                    await ctx.Channel.SendErrorAsync("You cannot manage this role!");
                    return;
                }

                if (currentUser.GetRoles().Max(x => x.Position) <= role.Position)
                {
                    await ctx.Channel.SendErrorAsync("I cannot manage this role!");
                    return;
                }

                if (_service.jobslist.Count() == 5)
                {
                    await ctx.Channel.SendErrorAsync(
                        $"Due to discord rate limits you may only have 5 mass role operations at a time, check your current jobs with `{Prefix}rolejobs`.");
                    return;
                }

                var guild = ctx.Guild as SocketGuild;
                var users = guild.Users.Where(c =>
                    !c.Roles.Contains(role) && !c.IsBot &&
                    DateTimeOffset.Now.Subtract(c.JoinedAt.Value) < time.Time);
                var count = users.Count();
                if (users.Count() == 0)
                {
                    await ctx.Channel.SendErrorAsync("All users at this account age already have this role!");
                    return;
                }

                var JobId = 0;
                if (_service.jobslist.FirstOrDefault() is null)
                    JobId = 1;
                else
                    JobId = _service.jobslist.FirstOrDefault().JobId + 1;
                await _service.AddToList(ctx.Guild, ctx.User as IGuildUser, JobId, count,
                    $"Adding a role to server members that have been here for {time.Time.Humanize()} or less", role);
                var count2 = 0;
                await ctx.Channel.SendConfirmAsync(
                    $"Adding {role.Mention} to {count} users who have acounts that are less than {time.Time.Humanize()} old..\nThis will take about {users.Count()}s.");
                using (ctx.Channel.EnterTypingState())
                {
                    foreach (var i in users)
                        try
                        {
                            var e = _service.JobCheck(ctx.Guild, JobId).FirstOrDefault().StoppedOrNot;
                            var t = e == "Stopped";
                            if (t)
                            {
                                await _service.RemoveJob(ctx.Guild, JobId);
                                await ctx.Channel.SendConfirmAsync(
                                    $"Massrole Stopped.\nApplied {role.Mention} to {count2} out of {count} users before stopped.");
                                return;
                            }

                            await i.AddRoleAsync(role);
                            await _service.UpdateCount(ctx.Guild, JobId, count2);
                            count2++;
                        }
                        catch (HttpException e)
                        {
                            if (e.HttpCode == HttpStatusCode.NotFound) continue;
                        }
                }

                await ctx.Channel.SendConfirmAsync($"Applied {role.Mention} to {count2} out of {count} users!");
            }


            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageRoles)]
            [BotPerm(GuildPerm.ManageRoles)]
            public async Task RemoveFromAll(IRole role)
            {
                var runnerUser = (IGuildUser)ctx.User;
                var currentUser = await ctx.Guild.GetUserAsync(ctx.Client.CurrentUser.Id);
                if (ctx.User.Id != runnerUser.Guild.OwnerId &&
                    runnerUser.GetRoles().Max(x => x.Position) <= role.Position)
                {
                    await ctx.Channel.SendErrorAsync("You cannot manage this role!");
                    return;
                }

                if (currentUser.GetRoles().Max(x => x.Position) <= role.Position)
                {
                    await ctx.Channel.SendErrorAsync("I cannot manage this role!");
                    return;
                }

                if (_service.jobslist.Count() == 5)
                {
                    await ctx.Channel.SendErrorAsync(
                        $"Due to discord rate limits you may only have 5 mass role operations at a time, check your current jobs with `{Prefix}rolejobs`.");
                    return;
                }

                var guild = ctx.Guild as SocketGuild;
                var users = guild.Users.Where(c => c.Roles.Contains(role));
                var count = users.Count();
                if (users.Count() == 0)
                {
                    await ctx.Channel.SendErrorAsync("No users have this role!");
                    return;
                }

                var JobId = 0;
                if (_service.jobslist.FirstOrDefault() is null)
                    JobId = 1;
                else
                    JobId = _service.jobslist.FirstOrDefault().JobId + 1;
                await _service.AddToList(ctx.Guild, ctx.User as IGuildUser, JobId, count,
                    "Removing a role from all server members", role);
                var count2 = 0;
                await ctx.Channel.SendConfirmAsync(
                    $"Removing {role.Mention} from {count} Members.\n + This will take about {users.Count()}s.");
                using (ctx.Channel.EnterTypingState())
                {
                    foreach (var i in users)
                        try
                        {
                            var e = _service.JobCheck(ctx.Guild, JobId).FirstOrDefault().StoppedOrNot;
                            var t = e == "Stopped";
                            if (t)
                            {
                                await _service.RemoveJob(ctx.Guild, JobId);
                                await ctx.Channel.SendConfirmAsync(
                                    $"Massrole Stopped.\nRemoved {role.Mention} from {count2} out of {count} server members before stopped.");
                                return;
                            }

                            await i.RemoveRoleAsync(role);
                            await _service.UpdateCount(ctx.Guild, JobId, count2);
                            count2++;
                        }
                        catch (HttpException e)
                        {
                            if (e.HttpCode == HttpStatusCode.NotFound) continue;
                        }
                }

                await _service.RemoveJob(ctx.Guild, JobId);
                await ctx.Channel.SendConfirmAsync($"Removed {role.Mention} from {count2} out of {count} members!");
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageRoles)]
            [BotPerm(GuildPerm.ManageRoles)]
            public async Task RemoveFromAllUsers(IRole role)
            {
                var runnerUser = (IGuildUser)ctx.User;
                var currentUser = await ctx.Guild.GetUserAsync(ctx.Client.CurrentUser.Id);
                if (ctx.User.Id != runnerUser.Guild.OwnerId &&
                    runnerUser.GetRoles().Max(x => x.Position) <= role.Position)
                {
                    await ctx.Channel.SendErrorAsync("You cannot manage this role!");
                    return;
                }

                if (currentUser.GetRoles().Max(x => x.Position) <= role.Position)
                {
                    await ctx.Channel.SendErrorAsync("I cannot manage this role!");
                    return;
                }

                if (_service.jobslist.Count() == 5)
                {
                    await ctx.Channel.SendErrorAsync(
                        $"Due to discord rate limits you may only have 5 mass role operations at a time, check your current jobs with `{Prefix}rolejobs`.");
                    return;
                }

                var guild = ctx.Guild as SocketGuild;
                var users = guild.Users.Where(c => c.Roles.Contains(role) && !c.IsBot);
                var count = users.Count();
                if (users.Count() == 0)
                {
                    await ctx.Channel.SendErrorAsync("No users have this role!");
                    return;
                }

                var JobId = 0;
                if (_service.jobslist.FirstOrDefault() is null)
                    JobId = 1;
                else
                    JobId = _service.jobslist.FirstOrDefault().JobId + 1;
                await _service.AddToList(ctx.Guild, ctx.User as IGuildUser, JobId, count,
                    "Removing a role from only users", role);
                var count2 = 0;
                await ctx.Channel.SendConfirmAsync(
                    $"Removing {role.Mention} from {count} users.\n + This will take about {users.Count()}s.");
                using (ctx.Channel.EnterTypingState())
                {
                    foreach (var i in users)
                        try
                        {
                            var e = _service.JobCheck(ctx.Guild, JobId).FirstOrDefault().StoppedOrNot;
                            var t = e == "Stopped";
                            if (t)
                            {
                                await _service.RemoveJob(ctx.Guild, JobId);
                                await ctx.Channel.SendConfirmAsync(
                                    $"Massrole Stopped.\nRemoved {role.Mention} from {count2} out of {count} users before stopped.");
                                return;
                            }

                            await i.RemoveRoleAsync(role);
                            await _service.UpdateCount(ctx.Guild, JobId, count2);
                            count2++;
                        }
                        catch (HttpException e)
                        {
                            if (e.HttpCode == HttpStatusCode.NotFound) continue;
                        }
                }

                await _service.RemoveJob(ctx.Guild, JobId);
                await ctx.Channel.SendConfirmAsync($"Removed {role.Mention} from {count2} out of {count} users!");
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageRoles)]
            [BotPerm(GuildPerm.ManageRoles)]
            public async Task RemoveFromAllBots(IRole role)
            {
                var runnerUser = (IGuildUser)ctx.User;
                var currentUser = await ctx.Guild.GetUserAsync(ctx.Client.CurrentUser.Id);
                if (ctx.User.Id != runnerUser.Guild.OwnerId &&
                    runnerUser.GetRoles().Max(x => x.Position) <= role.Position)
                {
                    await ctx.Channel.SendErrorAsync("You cannot manage this role!");
                    return;
                }

                if (currentUser.GetRoles().Max(x => x.Position) <= role.Position)
                {
                    await ctx.Channel.SendErrorAsync("I cannot manage this role!");
                    return;
                }

                if (_service.jobslist.Count() == 5)
                {
                    await ctx.Channel.SendErrorAsync(
                        $"Due to discord rate limits you may only have 5 mass role operations at a time, check your current jobs with `{Prefix}rolejobs`.");
                    return;
                }

                var guild = ctx.Guild as SocketGuild;
                var users = guild.Users.Where(c => c.Roles.Contains(role) && c.IsBot);
                var count = users.Count();
                if (users.Count() == 0)
                {
                    await ctx.Channel.SendErrorAsync("No bots have this role!");
                    return;
                }

                var JobId = 0;
                if (_service.jobslist.FirstOrDefault() is null)
                    JobId = 1;
                else
                    JobId = _service.jobslist.FirstOrDefault().JobId + 1;
                await _service.AddToList(ctx.Guild, ctx.User as IGuildUser, JobId, count,
                    "Removing a role from all bots", role);
                var count2 = 0;
                await ctx.Channel.SendConfirmAsync(
                    $"Removing {role.Mention} from {count} bots.\n + This will take about {users.Count()}s.");
                using (ctx.Channel.EnterTypingState())
                {
                    foreach (var i in users)
                        try
                        {
                            var e = _service.JobCheck(ctx.Guild, JobId).FirstOrDefault().StoppedOrNot;
                            var t = e == "Stopped";
                            if (t)
                            {
                                await _service.RemoveJob(ctx.Guild, JobId);
                                await ctx.Channel.SendConfirmAsync(
                                    $"Massrole Stopped.\nRemoved {role.Mention} from {count2} out of {count} bots before stopped.");
                                return;
                            }

                            await i.RemoveRoleAsync(role);
                            await _service.UpdateCount(ctx.Guild, JobId, count2);
                            count2++;
                        }
                        catch (HttpException e)
                        {
                            if (e.HttpCode == HttpStatusCode.NotFound) continue;
                        }
                }

                await _service.RemoveJob(ctx.Guild, JobId);
                await ctx.Channel.SendConfirmAsync($"Removed {role.Mention} from {count2} out of {count} bots!");
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageRoles)]
            [BotPerm(GuildPerm.ManageRoles)]
            public async Task AddRoleToRole(IRole role, IRole role2)
            {
                var runnerUser = (IGuildUser)ctx.User;
                var Client = await ctx.Guild.GetUserAsync(ctx.Client.CurrentUser.Id);
                if (ctx.User.Id != runnerUser.Guild.OwnerId &&
                    runnerUser.GetRoles().Max(x => x.Position) <= role2.Position ||
                    runnerUser.GetRoles().Max(x => x.Position) <= role.Position)
                {
                    await ctx.Channel.SendErrorAsync("You cannot manage these roles!");
                    return;
                }

                if (Client.GetRoles().Max(x => x.Position) <= role2.Position ||
                    Client.GetRoles().Max(x => x.Position) <= role.Position)
                {
                    await ctx.Channel.SendErrorAsync("I cannot manage these roles!");
                    return;
                }

                if (_service.jobslist.Count() == 5)
                {
                    await ctx.Channel.SendErrorAsync(
                        $"Due to discord rate limits you may only have 5 mass role operations at a time, check your current jobs with `{Prefix}rolejobs`.");
                    return;
                }

                var users = await ctx.Guild.GetUsersAsync();
                var inrole = users.Where(x => x.GetRoles().Contains(role));
                var inrole2 = users.Where(x => x.GetRoles().Contains(role2));
                var gmem = await ctx.Guild.GetUsersAsync();
                if (inrole.Count() == inrole2.Count())
                {
                    await ctx.Channel.SendErrorAsync($"All users in {role.Mention} already have {role2.Mention}!");
                    return;
                }

                var JobId = 0;
                if (_service.jobslist.FirstOrDefault() is null)
                    JobId = 1;
                else
                    JobId = _service.jobslist.FirstOrDefault().JobId + 1;
                await _service.AddToList(ctx.Guild, ctx.User as IGuildUser, JobId, inrole.Count(),
                    "Adding a role to users within a role", role, role2);
                await ctx.Channel.SendConfirmAsync(
                    $"Adding {role2.Mention} to users in {role.Mention}.\nThis will take about {inrole.Count()}s.");
                var count2 = 0;
                using (ctx.Channel.EnterTypingState())
                {
                    foreach (var i in inrole)
                        try
                        {
                            var e = _service.JobCheck(ctx.Guild, JobId).FirstOrDefault().StoppedOrNot;
                            var t = e == "Stopped";
                            if (t)
                            {
                                await _service.RemoveJob(ctx.Guild, JobId);
                                await ctx.Channel.SendConfirmAsync(
                                    $"Massrole Stopped.\nAdded {role2.Mention} to {count2} out of {inrole.Count()} users before stopped.");
                                return;
                            }

                            await i.AddRoleAsync(role2);
                            await _service.UpdateCount(ctx.Guild, JobId, count2);
                            count2++;
                        }
                        catch (HttpException e)
                        {
                            if (e.HttpCode == HttpStatusCode.NotFound) continue;
                        }
                }

                await _service.RemoveJob(ctx.Guild, JobId);
                await ctx.Channel.SendConfirmAsync($"Added {role2.Mention} to {count2} users.");
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageRoles)]
            [BotPerm(GuildPerm.ManageRoles)]
            public async Task RemoveFromRole(IRole role, IRole role2)
            {
                var runnerUser = (IGuildUser)ctx.User;
                var Client = await ctx.Guild.GetUserAsync(ctx.Client.CurrentUser.Id);
                if (ctx.User.Id != runnerUser.Guild.OwnerId &&
                    runnerUser.GetRoles().Max(x => x.Position) <= role2.Position ||
                    runnerUser.GetRoles().Max(x => x.Position) <= role.Position)
                {
                    await ctx.Channel.SendErrorAsync("You cannot manage these roles!");
                    return;
                }

                if (Client.GetRoles().Max(x => x.Position) <= role2.Position ||
                    Client.GetRoles().Max(x => x.Position) <= role.Position)
                {
                    await ctx.Channel.SendErrorAsync("I cannot manage these roles!");
                    return;
                }

                var users = await ctx.Guild.GetUsersAsync();
                var inrole = users.Where(x => x.GetRoles().Contains(role));
                var inrole2 = users.Where(x => x.GetRoles().Contains(role2));
                if (!inrole2.Any())
                {
                    await ctx.Channel.SendErrorAsync($"No users in {role.Mention} have {role2.Mention}!");
                    return;
                }

                var JobId = 0;
                if (_service.jobslist.FirstOrDefault() is null)
                    JobId = 1;
                else
                    JobId = _service.jobslist.FirstOrDefault().JobId + 1;
                await _service.AddToList(ctx.Guild, ctx.User as IGuildUser, JobId, inrole.Count(),
                    "Removing a role from users within a role", role, role2);
                var guildUsers = inrole as IGuildUser[] ?? inrole.ToArray();
                await ctx.Channel.SendConfirmAsync(
                    $"Removing {role2.Mention} from users in {role.Mention}.\nThis will take about {guildUsers.Count()}s.");
                var count2 = 0;
                using (ctx.Channel.EnterTypingState())
                {
                    foreach (var i in inrole)
                        try
                        {
                            var e = _service.JobCheck(ctx.Guild, JobId).FirstOrDefault().StoppedOrNot;
                            var t = e == "Stopped";
                            if (t)
                            {
                                await _service.RemoveJob(ctx.Guild, JobId);
                                await ctx.Channel.SendConfirmAsync(
                                    $"Massrole Stopped.\nRemoved {role2.Mention} from {count2} out of {inrole.Count()} users before stopped.");
                                return;
                            }

                            await i.RemoveRoleAsync(role2);
                            await _service.UpdateCount(ctx.Guild, JobId, count2);
                            count2++;
                        }
                        catch (HttpException e)
                        {
                            if (e.HttpCode == HttpStatusCode.NotFound) continue;
                        }
                }

                await _service.RemoveJob(ctx.Guild, JobId);
                await ctx.Channel.SendConfirmAsync($"Removed {role2.Mention} from {count2} users.");
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageRoles)]
            [BotPerm(GuildPerm.ManageRoles)]
            public async Task AddThenRemove(IRole role, IRole role2)
            {
                var Client = await ctx.Guild.GetUserAsync(ctx.Client.CurrentUser.Id);
                var runnerUser = (IGuildUser)ctx.User;
                if (ctx.User.Id != runnerUser.Guild.OwnerId &&
                    runnerUser.GetRoles().Max(x => x.Position) <= role2.Position ||
                    runnerUser.GetRoles().Max(x => x.Position) <= role.Position)
                {
                    await ctx.Channel.SendErrorAsync("You cannot manage these roles!");
                    return;
                }

                if (Client.GetRoles().Max(x => x.Position) <= role2.Position ||
                    Client.GetRoles().Max(x => x.Position) <= role.Position)
                {
                    await ctx.Channel.SendErrorAsync("I cannot manage these roles!");
                    return;
                }

                var users = await ctx.Guild.GetUsersAsync();
                var inrole = users.Where(x => x.GetRoles().Contains(role2));
                if (inrole.Count() == 0)
                {
                    await ctx.Channel.SendErrorAsync("No users have the role you are trying to remove!");
                    return;
                }

                var JobId = 0;
                if (_service.jobslist.FirstOrDefault() is null)
                    JobId = 1;
                else
                    JobId = _service.jobslist.FirstOrDefault().JobId + 1;
                await _service.AddToList(ctx.Guild, ctx.User as IGuildUser, JobId, inrole.Count(),
                    "Adding then Removing a Role", role, role2);
                await ctx.Channel.SendConfirmAsync(
                    $"Adding {role.Mention} to users in {role2.Mention} and removing {role2.Mention}.\nThis will take about {inrole.Count() * 2}s.");
                var count2 = 0;
                using (ctx.Channel.EnterTypingState())
                {
                    foreach (var i in inrole)
                        try
                        {
                            var e = _service.JobCheck(ctx.Guild, JobId).FirstOrDefault().StoppedOrNot;
                            var t = e == "Stopped";
                            if (t)
                            {
                                await _service.RemoveJob(ctx.Guild, JobId);
                                await ctx.Channel.SendConfirmAsync(
                                    $"Massrole Stopped.\nAdded {role2.Mention} and removed {role.Mention} from {count2} users out of {inrole.Count()} users before stopped.");
                                return;
                            }

                            await i.AddRoleAsync(role);
                            await i.RemoveRoleAsync(role2);
                            await _service.UpdateCount(ctx.Guild, JobId, count2);
                            count2++;
                        }
                        catch (HttpException e)
                        {
                            if (e.HttpCode == HttpStatusCode.NotFound) continue;
                        }
                }

                await _service.RemoveJob(ctx.Guild, JobId);
                await ctx.Channel.SendConfirmAsync(
                    $"Added {role2.Mention} to {count2} users and removed {role.Mention}.");
            }

            public class RoleShit
            {
                public string Mention { get; set; }
                public int UserCount { get; set; }
            }
        }
    }
}