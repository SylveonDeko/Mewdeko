using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using NadekoBot.Common.Collections;
using NadekoBot.Extensions;
using NadekoBot.Modules.Help.Services;
using NadekoBot.Core.Services;

namespace NadekoBot.Modules.Utility.Services
{
    public class VerboseErrorsService : INService, IUnloadableService
    {
        private readonly ConcurrentHashSet<ulong> guildsEnabled;
        private readonly DbService _db;
        private readonly CommandHandler _ch;
        private readonly HelpService _hs;

        public VerboseErrorsService(NadekoBot bot, DbService db, CommandHandler ch, HelpService hs)
        {
            _db = db;
            _ch = ch;
            _hs = hs;

            _ch.CommandErrored += LogVerboseError;

            guildsEnabled = new ConcurrentHashSet<ulong>(bot
                .AllGuildConfigs
                .Where(x => x.VerboseErrors)
                .Select(x => x.GuildId));
        }

        public Task Unload()
        {
            _ch.CommandErrored -= LogVerboseError;
            return Task.CompletedTask;
        }

        private async Task LogVerboseError(CommandInfo cmd, ITextChannel channel, string reason)
        {
            if (channel == null || !guildsEnabled.Contains(channel.GuildId))
                return;

            try
            {
                var embed = _hs.GetCommandHelp(cmd, channel.Guild)
                    .WithTitle("Command Error")
                    .WithDescription(reason)
                    .WithErrorColor();

                await channel.EmbedAsync(embed).ConfigureAwait(false);
            }
            catch
            {
                //ignore
            }
        }

        public bool ToggleVerboseErrors(ulong guildId, bool? enabled=null)
        {
            using (var uow = _db.GetDbContext())
            {
                var gc = uow.GuildConfigs.ForId(guildId, set => set);

                if (enabled==null) enabled = gc.VerboseErrors = !gc.VerboseErrors; // Old behaviour, now behind a condition
                else gc.VerboseErrors = (bool)enabled; // New behaviour, just set it.

                uow.SaveChanges();
            }

            if ((bool)enabled) // This doesn't need to be duplicated inside the using block
                guildsEnabled.Add(guildId);
            else
                guildsEnabled.TryRemove(guildId);

            return (bool)enabled;            
        }
    }
}
