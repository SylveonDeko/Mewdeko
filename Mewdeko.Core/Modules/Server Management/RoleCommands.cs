using Discord;
using System;
using Discord.Commands;
using Discord.WebSocket;
using Mewdeko.Common.Attributes;
using System.Threading.Tasks;
using Mewdeko.Modules.ServerManagement.Services;
using Mewdeko.Extensions;
using System.Linq;

namespace Mewdeko.Modules.ServerManagement
{
    public partial class ServerManagement
    {
        [Group]
        public class RoleCommands : MewdekoSubmodule<ServerManagementService>
        {
            [MewdekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageRoles)]
            public async Task MuteChannel(ITextChannel channel)
            {
                var guild = ctx.Guild;
                var usr = ctx.User as IGuildUser;
                var muterole = await _service.GetMuteRole(usr.Guild).ConfigureAwait(false);
                await channel.AddPermissionOverwriteAsync(muterole, new OverwritePermissions(sendMessages: PermValue.Allow));
                await channel.AddPermissionOverwriteAsync(muterole, new OverwritePermissions(viewChannel: PermValue.Allow));
                await ctx.Channel.SendConfirmAsync("Succesfully set " + channel.Mention + " as the mute channel!");
                
            }
            [MewdekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageRoles)]
            public async Task SetMultipleRoles(IGuildUser user, params IRole[] roles)
            {
                  await user.AddRolesAsync(roles);
                  await ctx.Channel.SendConfirmAsync(user + " has been given the roles:\n" + String.Join<String>("|", roles.Select(x => x.Mention)));
                
            }
            [MewdekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageRoles)]
            public async Task AddToAll(IRole role)
            {
                var guild = ctx.Guild as SocketGuild;
                var users = guild.Users.Where(c => !c.Roles.Contains(role));
                var count = users.Count();
                if(users.Count() == 0)
                {
                    await ctx.Channel.SendErrorAsync("All users already have this role!");
                    return;
                }
                int count2 = 0;
                await ctx.Channel.SendConfirmAsync($"Adding {role.Mention} to {count} Members.\n + This will take about {users.Count()}s.");
                using (ctx.Channel.EnterTypingState())
                {
                    
                    foreach (var i in users)
                    {
                        try
                        {
                            await i.AddRoleAsync(role);
                            count2++;
                        }
                        catch (Exception) { }
                    }
                }
                await ctx.Channel.SendConfirmAsync($"Applied {role.Mention} to {count2} out of {count} members!");
            }
            [MewdekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageRoles)]
            public async Task AddToAllBots(IRole role)
            {
                var guild = ctx.Guild as SocketGuild;
                var users = guild.Users.Where(c => !c.Roles.Contains(role) && c.IsBot);
                var count = users.Count();
                if (users.Count() == 0)
                {
                    await ctx.Channel.SendErrorAsync("All bots already have this role!");
                    return;
                }
                int count2 = 0;
                await ctx.Channel.SendConfirmAsync($"Adding {role.Mention} to {count} Members.\n + This will take about {users.Count()}s.");
                using (ctx.Channel.EnterTypingState())
                {

                    foreach (var i in users)
                    {
                        try
                        {
                            await i.AddRoleAsync(role);
                            count2++;
                        }
                        catch (Exception) { }
                    }
                }
                await ctx.Channel.SendConfirmAsync($"Applied {role.Mention} to {count2} out of {count} members!");
            }
            [MewdekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageRoles)]
            public async Task AddToAllUsers(IRole role)
            {
                var guild = ctx.Guild as SocketGuild;
                var users = guild.Users.Where(c => !c.Roles.Contains(role) && !c.IsBot);
                var count = users.Count();
                if (users.Count() == 0)
                {
                    await ctx.Channel.SendErrorAsync("All users already have this role!");
                    return;
                }
                int count2 = 0;
                await ctx.Channel.SendConfirmAsync($"Adding {role.Mention} to {count} users.\n + This will take about {users.Count()}s.");
                using (ctx.Channel.EnterTypingState())
                {

                    foreach (var i in users)
                    {
                        try
                        {
                            await i.AddRoleAsync(role);
                            count2++;
                        }
                        catch (Exception) { }
                    }
                }
                await ctx.Channel.SendConfirmAsync($"Applied {role.Mention} to {count2} out of {count} users!");
            }
            [MewdekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageRoles)]
            public async Task RemoveFromAll(IRole role)
            {
                var guild = ctx.Guild as SocketGuild;
                var users = guild.Users.Where(c => c.Roles.Contains(role));
                var count = users.Count();
                if (users.Count() == 0)
                {
                    await ctx.Channel.SendErrorAsync("No users have this role!");
                    return;
                }
                int count2 = 0;
                await ctx.Channel.SendConfirmAsync($"Removing {role.Mention} from {count} Members.\n + This will take about {users.Count()}s.");
                using (ctx.Channel.EnterTypingState())
                {
                    foreach (var i in users)
                    {
                        try
                        {
                            await i.RemoveRoleAsync(role);
                            count2++;
                        }
                        catch (Exception){}
                    }
                }
                await ctx.Channel.SendConfirmAsync($"Removed {role.Mention} from {count2} out of {count} members!");
            }
            [MewdekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageRoles)]
            public async Task RemoveFromAllUsers(IRole role)
            {
                var guild = ctx.Guild as SocketGuild;
                var users = guild.Users.Where(c => c.Roles.Contains(role)&& !c.IsBot);
                var count = users.Count();
                if (users.Count() == 0)
                {
                    await ctx.Channel.SendErrorAsync("No users have this role!");
                    return;
                }
                int count2 = 0;
                await ctx.Channel.SendConfirmAsync($"Removing {role.Mention} from {count} users.\n + This will take about {users.Count()}s.");
                using (ctx.Channel.EnterTypingState())
                {
                    foreach (var i in users)
                    {
                        try
                        {
                            await i.RemoveRoleAsync(role);
                            count2++;
                        }
                        catch (Exception) { }
                    }
                }
                await ctx.Channel.SendConfirmAsync($"Removed {role.Mention} from {count2} out of {count} users!");
            }
            [MewdekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageRoles)]
            public async Task RemoveFromAllBots(IRole role)
            {
                var guild = ctx.Guild as SocketGuild;
                var users = guild.Users.Where(c => c.Roles.Contains(role) && c.IsBot);
                var count = users.Count();
                if (users.Count() == 0)
                {
                    await ctx.Channel.SendErrorAsync("No bots have this role!");
                    return;
                }
                int count2 = 0;
                await ctx.Channel.SendConfirmAsync($"Removing {role.Mention} from {count} bots.\n + This will take about {users.Count()}s.");
                using (ctx.Channel.EnterTypingState())
                {
                    foreach (var i in users)
                    {
                        try
                        {
                            await i.RemoveRoleAsync(role);
                            count2++;
                        }
                        catch (Exception) { }
                    }
                }
                await ctx.Channel.SendConfirmAsync($"Removed {role.Mention} from {count2} out of {count} bots!");
            }
            [MewdekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageRoles)]
            public async Task AddRoleToRole(IRole role, IRole role2)
            {

                var users = await  ctx.Guild.GetUsersAsync();
                var inrole = users.Where(x => x.GetRoles().Contains(role));
                var inrole2 = users.Where(x => x.GetRoles().Contains(role2));
                var gmem = await ctx.Guild.GetUsersAsync();
                if (inrole.Count() == inrole2.Count())
                {
                    await ctx.Channel.SendErrorAsync($"All users in {role.Mention} already have {role2.Mention}!");
                    return;
                }
                await ctx.Channel.SendConfirmAsync($"Adding {role2.Mention} to users in {role.Mention}.\nThis will take about {inrole.Count()}s.");
                int count2 = 0;
                using (ctx.Channel.EnterTypingState())
                    foreach (var i in inrole)
                        try
                        {
                            await i.AddRoleAsync(role2);
                            count2++;
                        }
                        catch (Exception) { }

                await ctx.Channel.SendConfirmAsync($"Added {role2.Mention} to {count2} users.");
            }
            [MewdekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageRoles)]
            public async Task RemoveFromRole(IRole role, IRole role2)
            {

                var users = await ctx.Guild.GetUsersAsync();
                var inrole = users.Where(x => x.GetRoles().Contains(role));
                var inrole2 = users.Where(x => x.GetRoles().Contains(role2));
                if (!inrole2.Any())
                {
                    await ctx.Channel.SendErrorAsync($"No users in {role.Mention} have {role2.Mention}!");
                    return;
                }

                var guildUsers = inrole as IGuildUser[] ?? inrole.ToArray();
                await ctx.Channel.SendConfirmAsync($"Removing {role2.Mention} from users in {role.Mention}.\nThis will take about {guildUsers.Count()}s.");
                int count2 = 0;
                using (ctx.Channel.EnterTypingState())
                    foreach (var i in guildUsers)
                        try
                        {
                            await i.RemoveRoleAsync(role2);
                            count2++;
                        }
                        catch (Exception) { }

                await ctx.Channel.SendConfirmAsync($"Removed {role2.Mention} from {count2} users.");
            }
            [MewdekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageRoles)]
            public async Task AddThenRemove(IRole role, IRole role2)
            {
                var users = await ctx.Guild.GetUsersAsync();
                var inrole = users.Where(x => x.GetRoles().Contains(role));
                await ctx.Channel.SendConfirmAsync($"Adding {role2.Mention} to users in {role.Mention} and removing {role.Mention}.\nThis will take about {inrole.Count()*2}s.");
                int count2 = 0;
                using (ctx.Channel.EnterTypingState())
                    foreach (var i in inrole)
                        try
                        {
                            await i.AddRoleAsync(role2);
                            await i.RemoveRoleAsync(role);
                            count2++;
                        }
                        catch (Exception) { }

                await ctx.Channel.SendConfirmAsync($"Added {role2.Mention} to {count2} users and removed {role.Mention}.");
            }

            [MewdekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task OnlineInfo([Remainder]IRole role = null)
            {
                string e;
                SocketGuildUser[] users;
                if (string.IsNullOrWhiteSpace(role?.ToString()))
                {
                    e = "Online stats in the server:";
                    users = ((SocketGuild)ctx.Guild).Users.ToArray();
                }
                else
                {
                    e = $"Online stats in the role {role.Name}";
                    users = ((SocketGuild) ctx.Guild).Users.Where(x => x.Roles.Contains(role)).ToArray();
                }
                int online = users.Count(x => x.Status == UserStatus.Online);
                int offline = users.Count(x => x.Status == UserStatus.Offline);
                int dnd = users.Count(x => x.Status == UserStatus.DoNotDisturb);
                int invisible = users.Count(x => x.Status == UserStatus.Invisible);
                int afk = users.Count(x => x.Status == UserStatus.AFK);
                var em = new EmbedBuilder()
                {
                    Title = e,
                    Description =
                        $@"<:online:313956277808005120> Online Users: {online}
<:offline:313956277237710868> Offline Users: {offline}
<:away:313956277220802560> AFK Users: {afk}
<:dnd:313956276893646850> DND Users: {dnd}
<:invisible:313956277107556352> Invisible Users: {invisible}",
                    Color = Mewdeko.OkColor
                };
                await ctx.Channel.SendMessageAsync("", embed: em.Build());
            }
        }
    }
}