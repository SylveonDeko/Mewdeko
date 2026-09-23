using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.WordOfTheDay.Common;
using Mewdeko.Modules.WordOfTheDay.Services;

namespace Mewdeko.Modules.WordOfTheDay;

/// <summary>
///     Text commands for viewing and configuring the daily word.
/// </summary>
public class WordOfTheDay : MewdekoModuleBase<WordOfTheDayService>
{
    private const int WordsPerPage = 15;
    private readonly InteractiveService interactive;

    /// <summary>
    ///     Initializes a new instance of the <see cref="WordOfTheDay" /> class.
    /// </summary>
    /// <param name="interactive">Interactive service for paginated output.</param>
    public WordOfTheDay(InteractiveService interactive)
    {
        this.interactive = interactive;
    }

    /// <summary>
    ///     Shows today's word, or a preview if nothing has been posted yet today.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task Wotd()
    {
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

        var (text, embeds, components) = Service.BuildMessage(ctx.Guild, ctx.Channel, config, entry, localNow);
        var content = note ?? (posted is null ? text : null);
        await ctx.Channel.SendMessageAsync(content, embeds: embeds, components: components);
    }

    /// <summary>
    ///     Enables or disables daily word posting.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task WotdToggle()
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
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task WotdChannel(ITextChannel? channel = null)
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
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task WotdTime(int hour)
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
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task WotdTimezone([Remainder] string timezone)
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
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task WotdPingRole(IRole? role = null)
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
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task WotdMessage([Remainder] string? template = null)
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
    /// <param name="topic">One to five words describing the theme, such as "science" or "cooking".</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task WotdTopic([Remainder] string? topic = null)
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
    /// <param name="partOfSpeech">Any, Noun, Verb, Adjective, or Adverb.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task WotdPos(WordPartOfSpeech partOfSpeech)
    {
        await Service.UpdateConfigAsync(ctx.Guild.Id, c => c.PartOfSpeech = (int)partOfSpeech);
        await ConfirmAsync(Strings.WotdPosSet(ctx.Guild.Id, partOfSpeech.ToString()));
    }

    /// <summary>
    ///     Sets how obscure Datamuse words should be.
    /// </summary>
    /// <param name="difficulty">Any, Common, Moderate, or Rare.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task WotdDifficulty(WordDifficulty difficulty)
    {
        await Service.UpdateConfigAsync(ctx.Guild.Id, c => c.Difficulty = (int)difficulty);
        await ConfirmAsync(Strings.WotdDifficultySet(ctx.Guild.Id, difficulty.ToString()));
    }

    /// <summary>
    ///     Chooses where words come from.
    /// </summary>
    /// <param name="mode">Dictionary, Custom, or Mixed.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task WotdMode(WordSourceMode mode)
    {
        await Service.UpdateConfigAsync(ctx.Guild.Id, c => c.SourceMode = (int)mode);
        await ConfirmAsync(Strings.WotdModeSet(ctx.Guild.Id, mode.ToString()));
    }

    /// <summary>
    ///     Adds a word to the server's custom pool, looking up a definition when none is given.
    /// </summary>
    /// <param name="word">The word to add.</param>
    /// <param name="definition">An optional definition.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task WotdAdd(string word, [Remainder] string? definition = null)
    {
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
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task WotdRemove([Remainder] string word)
    {
        if (await Service.RemoveCustomWordAsync(ctx.Guild.Id, word))
            await ConfirmAsync(Strings.WotdWordRemoved(ctx.Guild.Id, word));
        else
            await ErrorAsync(Strings.WotdWordNotFound(ctx.Guild.Id, word));
    }

    /// <summary>
    ///     Lists the server's custom words.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task WotdList()
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

        await interactive.SendPaginatorAsync(paginator, ctx.Channel, TimeSpan.FromMinutes(10));

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
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task WotdHistory()
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
        await ctx.Channel.SendMessageAsync(embed: embed.Build());
    }

    /// <summary>
    ///     Shows the current configuration.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task WotdConfig()
    {
        var config = await Service.GetConfigAsync(ctx.Guild.Id);
        var customCount = (await Service.GetCustomWordsAsync(ctx.Guild.Id)).Count;
        var ruleCount = (await Service.GetScheduleRulesAsync(ctx.Guild.Id)).Count;
        var embed = WotdFormatter.BuildConfigEmbed(Strings, ctx.Guild.Id, config, customCount, ruleCount);
        await ctx.Channel.SendMessageAsync(embed: embed.Build());
    }

    /// <summary>
    ///     Sets a topic for a weekday, or removes that weekday's rule when no topic is given.
    /// </summary>
    /// <param name="day">The day of the week.</param>
    /// <param name="topic">Topic words for that day.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task WotdDay(DayOfWeek day, [Remainder] string? topic = null)
    {
        await SetRuleTopicAsync(ScheduleRuleType.DayOfWeek, (int)day, day.ToString(), topic);
    }

    /// <summary>
    ///     Sets a part of speech and optional difficulty for a weekday.
    /// </summary>
    /// <param name="day">The day of the week.</param>
    /// <param name="partOfSpeech">Part of speech, or Any to inherit.</param>
    /// <param name="difficulty">Difficulty, or Any to inherit.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task WotdDayFilter(DayOfWeek day, WordPartOfSpeech partOfSpeech,
        WordDifficulty difficulty = WordDifficulty.Any)
    {
        var rule = await Service.UpsertScheduleRuleAsync(ctx.Guild.Id, ScheduleRuleType.DayOfWeek, (int)day, null,
            partOfSpeech, difficulty);
        await ConfirmAsync(Strings.WotdRuleSet(ctx.Guild.Id, day.ToString(),
            WotdFormatter.DescribeRule(Strings, ctx.Guild.Id, rule)));
    }

    /// <summary>
    ///     Sets a topic for a month, or removes that month's rule when no topic is given.
    /// </summary>
    /// <param name="month">Month number or name.</param>
    /// <param name="topic">Topic words for that month.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task WotdMonth(string month, [Remainder] string? topic = null)
    {
        if (!WordOfTheDayService.TryParseMonth(month, out var monthNumber))
        {
            await ErrorAsync(Strings.WotdMonthInvalid(ctx.Guild.Id, month));
            return;
        }

        await SetRuleTopicAsync(ScheduleRuleType.Month, monthNumber, WordOfTheDayService.MonthName(monthNumber),
            topic);
    }

    /// <summary>
    ///     Sets a part of speech and optional difficulty for a month.
    /// </summary>
    /// <param name="month">Month number or name.</param>
    /// <param name="partOfSpeech">Part of speech, or Any to inherit.</param>
    /// <param name="difficulty">Difficulty, or Any to inherit.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task WotdMonthFilter(string month, WordPartOfSpeech partOfSpeech,
        WordDifficulty difficulty = WordDifficulty.Any)
    {
        if (!WordOfTheDayService.TryParseMonth(month, out var monthNumber))
        {
            await ErrorAsync(Strings.WotdMonthInvalid(ctx.Guild.Id, month));
            return;
        }

        var rule = await Service.UpsertScheduleRuleAsync(ctx.Guild.Id, ScheduleRuleType.Month, monthNumber, null,
            partOfSpeech, difficulty);
        await ConfirmAsync(Strings.WotdRuleSet(ctx.Guild.Id, WordOfTheDayService.MonthName(monthNumber),
            WotdFormatter.DescribeRule(Strings, ctx.Guild.Id, rule)));
    }

    /// <summary>
    ///     Shows all weekday and month rules plus today's effective filters.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task WotdSchedule()
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
        await ctx.Channel.SendMessageAsync(embed: embed.Build());
    }

    /// <summary>
    ///     Toggles creating a discussion thread under each daily word post.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task WotdThread()
    {
        var updated = await Service.UpdateConfigAsync(ctx.Guild.Id, c => c.CreateThread = !c.CreateThread);
        await ConfirmAsync(updated.CreateThread
            ? Strings.WotdThreadEnabled(ctx.Guild.Id)
            : Strings.WotdThreadDisabled(ctx.Guild.Id));
    }

    /// <summary>
    ///     Sets, shows, or clears the discussion thread name template.
    /// </summary>
    /// <param name="template">The template, "clear" to reset, or empty to view.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task WotdThreadName([Remainder] string? template = null)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            var config = await Service.GetConfigAsync(ctx.Guild.Id);
            var current = string.IsNullOrWhiteSpace(config.ThreadName)
                ? Strings.WotdThreadDefaultName(ctx.Guild.Id)
                : config.ThreadName;
            await ConfirmAsync(Strings.WotdThreadNameCurrent(ctx.Guild.Id, current));
            return;
        }

        if (template.Equals("clear", StringComparison.OrdinalIgnoreCase))
        {
            await Service.UpdateConfigAsync(ctx.Guild.Id, c => c.ThreadName = null);
            await ConfirmAsync(Strings.WotdThreadNameCleared(ctx.Guild.Id));
            return;
        }

        var cleaned = template.Trim();
        await Service.UpdateConfigAsync(ctx.Guild.Id, c => c.ThreadName = cleaned);
        await ConfirmAsync(Strings.WotdThreadNameSet(ctx.Guild.Id, cleaned));
    }

    /// <summary>
    ///     Sets how long a discussion thread stays open before auto-archiving.
    /// </summary>
    /// <param name="duration">1h, 24h, 3d, or 7d.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task WotdThreadArchive(string duration)
    {
        if (!WotdFormatter.TryParseArchive(duration, out var minutes))
        {
            await ErrorAsync(Strings.WotdThreadArchiveInvalid(ctx.Guild.Id));
            return;
        }

        await Service.UpdateConfigAsync(ctx.Guild.Id, c => c.ThreadAutoArchiveMinutes = minutes);
        await ConfirmAsync(Strings.WotdThreadArchiveSet(ctx.Guild.Id,
            WotdFormatter.ArchiveLabel(Strings, ctx.Guild.Id, minutes)));
    }

    private async Task SetRuleTopicAsync(ScheduleRuleType type, int key, string name, string? topic)
    {
        if (string.IsNullOrWhiteSpace(topic) || topic.Equals("clear", StringComparison.OrdinalIgnoreCase))
        {
            if (await Service.RemoveScheduleRuleAsync(ctx.Guild.Id, type, key))
                await ConfirmAsync(Strings.WotdRuleRemoved(ctx.Guild.Id, name));
            else
                await ErrorAsync(Strings.WotdRuleNotFound(ctx.Guild.Id, name));
            return;
        }

        var cleaned = string.Join(' ', topic.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(5));
        var rule = await Service.UpsertScheduleRuleAsync(ctx.Guild.Id, type, key, cleaned, null, null);
        await ConfirmAsync(Strings.WotdRuleSet(ctx.Guild.Id, name,
            WotdFormatter.DescribeRule(Strings, ctx.Guild.Id, rule)));
    }

    /// <summary>
    ///     Posts a new word immediately, regardless of schedule.
    /// </summary>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageGuild)]
    public async Task WotdPost()
    {
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
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    public async Task WotdReset()
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
