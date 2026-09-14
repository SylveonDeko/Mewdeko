using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using MathNet.Symbolics;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.TypeReaders;
using Mewdeko.Modules.Utility.Common;
using Mewdeko.Modules.Utility.Services;
using Mewdeko.Services.Settings;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot.SkiaSharp;
using Embed = Discord.Embed;
using StringExtensions = Mewdeko.Extensions.StringExtensions;

namespace Mewdeko.Modules.Utility;

public partial class SlashUtility
{
    /// <summary>
    ///     Lookup and miscellaneous utility commands such as ids, role lists, emote lists, chat saving and media
    ///     conversion.
    /// </summary>
    [Group("info", "Ids, role lists, emotes, chat saving and other utilities")]
    public class UtilityInfo(
        DiscordShardedClient client,
        IBotCredentials creds,
        BotConfigService config,
        InteractiveService interactivity,
        DownloadTracker tracker,
        MediaConversionService mediaConversionService,
        VerboseErrorsService verboseErrors,
        ILogger<UtilityInfo> logger) : MewdekoSlashSubmodule<UtilityService>
    {
        /// <summary>
        ///     Gets the role id of a specified role.
        /// </summary>
        /// <param name="role">The role to get the id of.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("role-id", "Gets the id of a role")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public Task RoleId(IRole role)
        {
            return ReplyConfirmAsync(Strings.Roleid(ctx.Guild.Id, Strings.Id(ctx.Guild.Id),
                Format.Bold(role.ToString()), Format.Code(role.Id.ToString())));
        }

        /// <summary>
        ///     Gets the channel id of the current channel.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("channel-id", "Gets the id of the current channel")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public Task ChannelId()
        {
            return ReplyConfirmAsync(Strings.Channelid(ctx.Guild.Id, Strings.Id(ctx.Guild.Id),
                Format.Code(ctx.Channel.Id.ToString())));
        }

        /// <summary>
        ///     Gets the server id of the current server.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("server-id", "Gets the id of this server")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public Task ServerId()
        {
            return ReplyConfirmAsync(Strings.Serverid(ctx.Guild.Id, Strings.Id(ctx.Guild.Id),
                Format.Code(ctx.Guild.Id.ToString())));
        }

        /// <summary>
        ///     Gets a list of roles in the current server. Shows a user's roles if a user is specified.
        /// </summary>
        /// <param name="target">The user to get the roles of.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("roles", "Lists the roles of the server or of a user")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task Roles(IGuildUser? target = null)
        {
            var guild = ctx.Guild;

            if (target != null)
            {
                var roles = target.GetRoles().Except([
                    guild.EveryoneRole
                ]).OrderBy(r => -r.Position);
                if (!roles.Any())
                {
                    await ReplyErrorAsync(Strings.NoRolesOnPage(ctx.Guild.Id)).ConfigureAwait(false);
                }
                else
                {
                    var paginator = new LazyPaginatorBuilder()
                        .AddUser(ctx.User)
                        .WithPageFactory(PageFactory)
                        .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                        .WithMaxPageIndex(roles.Count() / 10)
                        .WithDefaultCanceledPage()
                        .WithDefaultEmotes()
                        .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                        .Build();
                    await interactivity.SendPaginatorAsync(paginator, (ctx.Interaction as SocketInteraction)!,
                        TimeSpan.FromMinutes(60));

                    async Task<PageBuilder> PageFactory(int page)
                    {
                        await Task.CompletedTask;
                        return new PageBuilder().WithOkColor().WithTitle(Strings.RolesListFor(ctx.Guild.Id, target))
                            .WithDescription(string.Join("\n",
                                roles.Skip(page * 10).Take(10).Select(x =>
                                    $"{x.Mention} | {x.Id} | {x.GetMembersAsync().GetAwaiter().GetResult().Count()} Members")));
                    }
                }
            }
            else
            {
                var roles = guild.Roles.Except([
                    guild.EveryoneRole
                ]).OrderBy(r => -r.Position);
                if (!roles.Any())
                {
                    await ReplyErrorAsync(Strings.NoRolesOnPage(ctx.Guild.Id)).ConfigureAwait(false);
                }
                else
                {
                    var paginator = new LazyPaginatorBuilder()
                        .AddUser(ctx.User)
                        .WithPageFactory(PageFactory)
                        .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                        .WithMaxPageIndex(roles.Count() / 10)
                        .WithDefaultCanceledPage()
                        .WithDefaultEmotes()
                        .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                        .Build();
                    await interactivity.SendPaginatorAsync(paginator, (ctx.Interaction as SocketInteraction)!,
                        TimeSpan.FromMinutes(60));

                    async Task<PageBuilder> PageFactory(int page)
                    {
                        await Task.CompletedTask;
                        return new PageBuilder().WithOkColor().WithTitle(Strings.GuildRolesList(ctx.Guild.Id))
                            .WithDescription(string.Join("\n",
                                roles.Skip(page * 10).Take(10).Select(x => x as SocketRole)
                                    .Select(x =>
                                        $"{x.Mention} | {x.Id} | {x.GetMembersAsync().GetAwaiter().GetResult().Count()}")));
                    }
                }
            }
        }

        /// <summary>
        ///     Shows a list of users in a specified role. If a second role is given, shows users that have both roles.
        /// </summary>
        /// <param name="role">The role to search for.</param>
        /// <param name="role2">Optional second role the users must also have.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("in-role", "Lists users in a role, or users that have both of two roles")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task InRole(IRole role, IRole? role2 = null)
        {
            await DeferAsync();
            await tracker.EnsureUsersDownloadedAsync(ctx.Guild).ConfigureAwait(false);
            var users = await ctx.Guild.GetUsersAsync().ConfigureAwait(false);

            if (role2 is not null)
            {
                var bothUsers = users
                    .Where(u => u.RoleIds.Contains(role.Id) && u.RoleIds.Contains(role2.Id))
                    .Select(u => u.ToString())
                    .ToArray();

                var bothPaginator = new LazyPaginatorBuilder()
                    .AddUser(ctx.User)
                    .WithPageFactory(BothPageFactory)
                    .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                    .WithMaxPageIndex(bothUsers.Length / 20)
                    .WithDefaultEmotes()
                    .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                    .Build();

                await interactivity.SendPaginatorAsync(bothPaginator, (ctx.Interaction as SocketInteraction)!,
                    TimeSpan.FromMinutes(60),
                    InteractionResponseType.DeferredChannelMessageWithSource).ConfigureAwait(false);

                async Task<PageBuilder> BothPageFactory(int page)
                {
                    await Task.CompletedTask.ConfigureAwait(false);
                    return new PageBuilder().WithOkColor()
                        .WithTitle(Format.Bold(
                            Strings.UsersInRoles(ctx.Guild.Id, role.Name, role2.Name, bothUsers.Length)))
                        .WithDescription(string.Join("\n",
                            bothUsers.Skip(page * 20).Take(20)));
                }

                return;
            }

            var roleUsers = users
                .Where(u => u.RoleIds.Contains(role.Id))
                .ToArray();

            var paginator = new LazyPaginatorBuilder()
                .AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(roleUsers.Length / 20)
                .WithDefaultEmotes()
                .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                .Build();

            await interactivity.SendPaginatorAsync(paginator, (ctx.Interaction as SocketInteraction)!,
                TimeSpan.FromMinutes(60),
                InteractionResponseType.DeferredChannelMessageWithSource).ConfigureAwait(false);

            async Task<PageBuilder> PageFactory(int page)
            {
                await Task.CompletedTask.ConfigureAwait(false);
                return new PageBuilder().WithOkColor()
                    .WithTitle(
                        $"{Format.Bold(Strings.InroleList(ctx.Guild.Id, Format.Bold(role.Name)))} - {roleUsers.Length}")
                    .WithDescription(string.Join("\n",
                        roleUsers.Skip(page * 20).Take(20)
                            .Select(x => $"{x} `{x.Id}`"))).AddField("User Stats",
                        Strings.InRoleStatusCounts(ctx.Guild.Id,
                            roleUsers.Count(x => x.Status == UserStatus.Online),
                            roleUsers.Count(x => x.Status == UserStatus.DoNotDisturb),
                            roleUsers.Count(x => x.Status == UserStatus.Idle),
                            roleUsers.Count(x => x.Status == UserStatus.Offline)));
            }
        }

        /// <summary>
        ///     Gets the topic of the current channel. Shows the topic of a specified channel if one is specified.
        /// </summary>
        /// <param name="channel">The channel to get the topic of.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("channel-topic", "Shows the topic of the current or given channel")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task ChannelTopic(ITextChannel? channel = null)
        {
            channel ??= (ITextChannel)ctx.Channel;

            var topic = channel.Topic;
            if (string.IsNullOrWhiteSpace(topic))
                await ReplyErrorAsync(Strings.NoTopicSet(ctx.Guild.Id)).ConfigureAwait(false);
            else
                await ctx.Interaction.RespondAsync(embed: new EmbedBuilder().WithOkColor()
                        .WithTitle(Strings.ChannelTopic(ctx.Guild.Id)).WithDescription(topic).Build())
                    .ConfigureAwait(false);
        }

        /// <summary>
        ///     Gets the emotes in a guild. If an emote type is specified, only the emotes of that type will be listed.
        /// </summary>
        /// <param name="emotetype">The type of emotes to list (animated or nonanimated).</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("emote-list", "Lists the emotes of this server")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task EmoteList(
            [Summary("type", "Only list animated or non animated emotes")]
            [Choice("animated", "animated")]
            [Choice("nonanimated", "nonanimated")]
            string? emotetype = null)
        {
            var emotes = emotetype switch
            {
                "animated" => ctx.Guild.Emotes.Where(x => x.Animated).ToArray(),
                "nonanimated" => ctx.Guild.Emotes.Where(x => !x.Animated).ToArray(),
                _ => ctx.Guild.Emotes.ToArray()
            };

            if (emotes.Length == 0)
            {
                await ErrorAsync(Strings.NoEmotes(ctx.Guild.Id));
                return;
            }

            var paginator = new LazyPaginatorBuilder()
                .AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(emotes.Length / 10)
                .WithDefaultEmotes()
                .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                .Build();

            await interactivity.SendPaginatorAsync(paginator, (ctx.Interaction as SocketInteraction)!,
                TimeSpan.FromMinutes(60)).ConfigureAwait(false);

            async Task<PageBuilder> PageFactory(int page)
            {
                await Task.CompletedTask.ConfigureAwait(false);
                var titleText = emotetype switch
                {
                    "animated" => $"{emotes.Length} Animated Emotes",
                    "nonanimated" => $"{emotes.Length} Non Animated Emotes",
                    _ =>
                        $"{emotes.Count(x => x.Animated)} Animated Emotes | {emotes.Count(x => !x.Animated)} Non Animated Emotes"
                };

                return new PageBuilder()
                    .WithTitle(titleText)
                    .WithDescription(string.Join("\n",
                        emotes.OrderBy(x => x.Name).Skip(10 * page).Take(10)
                            .Select(x => $"{x} `{x.Name}` [Link]({x.Url})")))
                    .WithOkColor();
            }
        }

        /// <summary>
        ///     Saves the chat log of a channel. Public mewdeko saves this to the nginx cdn then sends you a link to display it
        ///     on the cdn.
        /// </summary>
        /// <param name="time">How far back to save, for example 1h or 2d. Maximum of 3 days.</param>
        /// <param name="channel">The channel to save. Defaults to the current channel.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("save-chat", "Saves the chat log of a channel as an html file")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageMessages)]
        public async Task SaveChat(
            [Summary("time", "How far back to save, for example 1h or 2d. Maximum of 3 days")]
            TimeSpan time,
            ITextChannel? channel = null)
        {
            var curTime = DateTime.UtcNow.Subtract(time);
            if (!Directory.Exists(creds.ChatSavePath))
            {
                await ErrorAsync(Strings.ChatSaveMissing(ctx.Guild.Id));
                return;
            }

            var secureString = StringExtensions.GenerateSecureString(16);
            try
            {
                Directory.CreateDirectory($"{creds.ChatSavePath}/{ctx.Guild.Id}/{secureString}");
            }
            catch (Exception ex)
            {
                await ErrorAsync(Strings.FailedToCreateDirectory(ctx.Guild.Id, ex.Message)).ConfigureAwait(false);
                return;
            }

            if (time.Days > 3)
            {
                await ErrorAsync(Strings.MaxTimeLimit(ctx.Guild.Id));
                return;
            }

            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                Arguments =
                    $"../ChatExporter/DiscordChatExporter.Cli.dll export -t {creds.Token} -c {channel?.Id ?? ctx.Channel.Id} --after {curTime:yyyy-MM-ddTHH:mm:ssZ} --output \"{creds.ChatSavePath}/{ctx.Guild.Id}/{secureString}/{ctx.Guild.Name.Replace(" ", "-")}-{(channel?.Name ?? ctx.Channel.Name).Replace(" ", "-")}-{curTime:yyyy-MM-ddTHH-mm-ssZ}.html\" --media true",
                FileName = "dotnet",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            process.Start();
            await ConfirmAsync(Strings.SavingChatLog(ctx.Guild.Id, config.Data.LoadingEmote));

            await process.WaitForExitAsync().ConfigureAwait(false);
            if (creds.ChatSavePath.Contains("/usr/share/nginx/cdn"))
            {
                var fileName =
                    $"{ctx.Guild.Name.Replace(" ", "-")}-{(channel?.Name ?? ctx.Channel.Name).Replace(" ", "-")}-{curTime:yyyy-MM-ddTHH-mm-ssZ}.html";
                await ctx.User.SendConfirmAsync(
                        Strings.ChatLogUrlCdn(ctx.Guild.Id, ctx.Guild.Id, secureString, fileName))
                    .ConfigureAwait(false);
            }
            else
                await ConfirmAsync(Strings.ChatLogUrlLocal(ctx.Guild.Id, creds.ChatSavePath, ctx.Guild.Id,
                    secureString)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Checks a url for viruses using the virustotal api.
        /// </summary>
        /// <param name="url">The url to check.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("vcheck", "Checks a url for viruses using virustotal")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task VCheck([Summary("url", "The url to check")] string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                await ErrorAsync(Strings.UrlMissing(ctx.Guild.Id));
                return;
            }

            await DeferAsync();
            var result = await UtilityService.UrlChecker(url).ConfigureAwait(false);
            var eb = new EmbedBuilder();
            eb.WithOkColor();
            eb.WithDescription(result.Permalink);
            eb.AddField("Virus Positives", result.Positives, true);
            eb.AddField("Number of scans", result.Total, true);
            await ctx.Interaction.FollowupAsync(embed: eb.Build()).ConfigureAwait(false);
        }

        /// <summary>
        ///     Shows a list of users playing a specified game.
        /// </summary>
        /// <param name="game">The game to search for.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("whos-playing", "Shows users playing the given game")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task WhosPlaying([Summary("game", "The game to search for")] string game)
        {
            game = game.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(game))
                return;

            if (ctx.Guild is not SocketGuild socketGuild)
            {
                logger.LogWarning("Can't cast guild to socket guild");
                return;
            }

            var rng = new MewdekoRandom();
            var arr = await Task.Run(() => socketGuild.Users
                .Where(x => x.Activities.Any())
                .Where(u => u.Activities.FirstOrDefault().Name.ToUpperInvariant().Contains(game))
                .OrderBy(_ => rng.Next())
                .ToArray()).ConfigureAwait(false);

            var i = 0;
            if (arr.Length == 0)
            {
                await ReplyErrorAsync(Strings.NobodyPlayingGame(ctx.Guild.Id)).ConfigureAwait(false);
            }
            else
            {
                var paginator = new LazyPaginatorBuilder()
                    .AddUser(ctx.User)
                    .WithPageFactory(PageFactory)
                    .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                    .WithMaxPageIndex(arr.Length / 20)
                    .WithDefaultEmotes()
                    .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                    .Build();

                await interactivity.SendPaginatorAsync(paginator, (ctx.Interaction as SocketInteraction)!,
                    TimeSpan.FromMinutes(60)).ConfigureAwait(false);

                async Task<PageBuilder> PageFactory(int page)
                {
                    await Task.CompletedTask.ConfigureAwait(false);
                    var pagebuilder = new PageBuilder().WithOkColor()
                        .WithDescription(string.Join("\n",
                            arr.Skip(page * 20).Take(20).Select(x =>
                                $"{i++ + 1}. {x.Username}#{x.Discriminator} `{x.Id}`: `{(x.Activities.FirstOrDefault() is CustomStatusGame cs ? cs.State : x.Activities.FirstOrDefault().Name)}`")));
                    return pagebuilder;
                }
            }
        }

        /// <summary>
        ///     Enlarges one or more specified custom emojis.
        /// </summary>
        /// <param name="emojis">The custom emojis to enlarge, separated by spaces.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("show-emojis", "Shows the image links of the given custom emojis")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task Showemojis([Summary("emojis", "Custom emojis separated by spaces")] string emojis)
        {
            var tags = emojis.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => Emote.TryParse(x, out var emote) ? emote : null)
                .Where(x => x is not null);

            var result = string.Join("\n", tags.Select(m => Strings.Showemojis(ctx.Guild.Id, m, m.Url)));

            if (string.IsNullOrWhiteSpace(result))
                await ReplyErrorAsync(Strings.ShowemojisNone(ctx.Guild.Id)).ConfigureAwait(false);
            else
                await ctx.Interaction.RespondAsync(result.TrimTo(2000)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Converts an attached media file to a different format.
        /// </summary>
        /// <param name="targetFormat">The format to convert to (e.g., gif, mp4, mp3, jpg, png).</param>
        /// <param name="file">The media file to convert.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("convert", "Converts a media file to a different format")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task Convert(
            [Summary("format", "The format to convert to, for example gif, mp4, mp3, jpg or png")]
            string targetFormat,
            [Summary("file", "The media file to convert")]
            IAttachment file)
        {
            if (string.IsNullOrWhiteSpace(targetFormat))
            {
                await ErrorAsync(Strings.ConvertMediaNoFormat(ctx.Guild.Id, Config.ErrorEmote)).ConfigureAwait(false);
                return;
            }

            var attachment = file;
            if (attachment == null)
            {
                await ErrorAsync(Strings.ConvertMediaNoFile(ctx.Guild.Id, Config.ErrorEmote)).ConfigureAwait(false);
                return;
            }

            if (!IsValidFilename(attachment.Filename))
            {
                await ErrorAsync(Strings.ConvertMediaError(ctx.Guild.Id, Config.ErrorEmote,
                    Strings.ConvertMediaInvalidFilename(ctx.Guild.Id))).ConfigureAwait(false);
                return;
            }

            var inputExtension = Path.GetExtension(attachment.Filename).ToLower().TrimStart('.');
            var outputExtension = targetFormat.ToLower().TrimStart('.');

            var allowedInputFormats = new[]
            {
                "mp4", "gif", "mp3", "wav", "jpg", "jpeg", "png", "webp", "webm", "avi", "mov", "mkv", "flv", "m4a",
                "ogg", "bmp", "tiff", "svg", "flac", "alac", "ape"
            };
            if (!allowedInputFormats.Contains(inputExtension))
            {
                await ErrorAsync(Strings.ConvertMediaError(ctx.Guild.Id, Config.ErrorEmote,
                    Strings.ConvertMediaInputNotSupported(ctx.Guild.Id, inputExtension))).ConfigureAwait(false);
                return;
            }

            var supportedFormats = new[]
            {
                "mp4", "gif", "mp3", "wav", "jpg", "jpeg", "png", "webp", "webm", "avi", "mov", "mkv", "flv"
            };

            if (!supportedFormats.Contains(outputExtension))
            {
                await ErrorAsync(Strings.ConvertMediaUnsupported(ctx.Guild.Id, Config.ErrorEmote, outputExtension,
                    string.Join(", ", supportedFormats))).ConfigureAwait(false);
                return;
            }

            if (attachment.Size > 100 * 1024 * 1024)
            {
                await ErrorAsync(Strings.ConvertMediaTooLarge(ctx.Guild.Id, Config.ErrorEmote)).ConfigureAwait(false);
                return;
            }

            var request = new ConversionRequest
            {
                FileUrl = attachment.Url,
                InputExtension = inputExtension,
                OutputExtension = outputExtension,
                OriginalFilename = attachment.Filename,
                GuildId = ctx.Guild.Id
            };

            var (queuePosition, estimatedWait) = mediaConversionService.EnqueueConversion(request);
            var (queueLength, activeConversions) = mediaConversionService.GetQueueStats();

            var queueMessage = queuePosition == 1 && activeConversions < 8
                ? Strings.ConvertMediaProcessingNow(ctx.Guild.Id, Config.LoadingEmote)
                : Strings.ConvertMediaQueued(ctx.Guild.Id, Config.LoadingEmote, queuePosition,
                    estimatedWait.TotalSeconds, activeConversions);

            var queueEmbed = new EmbedBuilder()
                .WithColor(Mewdeko.OkColor)
                .WithDescription(queueMessage)
                .Build();

            await ctx.Interaction.RespondAsync(embed: queueEmbed).ConfigureAwait(false);

            try
            {
                var result = await request.CompletionSource.Task.ConfigureAwait(false);

                if (!result.Success)
                {
                    var errorMessage = result.Error switch
                    {
                        "CONVERSION_TIMEOUT" => Strings.ConvertMediaTimeout(ctx.Guild.Id, Config.ErrorEmote),
                        "NO_OUTPUT_FILE" => Strings.ConvertMediaNoOutput(ctx.Guild.Id, Config.ErrorEmote),
                        "FILE_TOO_LARGE" => Strings.ConvertMediaDiscordLimit(ctx.Guild.Id, Config.ErrorEmote),
                        var error when error.StartsWith("CONVERSION_FAILED|") =>
                            Strings.ConvertMediaFailed(ctx.Guild.Id, Config.ErrorEmote, error.Split('|')[1]),
                        var error when error.StartsWith("GENERAL_ERROR|") =>
                            Strings.ConvertMediaError(ctx.Guild.Id, Config.ErrorEmote, error.Split('|')[1]),
                        _ => Strings.ConvertMediaError(ctx.Guild.Id, Config.ErrorEmote, result.Error)
                    };
                    await ErrorAsync(errorMessage).ConfigureAwait(false);
                    return;
                }

                var originalBaseName = SanitizeFilename(Path.GetFileNameWithoutExtension(attachment.Filename));
                var outputFilename = originalBaseName + "." + outputExtension;

                var successMessage =
                    Strings.ConvertMediaSuccess(ctx.Guild.Id, Config.SuccessEmote, inputExtension, outputExtension);
                if (IsLosslessToLossyConversion(inputExtension, outputExtension))
                {
                    successMessage += GetLosslessToLossyEasterEgg(ctx.Guild.Id);
                }

                var embed = new EmbedBuilder()
                    .WithColor(Mewdeko.OkColor)
                    .WithDescription(successMessage)
                    .Build();

                using var stream = new MemoryStream(result.Data);
                await ctx.Interaction.FollowupWithFileAsync(stream, outputFilename, embed: embed)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await ErrorAsync(Strings.ConvertMediaError(ctx.Guild.Id, Config.ErrorEmote, ex.Message))
                    .ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Toggles link previews on or off for the server.
        /// </summary>
        /// <param name="enabled">Whether link previews should be enabled.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("preview-links", "Enables or disables link previews for this server")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task PreviewLinks(bool enabled)
        {
            await Service.PreviewLinks(ctx.Guild, enabled ? "y" : "n").ConfigureAwait(false);
            switch (await Service.GetPLinks(ctx.Guild.Id))
            {
                case 1:
                    await ConfirmAsync(Strings.LinkPreviewsEnabled(ctx.Guild.Id));
                    break;
                case 0:
                    await ConfirmAsync(Strings.LinkPreviewsDisabled(ctx.Guild.Id));
                    break;
            }
        }

        /// <summary>
        ///     Toggles verbose error messages for commands.
        /// </summary>
        /// <param name="newstate">The new state of verbose errors. If null, the state will be toggled.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("verbose-error", "Toggles verbose command error messages")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageMessages)]
        public async Task VerboseError(
            [Summary("enabled", "The new state. Leave empty to toggle")]
            bool? newstate = null)
        {
            var state = await verboseErrors.ToggleVerboseErrors(ctx.Guild.Id, newstate);

            if (state)
                await ReplyConfirmAsync(Strings.VerboseErrorsEnabled(ctx.Guild.Id)).ConfigureAwait(false);
            else
                await ReplyConfirmAsync(Strings.VerboseErrorsDisabled(ctx.Guild.Id)).ConfigureAwait(false);
        }

        private static bool IsValidFilename(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
                return false;

            if (filename.Length > 200)
                return false;

            var fileName = Path.GetFileName(filename);
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            if (filename.Contains("..") || filename.Contains("./") || filename.Contains(".\\"))
                return false;

            if (filename.Contains('\0'))
                return false;

            var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName).ToUpperInvariant();
            var reservedNames = new[]
            {
                "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
                "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
            };
            if (reservedNames.Contains(nameWithoutExt))
                return false;

            var invalidChars = Path.GetInvalidFileNameChars().Concat([
                '<', '>', ':', '"', '|', '?', '*', ';', '&', '$', '`'
            ]).ToArray();
            if (filename.IndexOfAny(invalidChars) >= 0)
                return false;

            if (filename.StartsWith('.') || filename.EndsWith('.') || filename.Contains("..."))
                return false;

            return true;
        }

        private static string SanitizeFilename(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
                return "converted_file";

            var invalidChars = Path.GetInvalidFileNameChars().Concat([
                '<', '>', ':', '"', '|', '?', '*', ';', '&', '$', '`', '.'
            ]).ToArray();
            var sanitized = string.Join("_", filename.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));

            if (string.IsNullOrWhiteSpace(sanitized))
                return "converted_file";

            if (sanitized.Length > 50)
                sanitized = sanitized[..50];

            sanitized = sanitized.Trim();

            return string.IsNullOrWhiteSpace(sanitized) ? "converted_file" : sanitized;
        }

        private static bool IsLosslessToLossyConversion(string inputExt, string outputExt)
        {
            var losslessFormats = new[]
            {
                "wav", "flac", "aiff", "alac", "ape", "wv"
            };
            var lossyFormats = new[]
            {
                "mp3", "aac", "ogg", "m4a", "wma"
            };

            return losslessFormats.Contains(inputExt.ToLower()) && lossyFormats.Contains(outputExt.ToLower());
        }

        private string GetLosslessToLossyEasterEgg(ulong guildId)
        {
            var random = new Random();
            var messageIndex = random.Next(1, 9);

            return messageIndex switch
            {
                1 => Strings.ConvertLosslessToLossyOne(guildId),
                2 => Strings.ConvertLosslessToLossyTwo(guildId),
                3 => Strings.ConvertLosslessToLossyThree(guildId),
                4 => Strings.ConvertLosslessToLossyFour(guildId),
                5 => Strings.ConvertLosslessToLossyFive(guildId),
                6 => Strings.ConvertLosslessToLossySix(guildId),
                7 => Strings.ConvertLosslessToLossySeven(guildId),
                8 => Strings.ConvertLosslessToLossyEight(guildId),
                _ => Strings.ConvertLosslessToLossyOne(guildId)
            };
        }
    }

    /// <summary>
    ///     Network utilities such as traceroute and icmp ping.
    /// </summary>
    [Group("net", "Network utilities")]
    public class UtilityNet : MewdekoSlashSubmodule
    {
        /// <summary>
        ///     Executes a traceroute operation to the specified hostname, displaying the route that packets take to reach an
        ///     IP address or domain.
        /// </summary>
        /// <param name="hostname">The IP address or domain name to trace the route to.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("traceroute", "Traces the route packets take to a host")]
        [CheckPermissions]
        [InteractionRatelimit(10)]
        public async Task Traceroute([Summary("hostname", "The ip address or domain to trace")] string hostname)
        {
            await DeferAsync();
            var guildId = ctx.Guild?.Id ?? 0;
            var traceRt = Utility.GetTraceRoute(hostname).ToList();
            if (traceRt.Any())
            {
                var eb = new EmbedBuilder()
                    .WithOkColor()
                    .WithDescription(string.Join("\n", traceRt.Select(x => $"{x}")));
                await ctx.Interaction.FollowupAsync(embed: eb.Build());
            }
            else
                await ErrorAsync(Strings.TracerouteFailed(guildId));
        }

        /// <summary>
        ///     Pings an IP address. Maximum of 10 pings.
        /// </summary>
        /// <param name="ip">The IP address to ping.</param>
        /// <param name="count">The number of pings to send. Default is 1.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("ping-ip", "Pings an ip address or host up to 10 times")]
        [CheckPermissions]
        [InteractionRatelimit(10)]
        public async Task PingIp(
            [Summary("ip", "The ip address or host to ping")]
            string ip,
            [Summary("count", "How many pings to send, maximum of 10")]
            int count = 1)
        {
            var guildId = ctx.Guild?.Id ?? 0;
            if (count > 10)
            {
                await ErrorAsync(Strings.MaximumPings(guildId));
                return;
            }

            await DeferAsync();
            var ping = new Ping();
            var embeds = new List<Embed>();

            for (var i = 0; i < count; i++)
            {
                var pingReply = await ping.SendPingAsync(ip);
                if (pingReply.Status == IPStatus.Unknown)
                {
                    await ErrorAsync(Strings.IcmpEchoFailed(guildId));
                    break;
                }

                if (pingReply.Status == IPStatus.Success)
                {
                    var eb = new EmbedBuilder()
                        .WithTitle(Strings.PingNumber(guildId, i + 1))
                        .WithDescription(Strings.PingAddressLatency(guildId, pingReply.Address,
                            pingReply.RoundtripTime))
                        .WithOkColor();
                    embeds.Add(eb.Build());
                }
                else if (pingReply.Status == IPStatus.DestinationNetworkUnreachable)
                {
                    await ErrorAsync(Strings.NetworkUnreachable(guildId));
                    break;
                }
                else if (pingReply.Status == IPStatus.DestinationHostUnreachable)
                {
                    await ErrorAsync(Strings.HostUnreachable(guildId));
                    break;
                }
                else if (pingReply.Status == IPStatus.DestinationPortUnreachable)
                {
                    await ErrorAsync(Strings.PortUnreachable(guildId));
                    break;
                }
                else if (pingReply.Status == IPStatus.NoResources)
                {
                    await ErrorAsync(Strings.PingNoResources(guildId));
                    break;
                }
                else if (pingReply.Status == IPStatus.BadOption)
                {
                    await ErrorAsync(Strings.PingBadOption(guildId));
                    break;
                }
                else if (pingReply.Status == IPStatus.HardwareError)
                {
                    await ErrorAsync(Strings.PingHardwareError(guildId));
                    break;
                }
                else if (pingReply.Status == IPStatus.TimedOut)
                {
                    await ErrorAsync(Strings.PingTimeout(guildId));
                    break;
                }
                else if (pingReply.Status == IPStatus.BadDestination)
                {
                    await ErrorAsync(Strings.CheckIp(guildId));
                    break;
                }
                else if (pingReply.Status == IPStatus.DestinationUnreachable)
                {
                    await ErrorAsync(Strings.DestinationUnreachable(guildId));
                    break;
                }
            }

            if (embeds.Any())
                await ctx.Interaction.FollowupAsync(embeds: embeds.ToArray());
        }
    }

    /// <summary>
    ///     Calculator functionality including expression evaluation, graphing, and symbolic math.
    /// </summary>
    [Group("calc", "Calculator, graphing and symbolic math")]
    public class UtilityCalc : MewdekoSlashSubmodule
    {
        /// <summary>
        ///     Evaluates a mathematical expression and returns the result.
        /// </summary>
        /// <param name="expression">The expression to evaluate.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [SlashCommand("calculate", "Evaluates a mathematical expression")]
        [CheckPermissions]
        public async Task Calculate([Summary("expression", "The expression to evaluate")] string expression)
        {
            var guildId = ctx.Guild?.Id ?? 0;
            try
            {
                var result = Evaluate(expression);
                await ctx.Interaction.RespondAsync(embed: new EmbedBuilder().WithOkColor()
                    .WithTitle(Strings.CalcResult(guildId)).WithDescription(result.ToString()).Build());
            }
            catch (Exception ex)
            {
                await ctx.Interaction.RespondAsync(embed: new EmbedBuilder().WithErrorColor()
                    .WithTitle(Strings.CalcError(guildId)).WithDescription(ex.Message).Build());
            }
        }

        /// <summary>
        ///     Graphs a mathematical function.
        /// </summary>
        /// <param name="function">The function to graph, using x as the variable.</param>
        /// <param name="start">The start of the x-axis range.</param>
        /// <param name="end">The end of the x-axis range.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [SlashCommand("graph", "Graphs a mathematical function")]
        [CheckPermissions]
        public async Task Graph(
            [Summary("function", "The function to graph, using x as the variable")]
            string function,
            [Summary("start", "Start of the x axis range")]
            double start = -10,
            [Summary("end", "End of the x axis range")]
            double end = 10)
        {
            var guildId = ctx.Guild?.Id ?? 0;
            await DeferAsync();
            try
            {
                var plotModel = new PlotModel
                {
                    Title = Strings.GraphTitle(guildId, function)
                };
                plotModel.Axes.Add(new LinearAxis
                {
                    Position = AxisPosition.Bottom, Title = "X"
                });
                plotModel.Axes.Add(new LinearAxis
                {
                    Position = AxisPosition.Left, Title = "Y"
                });

                var series = new FunctionSeries(
                    x => Evaluate(function.Replace("x", x.ToString())),
                    start, end, 0.1, function);
                plotModel.Series.Add(series);

                using var stream = new MemoryStream();
                var pngExporter = new PngExporter
                {
                    Width = 600, Height = 400
                };
                pngExporter.Export(plotModel, stream);
                stream.Position = 0;

                await ctx.Interaction
                    .FollowupWithFileAsync(stream, "graph.png", Strings.GraphCaption(guildId, function))
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await ctx.Interaction.FollowupAsync(embed: new EmbedBuilder().WithErrorColor()
                    .WithTitle(Strings.GraphError(guildId)).WithDescription(ex.Message).Build());
            }
        }

        /// <summary>
        ///     Performs symbolic mathematics operations, expanding the given expression.
        /// </summary>
        /// <param name="expression">The symbolic expression to evaluate or manipulate.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [SlashCommand("symbolic", "Expands a symbolic expression")]
        [CheckPermissions]
        public async Task Symbolic([Summary("expression", "The symbolic expression to expand")] string expression)
        {
            var guildId = ctx.Guild?.Id ?? 0;
            try
            {
                var expr = Infix.ParseOrThrow(expression);
                var expanded = Algebraic.Expand(expr);
                await ctx.Interaction.RespondAsync(embed: new EmbedBuilder().WithOkColor()
                    .WithTitle(Strings.SymbolicResult(guildId)).WithDescription(Infix.Format(expanded)).Build());
            }
            catch (Exception ex)
            {
                await ctx.Interaction.RespondAsync(embed: new EmbedBuilder().WithErrorColor()
                    .WithTitle(Strings.SymbolicError(guildId)).WithDescription(ex.Message).Build());
            }
        }

        /// <summary>
        ///     Lists available mathematical operations that can be used in expressions.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        [SlashCommand("ops", "Lists the operations the calculator supports")]
        [CheckPermissions]
        public Task CalcOps()
        {
            var guildId = ctx.Guild?.Id ?? 0;
            var operations = new[]
            {
                "+", "-", "*", "/", "^", "sqrt", "abs", "sin", "cos", "tan", "asin", "acos", "atan", "log", "ln", "exp",
                "floor", "ceiling", "round"
            };

            var message = Strings.CalcOps(guildId, "/utility ") + "\n" + string.Join(", ", operations);

            return ctx.Interaction.RespondAsync(embed: new EmbedBuilder().WithOkColor()
                .WithTitle(Strings.CalcOpsTitle(guildId)).WithDescription(message).Build());
        }

        private static double Evaluate(string expression)
        {
            var expr = Infix.ParseOrThrow(expression);
            var variables = new Dictionary<string, FloatingPoint>();
            var result = MathNet.Symbolics.Evaluate.Evaluate(variables, expr);

            if (result != null)
            {
                return result.RealValue;
            }

            throw new InvalidOperationException($"Unable to evaluate expression to a numeric value: {expression}");
        }
    }

    /// <summary>
    ///     Commands for converting units from one system to another.
    /// </summary>
    [Group("units", "Unit conversion")]
    public class UtilityUnits : MewdekoSlashSubmodule<ConverterService>
    {
        /// <summary>
        ///     Lists all available units that can be converted.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("list", "Lists all units that can be converted")]
        [CheckPermissions]
        public Task ConvertList()
        {
            var guildId = ctx.Guild?.Id ?? 0;
            var units = Service.Units;
            var res = units.GroupBy(x => x.UnitType)
                .Aggregate(new EmbedBuilder().WithTitle(Strings.Convertlist(guildId))
                        .WithOkColor(),
                    (embed, g) => embed.AddField(efb =>
                        efb.WithName(g.Key.ToTitleCase())
                            .WithValue(string.Join(", ", g.Select(x => x.Triggers.FirstOrDefault())
                                .OrderBy(x => x)))));
            return ctx.Interaction.RespondAsync(embed: res.Build());
        }

        /// <summary>
        ///     Converts a specified value from one unit to another.
        /// </summary>
        /// <param name="origin">The original unit of the value.</param>
        /// <param name="target">The target unit to convert to.</param>
        /// <param name="value">The value to be converted.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("convert", "Converts a value from one unit to another")]
        [CheckPermissions]
        public async Task ConvertUnits(
            [Summary("origin", "The unit to convert from")]
            string origin,
            [Summary("target", "The unit to convert to")]
            string target,
            [Summary("value", "The value to convert")]
            double value)
        {
            var guildId = ctx.Guild?.Id ?? 0;
            var decimalValue = (decimal)value;
            var originUnit = Array.Find(Service.Units, x =>
                x.Triggers.Select(y => y.ToUpperInvariant()).Contains(origin.ToUpperInvariant()));
            var targetUnit = Array.Find(Service.Units, x =>
                x.Triggers.Select(y => y.ToUpperInvariant()).Contains(target.ToUpperInvariant()));
            if (originUnit == null || targetUnit == null)
            {
                await ReplyErrorAsync(Strings.ConvertNotFound(guildId, Format.Bold(origin), Format.Bold(target)))
                    .ConfigureAwait(false);
                return;
            }

            if (originUnit.UnitType != targetUnit.UnitType)
            {
                await ReplyErrorAsync(Strings.ConvertTypeError(guildId, Format.Bold(originUnit.Triggers.First()),
                    Format.Bold(targetUnit.Triggers.First()))).ConfigureAwait(false);
                return;
            }

            decimal res = 0;
            if (originUnit.Triggers == targetUnit.Triggers)
            {
                res = decimalValue;
            }
            else
                switch (originUnit.UnitType)
                {
                    case "temperature":
                        res = targetUnit.Triggers.First().ToUpperInvariant() switch
                        {
                            "C" => res - 273.15m,
                            "F" => res * (9m / 5m) - 459.67m,
                            _ => originUnit.Triggers.First().ToUpperInvariant() switch
                            {
                                "C" => decimalValue + 273.15m,
                                "F" => (decimalValue + 459.67m) * (5m / 9m),
                                _ => decimalValue
                            }
                        };
                        break;
                    case "currency":
                        res = decimalValue * targetUnit.Modifier / originUnit.Modifier;
                        break;
                    default:
                        res = decimalValue * originUnit.Modifier / targetUnit.Modifier;
                        break;
                }

            res = Math.Round(res, 4);

            await ConfirmAsync(Strings.Convert(guildId, decimalValue, originUnit.Triggers.Last(), res,
                targetUnit.Triggers.Last())).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Commands related to stream roles. When users start streaming they are automatically given a role.
    /// </summary>
    [Group("stream-role", "Give a role to users while they are streaming")]
    public class UtilityStreamRole : MewdekoSlashSubmodule<StreamRoleService>
    {
        /// <summary>
        ///     Sets a stream role for users in the server. When users with the from role start streaming they are assigned
        ///     the add role. Leaving both roles empty disables the feature.
        /// </summary>
        /// <param name="fromRole">The role to monitor for streaming activity.</param>
        /// <param name="addRole">The role to assign to users who start streaming.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [SlashCommand("set", "Sets the stream role. Leave both roles empty to disable it")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageRoles)]
        [RequireBotPermission(GuildPermission.ManageRoles)]
        public async Task StreamRole(
            [Summary("from-role", "The role whose members are monitored for streaming")]
            IRole? fromRole = null,
            [Summary("add-role", "The role given while streaming")]
            IRole? addRole = null)
        {
            if (fromRole is null && addRole is null)
            {
                await Service.StopStreamRole(ctx.Guild).ConfigureAwait(false);
                await ReplyConfirmAsync(Strings.StreamRoleDisabled(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (fromRole is null || addRole is null)
            {
                await ReplyErrorAsync(Strings.StreamRoleBothRequired(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await Service.SetStreamRole(fromRole, addRole).ConfigureAwait(false);

            await ReplyConfirmAsync(Strings.StreamRoleEnabled(ctx.Guild.Id, Format.Bold(fromRole.ToString()),
                Format.Bold(addRole.ToString()))).ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets a keyword for the stream role feature. Only users with streams containing this keyword will receive the
        ///     stream role. Leaving the keyword empty resets it.
        /// </summary>
        /// <param name="keyword">The keyword to set for the stream role feature.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [SlashCommand("keyword", "Sets or resets the keyword a stream title must contain")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageRoles)]
        [RequireBotPermission(GuildPermission.ManageRoles)]
        public async Task StreamRoleKeyword(
            [Summary("keyword", "The keyword. Leave empty to reset")]
            string? keyword = null)
        {
            var kw = await Service.SetKeyword(ctx.Guild, keyword).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(keyword))
                await ReplyConfirmAsync(Strings.StreamRoleKwReset(ctx.Guild.Id)).ConfigureAwait(false);
            else
                await ReplyConfirmAsync(Strings.StreamRoleKwSet(ctx.Guild.Id, Format.Bold(kw))).ConfigureAwait(false);
        }

        /// <summary>
        ///     Adds or removes a user to/from the blacklist for the stream role feature.
        /// </summary>
        /// <param name="action">The action to perform (add or remove).</param>
        /// <param name="user">The user to add or remove from the list.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [SlashCommand("blacklist", "Adds or removes a user from the stream role blacklist")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageRoles)]
        [RequireBotPermission(GuildPermission.ManageRoles)]
        public async Task StreamRoleBlacklist(AddRemove action, IGuildUser user)
        {
            var success = await Service
                .ApplyListAction(StreamRoleListType.Blacklist, ctx.Guild, action, user.Id, user.ToString())
                .ConfigureAwait(false);

            if (action == AddRemove.Add)
            {
                if (success)
                {
                    await ReplyConfirmAsync(Strings.StreamRoleBlAdd(ctx.Guild.Id, Format.Bold(user.ToString())))
                        .ConfigureAwait(false);
                }
                else
                {
                    await ReplyConfirmAsync(Strings.StreamRoleBlAddFail(ctx.Guild.Id, Format.Bold(user.ToString())))
                        .ConfigureAwait(false);
                }
            }
            else if (success)
            {
                await ReplyConfirmAsync(Strings.StreamRoleBlRem(ctx.Guild.Id, Format.Bold(user.ToString())))
                    .ConfigureAwait(false);
            }
            else
            {
                await ReplyErrorAsync(Strings.StreamRoleBlRemFail(ctx.Guild.Id, Format.Bold(user.ToString())))
                    .ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Adds or removes a user to/from the whitelist for the stream role feature.
        /// </summary>
        /// <param name="action">The action to perform (add or remove).</param>
        /// <param name="user">The user to add or remove from the list.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [SlashCommand("whitelist", "Adds or removes a user from the stream role whitelist")]
        [RequireContext(ContextType.Guild)]
        [SlashUserPerm(GuildPermission.ManageRoles)]
        [RequireBotPermission(GuildPermission.ManageRoles)]
        public async Task StreamRoleWhitelist(AddRemove action, IGuildUser user)
        {
            var success = await Service
                .ApplyListAction(StreamRoleListType.Whitelist, ctx.Guild, action, user.Id, user.ToString())
                .ConfigureAwait(false);

            if (action == AddRemove.Add)
            {
                if (success)
                {
                    await ReplyConfirmAsync(Strings.StreamRoleWlAdd(ctx.Guild.Id, Format.Bold(user.ToString())))
                        .ConfigureAwait(false);
                }
                else
                {
                    await ReplyConfirmAsync(Strings.StreamRoleWlAddFail(ctx.Guild.Id, Format.Bold(user.ToString())))
                        .ConfigureAwait(false);
                }
            }
            else if (success)
            {
                await ReplyConfirmAsync(Strings.StreamRoleWlRem(ctx.Guild.Id, Format.Bold(user.ToString())))
                    .ConfigureAwait(false);
            }
            else
            {
                await ReplyErrorAsync(Strings.StreamRoleWlRemFail(ctx.Guild.Id, Format.Bold(user.ToString())))
                    .ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    ///     Permission lookup commands.
    /// </summary>
    [Group("perms", "Permission lookups")]
    public class UtilityPerms(InteractiveService interactivity) : MewdekoSlashSubmodule
    {
        /// <summary>
        ///     Lists all roles that have the specified permissions.
        /// </summary>
        /// <param name="permissions">The permissions to search for, separated by spaces or commas.</param>
        /// <param name="searchType">The type of permission search (And or Or). Defaults to And.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("role-perm-list", "Lists roles that have the given permissions")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task RolePermList(
            [Summary("permissions", "Permission names separated by spaces, for example ManageGuild BanMembers")]
            string permissions,
            [Summary("search-type", "Whether roles must have all (And) or any (Or) of the permissions")]
            Utility.PermissionType searchType = Utility.PermissionType.And)
        {
            var perms = permissions.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries)
                .Select(x => Enum.TryParse<GuildPermission>(x, true, out var perm) ? perm : (GuildPermission?)null)
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .Distinct()
                .ToArray();

            if (perms.Length == 0)
            {
                await ErrorAsync(Strings.RolePermListInvalidPerms(ctx.Guild.Id));
                return;
            }

            List<IRole> rolesWithPerms;
            var rolesWithMatchedPerms = new Dictionary<IRole, List<GuildPermission>>();

            if (searchType == Utility.PermissionType.And)
            {
                rolesWithPerms = (from role in ctx.Guild.Roles
                    let hasAllPerms = perms.All(perm => role.Permissions.Has(perm))
                    where hasAllPerms
                    select role).ToList();
            }
            else
            {
                rolesWithPerms = (from role in ctx.Guild.Roles
                    let matchedPerms = perms.Where(perm => role.Permissions.Has(perm)).ToList()
                    where matchedPerms.Any()
                    select role).ToList();

                foreach (var role in rolesWithPerms)
                {
                    rolesWithMatchedPerms[role] = perms.Where(perm => role.Permissions.Has(perm)).ToList();
                }
            }

            if (!rolesWithPerms.Any() && !rolesWithMatchedPerms.Any())
            {
                await ErrorAsync(Strings.NoRolesWithPerms(ctx.Guild.Id));
                return;
            }

            var paginator = new LazyPaginatorBuilder()
                .WithUsers(ctx.User)
                .WithMaxPageIndex(searchType == Utility.PermissionType.Or
                    ? (rolesWithMatchedPerms.Count - 1) / 6
                    : (rolesWithPerms.Count - 1) / 6)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber)
                .WithDefaultEmotes()
                .Build();

            await interactivity.SendPaginatorAsync(paginator, (ctx.Interaction as SocketInteraction)!,
                TimeSpan.FromMinutes(5));

            async Task<PageBuilder> PageFactory(int pagenum)
            {
                var embed = new PageBuilder()
                    .WithOkColor()
                    .WithTitle(Strings.RolesWithPermissions(ctx.Guild.Id));

                if (searchType == Utility.PermissionType.And)
                {
                    foreach (var role in rolesWithPerms.Skip(pagenum * 6).Take(6))
                    {
                        embed.AddField(role.Name,
                            $"`Id`: {role.Id}\n`Mention`: {role.Mention}\n`Users`: {(await role.GetMembersAsync()).Count()}");
                    }
                }
                else
                {
                    foreach (var role in rolesWithMatchedPerms.Skip(pagenum * 6).Take(6))
                    {
                        embed.AddField(role.Key.Name,
                            $"`Id`: {role.Key.Id}\n`Mention`: {role.Key.Mention}\n`Users`: {(await role.Key.GetMembersAsync()).Count()}\n`Matched Permissions`: {string.Join(", ", role.Value)}");
                    }
                }

                return embed;
            }
        }
    }
}