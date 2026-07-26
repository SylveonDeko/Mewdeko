namespace Mewdeko.Controllers.Common.DashboardAccess;

/// <summary>
///     Marks a controller or action that does not represent a guild dashboard section and must not be
///     evaluated by <see cref="DashboardAccessEnforcementFilter" />.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class SkipDashboardAccessAttribute : Attribute;