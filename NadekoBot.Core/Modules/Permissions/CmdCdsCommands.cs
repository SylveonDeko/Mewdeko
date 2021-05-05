using Discord;
using Discord.Commands;
using Microsoft.EntityFrameworkCore;
using NadekoBot.Extensions;
using NadekoBot.Core.Services;
using NadekoBot.Core.Services.Database.Models;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using NadekoBot.Common.Attributes;
using NadekoBot.Common.Collections;
using NadekoBot.Modules.Permissions.Services;

namespace NadekoBot.Modules.Permissions
{
    public partial class Permissions
    {
        [Group]
        public class CmdCdsCommands : NadekoSubmodule
        {
            private readonly DbService _db;
            private readonly CmdCdService _service;

            private ConcurrentDictionary<ulong, ConcurrentHashSet<CommandCooldown>> CommandCooldowns
                => _service.CommandCooldowns;
            private ConcurrentDictionary<ulong, ConcurrentHashSet<ActiveCooldown>> ActiveCooldowns
                => _service.ActiveCooldowns;

            public CmdCdsCommands(CmdCdService service, DbService db)
            {
                _service = service;
                _db = db;
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task CmdCooldown(CommandInfo command, int secs)
            {
                var channel = (ITextChannel)ctx.Channel;
                if (secs < 0 || secs > 3600)
                {
                    await ReplyErrorLocalizedAsync("invalid_second_param_between", 0, 3600).ConfigureAwait(false);
                    return;
                }

                using (var uow = _db.GetDbContext())
                {
                    var config = uow.GuildConfigs.ForId(channel.Guild.Id, set => set.Include(gc => gc.CommandCooldowns));
                    var localSet = CommandCooldowns.GetOrAdd(channel.Guild.Id, new ConcurrentHashSet<CommandCooldown>());

                    var toDelete = config.CommandCooldowns.FirstOrDefault(cc => cc.CommandName == command.Aliases.First().ToLowerInvariant());
                    if (toDelete != null)
                        uow._context.Set<CommandCooldown>().Remove(toDelete);
                    localSet.RemoveWhere(cc => cc.CommandName == command.Aliases.First().ToLowerInvariant());
                    if (secs != 0)
                    {
                        var cc = new CommandCooldown()
                        {
                            CommandName = command.Aliases.First().ToLowerInvariant(),
                            Seconds = secs,
                        };
                        config.CommandCooldowns.Add(cc);
                        localSet.Add(cc);
                    }
                    await uow.SaveChangesAsync();
                }
                if (secs == 0)
                {
                    var activeCds = ActiveCooldowns.GetOrAdd(channel.Guild.Id, new ConcurrentHashSet<ActiveCooldown>());
                    activeCds.RemoveWhere(ac => ac.Command == command.Aliases.First().ToLowerInvariant());
                    await ReplyConfirmLocalizedAsync("cmdcd_cleared",
                        Format.Bold(command.Aliases.First())).ConfigureAwait(false);
                }
                else
                {
                    await ReplyConfirmLocalizedAsync("cmdcd_add",
                        Format.Bold(command.Aliases.First()),
                        Format.Bold(secs.ToString())).ConfigureAwait(false);
                }
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task AllCmdCooldowns()
            {
                var channel = (ITextChannel)ctx.Channel;
                var localSet = CommandCooldowns.GetOrAdd(channel.Guild.Id, new ConcurrentHashSet<CommandCooldown>());

                if (!localSet.Any())
                    await ReplyConfirmLocalizedAsync("cmdcd_none").ConfigureAwait(false);
                else
                    await channel.SendTableAsync("", localSet.Select(c => c.CommandName + ": " + c.Seconds + GetText("sec")), s => $"{s,-30}", 2).ConfigureAwait(false);
            }
        }
    }
}
