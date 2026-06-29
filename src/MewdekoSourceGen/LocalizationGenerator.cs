using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace MewdekoSourceGen;

/// <summary>
///     Source generator that creates strongly-typed wrappers for localization strings.
///     Processes all responses.*.json files to generate culture-aware access methods.
/// </summary>
/// <remarks>
///     This generator creates a partial class that wraps the IBotStrings interface,
///     providing strongly-typed methods for each localization key while maintaining
///     culture awareness and guild-specific language settings.
///     The generator will:
///     1. Process all responses.*.json files in the project
///     2. Generate methods for each unique key across all locale files
///     3. Support both guild-based and explicit culture specification
///     4. Maintain fallback behavior to default locale
///     5. Provide comprehensive documentation about locale support
/// </remarks>
[Generator]
public class LocalizationGenerator : IIncrementalGenerator
{
    /// <summary>
    ///     Regular expression pattern to match flat locale files and extract the culture code.
    ///     Matches files named 'responses.LOCALE.json' where LOCALE is the culture code.
    /// </summary>
    private static readonly Regex LangFilePattern = new(@"responses\.(.+)\.json$");

    /// <summary>
    ///     Regular expression pattern to match per-locale split files.
    ///     Matches files like 'responses/LOCALE/MODULE.json' or 'responses\LOCALE\MODULE.json'.
    /// </summary>
    private static readonly Regex SplitLangFilePattern = new(@"responses[\\/]([^\\/]+)[\\/][^\\/]+\.json$");

    private static readonly Regex InvalidCharsRegex = new(@"[^A-Za-z0-9_]");
    private static readonly Regex StartsWithDigitRegex = new(@"^\d+");
    private static readonly Regex ContainsDigitRegex = new(@"\d");
    private static readonly Regex FormatArgsRegex = new(@"\{(\d+)\}");

    /// <summary>
    ///     Initializes the incremental source generator.
    /// </summary>
    /// <param name="context">The initialization context that handles incremental generation.</param>
    /// <remarks>
    ///     This method sets up the pipeline for processing locale files:
    ///     1. Identifies all response files matching the pattern
    ///     2. Extracts locale and content from each file
    ///     3. Combines all locales to generate the final source
    /// </remarks>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Register all response files (flat 'responses.LOCALE.json' or split 'responses/LOCALE/MODULE.json')
        var responseFiles = context.AdditionalTextsProvider
            .Where(file => LangFilePattern.IsMatch(file.Path) || SplitLangFilePattern.IsMatch(file.Path));

        // Transform the files into a collection of locale-specific responses (one entry per file)
        var localeResponses = responseFiles.Select((text, cancelToken) =>
        {
            string locale;
            var flatMatch = LangFilePattern.Match(text.Path);
            if (flatMatch.Success)
            {
                locale = flatMatch.Groups[1].Value;
            }
            else
            {
                var splitMatch = SplitLangFilePattern.Match(text.Path);
                locale = splitMatch.Groups[1].Value;
            }

            var content = text.GetText(cancelToken)?.ToString() ?? "{}";
            var responses = ParseJsonToDictionary(content);
            return (locale, responses);
        });

        var rootNamespaceProvider = context.AnalyzerConfigOptionsProvider
            .Select((provider, _) =>
                provider.GlobalOptions.TryGetValue("build_property.RootNamespace", out var rootNamespace)
                    ? rootNamespace
                    : "Mewdeko");

        // Merge per-locale fragments into a single dictionary per locale before generating
        var combinedSource = localeResponses.Collect()
            .Combine(rootNamespaceProvider)
            .Select((tuple, _) =>
            {
                var (items, rootNamespace) = tuple;
                var merged = new Dictionary<string, Dictionary<string, string>>();
                foreach (var (locale, responses) in items)
                {
                    if (!merged.TryGetValue(locale, out var dict))
                    {
                        dict = new Dictionary<string, string>();
                        merged[locale] = dict;
                    }

                    foreach (var kvp in responses)
                        dict[kvp.Key] = kvp.Value;
                }

                return GenerateSource(merged.Select(kvp => (kvp.Key, kvp.Value)).ToImmutableArray(), rootNamespace);
            });

        context.RegisterSourceOutput(combinedSource, (sourceProductionContext, source) =>
        {
            sourceProductionContext.AddSource("GeneratedBotStrings.g.cs", source);
        });
    }

    /// <summary>
    ///     Generates the source code for the localization wrapper class.
    /// </summary>
    /// <param name="allResponses">Collection of responses from all locale files.</param>
    /// <returns>The generated source code as a SourceText object.</returns>
    /// <remarks>
    ///     Generates:
    ///     1. A wrapper class with IBotStrings and ILocalization dependencies
    ///     2. Helper methods for culture resolution
    ///     3. Strongly-typed methods for each localization key
    ///     4. Documentation including locale support information
    /// </remarks>
    private static SourceText GenerateSource(
        ImmutableArray<(string locale, Dictionary<string, string> responses)> allResponses,
        string rootNamespace)
    {
        var guildIdType = rootNamespace == "MewdekoStoat" ? "string" : "ulong?";
        var sourceBuilder = new StringBuilder($$"""
                                                #nullable disable

                                                using System;
                                                using System.Globalization;
                                                using {{rootNamespace}}.Services.strings;

                                                namespace {{rootNamespace}}.Services.Strings
                                                {
                                                    /// <summary>
                                                    /// Provides strongly-typed access to localization strings.
                                                    /// Generated from responses.*.json files
                                                    /// </summary>
                                                    /// <remarks>
                                                    /// This class wraps the IBotStrings interface to provide:
                                                    /// - Strongly-typed access to localization keys
                                                    /// - Guild-specific language support
                                                    /// - Explicit culture specification
                                                    /// - Proper fallback behavior
                                                    /// </remarks>
                                                    public partial class GeneratedBotStrings
                                                    {
                                                        private readonly IBotStrings _strings;
                                                        private readonly ILocalization _localization;

                                                        /// <summary>
                                                        /// Initializes a new instance of the <see cref="GeneratedBotStrings"/> class.
                                                        /// </summary>
                                                        /// <param name="strings">The bot strings service that provides localization.</param>
                                                        /// <param name="localization">The localization service that handles culture resolution.</param>
                                                        public GeneratedBotStrings(IBotStrings strings, ILocalization localization)
                                                        {
                                                            _strings = strings;
                                                            _localization = localization;
                                                        }

                                                        /// <summary>
                                                        /// Gets the appropriate culture info for the specified guild.
                                                        /// </summary>
                                                        /// <param name="guildId">The ID of the guild, or null for default culture.</param>
                                                        /// <returns>The resolved CultureInfo for the guild or default.</returns>
                                                        private CultureInfo GetCultureInfo({{guildIdType}} guildId = null) =>
                                                            _localization.GetCultureInfo(guildId);

                                                """);

        // Get all unique keys across all locales
        var allKeys = allResponses.SelectMany(x => x.responses.Keys).Distinct().OrderBy(x => x);

        var methodNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var key in allKeys)
        {
            var pascalCaseKey = SnakeToPascalCase(key);

            // Ensure the method name is unique
            if (!methodNames.Add(pascalCaseKey))
            {
                var suffix = 1;
                string uniqueName;
                do
                {
                    uniqueName = pascalCaseKey + suffix++;
                } while (!methodNames.Add(uniqueName));

                pascalCaseKey = uniqueName;
            }

            // Get default (en-US) value for documentation
            var defaultResponse = allResponses.FirstOrDefault(x => x.locale == "en-US").responses;
            var defaultValue = defaultResponse != null && defaultResponse.ContainsKey(key)
                ? defaultResponse[key]
                : string.Empty;

            var (paramCount, sequential) = AnalyzeStringParameters(defaultValue);

            string parametersList, argumentsList;
            if (paramCount == 0)
            {
                // No parameters
                parametersList = $"{guildIdType} guildId";
                argumentsList = "Array.Empty<object>()";
            }
            else if (sequential)
            {
                // Sequential parameters
                parametersList =
                    $"{guildIdType} guildId, {string.Join(", ", Enumerable.Range(0, paramCount).Select(i => $"object param{i}"))}";
                argumentsList =
                    $"new object[] {{ {string.Join(", ", Enumerable.Range(0, paramCount).Select(i => $"param{i}"))} }}";
            }
            else
            {
                // Non-sequential parameters
                parametersList = $"{guildIdType} guildId, params object[] data";
                argumentsList = "data";
            }

            var supportedLocales = string.Join(", ",
                allResponses.Where(x => x.responses.ContainsKey(key))
                    .Select(x => x.locale));

            // Escape any potential code-like content in the documentation
            var escapedKey = SecurityElement.Escape(key);
            var escapedDefaultValue = SanitizeDocumentationValue(defaultValue);
            var escapedSupportedLocales = SecurityElement.Escape(supportedLocales);

            sourceBuilder.AppendLine($"""

                                              /// <summary>Gets the localized string for key "{escapedKey}"</summary>
                                              /// <remarks>
                                              /// Default (en-US): "{escapedDefaultValue}"
                                              /// Available in locales: {escapedSupportedLocales}
                                              /// Parameter count: {paramCount}
                                              /// </remarks>
                                              /// <param name="guildId">The guild ID for culture resolution, or null for default culture.</param>
                                              {(paramCount > 0 ? (sequential
                                                      ? string.Join("\n        ", Enumerable.Range(0, paramCount).Select(i => $"/// <param name=\"param{i}\">Format parameter {i}</param>"))
                                                      : "/// <param name=\"data\">Optional format parameters</param>")
                                                  : "")}
                                              /// <returns>The localized string with optional formatting applied.</returns>
                                              public string {pascalCaseKey}({parametersList}) =>
                                                  _strings.GetText(@"{key}", GetCultureInfo(guildId), {argumentsList});

                                      """);
        }

        sourceBuilder.AppendLine("    }"); // class close
        sourceBuilder.AppendLine("}"); // namespace close

        return SourceText.From(sourceBuilder.ToString(), Encoding.UTF8);
    }

    private static (int count, bool sequential) AnalyzeStringParameters(string input)
    {
        var matches = FormatArgsRegex.Matches(input);
        if (matches.Count == 0)
            return (0, false);

        var parameters = new List<int>();
        foreach (Match match in matches)
        {
            parameters.Add(int.Parse(match.Groups[1].Value));
        }

        parameters = parameters.Distinct().OrderBy(x => x).ToList();

        var sequential = parameters.Count > 0 &&
                         parameters[0] == 0 &&
                         parameters.Last() == parameters.Count - 1;

        return (parameters.Count, sequential);
    }

    private static bool IsReservedKeyword(string identifier)
    {
        // List of C# reserved keywords
        var reservedKeywords = new HashSet<string>
        {
            "abstract",
            "as",
            "base",
            "bool",
            "break",
            "byte",
            "case",
            "catch",
            "char",
            "checked",
            "class",
            "const",
            "continue",
            "decimal",
            "default",
            "delegate",
            "do",
            "double",
            "else",
            "enum",
            "event",
            "explicit",
            "extern",
            "false",
            "finally",
            "fixed",
            "float",
            "for",
            "foreach",
            "goto",
            "if",
            "implicit",
            "in",
            "int",
            "interface",
            "internal",
            "is",
            "lock",
            "long",
            "namespace",
            "new",
            "null",
            "object",
            "operator",
            "out",
            "override",
            "params",
            "private",
            "protected",
            "public",
            "readonly",
            "ref",
            "return",
            "sbyte",
            "sealed",
            "short",
            "sizeof",
            "stackalloc",
            "static",
            "string",
            "struct",
            "switch",
            "this",
            "throw",
            "true",
            "try",
            "typeof",
            "uint",
            "ulong",
            "unchecked",
            "unsafe",
            "ushort",
            "using",
            "virtual",
            "void",
            "volatile",
            "while"
        };

        return reservedKeywords.Contains(identifier);
    }

    private static bool IsValidIdentifier(string identifier)
    {
        if (string.IsNullOrEmpty(identifier))
            return false;

        // Must start with letter or underscore
        if (!char.IsLetter(identifier[0]) && identifier[0] != '_')
            return false;

        // Rest can be letters, digits, or underscores
        for (var i = 1; i < identifier.Length; i++)
        {
            if (!char.IsLetterOrDigit(identifier[i]) && identifier[i] != '_')
                return false;
        }

        return !IsReservedKeyword(identifier);
    }

    private static string SanitizeDocumentationValue(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        return SecurityElement.Escape(input)
            .Replace("\r\n", "\\n")
            .Replace("\n", "\\n")
            .Replace("\r", "\\n")
            .Replace("{", "{{")
            .Replace("}", "}}")
            .Replace("\"", "&quot;");
    }

    /// <summary>
    ///     Converts a snake_case string to PascalCase.
    /// </summary>
    /// <param name="input">The snake_case string to convert.</param>
    /// <returns>The PascalCase version of the input string.</returns>
    /// <remarks>
    ///     For example:
    ///     - "hello_world" becomes "HelloWorld"
    ///     - "my_snake_case_string" becomes "MySnakeCaseString"
    /// </remarks>
    private static string SnakeToPascalCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        // Dictionary for number words
        var numberWords = new Dictionary<string, string>
        {
            {
                "0", "Zero"
            },
            {
                "1", "One"
            },
            {
                "2", "Two"
            },
            {
                "3", "Three"
            },
            {
                "4", "Four"
            },
            {
                "5", "Five"
            },
            {
                "6", "Six"
            },
            {
                "7", "Seven"
            },
            {
                "8", "Eight"
            },
            {
                "9", "Nine"
            }
        };

        // Replace leading numbers with words
        var result = StartsWithDigitRegex.Replace(input, match =>
        {
            var numbers = match.Value.ToCharArray();
            return string.Concat(numbers.Select(n => numberWords[n.ToString()]));
        });

        // Replace remaining numbers and special characters
        result = ContainsDigitRegex.Replace(result, match => numberWords[match.Value]);

        // Remove invalid characters and split into words
        var words = InvalidCharsRegex.Replace(result, "_")
            .Split(['_', '-'], StringSplitOptions.RemoveEmptyEntries);

        // Convert to PascalCase
        var identifier = string.Join("", words.Select(word =>
            char.ToUpperInvariant(word[0]) + word.Substring(1).ToLowerInvariant()));

        // Handle empty or invalid starting character
        if (string.IsNullOrEmpty(identifier) || (!char.IsLetter(identifier[0]) && identifier[0] != '_'))
        {
            identifier = "Response" + identifier;
        }

        // Handle reserved keywords
        if (IsReservedKeyword(identifier))
        {
            identifier = "Response" + identifier;
        }

        // Handle duplicates by adding a suffix if needed
        var finalIdentifier = identifier;
        var suffix = 1;
        while (!IsValidIdentifier(finalIdentifier))
        {
            finalIdentifier = identifier + suffix++;
        }

        return finalIdentifier;
    }

    /// <summary>
    ///     Simple JSON parser that extracts string key-value pairs from a JSON object.
    ///     This is a minimal implementation for source generators targeting netstandard2.0.
    /// </summary>
    /// <param name="json">The JSON string to parse.</param>
    /// <returns>A dictionary of string keys to string values.</returns>
    private static Dictionary<string, string> ParseJsonToDictionary(string json)
    {
        var result = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(json))
            return result;

        // Remove whitespace and brackets
        json = json.Trim();
        if (json.StartsWith("{") && json.EndsWith("}"))
        {
            json = json.Substring(1, json.Length - 2);
        }

        // Simple state machine to parse key-value pairs
        var inString = false;
        var escaped = false;
        var currentKey = "";
        var currentValue = "";
        var parsingKey = true;
        var currentStr = new StringBuilder();

        for (var i = 0; i < json.Length; i++)
        {
            var c = json[i];

            if (escaped)
            {
                // Handle escaped characters
                switch (c)
                {
                    case 'n':
                        currentStr.Append('\n');
                        break;
                    case 'r':
                        currentStr.Append('\r');
                        break;
                    case 't':
                        currentStr.Append('\t');
                        break;
                    case '\\':
                        currentStr.Append('\\');
                        break;
                    case '"':
                        currentStr.Append('"');
                        break;
                    case '/':
                        currentStr.Append('/');
                        break;
                    default:
                        currentStr.Append(c);
                        break;
                }

                escaped = false;
                continue;
            }

            switch (c)
            {
                case '\\' when inString:
                    escaped = true;
                    continue;
                case '"':
                {
                    if (inString)
                    {
                        // End of string
                        if (parsingKey)
                        {
                            currentKey = currentStr.ToString();
                        }
                        else
                        {
                            currentValue = currentStr.ToString();
                        }

                        currentStr.Clear();
                    }

                    inString = !inString;
                    continue;
                }
            }

            if (inString)
            {
                currentStr.Append(c);
                continue;
            }

            // Skip whitespace outside strings
            if (char.IsWhiteSpace(c))
                continue;

            switch (c)
            {
                case ':' when parsingKey:
                    parsingKey = false;
                    continue;
                case ',':
                {
                    if (!string.IsNullOrEmpty(currentKey))
                    {
                        result[currentKey] = currentValue ?? "";
                    }

                    currentKey = "";
                    currentValue = "";
                    parsingKey = true;
                    break;
                }
            }
        }

        // Process the final key-value pair if it exists
        if (!string.IsNullOrEmpty(currentKey))
        {
            result[currentKey] = currentValue ?? "";
        }

        return result;
    }
}