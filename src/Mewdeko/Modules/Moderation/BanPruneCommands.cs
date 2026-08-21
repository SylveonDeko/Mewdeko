using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Moderation.Common;
using Mewdeko.Modules.Moderation.Services;

namespace Mewdeko.Modules.Moderation;

public partial class Moderation
{
    /// <summary>
    ///     Commands for choosing how many days of messages each ban action purges.
    /// </summary>
    /// <param name="banPrune">The service holding the purge settings</param>
    [Group]
    public class BanPruneCommands(BanPruneService banPrune) : MewdekoSubmodule
    {
        /// <summary>
        ///     Shows the purge a ban issued in this channel would use, along with every configured setting.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.BanMembers)]
        public async Task BanPrune()
        {
            var effective = await banPrune
                .GetPruneDaysAsync(ctx.Guild.Id, BanPruneAction.Ban, ctx.Channel)
                .ConfigureAwait(false);

            var settings = await banPrune.GetSettingListAsync(ctx.Guild.Id).ConfigureAwait(false);

            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle(Strings.BanPruneListTitle(ctx.Guild.Id))
                .WithDescription(Strings.BanPruneEffective(ctx.Guild.Id, DescribeDays(effective)));

            if (settings.Count == 0)
            {
                eb.AddField(Strings.BanPruneDefaultsTitle(ctx.Guild.Id),
                    Strings.BanPruneListEmpty(ctx.Guild.Id));
            }
            else
            {
                var guildLines = settings
                    .Where(x => x.ScopeType == (int)BanPruneScope.Guild)
                    .Select(x => Strings.BanPruneCurrent(ctx.Guild.Id, DescribeAction(x.ActionKey),
                        DescribeDays(x.PruneDays)))
                    .ToList();

                var overrideLines = settings
                    .Where(x => x.ScopeType != (int)BanPruneScope.Guild)
                    .Select(x => Strings.BanPruneCurrent(ctx.Guild.Id,
                        $"{DescribeScope((BanPruneScope)x.ScopeType, x.ScopeId)} / {DescribeAction(x.ActionKey)}",
                        DescribeDays(x.PruneDays)))
                    .ToList();

                if (guildLines.Count > 0)
                    eb.AddField(Strings.BanPruneDefaultsTitle(ctx.Guild.Id), string.Join("\n", guildLines));

                if (overrideLines.Count > 0)
                    eb.AddField(Strings.BanPruneOverridesTitle(ctx.Guild.Id), string.Join("\n", overrideLines));
            }

            await ctx.Channel.EmbedAsync(eb).ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets how many days of messages one ban action purges.
        /// </summary>
        /// <param name="action">The action key, or "all" to cover every action</param>
        /// <param name="days">The purge in days, 0 through 7</param>
        /// <param name="target">A channel or category to scope the setting to, or nothing for the server default</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task BanPruneSet(string action, int days, IGuildChannel? target = null)
        {
            if (!TryResolveAction(action, out var resolved))
            {
                await ErrorAsync(Strings.BanPruneActionUnknown(ctx.Guild.Id, action, KnownActions()))
                    .ConfigureAwait(false);
                return;
            }

            var (scope, scopeId) = ResolveScope(target);
            await banPrune.SetAsync(ctx.Guild.Id, scope, scopeId, resolved, days).ConfigureAwait(false);

            await SuccessAsync(Strings.BanPruneSet(ctx.Guild.Id,
                    DescribeAction(resolved?.Key),
                    DescribeDays(Math.Clamp(days, 0, BanPruneService.MaxPruneDays)),
                    DescribeScope(scope, scopeId)))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Removes a purge setting so the action falls back to a broader scope or its default.
        /// </summary>
        /// <param name="action">The action key, or "all" for the setting covering every action</param>
        /// <param name="target">The channel or category the setting is on, or nothing for the server default</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task BanPruneClear(string action, IGuildChannel? target = null)
        {
            if (!TryResolveAction(action, out var resolved))
            {
                await ErrorAsync(Strings.BanPruneActionUnknown(ctx.Guild.Id, action, KnownActions()))
                    .ConfigureAwait(false);
                return;
            }

            var (scope, scopeId) = ResolveScope(target);
            var removed = await banPrune.ClearAsync(ctx.Guild.Id, scope, scopeId, resolved).ConfigureAwait(false);

            var message = removed
                ? Strings.BanPruneCleared(ctx.Guild.Id, DescribeAction(resolved?.Key), DescribeScope(scope, scopeId))
                : Strings.BanPruneClearedNone(ctx.Guild.Id, DescribeAction(resolved?.Key),
                    DescribeScope(scope, scopeId));

            await ConfirmAsync(message).ConfigureAwait(false);
        }

        /// <summary>
        ///     Removes every purge setting in the server.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task BanPruneReset()
        {
            var removed = await banPrune.ResetAsync(ctx.Guild.Id).ConfigureAwait(false);
            await SuccessAsync(Strings.BanPruneReset(ctx.Guild.Id, removed)).ConfigureAwait(false);
        }

        private static bool TryResolveAction(string input, out BanPruneAction? action)
        {
            if (input.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                action = null;
                return true;
            }

            action = BanPruneAction.FromKey(input);
            return action is not null;
        }

        private static (BanPruneScope Scope, ulong ScopeId) ResolveScope(IGuildChannel? target)
        {
            return target switch
            {
                null => (BanPruneScope.Guild, 0UL),
                ICategoryChannel => (BanPruneScope.Category, target.Id),
                _ => (BanPruneScope.Channel, target.Id)
            };
        }

        private static string KnownActions()
        {
            return string.Join(", ", BanPruneAction.All.Select(x => $"`{x.Key}`").Prepend("`all`"));
        }

        private string DescribeAction(string? key)
        {
            return string.IsNullOrEmpty(key)
                ? Strings.BanPruneSetAllActions(ctx.Guild.Id)
                : BanPruneAction.FromKey(key)?.DisplayName ?? key;
        }

        private string DescribeDays(int days)
        {
            return days <= 0
                ? Strings.BanPruneDaysNone(ctx.Guild.Id)
                : Strings.BanPruneDays(ctx.Guild.Id, days);
        }

        private string DescribeScope(BanPruneScope scope, ulong scopeId)
        {
            return scope switch
            {
                BanPruneScope.Category => Strings.BanPruneScopeCategory(ctx.Guild.Id, $"<#{scopeId}>"),
                BanPruneScope.Channel => Strings.BanPruneScopeChannel(ctx.Guild.Id, $"<#{scopeId}>"),
                _ => Strings.BanPruneScopeGuild(ctx.Guild.Id)
            };
        }
    }
}