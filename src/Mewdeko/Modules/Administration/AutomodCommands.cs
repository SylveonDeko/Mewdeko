using DataModel;
using Discord.Commands;
using Humanizer;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.Administration.Common;
using Mewdeko.Modules.Administration.Services;

namespace Mewdeko.Modules.Administration;

public partial class Administration
{
    /// <summary>
    ///     Commands for the caps filter, attachment, emoji, spoiler and zalgo rules, the media-only, image-only,
    ///     video-only and commands-only channel rules, unsafe files, and the extra options on the existing protections.
    /// </summary>
    /// <param name="protection">The protection service.</param>
    /// <param name="extras">The automod extras service.</param>
    [Group]
    public class AutomodCommands(ProtectionService protection, AutomodExtrasService extras)
        : MewdekoSubmodule<AutomodService>
    {
        /// <summary>
        ///     Shows every automod rule and the extra options on the existing protections.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task Automod()
        {
            await ctx.Channel.SendMessageAsync(embed: Service.BuildOverview(ctx.Guild.Id, protection).Build())
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Turns the caps filter off.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public Task AutomodCaps()
        {
            return DisableAsync(AutomodRule.Caps);
        }

        /// <summary>
        ///     Turns on the caps filter.
        /// </summary>
        /// <param name="minLength">The shortest message that is checked.</param>
        /// <param name="percent">The share of letters that must be uppercase.</param>
        /// <param name="actions">The actions to apply, for example "delete warn".</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task AutomodCaps(int minLength, int percent, [Remainder] string actions)
        {
            if (minLength is < 1 or > 2000 || percent is < 1 or > 100)
            {
                await ReplyErrorAsync(Strings.InvalidInput(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await EnableAsync(AutomodRule.Caps, actions, x =>
            {
                x.MinLength = minLength;
                x.CapsPercent = percent;
            }).ConfigureAwait(false);
        }

        /// <summary>
        ///     Turns attachment spam protection off.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public Task AutomodAttachments()
        {
            return DisableAsync(AutomodRule.Attachments);
        }

        /// <summary>
        ///     Turns on attachment spam protection.
        /// </summary>
        /// <param name="max">How many attachments one member may post in the window.</param>
        /// <param name="seconds">The length of the window in seconds.</param>
        /// <param name="actions">The actions to apply, for example "delete timeout".</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task AutomodAttachments(int max, int seconds, [Remainder] string actions)
        {
            if (max is < 1 or > 100 || seconds is < 1 or > 600)
            {
                await ReplyErrorAsync(Strings.InvalidInput(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await EnableAsync(AutomodRule.Attachments, actions, x =>
            {
                x.MaxAttachments = max;
                x.WindowSeconds = seconds;
            }).ConfigureAwait(false);
        }

        /// <summary>
        ///     Turns media-only channels off.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public Task AutomodMedia()
        {
            return DisableAsync(AutomodRule.Media);
        }

        /// <summary>
        ///     Turns on media-only channels.
        /// </summary>
        /// <param name="actions">The actions to apply, for example "delete dm".</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public Task AutomodMedia([Remainder] string actions)
        {
            return EnableAsync(AutomodRule.Media, actions, _ =>
            {
            });
        }

        /// <summary>
        ///     Adds a channel to the media-only list, or removes it.
        /// </summary>
        /// <param name="channel">The channel to toggle.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task AutomodMediaChannel([Remainder] IGuildChannel channel)
        {
            var added = await Service.ToggleChannelAsync(ctx.Guild.Id, AutomodRule.Media, channel.Id)
                .ConfigureAwait(false);
            var mention = $"<#{channel.Id}>";
            await ReplyConfirmAsync(added
                ? Strings.AutomodMediaChannelAdded(ctx.Guild.Id, mention)
                : Strings.AutomodMediaChannelRemoved(ctx.Guild.Id, mention)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Turns image-only channels off.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public Task AutomodImageOnly()
        {
            return DisableAsync(AutomodRule.ImageOnly);
        }

        /// <summary>
        ///     Turns on image-only channels.
        /// </summary>
        /// <param name="actions">The actions to apply, for example "delete dm".</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public Task AutomodImageOnly([Remainder] string actions)
        {
            return EnableAsync(AutomodRule.ImageOnly, actions, _ =>
            {
            });
        }

        /// <summary>
        ///     Adds a channel to the image-only list, or removes it.
        /// </summary>
        /// <param name="channel">The channel to toggle.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public Task AutomodImageChannel([Remainder] IGuildChannel channel)
        {
            return ToggleChannelAsync(AutomodRule.ImageOnly, channel);
        }

        /// <summary>
        ///     Turns video-only channels off.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public Task AutomodVideoOnly()
        {
            return DisableAsync(AutomodRule.VideoOnly);
        }

        /// <summary>
        ///     Turns on video-only channels.
        /// </summary>
        /// <param name="actions">The actions to apply, for example "delete dm".</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public Task AutomodVideoOnly([Remainder] string actions)
        {
            return EnableAsync(AutomodRule.VideoOnly, actions, _ =>
            {
            });
        }

        /// <summary>
        ///     Adds a channel to the video-only list, or removes it.
        /// </summary>
        /// <param name="channel">The channel to toggle.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public Task AutomodVideoChannel([Remainder] IGuildChannel channel)
        {
            return ToggleChannelAsync(AutomodRule.VideoOnly, channel);
        }

        /// <summary>
        ///     Turns commands-only channels off.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public Task AutomodCommandsOnly()
        {
            return DisableAsync(AutomodRule.Commands);
        }

        /// <summary>
        ///     Turns on commands-only channels.
        /// </summary>
        /// <param name="actions">The actions to apply, for example "delete message".</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public Task AutomodCommandsOnly([Remainder] string actions)
        {
            return EnableAsync(AutomodRule.Commands, actions, _ =>
            {
            });
        }

        /// <summary>
        ///     Adds a channel to the commands-only list, or removes it.
        /// </summary>
        /// <param name="channel">The channel to toggle.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public Task AutomodCommandsChannel([Remainder] IGuildChannel channel)
        {
            return ToggleChannelAsync(AutomodRule.Commands, channel);
        }

        /// <summary>
        ///     Turns emoji spam protection off.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public Task AutomodEmoji()
        {
            return DisableAsync(AutomodRule.Emoji);
        }

        /// <summary>
        ///     Turns on emoji spam protection.
        /// </summary>
        /// <param name="max">How many emojis a message may hold.</param>
        /// <param name="actions">The actions to apply, for example "delete warn".</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task AutomodEmoji(int max, [Remainder] string actions)
        {
            if (max is < 1 or > 200)
            {
                await ReplyErrorAsync(Strings.InvalidInput(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await EnableAsync(AutomodRule.Emoji, actions, x => x.MaxEmojis = max).ConfigureAwait(false);
        }

        /// <summary>
        ///     Turns spoiler spam protection off.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public Task AutomodSpoilers()
        {
            return DisableAsync(AutomodRule.Spoilers);
        }

        /// <summary>
        ///     Turns on spoiler spam protection.
        /// </summary>
        /// <param name="max">How many spoilers a message may hold.</param>
        /// <param name="actions">The actions to apply, for example "delete warn".</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task AutomodSpoilers(int max, [Remainder] string actions)
        {
            if (max is < 1 or > 100)
            {
                await ReplyErrorAsync(Strings.InvalidInput(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await EnableAsync(AutomodRule.Spoilers, actions, x => x.MaxSpoilers = max).ConfigureAwait(false);
        }

        /// <summary>
        ///     Turns the zalgo text filter off.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public Task AutomodZalgo()
        {
            return DisableAsync(AutomodRule.Zalgo);
        }

        /// <summary>
        ///     Turns on the zalgo text filter.
        /// </summary>
        /// <param name="marks">How many combining marks one character may carry.</param>
        /// <param name="actions">The actions to apply, for example "delete warn".</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task AutomodZalgo(int marks, [Remainder] string actions)
        {
            if (marks is < 1 or > 30)
            {
                await ReplyErrorAsync(Strings.InvalidInput(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await EnableAsync(AutomodRule.Zalgo, actions, x => x.ZalgoThreshold = marks).ConfigureAwait(false);
        }

        /// <summary>
        ///     Toggles whether messages in threads of media-only channels are allowed.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task AutomodMediaThreads()
        {
            var allow = !(Service.Get(ctx.Guild.Id, AutomodRule.Media)?.AllowThreads ?? false);
            await Service.UpdateAsync(ctx.Guild.Id, AutomodRule.Media, x => x.AllowThreads = allow)
                .ConfigureAwait(false);
            await ReplyConfirmAsync(allow
                ? Strings.AutomodMediaThreadsOn(ctx.Guild.Id)
                : Strings.AutomodMediaThreadsOff(ctx.Guild.Id)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Turns unsafe file blocking off.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public Task AutomodFiles()
        {
            return DisableAsync(AutomodRule.Files);
        }

        /// <summary>
        ///     Turns on unsafe file blocking.
        /// </summary>
        /// <param name="actions">The actions to apply, for example "delete warn".</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public Task AutomodFiles([Remainder] string actions)
        {
            return EnableAsync(AutomodRule.Files, actions, _ =>
            {
            });
        }

        /// <summary>
        ///     Shows the blocked file types.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task AutomodFileType()
        {
            var list = AutomodService.GetExtensions(Service.Get(ctx.Guild.Id, AutomodRule.Files));
            await ReplyConfirmAsync(Strings.AutomodExtList(ctx.Guild.Id, string.Join(", ", list)))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Adds a file type to the blocked list, removes it, or puts the list back to the default.
        /// </summary>
        /// <param name="extension">The extension to toggle, or "reset".</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task AutomodFileType(string extension)
        {
            if (extension.Equals("reset", StringComparison.OrdinalIgnoreCase))
            {
                await Service.ResetExtensionsAsync(ctx.Guild.Id).ConfigureAwait(false);
                await ReplyConfirmAsync(Strings.AutomodExtReset(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var normalized = extension.TrimStart('.').ToLowerInvariant();
            if (normalized.Length is 0 or > 16 || !normalized.All(char.IsLetterOrDigit))
            {
                await ReplyErrorAsync(Strings.InvalidInput(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var added = await Service.ToggleExtensionAsync(ctx.Guild.Id, normalized).ConfigureAwait(false);
            await ReplyConfirmAsync(added
                ? Strings.AutomodExtAdded(ctx.Guild.Id, normalized)
                : Strings.AutomodExtRemoved(ctx.Guild.Id, normalized)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets how long timeouts and mutes from a rule last.
        /// </summary>
        /// <param name="rule">The rule.</param>
        /// <param name="time">The duration.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task AutomodDuration(AutomodRule rule, StoopidTime time)
        {
            if (time.Time < TimeSpan.FromMinutes(1))
            {
                await ReplyErrorAsync(Strings.TimeoutNeedsTime(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (time.Time.Days > 28)
            {
                await ReplyErrorAsync(Strings.TimeoutLengthTooLong(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await Service.UpdateAsync(ctx.Guild.Id, rule, x => x.ActionMinutes = (int)time.Time.TotalMinutes)
                .ConfigureAwait(false);
            await ReplyConfirmAsync(Strings.AutomodDurationSet(ctx.Guild.Id, Service.RuleName(ctx.Guild.Id, rule),
                time.Time.Humanize())).ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets the message a rule sends in the channel, or puts the default back.
        /// </summary>
        /// <param name="rule">The rule.</param>
        /// <param name="text">The message, or nothing for the default.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task AutomodMessage(AutomodRule rule, [Remainder] string? text = null)
        {
            var custom = string.IsNullOrWhiteSpace(text) ? null : text.Trim().TrimTo(500);
            await Service.UpdateAsync(ctx.Guild.Id, rule, x => x.ActionMessage = custom).ConfigureAwait(false);
            var name = Service.RuleName(ctx.Guild.Id, rule);
            await ReplyConfirmAsync(custom is null
                ? Strings.AutomodMessageReset(ctx.Guild.Id, name)
                : Strings.AutomodMessageSet(ctx.Guild.Id, name)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Toggles a role as exempt from a rule.
        /// </summary>
        /// <param name="rule">The rule.</param>
        /// <param name="role">The role to toggle.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task AutomodExempt(AutomodRule rule, [Remainder] IRole role)
        {
            var added = await Service.ToggleExemptAsync(ctx.Guild.Id, rule, role.Id, true).ConfigureAwait(false);
            var name = Service.RuleName(ctx.Guild.Id, rule);
            await ReplyConfirmAsync(added
                ? Strings.AutomodExemptRoleAdded(ctx.Guild.Id, role.Mention, name)
                : Strings.AutomodExemptRoleRemoved(ctx.Guild.Id, role.Mention, name)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Toggles a channel as exempt from a rule.
        /// </summary>
        /// <param name="rule">The rule.</param>
        /// <param name="channel">The channel to toggle.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task AutomodExempt(AutomodRule rule, [Remainder] IGuildChannel channel)
        {
            var added = await Service.ToggleExemptAsync(ctx.Guild.Id, rule, channel.Id, false).ConfigureAwait(false);
            var name = Service.RuleName(ctx.Guild.Id, rule);
            await ReplyConfirmAsync(added
                ? Strings.AutomodExemptChannelAdded(ctx.Guild.Id, $"<#{channel.Id}>", name)
                : Strings.AutomodExemptChannelRemoved(ctx.Guild.Id, $"<#{channel.Id}>", name)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets the channel every automod action is posted in, or turns the log off.
        /// </summary>
        /// <param name="channel">The channel, or nothing to turn the log off.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task AutomodLog([Remainder] ITextChannel? channel = null)
        {
            await extras.UpdateAsync(ctx.Guild.Id, x => x.LogChannelId = channel?.Id).ConfigureAwait(false);
            await ReplyConfirmAsync(channel is null
                ? Strings.AutomodLogCleared(ctx.Guild.Id)
                : Strings.AutomodLogSet(ctx.Guild.Id, channel.Mention)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets the staff channel flagged messages are sent to for review, or turns review off.
        /// </summary>
        /// <param name="channel">The channel, or nothing to turn review off.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task AutomodReview([Remainder] ITextChannel? channel = null)
        {
            await extras.UpdateAsync(ctx.Guild.Id, x => x.ReviewChannelId = channel?.Id).ConfigureAwait(false);
            await ReplyConfirmAsync(channel is null
                ? Strings.AutomodReviewCleared(ctx.Guild.Id)
                : Strings.AutomodReviewSet(ctx.Guild.Id, channel.Mention)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Toggles a role as exempt from every automod rule, protection and filter.
        /// </summary>
        /// <param name="role">The role to toggle.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task AutomodServerExempt([Remainder] IRole role)
        {
            var added = await extras.ToggleExemptAsync(ctx.Guild.Id, role.Id, true).ConfigureAwait(false);
            await ReplyConfirmAsync(added
                ? Strings.AutomodServerExemptAdded(ctx.Guild.Id, role.Mention)
                : Strings.AutomodServerExemptRemoved(ctx.Guild.Id, role.Mention)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Toggles a channel as exempt from every automod rule, protection and filter.
        /// </summary>
        /// <param name="channel">The channel to toggle.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task AutomodServerExempt([Remainder] IGuildChannel channel)
        {
            var added = await extras.ToggleExemptAsync(ctx.Guild.Id, channel.Id, false).ConfigureAwait(false);
            await ReplyConfirmAsync(added
                ? Strings.AutomodServerExemptAdded(ctx.Guild.Id, $"<#{channel.Id}>")
                : Strings.AutomodServerExemptRemoved(ctx.Guild.Id, $"<#{channel.Id}>")).ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets whether an existing protection also deletes the offending message and DMs the member.
        /// </summary>
        /// <param name="target">The protection: Spam, MassMention, Pattern or MassPost.</param>
        /// <param name="deleteMessage">Whether to delete the message.</param>
        /// <param name="notifyUser">Whether to DM the member.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task ProtectionOptions(ProtectionOptionTarget target, bool deleteMessage, bool notifyUser)
        {
            var name = AutomodService.ProtectionName(target);
            var result = await protection
                .SetProtectionOptionsAsync(ctx.Guild.Id, target, target == ProtectionOptionTarget.Pattern
                    ? null
                    : deleteMessage, notifyUser).ConfigureAwait(false);

            switch (result)
            {
                case ProtectionOptionResult.NotEnabled:
                    await ReplyErrorAsync(Strings.ProtectionOptionNotEnabled(ctx.Guild.Id, name)).ConfigureAwait(false);
                    return;
                case ProtectionOptionResult.Unsupported:
                    await ReplyErrorAsync(Strings.ProtectionOptionUnsupported(ctx.Guild.Id, name))
                        .ConfigureAwait(false);
                    return;
            }

            await ReplyConfirmAsync(Strings.ProtectionOptionsSet(ctx.Guild.Id, name, deleteMessage, notifyUser))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Toggles a role as exempt from Anti-Mass-Mention or Anti-Pattern.
        /// </summary>
        /// <param name="target">The protection: MassMention or Pattern.</param>
        /// <param name="role">The role to toggle.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public Task ProtectionExempt(ProtectionOptionTarget target, [Remainder] IRole role)
        {
            return ToggleProtectionExemptAsync(target, role.Id, true, role.Mention);
        }

        /// <summary>
        ///     Toggles a channel as exempt from Anti-Mass-Mention.
        /// </summary>
        /// <param name="target">The protection: MassMention.</param>
        /// <param name="channel">The channel to toggle.</param>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public Task ProtectionExempt(ProtectionOptionTarget target, [Remainder] IGuildChannel channel)
        {
            return ToggleProtectionExemptAsync(target, channel.Id, false, $"<#{channel.Id}>");
        }

        private async Task ToggleProtectionExemptAsync(ProtectionOptionTarget target, ulong id, bool isRole,
            string mention)
        {
            var name = AutomodService.ProtectionName(target);
            var (result, added) = await protection.ToggleProtectionExemptAsync(ctx.Guild.Id, target, id, isRole)
                .ConfigureAwait(false);

            switch (result)
            {
                case ProtectionOptionResult.NotEnabled:
                    await ReplyErrorAsync(Strings.ProtectionOptionNotEnabled(ctx.Guild.Id, name)).ConfigureAwait(false);
                    return;
                case ProtectionOptionResult.Unsupported:
                    await ReplyErrorAsync(Strings.ProtectionOptionUnsupported(ctx.Guild.Id, name))
                        .ConfigureAwait(false);
                    return;
            }

            await ReplyConfirmAsync(added
                ? Strings.ProtectionExemptAdded(ctx.Guild.Id, mention, name)
                : Strings.ProtectionExemptRemoved(ctx.Guild.Id, mention, name)).ConfigureAwait(false);
        }

        private async Task ToggleChannelAsync(AutomodRule rule, IGuildChannel channel)
        {
            var added = await Service.ToggleChannelAsync(ctx.Guild.Id, rule, channel.Id).ConfigureAwait(false);
            var mention = $"<#{channel.Id}>";
            var name = Service.RuleName(ctx.Guild.Id, rule);
            await ReplyConfirmAsync(added
                ? Strings.AutomodChannelAdded(ctx.Guild.Id, mention, name)
                : Strings.AutomodChannelRemoved(ctx.Guild.Id, mention, name)).ConfigureAwait(false);
        }

        private async Task DisableAsync(AutomodRule rule)
        {
            var (ok, message) = await Service.DisableRuleAsync(ctx.Guild.Id, rule).ConfigureAwait(false);
            await (ok ? ReplyConfirmAsync(message) : ReplyErrorAsync(message)).ConfigureAwait(false);
        }

        private async Task EnableAsync(AutomodRule rule, string actionText, Action<AutomodRuleSetting> change)
        {
            var (ok, message) = await Service.EnableRuleAsync(ctx.Guild.Id, rule, actionText, change)
                .ConfigureAwait(false);
            await (ok ? ReplyConfirmAsync(message) : ReplyErrorAsync(message)).ConfigureAwait(false);
        }
    }
}
