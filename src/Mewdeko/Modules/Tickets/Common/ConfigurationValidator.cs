namespace Mewdeko.Modules.Tickets.Common;

/// <summary>
///     Validation helper for user configurations
/// </summary>
public static class ConfigurationValidator
{
    /// <summary>
    ///     Validates button configuration and returns issues
    /// </summary>
    /// <param name="basicSettings">Basic button settings</param>
    /// <param name="modalConfig">Modal configuration</param>
    /// <param name="behaviorSettings">Behavior settings</param>
    /// <returns>List of validation issues</returns>
    public static List<string> ValidateButtonConfiguration(
        Dictionary<string, string> basicSettings,
        string modalConfig,
        Dictionary<string, string> behaviorSettings)
    {
        var issues = new List<string>();

        // Validate required fields
        if (!basicSettings.ContainsKey("label") || string.IsNullOrEmpty(basicSettings["label"]))
            issues.Add("Button label is required");

        if (basicSettings.TryGetValue("style", out var setting))
        {
            var style = setting.ToLower();
            if (!new[]
                {
                    "primary", "secondary", "success", "danger"
                }.Contains(style))
                issues.Add("Button style must be: primary, secondary, success, or danger");
        }

        // Validate modal configuration
        if (!string.IsNullOrEmpty(modalConfig))
        {
            var modalValidation = ValidateModalConfiguration(modalConfig);
            issues.AddRange(modalValidation);
        }

        // Validate behavior settings
        if (behaviorSettings.TryGetValue("auto_close_hours", out var behaviorSetting))
        {
            if (!int.TryParse(behaviorSetting, out var hours) || hours < 1 || hours > 168)
                issues.Add("Auto-close hours must be between 1 and 168 (1 week)");
        }

        if (behaviorSettings.TryGetValue("response_time_minutes", out var setting1))
        {
            if (!int.TryParse(setting1, out var minutes) || minutes < 1)
                issues.Add("Response time must be at least 1 minute");
        }

        return issues;
    }

    /// <summary>
    ///     Validates modal configuration
    /// </summary>
    /// <param name="modalConfig">The modal configuration to validate</param>
    /// <returns>List of validation issues</returns>
    private static List<string> ValidateModalConfiguration(string modalConfig)
    {
        var issues = new List<string>();
        var lines = modalConfig.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var fieldCount = lines.Count(l => l.StartsWith("- "));

        if (fieldCount > 5)
            issues.Add("Modal can have maximum 5 fields");

        foreach (var line in lines.Where(l => l.StartsWith("- ")))
        {
            var fieldParts = line[2..].Split('|');
            if (fieldParts.Length < 2)
                issues.Add($"Invalid field format: {line}. Use: Label|Type|Required");
            else if (!new[]
                     {
                         "short", "long"
                     }.Contains(fieldParts[1].ToLower()))
                issues.Add($"Field type must be 'short' or 'long': {fieldParts[1]}");
        }

        return issues;
    }

    /// <summary>
    ///     Validates select menu configuration
    /// </summary>
    /// <param name="options">The options configuration</param>
    /// <returns>List of validation issues</returns>
    public static List<string> ValidateSelectMenuConfiguration(string options)
    {
        var issues = new List<string>();
        var lines = options.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        switch (lines.Length)
        {
            case 0:
                issues.Add("Select menu must have at least one option");
                break;
            case > 25:
                issues.Add("Select menu can have maximum 25 options");
                break;
        }

        foreach (var line in lines)
        {
            var parts = line.Split('|');
            if (parts.Length < 2)
                issues.Add($"Invalid option format: {line}. Use: Label|Emoji|Description");
            else if (parts[0].Length > 100)
                issues.Add($"Option label too long (max 100 chars): {parts[0]}");
            else if (parts.Length > 2 && parts[2].Length > 100)
                issues.Add($"Option description too long (max 100 chars): {parts[2]}");
        }

        return issues;
    }
}