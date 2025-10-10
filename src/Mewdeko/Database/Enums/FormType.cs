namespace Mewdeko.Database.Enums;

/// <summary>
///     Represents the type of a form.
/// </summary>
public enum FormType
{
    /// <summary>
    ///     A regular form accessible to guild members.
    /// </summary>
    Regular = 0,

    /// <summary>
    ///     A ban appeal form accessible to banned users.
    /// </summary>
    BanAppeal = 1,

    /// <summary>
    ///     A join application form accessible to external users.
    /// </summary>
    JoinApplication = 2
}