namespace Mewdeko.Modules.ChannelAccess.Services;

/// <summary>
///     The lifecycle state of a channel access application.
/// </summary>
public enum AccessApplicationStatus
{
    /// <summary>
    ///     The application is open and being voted on.
    /// </summary>
    Pending = 0,

    /// <summary>
    ///     The application passed and the access role was granted.
    /// </summary>
    Approved = 1,

    /// <summary>
    ///     The application was rejected.
    /// </summary>
    Denied = 2,

    /// <summary>
    ///     The applicant pulled their own application.
    /// </summary>
    Withdrawn = 3,

    /// <summary>
    ///     The voting window closed without a decision.
    /// </summary>
    Expired = 4
}

/// <summary>
///     How an approved applicant is let into the channel.
/// </summary>
public enum AccessGrantMode
{
    /// <summary>
    ///     Give the applicant a role that can see the channel.
    /// </summary>
    Role = 0,

    /// <summary>
    ///     Write a permission overwrite for the applicant straight onto the channel, adding them
    ///     individually rather than through a role.
    /// </summary>
    ChannelPermission = 1
}

/// <summary>
///     What happens to an application when its voting window runs out.
/// </summary>
public enum AccessExpiryBehavior
{
    /// <summary>
    ///     Deny the application.
    /// </summary>
    Deny = 0,

    /// <summary>
    ///     Approve if approvals outnumber denials, otherwise deny.
    /// </summary>
    Majority = 1,

    /// <summary>
    ///     Leave the application open until a human resolves it.
    /// </summary>
    StayOpen = 2
}

/// <summary>
///     The outcome of an attempt to cast a vote on an application.
/// </summary>
public enum AccessVoteResult
{
    /// <summary>
    ///     The vote was recorded.
    /// </summary>
    Recorded,

    /// <summary>
    ///     The vote replaced the voter's previous vote.
    /// </summary>
    Changed,

    /// <summary>
    ///     The voter clicked the option they had already picked, so the vote was removed.
    /// </summary>
    Removed,

    /// <summary>
    ///     The voter is not allowed to vote on this application.
    /// </summary>
    NotEligible,

    /// <summary>
    ///     The application is no longer open.
    /// </summary>
    NotPending,

    /// <summary>
    ///     The applicant tried to vote on their own application.
    /// </summary>
    OwnApplication
}