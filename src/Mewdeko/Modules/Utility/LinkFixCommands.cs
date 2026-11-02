using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Utility.Common;
using Mewdeko.Modules.Utility.Services;

namespace Mewdeko.Modules.Utility;

public partial class Utility
{
    /// <summary>
    ///     Commands for replying to social media links with a copy that goes through an embed fixing service.
    /// </summary>
    /// <param name="guildSettings">The guild settings service, for the prefix.</param>
    public class LinkFixCommands(GuildSettingsService guildSettings) : MewdekoSubmodule<LinkFixService>
    {
        /// <summary>
        ///     Turns link fixing on or off for the server.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task LinkFix()
        {
            var config = await Service.GetConfigAsync(ctx.Guild.Id);
            await Service.SetEnabledAsync(ctx.Guild.Id, !config.Enabled);

            if (config.Enabled)
                await ReplyConfirmAsync(Strings.LinkfixDisabled(ctx.Guild.Id));
            else
                await ReplyConfirmAsync(Strings.LinkfixEnabled(ctx.Guild.Id));
        }

        /// <summary>
        ///     Turns link fixing for one site on or off.
        /// </summary>
        /// <param name="platform">The site, such as twitter or instagram.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task LinkFix(string platform)
        {
            if (LinkFixPlatforms.Find(platform) is not { } site)
            {
                await ReplyErrorAsync(Strings.LinkfixUnknownPlatform(ctx.Guild.Id, PlatformNames()));
                return;
            }

            var config = await Service.GetConfigAsync(ctx.Guild.Id);
            var enable = config.DisabledPlatforms.Contains(site.Name);
            await Service.SetPlatformEnabledAsync(ctx.Guild.Id, site, enable);

            if (enable)
                await ReplyConfirmAsync(Strings.LinkfixPlatformEnabled(ctx.Guild.Id, site.DisplayName));
            else
                await ReplyConfirmAsync(Strings.LinkfixPlatformDisabled(ctx.Guild.Id, site.DisplayName));
        }

        /// <summary>
        ///     Sets the embed fixing host used for a site, or goes back to the default with "default".
        /// </summary>
        /// <param name="platform">The site, such as twitter or instagram.</param>
        /// <param name="host">The host to rewrite links to, such as fixupx.com, or "default".</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task LinkFixHost(string platform, string host)
        {
            if (LinkFixPlatforms.Find(platform) is not { } site)
            {
                await ReplyErrorAsync(Strings.LinkfixUnknownPlatform(ctx.Guild.Id, PlatformNames()));
                return;
            }

            if (host.Equals("default", StringComparison.OrdinalIgnoreCase))
            {
                await Service.SetHostAsync(ctx.Guild.Id, site, null);
                await ReplyConfirmAsync(Strings.LinkfixHostReset(ctx.Guild.Id, site.DisplayName, site.DefaultHost));
                return;
            }

            host = host.Trim().TrimEnd('/');
            if (host.Contains("://"))
                host = host[(host.IndexOf("://", StringComparison.Ordinal) + 3)..];

            if (!LinkFixPlatforms.IsValidHost(host))
            {
                await ReplyErrorAsync(Strings.LinkfixInvalidHost(ctx.Guild.Id));
                return;
            }

            await Service.SetHostAsync(ctx.Guild.Id, site, host);
            await ReplyConfirmAsync(Strings.LinkfixHostSet(ctx.Guild.Id, site.DisplayName, host.ToLowerInvariant()));
        }

        /// <summary>
        ///     Shows whether link fixing is on, and the host and state of each site.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task LinkFixes()
        {
            var config = await Service.GetConfigAsync(ctx.Guild.Id);
            var status = config.Enabled
                ? Strings.LinkfixStatusOn(ctx.Guild.Id)
                : Strings.LinkfixStatusOff(ctx.Guild.Id, await guildSettings.GetPrefix(ctx.Guild));

            var lines = LinkFixPlatforms.All.Select(site => Strings.LinkfixListEntry(ctx.Guild.Id, site.DisplayName,
                config.Hosts.GetValueOrDefault(site.Name, site.DefaultHost),
                config.DisabledPlatforms.Contains(site.Name) ? Strings.LinkfixOff(ctx.Guild.Id) : Strings.LinkfixOn(ctx.Guild.Id)));

            await ctx.Channel.SendMessageAsync(embed: new EmbedBuilder()
                .WithOkColor()
                .WithTitle(Strings.LinkfixListTitle(ctx.Guild.Id))
                .WithDescription($"{status}\n\n{string.Join('\n', lines)}")
                .Build());
        }

        private static string PlatformNames() => string.Join(", ", LinkFixPlatforms.All.Select(site => site.Name));
    }
}
