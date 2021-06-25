using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Mewdeko.Common.Attributes;
using Mewdeko.Core.Common.TypeReaders.Models;
using Mewdeko.Extensions;
using Mewdeko.Modules.Administration.Services;
using Serilog;

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
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageRoles)]
            public async Task MuteRole([Leftover] IRole role = null)
            {
                if (role is null)
                {
                    var muteRole = await _service.GetMuteRole(ctx.Guild).ConfigureAwait(false);
                    await ReplyConfirmLocalizedAsync("mute_role", Format.Code(muteRole.Name)).ConfigureAwait(false);
                    return;
                }

                if (Context.User.Id != Context.Guild.OwnerId &&
                    role.Position >= ((SocketGuildUser) Context.User).Roles.Max(x => x.Position))
                {
                    await ReplyErrorLocalizedAsync("insuf_perms_u").ConfigureAwait(false);
                    return;
                }

                await _service.SetMuteRoleAsync(ctx.Guild.Id, role.Name).ConfigureAwait(false);

                await ReplyConfirmLocalizedAsync("mute_role_set").ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageRoles | GuildPerm.MuteMembers)]
            [Priority(0)]
            public async Task Mute(IGuildUser target, [Leftover] string reason = "")
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
                    Log.Warning(ex.ToString());
                    await ReplyErrorLocalizedAsync("mute_error").ConfigureAwait(false);
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageRoles | GuildPerm.MuteMembers)]
            [Priority(2)]
            public async Task Mute(IGuildUser user, StoopidTime time, string reason = "")
            {
                await Mute(time, user, reason);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageRoles | GuildPerm.MuteMembers)]
            [Priority(1)]
            public async Task Mute(StoopidTime time, IGuildUser user, [Leftover] string reason = "")
            {
                if (time.Time < TimeSpan.FromMinutes(1) || time.Time > TimeSpan.FromDays(1))
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
                    Log.Warning(ex, "Error in mute command");
                    await ReplyErrorLocalizedAsync("mute_error").ConfigureAwait(false);
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageRoles | GuildPerm.MuteMembers)]
            public async Task Unmute(IGuildUser user, [Leftover] string reason = "")
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
            [Priority(0)]
            public async Task ChatMute(IGuildUser user, [Leftover] string reason = "")
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
                    Log.Warning(ex.ToString());
                    await ReplyErrorLocalizedAsync("mute_error").ConfigureAwait(false);
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageRoles)]
            public async Task ChatUnmute(IGuildUser user, [Leftover] string reason = "")
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
            [MewdekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.MuteMembers)]
            [Priority(1)]
            public async Task VoiceMute(StoopidTime time, IGuildUser user, [Leftover] string reason = "")
            {
                if (time.Time < TimeSpan.FromMinutes(1) || time.Time > TimeSpan.FromDays(49))
                    return;
                try
                {
                    if (!await VerifyMutePermissions((IGuildUser)ctx.User, user))
                        return;

                    await _service.TimedMute(user, ctx.User, time.Time, MuteType.Voice, reason: reason).ConfigureAwait(false);
                    await ReplyConfirmLocalizedAsync("user_voice_mute_time", Format.Bold(user.ToString()), (int)time.Time.TotalMinutes).ConfigureAwait(false);
                }
                catch
                {
                    await ReplyErrorLocalizedAsync("mute_error").ConfigureAwait(false);
                }
            }
            [MewdekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageRoles)]
            [Priority(1)]
            public async Task ChatMute(StoopidTime time, IGuildUser user, [Leftover] string reason = "")
            {
                if (time.Time < TimeSpan.FromMinutes(1) || time.Time > TimeSpan.FromDays(49))
                    return;
                try
                {
                    if (!await VerifyMutePermissions((IGuildUser)ctx.User, user))
                        return;

                    await _service.TimedMute(user, ctx.User, time.Time, MuteType.Chat, reason: reason).ConfigureAwait(false);
                    await ReplyConfirmLocalizedAsync("user_chat_mute_time", Format.Bold(user.ToString()), (int)time.Time.TotalMinutes).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex.ToString());
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
            public async Task VoiceMute(IGuildUser user, [Leftover] string reason = "")
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
            public async Task VoiceUnmute(IGuildUser user, [Leftover] string reason = "")
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