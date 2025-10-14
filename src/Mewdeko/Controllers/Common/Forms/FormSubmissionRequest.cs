namespace Mewdeko.Controllers.Common.Forms;

/// <summary>
///     Request model for submitting a form response.
/// </summary>
public class FormSubmissionRequest
{
    /// <summary>
    ///     The user ID submitting the form.
    /// </summary>
    public required ulong UserId { get; set; }

    /// <summary>
    ///     The user's Discord username.
    /// </summary>
    public required string Username { get; set; }

    /// <summary>
    ///     Optional Turnstile captcha token (required if form.RequireCaptcha is true).
    /// </summary>
    public string? TurnstileToken { get; set; }

    /// <summary>
    ///     Dictionary of questionId -> answer.
    ///     Answer can be a string or string[] for multi-select questions.
    /// </summary>
    public required Dictionary<int, object> Answers { get; set; }

    /// <summary>
    ///     Optional IP address for spam prevention.
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    ///     User's Discord premium type from OAuth (0=None, 1=NitroClassic, 2=Nitro, 3=NitroBasic).
    ///     Used for Nitro-based conditional logic since backend can't access this from gateway.
    /// </summary>
    public int? PremiumType { get; set; }
}