namespace Mewdeko.Controllers.Common.Birthday;

/// <summary>
///     Request model for setting a member's birthday.
/// </summary>
public class SetBirthdayRequest
{
    /// <summary>
    ///     The birthday to set.
    /// </summary>
    public DateTime Birthday { get; set; }
}
