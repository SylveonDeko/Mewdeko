using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.WordOfTheDay.Common;
using Mewdeko.Modules.WordOfTheDay.Services;

namespace Mewdeko.Modules.WordOfTheDay;

/// <summary>
///     Slash commands for viewing and configuring the daily word.
/// </summary>
[Group("wotd", "Word of the Day settings and lookups")]
public class SlashWordOfTheDay : MewdekoSlashModuleBase<WordOfTheDayService>
{
    private const int WordsPerPage = 15;
    private readonly InteractiveService interactive;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SlashWordOfTheDay" /> class.
    /// </summary>
    /// <param name="interactive">Interactive service for paginated output.</param>
    public SlashWordOfTheDay(InteractiveService interactive)
    {
        this.interactive = interactive;
    }

    /// <summary>
    ///     Shows today's word, or a preview if nothing has been posted yet today.
    /// </summary>
    [SlashCommand("show", "Shows today's word of the day")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Show()
    {
        await DeferAsync();
        var config = await Service.GetConfigAsync(ctx.Guild.Id);
        var localNow = WordOfTheDayService.GetLocalNow(config);
        var posted = await Service.GetPostedForDateAsync(ctx.Guild.Id, localNow.Date);

        WordEntry entry;
        string? note = null;
        if (posted is not null)
        {
            entry = new WordEntry
            {
                Word = posted.Word,
                Definition = posted.Definition,
                PartOfSpeech = posted.PartOfSpeech,
                Example = posted.Example,
                Phonetic = posted.Phonetic
            };
        }
        else
        {
            var preview = await Service.SelectWordAsync(config);
            if (preview is null)
            {
                await ErrorAsync(Strings.WotdNoWordFound(ctx.Guild.Id));
                return;
            }

            entry = preview;
            note = Strings.WotdPreviewNote(ctx.Guild.Id);
        }

        var (_, embeds, components) = Service.BuildMessage(ctx.Guild, ctx.Channel, config, entry, localNow);
        await ctx.Interaction.FollowupAsync(note, embeds: embeds, components: components);
    }

    /// <summary>
    ///     Enables or disables daily word posting.
    /// </summary>
    [SlashCommand("toggle", "Turns daily word posting on or off")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    [CheckPermissions]
    public async Task Toggle()
    {
        var config = await Service.GetConfigAsync(ctx.Guild.Id);

        if (!config.Enabled && !config.ChannelId.HasValue)
        {
            await ErrorAsync(Strings.WotdEnableNeedsChannel(ctx.Guild.Id));
            return;
        }

        var updated = await Service.UpdateConfigAsync(ctx.Guild.Id, c => c.Enabled = !c.Enabled);

        if (updated.Enabled)
        {
            var channel = await ctx.Guild.GetTextChannelAsync(updated.ChannelId!.Value);
            await ConfirmAsync(Strings.WotdEnabled(ctx.Guild.Id, channel?.Mention ?? updated.ChannelId.ToString(),
                updated.PostHour.ToString("00"), updated.Timezone));
        }
        else
        {
            await ConfirmAsync(Strings.WotdDisabled(ctx.Guild.Id));
        }
    }

    /// <summary>
    ///     Sets the channel daily words are posted to, or clears it when no channel is given.
    /// </summary>
    /// <param name="channel">The channel to post in.</param>
    [SlashCommand("channel", "Sets the channel daily words are posted in")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    [CheckPermissions]
    public async Task Channel(ITextChannel? channel = null)
    {
        if (channel is null)
        {
            await Service.UpdateConfigAsync(ctx.Guild.Id, c =>
            {
                c.ChannelId = null;
                c.Enabled = false;
            });
            await ConfirmAsync(Strings.WotdChannelCleared(ctx.Guild.Id));
            return;
        }

        await Service.UpdateConfigAsync(ctx.Guild.Id, c =>
        {
            c.ChannelId = channel.Id;
            c.Enabled = true;
        });
        await ConfirmAsync(Strings.WotdChannelSet(ctx.Guild.Id, channel.Mention));
    }

    /// <summary>
    ///     Sets the local hour of the day the word is posted at.
    /// </summary>
    /// <param name="hour">Hour in 24 hour format, 0 to 23.</param>
    [SlashCommand("time", "Sets the hour of the day the word is posted at")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    [CheckPermissions]
    public async Task Time([MinValue(0)] [MaxValue(23)] int hour)
    {
        if (hour is < 0 or > 23)
        {
            await ErrorAsync(Strings.WotdHourRangeError(ctx.Guild.Id));
            return;
        }

        var updated = await Service.UpdateConfigAsync(ctx.Guild.Id, c => c.PostHour = hour);
        await ConfirmAsync(Strings.WotdHourSet(ctx.Guild.Id, hour.ToString("00"), updated.Timezone));
    }

    /// <summary>
    ///     Sets the timezone used to decide when a new day starts.
    /// </summary>
    /// <param name="timezone">An IANA timezone name such as Europe/London.</param>
    [SlashCommand("timezone", "Sets the timezone for the daily post")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    [CheckPermissions]
    public async Task Timezone(string timezone)
    {
        if (!WordOfTheDayService.TryGetTimeZone(timezone, out var tz))
        {
            await ErrorAsync(Strings.WotdTimezoneInvalid(ctx.Guild.Id, timezone));
            return;
        }

        await Service.UpdateConfigAsync(ctx.Guild.Id, c => c.Timezone = tz.Id);
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        await ConfirmAsync(Strings.WotdTimezoneSet(ctx.Guild.Id, tz.Id, localNow.ToString("HH:mm")));
    }

    /// <summary>
    ///     Sets a role to ping with each daily word, or clears it when no role is given.
    /// </summary>
    /// <param name="role">The role to ping.</param>
    [SlashCommand("pingrole", "Sets a role to ping with each daily word")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    [CheckPermissions]
    public async Task PingRole(IRole? role = null)
    {
        await Service.UpdateConfigAsync(ctx.Guild.Id, c => c.PingRoleId = role?.Id);

        if (role is null)
            await ConfirmAsync(Strings.WotdPingroleCleared(ctx.Guild.Id));
        else
            await ConfirmAsync(Strings.WotdPingroleSet(ctx.Guild.Id, role.Mention));
    }

    /// <summary>
    ///     Sets a custom message template, shows the current one, or clears it with "clear".
    /// </summary>
    /// <param name="template">The template text, supporting embed JSON and placeholders.</param>
    [SlashCommand("message", "Sets or shows the custom daily word template")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    [CheckPermissions]
    public async Task Message(string? template = null)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            var config = await Service.GetConfigAsync(ctx.Guild.Id);
            var current = string.IsNullOrWhiteSpace(config.MessageTemplate)
                ? Strings.WotdConfigDefault(ctx.Guild.Id)
                : config.MessageTemplate;
            await ConfirmAsync(Strings.WotdMessageCurrent(ctx.Guild.Id, current) + "\n\n" +
                               Strings.WotdMessagePlaceholders(ctx.Guild.Id));
            return;
        }

        if (template.Equals("clear", StringComparison.OrdinalIgnoreCase) ||
            template.Equals("reset", StringComparison.OrdinalIgnoreCase))
        {
            await Service.UpdateConfigAsync(ctx.Guild.Id, c => c.MessageTemplate = null);
            await ConfirmAsync(Strings.WotdMessageCleared(ctx.Guild.Id));
            return;
        }

        await Service.UpdateConfigAsync(ctx.Guild.Id, c => c.MessageTemplate = template);
        await ConfirmAsync(Strings.WotdMessageSet(ctx.Guild.Id));
    }

    /// <summary>
    ///     Sets a topic that Datamuse words should lean toward, or clears it when empty.
    /// </summary>
    /// <param name="topic">One to five words describing the theme.</param>
    [SlashCommand("topic", "Sets a topic that dictionary words lean toward")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    [CheckPermissions]
    public async Task Topic(string? topic = null)
    {
        if (string.IsNullOrWhiteSpace(topic) || topic.Equals("clear", StringComparison.OrdinalIgnoreCase))
        {
            await Service.UpdateConfigAsync(ctx.Guild.Id, c => c.Topic = null);
            await ConfirmAsync(Strings.WotdTopicCleared(ctx.Guild.Id));
            return;
        }

        var cleaned = string.Join(' ', topic.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(5));
        await Service.UpdateConfigAsync(ctx.Guild.Id, c => c.Topic = cleaned);
        await ConfirmAsync(Strings.WotdTopicSet(ctx.Guild.Id, cleaned));
    }

    /// <summary>
    ///     Restricts Datamuse words to a part of speech.
    /// </summary>
    /// <param name="partOfSpeech">The part of speech to allow.</param>
    [SlashCommand("pos", "Restricts dictionary words to a part of speech")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    [CheckPermissions]
    public async Task Pos(WordPartOfSpeech partOfSpeech)
    {
        await Service.UpdateConfigAsync(ctx.Guild.Id, c => c.PartOfSpeech = (int)partOfSpeech);
        await ConfirmAsync(Strings.WotdPosSet(ctx.Guild.Id, partOfSpeech.ToString()));
    }

    /// <summary>
    ///     Sets how obscure Datamuse words should be.
    /// </summary>
    /// <param name="difficulty">The difficulty level.</param>
    [SlashCommand("difficulty", "Sets how obscure dictionary words should be")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    [CheckPermissions]
    public async Task Difficulty(WordDifficulty difficulty)
    {
        await Service.UpdateConfigAsync(ctx.Guild.Id, c => c.Difficulty = (int)difficulty);
        await ConfirmAsync(Strings.WotdDifficultySet(ctx.Guild.Id, difficulty.ToString()));
    }

    /// <summary>
    ///     Chooses where words come from.
    /// </summary>
    /// <param name="mode">The word source.</param>
    [SlashCommand("mode", "Chooses where daily words come from")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    [CheckPermissions]
    public async Task Mode(WordSourceMode mode)
    {
        await Service.UpdateConfigAsync(ctx.Guild.Id, c => c.SourceMode = (int)mode);
        await ConfirmAsync(Strings.WotdModeSet(ctx.Guild.Id, mode.ToString()));
    }

    /// <summary>
    ///     Adds a word to the server's custom pool, looking up a definition when none is given.
    /// </summary>
    /// <param name="word">The word to add.</param>
    /// <param name="definition">An optional definition.</param>
    [SlashCommand("add", "Adds a word to the custom word list")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    [CheckPermissions]
    public async Task Add(string word, string? definition = null)
    {
        await DeferAsync();
        var (entry, exists) = await Service.AddCustomWordAsync(ctx.Guild.Id, ctx.User.Id, word, definition);

        if (exists)
        {
            await ErrorAsync(Strings.WotdWordExists(ctx.Guild.Id, word));
            return;
        }

        if (entry is null)
        {
            await ErrorAsync(Strings.WotdWordNoDefinition(ctx.Guild.Id, word));
            return;
        }

        await ConfirmAsync(Strings.WotdWordAdded(ctx.Guild.Id, entry.Word));
    }

    /// <summary>
    ///     Removes a word from the server's custom pool.
    /// </summary>
    /// <param name="word">The word to remove.</param>
    [SlashCommand("remove", "Removes a word from the custom word list")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    [CheckPermissions]
    public async Task Remove(string word)
    {
        if (await Service.RemoveCustomWordAsync(ctx.Guild.Id, word))
            await ConfirmAsync(Strings.WotdWordRemoved(ctx.Guild.Id, word));
        else
            await ErrorAsync(Strings.WotdWordNotFound(ctx.Guild.Id, word));
    }

    /// <summary>
    ///     Lists the server's custom words.
    /// </summary>
    [SlashCommand("list", "Lists the custom word list")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task List()
    {
        var words = await Service.GetCustomWordsAsync(ctx.Guild.Id);
        if (words.Count == 0)
        {
            await ErrorAsync(Strings.WotdListEmpty(ctx.Guild.Id));
            return;
        }

        var pageCount = (words.Count - 1) / WordsPerPage;
        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(pageCount)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await interactive.SendPaginatorAsync(paginator, ctx.Interaction, TimeSpan.FromMinutes(10),
            InteractionResponseType.DeferredChannelMessageWithSource);

        Task<PageBuilder> PageFactory(int page)
        {
            var title = Strings.WotdListTitle(ctx.Guild.Id, words.Count);
            var body = WotdFormatter.FormatWordPage(words.Skip(page * WordsPerPage).Take(WordsPerPage));
            return Task.FromResult(new PageBuilder().WithOkColor().WithTitle(title).WithDescription(body));
        }
    }

    /// <summary>
    ///     Shows the most recently posted words.
    /// </summary>
    [SlashCommand("history", "Shows recently posted words")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task History()
    {
        var history = await Service.GetHistoryAsync(ctx.Guild.Id, 15);
        if (history.Count == 0)
        {
            await ErrorAsync(Strings.WotdHistoryEmpty(ctx.Guild.Id));
            return;
        }

        var title = Strings.WotdHistoryTitle(ctx.Guild.Id);
        var body = WotdFormatter.FormatHistory(history);
        var embed = new EmbedBuilder().WithOkColor().WithTitle(title).WithDescription(body);
        await ctx.Interaction.RespondAsync(embed: embed.Build());
    }

    /// <summary>
    ///     Shows the current configuration.
    /// </summary>
    [SlashCommand("config", "Shows the word of the day configuration")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Config()
    {
        var config = await Service.GetConfigAsync(ctx.Guild.Id);
        var customCount = (await Service.GetCustomWordsAsync(ctx.Guild.Id)).Count;
        var ruleCount = (await Service.GetScheduleRulesAsync(ctx.Guild.Id)).Count;
        var embed = WotdFormatter.BuildConfigEmbed(Strings, ctx.Guild.Id, config, customCount, ruleCount);
        await ctx.Interaction.RespondAsync(embed: embed.Build());
    }

    /// <summary>
    ///     Sets or removes a weekday rule.
    /// </summary>
    /// <param name="day">The day of the week.</param>
    /// <param name="topic">Topic words for that day. Omit with no filters to remove the rule.</param>
    /// <param name="partOfSpeech">Part of speech override.</param>
    /// <param name="difficulty">Difficulty override.</param>
    [SlashCommand("day", "Sets a topic or filters for a weekday")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    [CheckPermissions]
    public async Task Day(DayOfWeek day, string? topic = null, WordPartOfSpeech? partOfSpeech = null,
        WordDifficulty? difficulty = null)
    {
        await SetRuleAsync(ScheduleRuleType.DayOfWeek, (int)day, day.ToString(), topic, partOfSpeech, difficulty);
    }

    /// <summary>
    ///     Sets or removes a month rule.
    /// </summary>
    /// <param name="month">Month number or name.</param>
    /// <param name="topic">Topic words for that month. Omit with no filters to remove the rule.</param>
    /// <param name="partOfSpeech">Part of speech override.</param>
    /// <param name="difficulty">Difficulty override.</param>
    [SlashCommand("month", "Sets a topic or filters for a month")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    [CheckPermissions]
    public async Task Month(string month, string? topic = null, WordPartOfSpeech? partOfSpeech = null,
        WordDifficulty? difficulty = null)
    {
        if (!WordOfTheDayService.TryParseMonth(month, out var monthNumber))
        {
            await ErrorAsync(Strings.WotdMonthInvalid(ctx.Guild.Id, month));
            return;
        }

        await SetRuleAsync(ScheduleRuleType.Month, monthNumber, WordOfTheDayService.MonthName(monthNumber), topic,
            partOfSpeech, difficulty);
    }

    /// <summary>
    ///     Shows all weekday and month rules plus today's effective filters.
    /// </summary>
    [SlashCommand("schedule", "Shows weekday and month rules")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Schedule()
    {
        var config = await Service.GetConfigAsync(ctx.Guild.Id);
        var rules = await Service.GetScheduleRulesAsync(ctx.Guild.Id);
        var today = await Service.GetEffectiveFiltersAsync(config, WordOfTheDayService.GetLocalNow(config).Date);

        if (rules.Count == 0)
        {
            await ConfirmAsync(Strings.WotdScheduleEmpty(ctx.Guild.Id) + "\n" +
                               WotdFormatter.DescribeFilters(Strings, ctx.Guild.Id, today));
            return;
        }

        var embed = WotdFormatter.BuildScheduleEmbed(Strings, ctx.Guild.Id, rules, today);
        await ctx.Interaction.RespondAsync(embed: embed.Build());
    }

    private async Task SetRuleAsync(ScheduleRuleType type, int key, string name, string? topic,
        WordPartOfSpeech? partOfSpeech, WordDifficulty? difficulty)
    {
        var clearing = string.IsNullOrWhiteSpace(topic) || topic.Equals("clear", StringComparison.OrdinalIgnoreCase);
        if (clearing && partOfSpeech is null && difficulty is null)
        {
            if (await Service.RemoveScheduleRuleAsync(ctx.Guild.Id, type, key))
                await ConfirmAsync(Strings.WotdRuleRemoved(ctx.Guild.Id, name));
            else
                await ErrorAsync(Strings.WotdRuleNotFound(ctx.Guild.Id, name));
            return;
        }

        string? cleaned = null;
        if (!string.IsNullOrWhiteSpace(topic))
        {
            cleaned = topic.Equals("clear", StringComparison.OrdinalIgnoreCase)
                ? ""
                : string.Join(' ', topic.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(5));
        }

        var rule = await Service.UpsertScheduleRuleAsync(ctx.Guild.Id, type, key, cleaned, partOfSpeech, difficulty);
        await ConfirmAsync(Strings.WotdRuleSet(ctx.Guild.Id, name,
            WotdFormatter.DescribeRule(Strings, ctx.Guild.Id, rule)));
    }

    /// <summary>
    ///     Posts a new word immediately, regardless of schedule.
    /// </summary>
    [SlashCommand("post", "Posts a new word right now")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    [CheckPermissions]
    public async Task Post()
    {
        await DeferAsync();
        var (entry, failure) = await Service.PostNowAsync(ctx.Guild.Id, true);
        if (entry is not null)
        {
            await ConfirmAsync(Strings.WotdPosted(ctx.Guild.Id, entry.Word));
            return;
        }

        await ErrorAsync(WotdFormatter.FailureMessage(Strings, ctx.Guild.Id, failure));
    }

    /// <summary>
    ///     Resets all settings, custom words, and history for this server after confirmation.
    /// </summary>
    [SlashCommand("reset", "Resets all word of the day settings for this server")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    [CheckPermissions]
    public async Task Reset()
    {
        if (!await PromptUserConfirmAsync(Strings.WotdResetConfirm(ctx.Guild.Id), ctx.User.Id))
        {
            await ConfirmAsync(Strings.WotdResetCancelled(ctx.Guild.Id));
            return;
        }

        await Service.ResetAsync(ctx.Guild.Id);
        await ConfirmAsync(Strings.WotdResetDone(ctx.Guild.Id));
    }
}
