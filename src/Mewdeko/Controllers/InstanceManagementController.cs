using Mewdeko.Controllers.Common.DashboardAccess;
using Mewdeko.Modules.OwnerOnly.Services;
using Mewdeko.Services.Impl;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Controller for managing multiple bot instances. Everything except the instance list and the key exchange
///     is limited to bot owners.
/// </summary>
[ApiController]
[Route("botapi/[controller]")]
[Authorize("ApiKeyPolicy")]
[RequireOwner]
public class InstanceManagementController : Controller
{
    private readonly BotCredentials credentials;
    private readonly InstanceManagementService instanceManagementService;
    private readonly ILogger<InstanceManagementController> logger;

    /// <summary>
    ///     Initializes a new instance of the controller.
    /// </summary>
    /// <param name="instanceManagementService">The instancemanagementservice service.</param>
    /// <param name="credentials">The bot credentials used to tell owners from everyone else.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    public InstanceManagementController(
        InstanceManagementService instanceManagementService,
        BotCredentials credentials,
        ILogger<InstanceManagementController> logger)
    {
        this.instanceManagementService = instanceManagementService;
        this.credentials = credentials;
        this.logger = logger;
    }

    /// <summary>
    ///     Gets all active bot instances. A signed-in user who is not an owner gets the list without hosts.
    /// </summary>
    /// <returns>List of active bot instances.</returns>
    [HttpGet]
    [SkipRequireOwner]
    public async Task<IActionResult> GetInstances()
    {
        try
        {
            var instances = await instanceManagementService.GetActiveInstancesAsync();
            var hasAuthorization = Request.Headers.ContainsKey("Authorization");
            var caller = await DashboardCaller.GetUserIdAsync(HttpContext);
            if (hasAuthorization && !DashboardCaller.IsOwner(caller, credentials.IsOwner))
            {
                foreach (var instance in instances)
                {
                    instance.Host = "";
                    instance.OwnerUserId = null;
                }
            }

            return Ok(instances);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get instances");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Decides whether a request may read the custom bot keys.
    /// </summary>
    /// <param name="isMaster">Whether this instance is the master.</param>
    /// <param name="hasAuthorization">Whether the request carries a user token.</param>
    /// <param name="exchangeHeader">The value of the key exchange header.</param>
    /// <returns>True only on the master, for a request with the exchange header and no user token.</returns>
    public static bool CanExchangeKeys(bool isMaster, bool hasAuthorization, string? exchangeHeader)
    {
        return isMaster && !hasAuthorization && exchangeHeader == "1";
    }

    /// <summary>
    ///     Gets the API key of every custom bot that has its own, keyed by port, for the dashboard server. Only a
    ///     request that carries the exchange header and no user token is answered, so it can never be reached
    ///     through a user session.
    /// </summary>
    /// <returns>A map from instance port to that instance's API key.</returns>
    [HttpGet("keys")]
    [SkipRequireOwner]
    public async Task<IActionResult> GetInstanceKeys()
    {
        if (!CanExchangeKeys(credentials.IsMasterInstance, Request.Headers.ContainsKey("Authorization"),
                Request.Headers["X-Key-Exchange"]))
            return StatusCode(StatusCodes.Status403Forbidden);

        try
        {
            return Ok(await instanceManagementService.GetInstanceKeysAsync());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to read instance keys");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    ///     Adds a new bot instance.
    /// </summary>
    /// <param name="port">The port number the instance is running on.</param>
    /// <param name="host">
    ///     Optional hostname the instance is reachable on. Defaults to "localhost" for instances
    ///     running on the same host as this one.
    /// </param>
    [HttpPost("{port}")]
    public async Task<IActionResult> AddInstance(int port, [FromQuery] string? host = null)
    {
        if (!InstanceManagementService.IsValidHost(host))
            return BadRequest("Invalid host");

        try
        {
            var result = await instanceManagementService.AddInstanceAsync(port, host);
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