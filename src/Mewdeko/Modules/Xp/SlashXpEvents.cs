using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Humanizer;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.Modals;
using Mewdeko.Modules.Currency.Services;
using Mewdeko.Modules.Xp.Extensions;
using Mewdeko.Modules.Xp.Models;
using Mewdeko.Modules.Xp.Services;

namespace Mewdeko.Modules.Xp;

/// <summary>
///     Slash commands for XP boost events, competitions and level-up notifications.
/// </summary>
public partial class SlashXp
{
    /// <summary>
    ///     Temporary XP boost event commands.
    /// </summary>
    [Group("boost", "Manage temporary XP boost events")]
    public class XpBoostCommands : MewdekoSlashSubmodule<XpService>
    {
        /// <summary>
        ///     Creates a temporary XP boost event.
        /// </summary>
        /// <param name="time">How long the boost should last.</param>
        /// <param name="multiplier">The XP multiplier for the boost.</param>
        /// <param name="name">The name of the boost event.</param>
        [SlashCommand("create", "Creates a temporary XP boost event")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task XpBoost([Summary("time", "How long the boost lasts, for example 2h")] TimeSpan time,
            [Summary("multiplier", "The XP multiplier, must be above 1.0")]
            double multiplier,
            [Summary("name", "The name of the boost event")]
            string name)
        {
            if (time.TotalMinutes < 5)
            {
                await ReplyErrorAsync(Strings.XpBoostTooShort(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (multiplier <= 1.0)
            {
                await ReplyErrorAsync(Strings.XpBoostMultiplierTooLow(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                await ReplyErrorAsync(Strings.XpBoostNeedsName(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var startTime = DateTime.UtcNow;
            var endTime = startTime + time;

            var boostEvent = await Service.CreateXpBoostEventAsync(
                ctx.Guild.Id,
                name,
                multiplier,
                startTime,
                endTime
            );

            await ReplyConfirmAsync(Strings.XpBoostCreated(
                ctx.Guild.Id,
                boostEvent.Name,
                boostEvent.Multiplier,
                time.Humanize()
            )).ConfigureAwait(false);
        }

        /// <summary>
        ///     Lists active XP boost events.
        /// </summary>
        [SlashCommand("list", "Lists active XP boost events")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task XpBoosts()
        {
            var boosts = await Service.GetActiveBoostEventsAsync(ctx.Guild.Id);

            if (boosts.Count == 0)
            {
                await ReplyErrorAsync(Strings.XpNoActiveBoosts(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var embed = new EmbedBuilder()
                .WithOkColor()
                .WithTitle(Strings.XpActiveBoostsTitle(ctx.Guild.Id));

            foreach (var boost in boosts)
            {
                var timeLeft = boost.EndTime - DateTime.UtcNow;
                embed.AddField(
                    $"{boost.Name} ({boost.Multiplier}x)",
                    Strings.XpBoostTimeLeft(ctx.Guild.Id, timeLeft.Humanize())
                );
            }

            await ctx.Interaction.RespondAsync(embed: embed.Build());
        }

        /// <summary>
        ///     Cancels an XP boost event.
        /// </summary>
        /// <param name="eventId">ID of the event to cancel.</param>
        [SlashCommand("cancel", "Cancels an XP boost event")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task CancelBoost([Summary("event-id", "The ID of the boost event to cancel")] int eventId)
        {
            var success = await Service.CancelXpBoostEventAsync(eventId);

            if (success)
                await ReplyConfirmAsync(Strings.XpBoostCancelled(ctx.Guild.Id, eventId)).ConfigureAwait(false);
            else
                await ReplyErrorAsync(Strings.XpBoostNotFound(ctx.Guild.Id, eventId)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     XP competition commands.
    /// </summary>
    [Group("competition", "Manage XP competitions")]
    public class XpCompetitionCommands(ICurrencyService currencyService) : MewdekoSlashSubmodule<XpService>
    {
        /// <summary>
        ///     Creates a new XP competition.
        /// </summary>
        /// <param name="time">How long the competition should last.</param>
        /// <param name="type">The competition type: MostGained, ReachLevel, or HighestTotal.</param>
        /// <param name="name">The name of the competition.</param>
        /// <param name="targetLevel">For ReachLevel competitions, the target level.</param>
        [SlashCommand("create", "Creates a new XP competition")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task XpCompetition(
            [Summary("time", "How long the competition lasts, at least 1h")]
            TimeSpan time,
            [Summary("type", "The competition type")]
            XpCompetitionType type,
            [Summary("name", "The name of the competition")]
            string name,
            [Summary("target-level", "Target level for ReachLevel competitions")]
            int targetLevel = 0)
        {
            if (time.TotalMinutes < 60)
            {
                await ReplyErrorAsync(Strings.XpCompetitionTooShort(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                await ReplyErrorAsync(Strings.XpCompetitionNeedsName(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (type == XpCompetitionType.ReachLevel && targetLevel <= 0)
            {
                await ReplyErrorAsync(Strings.XpCompetitionNeedsLevel(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var startTime = DateTime.UtcNow;
            var endTime = startTime + time;

            var competition = await Service.CreateCompetitionAsync(
                ctx.Guild.Id,
                name,
                type,
                startTime,
                endTime,
                targetLevel,
                ctx.Channel.Id
            );

            await Service.StartCompetitionAsync(competition.Id);

            await ReplyConfirmAsync(Strings.XpCompetitionCreated(
                ctx.Guild.Id,
                competition.Name,
                competition.Type.ToString(),
                time.Humanize(),
                competition.Type == (int)XpCompetitionType.ReachLevel ? competition.TargetLevel.ToString() : ""
            )).ConfigureAwait(false);
        }

        /// <summary>
        ///     Adds a reward for a competition placement.
        /// </summary>
        /// <param name="competitionId">The ID of the competition.</param>
        /// <param name="position">The position to reward (1 for first place, etc.).</param>
        /// <param name="type">The type of reward: Role, XP, or Currency.</param>
        /// <param name="reward">For Role rewards, the role mention or id; for XP or Currency rewards, the amount.</param>
        [SlashCommand("add-reward", "Adds a reward for a competition placement")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task AddCompetitionReward(
            [Summary("competition-id", "The ID of the competition")]
            int competitionId,
            [Summary("place", "The placement to reward, 1 for first place")]
            int position,
            [Summary("reward-type", "Role, XP, or Currency")]
            XpCompetitionRewardType type,
            [Summary("reward", "Role mention or id, or the amount for XP and Currency")]
            string reward)
        {
            if (position <= 0)
            {
                await ReplyErrorAsync(Strings.XpCompetitionPositionInvalid(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            switch (type)
            {
                case XpCompetitionRewardType.Role:
                    IRole? roleMatch = null;
                    if (MentionUtils.TryParseRole(reward, out var roleId) || ulong.TryParse(reward, out roleId))
                        roleMatch = ctx.Guild.GetRole(roleId);

                    if (roleMatch == null)
                    {
                        await ReplyErrorAsync(Strings.XpCompetitionRoleNotFound(ctx.Guild.Id)).ConfigureAwait(false);
                        return;
                    }

                    await Service.AddCompetitionRewardAsync(competitionId, position, roleMatch.Id);
                    await ReplyConfirmAsync(Strings.XpCompetitionRoleRewardAdded(
                        ctx.Guild.Id,
                        position,
                        roleMatch.Mention,
                        competitionId
                    )).ConfigureAwait(false);
                    break;

                case XpCompetitionRewardType.Xp:
                    if (!int.TryParse(reward, out var xpAmount) || xpAmount <= 0)
                    {
                        await ReplyErrorAsync(Strings.XpCompetitionAmountInvalid(ctx.Guild.Id)).ConfigureAwait(false);
                        return;
                    }

                    await Service.AddCompetitionRewardAsync(competitionId, position, 0, xpAmount);
                    await ReplyConfirmAsync(Strings.XpCompetitionXpRewardAdded(
                        ctx.Guild.Id,
                        position,
                        xpAmount,
                        competitionId
                    )).ConfigureAwait(false);
                    break;

                case XpCompetitionRewardType.Currency:
                    if (!long.TryParse(reward, out var currencyAmount) || currencyAmount <= 0)
                    {
                        await ReplyErrorAsync(Strings.XpCompetitionAmountInvalid(ctx.Guild.Id)).ConfigureAwait(false);
                        return;
                    }

                    await Service.AddCompetitionRewardAsync(competitionId, position, 0, 0, currencyAmount);
                    await ReplyConfirmAsync(Strings.XpCompetitionCurrencyRewardAdded(
                        ctx.Guild.Id,
                        position,
                        currencyAmount,
                        await currencyService.GetCurrencyEmote(ctx.Guild.Id),
                        competitionId
                    )).ConfigureAwait(false);
                    break;

                default:
                    await ReplyErrorAsync(Strings.XpCompetitionRewardInvalid(ctx.Guild.Id)).ConfigureAwait(false);
                    break;
            }
        }

        /// <summary>
        ///     Lists active XP competitions.
        /// </summary>
        [SlashCommand("list", "Lists active XP competitions")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task XpCompetitions()
        {
            var competitions = await Service.GetActiveCompetitionsAsync(ctx.Guild.Id);

            if (competitions.Count == 0)
            {
                await ReplyErrorAsync(Strings.XpNoActiveCompetitions(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var embed = new EmbedBuilder()
                .WithOkColor()
                .WithTitle(Strings.XpActiveCompetitionsTitle(ctx.Guild.Id));

            foreach (var comp in competitions)
            {
                var timeLeft = comp.EndTime - DateTime.UtcNow;
                var description = Strings.XpCompetitionInfo(
                    ctx.Guild.Id,
                    comp.Type.ToString(),
                    timeLeft.Humanize(),
                    comp.Type == (int)XpCompetitionType.ReachLevel ? comp.TargetLevel.ToString() : ""
                );

                embed.AddField(comp.Name, description);
            }

            await ctx.Interaction.RespondAsync(embed: embed.Build());
        }

        /// <summary>
        ///     Shows the leaderboard for an active competition.
        /// </summary>
        /// <param name="competitionId">ID of the competition.</param>
        [SlashCommand("leaderboard", "Shows the leaderboard for a competition")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task CompetitionLeaderboard(
            [Summary("competition-id", "The ID of the competition")]
            int competitionId)
        {
            await DeferAsync();
            var competition = await Service.GetCompetitionAsync(competitionId);

            if (competition == null || competition.GuildId != ctx.Guild.Id)
            {
                await ReplyErrorAsync(Strings.XpCompetitionNotFound(ctx.Guild.Id, competitionId)).ConfigureAwait(false);
                return;
            }

            var entries = await Service.GetCompetitionEntriesAsync(competitionId);

            if (entries.Count == 0)
            {
                await ReplyErrorAsync(Strings.XpCompetitionNoEntries(ctx.Guild.Id, competition.Name))
                    .ConfigureAwait(false);
                return;
            }

            var embed = new EmbedBuilder()
                .WithOkColor()
                .WithTitle(Strings.XpCompetitionLeaderboardTitle(ctx.Guild.Id, competition.Name));

            List<(string Username, string Value, int Position)> leaderboard = new();

            switch ((XpCompetitionType)competition.Type)
            {
                case XpCompetitionType.MostGained:
                    var gainedSorted = entries.OrderByDescending(e => e.CurrentXp - e.StartingXp).ToList();
                    for (var i = 0; i < Math.Min(10, gainedSorted.Count); i++)
                    {
                        var entry = gainedSorted[i];
                        var user = await ctx.Guild.GetUserAsync(entry.UserId);
                        var username = user?.ToString() ?? entry.UserId.ToString();
                        var gained = entry.CurrentXp - entry.StartingXp;
                        leaderboard.Add((username, gained.ToString("N0"), i + 1));
                    }

                    break;

                case XpCompetitionType.ReachLevel:
                    var levelSorted = entries
                        .Where(e => e.AchievedTargetAt != null)
                        .OrderBy(e => e.AchievedTargetAt)
                        .ToList();

                    for (var i = 0; i < Math.Min(10, levelSorted.Count); i++)
                    {
                        var entry = levelSorted[i];
                        var user = await ctx.Guild.GetUserAsync(entry.UserId);
                        var username = user?.ToString() ?? entry.UserId.ToString();
                        var achievedAt = (entry.AchievedTargetAt ?? DateTime.UtcNow).ToString("yyyy-MM-dd HH:mm:ss");
                        leaderboard.Add((username, achievedAt, i + 1));
                    }

                    break;

                case XpCompetitionType.HighestTotal:
                    var totalSorted = entries.OrderByDescending(e => e.CurrentXp).ToList();
                    for (var i = 0; i < Math.Min(10, totalSorted.Count); i++)
                    {
                        var entry = totalSorted[i];
                        var user = await ctx.Guild.GetUserAsync(entry.UserId);
                        var username = user?.ToString() ?? entry.UserId.ToString();
                        leaderboard.Add((username, entry.CurrentXp.ToString("N0"), i + 1));
                    }

                    break;
            }

            var description = new List<string>();
            foreach (var entry in leaderboard)
            {
                description.Add(Strings.XpCompetitionLeaderboardLine(
                    ctx.Guild.Id,
                    entry.Position,
                    entry.Username,
                    entry.Value
                ));
            }

            if (description.Count == 0)
            {
                description.Add(Strings.XpCompetitionNoQualifiedEntries(ctx.Guild.Id));
            }

            embed.WithDescription(string.Join("\n", description));

            var timeLeft = competition.EndTime - DateTime.UtcNow;
            if (timeLeft.TotalSeconds > 0)
            {
                embed.WithFooter(Strings.XpCompetitionTimeLeft(ctx.Guild.Id, timeLeft.Humanize()));
            }
            else
            {
                embed.WithFooter(Strings.XpCompetitionEnded(ctx.Guild.Id));
            }

            await ctx.Interaction.FollowupAsync(embed: embed.Build());
        }

        /// <summary>
        ///     Ends a competition early and distributes rewards.
        /// </summary>
        /// <param name="competitionId">ID of the competition to end.</param>
        [SlashCommand("end", "Ends a competition early and distributes rewards")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task EndCompetition(
            [Summary("competition-id", "The ID of the competition to end")]
            int competitionId)
        {
            var competition = await Service.GetCompetitionAsync(competitionId);

            if (competition == null || competition.GuildId != ctx.Guild.Id)
            {
                await ReplyErrorAsync(Strings.XpCompetitionNotFound(ctx.Guild.Id, competitionId)).ConfigureAwait(false);
                return;
            }

            if (competition.EndTime < DateTime.UtcNow)
            {
                await ReplyErrorAsync(Strings.XpCompetitionAlreadyEnded(ctx.Guild.Id, competition.Name))
                    .ConfigureAwait(false);
                return;
            }

            await DeferAsync();
            await Service.FinalizeCompetitionAsync(competitionId);
            await ReplyConfirmAsync(Strings.XpCompetitionManuallyEnded(ctx.Guild.Id, competition.Name))
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Level-up notification commands.
    /// </summary>
    [Group("levelup", "Configure level-up notifications and messages")]
    public class XpLevelUp(InteractiveService interactivity, DiscordShardedClient client, ILogger<XpLevelUp> logger)
        : MewdekoSlashSubmodule<XpService>
    {
        /// <summary>
        ///     Sets the channel where level-up notifications will be sent.
        /// </summary>
        /// <param name="channel">The channel to send level-up notifications to, or empty to clear it.</param>
        [SlashCommand("channel", "Sets the channel for level-up notifications, leave empty to clear")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task LevelUpChannel(
            [Summary("channel", "The channel for level-up notifications")]
            ITextChannel? channel = null)
        {
            await Service.SetLevelUpChannelAsync(ctx.Guild.Id, channel?.Id ?? 0);

            if (channel != null)
            {
                await ReplyConfirmAsync(Strings.LevelUpChannelSet(ctx.Guild.Id, channel.Mention)).ConfigureAwait(false);
            }
            else
            {
                await ReplyConfirmAsync(Strings.LevelUpChannelCleared(ctx.Guild.Id)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Lists all custom level-up messages for this server.
        /// </summary>
        [SlashCommand("messages", "Lists all custom level-up messages for this server")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task LevelUpMessages()
        {
            await DeferAsync();
            var messages = await Service.GetLevelUpMessagesAsync(ctx.Guild.Id);

            if (messages.Count == 0)
            {
                await ReplyErrorAsync(Strings.LevelUpNoCustomMessages(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var paginator = new LazyPaginatorBuilder()
                .AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(messages.Count / 5)
                .WithDefaultEmotes()
                .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                .Build();

            await interactivity.SendPaginatorAsync(paginator, (ctx.Interaction as SocketInteraction)!,
                TimeSpan.FromMinutes(60),
                InteractionResponseType.DeferredChannelMessageWithSource).ConfigureAwait(false);

            async Task<PageBuilder> PageFactory(int page)
            {
                await Task.CompletedTask;
                var embed = new PageBuilder()
                    .WithTitle(Strings.LevelUpMessagesTitle(ctx.Guild.Id))
                    .WithOkColor();

                var startIndex = page * 5;
                var messagesToShow = messages.Skip(startIndex).Take(5);

                foreach (var msg in messagesToShow)
                {
                    var truncated = msg.MessageContent.Length > 200
                        ? msg.MessageContent[..200] + "..."
                        : msg.MessageContent;

                    var status = msg.IsEnabled ? Strings.Enabled(ctx.Guild.Id) : Strings.Disabled(ctx.Guild.Id);
                    embed.AddField(
                        $"#{msg.Id} ({status})",
                        $"```{truncated}```");
                }

                return embed;
            }
        }

        /// <summary>
        ///     Opens a modal to add a custom level-up message template with placeholder support.
        /// </summary>
        [SlashCommand("add-message", "Adds a custom level-up message template with placeholder support")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task AddLevelUpMessage()
        {
            await RespondWithModalAsync<XpLevelUpMessageModal>("xp_levelup_add");
        }

        /// <summary>
        ///     Handles the submitted level-up message modal and stores the template.
        /// </summary>
        /// <param name="modal">The submitted modal containing the message template.</param>
        [ModalInteraction("xp_levelup_add", true)]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task AddLevelUpMessageSubmitted(XpLevelUpMessageModal modal)
        {
            var message = modal.Message;
            if (string.IsNullOrWhiteSpace(message))
            {
                await ReplyErrorAsync(Strings.LevelUpMessageEmpty(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await Service.AddLevelUpMessageAsync(ctx.Guild.Id, message);
            await ReplyConfirmAsync(Strings.LevelUpMessageAdded(ctx.Guild.Id)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Removes a custom level-up message by ID.
        /// </summary>
        /// <param name="messageId">The ID of the message to remove.</param>
        [SlashCommand("remove-message", "Removes a custom level-up message by ID")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task RemoveLevelUpMessage([Summary("message-id", "The ID of the message to remove")] int messageId)
        {
            var success = await Service.RemoveLevelUpMessageAsync(ctx.Guild.Id, messageId);

            if (success)
            {
                await ReplyConfirmAsync(Strings.LevelUpMessageRemoved(ctx.Guild.Id, messageId)).ConfigureAwait(false);
            }
            else
            {
                await ReplyErrorAsync(Strings.LevelUpMessageNotFound(ctx.Guild.Id, messageId)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Enables or disables a custom level-up message.
        /// </summary>
        /// <param name="messageId">The ID of the message to toggle.</param>
        /// <param name="enabled">Whether to enable (true) or disable (false) the message.</param>
        [SlashCommand("toggle-message", "Enables or disables a custom level-up message")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task ToggleLevelUpMessage([Summary("message-id", "The ID of the message to toggle")] int messageId,
            [Summary("enabled", "True to enable, false to disable")]
            bool enabled)
        {
            var success = await Service.ToggleLevelUpMessageAsync(ctx.Guild.Id, messageId, enabled);

            if (success)
            {
                var status = enabled ? Strings.Enabled(ctx.Guild.Id) : Strings.Disabled(ctx.Guild.Id);
                await ReplyConfirmAsync(Strings.LevelUpMessageToggled(ctx.Guild.Id, messageId, status))
                    .ConfigureAwait(false);
            }
            else
            {
                await ReplyErrorAsync(Strings.LevelUpMessageNotFound(ctx.Guild.Id, messageId)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Opens a modal to test a level-up message template. Leave the input empty to test a random custom
        ///     message or the default one.
        /// </summary>
        [SlashCommand("test-message", "Tests a level-up message template, leave the input empty for a stored one")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task TestLevelUpMessage()
        {
            await RespondWithModalAsync<XpLevelUpTestModal>("xp_levelup_test");
        }

        /// <summary>
        ///     Handles the submitted test modal and sends the processed level-up message to the current channel.
        /// </summary>
        /// <param name="modal">The submitted modal, whose message may be empty.</param>
        [ModalInteraction("xp_levelup_test", true)]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task TestLevelUpMessageSubmitted(XpLevelUpTestModal modal)
        {
            await DeferAsync();
            var testUser = (IGuildUser)ctx.User;
            var message = modal.Message;

            if (string.IsNullOrWhiteSpace(message))
            {
                var customMessages = await Service.GetLevelUpMessagesAsync(ctx.Guild.Id);
                if (customMessages.Count > 0)
                {
                    var random = new Random();
                    message = customMessages[random.Next(customMessages.Count)].MessageContent;
                }
                else
                {
                    var settings = await Service.GetGuildXpSettingsAsync(ctx.Guild.Id);
                    message = settings.LevelUpMessage;
                }
            }

            var userPrefs = await Service.GetUserPreferencesAsync(ctx.User.Id);
            var pingsDisabled = userPrefs?.LevelUpPingsDisabled ?? false;

            var replacer = new ReplacementBuilder()
                .WithDefault(testUser, ctx.Channel, ctx.Guild as SocketGuild, client)
                .WithXpPlaceholders(
                    testUser,
                    ctx.Guild,
                    ctx.Channel as ITextChannel,
                    9,
                    10,
                    5000,
                    200,
                    1000,
                    25,
                    5,
                    ctx.User,
                    pingsDisabled)
                .Build();

            var processedMessage = ReplacementBuilderExtensions.ProcessLevelUpMessage(
                message, replacer, testUser, pingsDisabled);

            try
            {
                if (SmartEmbed.TryParse(processedMessage, ctx.Guild.Id, out var embed, out var plainText,
                        out var components))
                {
                    await ctx.Interaction.FollowupAsync(plainText, embed, components: components?.Build());
                }
                else
                {
                    await ctx.Interaction.FollowupAsync(
                        embed: new EmbedBuilder()
                            .WithColor(Color.Green)
                            .WithDescription(processedMessage)
                            .WithTitle(Strings.LevelUpTestTitle(ctx.Guild.Id))
                            .Build()
                    );
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error testing level-up message in guild {GuildId}", ctx.Guild.Id);
                await ReplyErrorAsync(Strings.LevelUpTestError(ctx.Guild.Id)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Toggles level-up ping notifications for yourself.
        /// </summary>
        /// <param name="enabled">Whether to enable (true) or disable (false) level-up pings.</param>
        [SlashCommand("pings", "Toggles level-up ping notifications for yourself")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task LevelUpPings(
            [Summary("enabled", "True to receive pings, false to disable them")]
            bool enabled)
        {
            await Service.SetUserLevelUpPingsAsync(ctx.User.Id, !enabled);

            var status = enabled ? Strings.Enabled(ctx.Guild?.Id ?? 0) : Strings.Disabled(ctx.Guild?.Id ?? 0);
            await ReplyConfirmAsync(Strings.LevelUpPingsToggled(ctx.Guild?.Id ?? 0, status)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Shows available level-up placeholders that can be used in custom messages.
        /// </summary>
        [SlashCommand("placeholders", "Shows placeholders available for custom level-up messages")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageGuild)]
        public async Task LevelUpPlaceholders()
        {
            var embed = new EmbedBuilder()
                .WithTitle(Strings.LevelUpPlaceholdersTitle(ctx.Guild.Id))
                .WithDescription(Strings.LevelUpPlaceholdersDesc(ctx.Guild.Id))
                .WithOkColor()
                .AddField(Strings.LevelUpPlaceholdersUser(ctx.Guild.Id),
                    "`%xp.user%` - User (respects ping settings)\n" +
                    "`%xp.user.mention%` - User mention (respects ping settings)\n" +
                    "`%xp.user.name%` - Username\n" +
                    "`%xp.user.displayname%` - Display name\n" +
                    "`%xp.user.nickname%` - Nickname (or username)\n" +
                    "`%xp.user.avatar%` - Avatar URL\n" +
                    "`%xp.user.id%` - User ID")
                .AddField(Strings.LevelUpPlaceholdersLevel(ctx.Guild.Id),
                    "`%xp.level.old%` - Previous level\n" +
                    "`%xp.level.new%` - New level\n" +
                    "`%xp.level.current%` - Current level (same as new)\n" +
                    "`%xp.level.next%` - Next level\n" +
                    "`%xp.level.difference%` - Level increase")
                .AddField(Strings.LevelUpPlaceholdersXp(ctx.Guild.Id),
                    "`%xp.total%` - Total XP\n" +
                    "`%xp.current%` - XP in current level\n" +
                    "`%xp.needed%` - XP needed for next level\n" +
                    "`%xp.remaining%` - XP remaining to next level\n" +
                    "`%xp.gained%` - XP gained this session\n" +
                    "`%xp.progress%` - Progress percentage")
                .AddField(Strings.LevelUpPlaceholdersOther(ctx.Guild.Id),
                    "`%xp.rank%` - Current rank\n" +
                    "`%xp.rank.ordinal%` - Rank in words (first, second)\n" +
                    "`%xp.rank.suffix%` - Rank suffix (st, nd, rd, th)\n" +
                    "`%xp.guild.name%` - Server name\n" +
                    "`%xp.time%` - Current time\n" +
                    "`%xp.timestamp%` - Discord timestamp")
                .AddField(Strings.LevelUpPlaceholdersLegacy(ctx.Guild.Id),
                    "`%user%`, `%user.mention%`, `%level%`, `%xp%`, `%rank%`, `%server%`, `%guild%`");

            await ctx.Interaction.RespondAsync(embed: embed.Build());
        }
    }
}