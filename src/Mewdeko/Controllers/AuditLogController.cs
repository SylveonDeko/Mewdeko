using System.Text.Json.Nodes;
using DataModel;
using Mewdeko.Controllers.Common.AuditLog;
using Mewdeko.Database.Enums;
using Mewdeko.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Mewdeko.Controllers;

/// <summary>
///     Read API for the dashboard audit log: who accessed the dashboard, what they
///     changed, and what they viewed. Scoped per guild.
/// </summary>
[ApiController]
[Route("botapi/[controller]/{guildId}")]
[Authorize("ApiKeyPolicy")]
public class AuditLogController(DashboardAuditService auditService) : Controller
{
    /// <summary>
    ///     Returns a page of audit log entries for a guild, newest first, with optional filters.
    /// </summary>
    /// <param name="guildId">The guild to read the audit log for.</param>
    /// <param name="userId">Optional Discord user id filter.</param>
    /// <param name="action">Optional action type filter.</param>
    /// <param name="section">Optional dashboard section filter.</param>
    /// <param name="after">Optional lower bound on the entry timestamp (UTC).</param>
    /// <param name="before">Optional upper bound on the entry timestamp (UTC).</param>
    /// <param name="page">The 1-based page number.</param>
    /// <param name="pageSize">The page size (max 200).</param>
    [HttpGet]
    public async Task<IActionResult> GetAuditLog(
        ulong guildId,
        [FromQuery] ulong? userId = null,
        [FromQuery] AuditAction? action = null,
        [FromQuery] string? section = null,
        [FromQuery] DateTime? after = null,
        [FromQuery] DateTime? before = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var (items, total) = await auditService.GetForGuildAsync(
            guildId, userId, action, section, after, before, page, pageSize);

        return Ok(new AuditLogPageResponse
        {
            Items = items.Select(MapEntry).ToList(),
            Total = total,
            Page = page < 1 ? 1 : page,
            PageSize = pageSize
        });
    }

    private static AuditLogEntryResponse MapEntry(DashboardAuditLog entry) => new()
    {
        Id = entry.Id,
        UserId = entry.UserId,
        UserName = entry.UserName,
        Action = entry.Action,
        Section = entry.Section,
        Endpoint = entry.Endpoint,
        HttpMethod = entry.HttpMethod,
        Changes = ParseChanges(entry.Changes),
        UserAgent = entry.UserAgent,
        DateAdded = DateTime.SpecifyKind(entry.DateAdded, DateTimeKind.Utc)
    };

    private static JsonNode? ParseChanges(string? changes)
    {
        if (string.IsNullOrWhiteSpace(changes))
            return null;
        try
        {
            return JsonNode.Parse(changes);
        }
        catch
        {
            return null;
        }
    }
}
