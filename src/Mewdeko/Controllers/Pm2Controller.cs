using System.IO;
using Mewdeko.AuthHandlers;
using Mewdeko.Modules.OwnerOnly.Common;
using Mewdeko.Modules.OwnerOnly.Services;
using Mewdeko.Services.Impl;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Mewdeko.Controllers;

/// <summary>
///     Read only access to the pm2 process table and log files on the host this instance runs on. Bot wide and
///     capable of exposing anything the process writes to its console, so it is limited to bot owners.
/// </summary>
[ApiController]
[Route("botapi/[controller]")]
[Authorize("ApiKeyPolicy")]
public class Pm2Controller(Pm2LogService pm2, BotCredentials creds, ILogger<Pm2Controller> logger) : Controller
{
    private const int DefaultLines = 300;

    /// <summary>
    ///     Rejects anyone who is not a bot owner. The shared API key is attached by the dashboard proxy to every
    ///     request, so the caller's identity has to come from the dashboard JWT and be checked here rather than
    ///     trusted from the client side owner check alone.
    /// </summary>
    /// <param name="context">The action context.</param>
    /// <param name="next">The next action in the pipeline.</param>
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var authResult = await HttpContext.AuthenticateAsync(DashJwtConstants.SchemeName);
        var userIdClaim = authResult.Principal?.FindFirst(DashJwtConstants.UserIdClaim)?.Value;

        if (!ulong.TryParse(userIdClaim, out var userId) || !creds.IsOwner(userId))
        {
            context.Result = Forbid();
            return;
        }

        await next();
    }

    /// <summary>
    ///     Lists the processes pm2 manages on this host.
    /// </summary>
    [HttpGet("processes")]
    public async Task<IActionResult> GetProcesses()
    {
        return Ok(await pm2.GetProcessesAsync());
    }

    /// <summary>
    ///     Returns the last lines of a process's log.
    /// </summary>
    /// <param name="pmId">The pm2 id of the process.</param>
    /// <param name="stream">out or error.</param>
    /// <param name="lines">How many lines to return, up to <see cref="Pm2LogService.MaxLines" />.</param>
    [HttpGet("logs/{pmId:int}")]
    public async Task<IActionResult> GetTail(int pmId, [FromQuery] string stream = "out",
        [FromQuery] int lines = DefaultLines)
    {
        if (!TryParseStream(stream, out var logStream))
            return BadRequest("stream must be out or error");

        var resolved = await ResolveLog(pmId, logStream);
        if (resolved.Result != null)
            return resolved.Result;

        var chunk = await pm2.ReadTailAsync(resolved.Path!, logStream, lines);
        return chunk == null ? NotFound("That log file does not exist yet") : Ok(chunk);
    }

    /// <summary>
    ///     Returns the lines appended to a process's log since a byte offset, for live following.
    /// </summary>
    /// <param name="pmId">The pm2 id of the process.</param>
    /// <param name="stream">out or error.</param>
    /// <param name="offset">The End offset of the previously received chunk.</param>
    /// <param name="lines">How many lines to return if the file was rotated and a fresh tail is needed.</param>
    [HttpGet("logs/{pmId:int}/updates")]
    public async Task<IActionResult> GetUpdates(int pmId, [FromQuery] long offset, [FromQuery] string stream = "out",
        [FromQuery] int lines = DefaultLines)
    {
        if (!TryParseStream(stream, out var logStream))
            return BadRequest("stream must be out or error");

        var resolved = await ResolveLog(pmId, logStream);
        if (resolved.Result != null)
            return resolved.Result;

        var chunk = await pm2.ReadAfterAsync(resolved.Path!, logStream, offset, lines);
        return chunk == null ? NotFound("That log file does not exist yet") : Ok(chunk);
    }

    /// <summary>
    ///     Streams a process's whole log file as a download.
    /// </summary>
    /// <param name="pmId">The pm2 id of the process.</param>
    /// <param name="stream">out or error.</param>
    [HttpGet("logs/{pmId:int}/download")]
    public async Task<IActionResult> Download(int pmId, [FromQuery] string stream = "out")
    {
        if (!TryParseStream(stream, out var logStream))
            return BadRequest("stream must be out or error");

        var resolved = await ResolveLog(pmId, logStream);
        if (resolved.Result != null)
            return resolved.Result;

        FileStream? file;
        try
        {
            file = Pm2LogService.OpenForDownload(resolved.Path!);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not open pm2 log {Path} for download", resolved.Path);
            return StatusCode(500, "The log file could not be opened");
        }

        if (file == null)
            return NotFound("That log file does not exist yet");

        var fileName = $"{resolved.Process!.Name}-{logStream.ToString().ToLowerInvariant()}.log";
        return File(file, "text/plain; charset=utf-8", fileName, true);
    }

    /// <summary>
    ///     Looks a process up and picks the file behind the requested stream, producing the error response to
    ///     return when either is missing.
    /// </summary>
    private async Task<(IActionResult? Result, Pm2ProcessInfo? Process, string? Path)> ResolveLog(int pmId,
        Pm2LogStream stream)
    {
        var process = await pm2.FindProcessAsync(pmId);
        if (process == null)
            return (NotFound("pm2 does not know a process with that id"), null, null);

        var path = Pm2LogService.LogPath(process, stream);
        return path == null
            ? (NotFound("pm2 reports no file for that stream"), process, null)
            : (null, process, path);
    }

    private static bool TryParseStream(string? value, out Pm2LogStream stream)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case null or "" or "out" or "stdout":
                stream = Pm2LogStream.Out;
                return true;
            case "error" or "err" or "stderr":
                stream = Pm2LogStream.Error;
                return true;
            default:
                stream = Pm2LogStream.Out;
                return false;
        }
    }
}