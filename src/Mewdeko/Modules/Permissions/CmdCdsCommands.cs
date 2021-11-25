using Discord;
using Discord.Commands;
using Microsoft.EntityFrameworkCore;
using Mewdeko.Services;
using Mewdeko.Services.Database.Models;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Mewdeko._Extensions;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using Mewdeko.Common.Collections;
using Mewdeko.Common.TypeReaders;
using Mewdeko.Modules.Permissions.Services;

namespace Mewdeko.Modules.Permissions
{
    public partial class Permissions
    {
        [Group]
        public class CmdCdsCommands : MewdekoSubmodule
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

            [MewdekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task CmdCooldown(CommandOrCrInfo command, int secs)
            {
                var channel = (ITextChannel)ctx.Channel;
                if (secs < 0 || secs > 3600)
                {
                    await ReplyErrorLocalizedAsync("invalid_second_param_between", 0, 3600).ConfigureAwait(false);
                    return;
                }

                var name = command.Name.ToLowerInvariant();
                using (var uow = _db.GetDbContext())
                {
                    var config = uow.GuildConfigs.ForId(channel.Guild.Id, set => set.Include(gc => gc.CommandCooldowns));
                    var localSet = CommandCooldowns.GetOrAdd(channel.Guild.Id, new ConcurrentHashSet<CommandCooldown>());

                    var toDelete = config.CommandCooldowns.FirstOrDefault(cc => cc.CommandName == name);
                    if (toDelete != null)
                        uow._context.Set<CommandCooldown>().Remove(toDelete);
                    localSet.RemoveWhere(cc => cc.CommandName == name);
                    if (secs != 0)
                    {
                        var cc = new CommandCooldown()
                        {
                            CommandName = name,
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
                    activeCds.RemoveWhere(ac => ac.Command == name);
                    await ReplyConfirmLocalizedAsync("cmdcd_cleared",
                        Format.Bold(name)).ConfigureAwait(false);
                }
                else
                {
                    await ReplyConfirmLocalizedAsync("cmdcd_add",
                        Format.Bold(name),
                        Format.Bold(secs.ToString())).ConfigureAwait(false);
                }
            }

            [MewdekoCommand, Usage, Description, Aliases]
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
