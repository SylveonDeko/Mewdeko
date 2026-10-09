namespace Mewdeko.Modules.Import.Common;

/// <summary>
///     Why an import could not go ahead.
/// </summary>
public enum ImportError
{
    /// <summary>
    ///     The source answered with something unexpected.
    /// </summary>
    SourceFailed,

    /// <summary>
    ///     The MEE6 leaderboard is private or the server has never used MEE6.
    /// </summary>
    LeaderboardPrivate,

    /// <summary>
    ///     The source has no data for this server.
    /// </summary>
    NotFound,

    /// <summary>
    ///     The API key or token was rejected.
    /// </summary>
    BadKey,

    /// <summary>
    ///     An API key or token is needed for this source.
    /// </summary>
    KeyRequired,

    /// <summary>
    ///     A file is needed for this source.
    /// </summary>
    FileRequired,

    /// <summary>
    ///     The source kept rate limiting requests.
    /// </summary>
    RateLimited,

    /// <summary>
    ///     The file could not be read as any known format.
    /// </summary>
    UnreadableFile,

    /// <summary>
    ///     The data had no members in it.
    /// </summary>
    Empty,

    /// <summary>
    ///     This bot uses one currency for every server, so per-server balances cannot be imported.
    /// </summary>
    GlobalCurrency,

    /// <summary>
    ///     Another import is already running on this server.
    /// </summary>
    Busy,

    /// <summary>
    ///     The import job no longer exists.
    /// </summary>
    JobMissing,

    /// <summary>
    ///     The import job is not ready to be written.
    /// </summary>
    NotReady,

    /// <summary>
    ///     The source holds a different kind of data than was asked for.
    /// </summary>
    WrongKind,

    /// <summary>
    ///     The file was exported from a different server.
    /// </summary>
    WrongServer,

    /// <summary>
    ///     The import can no longer be undone.
    /// </summary>
    UndoExpired,

    /// <summary>
    ///     A newer import has to be undone first.
    /// </summary>
    UndoNotLatest
}

/// <summary>
///     Thrown when an import cannot go ahead, carrying a reason clients can show in their own words.
/// </summary>
/// <param name="error">Why the import stopped.</param>
public sealed class ImportException(ImportError error) : Exception(error.ToString())
{
    /// <summary>
    ///     Why the import stopped.
    /// </summary>
    public ImportError Error { get; } = error;
}
