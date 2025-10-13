using Mewdeko.Modules.OwnerOnly.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Controller for managing multiple bot instances.
/// </summary>
[ApiController]
[Route("botapi/[controller]")]
[Authorize("ApiKeyPolicy")]
public class InstanceManagementController : Controller
{
    private readonly InstanceManagementService instanceManagementService;
    private readonly ILogger<InstanceManagementController> logger;

    /// <summary>
    ///     Initializes a new instance of the controller.
    /// </summary>
    /// <param name="instanceManagementService">The instancemanagementservice service.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    public InstanceManagementController(
        InstanceManagementService instanceManagementService,
        ILogger<InstanceManagementController> logger)
    {
        this.instanceManagementService = instanceManagementService;
        this.logger = logger;
    }

    /// <summary>
    ///     Gets all active bot instances.
    /// </summary>
    /// <returns>List of active bot instances.</returns>
    [HttpGet]
    public async Task<IActionResult> GetInstances()
    {
        try
        {
            var instances = await instanceManagementService.GetActiveInstancesAsync();
            return Ok(instances);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get instances");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Adds a new bot instance.
    /// </summary>
    /// <param name="port">The port number the instance is running on.</param>
    [HttpPost("{port}")]
    public async Task<IActionResult> AddInstance(int port)
    {
        try
        {
            var result = await instanceManagementService.AddInstanceAsync(port);
            if (!result.Success)
                return BadRequest("Failed to add instance");

            return Ok(result.Status);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to add instance on port {Port}", port);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Removes a bot instance.
    /// </summary>
    /// <param name="port">The port number of the instance to remove.</param>
    [HttpDelete("{port}")]
    public async Task<IActionResult> RemoveInstance(int port)
    {
        try
        {
            var result = await instanceManagementService.RemoveInstanceAsync(port);
            if (!result)
                return NotFound();

            return Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to remove instance on port {Port}", port);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Triggers an update on this instance (git pull + restart).
    /// </summary>
    [HttpPost("update")]
    public IActionResult TriggerUpdate()
    {
        try
        {
            // Start the update process in the background
            _ = instanceManagementService.ExecuteUpdateAsync();

            return Ok(new
            {
                message = "Update initiated"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to trigger update");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Triggers a restart on this instance.
    /// </summary>
    [HttpPost("restart")]
    public IActionResult TriggerRestart()
    {
        try
        {
            // Start the restart process in the background
            _ = instanceManagementService.ExecuteRestartAsync();

            return Ok(new
            {
                message = "Restart initiated"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to trigger restart");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Updates all registered instances (master only).
    /// </summary>
    [HttpPost("update-all")]
    public async Task<IActionResult> UpdateAllInstances()
    {
        try
        {
            var results = await instanceManagementService.UpdateAllInstancesAsync();
            return Ok(results);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update all instances");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Restarts all registered instances (master only).
    /// </summary>
    [HttpPost("restart-all")]
    public async Task<IActionResult> RestartAllInstances()
    {
        try
        {
            var results = await instanceManagementService.RestartAllInstancesAsync();
            return Ok(results);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to restart all instances");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Updates a specific instance by port (master only).
    /// </summary>
    /// <param name="port">The port number of the instance to update.</param>
    [HttpPost("update/{port}")]
    public async Task<IActionResult> UpdateInstance(int port)
    {
        try
        {
            var result = await instanceManagementService.UpdateInstanceAsync(port);
            if (!result.Success)
                return BadRequest(result.Message);

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update instance on port {Port}", port);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Restarts a specific instance by port (master only).
    /// </summary>
    /// <param name="port">The port number of the instance to restart.</param>
    [HttpPost("restart/{port}")]
    public async Task<IActionResult> RestartInstance(int port)
    {
        try
        {
            var result = await instanceManagementService.RestartInstanceAsync(port);
            if (!result.Success)
                return BadRequest(result.Message);

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to restart instance on port {Port}", port);
            return StatusCode(500, "Internal server error");
        }
    }
}