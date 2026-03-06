using System.Net.Http;
using System.Text.Json;
using DataModel;
using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Lavalink4NET;
using Lavalink4NET.DiscordNet;
using Lavalink4NET.Players;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Music.CustomPlayer;

namespace Mewdeko.Modules.Music;

public partial class Music
{
    /// <summary>
    ///     Submodule for TTS (text-to-speech) commands. Manages per-voice-channel TTS settings,
    ///     user voices, blocking, and guild-wide TTS configuration.
    /// </summary>
    public class MusicTts(
        IAudioService service,
        GuildSettingsService guildSettingsService,
        IDataConnectionFactory dbFactory,
        InteractiveService interactiveService) : MewdekoSubmodule
    {
        /// <summary>
        ///     Enables or disables TTS for the voice channel you are currently in.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task TtsEnable()
        {
            var (player, result) = await GetPlayerAsync(false);
            if (result is not null)
            {
                await SendErrorAsync(result);
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
                await ReplyConfirmAsync("TTS has been **disabled** for this voice channel.");
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
                await ReplyConfirmAsync("TTS has been **enabled** for this voice channel.");
            }
        }

        /// <summary>
        ///     Links a text channel to the current voice channel for TTS.
        ///     Messages in the linked text channel will be read aloud.
        ///     Use without arguments to unlink.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task TtsLink(ITextChannel channel = null)
        {
            var (player, result) = await GetPlayerAsync(false);
            if (result is not null)
            {
                await SendErrorAsync(result);
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
                await ReplyConfirmAsync("Text channel unlinked. TTS will only read from VC built-in text chat.");
            else
                await ReplyConfirmAsync($"TTS linked to <#{channel.Id}>.");
        }

        /// <summary>
        ///     Toggles join/leave announcements for the current voice channel.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task TtsAnnounce()
        {
            var (player, result) = await GetPlayerAsync(false);
            if (result is not null)
            {
                await SendErrorAsync(result);
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
                ? "Join/leave announcements **enabled** for this voice channel."
                : "Join/leave announcements **disabled** for this voice channel.");
        }

        /// <summary>
        ///     Sets the join announcement format for the current voice channel.
        ///     Use {user} as a placeholder for the username.
        ///     Use without arguments to reset to default.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task TtsJoinFormat([Remainder] string format = null)
        {
            var (player, result) = await GetPlayerAsync(false);
            if (result is not null)
            {
                await SendErrorAsync(result);
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
                ? "Join format reset to default."
                : $"Join format set to: **{format}**");
        }

        /// <summary>
        ///     Sets the leave announcement format for the current voice channel.
        ///     Use {user} as a placeholder for the username.
        ///     Use without arguments to reset to default.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task TtsLeaveFormat([Remainder] string format = null)
        {
            var (player, result) = await GetPlayerAsync(false);
            if (result is not null)
            {
                await SendErrorAsync(result);
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
                ? "Leave format reset to default."
                : $"Leave format set to: **{format}**");
        }

        /// <summary>
        ///     Sets the TTS volume (0-100). Separate from music volume.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task TtsVolume(int volume)
        {
            if (volume is < 0 or > 100)
            {
                await ReplyErrorAsync("Volume must be between 0 and 100.");
                return;
            }

            var (player, result) = await GetPlayerAsync(false);
            if (result is not null)
            {
                await SendErrorAsync(result);
                return;
            }

            await player.UpdateTtsGuildSettingAsync(s => s.TtsVolume = volume);
            await ReplyConfirmAsync($"TTS volume set to **{volume}%**.");
        }

        /// <summary>
        ///     Sets the TTS playback speed (0.5-2.0).
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task TtsSpeed(float speed)
        {
            if (speed is < 0.5f or > 2.0f)
            {
                await ReplyErrorAsync("Speed must be between 0.5 and 2.0.");
                return;
            }

            var (player, result) = await GetPlayerAsync(false);
            if (result is not null)
            {
                await SendErrorAsync(result);
                return;
            }

            await player.UpdateTtsGuildSettingAsync(s => s.TtsSpeed = speed);
            await ReplyConfirmAsync($"TTS speed set to **{speed:F1}x**.");
        }

        /// <summary>
        ///     Sets the default TTS voice for the guild. Use without arguments to reset.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task TtsDefaultVoice([Remainder] string voice = null)
        {
            var (player, result) = await GetPlayerAsync(false);
            if (result is not null)
            {
                await SendErrorAsync(result);
                return;
            }

            await player.UpdateTtsGuildSettingAsync(s => s.TtsDefaultVoice = voice);

            await ReplyConfirmAsync(string.IsNullOrWhiteSpace(voice)
                ? "Default TTS voice has been reset to the system default."
                : $"Default TTS voice set to **{voice}**.");
        }

        /// <summary>
        ///     Sets your personal TTS voice for this server. Use without arguments to reset.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task TtsVoice([Remainder] string voice = null)
        {
            var (player, result) = await GetPlayerAsync(false);
            if (result is not null)
            {
                await SendErrorAsync(result);
                return;
            }

            await player.UpsertTtsUserSettingAsync(ctx.User.Id, s => s.Voice = voice);

            await ReplyConfirmAsync(string.IsNullOrWhiteSpace(voice)
                ? "Your TTS voice has been reset to the server default."
                : $"Your TTS voice set to **{voice}**.");
        }

        /// <summary>
        ///     Searches available TTS voices by name, language, or gender.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task TtsVoices([Remainder] string search = null)
        {
            if (string.IsNullOrWhiteSpace(search))
            {
                await ReplyErrorAsync("Please provide a search term (e.g. voice name, language, or gender).");
                return;
            }

            List<FloweryVoice> voices;
            try
            {
                using var http = new HttpClient();
                http.DefaultRequestHeaders.UserAgent.ParseAdd("Mewdeko/1.0");
                var json = await http.GetStringAsync("https://api.flowery.pw/v1/tts/voices");
                var response = JsonSerializer.Deserialize<FloweryVoiceResponse>(json,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                voices = response?.Voices ?? [];
            }
            catch
            {
                await ReplyErrorAsync("Failed to fetch voice list from Flowery TTS API.");
                return;
            }

            var results = voices
                .Where(v => v.Source is not "SAM")
                .Where(v =>
                    v.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (v.Gender?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (v.Language?.Name?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (v.Language?.Code?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (v.Source?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false))
                .ToList();

            if (results.Count == 0)
            {
                await ReplyErrorAsync($"No voices found matching **{search}**.");
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

            await interactiveService.SendPaginatorAsync(paginator, ctx.Channel, TimeSpan.FromMinutes(5));

            IPage GeneratePage(IComponentPaginator p)
            {
                var startIndex = p.CurrentPageIndex * voicesPerPage;
                var voicesOnPage = results.Skip(startIndex).Take(voicesPerPage).ToList();

                var containerComponents = new List<IMessageComponentBuilder>();

                containerComponents.Add(new TextDisplayBuilder()
                    .WithContent($"# TTS Voices matching \"{search}\" ({results.Count} total)"));

                containerComponents.Add(new SeparatorBuilder());

                var lines = voicesOnPage.Select((v, i) =>
                    $"`{startIndex + i + 1}.` **{v.Name}** — {v.Gender}, {v.Language?.Name} ({v.Source})");

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
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task TtsBlock(IGuildUser user)
        {
            var (player, result) = await GetPlayerAsync(false);
            if (result is not null)
            {
                await SendErrorAsync(result);
                return;
            }

            var isBlocked = await player.IsTtsUserBlockedAsync(user.Id);
            await player.UpsertTtsUserSettingAsync(user.Id, s => s.IsBlocked = !isBlocked);

            await ReplyConfirmAsync(!isBlocked
                ? $"**{user.DisplayName}** has been blocked from TTS."
                : $"**{user.DisplayName}** has been unblocked from TTS.");
        }

        /// <summary>
        ///     Lists all users blocked from TTS in this server.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task TtsBlockList()
        {
            await using var db = await dbFactory.CreateConnectionAsync();
            var blockedUsers = await db.GetTable<TtsUserSetting>()
                .Where(x => x.GuildId == ctx.Guild.Id && x.IsBlocked)
                .ToListAsync();

            if (blockedUsers.Count == 0)
            {
                await ReplyConfirmAsync("No users are blocked from TTS.");
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

            await interactiveService.SendPaginatorAsync(paginator, ctx.Channel, TimeSpan.FromMinutes(5));

            IPage GeneratePage(IComponentPaginator p)
            {
                var startIndex = p.CurrentPageIndex * usersPerPage;
                var usersOnPage = blockedUsers.Skip(startIndex).Take(usersPerPage).ToList();

                var containerComponents = new List<IMessageComponentBuilder>();

                containerComponents.Add(new TextDisplayBuilder()
                    .WithContent($"# TTS Blocked Users ({blockedUsers.Count} total)"));

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
        ///     Sets the role required to use TTS. Use without arguments to remove the restriction.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task TtsRole(IRole role = null)
        {
            var (player, result) = await GetPlayerAsync(false);
            if (result is not null)
            {
                await SendErrorAsync(result);
                return;
            }

            await player.UpdateTtsGuildSettingAsync(s => s.TtsRoleId = role?.Id);

            await ReplyConfirmAsync(role is null
                ? "TTS role restriction removed. Anyone can use TTS."
                : $"TTS restricted to users with the **{role.Name}** role.");
        }

        /// <summary>
        ///     Toggles whether TTS reads reply context (e.g. "replying to User").
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task TtsReplyContext()
        {
            var (player, result) = await GetPlayerAsync(false);
            if (result is not null)
            {
                await SendErrorAsync(result);
                return;
            }

            var settings = await player.GetTtsGuildSettings();
            var newVal = !settings.TtsReplyContext;
            await player.UpdateTtsGuildSettingAsync(s => s.TtsReplyContext = newVal);

            await ReplyConfirmAsync(newVal
                ? "TTS will now read reply context."
                : "TTS will no longer read reply context.");
        }

        /// <summary>
        ///     Toggles whether TTS narrates attachments (e.g. "sent an image").
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task TtsAttachmentNarration()
        {
            var (player, result) = await GetPlayerAsync(false);
            if (result is not null)
            {
                await SendErrorAsync(result);
                return;
            }

            var settings = await player.GetTtsGuildSettings();
            var newVal = !settings.TtsAttachmentNarration;
            await player.UpdateTtsGuildSettingAsync(s => s.TtsAttachmentNarration = newVal);

            await ReplyConfirmAsync(newVal
                ? "TTS will now narrate attachments."
                : "TTS will no longer narrate attachments.");
        }

        /// <summary>
        ///     Toggles whether consecutive messages from the same user skip the name prefix.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task TtsConsecutiveGrouping()
        {
            var (player, result) = await GetPlayerAsync(false);
            if (result is not null)
            {
                await SendErrorAsync(result);
                return;
            }

            var settings = await player.GetTtsGuildSettings();
            var newVal = !settings.TtsConsecutiveGrouping;
            await player.UpdateTtsGuildSettingAsync(s => s.TtsConsecutiveGrouping = newVal);

            await ReplyConfirmAsync(newVal
                ? "Consecutive messages from the same user will skip the name prefix."
                : "Every message will include the speaker's name.");
        }

        /// <summary>
        ///     Sets the maximum TTS queue size (1-50).
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task TtsMaxQueue(int size)
        {
            if (size is < 1 or > 50)
            {
                await ReplyErrorAsync("Queue size must be between 1 and 50.");
                return;
            }

            var (player, result) = await GetPlayerAsync(false);
            if (result is not null)
            {
                await SendErrorAsync(result);
                return;
            }

            await player.UpdateTtsGuildSettingAsync(s => s.TtsMaxQueueSize = size);
            await ReplyConfirmAsync($"TTS max queue size set to **{size}**.");
        }

        /// <summary>
        ///     Shows the current TTS settings for this guild and voice channel.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task TtsSettings()
        {
            var (player, result) = await GetPlayerAsync(false);
            if (result is not null)
            {
                await SendErrorAsync(result);
                return;
            }

            var settings = await player.GetTtsGuildSettings();
            var vcSetting = await player.GetTtsVcSettingAsync(player.VoiceChannelId);
            var userSetting = await player.GetTtsUserSettingAsync(ctx.User.Id);

            var vcEnabled = vcSetting?.Enabled == true ? "Yes" : "No";
            var linkedChannel = vcSetting?.LinkedTextChannelId.HasValue == true
                ? $"<#{vcSetting.LinkedTextChannelId.Value}>"
                : "None (VC text chat only)";
            var announce = vcSetting?.AnnounceJoinLeave == true ? "Yes" : "No";
            var joinFmt = vcSetting?.JoinFormat ?? "{user} joined the channel";
            var leaveFmt = vcSetting?.LeaveFormat ?? "{user} left the channel";
            var defaultVoice = string.IsNullOrWhiteSpace(settings.TtsDefaultVoice)
                ? "System default"
                : settings.TtsDefaultVoice;
            var yourVoice = string.IsNullOrWhiteSpace(userSetting.Voice) ? "Server default" : userSetting.Voice;
            var roleText = settings.TtsRoleId.HasValue ? $"<@&{settings.TtsRoleId.Value}>" : "None (everyone)";

            var components = new ComponentBuilderV2()
                .WithContainer([
                    new TextDisplayBuilder("# TTS Settings")
                ], Mewdeko.OkColor)
                .WithSeparator()
                .WithContainer(new TextDisplayBuilder(
                    $"**VC TTS Enabled:** {vcEnabled}\n" +
                    $"**Linked Text Channel:** {linkedChannel}\n" +
                    $"**Join/Leave Announce:** {announce}\n" +
                    $"**Join Format:** {joinFmt}\n" +
                    $"**Leave Format:** {leaveFmt}"))
                .WithSeparator()
                .WithContainer(new TextDisplayBuilder(
                    $"**Volume:** {settings.TtsVolume}%\n" +
                    $"**Speed:** {settings.TtsSpeed:F1}x\n" +
                    $"**Default Voice:** {defaultVoice}\n" +
                    $"**Your Voice:** {yourVoice}\n" +
                    $"**Required Role:** {roleText}\n" +
                    $"**Reply Context:** {(settings.TtsReplyContext ? "Yes" : "No")}\n" +
                    $"**Attachment Narration:** {(settings.TtsAttachmentNarration ? "Yes" : "No")}\n" +
                    $"**Consecutive Grouping:** {(settings.TtsConsecutiveGrouping ? "Yes" : "No")}\n" +
                    $"**Max Queue Size:** {settings.TtsMaxQueueSize}"));

            await ctx.Channel.SendMessageAsync(components: components.Build(),
                flags: MessageFlags.ComponentsV2, allowedMentions: AllowedMentions.None);
        }

        /// <summary>
        ///     Removes all TTS settings for the current voice channel.
        /// </summary>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.ManageGuild)]
        public async Task TtsRemove()
        {
            var (player, result) = await GetPlayerAsync(false);
            if (result is not null)
            {
                await SendErrorAsync(result);
                return;
            }

            await player.RemoveTtsVcSettingAsync(player.VoiceChannelId);
            await ReplyConfirmAsync("TTS settings removed for this voice channel.");
        }

        private async Task SendErrorAsync(string message)
        {
            var components = new ComponentBuilderV2()
                .WithContainer([
                    new TextDisplayBuilder($"# {Strings.MusicPlayerError(ctx.Guild.Id)}")
                ], Mewdeko.ErrorColor)
                .WithSeparator()
                .WithContainer(new TextDisplayBuilder(message));

            await ctx.Channel.SendMessageAsync(components: components.Build(),
                flags: MessageFlags.ComponentsV2, allowedMentions: AllowedMentions.None);
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

                await result.Player.SetVolumeAsync(await result.Player.GetVolume() / 100f).ConfigureAwait(false);

                if (result.IsSuccess) return (result.Player, null);
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
    }
}

file class FloweryVoiceResponse
{
    public List<FloweryVoice> Voices { get; set; } = [];
}

file class FloweryVoice
{
    public string Name { get; } = "";
    public string? Gender { get; set; }
    public string? Source { get; set; }
    public FloweryLanguage? Language { get; set; }
}

file class FloweryLanguage
{
    public string? Name { get; set; }
    public string? Code { get; set; }
}