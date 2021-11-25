using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Mewdeko._Extensions;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Services.Database.Models;
using Mewdeko.Modules.Administration.Services;
using static Mewdeko.Modules.Administration.Services.LogCommandService;

namespace Mewdeko.Modules.Administration
{
    public partial class Administration
    {
        [Group]
        public class LogCommands : MewdekoSubmodule<LogCommandService>
        {
            public enum EnableDisable
            {
                Enable,
                Disable
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPermission.Administrator)]
            [Priority(0)]
            public async Task LogServer(PermissionAction action)
            {
                await Service.LogServer(ctx.Guild.Id, ctx.Channel.Id, action.Value).ConfigureAwait(false);
                if (action.Value)
                    await ReplyConfirmLocalizedAsync("log_all").ConfigureAwait(false);
                else
                    await ReplyConfirmLocalizedAsync("log_disabled").ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPermission.Administrator)]
            [Priority(1)]
            public async Task LogServer(ITextChannel channel, PermissionAction action)
            {
                await Service.LogServer(ctx.Guild.Id, channel.Id, action.Value).ConfigureAwait(false);
                if (action.Value)
                    await ctx.Channel.SendConfirmAsync("Logging of all events have been enabled in " + channel.Mention)
                        .ConfigureAwait(false);
                else
                    await ReplyConfirmLocalizedAsync("log_disabled").ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPermission.Administrator)]
            [Priority(0)]
            public async Task LogIgnore()
            {
                var channel = (ITextChannel)ctx.Channel;

                var removed = Service.LogIgnore(ctx.Guild.Id, ctx.Channel.Id);

                if (!removed)
                    await ReplyConfirmLocalizedAsync("log_ignore",
                        Format.Bold(channel.Mention + "(" + channel.Id + ")")).ConfigureAwait(false);
                else
                    await ReplyConfirmLocalizedAsync("log_not_ignore",
                        Format.Bold(channel.Mention + "(" + channel.Id + ")")).ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPermission.Administrator)]
            [Priority(1)]
            public async Task LogIgnore(ITextChannel channel)
            {
                var removed = Service.LogIgnore(ctx.Guild.Id, channel.Id);

                if (!removed)
                    await ReplyConfirmLocalizedAsync("log_ignore",
                        Format.Bold(channel.Mention + "(" + channel.Id + ")")).ConfigureAwait(false);
                else
                    await ReplyConfirmLocalizedAsync("log_not_ignore",
                        Format.Bold(channel.Mention + "(" + channel.Id + ")")).ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPermission.Administrator)]
            [Priority(2)]
            public async Task LogIgnore(IVoiceChannel channel)
            {
                var removed = Service.LogIgnore(ctx.Guild.Id, channel.Id);

                if (!removed)
                    await ReplyConfirmLocalizedAsync("log_ignore", Format.Bold(channel.Name + "(" + channel.Id + ")"))
                        .ConfigureAwait(false);
                else
                    await ReplyConfirmLocalizedAsync("log_not_ignore",
                        Format.Bold(channel.Name + "(" + channel.Id + ")")).ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPermission.Administrator)]
            public async Task LogEvents()
            {
                Service.GuildLogSettings.TryGetValue(ctx.Guild.Id, out var l);
                var str = string.Join("\n", Enum.GetNames(typeof(LogType))
                    .Select(x =>
                    {
                        var val = l == null ? null : GetLogProperty(l, Enum.Parse<LogType>(x));
                        if (val != null)
                            return $"{Format.Bold(x)} <#{val}>";
                        return Format.Bold(x);
                    }));

                await ctx.Channel.SendConfirmAsync(Format.Bold(GetText("log_events")) + "\n" +
                                                   str)
                    .ConfigureAwait(false);
            }

            private static ulong? GetLogProperty(LogSetting l, LogType type)
            {
                switch (type)
                {
                    case LogType.Other:
                        return l.LogOtherId;
                    case LogType.MessageUpdated:
                        return l.MessageUpdatedId;
                    case LogType.MessageDeleted:
                        return l.MessageDeletedId;
                    case LogType.UserJoined:
                        return l.UserJoinedId;
                    case LogType.UserLeft:
                        return l.UserLeftId;
                    case LogType.UserBanned:
                        return l.UserBannedId;
                    case LogType.UserUnbanned:
                        return l.UserUnbannedId;
                    case LogType.UserUpdated:
                        return l.UserUpdatedId;
                    case LogType.ChannelCreated:
                        return l.ChannelCreatedId;
                    case LogType.ChannelDestroyed:
                        return l.ChannelDestroyedId;
                    case LogType.ChannelUpdated:
                        return l.ChannelUpdatedId;
                    case LogType.UserPresence:
                        return l.LogUserPresenceId;
                    case LogType.VoicePresence:
                        return l.LogVoicePresenceId;
                    case LogType.VoicePresenceTTS:
                        return l.LogVoicePresenceTTSId;
                    case LogType.UserMuted:
                        return l.UserMutedId;
                    default:
                        return null;
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPermission.Administrator)]
            [Priority(0)]
            public async Task Log(LogType type)
            {
                var val = await Service.Log(ctx.Guild.Id, ctx.Channel.Id, type);

                if (val)
                    await ReplyConfirmLocalizedAsync("log", Format.Bold(type.ToString())).ConfigureAwait(false);
                else
                    await ReplyConfirmLocalizedAsync("log_stop", Format.Bold(type.ToString())).ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPermission.Administrator)]
            [Priority(1)]
            public async Task Log(LogType type, ITextChannel channel)
            {
                var val = await Service.Log(ctx.Guild.Id, channel.Id, type);

                if (val)
                {
                    await ctx.Channel.SendConfirmAsync("Logging has been enabled for the event " +
                                                       Format.Bold(type.ToString()) + " in " + channel.Mention);
                    return;
                }

                await Service.Log(ctx.Guild.Id, channel.Id, type);

                await ctx.Channel.SendConfirmAsync("Event Logging for " + Format.Bold(type.ToString()) +
                                                   " has been switched to " + channel.Mention);
            }
        }
    }
}