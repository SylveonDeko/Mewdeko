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
///     The Docker daemon on the host this instance runs on: container list, resource samples, logs, start,
///     stop and restart, plus compose pull and up per project. Everything here can affect every service on
///     the machine, so it is limited to bot owners.
/// </summary>
[ApiController]
[Route("botapi/[controller]")]
[Authorize("ApiKeyPolicy")]
public class DockerController(DockerService docker, BotCredentials creds, ILogger<DockerController> logger)
    : Controller
{
    private const int DefaultTail = 300;

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
    ///     Returns the daemon summary with every container and compose project on the host.
    /// </summary>
    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview()
    {
        return Ok(await docker.GetOverviewAsync());
    }

    /// <summary>
    ///     Takes one resource sample for a container.
    /// </summary>
    /// <param name="id">The container id, id prefix or name.</param>
    [HttpGet("containers/{id}/stats")]
    public async Task<IActionResult> GetStats(string id)
    {
        var container = await docker.FindContainerAsync(id);
        if (container == null)
            return NotFound("Docker does not know a container with that id");

        if (container.State != "running")
            return BadRequest("Stats are only available for running containers");

        var stats = await docker.GetStatsAsync(container);
        return stats == null ? StatusCode(502, "The daemon did not return a sample") : Ok(stats);
    }

    /// <summary>
    ///     Returns the last lines of a container's log, or the lines written since a cursor.
    /// </summary>
    /// <param name="id">The container id, id prefix or name.</param>
    /// <param name="tail">How many lines for a fresh tail, up to <see cref="DockerService.MaxTail" />.</param>
    /// <param name="since">The cursor from the previous chunk, for live following.</param>
    [HttpGet("containers/{id}/logs")]
    public async Task<IActionResult> GetLogs(string id, [FromQuery] int tail = DefaultTail,
        [FromQuery] string? since = null)
    {
        var container = await docker.FindContainerAsync(id);
        if (container == null)
            return NotFound("Docker does not know a container with that id");

        var chunk = await docker.GetLogsAsync(container, tail, since);
        return chunk == null ? StatusCode(502, "The daemon did not return the log") : Ok(chunk);
    }

    /// <summary>
    ///     Starts a stopped container.
    /// </summary>
    /// <param name="id">The container id, id prefix or name.</param>
    [HttpPost("containers/{id}/start")]
    public Task<IActionResult> Start(string id)
    {
        return RunAction(id, "start");
    }

    /// <summary>
    ///     Stops a running container, giving it fifteen seconds to exit before it is killed.
    /// </summary>
    /// <param name="id">The container id, id prefix or name.</param>
    [HttpPost("containers/{id}/stop")]
    public Task<IActionResult> Stop(string id)
    {
        return RunAction(id, "stop");
    }

    /// <summary>
    ///     Restarts a container.
    /// </summary>
    /// <param name="id">The container id, id prefix or name.</param>
    [HttpPost("containers/{id}/restart")]
    public Task<IActionResult> Restart(string id)
    {
        return RunAction(id, "restart");
    }

    /// <summary>
    ///     Starts a compose operation on a project in the background and returns the job to poll.
    /// </summary>
    /// <param name="name">The compose project name.</param>
    /// <param name="operation">pull, up, update (pull then up) or build (build then up).</param>
    [HttpPost("projects/{name}/{operation}")]
    public async Task<IActionResult> StartComposeJob(string name, string operation)
    {
        var overview = await docker.GetOverviewAsync();
        if (!overview.ComposeAvailable)
            return BadRequest("docker compose is not available to the bot on this host");

        var project = await docker.FindProjectAsync(name);
        if (project == null)
            return NotFound("No container carries that compose project label");

        if (!project.Operable)
            return BadRequest(
                "The project's compose files are not visible from the bot, so it cannot be operated on from here");

        var job = docker.StartComposeJob(project, operation.ToLowerInvariant());
        if (job == null)
            return BadRequest("operation must be pull, up, update or build");

        logger.LogInformation("Compose {Operation} started on {Project} as job {Job}", operation, name, job.Id);
        return Ok(job);
    }

    /// <summary>
    ///     Lists the compose jobs still in memory, newest first.
    /// </summary>
    [HttpGet("jobs")]
    public IActionResult ListJobs()
    {
        return Ok(docker.ListJobs());
    }

    /// <summary>
    ///     Returns a compose job with the output it has produced so far.
    /// </summary>
    /// <param name="id">The job id.</param>
    [HttpGet("jobs/{id}")]
    public IActionResult GetJob(string id)
    {
        var job = docker.FindJob(id);
        return job == null ? NotFound("That job is not known or has been trimmed") : Ok(job);
    }

    private async Task<IActionResult> RunAction(string id, string action)
    {
        var container = await docker.FindContainerAsync(id);
        if (container == null)
            return NotFound("Docker does not know a container with that id");

        var result = await docker.RunActionAsync(container, action);
        logger.LogInformation("Docker {Action} on {Name} ({Id}): {Outcome}", action, container.Name,
            container.Id[..12], result.Message);
        return result.Success ? Ok(result) : StatusCode(502, result);
    }
}
