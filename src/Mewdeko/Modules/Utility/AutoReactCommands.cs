using DataModel;
using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Utility.Services;

namespace Mewdeko.Modules.Utility;

public partial class Utility
{
    /// <summary>
    ///     Commands for rules that react to new messages in a channel.
    /// </summary>
    /// <param name="guildSettings">The guild settings service, for the prefix.</param>
    public class AutoReactCommands(GuildSettingsService guildSettings) : MewdekoSubmodule<AutoReactionService>
    {
        /// <summary>
        ///     Lists, shows, adds, edits, moves, removes, enables or disables auto reaction rules, or sets the manager role.
        /// </summary>
        /// <param name="option">list, show, add, edit, move, remove, enable, disable or manager.</param>
        /// <param name="value">
        ///     The channel and emojis for add, the role for manager, or the rule number and what to change for the
        ///     rest.
        /// </param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [AutoReactManager]
        public async Task AutoReact(string option = "list", [Remainder] string? value = null)
        {
            var guildId = ctx.Guild.Id;
            var prefix = await guildSettings.GetPrefix(ctx.Guild);
            value = value?.Trim();

            switch (option.ToLowerInvariant())
            {
                case "list":
                {
                    var rules = await Service.GetRulesAsync(guildId);
                    if (rules.Count == 0)
                    {
                        await ReplyErrorAsync(Strings.AutoReactListEmpty(guildId, prefix));
                        return;
                    }

                    await ctx.Channel.SendMessageAsync(embed: Service.ListEmbed(guildId, rules),
                        allowedMentions: AllowedMentions.None);
                    return;
                }
                case "add":
                    await AddAsync(value, prefix);
                    return;
                case "manager":
                    await ManagerAsync(value, prefix);
                    return;
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                await ReplyErrorAsync(Strings.AutoReactMissingArgument(guildId, prefix));
                return;
            }

            var parts = value.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries);
            if (!int.TryParse(parts[0].TrimStart('#'), out var id))
            {
                await ReplyErrorAsync(Strings.AutoReactBadId(guildId, prefix));
                return;
            }

            switch (option.ToLowerInvariant())
            {
                case "show":
                {
                    var rule = await Service.GetRuleAsync(guildId, id);
                    if (rule is null)
                        await ReplyErrorAsync(Strings.AutoReactNotFound(guildId, id, prefix));
                    else
                        await ctx.Channel.SendMessageAsync(embed: Service.ShowEmbed(rule),
                            allowedMentions: AllowedMentions.None);
                    return;
                }
                case "remove":
                case "delete":
                    if (await Service.RemoveAsync(guildId, id))
                        await ReplyConfirmAsync(Strings.AutoReactRemoved(guildId, id));
                    else
                        await ReplyErrorAsync(Strings.AutoReactNotFound(guildId, id, prefix));
                    return;
                case "enable":
                case "on":
                    await ApplyAsync(id, x => x.Enabled = true, Strings.AutoReactEnabled(guildId, id), prefix);
                    return;
                case "disable":
                case "off":
                    await ApplyAsync(id, x => x.Enabled = false, Strings.AutoReactDisabled(guildId, id), prefix);
                    return;
                case "edit":
                    await EditAsync(id, parts.Length > 1 ? parts[1] : null, prefix);
                    return;
                case "move":
                {
                    if (parts.Length < 2 || !int.TryParse(parts[1], out var position))
                    {
                        await ReplyErrorAsync(Strings.AutoReactBadPosition(guildId, prefix));
                        return;
                    }

                    var (found, moved) = await Service.MoveAsync(guildId, id, position);
                    if (found)
                        await ReplyConfirmAsync(Strings.AutoReactMoved(guildId, id, moved));
                    else
                        await ReplyErrorAsync(Strings.AutoReactNotFound(guildId, id, prefix));
                    return;
                }
                default:
                    await ReplyErrorAsync(Strings.AutoReactUnknownOption(guildId));
                    return;
            }
        }

        private async Task ManagerAsync(string? value, string prefix)
        {
            var guildId = ctx.Guild.Id;
            if (string.IsNullOrWhiteSpace(value))
            {
                var current = await Service.GetManagerRoleAsync(guildId);
                if (current is { } roleId)
                    await ReplyConfirmAsync(Strings.AutoReactManagerCurrent(guildId, $"<@&{roleId}>"));
                else
                    await ReplyErrorAsync(Strings.AutoReactManagerNone(guildId, prefix));
                return;
            }

            if (!((IGuildUser)ctx.User).GuildPermissions.ManageGuild)
            {
                await ReplyErrorAsync(Strings.AutoReactManagerNeedsPerm(guildId));
                return;
            }

            if (IsNone(value.Trim()))
            {
                await Service.SetManagerRoleAsync(guildId, null);
                await ReplyConfirmAsync(Strings.AutoReactManagerCleared(guildId));
                return;
            }

            var id = AutoReactionService.ExtractIds(value).FirstOrDefault();
            var role = id == 0 ? null : ctx.Guild.GetRole(id);
            if (role is null)
            {
                await ReplyErrorAsync(Strings.AutoReactBadValue(guildId, "manager"));
                return;
            }

            await Service.SetManagerRoleAsync(guildId, role.Id);
            await ReplyConfirmAsync(Strings.AutoReactManagerSet(guildId, role.Mention));
        }

        private async Task AddAsync(string? value, string prefix)
        {
            var guildId = ctx.Guild.Id;
            var parts = (value ?? "").Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                await ReplyErrorAsync(Strings.AutoReactMissingArgument(guildId, prefix));
                return;
            }

            var channel = await ReadChannelAsync(parts[0]);
            if (channel is null)
            {
                await ReplyErrorAsync(Strings.AutoReactBadChannel(guildId));
                return;
            }

            var bad = AutoReactionService.TryParseEmojis(ctx.Guild, parts[1], out var emojis);
            if (bad is not null)
            {
                await ReplyErrorAsync(Strings.AutoReactBadEmoji(guildId, bad));
                return;
            }

            var (error, rule) = await Service.AddAsync(guildId, new AutoReaction
            {
                ChannelId = channel.Id, Emojis = emojis
            });
            if (rule is null)
            {
                await ReplyErrorAsync(Service.ErrorMessage(guildId, error, 0, prefix));
                return;
            }

            await ReplyConfirmAsync(Strings.AutoReactAdded(guildId, rule.Id, $"<#{channel.Id}>"));
            await WarnAboutPermsAsync(channel);
        }

        private async Task EditAsync(int id, string? edit, string prefix)
        {
            var guildId = ctx.Guild.Id;
            var parts = (edit ?? "").Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                await ReplyErrorAsync(Strings.AutoReactMissingArgument(guildId, prefix));
                return;
            }

            var name = parts[0].ToLowerInvariant();
            var text = parts[1];
            var on = ParseBool(text);
            IGuildChannel? channel = null;
            string emojis = "";
            Action<AutoReaction>? change = null;

            switch (name)
            {
                case "channel":
                    channel = await ReadChannelAsync(text);
                    if (channel is null)
                    {
                        await ReplyErrorAsync(Strings.AutoReactBadChannel(guildId));
                        return;
                    }

                    change = x => x.ChannelId = channel.Id;
                    break;
                case "emojis":
                {
                    var bad = AutoReactionService.TryParseEmojis(ctx.Guild, text, out emojis);
                    if (bad is not null)
                    {
                        await ReplyErrorAsync(Strings.AutoReactBadEmoji(guildId, bad));
                        return;
                    }

                    var stored = emojis;
                    change = x => x.Emojis = stored;
                    break;
                }
                case "type":
                {
                    if (!TryParseType(text, out var type))
                    {
                        await ReplyErrorAsync(Strings.AutoReactBadValue(guildId, name));
                        return;
                    }

                    change = x => x.MessageTypes = type;
                    break;
                }
                case "bots":
                case "webhooks":
                case "replies":
                case "delete":
                    if (on is null)
                    {
                        await ReplyErrorAsync(Strings.AutoReactBadValue(guildId, name));
                        return;
                    }

                    change = name switch
                    {
                        "bots" => x => x.IgnoreBots = on.Value,
                        "webhooks" => x => x.IgnoreWebhooks = on.Value,
                        "delete" => x => x.DeleteNonMatching = on.Value,
                        _ => x => x.IgnoreReplies = on.Value
                    };
                    break;
                case "allowedroles":
                case "ignoredroles":
                case "ignoredmembers":
                {
                    var ids = IsNone(text) ? "" : AutoReactionService.JoinIds(AutoReactionService.ExtractIds(text));
                    change = name switch
                    {
                        "allowedroles" => x => x.AllowedRoleIds = ids,
                        "ignoredroles" => x => x.IgnoredRoleIds = ids,
                        _ => x => x.IgnoredUserIds = ids
                    };
                    break;
                }
                case "skipwords":
                case "requirewords":
                case "requiredwords":
                {
                    var words = IsNone(text) ? "" : AutoReactionService.JoinWords(text.Split(','));
                    if (words is null)
                    {
                        await ReplyErrorAsync(Strings.AutoReactBadWords(guildId, AutoReactionService.MaxWords,
                            AutoReactionService.MaxWordLength));
                        return;
                    }

                    change = name == "skipwords" ? x => x.SkipWords = words : x => x.RequiredWords = words;
                    break;
                }
                default:
                    await ReplyErrorAsync(Strings.AutoReactUnknownEdit(guildId));
                    return;
            }

            await ApplyAsync(id, change, Strings.AutoReactUpdated(guildId, id), prefix);
            if (channel is not null)
                await WarnAboutPermsAsync(channel);
            if (name == "delete" && on == true && await Service.GetRuleAsync(guildId, id) is { } rule &&
                await ctx.Guild.GetChannelAsync(rule.ChannelId) is { } ruleChannel)
                await WarnAboutDeletePermsAsync(ruleChannel);
        }

        private async Task WarnAboutDeletePermsAsync(IGuildChannel channel)
        {
            var me = await ctx.Guild.GetCurrentUserAsync();
            if (!me.GetPermissions(channel).ManageMessages)
                await ReplyErrorAsync(Strings.AutoReactMissingDeletePerm(ctx.Guild.Id, $"<#{channel.Id}>"));
        }

        private async Task ApplyAsync(int id, Action<AutoReaction> change, string success, string prefix)
        {
            var (error, _) = await Service.UpdateAsync(ctx.Guild.Id, id, change);
            if (error == AutoReactionError.None)
                await ReplyConfirmAsync(success);
            else
                await ReplyErrorAsync(Service.ErrorMessage(ctx.Guild.Id, error, id, prefix));
        }

        private async Task WarnAboutPermsAsync(IGuildChannel channel)
        {
            var me = await ctx.Guild.GetCurrentUserAsync();
            var perms = me.GetPermissions(channel);
            if (!perms.AddReactions || !perms.ReadMessageHistory)
                await ReplyErrorAsync(Strings.AutoReactMissingPerms(ctx.Guild.Id, $"<#{channel.Id}>"));
        }

        private async Task<IGuildChannel?> ReadChannelAsync(string text)
        {
            var id = AutoReactionService.ExtractIds(text).FirstOrDefault();
            if (id == 0)
                return null;

            var channel = await ctx.Guild.GetChannelAsync(id);
            return channel is ITextChannel or IForumChannel ? channel : null;
        }

        private static bool IsNone(string text) => text.Equals("none", StringComparison.OrdinalIgnoreCase);

        private static bool? ParseBool(string text) => text.ToLowerInvariant() switch
        {
            "on" or "true" or "yes" or "y" => true,
            "off" or "false" or "no" or "n" => false,
            _ => null
        };

        private static bool TryParseType(string text, out AutoReactionMessageType type)
        {
            if (Enum.TryParse(text, true, out type) && Enum.IsDefined(type))
                return true;
            return Enum.TryParse(text + "s", true, out type) && Enum.IsDefined(type);
        }
    }
}
