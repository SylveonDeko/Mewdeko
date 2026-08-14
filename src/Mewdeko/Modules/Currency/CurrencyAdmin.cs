using System.Globalization;
using System.Text;
using DataModel;
using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Currency.Services;

namespace Mewdeko.Modules.Currency;

/// <summary>
///     Economy tuning and reporting. Every rate that used to be a hardcoded constant is adjustable here,
///     and the analytics commands show the effect those rates are actually having.
/// </summary>
public partial class Currency
{
    /// <summary>
    ///     Maps a setting name to the code that parses a value for it and applies it.
    /// </summary>
    /// <remarks>
    ///     A table rather than one command per setting: there are over twenty knobs, and a command each
    ///     would bury the rest of the module in the help listing for no gain in clarity.
    /// </remarks>
    private static readonly Dictionary<string, Func<Currency, string, Task<string>>> Setters = new()
    {
        ["gambling"] = (m, v) => m.ApplyAsync(v, ParseBool, (c, x) => c.GamblingEnabled = x),
        ["minbet"] = (m, v) => m.ApplyAsync(v, ParseLong, (c, x) => c.MinBet = Math.Max(1, x)),
        ["maxbet"] = (m, v) => m.ApplyAsync(v, ParseLong, (c, x) => c.MaxBet = x),
        ["payoutmultiplier"] = (m, v) => m.ApplyAsync(v, x => ParseDouble(x, 0.1, 5.0),
            (c, x) => c.PayoutMultiplier = x),
        ["gamecooldown"] = (m, v) => m.ApplyAsync(v, x => ParseInt(x, 0, 86400),
            (c, x) => c.GameCooldownSeconds = x),
        ["losslimit"] = (m, v) => m.ApplyAsync(v, ParseLong, (c, x) => c.LossLimitPerDay = x),
        ["pay"] = (m, v) => m.ApplyAsync(v, ParseBool, (c, x) => c.PayEnabled = x),
        ["paytax"] = (m, v) => m.ApplyAsync(v, x => ParseInt(x, 0, 100), (c, x) => c.PayTaxPercent = x),
        ["paycooldown"] = (m, v) => m.ApplyAsync(v, x => ParseInt(x, 0, 86400), (c, x) => c.PayCooldownSeconds = x),
        ["payminimum"] = (m, v) => m.ApplyAsync(v, ParseLong, (c, x) => c.PayMinimum = Math.Max(1, x)),
        ["bank"] = (m, v) => m.ApplyAsync(v, ParseBool, (c, x) => c.BankEnabled = x),
        ["bankcapacity"] = (m, v) => m.ApplyAsync(v, ParseLong, (c, x) => c.BankCapacity = x),
        ["bankinterest"] = (m, v) => m.ApplyAsync(v, x => ParseDouble(x, 0, 100),
            (c, x) => c.BankInterestPercent = x),
        ["bankinteresthours"] = (m, v) => m.ApplyAsync(v, x => ParseInt(x, 1, 720),
            (c, x) => c.BankInterestHours = x),
        ["work"] = (m, v) => m.ApplyAsync(v, ParseBool, (c, x) => c.WorkEnabled = x),
        ["workmin"] = (m, v) => m.ApplyAsync(v, ParseLong, (c, x) => c.WorkMinReward = x),
        ["workmax"] = (m, v) => m.ApplyAsync(v, ParseLong, (c, x) => c.WorkMaxReward = x),
        ["workcooldown"] = (m, v) => m.ApplyAsync(v, x => ParseInt(x, 0, 86400), (c, x) => c.WorkCooldownSeconds = x),
        ["crime"] = (m, v) => m.ApplyAsync(v, ParseBool, (c, x) => c.CrimeEnabled = x),
        ["crimemin"] = (m, v) => m.ApplyAsync(v, ParseLong, (c, x) => c.CrimeMinReward = x),
        ["crimemax"] = (m, v) => m.ApplyAsync(v, ParseLong, (c, x) => c.CrimeMaxReward = x),
        ["crimechance"] = (m, v) => m.ApplyAsync(v, x => ParseInt(x, 0, 100), (c, x) => c.CrimeSuccessChance = x),
        ["crimefinemin"] = (m, v) => m.ApplyAsync(v, ParseLong, (c, x) => c.CrimeFineMin = x),
        ["crimefinemax"] = (m, v) => m.ApplyAsync(v, ParseLong, (c, x) => c.CrimeFineMax = x),
        ["crimecooldown"] = (m, v) => m.ApplyAsync(v, x => ParseInt(x, 0, 86400),
            (c, x) => c.CrimeCooldownSeconds = x),
        ["rob"] = (m, v) => m.ApplyAsync(v, ParseBool, (c, x) => c.RobEnabled = x),
        ["robchance"] = (m, v) => m.ApplyAsync(v, x => ParseInt(x, 0, 100), (c, x) => c.RobSuccessChance = x),
        ["robmaxsteal"] = (m, v) => m.ApplyAsync(v, x => ParseInt(x, 1, 100), (c, x) => c.RobMaxStealPercent = x),
        ["robfine"] = (m, v) => m.ApplyAsync(v, x => ParseInt(x, 0, 100), (c, x) => c.RobFinePercent = x),
        ["robminimum"] = (m, v) => m.ApplyAsync(v, ParseLong, (c, x) => c.RobMinimumWallet = x),
        ["robcooldown"] = (m, v) => m.ApplyAsync(v, x => ParseInt(x, 0, 86400), (c, x) => c.RobCooldownSeconds = x),
        ["dailystreak"] = (m, v) => m.ApplyAsync(v, ParseBool, (c, x) => c.DailyStreakEnabled = x),
        ["dailystreakbonus"] = (m, v) => m.ApplyAsync(v, ParseLong, (c, x) => c.DailyStreakBonus = x),
        ["dailystreakmax"] = (m, v) => m.ApplyAsync(v, ParseLong, (c, x) => c.DailyStreakMaxBonus = x)
    };

    /// <summary>
    ///     The analytics service reporting on economy health.
    /// </summary>
    public CurrencyAnalyticsService AnalyticsService { get; set; }

    /// <summary>
    ///     Shows every economy setting and its current value.
    /// </summary>
    /// <example>.economyconfig</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    public async Task EconomyConfig()
    {
        var config = await ConfigService.GetConfigAsync(ctx.Guild.Id);
        var emote = await Service.GetCurrencyEmote(ctx.Guild.Id);

        var betting = new StringBuilder()
            .AppendLine($"`gambling` {Fmt(config.GamblingEnabled)}")
            .AppendLine($"`minbet` {config.MinBet:N0} {emote}")
            .AppendLine($"`maxbet` {(config.MaxBet > 0 ? $"{config.MaxBet:N0} {emote}" : Unlimited())}")
            .AppendLine($"`payoutmultiplier` {config.PayoutMultiplier:0.00}x")
            .AppendLine($"`gamecooldown` {config.GameCooldownSeconds}s")
            .AppendLine($"`losslimit` {(config.LossLimitPerDay > 0 ? $"{config.LossLimitPerDay:N0} {emote}" : Off())}");

        var transfers = new StringBuilder()
            .AppendLine($"`pay` {Fmt(config.PayEnabled)}")
            .AppendLine($"`paytax` {config.PayTaxPercent}%")
            .AppendLine($"`paycooldown` {config.PayCooldownSeconds}s")
            .AppendLine($"`payminimum` {config.PayMinimum:N0} {emote}");

        var bank = new StringBuilder()
            .AppendLine($"`bank` {Fmt(config.BankEnabled)}")
            .AppendLine(
                $"`bankcapacity` {(config.BankCapacity > 0 ? $"{config.BankCapacity:N0} {emote}" : Unlimited())}")
            .AppendLine($"`bankinterest` {config.BankInterestPercent:0.##}%")
            .AppendLine($"`bankinteresthours` {config.BankInterestHours}h");

        var earning = new StringBuilder()
            .AppendLine($"`work` {Fmt(config.WorkEnabled)} {config.WorkMinReward:N0}-{config.WorkMaxReward:N0}")
            .AppendLine($"`workcooldown` {config.WorkCooldownSeconds}s")
            .AppendLine($"`crime` {Fmt(config.CrimeEnabled)} {config.CrimeMinReward:N0}-{config.CrimeMaxReward:N0}")
            .AppendLine($"`crimechance` {config.CrimeSuccessChance}%")
            .AppendLine($"`crimefine` {config.CrimeFineMin:N0}-{config.CrimeFineMax:N0}")
            .AppendLine($"`crimecooldown` {config.CrimeCooldownSeconds}s");

        var robbery = new StringBuilder()
            .AppendLine($"`rob` {Fmt(config.RobEnabled)}")
            .AppendLine($"`robchance` {config.RobSuccessChance}%")
            .AppendLine($"`robmaxsteal` {config.RobMaxStealPercent}%")
            .AppendLine($"`robfine` {config.RobFinePercent}%")
            .AppendLine($"`robminimum` {config.RobMinimumWallet:N0} {emote}")
            .AppendLine($"`robcooldown` {config.RobCooldownSeconds}s");

        var daily = new StringBuilder()
            .AppendLine($"`dailystreak` {Fmt(config.DailyStreakEnabled)}")
            .AppendLine($"`dailystreakbonus` {config.DailyStreakBonus:N0} {emote}")
            .AppendLine(
                $"`dailystreakmax` {(config.DailyStreakMaxBonus > 0 ? $"{config.DailyStreakMaxBonus:N0} {emote}" : Unlimited())}");

        var eb = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.EconomyConfigTitle(ctx.Guild.Id, ctx.Guild.Name))
            .WithDescription(Strings.EconomyConfigHelp(ctx.Guild.Id))
            .AddField(Strings.EconomyConfigBetting(ctx.Guild.Id), betting.ToString(), true)
            .AddField(Strings.EconomyConfigTransfers(ctx.Guild.Id), transfers.ToString(), true)
            .AddField(Strings.EconomyConfigBank(ctx.Guild.Id), bank.ToString(), true)
            .AddField(Strings.EconomyConfigEarning(ctx.Guild.Id), earning.ToString(), true)
            .AddField(Strings.EconomyConfigRobbery(ctx.Guild.Id), robbery.ToString(), true)
            .AddField(Strings.EconomyConfigDaily(ctx.Guild.Id), daily.ToString(), true);

        await ctx.Channel.SendMessageAsync(embed: eb.Build());
        return;

        string Fmt(bool value)
        {
            return value ? Strings.EconomyConfigOn(ctx.Guild.Id) : Strings.EconomyConfigOff(ctx.Guild.Id);
        }

        string Unlimited()
        {
            return Strings.EconomyConfigUnlimited(ctx.Guild.Id);
        }

        string Off()
        {
            return Strings.EconomyConfigOff(ctx.Guild.Id);
        }
    }

    /// <summary>
    ///     Changes one economy setting. Run the config command to see every available setting name.
    /// </summary>
    /// <param name="setting">The setting to change.</param>
    /// <param name="value">The new value.</param>
    /// <example>.economyset maxbet 10000</example>
    /// <example>.economyset rob true</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    public async Task EconomySet(string setting, [Remainder] string value)
    {
        var key = setting.ToLowerInvariant().Replace("_", "").Replace("-", "");

        if (!Setters.TryGetValue(key, out var setter))
        {
            await ErrorAsync(Strings.EconomySetUnknown(ctx.Guild.Id, setting));
            return;
        }

        string applied;

        try
        {
            applied = await setter(this, value.Trim());
        }
        catch (FormatException)
        {
            await ErrorAsync(Strings.EconomySetBadValue(ctx.Guild.Id, value));
            return;
        }

        await ConfirmAsync(Strings.EconomySetOk(ctx.Guild.Id, key, applied));
    }

    /// <summary>
    ///     Restores every economy setting to its default.
    /// </summary>
    /// <example>.economyreset</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    public async Task EconomyReset()
    {
        if (!await PromptUserConfirmAsync(Strings.EconomyResetConfirm(ctx.Guild.Id), ctx.User.Id))
            return;

        await ConfigService.ResetAsync(ctx.Guild.Id);
        await ConfirmAsync(Strings.EconomyResetOk(ctx.Guild.Id));
    }

    /// <summary>
    ///     Reports the size and concentration of the server's money supply.
    /// </summary>
    /// <example>.economystats</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task EconomyStats()
    {
        var snapshot = await AnalyticsService.GetSnapshotAsync(ctx.Guild.Id);
        var emote = await Service.GetCurrencyEmote(ctx.Guild.Id);

        if (snapshot.Holders == 0)
        {
            await ErrorAsync(Strings.EconomyStatsEmpty(ctx.Guild.Id));
            return;
        }

        var history = await AnalyticsService.GetSupplyHistoryAsync(ctx.Guild.Id, 7);
        var weekNet = history.Sum(x => x.Net);

        var eb = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.EconomyStatsTitle(ctx.Guild.Id, ctx.Guild.Name))
            .AddField(Strings.EconomyStatsSupply(ctx.Guild.Id), $"{snapshot.MoneySupply:N0} {emote}", true)
            .AddField(Strings.EconomyStatsHolders(ctx.Guild.Id), $"{snapshot.Holders:N0}", true)
            .AddField(Strings.EconomyStatsWeekChange(ctx.Guild.Id), $"{weekNet:+#,##0;-#,##0;0} {emote}", true)
            .AddField(Strings.EconomyStatsWallets(ctx.Guild.Id), $"{snapshot.InWallets:N0} {emote}", true)
            .AddField(Strings.EconomyStatsBanked(ctx.Guild.Id), $"{snapshot.InBanks:N0} {emote}", true)
            .AddField(Strings.EconomyStatsMean(ctx.Guild.Id), $"{snapshot.Mean:N0} {emote}", true)
            .AddField(Strings.EconomyStatsMedian(ctx.Guild.Id), $"{snapshot.Median:N0} {emote}", true)
            .AddField(Strings.EconomyStatsGini(ctx.Guild.Id), $"{snapshot.Gini:0.000}", true)
            .AddField(Strings.EconomyStatsTopShare(ctx.Guild.Id), $"{snapshot.TopTenPercentShare:P1}", true)
            .WithFooter(Strings.EconomyStatsGiniHint(ctx.Guild.Id));

        await ctx.Channel.SendMessageAsync(embed: eb.Build());
    }

    /// <summary>
    ///     Shows where currency has been entering and leaving circulation.
    /// </summary>
    /// <param name="days">How many days back to report. Defaults to 7.</param>
    /// <example>.economyflow 30</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task EconomyFlow(int days = 7)
    {
        days = Math.Clamp(days, 1, 365);

        var window = TimeSpan.FromDays(days);
        var buckets = await AnalyticsService.GetFlowAsync(ctx.Guild.Id, window);
        var emote = await Service.GetCurrencyEmote(ctx.Guild.Id);

        if (buckets.Count == 0)
        {
            await ErrorAsync(Strings.EconomyFlowEmpty(ctx.Guild.Id, days));
            return;
        }

        var faucets = new StringBuilder();
        var sinks = new StringBuilder();

        foreach (var bucket in buckets)
        {
            var line = $"`{bucket.Category}` {bucket.Net:+#,##0;-#,##0;0} {emote} ({bucket.Entries:N0})\n";

            if (bucket.Net >= 0)
                faucets.Append(line);
            else
                sinks.Append(line);
        }

        var tax = await AnalyticsService.GetTransferTaxAsync(ctx.Guild.Id, window);

        if (tax > 0)
            sinks.Append($"`TransferTax` -{tax:N0} {emote}\n");

        var net = buckets.Sum(x => x.Net);

        var eb = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.EconomyFlowTitle(ctx.Guild.Id, days))
            .AddField(Strings.EconomyFlowFaucets(ctx.Guild.Id),
                faucets.Length == 0 ? Strings.EconomyFlowNone(ctx.Guild.Id) : faucets.ToString())
            .AddField(Strings.EconomyFlowSinks(ctx.Guild.Id),
                sinks.Length == 0 ? Strings.EconomyFlowNone(ctx.Guild.Id) : sinks.ToString())
            .WithFooter(Strings.EconomyFlowNet(ctx.Guild.Id, net, emote));

        await ctx.Channel.SendMessageAsync(embed: eb.Build());
    }

    /// <summary>
    ///     Shows what each game actually paid back against what it took in, so payout rates can be
    ///     tuned from real results instead of guesswork.
    /// </summary>
    /// <param name="days">How many days back to report. Defaults to 7.</param>
    /// <example>.gamestats 30</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task GameStats(int days = 7)
    {
        days = Math.Clamp(days, 1, 365);

        var performance = await AnalyticsService.GetGamePerformanceAsync(ctx.Guild.Id, TimeSpan.FromDays(days));
        var emote = await Service.GetCurrencyEmote(ctx.Guild.Id);

        if (performance.Count == 0)
        {
            await ErrorAsync(Strings.GameStatsEmpty(ctx.Guild.Id, days));
            return;
        }

        var eb = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.GameStatsTitle(ctx.Guild.Id, days))
            .WithDescription(Strings.GameStatsHelp(ctx.Guild.Id))
            .WithFooter(Strings.GameStatsFooter(ctx.Guild.Id, performance.Sum(x => x.HouseTake), emote));

        foreach (var game in performance.Take(20))
        {
            eb.AddField(game.Game,
                Strings.GameStatsEntry(ctx.Guild.Id, game.ActualRtp, game.Wagered, emote, game.Plays, game.Players,
                    game.HouseTake), true);
        }

        await ctx.Channel.SendMessageAsync(embed: eb.Build());
    }

    /// <summary>
    ///     Parses a value, applies it to the guild's configuration and reports what was stored.
    /// </summary>
    private async Task<string> ApplyAsync<T>(string value, Func<string, T> parse, Action<CurrencyConfig, T> assign)
    {
        var parsed = parse(value);
        await ConfigService.UpdateAsync(ctx.Guild.Id, config => assign(config, parsed));

        return parsed switch
        {
            bool b => b ? Strings.EconomyConfigOn(ctx.Guild.Id) : Strings.EconomyConfigOff(ctx.Guild.Id),
            long l => l.ToString("N0", CultureInfo.InvariantCulture),
            double d => d.ToString("0.##", CultureInfo.InvariantCulture),
            _ => parsed?.ToString() ?? string.Empty
        };
    }

    private static long ParseLong(string value)
    {
        return long.TryParse(value.Replace(",", "").Replace("_", ""), NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var parsed)
            ? Math.Max(0, parsed)
            : throw new FormatException();
    }

    private static int ParseInt(string value, int min, int max)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? Math.Clamp(parsed, min, max)
            : throw new FormatException();
    }

    private static double ParseDouble(string value, double min, double max)
    {
        return double.TryParse(value.TrimEnd('%', 'x'), NumberStyles.Float, CultureInfo.InvariantCulture,
            out var parsed)
            ? Math.Clamp(parsed, min, max)
            : throw new FormatException();
    }

    private static bool ParseBool(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "true" or "yes" or "on" or "enable" or "enabled" or "1" => true,
            "false" or "no" or "off" or "disable" or "disabled" or "0" => false,
            _ => throw new FormatException()
        };
    }
}