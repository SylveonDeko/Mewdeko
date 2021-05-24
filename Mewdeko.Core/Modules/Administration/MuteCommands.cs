using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Mewdeko.Common.Attributes;
using Mewdeko.Core.Common.TypeReaders.Models;
using Mewdeko.Extensions;
using Mewdeko.Modules.Administration.Services;

namespace Mewdeko.Modules.Administration
{
    public partial class Administration
    {
        [Group]
        public class MuteCommands : MewdekoSubmodule<MuteService>
        {
            private async Task<bool> VerifyMutePermissions(IGuildUser runnerUser, IGuildUser targetUser)
            {
                var runnerUserRoles = runnerUser.GetRoles();
                var targetUserRoles = targetUser.GetRoles();
                if (runnerUser.Id != ctx.Guild.OwnerId &&
                    runnerUserRoles.Max(x => x.Position) <= targetUserRoles.Max(x => x.Position))
                {
                    await ReplyErrorLocalizedAsync("mute_perms").ConfigureAwait(false);
                    return false;
                }

                return true;
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [UserPerm(GuildPerm.Administrator)]
            [RequireContext(ContextType.Guild)]
            public async Task RemoveOnMute(string yesnt)
            {
                await _service.removeonmute(ctx.Guild, yesnt.Substring(0, 1).ToLower());
                var t = _service.GetRemoveOnMute(ctx.Guild.Id);
                switch (t)
                {
                    case 1:
                        await ctx.Channel.SendConfirmAsync(
                            "Role removal on mute and readding on unmute has been enabled!");
                        break;
                    case 0:
                        await ctx.Channel.SendConfirmAsync(
                            "Role removal on mute and readding on unmute has been disabled!");
                        break;
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageRoles)]
            [Priority(0)]
            public async Task SetMuteRole([Remainder] string name)
            {
                name = name.Trim();
                if (string.IsNullOrWhiteSpace(name))
                    return;

                await _service.SetMuteRoleAsync(ctx.Guild.Id, name).ConfigureAwait(false);

                await ReplyConfirmLocalizedAsync("mute_role_set").ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageRoles)]
            [Priority(1)]
            public Task SetMuteRole([Remainder] IRole role)
            {
                return SetMuteRole(role.Name);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.MuteMembers)]
            [Priority(0)]
            public async Task Mute(IGuildUser target, [Remainder] string reason = "")
            {
                try
                {
                    if (!await VerifyMutePermissions((IGuildUser) ctx.User, target))
                        return;

                    await _service.MuteUser(target, ctx.User, reason: reason).ConfigureAwait(false);
                    await ReplyConfirmLocalizedAsync("user_muted", Format.Bold(target.ToString()))
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    await ReplyErrorLocalizedAsync("mute_error").ConfigureAwait(false);
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.MuteMembers)]
            [Priority(1)]
            public async Task Mute(StoopidTime time, IGuildUser user, [Remainder] string reason = "")
            {
                if (time.Time < TimeSpan.FromMinutes(1) || time.Time > TimeSpan.FromDays(20))
                    return;
                try
                {
                    if (!await VerifyMutePermissions((IGuildUser) ctx.User, user))
                        return;

                    await _service.TimedMute(user, ctx.User, time.Time, reason: reason).ConfigureAwait(false);
                    await ReplyConfirmLocalizedAsync("user_muted_time", Format.Bold(user.ToString()),
                        (int) time.Time.TotalMinutes).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _log.Warn(ex);
                    await ReplyErrorLocalizedAsync("mute_error").ConfigureAwait(false);
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.MuteMembers)]
            public async Task Unmute(IGuildUser user, [Remainder] string reason = "")
            {
                try
                {
                    await _service.UnmuteUser(user.GuildId, user.Id, ctx.User, reason: reason).ConfigureAwait(false);
                    await ReplyConfirmLocalizedAsync("user_unmuted", Format.Bold(user.ToString()))
                        .ConfigureAwait(false);
                }
                catch
                {
                    await ReplyErrorLocalizedAsync("mute_error").ConfigureAwait(false);
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageRoles)]
            public async Task ChatMute(IGuildUser user, [Remainder] string reason = "")
            {
                try
                {
                    if (!await VerifyMutePermissions((IGuildUser) ctx.User, user))
                        return;

                    await _service.MuteUser(user, ctx.User, MuteType.Chat, reason).ConfigureAwait(false);
                    await ReplyConfirmLocalizedAsync("user_chat_mute", Format.Bold(user.ToString()))
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    await ReplyErrorLocalizedAsync("mute_error").ConfigureAwait(false);
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageRoles)]
            public async Task ChatUnmute(IGuildUser user, [Remainder] string reason = "")
            {
                try
                {
                    await _service.UnmuteUser(user.Guild.Id, user.Id, ctx.User, MuteType.Chat, reason)
                        .ConfigureAwait(false);
                    await ReplyConfirmLocalizedAsync("user_chat_unmute", Format.Bold(user.ToString()))
                        .ConfigureAwait(false);
                }
                catch
                {
                    await ReplyErrorLocalizedAsync("mute_error").ConfigureAwait(false);
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.MuteMembers)]
            public async Task VoiceMute(IGuildUser user, [Remainder] string reason = "")
            {
                try
                {
                    if (!await VerifyMutePermissions((IGuildUser) ctx.User, user))
                        return;

                    await _service.MuteUser(user, ctx.User, MuteType.Voice, reason).ConfigureAwait(false);
                    await ReplyConfirmLocalizedAsync("user_voice_mute", Format.Bold(user.ToString()))
                        .ConfigureAwait(false);
                }
                catch
                {
                    await ReplyErrorLocalizedAsync("mute_error").ConfigureAwait(false);
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.MuteMembers)]
            public async Task VoiceUnmute(IGuildUser user, [Remainder] string reason = "")
            {
                try
                {
                    await _service.UnmuteUser(user.GuildId, user.Id, ctx.User, MuteType.Voice, reason)
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
}