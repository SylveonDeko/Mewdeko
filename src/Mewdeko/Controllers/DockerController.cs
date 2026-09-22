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
///     stop and restart, compose pull and up per project, and the running build's version against what the
///     registry has published. Everything here can affect every service on the machine, so it is limited to
///     bot owners.
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
    ///     Describes this instance: its build, the container it runs in, and whether a newer image is published.
    /// </summary>
    /// <param name="refresh">Ask the registry again instead of using the cached answer.</param>
    [HttpGet("self")]
    public async Task<IActionResult> GetSelf([FromQuery] bool refresh = false)
    {
        if (refresh)
            await docker.GetPublishedAsync(true);

        return Ok(await docker.GetSelfAsync());
    }

    /// <summary>
    ///     Pulls the newest image and recreates this instance's own container. The work runs in a helper
    ///     container, so the job outlives this process; poll the returned job from any instance on the host.
    /// </summary>
    [HttpPost("self/update")]
    public Task<IActionResult> UpdateSelf()
    {
        return UpdateFleet(false);
    }

    /// <summary>
    ///     Pulls the newest image and recreates every container in this instance's compose project, which for a
    ///     fleet means every bot on the host. Only containers whose image or config changed are restarted.
    /// </summary>
    [HttpPost("self/update-all")]
    public Task<IActionResult> UpdateAll()
    {
        return UpdateFleet(true);
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
    ///     Starts a compose operation on a project in a helper container and returns the job to poll.
    /// </summary>
    /// <param name="name">The compose project name.</param>
    /// <param name="operation">pull (fetch and rebuild, restart nothing), up, or update (both).</param>
    /// <param name="services">Comma separated service names to limit the operation to; empty for the whole project.</param>
    [HttpPost("projects/{name}/{operation}")]
    public async Task<IActionResult> StartComposeJob(string name, string operation,
        [FromQuery] string? services = null)
    {
        var project = await docker.FindProjectAsync(name);
        if (project == null)
            return NotFound("No container carries that compose project label");

        var scope = (services ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var (job, error) = await docker.StartComposeJobAsync(project, operation.ToLowerInvariant(), scope);
        if (job == null)
            return BadRequest(error ?? "The job could not be started");

        logger.LogInformation("Compose {Operation} started on {Project} ({Services}) as job {Job}", operation, name,
            scope.Length > 0 ? string.Join(',', scope) : "all", job.Id[..12]);
        return Ok(job);
    }

    /// <summary>
    ///     Lists the compose jobs whose helper containers still exist, newest first, without output.
    /// </summary>
    [HttpGet("jobs")]
    public async Task<IActionResult> ListJobs()
    {
        return Ok(await docker.ListJobsAsync());
    }

    /// <summary>
    ///     Returns a compose job with the output it has produced so far.
    /// </summary>
    /// <param name="id">The job id.</param>
    [HttpGet("jobs/{id}")]
    public async Task<IActionResult> GetJob(string id)
    {
        var job = await docker.FindJobAsync(id);
        return job == null ? NotFound("That job is not known or has been trimmed") : Ok(job);
    }

    private async Task<IActionResult> UpdateFleet(bool wholeProject)
    {
        var self = await docker.GetSelfAsync();
        if (!self.CanUpdate || self.Project == null || self.Container?.ComposeService == null)
            return BadRequest(self.UpdateBlockedReason ?? "This instance cannot be updated from the dashboard");

        var scope = wholeProject ? [] : new[]
        {
            self.Container.ComposeService
        };
        var (job, error) = await docker.StartComposeJobAsync(self.Project, "update", scope);
        if (job == null)
            return BadRequest(error ?? "The update could not be started");

        logger.LogInformation("Update of {Scope} in {Project} started as job {Job}",
            wholeProject ? "the whole fleet" : self.Container.ComposeService, self.Project.Name, job.Id[..12]);
        return Ok(job);
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
