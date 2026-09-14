using System.Net.Http;
using System.Text.RegularExpressions;
using Discord.Interactions;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.Switch.Common;
using Mewdeko.Modules.Switch.Services;

namespace Mewdeko.Modules.Switch;

/// <summary>
///     Slash commands for Nintendo Switch homebrew tooling: error code lookups, switchbrew.org wiki search, and Ryujinx
///     log analysis.
/// </summary>
[Group("switch", "Nintendo Switch error codes, switchbrew search and Ryujinx log analysis")]
public class SlashSwitch : MewdekoSlashModuleBase<SwitchService>
{
    /// <summary>
    ///     Looks up a Nintendo Switch error code and displays what's known about it.
    /// </summary>
    /// <param name="code">The error code, either in NNNN-NNNN format or as hex (e.g. 0x2A2).</param>
    [SlashCommand("err", "Look up a Nintendo Switch error code")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Err([Summary("code", "Error code, NNNN-NNNN or 0x hex")] string code)
    {
        var lookup = Service.ResolveSwitchError(code);
        if (lookup is not null)
        {
            var description = lookup.IsKnownDescription
                ? lookup.ErrorDescription
                : Strings.SwitchErrUnknownCode(ctx.Guild.Id);
            var moduleName = lookup.ModuleName ?? Strings.Unknown(ctx.Guild.Id);

            var embed = new EmbedBuilder()
                .WithTitle(Strings.SwitchErrorTitle(ctx.Guild.Id, lookup.ErrorCode, lookup.HexCode))
                .WithDescription(description)
                .WithColor(lookup.IsKnownDescription ? Mewdeko.OkColor : Mewdeko.ErrorColor)
                .AddField(Strings.SwitchErrModuleField(ctx.Guild.Id),
                    $"{moduleName} ({lookup.ModuleId})", true)
                .AddField(Strings.SwitchErrDescriptionField(ctx.Guild.Id), lookup.Description, true)
                .WithFooter(Strings.SwitchErrFooter(ctx.Guild.Id));

            await ctx.Interaction.RespondAsync(embed: embed.Build()).ConfigureAwait(false);
            return;
        }

        var gameLookup = Service.ResolveSwitchGameError(code);
        if (gameLookup is not null)
        {
            var embed = new EmbedBuilder()
                .WithTitle(code)
                .WithDescription(gameLookup.ErrorDescription)
                .WithOkColor()
                .AddField(Strings.SwitchErrGameField(ctx.Guild.Id), gameLookup.GameName, true)
                .WithFooter(Strings.SwitchErrFooter(ctx.Guild.Id));

            await ctx.Interaction.RespondAsync(embed: embed.Build()).ConfigureAwait(false);
            return;
        }

        await ReplyErrorAsync(Strings.SwitchErrInvalidFormat(ctx.Guild.Id)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Converts a Nintendo Switch error code from NNNN-NNNN format to hexadecimal.
    /// </summary>
    /// <param name="code">The error code to convert, e.g. 2168-0002.</param>
    [SlashCommand("err2hex", "Convert an NNNN-NNNN error code to hex")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Err2Hex([Summary("code", "Error code in NNNN-NNNN format")] string code)
    {
        var hex = Service.SwitchErrorToHex(code);
        if (hex is null)
        {
            await ReplyErrorAsync(Strings.SwitchErrToHexInvalid(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await ConfirmAsync(Strings.SwitchHexValue(ctx.Guild.Id, hex.Value.ToString("X"))).ConfigureAwait(false);
    }

    /// <summary>
    ///     Converts a hexadecimal Nintendo Switch error code to the NNNN-NNNN format.
    /// </summary>
    /// <param name="hex">The hexadecimal error code to convert, e.g. 0x2A2.</param>
    [SlashCommand("hex2err", "Convert a hex error code to NNNN-NNNN format")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Hex2Err([Summary("hex", "Hexadecimal error code")] string hex)
    {
        var code = Service.HexToSwitchError(hex);
        if (code is null)
        {
            await ReplyErrorAsync(Strings.SwitchHexToErrInvalid(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await ConfirmAsync(code).ConfigureAwait(false);
    }

    /// <summary>
    ///     Searches the switchbrew.org wiki and displays the top matching pages.
    /// </summary>
    /// <param name="query">The search query.</param>
    [SlashCommand("switchbrew", "Search the switchbrew.org wiki")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Switchbrew([Summary("query", "The search query")] string query)
    {
        await DeferAsync();

        var results = await Service.SearchSwitchbrewAsync(query).ConfigureAwait(false);
        if (results.Count == 0)
        {
            await ReplyErrorAsync(Strings.NoResults(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var description = string.Join("\n\n", results.Select(r =>
            $"[{r.Title}](https://switchbrew.org/wiki/{Uri.EscapeDataString(r.Title.Replace(' ', '_'))})\n{r.Snippet.TrimTo(200)}"));

        var embed = new EmbedBuilder()
            .WithAuthor($"{Strings.SearchFor(ctx.Guild.Id)} {query.TrimTo(50)}")
            .WithDescription(description)
            .WithOkColor();

        await ctx.Interaction.FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
    }

    /// <summary>
    ///     Analyses an uploaded Ryujinx log file and reports hardware, settings and troubleshooting information.
    /// </summary>
    /// <param name="log">The Ryujinx log file (.log or .txt).</param>
    [SlashCommand("analyse-log", "Analyse a Ryujinx log file")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task AnalyseLog([Summary("log", "The Ryujinx log file")] IAttachment log)
    {
        await AnalyseAttachmentsAsync([log], ctx.User);
    }

    private async Task AnalyseAttachmentsAsync(IEnumerable<IAttachment> attachments, IUser author)
    {
        var attachment = attachments.FirstOrDefault(a =>
            a.Filename.EndsWith(".log", StringComparison.OrdinalIgnoreCase) ||
            a.Filename.EndsWith(".txt", StringComparison.OrdinalIgnoreCase));

        if (attachment is null)
        {
            await ReplyErrorAsync(Strings.SwitchLogNoAttachment(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await DeferAsync();

        RyujinxLogAnalysis analysis;
        try
        {
            var logText = await Service.DownloadLogAsync(attachment.Url).ConfigureAwait(false);
            analysis = Service.AnalyseLog(logText);
        }
        catch (Exception ex) when (ex is FormatException or HttpRequestException)
        {
            await ReplyErrorAsync(Strings.SwitchLogInvalid(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var embed = BuildLogEmbed(analysis, author.ToString());
        await ctx.Interaction.FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
    }

    private EmbedBuilder BuildLogEmbed(RyujinxLogAnalysis analysis, string uploaderName)
    {
        var guildId = ctx.Guild.Id;
        var unknown = Strings.Unknown(guildId);

        var gameName = analysis.GameName is null
            ? unknown
            : Regex.Replace(analysis.GameName, @"\s\[(64|32)-bit\]$", string.Empty);

        var embed = new EmbedBuilder()
            .WithTitle(gameName)
            .WithColor(new Color(0x4A, 0x90, 0xE2))
            .WithFooter(Strings.SwitchLogFooter(guildId, uploaderName))
            .AddField(Strings.SwitchLogGeneralInfo(guildId),
                Strings.SwitchLogGeneralInfoValue(guildId,
                    analysis.Emulator.Version ?? unknown,
                    analysis.Emulator.Firmware ?? unknown,
                    analysis.Hardware.Cpu ?? unknown,
                    analysis.Hardware.Gpu ?? unknown,
                    analysis.Hardware.Ram ?? unknown,
                    analysis.Hardware.Os ?? unknown))
            .AddField(Strings.SwitchLogSystemSettings(guildId),
                Strings.SwitchLogSystemSettingsValue(guildId,
                    analysis.Settings.AudioBackend ?? unknown,
                    FormatDocked(analysis.Settings.Docked, guildId),
                    FormatToggle(analysis.Settings.Pptc, guildId),
                    FormatToggle(analysis.Settings.ShaderCache, guildId),
                    FormatToggle(analysis.Settings.VSync, guildId),
                    FormatHypervisor(analysis, guildId)), true)
            .AddField(Strings.SwitchLogGraphicsSettings(guildId),
                Strings.SwitchLogGraphicsSettingsValue(guildId,
                    analysis.Settings.GraphicsBackend ?? unknown,
                    FormatResolutionScale(analysis.Settings.ResolutionScale, guildId),
                    FormatAnisotropicFiltering(analysis.Settings.AnisotropicFiltering, guildId),
                    FormatAspectRatio(analysis.Settings.AspectRatio, guildId),
                    FormatToggle(analysis.Settings.TextureRecompression, guildId)), true)
            .AddField(Strings.SwitchLogErrors(guildId),
                analysis.LastError is null
                    ? Strings.SwitchLogNoErrors(guildId)
                    : $"```\n{string.Join('\n', analysis.LastError)}\n```")
            .AddField(Strings.SwitchLogMods(guildId), FormatMods(analysis, guildId))
            .AddField(Strings.SwitchLogCheats(guildId), FormatCheats(analysis, guildId))
            .AddField(Strings.SwitchLogNotes(guildId),
                analysis.Notes.Count == 0
                    ? Strings.SwitchLogNoNotes(guildId)
                    : string.Join('\n', analysis.Notes.Select(n => FormatNote(n, guildId))));

        return embed;
    }

    private string FormatMods(RyujinxLogAnalysis analysis, ulong guildId)
    {
        if (analysis.Mods.Count == 0)
            return Strings.SwitchLogNoMods(guildId);

        var lines = analysis.Mods.Select(m => $"{m.Name} ({(m.IsExeFs ? "ExeFS" : "RomFS")})").ToList();
        if (analysis.ModOverflowCount > 0)
            lines.Add(Strings.SwitchLogModsOverflow(guildId, analysis.ModOverflowCount));

        return string.Join('\n', lines);
    }

    private string FormatCheats(RyujinxLogAnalysis analysis, ulong guildId)
    {
        if (analysis.Cheats.Count == 0)
            return Strings.SwitchLogNoCheats(guildId);

        var lines = analysis.Cheats.ToList();
        if (analysis.CheatOverflowCount > 0)
            lines.Add(Strings.SwitchLogCheatsOverflow(guildId, analysis.CheatOverflowCount));

        return string.Join('\n', lines);
    }

    private string FormatToggle(bool? value, ulong guildId)
    {
        return value switch
        {
            true => Strings.SwitchLogEnabled(guildId),
            false => Strings.SwitchLogDisabled(guildId),
            null => Strings.Unknown(guildId)
        };
    }

    private string FormatDocked(bool? value, ulong guildId)
    {
        return value switch
        {
            true => Strings.SwitchLogDocked(guildId),
            false => Strings.SwitchLogHandheld(guildId),
            null => Strings.Unknown(guildId)
        };
    }

    private string FormatHypervisor(RyujinxLogAnalysis analysis, ulong guildId)
    {
        var isMac = analysis.Hardware.Os?.Contains("mac", StringComparison.OrdinalIgnoreCase) ?? false;
        return !isMac ? Strings.SwitchLogNotApplicable(guildId) : FormatToggle(analysis.Settings.Hypervisor, guildId);
    }

    private string FormatResolutionScale(string? raw, ulong guildId)
    {
        return raw switch
        {
            "1" => Strings.SwitchLogNative(guildId),
            "2" => "2x",
            "3" => "3x",
            "4" => "4x",
            null => Strings.Unknown(guildId),
            _ => Strings.SwitchLogCustom(guildId)
        };
    }

    private string FormatAnisotropicFiltering(string? raw, ulong guildId)
    {
        return raw switch
        {
            "2" => "2x",
            "4" => "4x",
            "8" => "8x",
            "16" => "16x",
            null => Strings.Unknown(guildId),
            _ => Strings.SwitchLogAuto(guildId)
        };
    }

    private string FormatAspectRatio(string? raw, ulong guildId)
    {
        return raw switch
        {
            "Fixed4x3" => "4:3",
            "Fixed16x9" => "16:9",
            "Fixed16x10" => "16:10",
            "Fixed21x9" => "21:9",
            "Fixed32x9" => "32:9",
            "Stretched" => Strings.SwitchLogStretch(guildId),
            null => Strings.Unknown(guildId),
            _ => Strings.Unknown(guildId)
        };
    }

    private string FormatNote(RyujinxLogNote note, ulong guildId)
    {
        if (note.Kind == RyujinxNoteKind.ControllerInfo)
            return string.Join('\n', note.Args!);

        var severity = note.Severity switch
        {
            RyujinxNoteSeverity.Critical => "[Critical]",
            RyujinxNoteSeverity.Error => "[Error]",
            RyujinxNoteSeverity.Warning => "[Warning]",
            RyujinxNoteSeverity.Info => "[Info]",
            _ => "[OK]"
        };

        var text = note.Kind switch
        {
            RyujinxNoteKind.ShaderCacheCollision => Strings.SwitchLogNoteShaderCacheCollision(guildId),
            RyujinxNoteKind.DumpHashError => Strings.SwitchLogNoteDumpHashError(guildId),
            RyujinxNoteKind.ShaderCacheCorruption => Strings.SwitchLogNoteShaderCacheCorruption(guildId),
            RyujinxNoteKind.KeysOutdated => Strings.SwitchLogNoteKeysOutdated(guildId),
            RyujinxNoteKind.FilePermissionError => Strings.SwitchLogNoteFilePermissionError(guildId),
            RyujinxNoteKind.SaveNotFound => Strings.SwitchLogNoteSaveNotFound(guildId),
            RyujinxNoteKind.MissingServices => Strings.SwitchLogNoteMissingServices(guildId),
            RyujinxNoteKind.VulkanOutOfMemory => Strings.SwitchLogNoteVulkanOutOfMemory(guildId),
            RyujinxNoteKind.TimeElapsed => Strings.SwitchLogNoteTimeElapsed(guildId, note.Args![0]),
            RyujinxNoteKind.DefaultUserProfile => Strings.SwitchLogNoteDefaultUserProfile(guildId),
            RyujinxNoteKind.NoControllerInfo => Strings.SwitchLogNoteNoControllerInfo(guildId),
            RyujinxNoteKind.IntelVulkanRecommended => Strings.SwitchLogNoteIntelVulkan(guildId),
            RyujinxNoteKind.AmdVulkanRecommended => Strings.SwitchLogNoteAmdVulkan(guildId),
            RyujinxNoteKind.RosettaShouldBeDisabled => Strings.SwitchLogNoteRosetta(guildId),
            RyujinxNoteKind.FirmwareNotFound => Strings.SwitchLogNoteFirmwareNotFound(guildId),
            RyujinxNoteKind.DummyAudioBackend => Strings.SwitchLogNoteDummyAudio(guildId),
            RyujinxNoteKind.PptcDisabled => Strings.SwitchLogNotePptcDisabled(guildId),
            RyujinxNoteKind.ShaderCacheDisabled => Strings.SwitchLogNoteShaderCacheDisabled(guildId),
            RyujinxNoteKind.ExpandRamEnabled => Strings.SwitchLogNoteExpandRam(guildId),
            RyujinxNoteKind.SoftwareMemoryManager => Strings.SwitchLogNoteSoftwareMemoryManager(guildId),
            RyujinxNoteKind.IgnoreMissingServicesEnabled => Strings.SwitchLogNoteIgnoreMissingServices(guildId),
            RyujinxNoteKind.VsyncDisabled => Strings.SwitchLogNoteVsyncDisabled(guildId),
            RyujinxNoteKind.FsIntegrityDisabled => Strings.SwitchLogNoteFsIntegrityDisabled(guildId),
            RyujinxNoteKind.BackendThreadingOff => Strings.SwitchLogNoteBackendThreadingOff(guildId),
            RyujinxNoteKind.CustomBuild => Strings.SwitchLogNoteCustomBuild(guildId),
            _ => string.Empty
        };

        return $"{severity} {text}";
    }
}