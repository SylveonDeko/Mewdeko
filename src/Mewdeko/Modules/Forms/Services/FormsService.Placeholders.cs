using DataModel;
using Mewdeko.Modules.Forms.Common;

namespace Mewdeko.Modules.Forms.Services;

/// <summary>
///     Filling in the placeholders a form author writes into the announcement and the success message.
/// </summary>
public partial class FormsService
{
    /// <summary>
    ///     The longest username taken from a request when the bot cannot look the person up itself.
    /// </summary>
    private const int MaxFallbackNameLength = 32;

    /// <summary>
    ///     Gathers what a form's placeholders resolve against.
    /// </summary>
    /// <param name="form">The form the text belongs to.</param>
    /// <param name="userId">Whoever the text is about, or null when it is about nobody.</param>
    /// <param name="fallbackName">A username to use when the bot cannot look the person up.</param>
    /// <param name="plainText">Whether the result is shown outside Discord.</param>
    /// <param name="escapeQuotes">Whether the result may be parsed as embed JSON afterwards.</param>
    /// <returns>The context.</returns>
    public async Task<FormPlaceholderContext> BuildPlaceholderContextAsync(
        Form form,
        ulong? userId,
        string? fallbackName,
        bool plainText,
        bool escapeQuotes)
    {
        var guild = client.GetGuild(form.GuildId);

        string? username = null;
        string? globalName = null;

        if (userId is { } id)
        {
            IUser? user = guild?.GetUser(id) ?? client.GetUser(id);

            username = user?.Username ?? fallbackName?.Trim().TrimTo(MaxFallbackNameLength);
            globalName = user?.GlobalName;
        }

        var shareCode = await GetShareCodeAsync(form.Id);

        var formUrl = shareCode == null || string.IsNullOrWhiteSpace(creds.DashboardUrl)
            ? null
            : $"{creds.DashboardUrl.TrimEnd('/')}/forms/{shareCode}";

        var roleName = form.RequiredRoleId is { } roleId ? guild?.GetRole(roleId)?.Name : null;

        return new FormPlaceholderContext(form, guild?.Name, userId, username, globalName, formUrl, roleName,
            plainText, escapeQuotes);
    }

    /// <summary>
    ///     Works out the text shown to a submitter once their response is in. The result is plain text
    ///     for the page to show as text, never as markup.
    /// </summary>
    /// <param name="form">The form that was submitted.</param>
    /// <param name="userId">The submitter.</param>
    /// <param name="fallbackName">The username the request carried.</param>
    /// <returns>The message, or null when the form has none and the page should use its own wording.</returns>
    public async Task<string?> ResolveSuccessMessageAsync(Form form, ulong userId, string? fallbackName)
    {
        if (string.IsNullOrWhiteSpace(form.SuccessMessage))
            return null;

        var context = await BuildPlaceholderContextAsync(form, userId, fallbackName, true, false);

        return FormPlaceholders.Apply(form.SuccessMessage, context)?.TrimTo(2000);
    }
}
