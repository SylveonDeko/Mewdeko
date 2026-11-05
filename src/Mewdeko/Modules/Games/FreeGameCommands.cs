using System.Text;
using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Games.Services;

namespace Mewdeko.Modules.Games;

public partial class Games
{
    /// <summary>
    ///     Commands for alerts about games that become free on the Epic Games Store, Steam and GOG.
    /// </summary>
    [Group]
    public class FreeGameCommands : MewdekoSubmodule<FreeGameService>
    {
        /// <summary>
        ///     Posts free game alerts in a channel, with an optional role ping.
        /// </summary>
        /// <param name="channel">The channel to post in.</param>
        /// <param name="role">The role to ping. Leave empty for no ping.</param>
        /// <example>.freegamealert #deals</example>
        /// <example>.freegamealert #deals @Gamers</example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task FreeGameAlert(ITextChannel channel, IRole? role = null)
        {
            await Service.SaveAlertAsync(ctx.Guild.Id, channel.Id, role?.Id);
            await ReplyConfirmAsync(role is null
                ? Strings.FreeGameAdded(ctx.Guild.Id, channel.Mention)
                : Strings.FreeGameAddedRole(ctx.Guild.Id, channel.Mention, role.Mention)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Stops free game alerts in a channel.
        /// </summary>
        /// <param name="channel">The channel to stop alerts in.</param>
        /// <example>.freegameremove #deals</example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task FreeGameRemove(ITextChannel channel)
        {
            await (await Service.RemoveAlertAsync(ctx.Guild.Id, channel.Id)
                ? ReplyConfirmAsync(Strings.FreeGameRemoved(ctx.Guild.Id, channel.Mention))
                : ReplyErrorAsync(Strings.FreeGameNone(ctx.Guild.Id, channel.Mention)));
        }

        /// <summary>
        ///     Chooses which stores a channel gets alerts for.
        /// </summary>
        /// <param name="channel">The alert channel.</param>
        /// <param name="stores">Any of epic, steam and gog.</param>
        /// <example>.freegamestores #deals epic gog</example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task FreeGameStores(ITextChannel channel, [Remainder] string stores)
        {
            var words = stores.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(x => x.ToLowerInvariant()).ToList();
            var epic = words.Contains("epic");
            var steam = words.Contains("steam");
            var gog = words.Contains("gog");
            if (!epic && !steam && !gog)
            {
                await ReplyErrorAsync(Strings.FreeGameStoresInvalid(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var alerts = await Service.GetAlertsAsync(ctx.Guild.Id);
            if (alerts.All(x => x.ChannelId != channel.Id))
            {
                await ReplyErrorAsync(Strings.FreeGameNone(ctx.Guild.Id, channel.Mention)).ConfigureAwait(false);
                return;
            }

            await Service.SaveAlertAsync(ctx.Guild.Id, channel.Id, null, epic, steam, gog, false);
            await ReplyConfirmAsync(Strings.FreeGameStoresSet(ctx.Guild.Id, channel.Mention,
                StoreNames(epic, steam, gog))).ConfigureAwait(false);
        }

        /// <summary>
        ///     Lists the channels that get free game alerts.
        /// </summary>
        /// <example>.freegamelist</example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task FreeGameList()
        {
            var alerts = await Service.GetAlertsAsync(ctx.Guild.Id);
            if (alerts.Count == 0)
            {
                await ReplyErrorAsync(Strings.FreeGameListEmpty(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var text = new StringBuilder();
            foreach (var alert in alerts)
                text.AppendLine(Strings.FreeGameListRow(ctx.Guild.Id, $"<#{alert.ChannelId}>",
                    alert.RoleId is { } roleId ? $"<@&{roleId}>" : "-", StoreNames(alert.Epic, alert.Steam, alert.Gog)));

            await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                .WithTitle(Strings.FreeGameListTitle(ctx.Guild.Id))
                .WithDescription(text.ToString())).ConfigureAwait(false);
        }

        /// <summary>
        ///     Shows the games that are free right now.
        /// </summary>
        /// <example>.freegamesnow</example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task FreeGamesNow()
        {
            var games = await Service.GetCurrentAsync();
            if (games.Count == 0)
            {
                await ReplyErrorAsync(Strings.FreeGameNowNone(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var text = string.Join("\n", games.Select(x => $"[{x.Title}]({x.Url}) ({x.Store})"));
            await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                .WithTitle(Strings.FreeGameNowTitle(ctx.Guild.Id))
                .WithDescription(text)).ConfigureAwait(false);
        }

        private static string StoreNames(bool epic, bool steam, bool gog)
        {
            return string.Join(", ", new[]
            {
                epic ? FreeGameService.Epic : null, steam ? FreeGameService.Steam : null,
                gog ? FreeGameService.Gog : null
            }.Where(x => x is not null));
        }
    }
}
