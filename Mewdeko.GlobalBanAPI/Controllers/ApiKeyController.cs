using Mewdeko.GlobalBanAPI.DbStuff;
using Mewdeko.GlobalBanAPI.DbStuff.Models;
using Mewdeko.GlobalBanAPI.Yml;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Mewdeko.GlobalBanAPI.Controllers;

[ApiController]
[Route("/")]
public class ApiKeyController(DbService service) : ControllerBase
{
    [HttpPut("AddApiKey", Name = "AddApiKey")]
    [EnableRateLimiting("fixed")]
    public async Task<IActionResult> Put([FromBody] string key)
    {
        var creds = DeserializeYaml.CredsDeserialize();
        if (string.IsNullOrEmpty(creds.MasterKey) || creds.MasterKey == "none")
        {
            return BadRequest("Master Key not set.");
        }

        if (!ControllerContext.HttpContext.Request.Headers.TryGetValue("ApiKey", out var potentialKey))
            return BadRequest("No Master Key provided.");

        if (creds.MasterKey != potentialKey)
        {
            return BadRequest("Invalid Master Key.");
        }

        await using var uow = service.GetDbContext();
        uow.Keys.Add(new Keys
        {
            Key = key
        });
        await uow.SaveChangesAsync();
        return Ok("API Key added.");
    }

    [HttpDelete("RemoveApiKey", Name = "RemoveApiKey")]
    [EnableRateLimiting("fixed")]
    public async Task<IActionResult> Delete([FromBody] string key)
    {
        var creds = DeserializeYaml.CredsDeserialize();
        if (string.IsNullOrEmpty(creds.MasterKey) || creds.MasterKey == "none")
        {
            return BadRequest("Master Key not set.");
        }

        if (!ControllerContext.HttpContext.Request.Headers.TryGetValue("ApiKey", out var potentialKey))
            return BadRequest("No Master Key provided.");

        if (creds.MasterKey != potentialKey)
        {
            return BadRequest("Invalid Master Key.");
        }

        await using var uow = service.GetDbContext();
        var keyToRemove = uow.Keys.FirstOrDefault(k => k.Key == key);
        if (keyToRemove == null)
        {
            return BadRequest("Key not found.");
        }

        uow.Keys.Remove(keyToRemove);
        await uow.SaveChangesAsync();
        return Ok("API Key removed.");
    }
}