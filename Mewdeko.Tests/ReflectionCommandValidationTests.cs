using System.Reflection;
using System.Text.Json;
using Mewdeko.Common.Attributes.TextCommands;
using YamlDotNet.Serialization;

namespace Mewdeko.Tests;

/// <summary>
///     Simple reflection-based tests to validate command attributes and localization.
/// </summary>
[TestFixture]
public class ReflectionCommandValidationTests
{
    [OneTimeSetUp]
    public void SetUp()
    {
        var projectRoot = GetProjectRoot();
        LoadDataFiles(projectRoot);
        commandMethods = GetAllCommandMethods();
    }

    private static Dictionary<string, string[]>? aliasesData;
    private static Dictionary<string, object>? commandStringsData;
    private static List<MethodInfo>? commandMethods;
    private static Dictionary<string, object>? responseStringsData;
    private static Type? generatedBotStringsType;

    /// <summary>
    ///     Every method with [Cmd] must also have [Aliases].
    /// </summary>
    [Test]
    public void EveryCommandMethodHasBothAttributes()
    {
        var failures = new List<string>();

        foreach (var method in commandMethods!)
        {
            var hasCmd = method.GetCustomAttribute<Cmd>() != null;
            var hasAliases = method.GetCustomAttribute<AliasesAttribute>() != null;

            if (hasCmd && !hasAliases)
            {
                failures.Add($"Method '{method.DeclaringType?.Name}.{method.Name}' has [Cmd] but missing [Aliases]");
            }

            if (hasAliases && !hasCmd)
            {
                failures.Add($"Method '{method.DeclaringType?.Name}.{method.Name}' has [Aliases] but missing [Cmd]");
            }
        }

        Assert.That(failures, Is.Empty,
            "Found methods with mismatched attributes:\n" + string.Join("\n", failures));
    }

    /// <summary>
    ///     Every command method must have an entry in aliases.yml.
    /// </summary>
    [Test]
    public void EveryCommandMethodHasAliasEntry()
    {
        var failures = new List<string>();

        foreach (var method in commandMethods!.Where(m => m.GetCustomAttribute<Cmd>() != null))
        {
            var methodName = method.Name.ToLowerInvariant();

            if (!aliasesData!.TryGetValue(methodName, out var value))
            {
                failures.Add($"Method '{method.DeclaringType?.Name}.{method.Name}' missing from aliases.yml");
            }
            else if (value.Length == 0)
            {
                failures.Add($"Method '{method.DeclaringType?.Name}.{method.Name}' has empty aliases in aliases.yml");
            }
        }

        Assert.That(failures, Is.Empty,
            "Found command methods missing from aliases.yml:\n" + string.Join("\n", failures));
    }

    /// <summary>
    ///     Every command must have localization strings.
    /// </summary>
    [Test]
    public void EveryCommandHasLocalizationStrings()
    {
        var failures = new List<string>();

        foreach (var method in commandMethods!.Where(m => m.GetCustomAttribute<Cmd>() != null))
        {
            var methodName = method.Name.ToLowerInvariant();

            if (!aliasesData!.TryGetValue(methodName, out var aliases) || aliases.Length == 0)
                continue; // Will be caught by previous test

            var primaryCommand = aliases[0];

            if (!commandStringsData!.ContainsKey(primaryCommand))
            {
                failures.Add(
                    $"Command '{primaryCommand}' (method: {method.DeclaringType?.Name}.{method.Name}) missing from commands.en-US.yml");
            }
            else if (commandStringsData[primaryCommand] is Dictionary<object, object> cmdData)
            {
                if (!cmdData.ContainsKey("desc") || string.IsNullOrWhiteSpace(cmdData["desc"].ToString()))
                {
                    failures.Add(
                        $"Command '{primaryCommand}' (method: {method.DeclaringType?.Name}.{method.Name}) missing or empty description");
                }
            }
        }

        Assert.That(failures, Is.Empty,
            "Found commands missing localization:\n" + string.Join("\n", failures));
    }

    /// <summary>
    ///     Every entry in aliases.yml should have a corresponding command method.
    /// </summary>
    [Test]
    public void EveryAliasEntryHasCommandMethod()
    {
        var methodNames = commandMethods!
            .Where(m => m.GetCustomAttribute<Cmd>() != null)
            .Select(m => m.Name.ToLowerInvariant())
            .ToHashSet();

        var failures = (from aliasKey in aliasesData!.Keys
            where !methodNames.Contains(aliasKey)
            select $"Alias entry '{aliasKey}' in aliases.yml has no corresponding command method").ToList();

        Assert.That(failures, Is.Empty,
            "Found orphaned alias entries:\n" + string.Join("\n", failures));
    }

    /// <summary>
    ///     No duplicate primary command names in aliases.yml.
    /// </summary>
    [Test]
    public void NoDuplicatePrimaryCommandNames()
    {
        var failures = new List<string>();
        var primaryCommands = new Dictionary<string, List<string>>();

        foreach (var (aliasKey, aliases) in aliasesData!)
        {
            if (aliases.Length == 0) continue;

            var primaryCommand = aliases[0];
            primaryCommands.TryAdd(primaryCommand, []);
            primaryCommands[primaryCommand].Add(aliasKey);
        }

        foreach (var (primaryCommand, methods) in primaryCommands.Where(kvp => kvp.Value.Count > 1))
        {
            failures.Add($"Primary command '{primaryCommand}' used by multiple methods: {string.Join(", ", methods)}");
        }

        Assert.That(failures, Is.Empty,
            "Found duplicate primary command names:\n" + string.Join("\n", failures));
    }

    /// <summary>
    ///     All response strings should be used somewhere in the codebase.
    /// </summary>
    [Test]
    public void AllResponseStringsAreUsed()
    {
        var failures = new List<string>();

        if (responseStringsData == null)
        {
            Assert.Inconclusive(
                "Response strings data not loaded - responses.en-US.json may not exist or be accessible");
            return;
        }

        if (generatedBotStringsType == null)
        {
            Assert.Inconclusive(
                "GeneratedBotStrings type not found - this test requires the main assembly to be loaded");
            return;
        }

        // Check each key in the response file
        foreach (var responseKey in responseStringsData.Keys.Where(responseKey =>
                     !responseKey.StartsWith("__loctest", StringComparison.OrdinalIgnoreCase)))
        {
            // Check for keys that would generate invalid C# method names
            // Note: keys starting with digits (like "8ball") are valid - source generator converts them
            if (string.IsNullOrWhiteSpace(responseKey) ||
                responseKey.Contains(':') ||
                responseKey.Contains(' ') ||
                responseKey.Contains('-') ||
                responseKey.Contains('.'))
            {
                failures.Add($"Response key '{responseKey}' has invalid format for C# method generation");
                continue;
            }

            // Convert the key to the expected method name
            var expectedMethodName = SnakeToPascalCase(responseKey);

            // Verify the method actually exists (sanity check)
            var method = generatedBotStringsType.GetMethod(expectedMethodName);
            if (method == null)
            {
                failures.Add(
                    $"Response key '{responseKey}' should generate method '{expectedMethodName}' but method not found");
                continue;
            }

            // Search for usage of this method in the codebase
            if (!IsMethodUsedInCodebase(expectedMethodName))
            {
                failures.Add(
                    $"Response key '{responseKey}' (method: {expectedMethodName}) is not used in the codebase");
            }
        }

        // Output unused keys to a file
        if (failures.Count > 0)
        {
            var projectRoot = GetProjectRoot();
            var unusedKeysFile = Path.Combine(projectRoot, "data", "strings", "responses", "unused_keys.txt");

            var unusedKeys = failures
                .Where(f => f.Contains("is not used in the codebase") || f.Contains("has invalid format"))
                .Select(failure =>
                {
                    // Extract the key from the failure message
                    var keyStart = failure.IndexOf("key '", StringComparison.Ordinal) + 5;
                    var keyEnd = failure.IndexOf('\'', keyStart);
                    return failure.Substring(keyStart, keyEnd - keyStart);
                }).ToList();

            File.WriteAllLines(unusedKeysFile, unusedKeys);
        }

        Assert.That(failures, Is.Empty,
            "Found unused response strings:\n" + string.Join("\n", failures));
    }

    /// <summary>
    ///     All commands in commands.yml should have corresponding entries in aliases.yml.
    /// </summary>
    [Test]
    public void AllCommandStringsHaveAliases()
    {
        if (commandStringsData == null || aliasesData == null)
        {
            Assert.Fail("Command strings or aliases data not loaded");
            return;
        }

        // Get unique base command names (part before first underscore)
        var baseCommands = commandStringsData.Keys
            .Select(key => key.Split('_')[0])
            .Distinct()
            .ToList();

        var failures = (from baseCommandKey in baseCommands
                let foundAsKey =
                    aliasesData.Keys.Any(aliasKey =>
                        string.Equals(aliasKey, baseCommandKey, StringComparison.OrdinalIgnoreCase))
                let foundAsFirstValue =
                    aliasesData.Values.Any(aliases =>
                        aliases.Length > 0 &&
                        string.Equals(aliases[0], baseCommandKey, StringComparison.OrdinalIgnoreCase))
                where !foundAsKey && !foundAsFirstValue
                select
                    $"Command '{baseCommandKey}' in commands.yml has no corresponding key or first alias in aliases.yml")
            .ToList();

        Assert.That(failures, Is.Empty,
            "Found commands without aliases:\n" + string.Join("\n", failures));
    }

    /// <summary>
    ///     Get all methods with command attributes using reflection.
    /// </summary>
    private static List<MethodInfo> GetAllCommandMethods()
    {
        var methods = new List<MethodInfo>();

        // Try to get the Mewdeko assembly from loaded assemblies
        var mewdekoAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "Mewdeko");

        if (mewdekoAssembly == null)
        {
            // Try to load it from a type we know exists
            try
            {
                mewdekoAssembly = typeof(Cmd).Assembly;
            }
            catch
            {
                throw new InvalidOperationException(
                    "Could not find Mewdeko assembly. Make sure the project is referenced and built.");
            }
        }

        // Find all types in the Modules namespace
        try
        {
            var moduleTypes = mewdekoAssembly.GetTypes()
                .Where(t => t.Namespace?.StartsWith("Mewdeko.Modules") == true);

            foreach (var type in moduleTypes)
            {
                var typeMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => m.GetCustomAttribute<Cmd>() != null ||
                                m.GetCustomAttribute<AliasesAttribute>() != null);

                methods.AddRange(typeMethods);
            }
        }
        catch (ReflectionTypeLoadException ex)
        {
            // If we can't load all types, work with what we can
            var loadedTypes = ex.Types.Where(t => t != null && t.Namespace?.StartsWith("Mewdeko.Modules") == true);

            foreach (var type in loadedTypes)
            {
                try
                {
                    var typeMethods = type!.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                        .Where(m => m.GetCustomAttribute<Cmd>() != null ||
                                    m.GetCustomAttribute<AliasesAttribute>() != null);

                    methods.AddRange(typeMethods);
                }
                catch
                {
                    // Skip types that can't be inspected
                }
            }
        }

        return methods;
    }

    /// <summary>
    ///     Convert snake_case to PascalCase, handling numbers like the source generator does.
    /// </summary>
    private static string SnakeToPascalCase(string snakeCase)
    {
        var numberWords = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
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

        // Split by underscore and process each part
        var parts = snakeCase.Split('_').Select(part =>
        {
            if (string.IsNullOrEmpty(part)) return "";

            // Process the part character by character to handle numbers anywhere
            var result = "";
            foreach (var c in part)
            {
                if (char.IsDigit(c) && numberWords.TryGetValue(c.ToString(), out var word))
                {
                    result += word;
                }
                else
                {
                    result += c;
                }
            }

            // Capitalize the first letter of the result
            return result.Length > 0 ? char.ToUpper(result[0]) + result[1..].ToLower() : "";
        });

        return string.Join("", parts);
    }

    private static Dictionary<string, string>? codebaseContent;

    /// <summary>
    ///     Load all codebase content once for faster searching.
    /// </summary>
    private static void LoadCodebaseContent()
    {
        if (codebaseContent != null) return;

        codebaseContent = new Dictionary<string, string>();
        var projectRoot = GetProjectRoot();
        var searchDirs = new[]
        {
            "Modules", "Services", "Controllers", "Common"
        };

        foreach (var dir in searchDirs)
        {
            var fullDir = Path.Combine(projectRoot, dir);
            if (!Directory.Exists(fullDir)) continue;

            foreach (var file in Directory.GetFiles(fullDir, "*.cs", SearchOption.AllDirectories))
            {
                try
                {
                    codebaseContent[file] = File.ReadAllText(file);
                }
                catch
                {
                    // Skip files we can't read
                }
            }
        }
    }

    /// <summary>
    ///     Check if a method is used anywhere in the codebase (using preloaded content).
    /// </summary>
    private static bool IsMethodUsedInCodebase(string methodName)
    {
        LoadCodebaseContent();

        var searchPattern = $"{methodName}(";
        return codebaseContent!.Values.Any(content => content.Contains(searchPattern));
    }

    private static void LoadDataFiles(string projectRoot)
    {
        // Load aliases.yml
        var aliasesPath = Path.Combine(projectRoot, "data", "aliases.yml");
        var aliasesYaml = File.ReadAllText(aliasesPath);
        aliasesData = new Deserializer().Deserialize<Dictionary<string, string[]>>(aliasesYaml);

        // Load all per-module command YAML files from data/strings/commands/en-US/
        var commandStringsDir = Path.Combine(projectRoot, "data", "strings", "commands", "en-US");
        commandStringsData = new Dictionary<string, object>();
        var deserializer = new Deserializer();
        foreach (var yamlFile in Directory.EnumerateFiles(commandStringsDir, "*.yml", SearchOption.TopDirectoryOnly))
        {
            var yaml = File.ReadAllText(yamlFile);
            if (string.IsNullOrWhiteSpace(yaml)) continue;
            var parsed = deserializer.Deserialize<Dictionary<string, object>>(yaml);
            if (parsed == null) continue;
            foreach (var kvp in parsed)
                commandStringsData[kvp.Key] = kvp.Value;
        }

        // Load responses from per-module split files in data/strings/responses/en-US/
        var responsesDir = Path.Combine(projectRoot, "data", "strings", "responses", "en-US");
        if (Directory.Exists(responsesDir))
        {
            responseStringsData = new Dictionary<string, object>();
            foreach (var file in Directory.EnumerateFiles(responsesDir, "*.json", SearchOption.TopDirectoryOnly))
            {
                var moduleJson = File.ReadAllText(file);
                if (string.IsNullOrWhiteSpace(moduleJson)) continue;
                var moduleDict = JsonSerializer.Deserialize<Dictionary<string, object>>(moduleJson);
                if (moduleDict == null) continue;
                foreach (var kvp in moduleDict)
                    responseStringsData[kvp.Key] = kvp.Value;
            }
        }

        // Load GeneratedBotStrings type via reflection
        try
        {
            var mewdekoAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Mewdeko");

            if (mewdekoAssembly == null)
            {
                // Try to load from the attribute assembly we know exists
                mewdekoAssembly = typeof(Cmd).Assembly;
            }

            generatedBotStringsType = mewdekoAssembly.GetTypes()
                .FirstOrDefault(t => t.Name == "GeneratedBotStrings");
        }
        catch
        {
            // If we can't load the type, that's okay for now
        }
    }

    private static string GetProjectRoot()
    {
        var currentDir = Directory.GetCurrentDirectory();
        while (!Directory.Exists(Path.Combine(currentDir, "src", "Mewdeko", "Modules")))
        {
            currentDir = Directory.GetParent(currentDir)?.FullName
                         ?? throw new DirectoryNotFoundException("Could not find project root");
        }

        return Path.Combine(currentDir, "src", "Mewdeko");
    }
}