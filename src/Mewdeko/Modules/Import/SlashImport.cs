using System.Globalization;
using System.Net.Http;
using System.Text;
using Discord.Interactions;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.Import.Common;
using Mewdeko.Modules.Import.Services;
using Mewdeko.Modules.Xp.Models;

namespace Mewdeko.Modules.Import;

/// <summary>
///     Slash commands for moving XP, level role rewards and balances over from other bots.
/// </summary>
[Group("import", "Bring XP and balances over from other bots")]
public class SlashImport(IHttpClientFactory httpFactory) : MewdekoSlashModuleBase<DataImportService>
{
    private const int MaxFileBytes = 25 * 1024 * 1024;

    /// <summary>
    ///     Imports member XP and level role rewards from another bot.
    /// </summary>
    /// <param name="source">The bot the data comes from.</param>
    /// <param name="file">The exported file, for Lurkr, Polaris, Arcane and other files.</param>
    /// <param name="apiKey">The API key, for Amari and Tatsu.</param>
    /// <param name="keepCurve">Switches to the source's level curve when it has one.</param>
    /// <param name="merge">How imported XP combines with XP members already have.</param>
    /// <param name="minimumLevel">Skips members below this level.</param>
    /// <param name="roleRewards">Also imports level role rewards.</param>
    /// <param name="syncRoles">Hands out reward roles to members afterwards.</param>
    [SlashCommand("xp", "Imports member XP and level role rewards from another bot")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task ImportXp(
        [Summary("source", "The bot the data comes from")]
        [Choice("MEE6", (int)ImportSource.Mee6)]
        [Choice("Lurkr", (int)ImportSource.Lurkr)]
        [Choice("Polaris", (int)ImportSource.Polaris)]
        [Choice("Arcane", (int)ImportSource.Arcane)]
        [Choice("Amari", (int)ImportSource.Amari)]
        [Choice("Tatsu", (int)ImportSource.Tatsu)]
        [Choice("Other JSON or CSV file", (int)ImportSource.File)]
        int source,
        [Summary("file", "The exported file, for Lurkr, Polaris, Arcane and other files")]
        IAttachment? file = null,
        [Summary("api-key", "The API key, for Amari and Tatsu")]
        string? apiKey = null,
        [Summary("keep-curve", "Use the source's level curve so levels match exactly")]
        bool keepCurve = true,
        [Summary("merge", "What happens to XP members already have here")]
        ImportMergeMode merge = ImportMergeMode.Replace,
        [Summary("minimum-level", "Skip members below this level")]
        int minimumLevel = 0,
        [Summary("role-rewards", "Also import level role rewards")]
        bool roleRewards = true,
        [Summary("sync-roles", "Hand out reward roles to members afterwards")]
        bool syncRoles = false)
    {
        await DeferAsync(true).ConfigureAwait(false);

        var importSource = (ImportSource)source;
        var job = await StartAsync(importSource, file, apiKey).ConfigureAwait(false);
        if (job?.Data is null)
            return;

        var options = new XpImportOptions
        {
            MergeMode = merge,
            MinimumLevel = Math.Max(0, minimumLevel),
            UseSourceCurve = keepCurve,
            ImportRoleRewards = roleRewards
        };

        var preview = await Service.PreviewAsync(job).ConfigureAwait(false);
        if (preview.Kind != ImportKind.Xp)
        {
            await ErrorAsync(ErrorText(ImportError.WrongKind, importSource)).ConfigureAwait(false);
            return;
        }

        var embed = PreviewEmbed(preview, importSource);
        var curveLine = keepCurve && preview.NativeCurve is { } native
            ? Strings.ImportPreviewCurveSwitch(ctx.Guild.Id, native)
            : job.Data.SourceXpForLevel is not null || job.Data.Members.Any(x => x.Level is not null)
                ? Strings.ImportPreviewCurveConvert(ctx.Guild.Id, preview.CurrentCurve)
                : Strings.ImportPreviewCurveRaw(ctx.Guild.Id, SourceName(importSource), preview.CurrentCurve);

        var details = new StringBuilder()
            .AppendLine(embed.Description)
            .AppendLine()
            .AppendLine(curveLine)
            .AppendLine(Strings.ImportPreviewMerge(ctx.Guild.Id, MergeText(merge)));
        if (options.MinimumLevel > 0)
            details.AppendLine(Strings.ImportPreviewMinLevel(ctx.Guild.Id, options.MinimumLevel));
        details.AppendLine().Append(Strings.ImportPreviewConfirm(ctx.Guild.Id));
        embed.WithDescription(details.ToString());

        if (!await PromptUserConfirmAsync(embed, ctx.User.Id, true, false).ConfigureAwait(false))
        {
            await ErrorAsync(Strings.ImportCancelled(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        try
        {
            var result = await Service.ApplyXpAsync(job, options, syncRoles).ConfigureAwait(false);
            var summary = new StringBuilder().AppendLine(Strings.ImportDoneXp(ctx.Guild.Id, Number(result.Members)));
            if (result.Skipped > 0)
                summary.AppendLine(Strings.ImportDoneSkipped(ctx.Guild.Id, Number(result.Skipped)));
            if (result.RoleRewards > 0)
                summary.AppendLine(Strings.ImportDoneRewards(ctx.Guild.Id, result.RoleRewards));
            if (result.CurveChanged is { } curve)
                summary.AppendLine(Strings.ImportDoneCurve(ctx.Guild.Id, curve));
            if (syncRoles)
                summary.AppendLine(Strings.ImportDoneSync(ctx.Guild.Id));
            summary.Append(Strings.ImportUndoHint(ctx.Guild.Id));

            await ConfirmAsync(summary.ToString()).ConfigureAwait(false);
        }
        catch (ImportException ex)
        {
            await ErrorAsync(ErrorText(ex.Error, importSource)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Imports member wallet and bank balances from UnbelievaBoat.
    /// </summary>
    /// <param name="token">An UnbelievaBoat application token authorized on this server.</param>
    /// <param name="merge">How imported balances combine with balances members already have.</param>
    [SlashCommand("currency", "Imports member balances from UnbelievaBoat")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task ImportCurrency(
        [Summary("token", "An UnbelievaBoat API token authorized on this server")]
        string token,
        [Summary("merge", "What happens to balances members already have here")]
        ImportMergeMode merge = ImportMergeMode.Replace)
    {
        await DeferAsync(true).ConfigureAwait(false);

        var job = await StartAsync(ImportSource.UnbelievaBoat, null, token).ConfigureAwait(false);
        if (job?.Data is null)
            return;

        var preview = await Service.PreviewAsync(job).ConfigureAwait(false);
        var embed = PreviewEmbed(preview, ImportSource.UnbelievaBoat);
        var details = new StringBuilder()
            .AppendLine(embed.Description)
            .AppendLine()
            .AppendLine(Strings.ImportPreviewMerge(ctx.Guild.Id, MergeText(merge)))
            .AppendLine()
            .Append(Strings.ImportPreviewConfirm(ctx.Guild.Id));
        embed.WithDescription(details.ToString());

        if (!await PromptUserConfirmAsync(embed, ctx.User.Id, true, false).ConfigureAwait(false))
        {
            await ErrorAsync(Strings.ImportCancelled(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        try
        {
            var result = await Service.ApplyCurrencyAsync(job, new CurrencyImportOptions { MergeMode = merge })
                .ConfigureAwait(false);
            var summary = new StringBuilder()
                .AppendLine(Strings.ImportDoneCurrency(ctx.Guild.Id, Number(result.Members)))
                .Append(Strings.ImportUndoHint(ctx.Guild.Id));
            await ConfirmAsync(summary.ToString()).ConfigureAwait(false);
        }
        catch (ImportException ex)
        {
            await ErrorAsync(ErrorText(ex.Error, ImportSource.UnbelievaBoat)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Imports MEE6 plugin settings from the file the MEE6 export script downloads.
    /// </summary>
    /// <param name="file">The exported settings file.</param>
    [SlashCommand("mee6-settings", "Brings over MEE6 welcome, levels, commands, role menus, filters and more")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task ImportMee6Settings(
        [Summary("file", "The file the MEE6 export script downloaded")]
        IAttachment file)
    {
        await DeferAsync(true).ConfigureAwait(false);

        var job = await StartAsync(ImportSource.Mee6Settings, file, null).ConfigureAwait(false);
        if (job?.Data is null)
            return;

        var preview = await Service.PreviewAsync(job).ConfigureAwait(false);
        var embed = new EmbedBuilder().WithOkColor()
            .WithTitle(Strings.ImportPreviewTitle(ctx.Guild.Id, SourceName(ImportSource.Mee6)));
        foreach (var section in preview.Sections.Take(25))
        {
            var lines = section.Details.Take(8).ToList();
            if (section.Details.Count > lines.Count)
                lines.Add(Strings.ImportSettingsMore(ctx.Guild.Id, section.Details.Count - lines.Count));
            var value = string.Join("\n", lines);
            embed.AddField(SectionName(section.Key), value.Length > 1024 ? value[..1021] + "..." : value);
        }

        var description = Strings.ImportPreviewConfirm(ctx.Guild.Id);
        embed.WithDescription(description);

        if (!await PromptUserConfirmAsync(embed, ctx.User.Id, true, false).ConfigureAwait(false))
        {
            await ErrorAsync(Strings.ImportCancelled(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        try
        {
            var (_, sections) = await Service.ApplySettingsAsync(job, Mee6Section.All).ConfigureAwait(false);
            var summary = new StringBuilder();
            foreach (var section in sections)
            {
                summary.AppendLine(section.Failed
                    ? Strings.ImportSettingsFailed(ctx.Guild.Id, SectionName(section.Section))
                    : Strings.ImportSettingsWritten(ctx.Guild.Id, SectionName(section.Section), section.Written,
                        section.Skipped));
            }

            summary.Append(Strings.ImportUndoHint(ctx.Guild.Id));
            await ConfirmAsync(summary.ToString()).ConfigureAwait(false);
        }
        catch (ImportException ex)
        {
            await ErrorAsync(ErrorText(ex.Error, ImportSource.Mee6)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Undoes the most recent import made in the last day.
    /// </summary>
    [SlashCommand("undo", "Undoes the most recent import from the last 24 hours")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task Undo()
    {
        await DeferAsync(true).ConfigureAwait(false);

        try
        {
            var undone = await Service.UndoAsync(ctx.Guild.Id, null).ConfigureAwait(false);
            await ConfirmAsync(Strings.ImportUndone(ctx.Guild.Id, SourceName(undone.Source),
                    TimestampTag.FromDateTime(DateTime.SpecifyKind(undone.DateAdded, DateTimeKind.Utc),
                        TimestampTagStyles.Relative)))
                .ConfigureAwait(false);
        }
        catch (ImportException ex)
        {
            await ErrorAsync(ErrorText(ex.Error, ImportSource.File)).ConfigureAwait(false);
        }
    }

    private async Task<ImportJob?> StartAsync(ImportSource source, IAttachment? file, string? apiKey)
    {
        string? content = null;
        if (DataImportService.NeedsFile(source))
        {
            if (file is null)
            {
                await ErrorAsync(ErrorText(ImportError.FileRequired, source)).ConfigureAwait(false);
                return null;
            }

            if (file.Size > MaxFileBytes)
            {
                await ErrorAsync(Strings.ImportFileTooLarge(ctx.Guild.Id)).ConfigureAwait(false);
                return null;
            }

            using var http = httpFactory.CreateClient();
            content = await http.GetStringAsync(file.Url).ConfigureAwait(false);
        }

        ImportJob job;
        try
        {
            job = Service.Start(ctx.Guild.Id, ctx.User.Id, source, apiKey, content);
        }
        catch (ImportException ex)
        {
            await ErrorAsync(ErrorText(ex.Error, source)).ConfigureAwait(false);
            return null;
        }

        var reading = Strings.ImportReading(ctx.Guild.Id, SourceName(source));
        await ctx.Interaction.FollowupAsync(embed: new EmbedBuilder().WithOkColor().WithDescription(reading).Build(),
            ephemeral: true).ConfigureAwait(false);

        await DataImportService.WaitAsync(job, async count =>
        {
            var progress = Strings.ImportReadingProgress(ctx.Guild.Id, SourceName(source), Number(count));
            await ctx.Interaction.ModifyOriginalResponseAsync(x =>
                x.Embed = new EmbedBuilder().WithOkColor().WithDescription(progress).Build()).ConfigureAwait(false);
        }).ConfigureAwait(false);

        if (job.Status == ImportJobStatus.Failed)
        {
            var error = Enum.TryParse<ImportError>(job.Error, out var parsed) ? parsed : ImportError.SourceFailed;
            await ErrorAsync(ErrorText(error, source)).ConfigureAwait(false);
            return null;
        }

        return job;
    }

    private EmbedBuilder PreviewEmbed(ImportPreview preview, ImportSource source)
    {
        var guildId = ctx.Guild.Id;
        var title = Strings.ImportPreviewTitle(guildId, SourceName(source));
        var description = preview.Kind == ImportKind.Xp
            ? Strings.ImportPreviewMembersXp(guildId, Number(preview.MemberCount), Number(preview.ExistingCount))
            : Strings.ImportPreviewMembersCurrency(guildId, Number(preview.MemberCount),
                Number(preview.ExistingCount));

        var top = string.Join("\n", preview.Top.Select((x, i) =>
        {
            var name = x.Name ?? x.UserId;
            return preview.Kind == ImportKind.Xp
                ? Strings.ImportPreviewTopXp(guildId, i + 1, name, x.Level?.ToString() ?? "?",
                    Number(x.Xp ?? 0))
                : Strings.ImportPreviewTopCurrency(guildId, i + 1, name, Number(x.Cash ?? 0),
                    Number(x.Bank ?? 0));
        }));

        var embed = new EmbedBuilder().WithOkColor().WithTitle(title).WithDescription(description);
        if (top.Length > 0)
            embed.AddField(Strings.ImportPreviewTop(guildId), top);

        if (preview.RoleRewards.Count > 0)
        {
            var rewards = string.Join("\n", preview.RoleRewards.Take(20).Select(x => x.Exists
                ? Strings.ImportPreviewReward(guildId, x.Level, MentionUtils.MentionRole(ulong.Parse(x.RoleId)))
                : Strings.ImportPreviewRewardMissing(guildId, x.Level)));
            embed.AddField(Strings.ImportPreviewRewards(guildId), rewards);
        }

        return embed;
    }

    private string MergeText(ImportMergeMode mode)
    {
        return mode switch
        {
            ImportMergeMode.KeepHigher => Strings.ImportMergeKeepHigher(ctx.Guild.Id),
            ImportMergeMode.Add => Strings.ImportMergeAdd(ctx.Guild.Id),
            _ => Strings.ImportMergeReplace(ctx.Guild.Id)
        };
    }

    private string ErrorText(ImportError error, ImportSource source)
    {
        var guildId = ctx.Guild.Id;
        var name = SourceName(source);
        return error switch
        {
            ImportError.LeaderboardPrivate => Strings.ImportErrorLeaderboardPrivate(guildId),
            ImportError.NotFound => Strings.ImportErrorNotFound(guildId, name),
            ImportError.BadKey => Strings.ImportErrorBadKey(guildId, name),
            ImportError.KeyRequired => Strings.ImportErrorKeyRequired(guildId, name),
            ImportError.FileRequired => Strings.ImportErrorFileRequired(guildId, name),
            ImportError.RateLimited => Strings.ImportErrorRateLimited(guildId, name),
            ImportError.UnreadableFile => Strings.ImportErrorUnreadableFile(guildId),
            ImportError.Empty => Strings.ImportErrorEmpty(guildId),
            ImportError.GlobalCurrency => Strings.ImportErrorGlobalCurrency(guildId),
            ImportError.Busy => Strings.ImportErrorBusy(guildId),
            ImportError.JobMissing => Strings.ImportErrorJobMissing(guildId),
            ImportError.NotReady => Strings.ImportErrorNotReady(guildId),
            ImportError.WrongKind => Strings.ImportErrorWrongKind(guildId),
            ImportError.UndoExpired => Strings.ImportErrorUndoExpired(guildId),
            ImportError.UndoNotLatest => Strings.ImportErrorUndoNotLatest(guildId),
            ImportError.WrongServer => Strings.ImportErrorWrongServer(guildId),
            _ => Strings.ImportErrorSourceFailed(guildId, name)
        };
    }

    private string SectionName(string key)
    {
        var guildId = ctx.Guild.Id;
        return key switch
        {
            Mee6Section.Welcome => Strings.ImportSectionWelcome(guildId),
            Mee6Section.Levels => Strings.ImportSectionLevels(guildId),
            Mee6Section.Birthdays => Strings.ImportSectionBirthdays(guildId),
            Mee6Section.Commands => Strings.ImportSectionCommands(guildId),
            Mee6Section.ReactionRoles => Strings.ImportSectionReactionRoles(guildId),
            Mee6Section.AutoMod => Strings.ImportSectionAutomod(guildId),
            Mee6Section.Twitch => Strings.ImportSectionTwitch(guildId),
            _ => Strings.ImportSectionEconomy(guildId)
        };
    }

    /// <summary>
    ///     The name a source is shown under.
    /// </summary>
    /// <param name="source">The source.</param>
    public static string SourceName(ImportSource source)
    {
        return source switch
        {
            ImportSource.Mee6 or ImportSource.Mee6Settings => "MEE6",
            ImportSource.UnbelievaBoat => "UnbelievaBoat",
            ImportSource.File => "file",
            _ => source.ToString()
        };
    }

    private static string Number(long value)
    {
        return value.ToString("N0", CultureInfo.InvariantCulture);
    }
}
