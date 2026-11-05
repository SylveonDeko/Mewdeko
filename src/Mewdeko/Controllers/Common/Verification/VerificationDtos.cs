namespace Mewdeko.Controllers.Common.Verification;

/// <summary>
///     A guild's verification settings, as read and as written.
/// </summary>
public class VerificationSettingsDto
{
    /// <summary>
    ///     Whether the gate is on.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    ///     The channel holding the verification button.
    /// </summary>
    public ulong? ChannelId { get; set; }

    /// <summary>
    ///     The role given on join and removed when a member passes.
    /// </summary>
    public ulong? UnverifiedRoleId { get; set; }

    /// <summary>
    ///     The role given when a member passes.
    /// </summary>
    public ulong? VerifiedRoleId { get; set; }

    /// <summary>
    ///     Wrong answers a member gets before failing, from 1 to 10.
    /// </summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>
    ///     Minutes a captcha is valid and a failed member waits, from 1 to 60.
    /// </summary>
    public int TimeoutMinutes { get; set; } = 10;

    /// <summary>
    ///     Whether a member who fails is kicked.
    /// </summary>
    public bool KickOnFail { get; set; }

    /// <summary>
    ///     The channel results are logged to.
    /// </summary>
    public ulong? LogChannelId { get; set; }
}

/// <summary>
///     A request to create or use a verification channel.
/// </summary>
public class VerificationSetupRequest
{
    /// <summary>
    ///     The channel to use, or null to use the saved one or create one.
    /// </summary>
    public ulong? ChannelId { get; set; }
}

/// <summary>
///     One channel overwrite the gate would change.
/// </summary>
public class VerificationPermissionChangeDto
{
    /// <summary>
    ///     The channel.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    ///     The channel name.
    /// </summary>
    public string ChannelName { get; set; } = "";

    /// <summary>
    ///     The role the overwrite is for.
    /// </summary>
    public ulong RoleId { get; set; }
}

/// <summary>
///     How many overwrites a change touched.
/// </summary>
public class VerificationPermissionResult
{
    /// <summary>
    ///     How many overwrites were changed or restored.
    /// </summary>
    public int Changed { get; set; }

    /// <summary>
    ///     How many could not be.
    /// </summary>
    public int Failed { get; set; }
}

/// <summary>
///     How many members a bulk verification covers.
/// </summary>
public class VerificationBulkResponse
{
    /// <summary>
    ///     The number of members.
    /// </summary>
    public int Count { get; set; }
}
