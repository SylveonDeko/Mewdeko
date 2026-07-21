using System.Text.RegularExpressions;

namespace Mewdeko.Modules.Switch.Common;

/// <summary>
///     Identifies which Ryujinx build produced a log file.
/// </summary>
public enum RyujinxBuildKind
{
    /// <summary>
    ///     A recent mainline release (e.g. 1.2.123).
    /// </summary>
    Master,

    /// <summary>
    ///     An older mainline release using the four-part version scheme.
    /// </summary>
    OldMaster,

    /// <summary>
    ///     An LDN (local multiplayer) fork build.
    /// </summary>
    Ldn,

    /// <summary>
    ///     A macOS-specific build.
    /// </summary>
    Mac,

    /// <summary>
    ///     A pull request / CI build.
    /// </summary>
    Pr,

    /// <summary>
    ///     A custom or unrecognized build.
    /// </summary>
    Custom
}

/// <summary>
///     How severe a <see cref="RyujinxLogNote" /> is. Used to sort notes and to pick a status icon for display.
/// </summary>
public enum RyujinxNoteSeverity
{
    /// <summary>
    ///     Everything looks correct.
    /// </summary>
    Ok,

    /// <summary>
    ///     Informational only, no action needed.
    /// </summary>
    Info,

    /// <summary>
    ///     Worth double-checking, may cause minor issues.
    /// </summary>
    Warning,

    /// <summary>
    ///     Likely to cause noticeable problems.
    /// </summary>
    Error,

    /// <summary>
    ///     Almost certainly the cause of the reported issue.
    /// </summary>
    Critical
}

/// <summary>
///     The kind of observation made about a Ryujinx log, used to look up a localized message.
/// </summary>
public enum RyujinxNoteKind
{
    /// <summary>A shader cache collision was detected.</summary>
    ShaderCacheCollision,

    /// <summary>A dump/firmware hash validation error was detected.</summary>
    DumpHashError,

    /// <summary>Shader cache corruption was detected.</summary>
    ShaderCacheCorruption,

    /// <summary>Keys or firmware appear to be out of date.</summary>
    KeysOutdated,

    /// <summary>A save data file permission error was detected.</summary>
    FilePermissionError,

    /// <summary>A missing save data error was detected.</summary>
    SaveNotFound,

    /// <summary>A missing service exception was detected.</summary>
    MissingServices,

    /// <summary>Vulkan reported it ran out of device memory.</summary>
    VulkanOutOfMemory,

    /// <summary>How much time had elapsed by the end of the log. Args[0] is the timestamp.</summary>
    TimeElapsed,

    /// <summary>The log was produced using the default user profile.</summary>
    DefaultUserProfile,

    /// <summary>Controller configuration lines were found. Args contains one entry per controller.</summary>
    ControllerInfo,

    /// <summary>No controller configuration was found even though a game was loaded.</summary>
    NoControllerInfo,

    /// <summary>An Intel iGPU was detected on Windows without Vulkan.</summary>
    IntelVulkanRecommended,

    /// <summary>An AMD GPU was detected on Windows without Vulkan.</summary>
    AmdVulkanRecommended,

    /// <summary>The CPU is reported as Rosetta-translated.</summary>
    RosettaShouldBeDisabled,

    /// <summary>No firmware was found despite a game being loaded.</summary>
    FirmwareNotFound,

    /// <summary>The Dummy audio backend is in use.</summary>
    DummyAudioBackend,

    /// <summary>PPTC caching is disabled.</summary>
    PptcDisabled,

    /// <summary>Shader caching is disabled.</summary>
    ShaderCacheDisabled,

    /// <summary>The alternative (expanded) memory layout is enabled.</summary>
    ExpandRamEnabled,

    /// <summary>The software page table memory manager is in use.</summary>
    SoftwareMemoryManager,

    /// <summary>Missing service errors are being ignored.</summary>
    IgnoreMissingServicesEnabled,

    /// <summary>V-Sync is disabled.</summary>
    VsyncDisabled,

    /// <summary>File system integrity checks are disabled.</summary>
    FsIntegrityDisabled,

    /// <summary>Graphics backend multithreading is turned off.</summary>
    BackendThreadingOff,

    /// <summary>The log was produced by a custom/unofficial build.</summary>
    CustomBuild
}

/// <summary>
///     An observation made about a Ryujinx log, ready to be turned into a localized display string.
/// </summary>
/// <param name="Kind">The kind of observation.</param>
/// <param name="Severity">How severe the observation is.</param>
/// <param name="Args">Extra data referenced by the note's message format, if any.</param>
public sealed record RyujinxLogNote(
    RyujinxNoteKind Kind,
    RyujinxNoteSeverity Severity,
    IReadOnlyList<string>? Args = null);

/// <summary>
///     Hardware information extracted from a Ryujinx log. Properties are <c>null</c> when not found.
/// </summary>
public sealed class RyujinxHardwareInfo
{
    /// <summary>
    ///     Gets or sets the reported CPU name.
    /// </summary>
    public string? Cpu { get; set; }

    /// <summary>
    ///     Gets or sets the reported GPU name.
    /// </summary>
    public string? Gpu { get; set; }

    /// <summary>
    ///     Gets or sets the reported available/total RAM, formatted in MiB.
    /// </summary>
    public string? Ram { get; set; }

    /// <summary>
    ///     Gets or sets the reported operating system.
    /// </summary>
    public string? Os { get; set; }
}

/// <summary>
///     Emulator information extracted from a Ryujinx log. Properties are <c>null</c> when not found.
/// </summary>
public sealed class RyujinxEmulatorInfo
{
    /// <summary>
    ///     Gets or sets the Ryujinx version string.
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    ///     Gets or sets the loaded firmware version.
    /// </summary>
    public string? Firmware { get; set; }

    /// <summary>
    ///     Gets or sets the comma-separated list of enabled log classes, if found.
    /// </summary>
    public string? LogsEnabled { get; set; }
}

/// <summary>
///     Emulator settings extracted from a Ryujinx log. Toggle-like settings are exposed as <see cref="bool" />?
///     (<c>null</c> when not found); free-form settings keep the raw value Ryujinx logged so the caller can decide
///     how to present it.
/// </summary>
public sealed class RyujinxSettings
{
    /// <summary>
    ///     Gets or sets the raw configured audio backend (e.g. "SDL2", "Dummy").
    /// </summary>
    public string? AudioBackend { get; set; }

    /// <summary>
    ///     Gets or sets the raw graphics backend multithreading mode (e.g. "Auto", "Off", "On").
    /// </summary>
    public string? BackendThreading { get; set; }

    /// <summary>
    ///     Gets or sets whether the console is running in docked mode (as opposed to handheld).
    /// </summary>
    public bool? Docked { get; set; }

    /// <summary>
    ///     Gets or sets whether the alternative memory layout (expanded RAM) is enabled.
    /// </summary>
    public bool? ExpandRam { get; set; }

    /// <summary>
    ///     Gets or sets whether file system integrity checks are enabled.
    /// </summary>
    public bool? FsIntegrity { get; set; }

    /// <summary>
    ///     Gets or sets the raw configured graphics backend (e.g. "Vulkan", "OpenGl").
    /// </summary>
    public string? GraphicsBackend { get; set; }

    /// <summary>
    ///     Gets or sets whether missing services are ignored instead of throwing.
    /// </summary>
    public bool? IgnoreMissingServices { get; set; }

    /// <summary>
    ///     Gets or sets the raw configured memory manager mode (e.g. "HostMapped", "SoftwarePageTable").
    /// </summary>
    public string? MemoryManager { get; set; }

    /// <summary>
    ///     Gets or sets whether the PPTC cache is enabled.
    /// </summary>
    public bool? Pptc { get; set; }

    /// <summary>
    ///     Gets or sets whether the shader cache is enabled.
    /// </summary>
    public bool? ShaderCache { get; set; }

    /// <summary>
    ///     Gets or sets whether V-Sync is enabled.
    /// </summary>
    public bool? VSync { get; set; }

    /// <summary>
    ///     Gets or sets whether the hypervisor is enabled. Only meaningful when running on macOS.
    /// </summary>
    public bool? Hypervisor { get; set; }

    /// <summary>
    ///     Gets or sets whether texture recompression is enabled.
    /// </summary>
    public bool? TextureRecompression { get; set; }

    /// <summary>
    ///     Gets or sets the raw resolution scale setting value (e.g. "-1", "1", "2", "3", "4").
    /// </summary>
    public string? ResolutionScale { get; set; }

    /// <summary>
    ///     Gets or sets the raw anisotropic filtering setting value (e.g. "-1", "2", "4", "8", "16").
    /// </summary>
    public string? AnisotropicFiltering { get; set; }

    /// <summary>
    ///     Gets or sets the raw aspect ratio setting value (e.g. "Fixed16x9", "Stretched").
    /// </summary>
    public string? AspectRatio { get; set; }
}

/// <summary>
///     A single mod detected in a Ryujinx log.
/// </summary>
/// <param name="Name">The mod's name.</param>
/// <param name="IsExeFs">Whether the mod patches ExeFS (as opposed to RomFS).</param>
public sealed record RyujinxModInfo(string Name, bool IsExeFs);

/// <summary>
///     The result of analysing a Ryujinx log file.
/// </summary>
public sealed class RyujinxLogAnalysis
{
    /// <summary>
    ///     Gets the hardware info extracted from the log.
    /// </summary>
    public RyujinxHardwareInfo Hardware { get; } = new();

    /// <summary>
    ///     Gets the emulator info extracted from the log.
    /// </summary>
    public RyujinxEmulatorInfo Emulator { get; } = new();

    /// <summary>
    ///     Gets the emulator settings extracted from the log.
    /// </summary>
    public RyujinxSettings Settings { get; } = new();

    /// <summary>
    ///     Gets the name of the game that was loaded, or <c>null</c> if none was detected.
    /// </summary>
    public string? GameName { get; set; }

    /// <summary>
    ///     Gets the last error block found in the log (raw, un-truncated lines), or <c>null</c> if no errors were
    ///     found.
    /// </summary>
    public IReadOnlyList<string>? LastError { get; set; }

    /// <summary>
    ///     Gets the enabled mods found in the log, capped to 5 entries; <see cref="ModOverflowCount" /> holds how
    ///     many more were found beyond that.
    /// </summary>
    public IReadOnlyList<RyujinxModInfo> Mods { get; set; } = [];

    /// <summary>
    ///     Gets how many mods beyond the first 5 were found.
    /// </summary>
    public int ModOverflowCount { get; set; }

    /// <summary>
    ///     Gets the installed cheats found in the log, capped to 5 entries; <see cref="CheatOverflowCount" /> holds
    ///     how many more were found beyond that.
    /// </summary>
    public IReadOnlyList<string> Cheats { get; set; } = [];

    /// <summary>
    ///     Gets how many cheats beyond the first 5 were found.
    /// </summary>
    public int CheatOverflowCount { get; set; }

    /// <summary>
    ///     Gets the sorted list of notes/warnings about the log, most severe first.
    /// </summary>
    public IReadOnlyList<RyujinxLogNote> Notes { get; set; } = [];

    /// <summary>
    ///     Gets which Ryujinx build kind produced the log.
    /// </summary>
    public RyujinxBuildKind BuildKind { get; set; }
}

/// <summary>
///     Parses Ryujinx log files and extracts hardware, settings and troubleshooting information,
///     mirroring the analysis performed by https://codeberg.org/TSRBerry/ryuko-ng.
/// </summary>
public sealed class RyujinxLogAnalyser
{
    private static readonly Regex TimestampHeaderRegex =
        new(@"\d{2}:\d{2}:\d{2}\.\d{3}.*", RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex TimestampLineRegex = new(@"(\d{2}:\d{2}:\d{2}\.\d{3})\s+?\|", RegexOptions.Compiled);
    private static readonly Regex CpuRegex = new(@"CPU:\s([^;\n\r]*)", RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly Regex OsRegex = new(@"Operating System:\s([^;\n\r]*)",
        RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly Regex GpuRegex = new(@"PrintGpuInformation:\s([^;\n\r]*)",
        RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly Regex RamRegex = new(
        @"RAM: Total ([\d.]+) (KB|KiB|MB|MiB|GB|GiB) ; Available ([\d.]+) (KB|KiB|MB|MiB|GB|GiB)",
        RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly Regex LogsEnabledRegex =
        new(@"Logs Enabled:\s([^;\n\r]*)", RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly Regex AppNameRegex = new(@"Loader [A-Za-z]*: Application Loaded:\s([^;\n\r]*)",
        RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly Regex ModsRegex = new(@"Found\s(enabled|disabled)?\s?mod\s'(.+?)'\s(\[.+?\])",
        RegexOptions.Compiled);

    private static readonly Regex CheatsRegex = new(
        @"Installing cheat\s'(.+)'(?!\s\d{2}:\d{2}:\d{2}\.\d{3}\s\|E\|\sTamperMachine\sCompile)",
        RegexOptions.Compiled);

    private static readonly Regex ControllersRegex = new(@"Hid Configure: ([^\r\n]+)", RegexOptions.Compiled);

    private static readonly Dictionary<string, double> SizeUnitBytes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["KB"] = 1_000,
        ["KiB"] = 1024,
        ["MB"] = 1_000_000,
        ["MiB"] = 1024 * 1024,
        ["GB"] = 1_000_000_000,
        ["GiB"] = 1024L * 1024 * 1024
    };

    private static readonly (string Setting, string Key)[] BoolSettingsMap =
    [
        ("Docked", "EnableDockedMode"), ("ExpandRam", "ExpandRam"), ("FsIntegrity", "EnableFsIntegrityChecks"),
        ("IgnoreMissingServices", "IgnoreMissingServices"), ("Pptc", "EnablePtc"),
        ("ShaderCache", "EnableShaderCache"), ("VSync", "EnableVsync"), ("Hypervisor", "UseHypervisor"),
        ("TextureRecompression", "EnableTextureRecompression")
    ];

    private static readonly (string Setting, string Key)[] RawSettingsMap =
    [
        ("AudioBackend", "AudioBackend"), ("GraphicsBackend", "GraphicsBackend"),
        ("MemoryManager", "MemoryManagerMode"), ("ResolutionScale", "ResScale"),
        ("AnisotropicFiltering", "MaxAnisotropy"), ("AspectRatio", "AspectRatio"),
        ("BackendThreading", "BackendThreading")
    ];

    private readonly List<List<string>> errorBlocks = [];
    private readonly string logText;
    private readonly List<RyujinxLogNote> notes = [];
    private readonly RyujinxLogAnalysis result = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="RyujinxLogAnalyser" /> class and immediately parses the
    ///     given log text.
    /// </summary>
    /// <param name="logText">The raw contents of a Ryujinx log file.</param>
    /// <exception cref="FormatException">Thrown when the text doesn't contain any recognizable log entries.</exception>
    public RyujinxLogAnalyser(string logText)
    {
        var normalized = logText.Replace("\r\n", "\n");
        var headerMatch = TimestampHeaderRegex.Match(normalized);
        if (!headerMatch.Success)
            throw new FormatException("No log entries found.");

        this.logText = headerMatch.Value;

        ParseErrors();
        ParseHardwareInfo();
        ParseEmulatorInfo();
        ParseSettings();
        ParseAppName();
        ParseMods();
        ParseCheats();
        result.BuildKind = GetBuildKind();
        ParseNotes();
    }

    /// <summary>
    ///     Checks whether a log indicates the application was loaded as homebrew rather than a licensed title.
    /// </summary>
    /// <param name="logText">The raw log text.</param>
    public static bool IsHomebrew(string logText)
    {
        return Regex.IsMatch(logText, "Load.*Application: Loading as [Hh]omebrew");
    }

    /// <summary>
    ///     Analyses the log and returns the extracted information.
    /// </summary>
    public RyujinxLogAnalysis Analyse()
    {
        result.Notes = SortNotes();
        return result;
    }

    private void ParseErrors()
    {
        List<string>? current = null;
        foreach (var rawLine in logText.Split('\n'))
        {
            if (rawLine.Trim().Length == 0)
                continue;

            if (rawLine.Contains("|E|"))
            {
                current = [rawLine];
                errorBlocks.Add(current);
            }
            else if (current is not null && rawLine.Length > 0 && rawLine[0] == ' ')
            {
                current.Add(rawLine);
            }
        }

        if (errorBlocks.Count > 0)
            result.LastError = errorBlocks[^1].Take(2).ToList();
    }

    private void ParseHardwareInfo()
    {
        var cpuMatch = CpuRegex.Match(logText);
        if (cpuMatch.Success)
            result.Hardware.Cpu = cpuMatch.Groups[1].Value.TrimEnd();

        var osMatch = OsRegex.Match(logText);
        if (osMatch.Success)
            result.Hardware.Os = osMatch.Groups[1].Value.TrimEnd();

        var gpuMatch = GpuRegex.Match(logText);
        if (gpuMatch.Success)
            result.Hardware.Gpu = gpuMatch.Groups[1].Value.TrimEnd();

        var ramMatch = RamRegex.Match(logText);
        if (ramMatch.Success &&
            double.TryParse(ramMatch.Groups[1].Value, out var total) &&
            double.TryParse(ramMatch.Groups[3].Value, out var available))
        {
            var totalMiB = total * SizeUnitBytes[ramMatch.Groups[2].Value] / SizeUnitBytes["MiB"];
            var availableMiB = available * SizeUnitBytes[ramMatch.Groups[4].Value] / SizeUnitBytes["MiB"];
            result.Hardware.Ram = $"{availableMiB:F0}/{totalMiB:F0} MiB";
        }
    }

    private void ParseEmulatorInfo()
    {
        foreach (var line in logText.Split('\n'))
        {
            if (line.Contains("Ryujinx Version:"))
                result.Emulator.Version = line.Split(' ', StringSplitOptions.RemoveEmptyEntries)[^1].Trim();
            else if (line.Contains("Firmware Version:"))
                result.Emulator.Firmware = line.Split(' ', StringSplitOptions.RemoveEmptyEntries)[^1].Trim();
        }

        var logsMatch = LogsEnabledRegex.Match(logText);
        if (logsMatch.Success)
            result.Emulator.LogsEnabled = logsMatch.Groups[1].Value.TrimEnd();
    }

    private string? GetSettingValue(string key)
    {
        string? value = null;
        var pattern = new Regex($@"LogValueChange: ({Regex.Escape(key)})\s");
        foreach (var line in logText.Split('\n'))
        {
            if (!pattern.IsMatch(line))
                continue;

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0)
                value = parts[^1];
        }

        return value;
    }

    private void ParseSettings()
    {
        foreach (var (setting, key) in BoolSettingsMap)
        {
            var value = GetSettingValue(key);
            if (value is null)
                continue;

            typeof(RyujinxSettings).GetProperty(setting)!.SetValue(result.Settings, value == "True");
        }

        foreach (var (setting, key) in RawSettingsMap)
        {
            var value = GetSettingValue(key);
            if (value is not null)
                typeof(RyujinxSettings).GetProperty(setting)!.SetValue(result.Settings, value);
        }

        if (result.Settings.Hypervisor.HasValue && !IsMacOs())
            result.Settings.Hypervisor = null;
    }

    private bool IsMacOs()
    {
        return result.Hardware.Os?.Contains("mac", StringComparison.OrdinalIgnoreCase) ?? false;
    }

    private void ParseMods()
    {
        var mods = new List<RyujinxModInfo>();
        foreach (Match match in ModsRegex.Matches(logText))
        {
            var status = match.Groups[1].Value;
            if (status is not ("" or "enabled"))
                continue;

            var modName = match.Groups[2].Value;
            var isExeFs = match.Groups[3].Value == "[E]";
            if (mods.Any(m => m.Name == modName && m.IsExeFs == isExeFs))
                continue;

            mods.Add(new RyujinxModInfo(modName, isExeFs));
        }

        if (mods.Count > 5)
        {
            result.ModOverflowCount = mods.Count - 5;
            mods = mods.Take(5).ToList();
        }

        result.Mods = mods;
    }

    private void ParseCheats()
    {
        var cheats = CheatsRegex.Matches(logText).Select(m => m.Groups[1].Value).ToList();
        if (cheats.Count > 5)
        {
            result.CheatOverflowCount = cheats.Count - 5;
            cheats = cheats.Take(5).ToList();
        }

        result.Cheats = cheats;
    }

    private void ParseAppName()
    {
        var matches = AppNameRegex.Matches(logText);
        if (matches.Count > 0)
            result.GameName = matches[^1].Groups[1].Value.TrimEnd();
    }

    private bool ContainsErrors(params string[] searchTerms)
    {
        foreach (var block in errorBlocks)
        {
            var line = string.Join('\n', block);
            if (searchTerms.Any(line.Contains))
                return true;
        }

        return false;
    }

    private RyujinxBuildKind GetBuildKind()
    {
        var version = result.Emulator.Version;
        if (version is null)
            return RyujinxBuildKind.Custom;
        if (Regex.IsMatch(version, @"^\d\.\d\.\d+$"))
            return RyujinxBuildKind.Master;
        if (Regex.IsMatch(version, @"^\d\.\d\.(\d){4}$"))
            return RyujinxBuildKind.OldMaster;
        if (Regex.IsMatch(version, @"^\d\.\d\.\d-macos\d+(?:\.\d+(?:\.\d+|$)|$)"))
            return RyujinxBuildKind.Mac;
        if (Regex.IsMatch(version, @"^\d\.\d\.\d-ldn\d+\.\d+(?:\.\d+|$)"))
            return RyujinxBuildKind.Ldn;
        if (Regex.IsMatch(version, @"^\d\.\d\.\d\+([a-f]|\d){7}$"))
            return RyujinxBuildKind.Pr;
        return RyujinxBuildKind.Custom;
    }

    private bool IsDefaultUserProfile()
    {
        return logText.Contains("UserId: 00000000000000010000000000000000");
    }

    private void ParseNotes()
    {
        if (ContainsErrors("Cache collision found"))
            notes.Add(new RyujinxLogNote(RyujinxNoteKind.ShaderCacheCollision, RyujinxNoteSeverity.Warning));

        if (ContainsErrors("ResultFsInvalidIvfcHash", "ResultFsNonRealDataVerificationFailed"))
            notes.Add(new RyujinxLogNote(RyujinxNoteKind.DumpHashError, RyujinxNoteSeverity.Warning));

        if (ContainsErrors(
                "Ryujinx.Graphics.Gpu.Shader.ShaderCache.Initialize()",
                "System.IO.InvalidDataException: End of Central Directory record could not be found",
                "ICSharpCode.SharpZipLib.Zip.ZipException: Cannot find central directory"))
            notes.Add(new RyujinxLogNote(RyujinxNoteKind.ShaderCacheCorruption, RyujinxNoteSeverity.Warning));

        if (ContainsErrors("MissingKeyException"))
            notes.Add(new RyujinxLogNote(RyujinxNoteKind.KeysOutdated, RyujinxNoteSeverity.Warning));

        if (ContainsErrors("ResultFsPermissionDenied"))
            notes.Add(new RyujinxLogNote(RyujinxNoteKind.FilePermissionError, RyujinxNoteSeverity.Warning));

        if (ContainsErrors("ResultFsTargetNotFound"))
            notes.Add(new RyujinxLogNote(RyujinxNoteKind.SaveNotFound, RyujinxNoteSeverity.Warning));

        if (ContainsErrors("ServiceNotImplementedException") && result.Settings.IgnoreMissingServices == false)
            notes.Add(new RyujinxLogNote(RyujinxNoteKind.MissingServices, RyujinxNoteSeverity.Warning));

        if (ContainsErrors("ErrorOutOfDeviceMemory") && result.Settings.TextureRecompression == false)
            notes.Add(new RyujinxLogNote(RyujinxNoteKind.VulkanOutOfMemory, RyujinxNoteSeverity.Warning));

        var timestampMatches = TimestampLineRegex.Matches(logText);
        if (timestampMatches.Count > 0)
            notes.Add(new RyujinxLogNote(RyujinxNoteKind.TimeElapsed, RyujinxNoteSeverity.Info,
                [timestampMatches[^1].Groups[1].Value]));

        if (IsDefaultUserProfile())
            notes.Add(new RyujinxLogNote(RyujinxNoteKind.DefaultUserProfile, RyujinxNoteSeverity.Warning));

        ParseControllerNotes();
        ParseOsNotes();
        ParseCpuNotes();

        if (result.Emulator.Firmware is null && result.GameName is not null)
            notes.Add(new RyujinxLogNote(RyujinxNoteKind.FirmwareNotFound, RyujinxNoteSeverity.Critical));

        ParseSettingsNotes();

        if (result.BuildKind == RyujinxBuildKind.Custom)
            notes.Add(new RyujinxLogNote(RyujinxNoteKind.CustomBuild, RyujinxNoteSeverity.Warning));
    }

    private void ParseControllerNotes()
    {
        var controllers = ControllersRegex.Matches(logText)
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToList();

        if (controllers.Count > 0)
            notes.Add(new RyujinxLogNote(RyujinxNoteKind.ControllerInfo, RyujinxNoteSeverity.Info, controllers));
        else if (result.GameName is not null)
            notes.Add(new RyujinxLogNote(RyujinxNoteKind.NoControllerInfo, RyujinxNoteSeverity.Warning));
    }

    private void ParseOsNotes()
    {
        if (result.Hardware.Os is null || !result.Hardware.Os.Contains("Windows") ||
            result.Settings.GraphicsBackend == "Vulkan")
            return;

        if (result.Hardware.Gpu?.Contains("Intel") == true)
            notes.Add(new RyujinxLogNote(RyujinxNoteKind.IntelVulkanRecommended, RyujinxNoteSeverity.Warning));
        if (result.Hardware.Gpu?.Contains("AMD") == true)
            notes.Add(new RyujinxLogNote(RyujinxNoteKind.AmdVulkanRecommended, RyujinxNoteSeverity.Warning));
    }

    private void ParseCpuNotes()
    {
        if (result.Hardware.Cpu?.Contains("VirtualApple") == true)
            notes.Add(new RyujinxLogNote(RyujinxNoteKind.RosettaShouldBeDisabled, RyujinxNoteSeverity.Error));
    }

    private void ParseSettingsNotes()
    {
        if (result.Settings.AudioBackend == "Dummy")
            notes.Add(new RyujinxLogNote(RyujinxNoteKind.DummyAudioBackend, RyujinxNoteSeverity.Warning));

        if (result.Settings.Pptc == false)
            notes.Add(new RyujinxLogNote(RyujinxNoteKind.PptcDisabled, RyujinxNoteSeverity.Error));

        if (result.Settings.ShaderCache == false)
            notes.Add(new RyujinxLogNote(RyujinxNoteKind.ShaderCacheDisabled, RyujinxNoteSeverity.Error));

        if (result.Settings.ExpandRam == true)
            notes.Add(new RyujinxLogNote(RyujinxNoteKind.ExpandRamEnabled, RyujinxNoteSeverity.Warning));

        if (result.Settings.MemoryManager == "SoftwarePageTable")
            notes.Add(new RyujinxLogNote(RyujinxNoteKind.SoftwareMemoryManager, RyujinxNoteSeverity.Error));

        if (result.Settings.IgnoreMissingServices == true)
            notes.Add(new RyujinxLogNote(RyujinxNoteKind.IgnoreMissingServicesEnabled, RyujinxNoteSeverity.Warning));

        if (result.Settings.VSync == false)
            notes.Add(new RyujinxLogNote(RyujinxNoteKind.VsyncDisabled, RyujinxNoteSeverity.Warning));

        if (result.Settings.FsIntegrity == false)
            notes.Add(new RyujinxLogNote(RyujinxNoteKind.FsIntegrityDisabled, RyujinxNoteSeverity.Warning));

        if (result.Settings.BackendThreading == "Off")
            notes.Add(new RyujinxLogNote(RyujinxNoteKind.BackendThreadingOff, RyujinxNoteSeverity.Error));
    }

    private List<RyujinxLogNote> SortNotes()
    {
        return notes.OrderByDescending(note => note.Severity).ToList();
    }
}