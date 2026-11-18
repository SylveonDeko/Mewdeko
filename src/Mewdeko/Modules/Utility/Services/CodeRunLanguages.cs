using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Mewdeko.Modules.Utility.Common;

namespace Mewdeko.Modules.Utility.Services;

/// <summary>
///     The languages the run command can execute, loaded from the language data file. A member's text is only ever
///     used to look an entry up here.
/// </summary>
public sealed partial class CodeRunLanguages
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly Dictionary<string, CodeRunLanguage> byName;

    private CodeRunLanguages(List<CodeRunLanguage> all)
    {
        All = all;
        byName = new Dictionary<string, CodeRunLanguage>(StringComparer.OrdinalIgnoreCase);
        foreach (var language in all)
        {
            byName[language.Name] = language;
            foreach (var alias in language.Aliases)
                byName[alias] = language;
        }
    }

    /// <summary>
    ///     Gets every configured language.
    /// </summary>
    public IReadOnlyList<CodeRunLanguage> All { get; }

    /// <summary>
    ///     Loads and validates the language file.
    /// </summary>
    /// <param name="path">The path of the JSON file.</param>
    /// <exception cref="InvalidDataException">Thrown when the file is not valid.</exception>
    public static CodeRunLanguages LoadFile(string path)
    {
        return Parse(File.ReadAllText(path));
    }

    /// <summary>
    ///     Parses and validates language data.
    /// </summary>
    /// <param name="json">The JSON text, an array of languages.</param>
    /// <exception cref="InvalidDataException">Thrown when the data is not valid.</exception>
    public static CodeRunLanguages Parse(string json)
    {
        List<CodeRunLanguage>? languages;
        try
        {
            languages = JsonSerializer.Deserialize<List<CodeRunLanguage>>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"The language file is not valid JSON: {ex.Message}", ex);
        }

        if (languages is not { Count: > 0 })
            throw new InvalidDataException("The language file has no languages");

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var language in languages)
        {
            Validate(language);
            foreach (var name in language.Aliases.Prepend(language.Name))
            {
                if (!seen.Add(name))
                    throw new InvalidDataException($"The name '{name}' is used by more than one language");
            }
        }

        return new CodeRunLanguages(languages);
    }

    /// <summary>
    ///     Checks one entry, throwing when it could not be run safely.
    /// </summary>
    /// <param name="language">The entry.</param>
    /// <exception cref="InvalidDataException">Thrown when the entry is not valid.</exception>
    public static void Validate(CodeRunLanguage language)
    {
        if (!NamePattern().IsMatch(language.Name))
            throw new InvalidDataException($"'{language.Name}' is not a valid language name");

        foreach (var alias in language.Aliases)
        {
            if (!NamePattern().IsMatch(alias))
                throw new InvalidDataException($"'{alias}' is not a valid alias for {language.Name}");
        }

        if (!ImagePattern().IsMatch(language.Image))
            throw new InvalidDataException($"{language.Name} needs an image with an explicit tag");

        var tag = language.Image[(language.Image.LastIndexOf(':') + 1)..];
        if (tag.Equals("latest", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"{language.Name} uses the latest tag, pin a version");

        if (!FilePattern().IsMatch(language.FileName))
            throw new InvalidDataException($"'{language.FileName}' is not a valid file name for {language.Name}");

        if (string.IsNullOrWhiteSpace(language.Run))
            throw new InvalidDataException($"{language.Name} has no run command");

        foreach (var name in language.Env.Keys)
        {
            if (!EnvPattern().IsMatch(name) || name == CodeRunRequestBuilder.CodeVariable)
                throw new InvalidDataException($"'{name}' is not a valid environment variable for {language.Name}");
        }
    }

    /// <summary>
    ///     Finds a language by name or alias, ignoring case.
    /// </summary>
    /// <param name="text">What the member typed.</param>
    /// <returns>The configured language, or null when there is none.</returns>
    public CodeRunLanguage? Find(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        return byName.GetValueOrDefault(text.Trim());
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9+#_-]{0,23}$")]
    private static partial Regex NamePattern();

    [GeneratedRegex("^[a-z0-9][a-z0-9._/-]*:[A-Za-z0-9_][A-Za-z0-9._-]*$")]
    private static partial Regex ImagePattern();

    [GeneratedRegex("^[A-Za-z0-9_][A-Za-z0-9._-]{0,63}$")]
    private static partial Regex FilePattern();

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex EnvPattern();
}
