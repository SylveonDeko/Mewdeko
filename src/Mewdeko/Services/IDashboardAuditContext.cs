using System.Text.Json.Nodes;

namespace Mewdeko.Services;

/// <summary>
///     Per-request scratch space that lets a controller hand the audit filter a
///     real before/after pair for a mutation. Controllers that want rich diffs
///     inject this and call <see cref="RecordBefore" /> with the current state at
///     the start of the action (and optionally <see cref="RecordAfter" /> with the
///     post-change state). Snapshots are taken eagerly, so a service mutating the
///     same entity afterwards cannot corrupt the recorded "before". When nothing
///     is recorded the audit filter falls back to logging the redacted request body.
/// </summary>
public interface IDashboardAuditContext
{
    /// <summary>
    ///     The redacted snapshot of the resource state taken before the mutation, if any.
    /// </summary>
    JsonNode? Before { get; }

    /// <summary>
    ///     The redacted snapshot of the resource state taken after the mutation, if any.
    /// </summary>
    JsonNode? After { get; }

    /// <summary>
    ///     Whether a before snapshot was recorded for this request.
    /// </summary>
    bool HasBefore { get; }

    /// <summary>
    ///     Whether an after snapshot was recorded for this request.
    /// </summary>
    bool HasAfter { get; }

    /// <summary>
    ///     Eagerly snapshots the resource state before the mutation. Call this at
    ///     the start of a controller action, before applying changes.
    /// </summary>
    /// <param name="state">The current state of the resource being changed.</param>
    void RecordBefore(object? state);

    /// <summary>
    ///     Eagerly snapshots the resource state after the mutation. Optional; when
    ///     omitted the audit filter pairs the before snapshot with the request body.
    /// </summary>
    /// <param name="state">The new state of the resource after the change.</param>
    void RecordAfter(object? state);
}

/// <inheritdoc />
public sealed class DashboardAuditContext : IDashboardAuditContext
{
    /// <inheritdoc />
    public JsonNode? Before { get; private set; }

    /// <inheritdoc />
    public JsonNode? After { get; private set; }

    /// <inheritdoc />
    public bool HasBefore { get; private set; }

    /// <inheritdoc />
    public bool HasAfter { get; private set; }

    /// <inheritdoc />
    public void RecordBefore(object? state)
    {
        Before = AuditChangeSerializer.Snapshot(state);
        HasBefore = true;
    }

    /// <inheritdoc />
    public void RecordAfter(object? state)
    {
        After = AuditChangeSerializer.Snapshot(state);
        HasAfter = true;
    }
}
