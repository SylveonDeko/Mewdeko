using System.Net.Http;
using System.Text.Json;
using System.Threading;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Modules.WordOfTheDay.Common;
using Mewdeko.Services.Analytics;
using Mewdeko.Services.Strings;

namespace Mewdeko.Modules.WordOfTheDay.Services;

/// <summary>
///     Picks, formats, and posts a daily vocabulary word per guild, sourced from Datamuse or a custom pool.
/// </summary>
public class WordOfTheDayService : INService, IDisposable
{
    private const string DatamuseUrl = "https://api.datamuse.com/words";
    private const string FreeDictionaryUrl = "https://api.dictionaryapi.dev/api/v2/entries/en/";
    private const int HistoryDedupeWindow = 90;
    private const int MinWordLength = 4;
    private const int MaxWordLength = 14;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly DiscordShardedClient client;
    private readonly IAnalyticsCollector collector;
    private readonly ConcurrentDictionary<ulong, (WordOfTheDayConfig Config, DateTime Expiry)> configCache = new();
    private readonly IDataConnectionFactory dbFactory;
    private readonly IHttpClientFactory httpFactory;
    private readonly ILogger<WordOfTheDayService> logger;
    private readonly SemaphoreSlim postLock = new(1, 1);
    private readonly Timer scheduleTimer;
    private readonly GeneratedBotStrings strings;
    private bool isDisposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="WordOfTheDayService" /> class.
    /// </summary>
    /// <param name="client">The Discord client.</param>
    /// <param name="dbFactory">Database connection factory.</param>
    /// <param name="httpFactory">HTTP client factory used for Datamuse and dictionary lookups.</param>
    /// <param name="strings">Localized bot strings.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="collector">Analytics collector.</param>
    public WordOfTheDayService(
        DiscordShardedClient client,
        IDataConnectionFactory dbFactory,
        IHttpClientFactory httpFactory,
        GeneratedBotStrings strings,
        ILogger<WordOfTheDayService> logger,
        IAnalyticsCollector collector)
    {
        this.client = client;
        this.dbFactory = dbFactory;
        this.httpFactory = httpFactory;
        this.strings = strings;
        this.logger = logger;
        this.collector = collector;

        scheduleTimer = new Timer(_ => _ = ProcessScheduledPostsAsync(), null,
            TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (isDisposed) return;
        scheduleTimer.Dispose();
        postLock.Dispose();
        isDisposed = true;
    }

    #region Configuration

    /// <summary>
    ///     Gets the configuration for a guild, creating a disabled default if none exists.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The guild's configuration.</returns>
    public async Task<WordOfTheDayConfig> GetConfigAsync(ulong guildId)
    {
        if (configCache.TryGetValue(guildId, out var cached) && cached.Expiry > DateTime.UtcNow)
            return cached.Config;

        await using var db = await dbFactory.CreateConnectionAsync();
        var config = await db.WordOfTheDayConfigs.FirstOrDefaultAsync(x => x.GuildId == guildId);

        if (config is null)
        {
            config = new WordOfTheDayConfig
            {
                GuildId = guildId,
                Enabled = false,
                PostHour = 9,
                Timezone = "UTC",
                DateAdded = DateTime.UtcNow,
                DateModified = DateTime.UtcNow
            };
            config.Id = await db.InsertWithInt32IdentityAsync(config);
        }

        configCache[guildId] = (config, DateTime.UtcNow.AddMinutes(30));
        return config;
    }

    /// <summary>
    ///     Applies an update to a guild's configuration and persists it.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="update">Mutation to apply.</param>
    /// <returns>The updated configuration.</returns>
    public async Task<WordOfTheDayConfig> UpdateConfigAsync(ulong guildId, Action<WordOfTheDayConfig> update)
    {
        var config = await GetConfigAsync(guildId);
        update(config);
        config.DateModified = DateTime.UtcNow;

        await using var db = await dbFactory.CreateConnectionAsync();
        await db.UpdateAsync(config);
        configCache.TryRemove(guildId, out _);
        return config;
    }

    /// <summary>
    ///     Resets a guild's configuration to defaults and clears its custom words and history.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    public async Task ResetAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        await db.WordOfTheDayConfigs.Where(x => x.GuildId == guildId).DeleteAsync();
        await db.WordOfTheDayWords.Where(x => x.GuildId == guildId).DeleteAsync();
        await db.WordOfTheDayHistories.Where(x => x.GuildId == guildId).DeleteAsync();
        await db.WordOfTheDaySchedules.Where(x => x.GuildId == guildId).DeleteAsync();
        configCache.TryRemove(guildId, out _);
    }

    #endregion

    #region Schedule Rules

    /// <summary>
    ///     Lists a guild's weekday and month rules.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>Rules ordered by type then key.</returns>
    public async Task<List<WordOfTheDaySchedule>> GetScheduleRulesAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.WordOfTheDaySchedules
            .Where(x => x.GuildId == guildId)
            .OrderBy(x => x.RuleType)
            .ThenBy(x => x.RuleKey)
            .ToListAsync();
    }

    /// <summary>
    ///     Creates or updates a rule. Null values leave that field of an existing rule unchanged.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="type">Weekday or month.</param>
    /// <param name="key">Day of week value, or month number 1 to 12.</param>
    /// <param name="topic">Topic override. Empty string clears the topic on the rule.</param>
    /// <param name="partOfSpeech">Part of speech override.</param>
    /// <param name="difficulty">Difficulty override.</param>
    /// <returns>The stored rule.</returns>
    public async Task<WordOfTheDaySchedule> UpsertScheduleRuleAsync(ulong guildId, ScheduleRuleType type, int key,
        string? topic, WordPartOfSpeech? partOfSpeech, WordDifficulty? difficulty)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var rule = await db.WordOfTheDaySchedules
            .FirstOrDefaultAsync(x => x.GuildId == guildId && x.RuleType == (int)type && x.RuleKey == key);

        if (rule is null)
        {
            rule = new WordOfTheDaySchedule
            {
                GuildId = guildId,
                RuleType = (int)type,
                RuleKey = key,
                DateAdded = DateTime.UtcNow
            };
            ApplyRuleFields(rule, topic, partOfSpeech, difficulty);
            rule.Id = await db.InsertWithInt32IdentityAsync(rule);
            return rule;
        }

        ApplyRuleFields(rule, topic, partOfSpeech, difficulty);
        await db.UpdateAsync(rule);
        return rule;
    }

    private static void ApplyRuleFields(WordOfTheDaySchedule rule, string? topic, WordPartOfSpeech? partOfSpeech,
        WordDifficulty? difficulty)
    {
        if (topic is not null)
            rule.Topic = topic.Length == 0 ? null : topic;
        if (partOfSpeech.HasValue)
            rule.PartOfSpeech = partOfSpeech.Value == WordPartOfSpeech.Any ? null : (int)partOfSpeech.Value;
        if (difficulty.HasValue)
            rule.Difficulty = difficulty.Value == WordDifficulty.Any ? null : (int)difficulty.Value;
    }

    /// <summary>
    ///     Deletes a rule.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="type">Weekday or month.</param>
    /// <param name="key">Day of week value, or month number.</param>
    /// <returns>True when a rule was deleted.</returns>
    public async Task<bool> RemoveScheduleRuleAsync(ulong guildId, ScheduleRuleType type, int key)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var deleted = await db.WordOfTheDaySchedules
            .Where(x => x.GuildId == guildId && x.RuleType == (int)type && x.RuleKey == key)
            .DeleteAsync();
        return deleted > 0;
    }

    /// <summary>
    ///     Resolves the filters for a date: weekday rule fields win, then month rule fields, then the base config.
    /// </summary>
    /// <param name="config">The guild configuration.</param>
    /// <param name="localDate">The guild-local date.</param>
    /// <returns>The effective filters.</returns>
    public async Task<WordFilters> GetEffectiveFiltersAsync(WordOfTheDayConfig config, DateTime localDate)
    {
        var filters = new WordFilters
        {
            Topic = string.IsNullOrWhiteSpace(config.Topic) ? null : config.Topic.Trim(),
            PartOfSpeech = (WordPartOfSpeech)config.PartOfSpeech,
            Difficulty = (WordDifficulty)config.Difficulty
        };

        var dayKey = (int)localDate.DayOfWeek;
        var monthKey = localDate.Month;

        await using var db = await dbFactory.CreateConnectionAsync();
        var rules = await db.WordOfTheDaySchedules
            .Where(x => x.GuildId == config.GuildId &&
                        ((x.RuleType == (int)ScheduleRuleType.DayOfWeek && x.RuleKey == dayKey) ||
                         (x.RuleType == (int)ScheduleRuleType.Month && x.RuleKey == monthKey)))
            .ToListAsync();

        var month = rules.FirstOrDefault(x => x.RuleType == (int)ScheduleRuleType.Month);
        var day = rules.FirstOrDefault(x => x.RuleType == (int)ScheduleRuleType.DayOfWeek);

        if (month is not null)
        {
            ApplyOverrides(filters, month);
            filters.SourceRule = MonthName(monthKey);
        }

        if (day is not null)
        {
            ApplyOverrides(filters, day);
            filters.SourceRule = localDate.DayOfWeek.ToString();
        }

        return filters;
    }

    private static void ApplyOverrides(WordFilters filters, WordOfTheDaySchedule rule)
    {
        if (!string.IsNullOrWhiteSpace(rule.Topic))
            filters.Topic = rule.Topic.Trim();
        if (rule.PartOfSpeech.HasValue)
            filters.PartOfSpeech = (WordPartOfSpeech)rule.PartOfSpeech.Value;
        if (rule.Difficulty.HasValue)
            filters.Difficulty = (WordDifficulty)rule.Difficulty.Value;
    }

    /// <summary>
    ///     Gets the English month name for a month number.
    /// </summary>
    /// <param name="month">Month number 1 to 12.</param>
    /// <returns>The month name.</returns>
    public static string MonthName(int month)
    {
        return System.Globalization.CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(month);
    }

    /// <summary>
    ///     Parses a month given as a number or an English name, full or abbreviated.
    /// </summary>
    /// <param name="input">User input such as "3", "march", or "mar".</param>
    /// <param name="month">The month number when successful.</param>
    /// <returns>True when parsed.</returns>
    public static bool TryParseMonth(string input, out int month)
    {
        month = 0;
        input = input.Trim();
        if (int.TryParse(input, out var number) && number is >= 1 and <= 12)
        {
            month = number;
            return true;
        }

        for (var i = 1; i <= 12; i++)
        {
            var name = MonthName(i);
            if (name.Equals(input, StringComparison.OrdinalIgnoreCase) ||
                name[..3].Equals(input, StringComparison.OrdinalIgnoreCase))
            {
                month = i;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Resolves a timezone identifier, accepting IANA or Windows names.
    /// </summary>
    /// <param name="id">The timezone identifier.</param>
    /// <param name="timeZone">The resolved timezone when successful.</param>
    /// <returns>True when the identifier is valid.</returns>
    public static bool TryGetTimeZone(string? id, out TimeZoneInfo timeZone)
    {
        timeZone = TimeZoneInfo.Utc;
        if (string.IsNullOrWhiteSpace(id)) return false;
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(id);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    ///     Gets the current local date for a guild based on its configured timezone.
    /// </summary>
    /// <param name="config">The guild configuration.</param>
    /// <returns>The local date and time.</returns>
    public static DateTime GetLocalNow(WordOfTheDayConfig config)
    {
        var tz = TryGetTimeZone(config.Timezone, out var resolved) ? resolved : TimeZoneInfo.Utc;
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
    }

    #endregion

    #region Custom Words

    /// <summary>
    ///     Adds a word to the guild's custom pool, looking up a definition if none is supplied.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user adding the word.</param>
    /// <param name="word">The word to add.</param>
    /// <param name="definition">An optional definition. When omitted, a dictionary lookup is attempted.</param>
    /// <returns>The stored row, or null when the word exists already or no definition could be found.</returns>
    public async Task<(WordOfTheDayWord? Entry, bool AlreadyExists)> AddCustomWordAsync(ulong guildId,
        ulong userId, string word, string? definition)
    {
        word = word.Trim();
        var lowered = word.ToLowerInvariant();

        await using var db = await dbFactory.CreateConnectionAsync();
        var exists = await db.WordOfTheDayWords
            .AnyAsync(x => x.GuildId == guildId && x.Word.ToLower() == lowered);
        if (exists) return (null, true);

        string? partOfSpeech = null;
        string? example = null;

        if (string.IsNullOrWhiteSpace(definition))
        {
            var looked = await LookupFreeDictionaryAsync(word);
            if (looked is null) return (null, false);
            definition = looked.Definition;
            partOfSpeech = looked.PartOfSpeech;
            example = looked.Example;
        }

        var entry = new WordOfTheDayWord
        {
            GuildId = guildId,
            Word = word,
            Definition = definition,
            PartOfSpeech = partOfSpeech,
            Example = example,
            AddedBy = userId,
            TimesUsed = 0,
            DateAdded = DateTime.UtcNow
        };
        entry.Id = await db.InsertWithInt32IdentityAsync(entry);
        return (entry, false);
    }

    /// <summary>
    ///     Removes a word from the guild's custom pool.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="word">The word to remove.</param>
    /// <returns>True when a row was deleted.</returns>
    public async Task<bool> RemoveCustomWordAsync(ulong guildId, string word)
    {
        var lowered = word.Trim().ToLowerInvariant();
        await using var db = await dbFactory.CreateConnectionAsync();
        var deleted = await db.WordOfTheDayWords
            .Where(x => x.GuildId == guildId && x.Word.ToLower() == lowered)
            .DeleteAsync();
        return deleted > 0;
    }

    /// <summary>
    ///     Lists the guild's custom words, least recently used first.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The custom word rows.</returns>
    public async Task<List<WordOfTheDayWord>> GetCustomWordsAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.WordOfTheDayWords
            .Where(x => x.GuildId == guildId)
            .OrderBy(x => x.LastUsed)
            .ThenBy(x => x.DateAdded)
            .ToListAsync();
    }

    /// <summary>
    ///     Gets the most recently posted words for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="count">Maximum rows to return.</param>
    /// <returns>History rows, newest first.</returns>
    public async Task<List<WordOfTheDayHistory>> GetHistoryAsync(ulong guildId, int count = 10)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.WordOfTheDayHistories
            .Where(x => x.GuildId == guildId)
            .OrderByDescending(x => x.PostedOn)
            .ThenByDescending(x => x.Id)
            .Take(count)
            .ToListAsync();
    }

    /// <summary>
    ///     Gets the word already posted for a guild on a given local date, if any.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="localDate">The guild-local date.</param>
    /// <returns>The history row or null.</returns>
    public async Task<WordOfTheDayHistory?> GetPostedForDateAsync(ulong guildId, DateTime localDate)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.WordOfTheDayHistories
            .Where(x => x.GuildId == guildId && x.PostedOn == localDate.Date)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync();
    }

    #endregion

    #region Word Selection

    /// <summary>
    ///     Chooses the next word for a guild according to its source mode and filters.
    /// </summary>
    /// <param name="config">The guild configuration.</param>
    /// <returns>A resolved word, or null when no source produced one.</returns>
    public async Task<WordEntry?> SelectWordAsync(WordOfTheDayConfig config)
    {
        var mode = (WordSourceMode)config.SourceMode;

        if (mode is WordSourceMode.Custom or WordSourceMode.Mixed)
        {
            var custom = await PickCustomWordAsync(config.GuildId, mode == WordSourceMode.Mixed);
            if (custom is not null) return custom;
            if (mode == WordSourceMode.Custom) return null;
        }

        return await PickDatamuseWordAsync(config);
    }

    private async Task<WordEntry?> PickCustomWordAsync(ulong guildId, bool unusedOnly)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var query = db.WordOfTheDayWords.Where(x => x.GuildId == guildId);
        if (unusedOnly) query = query.Where(x => x.TimesUsed == 0);

        var minUsed = await query.Select(x => (int?)x.TimesUsed).MinAsync();
        if (minUsed is null) return null;

        var candidates = await query.Where(x => x.TimesUsed == minUsed.Value).ToListAsync();
        var pick = candidates[Random.Shared.Next(candidates.Count)];

        var definition = pick.Definition;
        var partOfSpeech = pick.PartOfSpeech;
        var example = pick.Example;
        string? phonetic = null;

        var enrichment = await LookupFreeDictionaryAsync(pick.Word);
        if (enrichment is not null)
        {
            phonetic = enrichment.Phonetic;
            definition ??= enrichment.Definition;
            partOfSpeech ??= enrichment.PartOfSpeech;
            example ??= enrichment.Example;
        }

        if (string.IsNullOrWhiteSpace(definition)) return null;

        return new WordEntry
        {
            Word = pick.Word,
            Definition = definition,
            PartOfSpeech = partOfSpeech,
            Example = example,
            Phonetic = phonetic,
            IsCustom = true,
            CustomWordId = pick.Id
        };
    }

    private async Task<WordEntry?> PickDatamuseWordAsync(WordOfTheDayConfig config)
    {
        var recent = await GetRecentWordsAsync(config.GuildId);
        var filters = await GetEffectiveFiltersAsync(config, GetLocalNow(config).Date);
        var topic = filters.Topic;
        var pos = filters.PartOfSpeech;
        var difficulty = filters.Difficulty;

        var letters = "abcdefghijklmnopqrstuvwxyz".OrderBy(_ => Random.Shared.Next()).Take(6).ToList();

        foreach (var letter in letters)
        {
            var candidates = await QueryDatamuseAsync(letter, topic);
            var filtered = candidates
                .Where(c => IsUsableWord(c, pos, difficulty) && !recent.Contains(c.Word.ToLowerInvariant()))
                .ToList();

            if (filtered.Count == 0 && topic is not null)
            {
                candidates = await QueryDatamuseAsync(letter, null);
                filtered = candidates
                    .Where(c => IsUsableWord(c, pos, difficulty) && !recent.Contains(c.Word.ToLowerInvariant()))
                    .ToList();
            }

            if (filtered.Count == 0) continue;

            var pick = filtered[Random.Shared.Next(filtered.Count)];
            var (posCode, definition) = ParseDatamuseDefinition(pick, pos);
            if (definition is null) continue;

            var entry = new WordEntry
            {
                Word = pick.Word,
                Definition = definition,
                PartOfSpeech = ExpandPartOfSpeech(posCode)
            };

            var enrichment = await LookupFreeDictionaryAsync(pick.Word);
            if (enrichment is not null)
            {
                entry.Phonetic = enrichment.Phonetic;
                entry.Example = enrichment.Example;
            }

            return entry;
        }

        return null;
    }

    private async Task<HashSet<string>> GetRecentWordsAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var words = await db.WordOfTheDayHistories
            .Where(x => x.GuildId == guildId)
            .OrderByDescending(x => x.Id)
            .Take(HistoryDedupeWindow)
            .Select(x => x.Word)
            .ToListAsync();
        return words.Select(w => w.ToLowerInvariant()).ToHashSet();
    }

    private async Task<List<DatamuseWord>> QueryDatamuseAsync(char letter, string? topic)
    {
        try
        {
            using var http = httpFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(15);

            var url = $"{DatamuseUrl}?sp={letter}*&md=dpf&max=500";
            if (topic is not null)
                url += $"&topics={Uri.EscapeDataString(topic)}";

            await using var stream = await http.GetStreamAsync(url);
            var result = await JsonSerializer.DeserializeAsync<List<DatamuseWord>>(stream, JsonOptions);
            return result ?? [];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Datamuse query failed for letter {Letter} topic {Topic}", letter, topic);
            return [];
        }
    }

    private static bool IsUsableWord(DatamuseWord candidate, WordPartOfSpeech pos, WordDifficulty difficulty)
    {
        var word = candidate.Word;
        if (word.Length < MinWordLength || word.Length > MaxWordLength) return false;
        if (!word.All(char.IsLetter)) return false;
        if (candidate.Defs is null || candidate.Defs.Count == 0) return false;

        var tags = candidate.Tags ?? [];

        if (pos != WordPartOfSpeech.Any)
        {
            var wanted = PartOfSpeechCode(pos);
            if (!tags.Contains(wanted)) return false;
            if (!candidate.Defs.Any(d => d.StartsWith(wanted + "\t", StringComparison.Ordinal))) return false;
        }

        if (difficulty == WordDifficulty.Any) return true;

        var freqTag = tags.FirstOrDefault(t => t.StartsWith("f:", StringComparison.Ordinal));
        if (freqTag is null) return false;
        if (!double.TryParse(freqTag[2..], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var freq))
            return false;

        return difficulty switch
        {
            WordDifficulty.Common => freq >= 5.0,
            WordDifficulty.Moderate => freq is >= 0.5 and < 5.0,
            WordDifficulty.Rare => freq is > 0.01 and < 0.5,
            _ => true
        };
    }

    private static (string? PosCode, string? Definition) ParseDatamuseDefinition(DatamuseWord candidate,
        WordPartOfSpeech pos)
    {
        var defs = candidate.Defs ?? [];
        var wanted = pos == WordPartOfSpeech.Any ? null : PartOfSpeechCode(pos);

        foreach (var raw in defs)
        {
            var split = raw.Split('\t', 2);
            var code = split.Length == 2 ? split[0] : null;
            var text = split.Length == 2 ? split[1] : split[0];
            if (wanted is not null && code != wanted) continue;
            text = text.Trim();
            if (text.Length == 0) continue;
            if (text.StartsWith("(obsolete", StringComparison.OrdinalIgnoreCase)) continue;
            if (text.StartsWith("(archaic", StringComparison.OrdinalIgnoreCase)) continue;
            return (code, text);
        }

        return (null, null);
    }

    private static string PartOfSpeechCode(WordPartOfSpeech pos)
    {
        return pos switch
        {
            WordPartOfSpeech.Noun => "n",
            WordPartOfSpeech.Verb => "v",
            WordPartOfSpeech.Adjective => "adj",
            WordPartOfSpeech.Adverb => "adv",
            _ => ""
        };
    }

    private static string? ExpandPartOfSpeech(string? code)
    {
        return code switch
        {
            "n" => "noun",
            "v" => "verb",
            "adj" => "adjective",
            "adv" => "adverb",
            "u" => null,
            null => null,
            _ => code
        };
    }

    /// <summary>
    ///     Looks up a word in the Free Dictionary API for phonetics, a definition, and an example.
    /// </summary>
    /// <param name="word">The word to look up.</param>
    /// <returns>A partial entry, or null when the word is unknown or the service is unavailable.</returns>
    public async Task<WordEntry?> LookupFreeDictionaryAsync(string word)
    {
        try
        {
            using var http = httpFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(10);
            using var response = await http.GetAsync(FreeDictionaryUrl + Uri.EscapeDataString(word.ToLowerInvariant()));
            if (!response.IsSuccessStatusCode) return null;

            await using var stream = await response.Content.ReadAsStreamAsync();
            var entries = await JsonSerializer.DeserializeAsync<List<FreeDictionaryEntry>>(stream, JsonOptions);
            var first = entries?.FirstOrDefault();
            if (first?.Meanings is null || first.Meanings.Count == 0) return null;

            var meaning = first.Meanings.FirstOrDefault(m => m.Definitions is { Count: > 0 });
            var definition = meaning?.Definitions?.FirstOrDefault(d => !string.IsNullOrWhiteSpace(d.Definition));
            if (definition?.Definition is null) return null;

            var example = meaning!.Definitions!
                .Select(d => d.Example)
                .FirstOrDefault(e => !string.IsNullOrWhiteSpace(e));

            return new WordEntry
            {
                Word = word,
                Definition = definition.Definition,
                PartOfSpeech = meaning.PartOfSpeech,
                Example = example,
                Phonetic = first.Phonetic
            };
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Free Dictionary lookup failed for {Word}", word);
            return null;
        }
    }

    #endregion

    #region Posting

    /// <summary>
    ///     Builds the message for a word using the guild's template, or a default embed when none is set.
    /// </summary>
    /// <param name="guild">The guild the message is for.</param>
    /// <param name="channel">The destination channel.</param>
    /// <param name="config">The guild configuration.</param>
    /// <param name="entry">The word to render.</param>
    /// <param name="localDate">The guild-local date to display.</param>
    /// <returns>Plain text, embeds, and components to send.</returns>
    public (string? Text, Discord.Embed[]? Embeds, MessageComponent? Components) BuildMessage(IGuild guild,
        IMessageChannel channel, WordOfTheDayConfig config, WordEntry entry, DateTime localDate)
    {
        var pingText = config.PingRoleId.HasValue && guild.GetRole(config.PingRoleId.Value) is { } role
            ? role.Mention
            : null;

        if (!string.IsNullOrWhiteSpace(config.MessageTemplate))
        {
            var replacer = new ReplacementBuilder()
                .WithOverride("%wotd.word%", () => entry.Word)
                .WithOverride("%wotd.definition%", () => entry.Definition)
                .WithOverride("%wotd.pos%", () => entry.PartOfSpeech ?? "")
                .WithOverride("%wotd.example%", () => entry.Example ?? "")
                .WithOverride("%wotd.phonetic%", () => entry.Phonetic ?? "")
                .WithOverride("%wotd.date%", () => localDate.ToString("MMMM d, yyyy"))
                .WithOverride("%wotd.ping%", () => pingText ?? "")
                .WithDefault(client.CurrentUser, channel, guild as SocketGuild, client)
                .Build();

            var rendered = replacer.Replace(config.MessageTemplate);
            if (SmartEmbed.TryParse(rendered, guild.Id, out var embeds, out var plain, out var components))
            {
                var text = string.IsNullOrWhiteSpace(plain) ? pingText : plain;
                return (text, embeds, components?.Build());
            }

            return (rendered.SanitizeMentions(true), null, null);
        }

        var title = strings.WotdEmbedTitle(guild.Id, localDate.ToString("MMMM d, yyyy"));
        var heading = entry.Phonetic is null
            ? $"**{entry.Word}**"
            : $"**{entry.Word}**  {entry.Phonetic}";
        if (entry.PartOfSpeech is not null)
            heading += $"  *{entry.PartOfSpeech}*";

        var builder = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(title)
            .WithDescription(heading)
            .AddField(strings.WotdFieldDefinition(guild.Id), Clamp(entry.Definition, 1000));

        if (!string.IsNullOrWhiteSpace(entry.Example))
            builder.AddField(strings.WotdFieldExample(guild.Id), $"*{Clamp(entry.Example, 1000)}*");

        var footer = entry.IsCustom
            ? strings.WotdFooterCustom(guild.Id)
            : strings.WotdFooterDatamuse(guild.Id);
        builder.WithFooter(footer);

        return (pingText, [builder.Build()], null);
    }

    /// <summary>
    ///     Truncates text to fit within a Discord embed field.
    /// </summary>
    /// <param name="text">The text to clamp.</param>
    /// <param name="max">Maximum length including the ellipsis.</param>
    /// <returns>The original or shortened text.</returns>
    public static string Clamp(string text, int max)
    {
        return text.Length <= max ? text : text[..(max - 1)] + "…";
    }

    /// <summary>
    ///     Posts a word to the guild's configured channel and records it in history.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="force">When true, posts even if a word was already posted today.</param>
    /// <returns>The posted entry, or null when nothing was posted.</returns>
    public async Task<(WordEntry? Entry, string? FailureKey)> PostNowAsync(ulong guildId, bool force)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null) return (null, "guild");

        var config = await GetConfigAsync(guildId);
        if (!config.ChannelId.HasValue) return (null, "channel");

        var channel = guild.GetTextChannel(config.ChannelId.Value);
        if (channel is null) return (null, "channel");

        var localNow = GetLocalNow(config);
        if (!force && config.LastPostedDate.HasValue && config.LastPostedDate.Value.Date == localNow.Date)
            return (null, "already");

        var entry = await SelectWordAsync(config);
        if (entry is null) return (null, "noword");

        await postLock.WaitAsync();
        try
        {
            var (text, embeds, components) = BuildMessage(guild, channel, config, entry, localNow);
            await channel.SendMessageAsync(text, embeds: embeds, components: components,
                allowedMentions: AllowedMentions.All);
        }
        catch (Exception ex)
        {
            collector.Feature("word_of_the_day", guildId, false, ex.GetType().Name);
            logger.LogWarning(ex, "Failed to post word of the day in guild {GuildId}", guildId);
            return (null, "send");
        }
        finally
        {
            postLock.Release();
        }

        collector.Feature("word_of_the_day", guildId);
        await RecordPostAsync(config, entry, localNow.Date);
        return (entry, null);
    }

    private async Task RecordPostAsync(WordOfTheDayConfig config, WordEntry entry, DateTime localDate)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        await db.InsertAsync(new WordOfTheDayHistory
        {
            GuildId = config.GuildId,
            Word = entry.Word,
            PartOfSpeech = entry.PartOfSpeech,
            Definition = entry.Definition,
            Example = entry.Example,
            Phonetic = entry.Phonetic,
            PostedOn = localDate,
            DateAdded = DateTime.UtcNow
        });

        if (entry.CustomWordId.HasValue)
        {
            await db.WordOfTheDayWords
                .Where(x => x.Id == entry.CustomWordId.Value)
                .Set(x => x.TimesUsed, x => x.TimesUsed + 1)
                .Set(x => x.LastUsed, DateTime.UtcNow)
                .UpdateAsync();
        }

        await UpdateConfigAsync(config.GuildId, c => c.LastPostedDate = localDate);
    }

    private async Task ProcessScheduledPostsAsync()
    {
        List<WordOfTheDayConfig> due;
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            due = await db.WordOfTheDayConfigs
                .Where(x => x.Enabled && x.ChannelId != null)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load word of the day schedules");
            return;
        }

        foreach (var config in due)
        {
            try
            {
                if (client.GetGuild(config.GuildId) is null) continue;

                var localNow = GetLocalNow(config);
                if (localNow.Hour < config.PostHour) continue;
                if (config.LastPostedDate.HasValue && config.LastPostedDate.Value.Date == localNow.Date) continue;

                await PostNowAsync(config.GuildId, false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed scheduled word of the day for guild {GuildId}", config.GuildId);
            }
        }
    }

    #endregion
}
