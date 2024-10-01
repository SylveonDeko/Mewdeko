using Mewdeko.Modules.OwnerOnly.Services;
using Mewdeko.Services.Impl;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Controller for managing botwide configs
/// </summary>
[Route("[controller]")]
[Authorize("ApiKeyPolicy")]
public class BotConfigController(BotCredentials creds, OwnerOnlyService service) : Controller
{
    /// <summary>
    ///     Gets a ulong list of bot owners
    /// </summary>
    /// <returns>A collection of bot owners, ulongs</returns>
    [HttpGet("owners")]
    public async Task<IActionResult> GetOwners()
    {
        await Task.CompletedTask;
        return Ok(creds.OwnerIds);
    }

    /// <summary>
    ///     Gets the bots auto executing commands
    /// </summary>
    /// <returns></returns>
    [HttpGet("autocommands")]
    public async Task<IActionResult> GetAutocommands()
    {
        var autoCommands = await service.GetAutoCommands();
        return Ok(autoCommands);
    }

    /// <summary>
    ///     Gets the bots startup commands
    /// </summary>
    /// <returns></returns>
    [HttpGet("startupcommands")]
    public async Task<IActionResult> GetStartupCommands()
    {
        var startupCommands = await service.GetStartupCommands();
        return Ok(startupCommands);
    }
}