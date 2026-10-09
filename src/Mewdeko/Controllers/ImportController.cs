using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json.Serialization;
using Mewdeko.Controllers.Common.DashboardAccess;
using Mewdeko.Modules.Import.Common;
using Mewdeko.Modules.Import.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Imports XP, level role rewards and balances from other bots. The section is never granted through restricted
///     dashboard access, so only the server owner and administrators reach it.
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class ImportController(DataImportService imports) : Controller
{
    /// <summary>
    ///     Starts reading data from another bot. Poll the returned job until it is ready, then apply it.
    /// </summary>
    /// <param name="guildId">The server to import into.</param>
    /// <param name="request">The source, plus its API key or file.</param>
    [HttpPost("start")]
    [RequestSizeLimit(64 * 1024 * 1024)]
    public async Task<IActionResult> Start(ulong guildId, [FromBody] ImportStartRequest? request)
    {
        if (request is null)
            return BadRequest(new { error = nameof(ImportError.SourceFailed) });

        var userId = await HttpContext.GetDashboardUserIdAsync();
        if (userId is null)
            return Unauthorized();

        try
        {
            var file = request.File;
            if (!string.IsNullOrEmpty(request.FileGzip))
                file = await Decompress(request.FileGzip);

            var job = imports.Start(guildId, userId.Value, request.Source, request.ApiKey, file);
            return Ok(await imports.PreviewAsync(job));
        }
        catch (ImportException ex)
        {
            return BadRequest(new { error = ex.Error.ToString() });
        }
    }

    /// <summary>
    ///     Unpacks a base64 gzip or raw deflate upload, refusing anything that inflates past 200 MB.
    /// </summary>
    private static async Task<string> Decompress(string base64)
    {
        byte[] packed;
        try
        {
            packed = Convert.FromBase64String(base64);
        }
        catch (FormatException)
        {
            throw new ImportException(ImportError.UnreadableFile);
        }

        var isGzip = packed.Length > 2 && packed[0] == 0x1f && packed[1] == 0x8b;
        await using Stream input = isGzip
            ? new GZipStream(new MemoryStream(packed), CompressionMode.Decompress)
            : new DeflateStream(new MemoryStream(packed), CompressionMode.Decompress);
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        try
        {
            while ((read = await input.ReadAsync(buffer)) > 0)
            {
                if (output.Length + read > 200 * 1024 * 1024)
                    throw new ImportException(ImportError.UnreadableFile);
                output.Write(buffer, 0, read);
            }
        }
        catch (InvalidDataException)
        {
            throw new ImportException(ImportError.UnreadableFile);
        }

        return Encoding.UTF8.GetString(output.ToArray());
    }

    /// <summary>
    ///     Gets a job's progress, and its preview once the data is read.
    /// </summary>
    /// <param name="guildId">The server.</param>
    /// <param name="jobId">The job's ID.</param>
    [HttpGet("jobs/{jobId}")]
    public async Task<IActionResult> GetJob(ulong guildId, string jobId)
    {
        var job = imports.GetJob(guildId, jobId);
        if (job is null)
            return NotFound(new { error = nameof(ImportError.JobMissing) });

        return Ok(await imports.PreviewAsync(job));
    }

    /// <summary>
    ///     Writes a job's XP data.
    /// </summary>
    /// <param name="guildId">The server.</param>
    /// <param name="jobId">The job's ID.</param>
    /// <param name="request">How to write it.</param>
    [HttpPost("jobs/{jobId}/xp")]
    public async Task<IActionResult> ApplyXp(ulong guildId, string jobId, [FromBody] XpImportRequest? request)
    {
        var job = imports.GetJob(guildId, jobId);
        if (job is null)
            return NotFound(new { error = nameof(ImportError.JobMissing) });

        request ??= new XpImportRequest();
        try
        {
            var result = await imports.ApplyXpAsync(job, new XpImportOptions
            {
                MergeMode = request.MergeMode,
                MinimumLevel = Math.Max(0, request.MinimumLevel),
                UseSourceCurve = request.UseSourceCurve,
                ImportRoleRewards = request.ImportRoleRewards
            }, request.SyncRoles);
            return Ok(result);
        }
        catch (ImportException ex)
        {
            return BadRequest(new { error = ex.Error.ToString() });
        }
    }

    /// <summary>
    ///     Writes a job's balance data.
    /// </summary>
    /// <param name="guildId">The server.</param>
    /// <param name="jobId">The job's ID.</param>
    /// <param name="request">How to write it.</param>
    [HttpPost("jobs/{jobId}/currency")]
    public async Task<IActionResult> ApplyCurrency(ulong guildId, string jobId,
        [FromBody] CurrencyImportRequest? request)
    {
        var job = imports.GetJob(guildId, jobId);
        if (job is null)
            return NotFound(new { error = nameof(ImportError.JobMissing) });

        try
        {
            var result = await imports.ApplyCurrencyAsync(job, new CurrencyImportOptions
            {
                MergeMode = request?.MergeMode ?? ImportMergeMode.Replace
            });
            return Ok(result);
        }
        catch (ImportException ex)
        {
            return BadRequest(new { error = ex.Error.ToString() });
        }
    }

    /// <summary>
    ///     Writes the chosen sections of a job's MEE6 settings.
    /// </summary>
    /// <param name="guildId">The server.</param>
    /// <param name="jobId">The job's ID.</param>
    /// <param name="request">The sections to write.</param>
    [HttpPost("jobs/{jobId}/settings")]
    public async Task<IActionResult> ApplySettings(ulong guildId, string jobId,
        [FromBody] SettingsImportRequest? request)
    {
        var job = imports.GetJob(guildId, jobId);
        if (job is null)
            return NotFound(new { error = nameof(ImportError.JobMissing) });

        try
        {
            var (importId, sections) = await imports.ApplySettingsAsync(job, request?.Sections ?? Mee6Section.All);
            return Ok(new { importId, sections });
        }
        catch (ImportException ex)
        {
            return BadRequest(new { error = ex.Error.ToString() });
        }
    }

    /// <summary>
    ///     Lists the server's past imports.
    /// </summary>
    /// <param name="guildId">The server.</param>
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(ulong guildId)
    {
        return Ok(await imports.GetHistoryAsync(guildId));
    }

    /// <summary>
    ///     Undoes an import made in the last day.
    /// </summary>
    /// <param name="guildId">The server.</param>
    /// <param name="importId">The import record's ID.</param>
    [HttpPost("history/{importId:int}/undo")]
    public async Task<IActionResult> Undo(ulong guildId, int importId)
    {
        try
        {
            return Ok(await imports.UndoAsync(guildId, importId));
        }
        catch (ImportException ex)
        {
            return BadRequest(new { error = ex.Error.ToString() });
        }
    }
}

/// <summary>
///     Starts an import.
/// </summary>
[JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
public sealed class ImportStartRequest
{
    /// <summary>
    ///     Where the data comes from.
    /// </summary>
    public ImportSource Source { get; set; }

    /// <summary>
    ///     The API key or token, for Amari, Tatsu and UnbelievaBoat.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    ///     The exported file's text, for Lurkr, Polaris, Arcane and other files.
    /// </summary>
    public string? File { get; set; }

    /// <summary>
    ///     The exported file gzipped or raw deflated, then base64 encoded, so large exports fit through the dashboard.
    /// </summary>
    public string? FileGzip { get; set; }
}

/// <summary>
///     How to write an XP import.
/// </summary>
[JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
public sealed class XpImportRequest
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
    ///     Switches the server to the source's curve when it has one.
    /// </summary>
    public bool UseSourceCurve { get; set; } = true;

    /// <summary>
    ///     Also imports the source's level role rewards.
    /// </summary>
    public bool ImportRoleRewards { get; set; } = true;

    /// <summary>
    ///     Hands out level reward roles to imported members afterwards.
    /// </summary>
    public bool SyncRoles { get; set; }
}

/// <summary>
///     Which sections of a settings import to write.
/// </summary>
public sealed class SettingsImportRequest
{
    /// <summary>
    ///     Section keys such as <c>welcome</c> or <c>levels</c>.
    /// </summary>
    public string[]? Sections { get; set; }
}

/// <summary>
///     How to write a currency import.
/// </summary>
[JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
public sealed class CurrencyImportRequest
{
    /// <summary>
    ///     How imported balances combine with current balances.
    /// </summary>
    public ImportMergeMode MergeMode { get; set; } = ImportMergeMode.Replace;
}
