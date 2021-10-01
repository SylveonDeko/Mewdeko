using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using Mewdeko.Services;
using Mewdeko.Modules.Games.Common.ChatterBot;
using Mewdeko.Modules.Games.Services;

namespace Mewdeko.Modules.Games
{
    public partial class Games
    {
        [Group]
        public class ChatterBotCommands : MewdekoSubmodule<ChatterBotService>
        {
            private readonly DbService _db;

            public ChatterBotCommands(DbService db)
            {
                _db = db;
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.ManageMessages)]
            public async Task Cleverbot()
            {
                var channel = (ITextChannel)ctx.Channel;

                if (_service.ChatterBotGuilds.TryRemove(channel.Guild.Id, out _))
                {
                    using (var uow = _db.GetDbContext())
                    {
                        uow.GuildConfigs.SetCleverbotEnabled(ctx.Guild.Id, false);
                        await uow.SaveChangesAsync();
                    }

                    await ReplyConfirmLocalizedAsync("cleverbot_disabled").ConfigureAwait(false);
                    return;
                }

                _service.ChatterBotGuilds.TryAdd(channel.Guild.Id,
                    new Lazy<IChatterBotSession>(() => _service.CreateSession(), true));

                using (var uow = _db.GetDbContext())
                {
                    uow.GuildConfigs.SetCleverbotEnabled(ctx.Guild.Id, true);
                    await uow.SaveChangesAsync();
                }

                await ReplyConfirmLocalizedAsync("cleverbot_enabled").ConfigureAwait(false);
            }
        }
    }
}