namespace Mewdeko.Controllers.Common.Wizard;

/// <summary>
///     Request model for updating user wizard preferences
/// </summary>
public class WizardPreferencesRequest
{
    /// <summary>
    ///     User ID
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    ///     Whether user prefers guided setup for new guilds
    /// </summary>
    public bool PrefersGuidedSetup { get; set; }

    /// <summary>
    ///     User's preferred experience level (can be used to override automatic detection)
    /// </summary>
    public int? PreferredExperienceLevel { get; set; }
}

/// <summary>
///     Response model for user wizard preferences
/// </summary>
public class UserPreferencesResponse
{
    /// <summary>
    ///     User ID
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    ///     Whether user prefers guided setup
    /// </summary>
    public bool PrefersGuidedSetup { get; set; }

    /// <summary>
    ///     User's current experience level
    /// </summary>
    public int ExperienceLevel { get; set; }

    /// <summary>
    ///     Whether user has ever completed any wizard
    /// </summary>
    public bool HasCompletedAnyWizard { get; set; }

    /// <summary>
    ///     Number of guilds where user has completed wizard
    /// </summary>
    public int WizardCompletedCount { get; set; }

    /// <summary>
    ///     When user first accessed the dashboard
    /// </summary>
    public DateTime? FirstDashboardAccess { get; set; }
}

/// <summary>
///     Wizard types
/// </summary>
public enum WizardType
{
    /// <summary>
    ///     No wizard
    /// </summary>
    None,

    /// <summary>
    ///     Full first-time wizard with explanations
    /// </summary>
    FirstTime,

    /// <summary>
    ///     Quick setup wizard for experienced users
    /// </summary>
    QuickSetup
}

/// <summary>
///     User experience actions for leveling
/// </summary>
public enum UserAction
{
    /// <summary>
    ///     Completed their first wizard
    /// </summary>
    CompletedFirstWizard,

    /// <summary>
    ///     Configured multiple features (5+)
    /// </summary>
    ConfiguredMultipleFeatures,

    /// <summary>
    ///     Used advanced features like custom commands, complex permissions, etc.
    /// </summary>
    UsedAdvancedFeatures
}