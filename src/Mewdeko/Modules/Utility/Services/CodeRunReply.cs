using System.Globalization;
using System.Text;
using Mewdeko.Modules.Utility.Common;
using Mewdeko.Services.Strings;

namespace Mewdeko.Modules.Utility.Services;

/// <summary>
///     Builds the text of the run command's replies so the text command and the slash command answer the same way.
/// </summary>
public static class CodeRunReply
{
    private const int LanguageEchoLength = 20;

    /// <summary>
    ///     Builds the reply for a finished or refused run.
    /// </summary>
    /// <param name="strings">The localized strings.</param>
    /// <param name="guildId">The server, for its language.</param>
    /// <param name="language">The language that was asked for.</param>
    /// <param name="result">The run's result.</param>
    public static string ForResult(GeneratedBotStrings strings, ulong guildId, CodeRunLanguage language,
        CodeRunResult result)
    {
        switch (result.Status)
        {
            case CodeRunStatus.Unavailable:
                return strings.CodeRunUnavailable(guildId);
            case CodeRunStatus.NotInstalled:
                return strings.CodeRunNotInstalled(guildId, language.Name);
            case CodeRunStatus.UserBusy:
                return strings.CodeRunBusy(guildId);
            case CodeRunStatus.Full:
                return strings.CodeRunFull(guildId);
            case CodeRunStatus.Cooldown:
                return strings.CodeRunCooldown(guildId);
            case CodeRunStatus.Failed:
                return strings.CodeRunFailed(guildId);
        }

        var seconds = result.Duration.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture);
        var body = CodeRunOutput.Format(Encoding.UTF8.GetBytes(result.Output), result.OutputCut,
            strings.CodeRunCut(guildId), strings.CodeRunEmpty(guildId));
        return Compose(strings.CodeRunHeader(guildId, language.Name, seconds),
            result.ExitCode != 0 && !result.TimedOut ? strings.CodeRunExit(guildId, result.ExitCode) : null,
            result.TimedOut ? strings.CodeRunTimeout(guildId) : null, body);
    }

    /// <summary>
    ///     Builds the reply for input that could not be run.
    /// </summary>
    /// <param name="strings">The localized strings.</param>
    /// <param name="guildId">The server, for its language.</param>
    /// <param name="parsed">The parse result.</param>
    public static string ForParseError(GeneratedBotStrings strings, ulong guildId, CodeRunParseResult parsed)
    {
        return parsed.Status switch
        {
            CodeRunParseStatus.MissingLanguage => strings.CodeRunMissingLanguage(guildId),
            CodeRunParseStatus.UnknownLanguage => strings.CodeRunUnknownLanguage(guildId,
                Echo(parsed.LanguageText)),
            CodeRunParseStatus.NoCode => strings.CodeRunNoCode(guildId),
            _ => strings.CodeRunTooLong(guildId, CodeRunParser.MaxCodeLength)
        };
    }

    /// <summary>
    ///     Builds the language list reply.
    /// </summary>
    /// <param name="strings">The localized strings.</param>
    /// <param name="guildId">The server, for its language.</param>
    /// <param name="installed">The languages that are installed.</param>
    public static string ForLanguages(GeneratedBotStrings strings, ulong guildId,
        IReadOnlyCollection<CodeRunLanguage> installed)
    {
        if (installed.Count == 0)
            return strings.CodeRunNoneInstalled(guildId);

        var lines = installed.Select(l => l.Aliases.Count == 0
            ? strings.CodeRunLanguageLinePlain(guildId, l.Name)
            : strings.CodeRunLanguageLine(guildId, l.Name, string.Join(", ", l.Aliases)));
        return strings.CodeRunLanguages(guildId, string.Join('\n', lines));
    }

    /// <summary>
    ///     Joins the pieces of a run reply: a header line, the optional exit code and timeout lines, and the output
    ///     in a code block.
    /// </summary>
    /// <param name="header">The header line.</param>
    /// <param name="exitLine">The exit code line, or null.</param>
    /// <param name="timeoutLine">The timeout line, or null.</param>
    /// <param name="body">The prepared output.</param>
    public static string Compose(string header, string? exitLine, string? timeoutLine, string body)
    {
        var text = new StringBuilder(header);
        if (exitLine != null)
            text.Append(' ').Append(exitLine);
        if (timeoutLine != null)
            text.Append(' ').Append(timeoutLine);
        text.Append("\n```\n").Append(body).Append("\n```");
        return text.ToString();
    }

    private static string Echo(string text)
    {
        var shown = text.Length > LanguageEchoLength ? text[..LanguageEchoLength] : text;
        return $"`{shown.Replace('`', '\'')}`";
    }
}
