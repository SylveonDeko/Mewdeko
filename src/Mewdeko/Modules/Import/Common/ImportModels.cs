using System.Text.Json.Serialization;
using Mewdeko.Modules.Xp.Models;

namespace Mewdeko.Modules.Import.Common;

/// <summary>
///     The bots and file formats data can be imported from.
/// </summary>
public enum ImportSource
{
    /// <summary>
    ///     MEE6, read from its public leaderboard.
    /// </summary>
    Mee6 = 0,

    /// <summary>
    ///     Lurkr, read from the JSON file its export command produces.
    /// </summary>
    Lurkr = 1,

    /// <summary>
    ///     Polaris, read from any of its JSON or CSV exports.
    /// </summary>
    Polaris = 2,

    /// <summary>
    ///     Arcane, read from the JSON the community leaderboard export script produces.
    /// </summary>
    Arcane = 3,

    /// <summary>
    ///     Amari, read from its API with a key the server owner applies for.
    /// </summary>
    Amari = 4,

    /// <summary>
    ///     Tatsu, read from its API with a key made by its apikey command.
    /// </summary>
    Tatsu = 5,

    /// <summary>
    ///     Any JSON or CSV file with a user ID column and an XP or level column.
    /// </summary>
    File = 6,

    /// <summary>
    ///     UnbelievaBoat economy balances, read from its API with an application token.
    /// </summary>
    UnbelievaBoat = 7,

    /// <summary>
    ///     MEE6 plugin settings, read from the file the MEE6 export script downloads.
    /// </summary>
    Mee6Settings = 8
}

/// <summary>
///     What an import writes to.
/// </summary>
public enum ImportKind
{
    /// <summary>
    ///     Member XP and level role rewards.
    /// </summary>
    Xp = 0,

    /// <summary>
    ///     Member wallet and bank balances.
    /// </summary>
    Currency = 1,

    /// <summary>
    ///     Another bot's feature settings.
    /// </summary>
    Settings = 2
}

/// <summary>
///     How imported values combine with what members already have.
/// </summary>
public enum ImportMergeMode
{
    /// <summary>
    ///     Imported values replace existing values for every imported member.
    /// </summary>
    Replace = 0,

    /// <summary>
    ///     Each member keeps whichever is higher, their current value or the imported one.
    /// </summary>
    KeepHigher = 1,

    /// <summary>
    ///     Imported values are added on top of existing values.
    /// </summary>
    Add = 2
}

/// <summary>
///     The state of an import job.
/// </summary>
public enum ImportJobStatus
{
    /// <summary>
    ///     Data is still being downloaded or read.
    /// </summary>
    Fetching = 0,

    /// <summary>
    ///     Data was read and is waiting for confirmation.
    /// </summary>
    Ready = 1,

    /// <summary>
    ///     Reading the data failed; see the error.
    /// </summary>
    Failed = 2,

    /// <summary>
    ///     The data is being written.
    /// </summary>
    Applying = 3,

    /// <summary>
    ///     The data was written.
    /// </summary>
    Applied = 4
}

/// <summary>
///     One member's data as read from the source.
/// </summary>
/// <param name="UserId">The member's Discord ID.</param>
/// <param name="Xp">Total XP in the source, when it has one.</param>
/// <param name="Level">Level in the source, when it has one.</param>
/// <param name="Messages">Messages the source counted, when it has a count.</param>
/// <param name="Cash">Wallet balance, for currency imports.</param>
/// <param name="Bank">Bank balance, for currency imports.</param>
/// <param name="Name">The member's name in the source, for previews.</param>
public sealed record ImportedMember(
    ulong UserId,
    long? Xp,
    int? Level,
    long? Messages,
    long? Cash,
    long? Bank,
    string? Name);

/// <summary>
///     A level role reward read from the source.
/// </summary>
/// <param name="Level">The level the role is given at.</param>
/// <param name="RoleId">The role's ID.</param>
public sealed record ImportedRoleReward(int Level, ulong RoleId);

/// <summary>
///     Everything read from a source, before it is written.
/// </summary>
public sealed class ImportDataset
{
    /// <summary>
    ///     What the data is for.
    /// </summary>
    public ImportKind Kind { get; init; }

    /// <summary>
    ///     The members read, one entry per user.
    /// </summary>
    public List<ImportedMember> Members { get; init; } = [];

    /// <summary>
    ///     Level role rewards read, if the source has any.
    /// </summary>
    public List<ImportedRoleReward> RoleRewards { get; init; } = [];

    /// <summary>
    ///     The Mewdeko curve that matches the source exactly, when there is one.
    /// </summary>
    public XpCurveType? NativeCurve { get; init; }

    /// <summary>
    ///     Total XP the source needs for a level, when its curve is known, so progress inside a level survives a curve
    ///     change.
    /// </summary>
    [JsonIgnore]
    public Func<int, long>? SourceXpForLevel { get; init; }

    /// <summary>
    ///     MEE6 plugin settings, for settings imports.
    /// </summary>
    [JsonIgnore]
    public Mee6SettingsPlan? Mee6Settings { get; init; }
}

/// <summary>
///     Choices for how an XP import is written.
/// </summary>
public sealed class XpImportOptions
{
    /// <summary>
    ///     How imported XP combines with current XP.
    /// </summary>
    public ImportMergeMode MergeMode { get; set; } = ImportMergeMode.Replace;

    /// <summary>
    ///     Members below this level in the source are skipped.
    /// </summary>
    public int MinimumLevel { get; set; }

    /// <summary>
    ///     Switches the server to the source's curve so XP and levels stay identical. Ignored when the source has no
    ///     matching curve.
    /// </summary>
    public bool UseSourceCurve { get; set; } = true;

    /// <summary>
    ///     Also imports the source's level role rewards.
    /// </summary>
    public bool ImportRoleRewards { get; set; } = true;
}

/// <summary>
///     Choices for how a currency import is written.
/// </summary>
public sealed class CurrencyImportOptions
{
    /// <summary>
    ///     How imported balances combine with current balances.
    /// </summary>
    public ImportMergeMode MergeMode { get; set; } = ImportMergeMode.Replace;
}

/// <summary>
///     An import in progress, from fetching through to being written.
/// </summary>
public sealed class ImportJob
{
    /// <summary>
    ///     The job's ID.
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    ///     The server being imported into.
    /// </summary>
    public ulong GuildId { get; init; }

    /// <summary>
    ///     The user who started the import.
    /// </summary>
    public ulong UserId { get; init; }

    /// <summary>
    ///     Where the data comes from.
    /// </summary>
    public ImportSource Source { get; init; }

    /// <summary>
    ///     Where the job is up to.
    /// </summary>
    public ImportJobStatus Status { get; set; }

    /// <summary>
    ///     Members read so far, for progress while fetching.
    /// </summary>
    public int Progress { get; set; }

    /// <summary>
    ///     Why the job failed.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    ///     The data, once read.
    /// </summary>
    [JsonIgnore]
    public ImportDataset? Data { get; set; }

    /// <summary>
    ///     The import record, once written.
    /// </summary>
    public int? ImportId { get; set; }

    /// <summary>
    ///     When the job started.
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
///     A summary of what an import would write, shown before confirming.
/// </summary>
public sealed class ImportPreview
{
    /// <summary>
    ///     The job this preview belongs to.
    /// </summary>
    public string JobId { get; init; } = "";

    /// <summary>
    ///     Where the job is up to.
    /// </summary>
    public ImportJobStatus Status { get; init; }

    /// <summary>
    ///     Members read so far.
    /// </summary>
    public int Progress { get; init; }

    /// <summary>
    ///     Why the job failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    ///     Where the data comes from.
    /// </summary>
    public ImportSource Source { get; init; }

    /// <summary>
    ///     What the data is for.
    /// </summary>
    public ImportKind Kind { get; init; }

    /// <summary>
    ///     Members in the data.
    /// </summary>
    public int MemberCount { get; init; }

    /// <summary>
    ///     Members who already have data on this server.
    /// </summary>
    public int ExistingCount { get; init; }

    /// <summary>
    ///     The highest ranked members in the data.
    /// </summary>
    public List<ImportPreviewMember> Top { get; init; } = [];

    /// <summary>
    ///     Role rewards in the data, with whether each role still exists.
    /// </summary>
    public List<ImportPreviewReward> RoleRewards { get; init; } = [];

    /// <summary>
    ///     The curve that keeps levels identical, when there is one.
    /// </summary>
    public XpCurveType? NativeCurve { get; init; }

    /// <summary>
    ///     The server's current curve.
    /// </summary>
    public XpCurveType CurrentCurve { get; init; }

    /// <summary>
    ///     The import record, once written.
    /// </summary>
    public int? ImportId { get; init; }

    /// <summary>
    ///     What each settings section would write, for settings imports.
    /// </summary>
    public List<ImportSettingsSection> Sections { get; init; } = [];
}

/// <summary>
///     One section of a settings import, as shown before confirming.
/// </summary>
public sealed class ImportSettingsSection
{
    /// <summary>
    ///     The section key to send back when choosing it.
    /// </summary>
    public string Key { get; init; } = "";

    /// <summary>
    ///     How many settings or items it holds.
    /// </summary>
    public int Count { get; init; }

    /// <summary>
    ///     Short lines describing what it would write.
    /// </summary>
    public List<string> Details { get; init; } = [];
}

/// <summary>
///     A member shown in an import preview.
/// </summary>
public sealed class ImportPreviewMember
{
    /// <summary>
    ///     The member's Discord ID, as a string so clients keep full precision.
    /// </summary>
    public string UserId { get; init; } = "";

    /// <summary>
    ///     The member's name, from the server or the source.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    ///     The member's avatar on this server, when they are still in it.
    /// </summary>
    public string? AvatarUrl { get; init; }

    /// <summary>
    ///     Total XP in the source.
    /// </summary>
    public long? Xp { get; init; }

    /// <summary>
    ///     Level in the source.
    /// </summary>
    public int? Level { get; init; }

    /// <summary>
    ///     Wallet balance in the source.
    /// </summary>
    public long? Cash { get; init; }

    /// <summary>
    ///     Bank balance in the source.
    /// </summary>
    public long? Bank { get; init; }
}

/// <summary>
///     A role reward shown in an import preview.
/// </summary>
public sealed class ImportPreviewReward
{
    /// <summary>
    ///     The level the role is given at.
    /// </summary>
    public int Level { get; init; }

    /// <summary>
    ///     The role's ID, as a string so clients keep full precision.
    /// </summary>
    public string RoleId { get; init; } = "";

    /// <summary>
    ///     The role's name, when it still exists.
    /// </summary>
    public string? RoleName { get; init; }

    /// <summary>
    ///     Whether the role still exists on the server.
    /// </summary>
    public bool Exists { get; init; }
}

/// <summary>
///     The outcome of writing an import.
/// </summary>
/// <param name="ImportId">The import record, used to undo it.</param>
/// <param name="Members">Members written.</param>
/// <param name="Skipped">Members skipped by the minimum level.</param>
/// <param name="RoleRewards">Role rewards written.</param>
/// <param name="CurveChanged">The curve the server switched to, if it changed.</param>
public sealed record ImportResult(int ImportId, int Members, int Skipped, int RoleRewards, XpCurveType? CurveChanged);

/// <summary>
///     A past import, listed so it can be undone.
/// </summary>
public sealed class ImportHistoryEntry
{
    /// <summary>
    ///     The import record's ID.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    ///     Where the data came from.
    /// </summary>
    public ImportSource Source { get; init; }

    /// <summary>
    ///     What the data was for.
    /// </summary>
    public ImportKind Kind { get; init; }

    /// <summary>
    ///     Members written.
    /// </summary>
    public int MemberCount { get; init; }

    /// <summary>
    ///     Role rewards written.
    /// </summary>
    public int RoleRewardCount { get; init; }

    /// <summary>
    ///     Who ran the import, as a string so clients keep full precision.
    /// </summary>
    public string UserId { get; init; } = "";

    /// <summary>
    ///     When the import ran.
    /// </summary>
    public DateTime DateAdded { get; init; }

    /// <summary>
    ///     When the import was undone, if it was.
    /// </summary>
    public DateTime? UndoneAt { get; init; }

    /// <summary>
    ///     Whether the import can still be undone.
    /// </summary>
    public bool CanUndo { get; init; }
}
