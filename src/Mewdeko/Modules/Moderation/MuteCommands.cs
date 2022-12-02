using System.Threading.Tasks;
using Discord.Commands;
using Humanizer;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.Moderation.Services;
using Serilog;
using PermValue = Discord.PermValue;

namespace Mewdeko.Modules.Moderation;

public partial class Moderation
{
    [Group]
    public class MuteCommands : MewdekoSubmodule<MuteService>
    {
        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.MuteMembers), Priority(1)]
        public async Task Stfu(StoopidTime time, IGuildUser user) => await Stfu(user, time).ConfigureAwait(false);

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.Administrator)]
        public async Task RemoveOnMute(string yesnt)
        {
            if (yesnt.StartsWith("n"))
            {
                await Service.Removeonmute(ctx.Guild, "n").ConfigureAwait(false);
                await ctx.Channel.SendConfirmAsync("Removing roles on mute has been disabled!").ConfigureAwait(false);
            }

            if (yesnt.StartsWith("y"))
            {
                await Service.Removeonmute(ctx.Guild, "y").ConfigureAwait(false);
                await ctx.Channel.SendConfirmAsync("Removing roles on mute has been enabled!").ConfigureAwait(false);
            }
            else
            {
                await ctx.Channel.SendErrorAsync("Hey! Its either yes or no, Not that I care anyway, hmph.").ConfigureAwait(false);
            }
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.MuteMembers), Priority(0)]
        public async Task Stfu(IGuildUser user, StoopidTime? time = null)
        {
            if (!await CheckRoleHierarchy(user))
                return;
            var channel = ctx.Channel as SocketGuildChannel;
            var currentPerms = channel.GetPermissionOverwrite(user) ?? new OverwritePermissions();
            await channel.AddPermissionOverwriteAsync(user, currentPerms.Modify(sendMessages: PermValue.Deny)).ConfigureAwait(false);
            if (time is null)
                await ctx.Channel.SendConfirmAsync($"{user} has been muted in this channel!").ConfigureAwait(false);
            if (time != null)
            {
                await channel.AddPermissionOverwriteAsync(user, currentPerms.Modify(sendMessages: PermValue.Deny)).ConfigureAwait(false);
                await ctx.Channel.SendConfirmAsync(
                    $"{user} has been muted in this channel for {time.Time.Humanize()}!").ConfigureAwait(false);
                await Task.Delay((int)time.Time.TotalMilliseconds).ConfigureAwait(false);
                try
                {
                    await channel.AddPermissionOverwriteAsync(user,
                        currentPerms.Modify(sendMessages: PermValue.Inherit)).ConfigureAwait(false);
                }
                catch
                {
                    // ignored
                }
            }
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.Administrator)]
        public async Task UnmuteAll([Remainder] string? reason = null)
        {
            var users = (await ctx.Guild.GetUsersAsync().ConfigureAwait(false))
                .Where(x => x.RoleIds.ToList().Contains(Service.GetMuteRole(ctx.Guild).Result.Id));
            if (!users.Any())
            {
                await ctx.Channel.SendErrorAsync("There are no muted users or you don't have a mute role set.").ConfigureAwait(false);
                return;
            }

            if (await PromptUserConfirmAsync(
                    new EmbedBuilder().WithOkColor().WithDescription(
                        "Are you absolutely sure you want to unmute ***all*** users? This action is irreversible."),
                    ctx.User.Id).ConfigureAwait(false))
            {
                if (reason is null)
                {
                    if (await PromptUserConfirmAsync(new EmbedBuilder().WithOkColor().WithDescription("Would you like to add a reason for the unmute?"), ctx.User.Id)
                            .ConfigureAwait(false))
                    {
                        var msg = await ctx.Channel.SendMessageAsync("Please type out the unmute reason.").ConfigureAwait(false);
                        reason = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id).ConfigureAwait(false);
                        var eb = new EmbedBuilder().WithDescription($"Unmuting {users.Count()} users...");
                        await msg.ModifyAsync(x => x.Embed = eb.Build()).ConfigureAwait(false);
                        foreach (var i in users)
                        {
                            try
                            {
                                await Service.UnmuteUser(i.GuildId, i.Id, ctx.User, MuteType.All, reason).ConfigureAwait(false);
                            }
                            catch
                            {
                                // ignored
                            }
                        }

                        await msg.ModifyAsync(x =>
                            x.Embed = new EmbedBuilder().WithOkColor().WithDescription("Unmuted all users!").Build()).ConfigureAwait(false);
                    }
                    else
                    {
                        var eb = new EmbedBuilder().WithDescription($"Unmuting {users.Count()} users...");
                        var msg = await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
                        foreach (var i in users)
                        {
                            try
                            {
                                await Service.UnmuteUser(i.GuildId, i.Id, ctx.User).ConfigureAwait(false);
                            }
                            catch
                            {
                                // ignored
                            }
                        }

                        await msg.ModifyAsync(x =>
                            x.Embed = new EmbedBuilder().WithOkColor().WithDescription("Unmuted all users!").Build()).ConfigureAwait(false);
                    }
                }
                else
                {
                    var eb = new EmbedBuilder().WithDescription($"Unmuting {users.Count()} users...");
                    var msg = await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
                    foreach (var i in users)
                    {
                        try
                        {
                            await Service.UnmuteUser(i.GuildId, i.Id, ctx.User, MuteType.All, reason).ConfigureAwait(false);
                        }
                        catch
                        {
                            // ignored
                        }
                    }

                    await msg.ModifyAsync(x =>
                        x.Embed = new EmbedBuilder().WithOkColor().WithDescription("Unmuted all users!").Build()).ConfigureAwait(false);
                }
            }
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.MuteMembers)]
        public async Task Unstfu(IGuildUser user)
        {
            if (!await CheckRoleHierarchy(user))
                return;
            var channel = ctx.Channel as SocketGuildChannel;
            var currentPerms = channel.GetPermissionOverwrite(user) ?? new OverwritePermissions();
            await channel.AddPermissionOverwriteAsync(user, currentPerms.Modify(sendMessages: PermValue.Inherit)).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync($"{user} has been unmuted in this channel!").ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageRoles)]
        public async Task MuteRole([Remainder] IRole role = null)
        {
            if (role is null)
            {
                var muteRole = await Service.GetMuteRole(ctx.Guild).ConfigureAwait(false);
                await ReplyConfirmLocalizedAsync("mute_role", muteRole.Mention).ConfigureAwait(false);
                return;
            }

            if (Context.User.Id != Context.Guild.OwnerId &&
                role.Position >= ((SocketGuildUser)Context.User).Roles.Max(x => x.Position))
            {
                await ReplyErrorLocalizedAsync("insuf_perms_u").ConfigureAwait(false);
                return;
            }

            await Service.SetMuteRoleAsync(ctx.Guild.Id, role.Name).ConfigureAwait(false);

            await ReplyConfirmLocalizedAsync("mute_role_set").ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageRoles | GuildPermission.MuteMembers), Priority(0)]
        public async Task Mute(IGuildUser target, [Remainder] string reason = "")
        {
            try
            {
                if (!await CheckRoleHierarchy(target))
                    return;

                await Service.MuteUser(target, ctx.User, reason: reason).ConfigureAwait(false);
                await ReplyConfirmLocalizedAsync("user_muted", Format.Bold(target.ToString()))
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Warning(ex.ToString());
                await ReplyErrorLocalizedAsync("mute_error").ConfigureAwait(false);
            }
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageRoles | GuildPermission.MuteMembers), Priority(2)]
        public async Task Mute(IGuildUser user, StoopidTime time, string reason = "") => await Mute(time, user, reason).ConfigureAwait(false);

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageRoles | GuildPermission.MuteMembers), Priority(1)]
        public async Task Mute(StoopidTime time, IGuildUser user, [Remainder] string reason = "")
        {
            if (time.Time < TimeSpan.FromMinutes(1) || time.Time > TimeSpan.FromDays(90))
                return;
            try
            {
                if (!await CheckRoleHierarchy(user))
                    return;

                await Service.TimedMute(user, ctx.User, time.Time, reason: reason).ConfigureAwait(false);
                await ReplyConfirmLocalizedAsync("user_muted_time", Format.Bold(user.ToString()),
                    time.Time.Humanize()).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error in mute command");
                await ReplyErrorLocalizedAsync("mute_error").ConfigureAwait(false);
            }
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageRoles | GuildPermission.MuteMembers)]
        public async Task Unmute(IGuildUser user, [Remainder] string reason = "")
        {
            try
            {
                if (!await CheckRoleHierarchy(user))
                    return;
                await Service.UnmuteUser(user.GuildId, user.Id, ctx.User, reason: reason).ConfigureAwait(false);
                await ReplyConfirmLocalizedAsync("user_unmuted", Format.Bold(user.ToString()))
                    .ConfigureAwait(false);
            }
            catch
            {
                await ReplyErrorLocalizedAsync("mute_error").ConfigureAwait(false);
            }
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageRoles), Priority(0)]
        public async Task ChatMute(IGuildUser user, [Remainder] string reason = "")
        {
            try
            {
                if (!await CheckRoleHierarchy(user))
                    return;

                await Service.MuteUser(user, ctx.User, MuteType.Chat, reason).ConfigureAwait(false);
                await ReplyConfirmLocalizedAsync("user_chat_mute", Format.Bold(user.ToString()))
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Warning(ex.ToString());
                await ReplyErrorLocalizedAsync("mute_error").ConfigureAwait(false);
            }
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageRoles)]
        public async Task ChatUnmute(IGuildUser user, [Remainder] string reason = "")
        {
            try
            {
                if (!await CheckRoleHierarchy(user))
                    return;
                await Service.UnmuteUser(user.Guild.Id, user.Id, ctx.User, MuteType.Chat, reason)
                    .ConfigureAwait(false);
                await ReplyConfirmLocalizedAsync("user_chat_unmute", Format.Bold(user.ToString()))
                    .ConfigureAwait(false);
            }
            catch
            {
                await ReplyErrorLocalizedAsync("mute_error").ConfigureAwait(false);
            }
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.MuteMembers), Priority(1)]
        public async Task VoiceMute(StoopidTime time, IGuildUser user, [Remainder] string reason = "")
        {
            if (time.Time < TimeSpan.FromMinutes(1) || time.Time > TimeSpan.FromDays(49))
                return;
            try
            {
                if (!await CheckRoleHierarchy(user))
                    return;

                await Service.TimedMute(user, ctx.User, time.Time, MuteType.Voice, reason).ConfigureAwait(false);
                await ReplyConfirmLocalizedAsync("user_voice_mute_time", Format.Bold(user.ToString()),
                    time.Time.Humanize()).ConfigureAwait(false);
            }
            catch
            {
                await ReplyErrorLocalizedAsync("mute_error").ConfigureAwait(false);
            }
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.ManageRoles), Priority(1)]
        public async Task ChatMute(StoopidTime time, IGuildUser user, [Remainder] string reason = "")
        {
            if (time.Time < TimeSpan.FromMinutes(1) || time.Time > TimeSpan.FromDays(49))
                return;
            try
            {
                if (!await CheckRoleHierarchy(user))
                    return;

                await Service.TimedMute(user, ctx.User, time.Time, MuteType.Chat, reason).ConfigureAwait(false);
                await ReplyConfirmLocalizedAsync("user_chat_mute_time", Format.Bold(user.ToString()),
                    time.Time.Humanize()).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Warning(ex.ToString());
                await ReplyErrorLocalizedAsync("mute_error").ConfigureAwait(false);
            }
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.MuteMembers), Priority(1)]
        public async Task VoiceMute(IGuildUser user, [Remainder] string reason = "")
        {
            try
            {
                if (!await CheckRoleHierarchy(user))
                    return;

                await Service.MuteUser(user, ctx.User, MuteType.Voice, reason).ConfigureAwait(false);
                await ReplyConfirmLocalizedAsync("user_voice_mute", Format.Bold(user.ToString()))
                    .ConfigureAwait(false);
            }
            catch
            {
                await ReplyErrorLocalizedAsync("mute_error").ConfigureAwait(false);
            }
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.MuteMembers)]
        public async Task VoiceUnmute(IGuildUser user, [Remainder] string reason = "")
        {
            try
            {
                if (!await CheckRoleHierarchy(user))
                    return;
                await Service.UnmuteUser(user.GuildId, user.Id, ctx.User, MuteType.Voice, reason)
                    .ConfigureAwait(false);
                await ReplyConfirmLocalizedAsync("user_voice_unmute", Format.Bold(user.ToString()))
                    .ConfigureAwait(false);
            }
            catch
            {
                await ReplyErrorLocalizedAsync("mute_error").ConfigureAwait(false);
            }
        }
    }
}