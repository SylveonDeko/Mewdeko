using System.Text.Json;
using DataModel;
using Discord.Interactions;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.Autocompleters;
using Mewdeko.Common.Modals;
using Mewdeko.Modules.Chat_Triggers.Common;
using Mewdeko.Modules.Chat_Triggers.Services;
using Mewdeko.Modules.Utility.Common;

namespace Mewdeko.Modules.Chat_Triggers;

/// <summary>
///     Additional slash commands for chat triggers: reaction triggers, limits, responses, rewards, counters,
///     events and categories.
/// </summary>
public partial class SlashChatTriggers
{
    /// <summary>
    ///     Adds a new reaction-based chat trigger. The response is collected through a modal.
    /// </summary>
    /// <param name="reaction">The emoji or emote that will trigger this response.</param>
    [SlashCommand("react-add", "Adds a chat trigger that fires when a reaction is added")]
    [SlashUserPerm(GuildPermission.Administrator)]
    [CheckPermissions]
    public Task AddReactionTrigger(
        [Summary("reaction", "The emoji or emote that triggers the response")]
        string reaction)
    {
        return RespondWithModalAsync<ChatTriggerReactionAddModal>(
            $"ct_react_add:{Uri.EscapeDataString(reaction)}");
    }

    /// <summary>
    ///     Handles the reaction trigger modal submission.
    /// </summary>
    /// <param name="escapedReaction">The url-escaped reaction string.</param>
    /// <param name="modal">The modal containing the response message.</param>
    [ModalInteraction("ct_react_add:*", true)]
    [SlashUserPerm(GuildPermission.Administrator)]
    [CheckPermissions]
    public async Task AddReactionTriggerSubmitted(string escapedReaction, ChatTriggerReactionAddModal modal)
    {
        var reaction = Uri.UnescapeDataString(escapedReaction);
        if (string.IsNullOrWhiteSpace(modal.Message) || string.IsNullOrWhiteSpace(reaction))
        {
            await RespondAsync(Strings.TriggerAddInvalid(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var cr = await Service.AddReactionTriggerAsync(ctx.Guild?.Id, reaction, modal.Message).ConfigureAwait(false);

        await RespondAsync(embed: Service.GetEmbed(cr, ctx.Guild?.Id, Strings.NewReactionTrigger(ctx.Guild.Id)).Build())
            .ConfigureAwait(false);
        await FollowupWithTriggerStatus().ConfigureAwait(false);
    }

    /// <summary>
    ///     Commands that limit when and how often a chat trigger fires.
    /// </summary>
    [Group("limits", "Limit when and how often a trigger fires")]
    public class Limits : MewdekoSlashSubmodule<ChatTriggersService>
    {
        /// <summary>
        ///     Sets how long a chat trigger stays active before it stops firing.
        /// </summary>
        /// <param name="id">The ID of the chat trigger.</param>
        /// <param name="duration">How long the trigger stays active, or omit it to remove the expiry.</param>
        [SlashCommand("expiry", "Sets how long a trigger stays active before it stops firing")]
        [SlashUserPerm(GuildPermission.Administrator)]
        [CheckPermissions]
        public async Task CtExpiry(
            [Summary("id", "The chat trigger's id")] [Autocomplete(typeof(ChatTriggerAutocompleter))]
            int id,
            [Summary("duration", "How long the trigger stays active, omit to clear")]
            TimeSpan? duration = null)
        {
            var expiry = duration is null ? (DateTime?)null : DateTime.UtcNow + duration.Value;
            var res = await Service.ModifyAsync(ctx.Guild?.Id, id, ct => ct.ExpiresAt = expiry).ConfigureAwait(false);

            if (res is null)
            {
                await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (expiry is null)
            {
                await ReplyConfirmAsync(Strings.CtExpiryCleared(ctx.Guild.Id, id)).ConfigureAwait(false);
                return;
            }

            await RespondAsync(
                    embed: Service.GetEmbed(res, ctx.Guild?.Id, Strings.EditedChatTrig(ctx.Guild.Id)).Build())
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets how many times a chat trigger may fire before it stops.
        /// </summary>
        /// <param name="id">The ID of the chat trigger.</param>
        /// <param name="uses">The maximum number of uses, or 0 to remove the limit.</param>
        [SlashCommand("max-uses", "Sets how many times a trigger may fire before it stops")]
        [SlashUserPerm(GuildPermission.Administrator)]
        [CheckPermissions]
        public async Task CtMaxUses(
            [Summary("id", "The chat trigger's id")] [Autocomplete(typeof(ChatTriggerAutocompleter))]
            int id,
            [Summary("uses", "Maximum number of uses, 0 to remove the limit")]
            int uses)
        {
            if (uses < 0)
            {
                await ReplyErrorAsync(Strings.CtNegativeAmount(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var res = await Service.ModifyAsync(ctx.Guild?.Id, id, ct => ct.MaxUses = uses == 0 ? null : uses)
                .ConfigureAwait(false);

            if (res is null)
            {
                await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (uses == 0)
            {
                await ReplyConfirmAsync(Strings.CtMaxUsesCleared(ctx.Guild.Id, id)).ConfigureAwait(false);
                return;
            }

            await RespondAsync(
                    embed: Service.GetEmbed(res, ctx.Guild?.Id, Strings.EditedChatTrig(ctx.Guild.Id)).Build())
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets how old an account must be before a chat trigger will fire for it.
        /// </summary>
        /// <param name="id">The ID of the chat trigger.</param>
        /// <param name="age">The minimum account age, or omit it to remove the requirement.</param>
        [SlashCommand("min-age", "Sets how old an account must be before a trigger fires for it")]
        [SlashUserPerm(GuildPermission.Administrator)]
        [CheckPermissions]
        public Task CtMinAge(
            [Summary("id", "The chat trigger's id")] [Autocomplete(typeof(ChatTriggerAutocompleter))]
            int id,
            [Summary("age", "Minimum account age, omit to clear")]
            TimeSpan? age = null)
        {
            return ApplyEconomyEdit(id, (long)(age?.TotalMinutes ?? 0),
                (ct, value) => ct.MinAccountAgeMinutes = (int)value);
        }

        /// <summary>
        ///     Sets how long a user must have been in the server before a chat trigger will fire for them.
        /// </summary>
        /// <param name="id">The ID of the chat trigger.</param>
        /// <param name="membership">The minimum membership duration, or omit it to remove the requirement.</param>
        [SlashCommand("min-member", "Sets how long a user must be in the server before a trigger fires")]
        [SlashUserPerm(GuildPermission.Administrator)]
        [CheckPermissions]
        public Task CtMinMember(
            [Summary("id", "The chat trigger's id")] [Autocomplete(typeof(ChatTriggerAutocompleter))]
            int id,
            [Summary("membership", "Minimum membership duration, omit to clear")]
            TimeSpan? membership = null)
        {
            return ApplyEconomyEdit(id, (long)(membership?.TotalMinutes ?? 0),
                (ct, value) => ct.MinServerMembershipMinutes = (int)value);
        }

        /// <summary>
        ///     Sets a chat trigger's own cooldown, separate from the server-wide command cooldown.
        /// </summary>
        /// <param name="id">The ID of the chat trigger.</param>
        /// <param name="seconds">The cooldown in seconds, or 0 to remove it.</param>
        /// <param name="scope">Who the cooldown applies to.</param>
        [SlashCommand("cooldown", "Sets a trigger's own cooldown")]
        [SlashUserPerm(GuildPermission.Administrator)]
        [CheckPermissions]
        public Task CtCooldown(
            [Summary("id", "The chat trigger's id")] [Autocomplete(typeof(ChatTriggerAutocompleter))]
            int id,
            [Summary("seconds", "Cooldown in seconds, 0 to remove")]
            int seconds,
            [Summary("scope", "Who the cooldown applies to")]
            CtCooldownScope scope = CtCooldownScope.User)
        {
            return ApplyEconomyEdit(id, seconds, (ct, value) =>
            {
                ct.CooldownSeconds = (int)value;
                ct.CooldownScope = (int)scope;
            });
        }

        /// <summary>
        ///     Requires a counter to be within a range before a chat trigger will fire.
        /// </summary>
        /// <param name="id">The ID of the chat trigger.</param>
        /// <param name="name">The counter's name, or "clear" to remove the requirement.</param>
        /// <param name="min">The lowest value that allows the trigger to fire, or omit it for no lower bound.</param>
        /// <param name="max">The highest value that allows the trigger to fire, or omit it for no upper bound.</param>
        [SlashCommand("require-counter", "Requires a counter to be within a range before a trigger fires")]
        [SlashUserPerm(GuildPermission.Administrator)]
        [CheckPermissions]
        public async Task CtRequireCounter(
            [Summary("id", "The chat trigger's id")] [Autocomplete(typeof(ChatTriggerAutocompleter))]
            int id,
            [Summary("name", "The counter's name, or clear to remove the requirement")]
            string name,
            [Summary("min", "Lowest value that allows the trigger to fire")]
            long? min = null,
            [Summary("max", "Highest value that allows the trigger to fire")]
            long? max = null)
        {
            var clearing = string.Equals(name, "clear", StringComparison.OrdinalIgnoreCase);

            var res = await Service.ModifyAsync(ctx.Guild?.Id, id, ct =>
            {
                ct.CounterName = clearing ? null : name.ToLowerInvariant();
                ct.CounterMin = clearing ? null : min;
                ct.CounterMax = clearing ? null : max;
            }).ConfigureAwait(false);

            if (res is null)
            {
                await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await RespondAsync(
                    embed: Service.GetEmbed(res, ctx.Guild?.Id, Strings.EditedChatTrig(ctx.Guild.Id)).Build())
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Restricts a chat trigger to a window of the day, optionally on specific days of the week.
        /// </summary>
        /// <param name="id">The ID of the chat trigger.</param>
        /// <param name="start">The start of the window in 24-hour HH:mm format, or "clear" to remove the restriction.</param>
        /// <param name="end">The end of the window in 24-hour HH:mm format.</param>
        /// <param name="days">Space separated days of the week as names or numbers where 0 is Sunday.</param>
        [SlashCommand("time", "Restricts a trigger to a window of the day")]
        [SlashUserPerm(GuildPermission.Administrator)]
        [CheckPermissions]
        public async Task CtTime(
            [Summary("id", "The chat trigger's id")] [Autocomplete(typeof(ChatTriggerAutocompleter))]
            int id,
            [Summary("start", "Window start in HH:mm, or clear to remove")]
            string start,
            [Summary("end", "Window end in HH:mm")]
            string? end = null,
            [Summary("days", "Space separated day names or numbers, 0 is Sunday")]
            string? days = null)
        {
            if (string.Equals(start, "clear", StringComparison.OrdinalIgnoreCase))
            {
                var cleared = await Service.ModifyAsync(ctx.Guild?.Id, id, ct => ct.TimeConditions = null)
                    .ConfigureAwait(false);

                await (cleared is null
                        ? ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id))
                        : ReplyConfirmAsync(Strings.CtTimeCleared(ctx.Guild.Id, id)))
                    .ConfigureAwait(false);
                return;
            }

            if (end is null || !TimeSpan.TryParse(start, out _) || !TimeSpan.TryParse(end, out _))
            {
                await ReplyErrorAsync(Strings.CtTimeInvalid(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var parsedDays = new List<int>();
            var dayInputs = string.IsNullOrWhiteSpace(days)
                ? Array.Empty<string>()
                : days.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var day in dayInputs)
            {
                if (int.TryParse(day, out var dayNumber) && dayNumber is >= 0 and <= 6)
                    parsedDays.Add(dayNumber);
                else if (Enum.TryParse<DayOfWeek>(day, true, out var parsedDay))
                    parsedDays.Add((int)parsedDay);
            }

            var condition = new TimeCondition
            {
                StartTime = start, EndTime = end, DaysOfWeek = parsedDays.Count > 0 ? parsedDays.ToArray() : null
            };

            var json = JsonSerializer.Serialize(new[]
            {
                condition
            });

            var res = await Service.ModifyAsync(ctx.Guild?.Id, id, ct => ct.TimeConditions = json)
                .ConfigureAwait(false);

            await (res is null
                    ? ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id))
                    : ReplyConfirmAsync(Strings.CtTimeSet(ctx.Guild.Id, id, start, end)))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Enables or disables a chat trigger without deleting it.
        /// </summary>
        /// <param name="id">The ID of the chat trigger.</param>
        [SlashCommand("toggle", "Enables or disables a trigger without deleting it")]
        [SlashUserPerm(GuildPermission.Administrator)]
        [CheckPermissions]
        public async Task CtToggle(
            [Summary("id", "The chat trigger's id")] [Autocomplete(typeof(ChatTriggerAutocompleter))]
            int id)
        {
            var res = await Service.ModifyAsync(ctx.Guild?.Id, id, ct => ct.IsDisabled = !ct.IsDisabled)
                .ConfigureAwait(false);

            if (res is null)
            {
                await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await ReplyConfirmAsync(res.IsDisabled
                    ? Strings.CtDisabled(ctx.Guild.Id, id)
                    : Strings.CtEnabled(ctx.Guild.Id, id))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Applies an economy-related edit to a chat trigger, rejecting negative amounts.
        /// </summary>
        /// <param name="id">The ID of the chat trigger.</param>
        /// <param name="amount">The amount the caller supplied.</param>
        /// <param name="apply">The assignment to perform on the trigger.</param>
        private async Task ApplyEconomyEdit(int id, long amount, Action<ChatTrigger, long> apply)
        {
            if (amount < 0)
            {
                await ReplyErrorAsync(Strings.CtNegativeAmount(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var res = await Service.ModifyAsync(ctx.Guild?.Id, id, ct => apply(ct, amount)).ConfigureAwait(false);

            if (res is null)
            {
                await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await RespondAsync(
                    embed: Service.GetEmbed(res, ctx.Guild?.Id, Strings.EditedChatTrig(ctx.Guild.Id)).Build())
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Commands that control how a chat trigger responds.
    /// </summary>
    [Group("response", "Control how a trigger responds")]
    public class Response : MewdekoSlashSubmodule<ChatTriggersService>
    {
        /// <summary>
        ///     Adds an extra response to a chat trigger, for use with the trigger's response mode.
        /// </summary>
        /// <param name="id">The ID of the chat trigger.</param>
        [SlashCommand("add", "Adds an extra response to a trigger")]
        [SlashUserPerm(GuildPermission.Administrator)]
        [CheckPermissions]
        public Task CtAddResponse(
            [Summary("id", "The chat trigger's id")] [Autocomplete(typeof(ChatTriggerAutocompleter))]
            int id)
        {
            return RespondWithModalAsync<ChatTriggerResponseAddModal>($"ct_response_add:{id}");
        }

        /// <summary>
        ///     Handles the extra response modal submission.
        /// </summary>
        /// <param name="sId">The ID of the chat trigger.</param>
        /// <param name="modal">The modal containing the response.</param>
        [ModalInteraction("ct_response_add:*", true)]
        [SlashUserPerm(GuildPermission.Administrator)]
        [CheckPermissions]
        public async Task CtAddResponseSubmitted(string sId, ChatTriggerResponseAddModal modal)
        {
            var id = int.Parse(sId);
            var response = modal.Response;
            if (string.IsNullOrWhiteSpace(response))
            {
                await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var res = await Service.ModifyAsync(ctx.Guild?.Id, id, ct => ct.AdditionalResponses =
                    string.IsNullOrWhiteSpace(ct.AdditionalResponses)
                        ? response
                        : $"{ct.AdditionalResponses}@@@{response}")
                .ConfigureAwait(false);

            if (res is null)
            {
                await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await ReplyConfirmAsync(Strings.CtResponseAdded(ctx.Guild.Id, id, res.GetResponses().Count))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Removes every extra response from a chat trigger, leaving its primary response in place.
        /// </summary>
        /// <param name="id">The ID of the chat trigger.</param>
        [SlashCommand("clear", "Removes every extra response from a trigger")]
        [SlashUserPerm(GuildPermission.Administrator)]
        [CheckPermissions]
        public async Task CtClearResponses(
            [Summary("id", "The chat trigger's id")] [Autocomplete(typeof(ChatTriggerAutocompleter))]
            int id)
        {
            var res = await Service.ModifyAsync(ctx.Guild?.Id, id, ct => ct.AdditionalResponses = null)
                .ConfigureAwait(false);

            if (res is null)
            {
                await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await ReplyConfirmAsync(Strings.CtResponsesCleared(ctx.Guild.Id, id)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets how a chat trigger picks between its responses when it has more than one.
        /// </summary>
        /// <param name="id">The ID of the chat trigger.</param>
        /// <param name="mode">The response mode to use.</param>
        [SlashCommand("mode", "Sets how a trigger picks between its responses")]
        [SlashUserPerm(GuildPermission.Administrator)]
        [CheckPermissions]
        public async Task CtResponseMode(
            [Summary("id", "The chat trigger's id")] [Autocomplete(typeof(ChatTriggerAutocompleter))]
            int id,
            [Summary("mode", "The response mode to use")]
            CtResponseMode mode)
        {
            var res = await Service.ModifyAsync(ctx.Guild?.Id, id, ct => ct.ResponseMode = (int)mode)
                .ConfigureAwait(false);

            if (res is null)
            {
                await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await RespondAsync(
                    embed: Service.GetEmbed(res, ctx.Guild?.Id, Strings.EditedChatTrig(ctx.Guild.Id)).Build())
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets how long a chat trigger's own response stays before it is deleted.
        /// </summary>
        /// <param name="id">The ID of the chat trigger.</param>
        /// <param name="seconds">How many seconds to wait, or 0 to keep the response.</param>
        [SlashCommand("delete-after", "Sets how long a trigger's response stays before it is deleted")]
        [SlashUserPerm(GuildPermission.Administrator)]
        [CheckPermissions]
        public async Task CtDeleteAfter(
            [Summary("id", "The chat trigger's id")] [Autocomplete(typeof(ChatTriggerAutocompleter))]
            int id,
            [Summary("seconds", "Seconds to wait, 0 to keep the response")]
            int seconds)
        {
            if (seconds < 0)
            {
                await ReplyErrorAsync(Strings.CtNegativeAmount(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var res = await Service.ModifyAsync(ctx.Guild?.Id, id, ct => ct.DeleteResponseAfter = seconds)
                .ConfigureAwait(false);

            if (res is null)
            {
                await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await RespondAsync(
                    embed: Service.GetEmbed(res, ctx.Guild?.Id, Strings.EditedChatTrig(ctx.Guild.Id)).Build())
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Reports whether a chat trigger would fire for a sample message, and what is blocking it if not.
        ///     The sample is collected through a modal. Nothing is charged, sent or recorded.
        /// </summary>
        /// <param name="id">The ID of the chat trigger.</param>
        [SlashCommand("test", "Reports whether a trigger would fire for a sample message")]
        [SlashUserPerm(GuildPermission.Administrator)]
        [CheckPermissions]
        [RequireContext(ContextType.Guild)]
        public async Task CtTest(
            [Summary("id", "The chat trigger's id")] [Autocomplete(typeof(ChatTriggerAutocompleter))]
            int id)
        {
            var ct = await Service.GetGuildOrGlobalTriggers(ctx.Guild.Id, id).ConfigureAwait(false);

            if (ct is null)
            {
                await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await RespondWithModalAsync<ChatTriggerTestModal>($"ct_test:{id}").ConfigureAwait(false);
        }

        /// <summary>
        ///     Handles the test modal submission and runs the trigger test.
        /// </summary>
        /// <param name="sId">The ID of the chat trigger.</param>
        /// <param name="modal">The modal containing the sample message.</param>
        [ModalInteraction("ct_test:*", true)]
        [SlashUserPerm(GuildPermission.Administrator)]
        [CheckPermissions]
        [RequireContext(ContextType.Guild)]
        public async Task CtTestSubmitted(string sId, ChatTriggerTestModal modal)
        {
            var id = int.Parse(sId);
            var ct = await Service.GetGuildOrGlobalTriggers(ctx.Guild.Id, id).ConfigureAwait(false);

            if (ct is null)
            {
                await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (string.IsNullOrWhiteSpace(modal.Sample))
            {
                await ReplyErrorAsync(Strings.CtTestNoSample(ctx.Guild.Id, id)).ConfigureAwait(false);
                return;
            }

            await DeferAsync().ConfigureAwait(false);

            var (matched, blocker) = await Service
                .TestTriggerAsync(ct, (SocketGuild)ctx.Guild, (IGuildUser)ctx.User, ctx.Channel, modal.Sample)
                .ConfigureAwait(false);

            var eb = new EmbedBuilder()
                .WithTitle(Strings.CtTestTitle(ctx.Guild.Id, id))
                .AddField(Strings.Trigger(ctx.Guild.Id), ct.Trigger?.TrimTo(1024) ?? "-")
                .AddField(Strings.CtTestMatched(ctx.Guild.Id),
                    matched ? Strings.CtTestMatched(ctx.Guild.Id) : Strings.CtTestNotMatched(ctx.Guild.Id));

            if (blocker is null)
            {
                eb.WithOkColor().WithDescription(Strings.CtTestWouldFire(ctx.Guild.Id));

                var preview = ct.GetResponses().FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(preview))
                    eb.AddField(Strings.CtTestResponse(ctx.Guild.Id), preview.TrimTo(1024));
            }
            else
            {
                eb.WithErrorColor().AddField(Strings.CtTestBlockedBy(ctx.Guild.Id), blocker);
            }

            await ctx.Interaction.FollowupAsync(embed: eb.Build()).ConfigureAwait(false);
        }

        /// <summary>
        ///     Shows how often a chat trigger has fired, and who fired it most recently.
        /// </summary>
        /// <param name="id">The ID of the chat trigger.</param>
        [SlashCommand("stats", "Shows how often a trigger has fired and who fired it recently")]
        [SlashUserPerm(GuildPermission.Administrator)]
        [CheckPermissions]
        [RequireContext(ContextType.Guild)]
        public async Task CtStats(
            [Summary("id", "The chat trigger's id")] [Autocomplete(typeof(ChatTriggerAutocompleter))]
            int id)
        {
            var ct = await Service.GetGuildOrGlobalTriggers(ctx.Guild.Id, id).ConfigureAwait(false);

            if (ct is null)
            {
                await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var (total, recent) = await Service.GetTriggerHistoryAsync(ctx.Guild.Id, id).ConfigureAwait(false);

            if (total == 0)
            {
                await ReplyConfirmAsync(Strings.CtStatsNone(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle(Strings.CtStatsTitle(ctx.Guild.Id, id))
                .AddField(Strings.CtStatsTotal(ctx.Guild.Id), total.ToString("N0"));

            var lines = recent.Select(x =>
            {
                var when = x.DateAdded.HasValue
                    ? TimestampTag.FromDateTime(x.DateAdded.Value, TimestampTagStyles.Relative).ToString()
                    : "-";
                return $"<@{x.UserId}> in <#{x.ChannelId}> {when}";
            }).ToList();

            if (lines.Count > 0)
                eb.AddField(Strings.CtStatsRecent(ctx.Guild.Id), string.Join("\n", lines).TrimTo(1024));

            await RespondAsync(embed: eb.Build()).ConfigureAwait(false);
        }

        /// <summary>
        ///     Chains a chat trigger to another, so firing the first also runs the second.
        /// </summary>
        /// <param name="id">The ID of the chat trigger.</param>
        /// <param name="nextId">The ID of the trigger to chain to, or 0 to remove the chain.</param>
        [SlashCommand("chain", "Chains a trigger to another so firing the first also runs the second")]
        [SlashUserPerm(GuildPermission.Administrator)]
        [CheckPermissions]
        public async Task CtChain(
            [Summary("id", "The chat trigger's id")] [Autocomplete(typeof(ChatTriggerAutocompleter))]
            int id,
            [Summary("next-id", "The trigger to chain to, 0 to remove the chain")]
            int nextId)
        {
            if (id == nextId)
            {
                await ReplyErrorAsync(Strings.CtChainSelf(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (nextId != 0 &&
                await Service.GetGuildOrGlobalTriggers(ctx.Guild.Id, nextId).ConfigureAwait(false) is null)
            {
                await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var res = await Service.ModifyAsync(ctx.Guild?.Id, id, ct => ct.NextTriggerId = nextId == 0 ? null : nextId)
                .ConfigureAwait(false);

            if (res is null)
            {
                await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (nextId == 0)
            {
                await ReplyConfirmAsync(Strings.CtChainCleared(ctx.Guild.Id, id)).ConfigureAwait(false);
                return;
            }

            await RespondAsync(
                    embed: Service.GetEmbed(res, ctx.Guild?.Id, Strings.EditedChatTrig(ctx.Guild.Id)).Build())
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Toggles whether a chat trigger replies to the message that fired it.
        /// </summary>
        /// <param name="id">The ID of the chat trigger.</param>
        [SlashCommand("reply", "Toggles whether a trigger replies to the message that fired it")]
        [SlashUserPerm(GuildPermission.Administrator)]
        [CheckPermissions]
        public async Task CtReply(
            [Summary("id", "The chat trigger's id")] [Autocomplete(typeof(ChatTriggerAutocompleter))]
            int id)
        {
            var res = await Service.ModifyAsync(ctx.Guild?.Id, id, ct => ct.ReplyToTrigger = !ct.ReplyToTrigger)
                .ConfigureAwait(false);

            if (res is null)
            {
                await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await RespondAsync(
                    embed: Service.GetEmbed(res, ctx.Guild?.Id, Strings.EditedChatTrig(ctx.Guild.Id)).Build())
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Toggles whether a chat trigger responds to messages from other bots and webhooks.
        ///     A trigger set this way responds only to bot messages, never to human ones.
        /// </summary>
        /// <param name="id">The ID of the chat trigger.</param>
        [SlashCommand("allow-bots", "Toggles whether a trigger responds to messages from other bots")]
        [SlashUserPerm(GuildPermission.Administrator)]
        [CheckPermissions]
        public async Task CtAllowBots(
            [Summary("id", "The chat trigger's id")] [Autocomplete(typeof(ChatTriggerAutocompleter))]
            int id)
        {
            var res = await Service.ModifyAsync(ctx.Guild?.Id, id, ct => ct.AllowBots = !ct.AllowBots)
                .ConfigureAwait(false);

            if (res is null)
            {
                await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await RespondAsync(
                    embed: Service.GetEmbed(res, ctx.Guild?.Id, Strings.EditedChatTrig(ctx.Guild.Id)).Build())
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Commands that set the costs, rewards and requirements of a chat trigger.
    /// </summary>
    [Group("rewards", "Set trigger costs, rewards and requirements")]
    public class Rewards : MewdekoSlashSubmodule<ChatTriggersService>
    {
        /// <summary>
        ///     Sets how much currency a chat trigger costs the user that fires it.
        /// </summary>
        /// <param name="id">The ID of the chat trigger.</param>
        /// <param name="amount">The amount to charge, or 0 to make the trigger free.</param>
        [SlashCommand("cost", "Sets how much currency a trigger costs to fire")]
        [SlashUserPerm(GuildPermission.Administrator)]
        [CheckPermissions]
        public Task CtCost(
            [Summary("id", "The chat trigger's id")] [Autocomplete(typeof(ChatTriggerAutocompleter))]
            int id,
            [Summary("amount", "Amount to charge, 0 for free")]
            long amount)
        {
            return ApplyEconomyEdit(id, amount, (ct, value) => ct.CurrencyCost = value);
        }

        /// <summary>
        ///     Sets how much currency a chat trigger pays out to the user that fires it.
        /// </summary>
        /// <param name="id">The ID of the chat trigger.</param>
        /// <param name="amount">The amount to pay out, or 0 for none.</param>
        [SlashCommand("reward", "Sets how much currency a trigger pays out")]
        [SlashUserPerm(GuildPermission.Administrator)]
        [CheckPermissions]
        public Task CtReward(
            [Summary("id", "The chat trigger's id")] [Autocomplete(typeof(ChatTriggerAutocompleter))]
            int id,
            [Summary("amount", "Amount to pay out, 0 for none")]
            long amount)
        {
            return ApplyEconomyEdit(id, amount, (ct, value) => ct.CurrencyReward = value);
        }

        /// <summary>
        ///     Sets how much XP a chat trigger grants the user that fires it.
        /// </summary>
        /// <param name="id">The ID of the chat trigger.</param>
        /// <param name="amount">The amount of XP to grant, or 0 for none.</param>
        [SlashCommand("xp-reward", "Sets how much XP a trigger grants")]
        [SlashUserPerm(GuildPermission.Administrator)]
        [CheckPermissions]
        public Task CtXpReward(
            [Summary("id", "The chat trigger's id")] [Autocomplete(typeof(ChatTriggerAutocompleter))]
            int id,
            [Summary("amount", "XP to grant, 0 for none")]
            int amount)
        {
            return ApplyEconomyEdit(id, amount, (ct, value) => ct.XpReward = (int)value);
        }

        /// <summary>
        ///     Sets the XP level a user must have reached before a chat trigger will fire for them.
        /// </summary>
        /// <param name="id">The ID of the chat trigger.</param>
        /// <param name="level">The required level, or 0 for no requirement.</param>
        [SlashCommand("req-level", "Sets the XP level required to fire a trigger")]
        [SlashUserPerm(GuildPermission.Administrator)]
        [CheckPermissions]
        public Task CtReqLevel(
            [Summary("id", "The chat trigger's id")] [Autocomplete(typeof(ChatTriggerAutocompleter))]
            int id,
            [Summary("level", "Required level, 0 for none")]
            int level)
        {
            return ApplyEconomyEdit(id, level, (ct, value) => ct.RequiredXpLevel = (int)value);
        }

        /// <summary>
        ///     Sets the message shown when a user does not meet a chat trigger's requirements. The message is
        ///     collected through a modal, and leaving it empty makes the trigger fail silently.
        /// </summary>
        /// <param name="id">The ID of the chat trigger.</param>
        [SlashCommand("req-msg", "Sets the message shown when a user does not meet a trigger's requirements")]
        [SlashUserPerm(GuildPermission.Administrator)]
        [CheckPermissions]
        public Task CtReqMsg(
            [Summary("id", "The chat trigger's id")] [Autocomplete(typeof(ChatTriggerAutocompleter))]
            int id)
        {
            return RespondWithModalAsync<ChatTriggerReqMsgModal>($"ct_req_msg:{id}");
        }

        /// <summary>
        ///     Handles the requirement message modal submission.
        /// </summary>
        /// <param name="sId">The ID of the chat trigger.</param>
        /// <param name="modal">The modal containing the message.</param>
        [ModalInteraction("ct_req_msg:*", true)]
        [SlashUserPerm(GuildPermission.Administrator)]
        [CheckPermissions]
        public async Task CtReqMsgSubmitted(string sId, ChatTriggerReqMsgModal modal)
        {
            var id = int.Parse(sId);
            var message = string.IsNullOrWhiteSpace(modal.Message) ? null : modal.Message;

            var res = await Service.ModifyAsync(ctx.Guild?.Id, id, ct => ct.RequirementFailMessage = message)
                .ConfigureAwait(false);

            if (res is null)
            {
                await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                await ReplyConfirmAsync(Strings.CtRequirementFailCleared(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await RespondAsync(
                    embed: Service.GetEmbed(res, ctx.Guild?.Id, Strings.EditedChatTrig(ctx.Guild.Id)).Build())
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Applies an economy-related edit to a chat trigger, rejecting negative amounts.
        /// </summary>
        /// <param name="id">The ID of the chat trigger.</param>
        /// <param name="amount">The amount the caller supplied.</param>
        /// <param name="apply">The assignment to perform on the trigger.</param>
        private async Task ApplyEconomyEdit(int id, long amount, Action<ChatTrigger, long> apply)
        {
            if (amount < 0)
            {
                await ReplyErrorAsync(Strings.CtNegativeAmount(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var res = await Service.ModifyAsync(ctx.Guild?.Id, id, ct => apply(ct, amount)).ConfigureAwait(false);

            if (res is null)
            {
                await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await RespondAsync(
                    embed: Service.GetEmbed(res, ctx.Guild?.Id, Strings.EditedChatTrig(ctx.Guild.Id)).Build())
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Commands for the counters chat triggers read and update.
    /// </summary>
    [Group("counters", "Manage the counters chat triggers read and update")]
    public class Counters(TriggerCounterService counterService) : MewdekoSlashSubmodule<ChatTriggersService>
    {
        /// <summary>
        ///     Lists the counters chat triggers in this server read and update.
        /// </summary>
        [SlashCommand("list", "Lists the counters chat triggers in this server use")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task CtCounters()
        {
            var counters = await counterService.ListAsync(ctx.Guild.Id).ConfigureAwait(false);

            if (counters.Count == 0)
            {
                await ReplyConfirmAsync(Strings.CtCountersNone(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle(Strings.CtCountersTitle(ctx.Guild.Id))
                .WithDescription(string.Join("\n", counters.Select(x =>
                    Strings.CtCounterEntry(ctx.Guild.Id, x.Name, x.Value.ToString("N0")))));

            await RespondAsync(embed: eb.Build()).ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets a counter to an exact value.
        /// </summary>
        /// <param name="name">The counter's name.</param>
        /// <param name="value">The value to set it to.</param>
        [SlashCommand("set", "Sets a counter to an exact value")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.Administrator)]
        [CheckPermissions]
        public async Task CtCounterSet(
            [Summary("name", "The counter's name")]
            string name,
            [Summary("value", "The value to set")] long value)
        {
            name = name.ToLowerInvariant();
            await counterService.SetAsync(ctx.Guild.Id, name, value).ConfigureAwait(false);
            await ReplyConfirmAsync(Strings.CtCounterSet(ctx.Guild.Id, name, value.ToString("N0")))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Deletes a counter along with every per-user value stored under its name.
        /// </summary>
        /// <param name="name">The counter's name.</param>
        [SlashCommand("delete", "Deletes a counter and every per-user value under its name")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.Administrator)]
        [CheckPermissions]
        public async Task CtCounterDelete([Summary("name", "The counter's name")] string name)
        {
            name = name.ToLowerInvariant();
            var removed = await counterService.DeleteAsync(ctx.Guild.Id, name).ConfigureAwait(false);

            await (removed == 0
                    ? ReplyErrorAsync(Strings.CtCounterNotFound(ctx.Guild.Id, name))
                    : ReplyConfirmAsync(Strings.CtCounterDeleted(ctx.Guild.Id, name, removed)))
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Commands that make a chat trigger fire on bot events.
    /// </summary>
    [Group("events", "Make a trigger fire on bot events")]
    public class Events : MewdekoSlashSubmodule<ChatTriggersService>
    {
        /// <summary>
        ///     Makes a chat trigger fire on a bot event rather than on a message. Setting an event also enables the
        ///     Event trigger type, and setting it back to None disables that type again.
        /// </summary>
        /// <param name="id">The ID of the chat trigger.</param>
        /// <param name="eventType">The event to listen for, or None to stop listening.</param>
        [SlashCommand("set", "Makes a trigger fire on a bot event rather than on a message")]
        [SlashUserPerm(GuildPermission.Administrator)]
        [CheckPermissions]
        public async Task CtEvent(
            [Summary("id", "The chat trigger's id")] [Autocomplete(typeof(ChatTriggerAutocompleter))]
            int id,
            [Summary("event", "The event to listen for, None to stop")]
            CtEventType eventType)
        {
            var res = await Service.ModifyAsync(ctx.Guild?.Id, id, ct =>
            {
                ct.EventType = (int)eventType;
                ct.ValidTriggerTypes = eventType == CtEventType.None
                    ? ct.ValidTriggerTypes & ~(int)ChatTriggerType.Event
                    : ct.ValidTriggerTypes | (int)ChatTriggerType.Event;
            }).ConfigureAwait(false);

            if (res is null)
            {
                await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await RespondAsync(
                    embed: Service.GetEmbed(res, ctx.Guild?.Id, Strings.EditedChatTrig(ctx.Guild.Id)).Build())
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets the channel an event chat trigger responds in.
        /// </summary>
        /// <param name="id">The ID of the chat trigger.</param>
        /// <param name="channel">The channel to respond in, or omit it to respond where the event happened.</param>
        [SlashCommand("channel", "Sets the channel an event trigger responds in")]
        [SlashUserPerm(GuildPermission.Administrator)]
        [CheckPermissions]
        public async Task CtEventChannel(
            [Summary("id", "The chat trigger's id")] [Autocomplete(typeof(ChatTriggerAutocompleter))]
            int id,
            [Summary("channel", "Channel to respond in, omit to use where the event happened")]
            ITextChannel? channel = null)
        {
            var res = await Service.ModifyAsync(ctx.Guild?.Id, id, ct => ct.EventChannelId = channel?.Id ?? 0)
                .ConfigureAwait(false);

            if (res is null)
            {
                await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await RespondAsync(
                    embed: Service.GetEmbed(res, ctx.Guild?.Id, Strings.EditedChatTrig(ctx.Guild.Id)).Build())
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Commands for grouping chat triggers into categories.
    /// </summary>
    [Group("categories", "Group triggers into categories")]
    public class Categories : MewdekoSlashSubmodule<ChatTriggersService>
    {
        /// <summary>
        ///     Puts a chat trigger into a category so it can be managed alongside related triggers.
        /// </summary>
        /// <param name="id">The ID of the chat trigger.</param>
        /// <param name="category">The category name, or omit it to remove the trigger from its category.</param>
        [SlashCommand("set", "Puts a trigger into a category")]
        [SlashUserPerm(GuildPermission.Administrator)]
        [CheckPermissions]
        public async Task CtCategory(
            [Summary("id", "The chat trigger's id")] [Autocomplete(typeof(ChatTriggerAutocompleter))]
            int id,
            [Summary("category", "Category name, omit to remove from its category")]
            string? category = null)
        {
            var res = await Service.ModifyAsync(ctx.Guild?.Id, id,
                ct => ct.Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim()).ConfigureAwait(false);

            if (res is null)
            {
                await ReplyErrorAsync(Strings.NoFoundId(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (string.IsNullOrWhiteSpace(category))
            {
                await ReplyConfirmAsync(Strings.CtCategoryCleared(ctx.Guild.Id, id)).ConfigureAwait(false);
                return;
            }

            await RespondAsync(
                    embed: Service.GetEmbed(res, ctx.Guild?.Id, Strings.EditedChatTrig(ctx.Guild.Id)).Build())
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Enables or disables every chat trigger in a category at once.
        /// </summary>
        /// <param name="category">The category to act on.</param>
        /// <param name="enabled">Whether the triggers should be enabled.</param>
        [SlashCommand("toggle", "Enables or disables every trigger in a category at once")]
        [SlashUserPerm(GuildPermission.Administrator)]
        [CheckPermissions]
        [RequireContext(ContextType.Guild)]
        public async Task CtCategoryToggle(
            [Summary("category", "The category to act on")]
            string category,
            [Summary("enabled", "Whether the triggers should be enabled")]
            bool enabled)
        {
            var changed = await Service.SetCategoryDisabledAsync(ctx.Guild.Id, category, !enabled)
                .ConfigureAwait(false);

            if (changed == 0)
            {
                await ReplyErrorAsync(Strings.CtCategoryNone(ctx.Guild.Id, category)).ConfigureAwait(false);
                return;
            }

            var state = enabled ? Strings.CtEnabledWord(ctx.Guild.Id) : Strings.CtDisabledWord(ctx.Guild.Id);
            await ReplyConfirmAsync(Strings.CtCategoryToggled(ctx.Guild.Id, changed, category, state))
                .ConfigureAwait(false);
        }
    }
}