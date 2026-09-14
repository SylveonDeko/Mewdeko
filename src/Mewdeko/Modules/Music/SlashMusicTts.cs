using System.Net.Http;
using System.Threading;
using DataModel;
using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Lavalink4NET;
using Lavalink4NET.DiscordNet;
using Lavalink4NET.Players;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.Autocompleters;
using Mewdeko.Modules.Music.CustomPlayer;
using Mewdeko.Modules.Music.Services;
using Microsoft.Extensions.Caching.Memory;

namespace Mewdeko.Modules.Music;

/// <summary>
///     Slash commands for TTS (text-to-speech). Manages per-voice-channel TTS settings,
///     user voices, blocking, and guild-wide TTS configuration.
/// </summary>
[Group("tts", "Text to speech settings for voice channels")]
public class SlashMusicTts(
    IAudioService service,
    GuildSettingsService guildSettingsService,
    IDataConnectionFactory dbFactory,
    InteractiveService interactiveService,
    IHttpClientFactory httpClientFactory,
    IMemoryCache memoryCache) : MewdekoSlashCommandModule
{
    /// <summary>
    ///     Enables or disables TTS for the voice channel the bot is currently in.
    /// </summary>
    [SlashCommand("enable", "Toggles TTS for the current voice channel")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task TtsEnable()
    {
        await DeferAsync();

        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            await SendPlayerErrorAsync(result).ConfigureAwait(false);
            return;
        }

        var vcId = player.VoiceChannelId;
        var existing = await player.GetTtsVcSettingAsync(vcId);

        if (existing is { Enabled: true })
        {
            await player.UpsertTtsVcSettingAsync(new TtsVoiceChannelSetting
            {
                VoiceChannelId = vcId,
                Enabled = false,
                LinkedTextChannelId = existing.LinkedTextChannelId,
                AnnounceJoinLeave = existing.AnnounceJoinLeave,
                JoinFormat = existing.JoinFormat,
                LeaveFormat = existing.LeaveFormat
            });
            await ReplyConfirmAsync(Strings.TtsDisabled(ctx.Guild.Id)).ConfigureAwait(false);
        }
        else
        {
            await player.UpsertTtsVcSettingAsync(new TtsVoiceChannelSetting
            {
                VoiceChannelId = vcId,
                Enabled = true,
                LinkedTextChannelId = existing?.LinkedTextChannelId,
                AnnounceJoinLeave = existing?.AnnounceJoinLeave ?? false,
                JoinFormat = existing?.JoinFormat,
                LeaveFormat = existing?.LeaveFormat
            });
            await ReplyConfirmAsync(Strings.TtsEnabled(ctx.Guild.Id)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Links a text channel to the current voice channel for TTS. Messages in the linked text channel
    ///     will be read aloud. Omit the channel to unlink.
    /// </summary>
    /// <param name="channel">The text channel to link. Omit to unlink.</param>
    [SlashCommand("link", "Links a text channel to the current voice channel for TTS")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task TtsLink(
        [Summary("channel", "The text channel to link. Omit to unlink")]
        ITextChannel? channel = null)
    {
        await DeferAsync();

        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            await SendPlayerErrorAsync(result).ConfigureAwait(false);
            return;
        }

        var vcId = player.VoiceChannelId;
        var existing = await player.GetTtsVcSettingAsync(vcId);

        await player.UpsertTtsVcSettingAsync(new TtsVoiceChannelSetting
        {
            VoiceChannelId = vcId,
            Enabled = existing?.Enabled ?? true,
            LinkedTextChannelId = channel?.Id,
            AnnounceJoinLeave = existing?.AnnounceJoinLeave ?? false,
            JoinFormat = existing?.JoinFormat,
            LeaveFormat = existing?.LeaveFormat
        });

        if (channel is null)
            await ReplyConfirmAsync(Strings.TtsUnlinked(ctx.Guild.Id)).ConfigureAwait(false);
        else
            await ReplyConfirmAsync(Strings.TtsLinked(ctx.Guild.Id, channel.Id)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Toggles join/leave announcements for the current voice channel.
    /// </summary>
    [SlashCommand("announce", "Toggles join/leave announcements for the current voice channel")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task TtsAnnounce()
    {
        await DeferAsync();

        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            await SendPlayerErrorAsync(result).ConfigureAwait(false);
            return;
        }

        var vcId = player.VoiceChannelId;
        var existing = await player.GetTtsVcSettingAsync(vcId);
        var newVal = !(existing?.AnnounceJoinLeave ?? false);

        await player.UpsertTtsVcSettingAsync(new TtsVoiceChannelSetting
        {
            VoiceChannelId = vcId,
            Enabled = existing?.Enabled ?? true,
            LinkedTextChannelId = existing?.LinkedTextChannelId,
            AnnounceJoinLeave = newVal,
            JoinFormat = existing?.JoinFormat,
            LeaveFormat = existing?.LeaveFormat
        });

        await ReplyConfirmAsync(newVal
            ? Strings.TtsAnnounceEnabled(ctx.Guild.Id)
            : Strings.TtsAnnounceDisabled(ctx.Guild.Id)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the join announcement format for the current voice channel.
    ///     Supports %user.name%, %user.mention%, and %server.name% placeholders. Omit to reset to default.
    /// </summary>
    /// <param name="format">The join announcement format. Omit to reset.</param>
    [SlashCommand("join-format", "Sets the join announcement format for the current voice channel")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task TtsJoinFormat(
        [Summary("format", "Format with %user.name%, %user.mention%, %server.name%. Omit to reset")]
        string? format = null)
    {
        await DeferAsync();

        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            await SendPlayerErrorAsync(result).ConfigureAwait(false);
            return;
        }

        var vcId = player.VoiceChannelId;
        var existing = await player.GetTtsVcSettingAsync(vcId);

        await player.UpsertTtsVcSettingAsync(new TtsVoiceChannelSetting
        {
            VoiceChannelId = vcId,
            Enabled = existing?.Enabled ?? true,
            LinkedTextChannelId = existing?.LinkedTextChannelId,
            AnnounceJoinLeave = existing?.AnnounceJoinLeave ?? false,
            JoinFormat = format,
            LeaveFormat = existing?.LeaveFormat
        });

        await ReplyConfirmAsync(string.IsNullOrWhiteSpace(format)
            ? Strings.TtsJoinFormatReset(ctx.Guild.Id)
            : Strings.TtsJoinFormatSet(ctx.Guild.Id, format)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the leave announcement format for the current voice channel.
    ///     Supports %user.name%, %user.mention%, and %server.name% placeholders. Omit to reset to default.
    /// </summary>
    /// <param name="format">The leave announcement format. Omit to reset.</param>
    [SlashCommand("leave-format", "Sets the leave announcement format for the current voice channel")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task TtsLeaveFormat(
        [Summary("format", "Format with %user.name%, %user.mention%, %server.name%. Omit to reset")]
        string? format = null)
    {
        await DeferAsync();

        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            await SendPlayerErrorAsync(result).ConfigureAwait(false);
            return;
        }

        var vcId = player.VoiceChannelId;
        var existing = await player.GetTtsVcSettingAsync(vcId);

        await player.UpsertTtsVcSettingAsync(new TtsVoiceChannelSetting
        {
            VoiceChannelId = vcId,
            Enabled = existing?.Enabled ?? true,
            LinkedTextChannelId = existing?.LinkedTextChannelId,
            AnnounceJoinLeave = existing?.AnnounceJoinLeave ?? false,
            JoinFormat = existing?.JoinFormat,
            LeaveFormat = format
        });

        await ReplyConfirmAsync(string.IsNullOrWhiteSpace(format)
            ? Strings.TtsLeaveFormatReset(ctx.Guild.Id)
            : Strings.TtsLeaveFormatSet(ctx.Guild.Id, format)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the TTS volume (0-100). Separate from music volume.
    /// </summary>
    /// <param name="volume">The TTS volume between 0 and 100.</param>
    [SlashCommand("volume", "Sets the TTS volume, separate from music volume")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task TtsVolume([Summary("volume", "Volume between 0 and 100")] int volume)
    {
        if (volume is < 0 or > 100)
        {
            await ReplyErrorAsync(Strings.TtsVolumeInvalid(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await DeferAsync();

        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            await SendPlayerErrorAsync(result).ConfigureAwait(false);
            return;
        }

        await player.UpdateTtsGuildSettingAsync(s => s.TtsVolume = volume);
        await ReplyConfirmAsync(Strings.TtsVolumeSet(ctx.Guild.Id, volume)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the TTS playback speed (0.5-2.0).
    /// </summary>
    /// <param name="speed">The playback speed between 0.5 and 2.0.</param>
    [SlashCommand("speed", "Sets the TTS playback speed between 0.5 and 2.0")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task TtsSpeed([Summary("speed", "Speed between 0.5 and 2.0")] double speed)
    {
        if (speed is < 0.5 or > 2.0)
        {
            await ReplyErrorAsync(Strings.TtsSpeedInvalid(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await DeferAsync();

        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            await SendPlayerErrorAsync(result).ConfigureAwait(false);
            return;
        }

        var value = (float)speed;
        await player.UpdateTtsGuildSettingAsync(s => s.TtsSpeed = value);
        await ReplyConfirmAsync(Strings.TtsSpeedSet(ctx.Guild.Id, value.ToString("F1"))).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the default TTS voice for the guild. Omit the voice to reset.
    /// </summary>
    /// <param name="voice">The voice name. Omit to reset to the system default.</param>
    [SlashCommand("default-voice", "Sets the default TTS voice for this server")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task TtsDefaultVoice(
        [Summary("voice", "The voice name. Omit to reset")] [Autocomplete(typeof(TtsVoiceAutocompleter))]
        string? voice = null)
    {
        await DeferAsync();

        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            await SendPlayerErrorAsync(result).ConfigureAwait(false);
            return;
        }

        await player.UpdateTtsGuildSettingAsync(s => s.TtsDefaultVoice = voice);

        await ReplyConfirmAsync(string.IsNullOrWhiteSpace(voice)
            ? Strings.TtsDefaultVoiceReset(ctx.Guild.Id)
            : Strings.TtsDefaultVoiceSet(ctx.Guild.Id, voice)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets your personal TTS voice for this server. Omit the voice to reset.
    /// </summary>
    /// <param name="voice">The voice name. Omit to reset to the server default.</param>
    [SlashCommand("voice", "Sets your personal TTS voice for this server")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task TtsVoice(
        [Summary("voice", "The voice name. Omit to reset")] [Autocomplete(typeof(TtsVoiceAutocompleter))]
        string? voice = null)
    {
        await DeferAsync();

        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            await SendPlayerErrorAsync(result).ConfigureAwait(false);
            return;
        }

        await player.UpsertTtsUserSettingAsync(ctx.User.Id, s => s.Voice = voice);

        await ReplyConfirmAsync(string.IsNullOrWhiteSpace(voice)
            ? Strings.TtsVoiceReset(ctx.Guild.Id)
            : Strings.TtsVoiceSet(ctx.Guild.Id, voice)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Searches available TTS voices by name, language, or gender.
    /// </summary>
    /// <param name="search">The search term (voice name, language, or gender).</param>
    [SlashCommand("voices", "Searches available TTS voices by name, language, or gender")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task TtsVoices([Summary("search", "Voice name, language, or gender to search for")] string search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            await ReplyErrorAsync(Strings.TtsVoicesSearchRequired(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await DeferAsync();

        var voices = await TtsVoiceAutocompleter.GetVoicesAsync(httpClientFactory, memoryCache);
        if (voices.Count == 0)
        {
            await ReplyErrorAsync(Strings.TtsVoicesFetchFailed(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var results = voices
            .Where(v =>
                v.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (v.Gender?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (v.Language?.Name?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (v.Language?.Code?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (v.Source?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false))
            .ToList();

        if (results.Count == 0)
        {
            await ReplyErrorAsync(Strings.TtsVoicesNoResults(ctx.Guild.Id, search)).ConfigureAwait(false);
            return;
        }

        const int voicesPerPage = 15;
        var totalPages = Math.Max(1, (int)Math.Ceiling((double)results.Count / voicesPerPage));

        var paginator = new ComponentPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(GeneratePage)
            .WithPageCount(totalPages)
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .WithActionOnTimeout(ActionOnStop.DisableInput)
            .Build();

        await interactiveService.SendPaginatorAsync(paginator, ctx.Interaction, TimeSpan.FromMinutes(5),
            InteractionResponseType.DeferredChannelMessageWithSource);

        IPage GeneratePage(IComponentPaginator p)
        {
            var startIndex = p.CurrentPageIndex * voicesPerPage;
            var voicesOnPage = results.Skip(startIndex).Take(voicesPerPage).ToList();

            var containerComponents = new List<IMessageComponentBuilder>();

            containerComponents.Add(new TextDisplayBuilder()
                .WithContent($"# {Strings.TtsVoicesTitle(ctx.Guild.Id, search, results.Count)}"));

            containerComponents.Add(new SeparatorBuilder());

            var lines = voicesOnPage.Select((v, i) =>
                Strings.TtsVoicesEntry(ctx.Guild.Id, startIndex + i + 1, v.Name, v.Gender, v.Language?.Name,
                    v.Source));

            containerComponents.Add(new TextDisplayBuilder()
                .WithContent(string.Join("\n", lines)));

            containerComponents.Add(new SeparatorBuilder());

            var navigationRow = new ActionRowBuilder()
                .AddPreviousButton(p, style: ButtonStyle.Secondary)
                .AddNextButton(p, style: ButtonStyle.Secondary)
                .AddStopButton(p);

            containerComponents.Add(navigationRow);

            containerComponents.Add(new TextDisplayBuilder()
                .WithContent($"Page {p.CurrentPageIndex + 1}/{p.PageCount}"));

            var mainContainer = new ContainerBuilder()
                .WithComponents(containerComponents)
                .WithAccentColor(Mewdeko.OkColor);

            var componentsV2 = new ComponentBuilderV2()
                .AddComponent(mainContainer);

            return new PageBuilder()
                .WithComponents(componentsV2.Build())
                .Build();
        }
    }

    /// <summary>
    ///     Blocks or unblocks a user from using TTS in this server.
    /// </summary>
    /// <param name="user">The user to block or unblock.</param>
    [SlashCommand("block", "Blocks or unblocks a user from using TTS")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task TtsBlock([Summary("user", "The user to block or unblock")] IGuildUser user)
    {
        await DeferAsync();

        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            await SendPlayerErrorAsync(result).ConfigureAwait(false);
            return;
        }

        var isBlocked = await player.IsTtsUserBlockedAsync(user.Id);
        await player.UpsertTtsUserSettingAsync(user.Id, s => s.IsBlocked = !isBlocked);

        await ReplyConfirmAsync(!isBlocked
            ? Strings.TtsUserBlocked(ctx.Guild.Id, user.DisplayName)
            : Strings.TtsUserUnblocked(ctx.Guild.Id, user.DisplayName)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Lists all users blocked from TTS in this server.
    /// </summary>
    [SlashCommand("block-list", "Lists all users blocked from TTS")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task TtsBlockList()
    {
        await DeferAsync();

        await using var db = await dbFactory.CreateConnectionAsync();
        var blockedUsers = await db.GetTable<TtsUserSetting>()
            .Where(x => x.GuildId == ctx.Guild.Id && x.IsBlocked)
            .ToListAsync();

        if (blockedUsers.Count == 0)
        {
            await ReplyConfirmAsync(Strings.TtsBlockListEmpty(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        const int usersPerPage = 10;
        var totalPages = Math.Max(1, (int)Math.Ceiling((double)blockedUsers.Count / usersPerPage));

        var paginator = new ComponentPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(GeneratePage)
            .WithPageCount(totalPages)
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .WithActionOnTimeout(ActionOnStop.DisableInput)
            .Build();

        await interactiveService.SendPaginatorAsync(paginator, ctx.Interaction, TimeSpan.FromMinutes(5),
            InteractionResponseType.DeferredChannelMessageWithSource);

        IPage GeneratePage(IComponentPaginator p)
        {
            var startIndex = p.CurrentPageIndex * usersPerPage;
            var usersOnPage = blockedUsers.Skip(startIndex).Take(usersPerPage).ToList();

            var containerComponents = new List<IMessageComponentBuilder>();

            containerComponents.Add(new TextDisplayBuilder()
                .WithContent($"# {Strings.TtsBlockListTitle(ctx.Guild.Id, blockedUsers.Count)}"));

            containerComponents.Add(new SeparatorBuilder());

            var lines = usersOnPage.Select((x, i) =>
                $"`{startIndex + i + 1}.` <@{x.UserId}>");

            containerComponents.Add(new TextDisplayBuilder()
                .WithContent(string.Join("\n", lines)));

            containerComponents.Add(new SeparatorBuilder());

            var navigationRow = new ActionRowBuilder()
                .AddPreviousButton(p, style: ButtonStyle.Secondary)
                .AddNextButton(p, style: ButtonStyle.Secondary)
                .AddStopButton(p);

            containerComponents.Add(navigationRow);

            containerComponents.Add(new TextDisplayBuilder()
                .WithContent($"Page {p.CurrentPageIndex + 1}/{p.PageCount}"));

            var mainContainer = new ContainerBuilder()
                .WithComponents(containerComponents)
                .WithAccentColor(Mewdeko.OkColor);

            var componentsV2 = new ComponentBuilderV2()
                .AddComponent(mainContainer);

            return new PageBuilder()
                .WithComponents(componentsV2.Build())
                .Build();
        }
    }

    /// <summary>
    ///     Sets the role required to use TTS. Omit the role to remove the restriction.
    /// </summary>
    /// <param name="role">The role required to use TTS. Omit to remove the restriction.</param>
    [SlashCommand("role", "Sets the role required to use TTS")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task TtsRole([Summary("role", "The required role. Omit to remove the restriction")] IRole? role = null)
    {
        await DeferAsync();

        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            await SendPlayerErrorAsync(result).ConfigureAwait(false);
            return;
        }

        await player.UpdateTtsGuildSettingAsync(s => s.TtsRoleId = role?.Id);

        await ReplyConfirmAsync(role is null
            ? Strings.TtsRoleRemoved(ctx.Guild.Id)
            : Strings.TtsRoleSet(ctx.Guild.Id, role.Name)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Toggles whether TTS reads reply context (e.g. "replying to User").
    /// </summary>
    [SlashCommand("reply-context", "Toggles whether TTS reads reply context")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task TtsReplyContext()
    {
        await DeferAsync();

        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            await SendPlayerErrorAsync(result).ConfigureAwait(false);
            return;
        }

        var settings = await player.GetTtsGuildSettings();
        var newVal = !settings.TtsReplyContext;
        await player.UpdateTtsGuildSettingAsync(s => s.TtsReplyContext = newVal);

        await ReplyConfirmAsync(newVal
            ? Strings.TtsReplyContextOn(ctx.Guild.Id)
            : Strings.TtsReplyContextOff(ctx.Guild.Id)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Toggles whether TTS narrates attachments (e.g. "sent an image").
    /// </summary>
    [SlashCommand("attachment-narration", "Toggles whether TTS narrates attachments")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task TtsAttachmentNarration()
    {
        await DeferAsync();

        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            await SendPlayerErrorAsync(result).ConfigureAwait(false);
            return;
        }

        var settings = await player.GetTtsGuildSettings();
        var newVal = !settings.TtsAttachmentNarration;
        await player.UpdateTtsGuildSettingAsync(s => s.TtsAttachmentNarration = newVal);

        await ReplyConfirmAsync(newVal
            ? Strings.TtsAttachmentNarrationOn(ctx.Guild.Id)
            : Strings.TtsAttachmentNarrationOff(ctx.Guild.Id)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Toggles whether consecutive messages from the same user skip the name prefix.
    /// </summary>
    [SlashCommand("consecutive-grouping", "Toggles skipping the name prefix on consecutive messages")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task TtsConsecutiveGrouping()
    {
        await DeferAsync();

        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            await SendPlayerErrorAsync(result).ConfigureAwait(false);
            return;
        }

        var settings = await player.GetTtsGuildSettings();
        var newVal = !settings.TtsConsecutiveGrouping;
        await player.UpdateTtsGuildSettingAsync(s => s.TtsConsecutiveGrouping = newVal);

        await ReplyConfirmAsync(newVal
            ? Strings.TtsConsecutiveGroupingOn(ctx.Guild.Id)
            : Strings.TtsConsecutiveGroupingOff(ctx.Guild.Id)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Sets the maximum TTS queue size (1-50).
    /// </summary>
    /// <param name="size">The maximum queue size between 1 and 50.</param>
    [SlashCommand("max-queue", "Sets the maximum TTS queue size between 1 and 50")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task TtsMaxQueue([Summary("size", "Queue size between 1 and 50")] int size)
    {
        if (size is < 1 or > 50)
        {
            await ReplyErrorAsync(Strings.TtsMaxQueueInvalid(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await DeferAsync();

        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            await SendPlayerErrorAsync(result).ConfigureAwait(false);
            return;
        }

        await player.UpdateTtsGuildSettingAsync(s => s.TtsMaxQueueSize = size);
        await ReplyConfirmAsync(Strings.TtsMaxQueueSet(ctx.Guild.Id, size)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Shows the current TTS settings for this guild and voice channel.
    /// </summary>
    [SlashCommand("settings", "Shows the current TTS settings")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task TtsSettings()
    {
        await DeferAsync();

        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            await SendPlayerErrorAsync(result).ConfigureAwait(false);
            return;
        }

        var settings = await player.GetTtsGuildSettings();
        var vcSetting = await player.GetTtsVcSettingAsync(player.VoiceChannelId);
        var userSetting = await player.GetTtsUserSettingAsync(ctx.User.Id);

        var yes = Strings.TtsYes(ctx.Guild.Id);
        var no = Strings.TtsNo(ctx.Guild.Id);

        var vcEnabled = vcSetting?.Enabled == true ? yes : no;
        var linkedChannel = vcSetting?.LinkedTextChannelId.HasValue == true
            ? $"<#{vcSetting.LinkedTextChannelId.Value}>"
            : Strings.TtsSettingsNoLinkedChannel(ctx.Guild.Id);
        var announce = vcSetting?.AnnounceJoinLeave == true ? yes : no;
        var joinFmt = vcSetting?.JoinFormat ?? TtsService.DefaultJoinFormat;
        var leaveFmt = vcSetting?.LeaveFormat ?? TtsService.DefaultLeaveFormat;
        var defaultVoice = string.IsNullOrWhiteSpace(settings.TtsDefaultVoice)
            ? Strings.TtsSystemDefault(ctx.Guild.Id)
            : settings.TtsDefaultVoice;
        var yourVoice = string.IsNullOrWhiteSpace(userSetting.Voice)
            ? Strings.TtsServerDefault(ctx.Guild.Id)
            : userSetting.Voice;
        var roleText = settings.TtsRoleId.HasValue
            ? $"<@&{settings.TtsRoleId.Value}>"
            : Strings.TtsSettingsNoRole(ctx.Guild.Id);

        var components = new ComponentBuilderV2()
            .WithContainer([
                new TextDisplayBuilder($"# {Strings.TtsSettingsTitle(ctx.Guild.Id)}")
            ], Mewdeko.OkColor)
            .WithSeparator()
            .WithContainer(new TextDisplayBuilder(
                Strings.TtsSettingsChannel(ctx.Guild.Id, vcEnabled, linkedChannel, announce, joinFmt, leaveFmt)))
            .WithSeparator()
            .WithContainer(new TextDisplayBuilder(
                Strings.TtsSettingsGuild(ctx.Guild.Id, settings.TtsVolume, settings.TtsSpeed.ToString("F1"),
                    defaultVoice, yourVoice, roleText,
                    settings.TtsReplyContext ? yes : no,
                    settings.TtsAttachmentNarration ? yes : no,
                    settings.TtsConsecutiveGrouping ? yes : no,
                    settings.TtsMaxQueueSize)));

        await FollowupAsync(components: components.Build(), flags: MessageFlags.ComponentsV2,
            allowedMentions: AllowedMentions.None).ConfigureAwait(false);
    }

    /// <summary>
    ///     Removes all TTS settings for the current voice channel.
    /// </summary>
    [SlashCommand("remove", "Removes all TTS settings for the current voice channel")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task TtsRemove()
    {
        await DeferAsync();

        var (player, result) = await GetPlayerAsync(false);
        if (result is not null)
        {
            await SendPlayerErrorAsync(result).ConfigureAwait(false);
            return;
        }

        await player.RemoveTtsVcSettingAsync(player.VoiceChannelId);
        await ReplyConfirmAsync(Strings.TtsRemoved(ctx.Guild.Id)).ConfigureAwait(false);
    }

    private async Task SendPlayerErrorAsync(string message)
    {
        var components = new ComponentBuilderV2()
            .WithContainer([
                new TextDisplayBuilder($"# {Strings.MusicPlayerError(ctx.Guild.Id)}")
            ], Mewdeko.ErrorColor)
            .WithSeparator()
            .WithContainer(new TextDisplayBuilder(message));

        if (ctx.Interaction.HasResponded)
        {
            await FollowupAsync(components: components.Build(), flags: MessageFlags.ComponentsV2,
                allowedMentions: AllowedMentions.None).ConfigureAwait(false);
            return;
        }

        await RespondAsync(components: components.Build(), flags: MessageFlags.ComponentsV2,
            allowedMentions: AllowedMentions.None).ConfigureAwait(false);
    }

    private async ValueTask<(MewdekoPlayer, string?)> GetPlayerAsync(bool connectToVoiceChannel = true)
    {
        try
        {
            var channelBehavior = connectToVoiceChannel
                ? PlayerChannelBehavior.Join
                : PlayerChannelBehavior.None;

            var retrieveOptions = new PlayerRetrieveOptions(channelBehavior);

            var options = new MewdekoPlayerOptions
            {
                Channel = ctx.Channel as ITextChannel
            };

            var result = await service.Players
                .RetrieveAsync<MewdekoPlayer, MewdekoPlayerOptions>(Context, CreatePlayerAsync, options,
                    retrieveOptions)
                .ConfigureAwait(false);

            if (result.IsSuccess && result.Player is not null)
            {
                await result.Player.SetVolumeAsync(await result.Player.GetVolume() / 100f).ConfigureAwait(false);
                return (result.Player, null);
            }

            var errorMessage = result.Status switch
            {
                PlayerRetrieveStatus.UserNotInVoiceChannel => Strings.MusicNotInChannel(ctx.Guild.Id),
                PlayerRetrieveStatus.BotNotConnected => Strings.MusicBotNotConnect(ctx.Guild.Id,
                    await guildSettingsService.GetPrefix(ctx.Guild)),
                PlayerRetrieveStatus.VoiceChannelMismatch => Strings.MusicVoiceChannelMismatch(ctx.Guild.Id),
                PlayerRetrieveStatus.Success => null,
                PlayerRetrieveStatus.UserInSameVoiceChannel => null,
                PlayerRetrieveStatus.PreconditionFailed => null,
                _ => throw new ArgumentOutOfRangeException()
            };
            return (null, errorMessage);
        }
        catch (TimeoutException)
        {
            return (null, Strings.MusicLavalinkDisconnected(ctx.Guild.Id));
        }
    }

    private static ValueTask<MewdekoPlayer> CreatePlayerAsync(
        IPlayerProperties<MewdekoPlayer, MewdekoPlayerOptions> properties,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(properties);

        return ValueTask.FromResult(new MewdekoPlayer(properties));
    }
}