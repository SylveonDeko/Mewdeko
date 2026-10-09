using System.Net.Http;
using System.Text.Json;
using System.Threading;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using LinqToDB.Data;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Modules.Currency.Services;
using Mewdeko.Modules.Currency.Services.Impl;
using Mewdeko.Modules.Import.Common;
using Mewdeko.Modules.Xp.Models;
using Mewdeko.Modules.Xp.Services;

namespace Mewdeko.Modules.Import.Services;

/// <summary>
///     Moves member XP, level role rewards and balances over from other bots. Data is read into a job first so it
///     can be previewed, then written in one go with a snapshot that lets the import be undone for a day.
/// </summary>
public class DataImportService(
    IDataConnectionFactory dbFactory,
    DiscordShardedClient client,
    IHttpClientFactory httpFactory,
    XpService xpService,
    XpCacheManager xpCache,
    XpRewardManager xpRewards,
    XpRoleSyncService roleSync,
    ICurrencyService currency,
    Mee6SettingsImporter mee6Settings,
    ILogger<DataImportService> logger) : INService
{
    /// <summary>
    ///     How long an import can be undone for.
    /// </summary>
    public static readonly TimeSpan UndoWindow = TimeSpan.FromHours(24);

    private static readonly TimeSpan JobLifetime = TimeSpan.FromHours(1);
    private const int DeleteChunk = 1000;
    private const int PreviewSize = 10;

    private readonly ConcurrentDictionary<string, ImportJob> jobs = new();

    /// <summary>
    ///     Whether the source needs an API key or token.
    /// </summary>
    /// <param name="source">The source.</param>
    public static bool NeedsKey(ImportSource source)
    {
        return source is ImportSource.Amari or ImportSource.Tatsu or ImportSource.UnbelievaBoat;
    }

    /// <summary>
    ///     Whether the source is read from an uploaded file.
    /// </summary>
    /// <param name="source">The source.</param>
    public static bool NeedsFile(ImportSource source)
    {
        return source is ImportSource.Lurkr or ImportSource.Polaris or ImportSource.Arcane or ImportSource.File
            or ImportSource.Mee6Settings;
    }

    /// <summary>
    ///     Starts reading data from a source in the background.
    /// </summary>
    /// <param name="guildId">The server to import into.</param>
    /// <param name="userId">The user starting the import.</param>
    /// <param name="source">Where the data comes from.</param>
    /// <param name="apiKey">The API key or token, for sources that need one.</param>
    /// <param name="file">The file's text, for sources read from a file.</param>
    /// <returns>The job, which moves to <see cref="ImportJobStatus.Ready" /> once the data is read.</returns>
    /// <exception cref="ImportException">Another import is running, or a key or file is missing.</exception>
    public ImportJob Start(ulong guildId, ulong userId, ImportSource source, string? apiKey, string? file)
    {
        PruneJobs();

        if (NeedsKey(source) && string.IsNullOrWhiteSpace(apiKey))
            throw new ImportException(ImportError.KeyRequired);
        if (NeedsFile(source) && string.IsNullOrWhiteSpace(file))
            throw new ImportException(ImportError.FileRequired);
        if (jobs.Values.Any(x =>
                x.GuildId == guildId && x.Status is ImportJobStatus.Fetching or ImportJobStatus.Applying))
            throw new ImportException(ImportError.Busy);

        var job = new ImportJob
        {
            GuildId = guildId, UserId = userId, Source = source, Status = ImportJobStatus.Fetching
        };
        jobs[job.Id] = job;

        _ = Task.Run(async () =>
        {
            try
            {
                using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(30));
                var data = await ReadAsync(guildId, source, apiKey, file, x => job.Progress = x, cancellation.Token);
                if (data.Mee6Settings is { } settings && settings.GuildId != guildId)
                    throw new ImportException(ImportError.WrongServer);
                if (data.Members.Count == 0 && data.Mee6Settings?.AvailableSections().Any() != true)
                    throw new ImportException(ImportError.Empty);

                job.Data = data;
                job.Progress = data.Members.Count;
                job.Status = ImportJobStatus.Ready;
            }
            catch (ImportException ex)
            {
                job.Error = ex.Error.ToString();
                job.Status = ImportJobStatus.Failed;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Reading {Source} import for guild {GuildId} failed", source, guildId);
                job.Error = nameof(ImportError.SourceFailed);
                job.Status = ImportJobStatus.Failed;
            }
        });

        return job;
    }

    /// <summary>
    ///     Gets a job started on a server.
    /// </summary>
    /// <param name="guildId">The server.</param>
    /// <param name="jobId">The job's ID.</param>
    /// <returns>The job, or null when it has expired or belongs to another server.</returns>
    public ImportJob? GetJob(ulong guildId, string jobId)
    {
        return jobs.TryGetValue(jobId, out var job) && job.GuildId == guildId ? job : null;
    }

    /// <summary>
    ///     Waits for a job to finish reading.
    /// </summary>
    /// <param name="job">The job.</param>
    /// <param name="progress">Called every few seconds while reading, with the members read so far.</param>
    public static async Task WaitAsync(ImportJob job, Func<int, Task>? progress = null)
    {
        var lastReport = DateTime.UtcNow;
        while (job.Status == ImportJobStatus.Fetching)
        {
            await Task.Delay(500);
            if (progress is null || DateTime.UtcNow - lastReport < TimeSpan.FromSeconds(5))
                continue;
            lastReport = DateTime.UtcNow;
            await progress(job.Progress);
        }
    }

    /// <summary>
    ///     Summarizes what a job would write.
    /// </summary>
    /// <param name="job">The job.</param>
    public async Task<ImportPreview> PreviewAsync(ImportJob job)
    {
        var settings = await xpService.GetGuildXpSettingsAsync(job.GuildId);
        var data = job.Data;

        if (data is null)
        {
            return new ImportPreview
            {
                JobId = job.Id,
                Status = job.Status,
                Progress = job.Progress,
                Error = job.Error,
                Source = job.Source,
                CurrentCurve = (XpCurveType)settings.XpCurveType,
                ImportId = job.ImportId
            };
        }

        if (data.Mee6Settings is { } plan)
        {
            return new ImportPreview
            {
                JobId = job.Id,
                Status = job.Status,
                Progress = job.Progress,
                Error = job.Error,
                Source = job.Source,
                Kind = ImportKind.Settings,
                CurrentCurve = (XpCurveType)settings.XpCurveType,
                ImportId = job.ImportId,
                Sections = mee6Settings.Describe(job.GuildId, plan)
            };
        }

        await using var db = await dbFactory.CreateConnectionAsync();
        var existing = data.Kind == ImportKind.Xp
            ? await db.GuildUserXps.Where(x => x.GuildId == job.GuildId && x.TotalXp > 0).Select(x => x.UserId)
                .ToListAsync()
            : await db.GuildUserBalances.Where(x => x.GuildId == job.GuildId).Select(x => x.UserId).ToListAsync();
        var existingSet = existing.ToHashSet();

        var guild = client.GetGuild(job.GuildId);
        var top = (data.Kind == ImportKind.Xp
                ? data.Members.OrderByDescending(x => x.Xp ?? 0).ThenByDescending(x => x.Level ?? 0)
                : data.Members.OrderByDescending(x => (x.Cash ?? 0) + (x.Bank ?? 0)))
            .Take(PreviewSize)
            .Select(x =>
            {
                var member = guild?.GetUser(x.UserId);
                return new ImportPreviewMember
                {
                    UserId = x.UserId.ToString(),
                    Name = member?.DisplayName ?? x.Name,
                    AvatarUrl = member?.GetDisplayAvatarUrl(),
                    Xp = x.Xp,
                    Level = x.Level ?? (x.Xp is { } xp && data.SourceXpForLevel is { } curve
                        ? LevelFor(xp, curve)
                        : null),
                    Cash = x.Cash,
                    Bank = x.Bank
                };
            })
            .ToList();

        var rewards = data.RoleRewards.OrderBy(x => x.Level).Select(x =>
        {
            var role = guild?.GetRole(x.RoleId);
            return new ImportPreviewReward
            {
                Level = x.Level, RoleId = x.RoleId.ToString(), RoleName = role?.Name, Exists = role is not null
            };
        }).ToList();

        return new ImportPreview
        {
            JobId = job.Id,
            Status = job.Status,
            Progress = job.Progress,
            Error = job.Error,
            Source = job.Source,
            Kind = data.Kind,
            MemberCount = data.Members.Count,
            ExistingCount = data.Members.Count(x => existingSet.Contains(x.UserId)),
            Top = top,
            RoleRewards = rewards,
            NativeCurve = data.NativeCurve,
            CurrentCurve = (XpCurveType)settings.XpCurveType,
            ImportId = job.ImportId
        };
    }

    /// <summary>
    ///     Writes a job's XP data.
    /// </summary>
    /// <param name="job">A job whose data is ready.</param>
    /// <param name="options">How to write it.</param>
    /// <param name="syncRoles">Hands out level reward roles to imported members afterwards.</param>
    /// <exception cref="ImportException">The job is not ready or holds currency data.</exception>
    public async Task<ImportResult> ApplyXpAsync(ImportJob job, XpImportOptions options, bool syncRoles)
    {
        var data = TakeReady(job, ImportKind.Xp);

        try
        {
            var guildId = job.GuildId;
            var settings = await xpService.GetGuildXpSettingsAsync(guildId);
            var previousCurve = (XpCurveType)settings.XpCurveType;
            var useNative = options.UseSourceCurve && data.NativeCurve is not null;
            var targetCurve = useNative ? data.NativeCurve!.Value : previousCurve;

            await using var db = await dbFactory.CreateConnectionAsync();
            var current = (await db.GuildUserXps.Where(x => x.GuildId == guildId).ToListAsync())
                .ToDictionary(x => x.UserId);

            var now = DateTime.UtcNow;
            var rows = new List<GuildUserXp>();
            var snapshot = new List<SnapshotRow>();
            var skipped = 0;

            foreach (var member in data.Members)
            {
                if (SourceLevel(member, data, targetCurve) < options.MinimumLevel)
                {
                    skipped++;
                    continue;
                }

                var imported = ConvertXp(member, data, targetCurve, useNative);
                if (imported <= 0)
                {
                    skipped++;
                    continue;
                }

                current.TryGetValue(member.UserId, out var existing);
                var total = Merge(existing?.TotalXp ?? 0, imported, options.MergeMode);

                snapshot.Add(new SnapshotRow(member.UserId, existing is not null, existing?.TotalXp ?? 0, 0));
                rows.Add(new GuildUserXp
                {
                    GuildId = guildId,
                    UserId = member.UserId,
                    TotalXp = total,
                    BonusXp = existing?.BonusXp ?? 0,
                    LastActivity = existing?.LastActivity ?? now,
                    NotifyType = existing?.NotifyType ?? (int)XpNotificationType.Channel,
                    LastLevelUp = existing?.LastLevelUp ?? now,
                    Id = existing?.Id ?? 0,
                    DateAdded = existing?.DateAdded ?? now
                });
            }

            var rewardSnapshot = new List<SnapshotReward>();
            var rewardsWritten = 0;
            if (options.ImportRoleRewards && data.RoleRewards.Count > 0)
            {
                var guild = client.GetGuild(guildId);
                var currentRewards = (await xpService.GetRoleRewardsAsync(guildId)).ToDictionary(x => x.Level);
                foreach (var reward in data.RoleRewards.Where(x => guild?.GetRole(x.RoleId) is not null))
                {
                    currentRewards.TryGetValue(reward.Level, out var before);
                    rewardSnapshot.Add(new SnapshotReward(reward.Level, before?.RoleId));
                }
            }

            var record = new DataImport
            {
                GuildId = guildId,
                UserId = job.UserId,
                Source = (int)job.Source,
                Kind = (int)ImportKind.Xp,
                MemberCount = rows.Count,
                RoleRewardCount = rewardSnapshot.Count,
                Snapshot = JsonSerializer.Serialize(new ImportSnapshot
                {
                    PreviousCurve = useNative && targetCurve != previousCurve ? (int)previousCurve : null,
                    Rows = snapshot,
                    Rewards = rewardSnapshot
                }),
                DateAdded = now
            };

            await using (var transaction = await db.BeginTransactionAsync())
            {
                await DeleteXpRowsAsync(db, guildId, rows.Select(x => x.UserId).ToList());
                if (rows.Count > 0)
                    await db.BulkCopyAsync(rows);
                record.Id = await db.InsertWithInt32IdentityAsync(record);
                await transaction.CommitAsync();
            }

            await PurgeOldSnapshotsAsync(db);

            if (useNative && targetCurve != previousCurve)
                await xpService.UpdateGuildXpSettingsAsync(guildId, x => x.XpCurveType = (int)targetCurve);

            foreach (var reward in data.RoleRewards.Where(x => rewardSnapshot.Any(r => r.Level == x.Level)))
            {
                await xpRewards.SetRoleRewardAsync(guildId, reward.Level, reward.RoleId);
                rewardsWritten++;
            }

            await xpCache.ClearGuildXpCachesAsync(guildId);

            job.ImportId = record.Id;
            job.Status = ImportJobStatus.Applied;

            if (syncRoles)
                StartRoleSync(guildId);

            logger.LogInformation("Imported {Count} members from {Source} into guild {GuildId}", rows.Count,
                job.Source, guildId);

            return new ImportResult(record.Id, rows.Count, skipped, rewardsWritten,
                useNative && targetCurve != previousCurve ? targetCurve : null);
        }
        catch
        {
            job.Status = ImportJobStatus.Ready;
            throw;
        }
    }

    /// <summary>
    ///     Writes the chosen sections of a job's MEE6 settings.
    /// </summary>
    /// <param name="job">A job whose settings are ready.</param>
    /// <param name="sections">Section keys from <see cref="Mee6Section" />.</param>
    /// <returns>The import record and what each section wrote.</returns>
    /// <exception cref="ImportException">The job is not ready or holds member data.</exception>
    public async Task<(int ImportId, List<Mee6SectionResult> Sections)> ApplySettingsAsync(ImportJob job,
        IReadOnlyCollection<string> sections)
    {
        var data = TakeReady(job, ImportKind.Settings);
        try
        {
            var (results, undo) = await mee6Settings.ApplyAsync(job.GuildId, data.Mee6Settings!, sections);

            await using var db = await dbFactory.CreateConnectionAsync();
            var id = await db.InsertWithInt32IdentityAsync(new DataImport
            {
                GuildId = job.GuildId,
                UserId = job.UserId,
                Source = (int)job.Source,
                Kind = (int)ImportKind.Settings,
                MemberCount = results.Sum(x => x.Written),
                Snapshot = undo,
                DateAdded = DateTime.UtcNow
            });
            await PurgeOldSnapshotsAsync(db);

            job.ImportId = id;
            job.Status = ImportJobStatus.Applied;
            return (id, results);
        }
        catch
        {
            job.Status = ImportJobStatus.Ready;
            throw;
        }
    }

    /// <summary>
    ///     Writes a job's balance data.
    /// </summary>
    /// <param name="job">A job whose data is ready.</param>
    /// <param name="options">How to write it.</param>
    /// <exception cref="ImportException">The job is not ready, holds XP data, or the bot uses one global currency.</exception>
    public async Task<ImportResult> ApplyCurrencyAsync(ImportJob job, CurrencyImportOptions options)
    {
        if (currency is not GuildCurrencyService)
            throw new ImportException(ImportError.GlobalCurrency);

        var data = TakeReady(job, ImportKind.Currency);

        try
        {
            var guildId = job.GuildId;
            await using var db = await dbFactory.CreateConnectionAsync();
            var current = (await db.GuildUserBalances.Where(x => x.GuildId == guildId).ToListAsync())
                .GroupBy(x => x.UserId)
                .ToDictionary(x => x.Key, x => x.First());

            var now = DateTime.UtcNow;
            var rows = new List<GuildUserBalance>();
            var snapshot = new List<SnapshotRow>();

            foreach (var member in data.Members)
            {
                if (member.Cash is null && member.Bank is null)
                    continue;

                current.TryGetValue(member.UserId, out var existing);
                snapshot.Add(new SnapshotRow(member.UserId, existing is not null, existing?.Balance ?? 0,
                    existing?.Bank ?? 0));
                rows.Add(new GuildUserBalance
                {
                    GuildId = guildId,
                    UserId = member.UserId,
                    Balance = Math.Max(0, Merge(existing?.Balance ?? 0, member.Cash ?? 0, options.MergeMode)),
                    Bank = Math.Max(0, Merge(existing?.Bank ?? 0, member.Bank ?? 0, options.MergeMode)),
                    DateAdded = existing?.DateAdded ?? now
                });
            }

            var record = new DataImport
            {
                GuildId = guildId,
                UserId = job.UserId,
                Source = (int)job.Source,
                Kind = (int)ImportKind.Currency,
                MemberCount = rows.Count,
                Snapshot = JsonSerializer.Serialize(new ImportSnapshot { Rows = snapshot }),
                DateAdded = now
            };

            await using (var transaction = await db.BeginTransactionAsync())
            {
                await DeleteBalanceRowsAsync(db, guildId, rows.Select(x => x.UserId).ToList());
                if (rows.Count > 0)
                    await db.BulkCopyAsync(rows);
                record.Id = await db.InsertWithInt32IdentityAsync(record);
                await transaction.CommitAsync();
            }

            await PurgeOldSnapshotsAsync(db);

            job.ImportId = record.Id;
            job.Status = ImportJobStatus.Applied;

            logger.LogInformation("Imported {Count} balances from {Source} into guild {GuildId}", rows.Count,
                job.Source, guildId);

            return new ImportResult(record.Id, rows.Count, data.Members.Count - rows.Count, 0, null);
        }
        catch
        {
            job.Status = ImportJobStatus.Ready;
            throw;
        }
    }

    /// <summary>
    ///     Lists a server's imports, newest first.
    /// </summary>
    /// <param name="guildId">The server.</param>
    public async Task<List<ImportHistoryEntry>> GetHistoryAsync(ulong guildId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var imports = await db.DataImports
            .Where(x => x.GuildId == guildId)
            .OrderByDescending(x => x.DateAdded)
            .Take(25)
            .Select(x => new
            {
                x.Id, x.Source, x.Kind, x.MemberCount, x.RoleRewardCount, x.UserId, x.DateAdded, x.UndoneAt,
                HasSnapshot = x.Snapshot != ""
            })
            .ToListAsync();

        var latestByKind = imports.Where(x => x.UndoneAt is null).GroupBy(x => x.Kind)
            .ToDictionary(x => x.Key, x => x.First().Id);

        return imports.Select(x => new ImportHistoryEntry
        {
            Id = x.Id,
            Source = (ImportSource)x.Source,
            Kind = (ImportKind)x.Kind,
            MemberCount = x.MemberCount,
            RoleRewardCount = x.RoleRewardCount,
            UserId = x.UserId.ToString(),
            DateAdded = x.DateAdded,
            UndoneAt = x.UndoneAt,
            CanUndo = x.UndoneAt is null && x.HasSnapshot && DateTime.UtcNow - x.DateAdded < UndoWindow &&
                      latestByKind.GetValueOrDefault(x.Kind) == x.Id
        }).ToList();
    }

    /// <summary>
    ///     Puts back everything an import replaced.
    /// </summary>
    /// <param name="guildId">The server.</param>
    /// <param name="importId">The import record's ID, or null for the most recent import.</param>
    /// <returns>The import that was undone.</returns>
    /// <exception cref="ImportException">There is nothing to undo, the window has passed, or a newer import exists.</exception>
    public async Task<ImportHistoryEntry> UndoAsync(ulong guildId, int? importId)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var record = importId is { } id
            ? await db.DataImports.FirstOrDefaultAsync(x => x.Id == id && x.GuildId == guildId)
            : await db.DataImports.Where(x => x.GuildId == guildId && x.UndoneAt == null)
                .OrderByDescending(x => x.DateAdded).FirstOrDefaultAsync();

        if (record is null)
            throw new ImportException(ImportError.JobMissing);
        if (record.UndoneAt is not null || record.Snapshot.Length == 0 ||
            DateTime.UtcNow - record.DateAdded >= UndoWindow)
            throw new ImportException(ImportError.UndoExpired);

        var newer = await db.DataImports.AnyAsync(x =>
            x.GuildId == guildId && x.Kind == record.Kind && x.UndoneAt == null && x.Id > record.Id);
        if (newer)
            throw new ImportException(ImportError.UndoNotLatest);

        if (record.Kind == (int)ImportKind.Settings)
        {
            await mee6Settings.UndoAsync(guildId, record.Snapshot);
            await db.DataImports.Where(x => x.Id == record.Id)
                .Set(x => x.UndoneAt, DateTime.UtcNow)
                .Set(x => x.Snapshot, "")
                .UpdateAsync();
            return new ImportHistoryEntry
            {
                Id = record.Id,
                Source = (ImportSource)record.Source,
                Kind = ImportKind.Settings,
                MemberCount = record.MemberCount,
                UserId = record.UserId.ToString(),
                DateAdded = record.DateAdded,
                UndoneAt = DateTime.UtcNow
            };
        }

        var snapshot = JsonSerializer.Deserialize<ImportSnapshot>(record.Snapshot) ?? new ImportSnapshot();
        var userIds = snapshot.Rows.Select(x => x.U).ToList();

        await using (var transaction = await db.BeginTransactionAsync())
        {
            if (record.Kind == (int)ImportKind.Xp)
            {
                var current = (await db.GuildUserXps.Where(x => x.GuildId == guildId).ToListAsync())
                    .ToDictionary(x => x.UserId);
                var restored = snapshot.Rows.Where(x => x.E && current.ContainsKey(x.U)).Select(x =>
                {
                    var row = current[x.U];
                    row.TotalXp = x.A;
                    return row;
                }).ToList();

                await DeleteXpRowsAsync(db, guildId, userIds);
                if (restored.Count > 0)
                    await db.BulkCopyAsync(
                        restored);
            }
            else
            {
                var current = (await db.GuildUserBalances.Where(x => x.GuildId == guildId).ToListAsync())
                    .GroupBy(x => x.UserId)
                    .ToDictionary(x => x.Key, x => x.First());
                var restored = snapshot.Rows.Where(x => x.E).Select(x => new GuildUserBalance
                {
                    GuildId = guildId,
                    UserId = x.U,
                    Balance = x.A,
                    Bank = x.B,
                    DateAdded = current.GetValueOrDefault(x.U)?.DateAdded ?? DateTime.UtcNow
                }).ToList();

                await DeleteBalanceRowsAsync(db, guildId, userIds);
                if (restored.Count > 0)
                    await db.BulkCopyAsync(
                        restored);
            }

            await db.DataImports.Where(x => x.Id == record.Id)
                .Set(x => x.UndoneAt, DateTime.UtcNow)
                .Set(x => x.Snapshot, "")
                .UpdateAsync();

            await transaction.CommitAsync();
        }

        if (record.Kind == (int)ImportKind.Xp)
        {
            if (snapshot.PreviousCurve is { } curve)
                await xpService.UpdateGuildXpSettingsAsync(guildId, x => x.XpCurveType = curve);

            foreach (var reward in snapshot.Rewards)
                await xpRewards.SetRoleRewardAsync(guildId, reward.Level, reward.RoleId);

            await xpCache.ClearGuildXpCachesAsync(guildId);
        }

        return new ImportHistoryEntry
        {
            Id = record.Id,
            Source = (ImportSource)record.Source,
            Kind = (ImportKind)record.Kind,
            MemberCount = record.MemberCount,
            RoleRewardCount = record.RoleRewardCount,
            UserId = record.UserId.ToString(),
            DateAdded = record.DateAdded,
            UndoneAt = DateTime.UtcNow
        };
    }

    /// <summary>
    ///     Converts a member's source XP into XP on the server's curve. With the source's own curve the XP is kept as
    ///     is. Otherwise the member keeps their level and how far through it they were.
    /// </summary>
    /// <param name="member">The member's source data.</param>
    /// <param name="data">The data set, for the source's curve.</param>
    /// <param name="targetCurve">The curve the server will use.</param>
    /// <param name="useNative">Whether the server is switching to the source's own curve.</param>
    public static long ConvertXp(ImportedMember member, ImportDataset data, XpCurveType targetCurve, bool useNative)
    {
        if (useNative)
            return member.Xp ?? (member.Level is { } nativeLevel
                ? XpCalculator.CalculateXpForLevel(nativeLevel, targetCurve)
                : 0);

        var sourceCurve = data.SourceXpForLevel;
        var level = member.Level ?? (member.Xp is { } sourceXp && sourceCurve is not null
            ? LevelFor(sourceXp, sourceCurve)
            : null);

        if (level is null)
            return member.Xp ?? 0;

        var fraction = 0d;
        if (sourceCurve is not null && member.Xp is { } xp)
        {
            var low = sourceCurve(level.Value);
            var high = sourceCurve(level.Value + 1);
            if (high > low && xp >= low)
                fraction = Math.Clamp((double)(xp - low) / (high - low), 0, 0.999);
        }

        var targetLow = MinXpForLevel(level.Value, targetCurve);
        var targetHigh = MinXpForLevel(level.Value + 1, targetCurve);
        return targetLow + (long)((targetHigh - targetLow) * fraction);
    }

    /// <summary>
    ///     The smallest total XP that the curve places at <paramref name="level" /> or above. Found by searching the
    ///     curve's own level function, so rounding inside a curve can never leave a member a level short.
    /// </summary>
    /// <param name="level">The level.</param>
    /// <param name="curve">The curve.</param>
    public static long MinXpForLevel(int level, XpCurveType curve)
    {
        if (level <= 0)
            return 0;

        long low = 0;
        var high = Math.Max(XpCalculator.CalculateXpForLevel(level + 1, curve), 1) * 2;
        while (XpCalculator.CalculateLevel(high, curve) < level && high < long.MaxValue / 4)
            high *= 2;

        while (low < high)
        {
            var mid = low + (high - low) / 2;
            if (XpCalculator.CalculateLevel(mid, curve) >= level)
                high = mid;
            else
                low = mid + 1;
        }

        return low;
    }

    /// <summary>
    ///     The level reached with <paramref name="xp" /> on a curve given as total XP per level.
    /// </summary>
    /// <param name="xp">Total XP.</param>
    /// <param name="xpForLevel">Total XP needed for each level.</param>
    public static int LevelFor(long xp, Func<int, long> xpForLevel)
    {
        var level = 0;
        while (level < 10000 && xpForLevel(level + 1) <= xp)
            level++;
        return level;
    }

    private static int SourceLevel(ImportedMember member, ImportDataset data, XpCurveType targetCurve)
    {
        if (member.Level is { } level)
            return level;
        if (member.Xp is not { } xp)
            return 0;
        return data.SourceXpForLevel is { } curve ? LevelFor(xp, curve) : XpCalculator.CalculateLevel(xp, targetCurve);
    }

    private static long Merge(long existing, long imported, ImportMergeMode mode)
    {
        return mode switch
        {
            ImportMergeMode.KeepHigher => Math.Max(existing, imported),
            ImportMergeMode.Add => existing + imported,
            _ => imported
        };
    }

    private static ImportDataset TakeReady(ImportJob job, ImportKind kind)
    {
        lock (job)
        {
            if (job.Status != ImportJobStatus.Ready || job.Data is null)
                throw new ImportException(ImportError.NotReady);
            if (job.Data.Kind != kind)
                throw new ImportException(ImportError.WrongKind);
            job.Status = ImportJobStatus.Applying;
            return job.Data;
        }
    }

    private async Task<ImportDataset> ReadAsync(ulong guildId, ImportSource source, string? apiKey, string? file,
        Action<int> progress, CancellationToken token)
    {
        if (NeedsFile(source) && Mee6SettingsReader.IsExport(file!))
            return new ImportDataset { Kind = ImportKind.Settings, Mee6Settings = Mee6SettingsReader.Read(file!) };
        if (source == ImportSource.Mee6Settings)
            throw new ImportException(ImportError.UnreadableFile);
        if (NeedsFile(source))
            return ImportFileReader.Read(source, file!);

        var reader = new ImportApiReader(httpFactory.CreateClient());
        return source switch
        {
            ImportSource.Mee6 => await reader.ReadMee6Async(guildId, progress, token),
            ImportSource.Amari => await reader.ReadAmariAsync(guildId, apiKey!, progress, token),
            ImportSource.Tatsu => await reader.ReadTatsuAsync(guildId, apiKey!, progress, token),
            ImportSource.UnbelievaBoat => await reader.ReadUnbelievaBoatAsync(guildId, apiKey!, progress, token),
            _ => throw new ImportException(ImportError.SourceFailed)
        };
    }

    private void StartRoleSync(ulong guildId)
    {
        var guild = client.GetGuild(guildId);
        if (guild is null)
            return;

        _ = Task.Run(async () =>
        {
            try
            {
                await roleSync.SyncAllUsersAsync(guild);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Role sync after import failed for guild {GuildId}", guildId);
            }
        });
    }

    private static async Task DeleteXpRowsAsync(MewdekoDb db, ulong guildId, List<ulong> userIds)
    {
        foreach (var chunk in userIds.Chunk(DeleteChunk))
            await db.GuildUserXps.Where(x => x.GuildId == guildId && chunk.Contains(x.UserId)).DeleteAsync();
    }

    private static async Task DeleteBalanceRowsAsync(MewdekoDb db, ulong guildId, List<ulong> userIds)
    {
        foreach (var chunk in userIds.Chunk(DeleteChunk))
            await db.GuildUserBalances.Where(x => x.GuildId == guildId && chunk.Contains(x.UserId)).DeleteAsync();
    }

    private static async Task PurgeOldSnapshotsAsync(MewdekoDb db)
    {
        var cutoff = DateTime.UtcNow - UndoWindow;
        await db.DataImports.Where(x => x.DateAdded < cutoff && x.Snapshot != "")
            .Set(x => x.Snapshot, "")
            .UpdateAsync();
    }

    private void PruneJobs()
    {
        foreach (var job in jobs.Values.Where(x => DateTime.UtcNow - x.CreatedAt > JobLifetime).ToList())
            jobs.TryRemove(job.Id, out _);
    }

    /// <summary>
    ///     What an import replaced, kept so it can be put back.
    /// </summary>
    private sealed class ImportSnapshot
    {
        public int? PreviousCurve { get; set; }
        public List<SnapshotRow> Rows { get; set; } = [];
        public List<SnapshotReward> Rewards { get; set; } = [];
    }

    /// <summary>
    ///     One member's values before an import: whether they had a row, then XP or wallet, then bank.
    /// </summary>
    private sealed record SnapshotRow(ulong U, bool E, long A, long B);

    /// <summary>
    ///     A level's reward role before an import, or null when the level had none.
    /// </summary>
    private sealed record SnapshotReward(int Level, ulong? RoleId);
}
