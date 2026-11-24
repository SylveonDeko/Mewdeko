using System.Globalization;
using System.Text.RegularExpressions;
using DataModel;

namespace Mewdeko.Modules.Forms.Common;

/// <summary>
///     Replaces the placeholders a form author may write into the announcement and success message.
/// </summary>
/// <remarks>
///     Written with percent signs like every other placeholder in the bot. The user and server ones
///     use the names the rest of the bot documents, and the form ones exist only here. Tokens that are
///     not known are left exactly as written, so text meant for the general replacer passes through.
/// </remarks>
public static partial class FormPlaceholders
{
    /// <summary>
    ///     The form placeholders, so the dashboard and the wiki can list the same names.
    /// </summary>
    public static readonly IReadOnlyList<string> FormTokens =
    [
        "%form.name%", "%form.link%", "%form.role%", "%form.submissions%", "%form.opensat%", "%form.expiresat%"
    ];

    /// <summary>
    ///     Replaces every known placeholder in a piece of form authored text.
    /// </summary>
    /// <param name="template">The text as the form author wrote it.</param>
    /// <param name="context">What the placeholders resolve against.</param>
    /// <returns>The text with its placeholders replaced, or the template itself when it is empty.</returns>
    public static string? Apply(string? template, FormPlaceholderContext context)
    {
        if (string.IsNullOrEmpty(template))
            return template;

        return TokenRegex().Replace(template, match => Resolve(match, context));
    }

    private static string Resolve(Match match, FormPlaceholderContext context)
    {
        var form = context.Form;

        var value = match.Groups[1].Value.ToLowerInvariant() switch
        {
            "user.mention" => ResolveMention(context),
            "user.id" => context.UserId?.ToString() ?? string.Empty,
            "user.name" => context.Username ?? string.Empty,
            "user.globalname" => context.GlobalName ?? context.Username ?? string.Empty,
            "server.name" => context.GuildName ?? string.Empty,
            "server.id" => form.GuildId.ToString(),
            "form.name" => form.Name,
            "form.link" => context.FormUrl ?? string.Empty,
            "form.role" => ResolveRole(form, context),
            "form.submissions" => ResolveSubmissions(form),
            "form.opensat" => FormatTimestamp(form.OpensAt, context.PlainText),
            "form.expiresat" => FormatTimestamp(form.ExpiresAt, context.PlainText),
            _ => null
        };

        if (value == null)
            return match.Value;

        return context.EscapeQuotes ? value.Replace(@"\", @"\\").Replace("\"", "\\\"") : value;
    }

    private static string ResolveMention(FormPlaceholderContext context)
    {
        if (context.PlainText)
        {
            var name = context.GlobalName ?? context.Username;
            return string.IsNullOrEmpty(name) ? string.Empty : "@" + name;
        }

        return context.UserId is { } id ? $"<@{id}>" : string.Empty;
    }

    private static string ResolveRole(Form form, FormPlaceholderContext context)
    {
        if (context.PlainText)
            return string.IsNullOrEmpty(context.RequiredRoleName) ? string.Empty : "@" + context.RequiredRoleName;

        if (form.RequiredRoleId is > 0)
            return $"<@&{form.RequiredRoleId.Value}>";

        return context.RequiredRoleName ?? string.Empty;
    }

    private static string ResolveSubmissions(Form form)
    {
        if (form.MaxResponses is > 0)
            return form.MaxResponses.Value.ToString(CultureInfo.InvariantCulture);

        return form.AllowMultipleSubmissions ? "Unlimited" : "1";
    }

    private static string FormatTimestamp(DateTime? value, bool plainText)
    {
        if (value is not { } moment)
            return string.Empty;

        if (plainText)
            return moment.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);

        return $"<t:{new DateTimeOffset(DateTime.SpecifyKind(moment, DateTimeKind.Utc)).ToUnixTimeSeconds()}:F>";
    }

    [GeneratedRegex(@"%([a-zA-Z]+\.[a-zA-Z]+)%")]
    private static partial Regex TokenRegex();
}

/// <summary>
///     Everything a form placeholder can resolve against. A missing part turns the placeholders that
///     need it into an empty string.
/// </summary>
/// <param name="Form">The form the text was written on.</param>
/// <param name="GuildName">The name of the guild that owns the form.</param>
/// <param name="UserId">Whoever the text is about.</param>
/// <param name="Username">That user's username.</param>
/// <param name="GlobalName">That user's display name, falling back to their username.</param>
/// <param name="FormUrl">The address of the form's public page.</param>
/// <param name="RequiredRoleName">The name of the required role, used when it cannot be mentioned.</param>
/// <param name="PlainText">
///     Whether the text is shown outside Discord, where a mention or timestamp would appear as raw
///     markup, so names and dates are written out instead.
/// </param>
/// <param name="EscapeQuotes">
///     Whether quotes and backslashes in the replaced values are escaped, for text that may be parsed
///     as embed JSON afterwards.
/// </param>
public sealed record FormPlaceholderContext(
    Form Form,
    string? GuildName,
    ulong? UserId,
    string? Username,
    string? GlobalName,
    string? FormUrl,
    string? RequiredRoleName,
    bool PlainText = false,
    bool EscapeQuotes = false);
