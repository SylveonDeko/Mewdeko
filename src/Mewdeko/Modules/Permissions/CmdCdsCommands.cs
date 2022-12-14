using System.Threading.Tasks;
using Discord.Commands;
using Humanizer;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.Collections;
using Mewdeko.Common.TypeReaders;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.Permissions.Services;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Modules.Permissions;

public partial class Permissions
{
    [Group]
    public class CmdCdsCommands : MewdekoSubmodule
    {
        private readonly DbService db;
        private readonly CmdCdService service;

        public CmdCdsCommands(CmdCdService service, DbService db)
        {
            this.service = service;
            this.db = db;
        }

        private ConcurrentDictionary<ulong, ConcurrentHashSet<CommandCooldown>> CommandCooldowns
            => service.CommandCooldowns;

        private ConcurrentDictionary<ulong, ConcurrentHashSet<ActiveCooldown>> ActiveCooldowns
            => service.ActiveCooldowns;

        [Cmd, Aliases, RequireContext(ContextType.Guild)]
        public async Task CmdCooldown(CommandOrCrInfo command, StoopidTime time = default)
        {
            time ??= StoopidTime.FromInput("0s");
            var channel = (ITextChannel)ctx.Channel;
            if (time.Time.TotalSeconds is < 0 or > 90000)
            {
                await ReplyErrorLocalizedAsync("invalid_second_param_between", 0, 90000).ConfigureAwait(false);
                return;
            }

            var name = command.Name.ToLowerInvariant();
            var uow = db.GetDbContext();
            await using (uow.ConfigureAwait(false))
            {
                var config = await uow.ForGuildId(channel.Guild.Id, set => set.Include(gc => gc.CommandCooldowns));
                var localSet = CommandCooldowns.GetOrAdd(channel.Guild.Id, new ConcurrentHashSet<CommandCooldown>());

                var toDelete = config.CommandCooldowns.FirstOrDefault(cc => cc.CommandName == name);
                if (toDelete != null)
                    uow.CommandCooldown.Remove(toDelete);
                localSet.RemoveWhere(cc => cc.CommandName == name);
                if (time.Time.TotalSeconds != 0)
                {
                    var cc = new CommandCooldown
                    {
                        CommandName = name, Seconds = Convert.ToInt32(time.Time.TotalSeconds)
                    };
                    config.CommandCooldowns.Add(cc);
                    localSet.Add(cc);
                }

                await uow.SaveChangesAsync().ConfigureAwait(false);
            }

            if (time.Time.TotalSeconds == 0)
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
                    Format.Bold(time.Time.Humanize())).ConfigureAwait(false);
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