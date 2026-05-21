namespace Mewdeko.Controllers.Common.AuditLog;

/// <summary>
///     Marks a controller or action that the dashboard audit filter must ignore.
///     Use it for background polling and health-check endpoints the dashboard
///     hits automatically rather than because a user navigated to them.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true)]
public sealed class SkipAuditAttribute : Attribute;
