using Discord.Commands;
using Humanizer;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.Collections;
using Mewdeko.Common.TypeReaders;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Modules.Permissions.Services;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Modules.Permissions;

public partial class Permissions
{
    /// <summary>
    ///     Represents commands for managing command cooldowns.
    /// </summary>
    /// <param name="service">The command cooldown service</param>
    /// <param name="db">The database service</param>
    [Group]
    public class CmdCdsCommands(
        CmdCdService service,
        DbContextProvider dbProvider,
        GuildSettingsService settingsService)
        : MewdekoSubmodule
    {
        private ConcurrentDictionary<ulong, ConcurrentHashSet<ActiveCooldown>> ActiveCooldowns
        {
            get
            {
                return service.ActiveCooldowns;
            }
        }

        /// <summary>
        ///     Sets or clears the cooldown for a specified command in the guild.
        /// </summary>
        /// <param name="command">The command to set the cooldown for.</param>
        /// <param name="time">The duration of the cooldown. Defaults to 0s, clearing the cooldown.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <remarks>
        ///     Command cooldowns can be set between 0 seconds (effectively clearing the cooldown) and 90,000 seconds.
        ///     Setting a cooldown affects all instances of the command within the guild.
        /// </remarks>
        /// <example>
        ///     .cmdcd "command name" 30s - Sets a 30-second cooldown for the specified command.
        ///     .cmdcd "command name" - Clears the cooldown for the specified command.
        /// </example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
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

            await using var dbContext = await dbProvider.GetContextAsync();
            var gConfig = await settingsService.GetGuildConfig(channel.Guild.Id);
            var config = await dbContext.ForGuildId(channel.Guild.Id, set => set.Include(gc => gc.CommandCooldowns));

            var toDelete = config.CommandCooldowns.FirstOrDefault(cc => cc.CommandName == name);
            if (toDelete != null)
                dbContext.CommandCooldown.Remove(toDelete);
            if (time.Time.TotalSeconds != 0)
            {
                var cc = new CommandCooldown
                {
                    CommandName = name, Seconds = Convert.ToInt32(time.Time.TotalSeconds)
                };
                config.CommandCooldowns.Add(cc);
                await settingsService.UpdateGuildConfig(ctx.Guild.Id, gConfig).ConfigureAwait(false);
            }

            if (time.Time.TotalSeconds == 0)
            {
                var activeCds = ActiveCooldowns.GetOrAdd(channel.Guild.Id, []);
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

        /// <summary>
        ///     Displays all commands with active cooldowns in the guild.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <remarks>
        ///     This method lists all commands that currently have a cooldown set, along with the duration of each cooldown.
        ///     If no commands have cooldowns set, a message indicating this will be sent.
        /// </remarks>
        /// <example>
        ///     .allcmdcds - Lists all commands with their respective cooldowns.
        /// </example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task AllCmdCooldowns()
        {
            var channel = (ITextChannel)ctx.Channel;
            var config = await settingsService.GetGuildConfig(channel.Guild.Id);

            if (config.CommandCooldowns.Count == 0)
            {
                await ReplyConfirmLocalizedAsync("cmdcd_none").ConfigureAwait(false);
            }
            else
            {
                await channel.SendTableAsync("",
                        config.CommandCooldowns.Select(c => $"{c.CommandName}: {c.Seconds}{GetText("sec")}"),
                        s => $"{s,-30}", 2)
                    .ConfigureAwait(false);
            }
        }
    }
}