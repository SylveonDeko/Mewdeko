using Mewdeko.Modules.Utility.Common;

namespace Mewdeko.Modules.Utility.Services;

/// <summary>
///     What the text after the run command turned out to be.
/// </summary>
public enum CodeRunParseStatus
{
    /// <summary>
    ///     A language and code were found.
    /// </summary>
    Ok,

    /// <summary>
    ///     The member asked for the language list.
    /// </summary>
    ListLanguages,

    /// <summary>
    ///     No language was given.
    /// </summary>
    MissingLanguage,

    /// <summary>
    ///     The language is not configured.
    /// </summary>
    UnknownLanguage,

    /// <summary>
    ///     There is a language but no code.
    /// </summary>
    NoCode,

    /// <summary>
    ///     The code is over the length limit.
    /// </summary>
    TooLong
}

/// <summary>
///     The outcome of parsing the run command's input.
/// </summary>
/// <param name="Status">What was found.</param>
/// <param name="Language">The configured language when one was found.</param>
/// <param name="LanguageText">The language word the member gave, for error messages.</param>
/// <param name="Code">The code to run.</param>
public sealed record CodeRunParseResult(CodeRunParseStatus Status, CodeRunLanguage? Language, string LanguageText,
    string Code);

/// <summary>
///     Splits the text of a run command into a language and code. The language text is only used to look up a
///     configured entry.
/// </summary>
public static class CodeRunParser
{
    /// <summary>
    ///     The longest code that is run, in characters.
    /// </summary>
    public const int MaxCodeLength = 4000;

    private const string Fence = "```";

    /// <summary>
    ///     Parses <c>language code</c>, <c>language fenced-block</c> or a fenced block whose fence names the
    ///     language.
    /// </summary>
    /// <param name="input">Everything after the command name.</param>
    /// <param name="languages">The configured languages.</param>
    public static CodeRunParseResult Parse(string? input, CodeRunLanguages languages)
    {
        var text = (input ?? "").Trim();
        if (text.Length == 0)
            return Result(CodeRunParseStatus.MissingLanguage);

        if (text.Equals("languages", StringComparison.OrdinalIgnoreCase))
            return Result(CodeRunParseStatus.ListLanguages);

        string languageText;
        string code;
        if (text.StartsWith(Fence, StringComparison.Ordinal))
        {
            (languageText, code) = ReadFence(text);
        }
        else
        {
            var split = text.IndexOfAny([' ', '\t', '\r', '\n']);
            languageText = split < 0 ? text : text[..split];
            var rest = split < 0 ? "" : text[split..].Trim();
            code = rest.StartsWith(Fence, StringComparison.Ordinal) ? ReadFence(rest).Code : StripInlineCode(rest);
        }

        if (languageText.Length == 0)
            return Result(CodeRunParseStatus.MissingLanguage);

        var language = languages.Find(languageText);
        if (language == null)
            return Result(CodeRunParseStatus.UnknownLanguage, languageText: languageText);

        if (code.Length == 0)
            return Result(CodeRunParseStatus.NoCode, language, languageText);

        if (code.Length > MaxCodeLength)
            return Result(CodeRunParseStatus.TooLong, language, languageText, code);

        return Result(CodeRunParseStatus.Ok, language, languageText, code);
    }

    private static CodeRunParseResult Result(CodeRunParseStatus status, CodeRunLanguage? language = null,
        string languageText = "", string code = "")
    {
        return new CodeRunParseResult(status, language, languageText, code);
    }

    private static (string Language, string Code) ReadFence(string text)
    {
        var inner = text[Fence.Length..];
        var close = inner.LastIndexOf(Fence, StringComparison.Ordinal);
        if (close >= 0)
            inner = inner[..close];

        var newline = inner.IndexOf('\n');
        if (newline < 0)
            return ("", inner.Trim());

        var info = inner[..newline].Trim();
        var space = info.IndexOfAny([' ', '\t']);
        var language = space < 0 ? info : info[..space];
        return (language, inner[(newline + 1)..].Trim());
    }

    private static string StripInlineCode(string text)
    {
        if (text.Length > 2 && text[0] == '`' && text[^1] == '`')
            return text[1..^1].Trim();

        return text;
    }
}
