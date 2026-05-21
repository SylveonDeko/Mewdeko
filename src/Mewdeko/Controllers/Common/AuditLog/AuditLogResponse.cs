using System.Text.Json.Nodes;
using Mewdeko.Database.Enums;

namespace Mewdeko.Controllers.Common.AuditLog;

/// <summary>
///     A single dashboard audit log entry as returned to the dashboard.
/// </summary>
public class AuditLogEntryResponse
{
    /// <summary>
    ///     The entry's unique id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    ///     The Discord user id that performed the action.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    ///     The Discord username at the time of the action.
    /// </summary>
    public string UserName { get; set; } = "";

    /// <summary>
    ///     The kind of activity recorded.
    /// </summary>
    public AuditAction Action { get; set; }

    /// <summary>
    ///     The dashboard section the action belonged to.
    /// </summary>
    public string Section { get; set; } = "";

    /// <summary>
    ///     The bot API endpoint that was hit.
    /// </summary>
    public string Endpoint { get; set; } = "";

    /// <summary>
    ///     The HTTP method of the request.
    /// </summary>
    public string HttpMethod { get; set; } = "";

    /// <summary>
    ///     The before/after change document for mutations, parsed as JSON. Null for views.
    /// </summary>
    public JsonNode? Changes { get; set; }

    /// <summary>
    ///     The client user agent, when recorded.
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    ///     When the action occurred (UTC).
    /// </summary>
    public DateTime DateAdded { get; set; }
}

/// <summary>
///     A page of dashboard audit log entries plus the total count for pagination.
/// </summary>
public class AuditLogPageResponse
{
    /// <summary>
    ///     The entries on this page, newest first.
    /// </summary>
    public IReadOnlyList<AuditLogEntryResponse> Items { get; set; } = [];

    /// <summary>
    ///     The total number of entries matching the filters across all pages.
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    ///     The page number returned (1-based).
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    ///     The page size used.
    /// </summary>
    public int PageSize { get; set; }
}
