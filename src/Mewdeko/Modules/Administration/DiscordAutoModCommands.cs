using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Administration.Common;
using Mewdeko.Modules.Administration.Services;

namespace Mewdeko.Modules.Administration;

public partial class Administration
{
    /// <summary>
    ///     Commands for managing Discord's native AutoMod rules and what Mewdeko does when they fire.
    /// </summary>
    [Group]
    public class DiscordAutoModCommands : MewdekoSubmodule<DiscordAutoModService>
    {
        /// <summary>
        ///     Lists the server's native AutoMod rules.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task AutoModList()
        {
            var guildId = ctx.Guild.Id;
            var (failure, rules) = await Service.ListAsync(ctx.Guild);
            if (failure is not null)
            {
                await ReplyErrorAsync(failure.Message);
                return;
            }

            if (rules.Count == 0)
            {
                await ReplyErrorAsync(Strings.AutomodListEmpty(guildId));
                return;
            }

            await ctx.Channel.SendMessageAsync(embed: new EmbedBuilder()
                .WithOkColor()
                .WithTitle(Strings.AutomodListTitle(guildId))
                .WithDescription(string.Join("\n\n", rules.Select(x => Service.FormatRule(guildId, x))))
                .Build(), allowedMentions: AllowedMentions.None);
        }

        /// <summary>
        ///     Creates a native AutoMod rule.
        /// </summary>
        /// <param name="type">keyword, preset, mention or spam.</param>
        /// <param name="name">The rule name, in quotes if it has spaces.</param>
        /// <param name="value">Keywords, presets or the mention limit, depending on the type.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task AutoModCreate(string type, string name, [Remainder] string? value = null)
        {
            var guildId = ctx.Guild.Id;
            var spec = new AutoModRuleSpec
            {
                Name = name
            };

            switch (type.ToLowerInvariant())
            {
                case "keyword":
                case "keywords":
                    spec.Kind = AutoModRuleKind.Keyword;
                    spec.Keywords = value is null
                        ? []
                        : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                            .ToList();
                    break;
                case "preset":
                case "presets":
                    spec.Kind = AutoModRuleKind.Preset;
                    spec.Presets = DiscordAutoModService.ParsePresets(value ?? "");
                    break;
                case "mention":
                case "mentions":
                    spec.Kind = AutoModRuleKind.MentionSpam;
                    spec.MentionLimit = int.TryParse(value, out var limit) ? limit : 0;
                    break;
                case "spam":
                    spec.Kind = AutoModRuleKind.Spam;
                    break;
                default:
                    await ReplyErrorAsync(Strings.AutomodUnknownType(guildId));
                    return;
            }

            await ReplyAsync(await Service.CreateAsync(ctx.Guild, spec));
        }

        /// <summary>
        ///     Changes a native AutoMod rule.
        /// </summary>
        /// <param name="rule">The rule ID or name.</param>
        /// <param name="option">The option to change.</param>
        /// <param name="value">The value for the option.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task AutoModEdit(string rule, string option, [Remainder] string? value = null)
        {
            var found = await FindAsync(rule);
            if (found is null)
                return;

            await ReplyAsync(await Service.EditAsync(ctx.Guild, found, option, value));
        }

        /// <summary>
        ///     Deletes a native AutoMod rule.
        /// </summary>
        /// <param name="rule">The rule ID or name.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task AutoModDelete([Remainder] string rule)
        {
            var found = await FindAsync(rule);
            if (found is null)
                return;

            await ReplyAsync(await Service.DeleteAsync(ctx.Guild, found.Id));
        }

        /// <summary>
        ///     Shows or changes what Mewdeko does when a native AutoMod rule fires.
        /// </summary>
        /// <param name="rule">The rule ID or name.</param>
        /// <param name="option">show, warn, punish, escalate, logchannel or clear.</param>
        /// <param name="value">The value for the option.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task AutoModFollowUp(string rule, string option = "show", [Remainder] string? value = null)
        {
            var guildId = ctx.Guild.Id;
            var found = await FindAsync(rule);
            if (found is null)
                return;

            if (option.Equals("show", StringComparison.OrdinalIgnoreCase))
            {
                var followUp = await Service.GetFollowUpAsync(guildId, found.Id);
                if (followUp is null)
                {
                    await ReplyErrorAsync(Strings.AutomodFollowupNone(guildId));
                    return;
                }

                await ctx.Channel.SendMessageAsync(embed: new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle(Strings.AutomodFollowupTitle(guildId, found.Name))
                    .WithDescription(Service.FormatFollowUp(guildId, followUp))
                    .Build(), allowedMentions: AllowedMentions.None);
                return;
            }

            var (ok, message) = await Service.SetFollowUpOptionAsync(ctx.Guild, found.Id, option, value);
            if (ok)
                await ReplyConfirmAsync(message);
            else
                await ReplyErrorAsync(message);
        }

        private async Task<IAutoModRule?> FindAsync(string rule)
        {
            if (!await DiscordAutoModService.CanManageAsync(ctx.Guild))
            {
                await ReplyErrorAsync(Strings.AutomodNeedPermission(ctx.Guild.Id));
                return null;
            }

            var found = await Service.FindRuleAsync(ctx.Guild, rule);
            if (found is null)
                await ReplyErrorAsync(Strings.AutomodNotFound(ctx.Guild.Id));
            return found;
        }

        private Task ReplyAsync(AutoModResult result) =>
            result.Ok ? ReplyConfirmAsync(result.Message) : ReplyErrorAsync(result.Message);
    }
}
