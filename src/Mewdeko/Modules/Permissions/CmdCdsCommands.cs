using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.Collections;
using Mewdeko.Common.TypeReaders;
using Mewdeko.Modules.Permissions.Services;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace Mewdeko.Modules.Permissions;

public partial class Permissions
{
    [Group]
    public class CmdCdsCommands : MewdekoSubmodule
    {
        private readonly DbService _db;
        private readonly CmdCdService _service;

        public CmdCdsCommands(CmdCdService service, DbService db)
        {
            _service = service;
            _db = db;
        }

        private ConcurrentDictionary<ulong, ConcurrentHashSet<CommandCooldown>> CommandCooldowns
            => _service.CommandCooldowns;

        private ConcurrentDictionary<ulong, ConcurrentHashSet<ActiveCooldown>> ActiveCooldowns
            => _service.ActiveCooldowns;

        [Cmd, Aliases, RequireContext(ContextType.Guild)]
        public async Task CmdCooldown(CommandOrCrInfo command, int secs)
        {
            var channel = (ITextChannel)ctx.Channel;
            if (secs is < 0 or > 3600)
            {
                await ReplyErrorLocalizedAsync("invalid_second_param_between", 0, 3600).ConfigureAwait(false);
                return;
            }

            var name = command.Name.ToLowerInvariant();
            var uow = _db.GetDbContext();
            await using (uow.ConfigureAwait(false))
            {
                var config = await uow.ForGuildId(channel.Guild.Id, set => set.Include(gc => gc.CommandCooldowns));
                var localSet = CommandCooldowns.GetOrAdd(channel.Guild.Id, new ConcurrentHashSet<CommandCooldown>());

                var toDelete = config.CommandCooldowns.FirstOrDefault(cc => cc.CommandName == name);
                if (toDelete != null)
                    uow.CommandCooldown.Remove(toDelete);
                localSet.RemoveWhere(cc => cc.CommandName == name);
                if (secs != 0)
                {
                    var cc = new CommandCooldown
                    {
                        CommandName = name,
                        Seconds = secs
                    };
                    config.CommandCooldowns.Add(cc);
                    localSet.Add(cc);
                }

                await uow.SaveChangesAsync().ConfigureAwait(false);
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

        [Cmd, Aliases, RequireContext(ContextType.Guild)]
        public async Task AllCmdCooldowns()
        {
            var channel = (ITextChannel)ctx.Channel;
            var localSet = CommandCooldowns.GetOrAdd(channel.Guild.Id, new ConcurrentHashSet<CommandCooldown>());

            if (localSet.Count == 0)
            {
                await ReplyConfirmLocalizedAsync("cmdcd_none").ConfigureAwait(false);
            }
            else
            {
                await channel.SendTableAsync("",
                                    localSet.Select(c => $"{c.CommandName}: {c.Seconds}{GetText("sec")}"), s => $"{s,-30}", 2)
                                .ConfigureAwait(false);
            }
        }
    }
}