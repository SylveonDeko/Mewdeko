namespace Mewdeko.Modules.Administration.Common;

/// <summary>
///     The existing protections that can also delete the offending message, DM the member or have exemptions.
/// </summary>
public enum ProtectionOptionTarget
{
    /// <summary>
    ///     Anti-Spam.
    /// </summary>
    Spam,

    /// <summary>
    ///     Anti-Mass-Mention.
    /// </summary>
    MassMention,

    /// <summary>
    ///     Anti-Pattern.
    /// </summary>
    Pattern,

    /// <summary>
    ///     Anti-Mass-Post.
    /// </summary>
    MassPost
}

/// <summary>
///     The outcome of changing the extra options on an existing protection.
/// </summary>
public enum ProtectionOptionResult
{
    /// <summary>
    ///     The change was saved.
    /// </summary>
    Ok,

    /// <summary>
    ///     The protection is not turned on.
    /// </summary>
    NotEnabled,

    /// <summary>
    ///     The protection does not have that option.
    /// </summary>
    Unsupported
}

/// <summary>
///     The extra options currently set on one existing protection.
/// </summary>
/// <param name="Target">The protection.</param>
/// <param name="DeleteMessage">Whether the offending message is deleted.</param>
/// <param name="NotifyUser">Whether the member is sent a DM.</param>
/// <param name="ExemptRoles">The IDs of exempt roles.</param>
/// <param name="ExemptChannels">The IDs of exempt channels.</param>
public record ProtectionOptionsInfo(ProtectionOptionTarget Target, bool DeleteMessage, bool NotifyUser,
    IReadOnlyList<ulong> ExemptRoles, IReadOnlyList<ulong> ExemptChannels);
