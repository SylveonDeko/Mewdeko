using System.Net.Http;
using System.Text.Json;
using CodeHollow.FeedReader;
using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.Searches.Common;
using Mewdeko.Modules.Searches.Services;

namespace Mewdeko.Modules.Searches;

public partial class SlashSearch
{
    /// <summary>
    ///     Slash commands for interacting with osu! APIs and retrieving user data.
    /// </summary>
    /// <param name="creds">The bot credentials holding the osu! api key.</param>
    /// <param name="factory">The HTTP client factory.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    [Group("osu", "osu! profile and play lookups")]
    public class SlashSearchOsu(IBotCredentials creds, IHttpClientFactory factory, ILogger<SlashSearchOsu> logger)
        : MewdekoSlashSubmodule
    {
        /// <summary>
        ///     The osu! game modes.
        /// </summary>
        public enum OsuMode
        {
            /// <summary>
            ///     osu! standard.
            /// </summary>
            Standard = 0,

            /// <summary>
            ///     osu! taiko.
            /// </summary>
            Taiko = 1,

            /// <summary>
            ///     osu! catch the beat.
            /// </summary>
            Catch = 2,

            /// <summary>
            ///     osu! mania.
            /// </summary>
            Mania = 3
        }

        /// <summary>
        ///     Retrieves osu! user profile information.
        /// </summary>
        /// <param name="user">The osu! username to retrieve information for.</param>
        /// <param name="mode">The game mode to retrieve information for.</param>
        [SlashCommand("osu", "Show an osu! profile")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task Osu([Summary("user", "The osu! username")] string user,
            [Summary("mode", "The game mode")] OsuMode mode = OsuMode.Standard)
        {
            if (string.IsNullOrWhiteSpace(user))
                return;

            if (string.IsNullOrWhiteSpace(creds.OsuApiKey))
            {
                await ReplyErrorAsync(Strings.OsuApiKey(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await DeferAsync().ConfigureAwait(false);

            using var http = factory.CreateClient();
            var modeNumber = (int)mode;

            try
            {
                var smode = ResolveGameMode(modeNumber);
                var userReq = $"https://osu.ppy.sh/api/get_user?k={creds.OsuApiKey}&u={user}&m={modeNumber}";
                var userResString = await http.GetStringAsync(userReq)
                    .ConfigureAwait(false);
                var objs = JsonSerializer.Deserialize<List<OsuUserData>>(userResString);

                if (objs.Count == 0)
                {
                    await ReplyErrorAsync(Strings.OsuUserNotFound(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                }

                var obj = objs[0];
                var userId = obj.UserId;

                await ctx.Interaction.FollowupAsync(embed: new EmbedBuilder()
                    .WithOkColor()
                    .WithTitle(Strings.OsuProfileTitle(ctx.Guild.Id, smode, user))
                    .WithThumbnailUrl($"https://a.ppy.sh/{userId}")
                    .WithDescription($"https://osu.ppy.sh/u/{userId}")
                    .AddField(Strings.OsuOfficialRank(ctx.Guild.Id), $"#{obj.PpRank}", true)
                    .AddField(Strings.OsuCountryRank(ctx.Guild.Id),
                        $"#{obj.PpCountryRank} :flag_{obj.Country.ToLower()}:", true)
                    .AddField(Strings.OsuTotalPp(ctx.Guild.Id), Math.Round(obj.PpRaw, 2), true)
                    .AddField(Strings.OsuAccuracy(ctx.Guild.Id), $"{Math.Round(obj.Accuracy, 2)}%", true)
                    .AddField(Strings.OsuPlaycount(ctx.Guild.Id), obj.Playcount, true)
                    .AddField(Strings.OsuLevel(ctx.Guild.Id), Math.Round(obj.Level), true)
                    .Build()).ConfigureAwait(false);
            }
            catch (ArgumentOutOfRangeException)
            {
                await ReplyErrorAsync(Strings.OsuUserNotFound(ctx.Guild.Id)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await ReplyErrorAsync(Strings.OsuFailed(ctx.Guild.Id)).ConfigureAwait(false);
                logger.LogWarning(ex, "Osu command failed");
            }
        }

        /// <summary>
        ///     Retrieves osu!Gatari user profile information.
        /// </summary>
        /// <param name="user">The osu!Gatari username to retrieve information for.</param>
        /// <param name="mode">The game mode to retrieve information for.</param>
        [SlashCommand("gatari", "Show an osu!Gatari profile")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task Gatari([Summary("user", "The osu!Gatari username")] string user,
            [Summary("mode", "The game mode")] OsuMode mode = OsuMode.Standard)
        {
            await DeferAsync().ConfigureAwait(false);

            using var http = factory.CreateClient();
            var modeNumber = (int)mode;

            var modeStr = ResolveGameMode(modeNumber);
            var resString = await http
                .GetStringAsync($"https://api.gatari.pw/user/stats?u={user}&mode={modeNumber}")
                .ConfigureAwait(false);

            var statsResponse = JsonSerializer.Deserialize<GatariUserStatsResponse>(resString);
            if (statsResponse.Code != 200 || statsResponse.Stats.Id == 0)
            {
                await ReplyErrorAsync(Strings.OsuUserNotFound(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var usrResString = await http.GetStringAsync($"https://api.gatari.pw/users/get?u={user}")
                .ConfigureAwait(false);

            var userData = JsonSerializer.Deserialize<GatariUserResponse>(usrResString).Users[0];
            var userStats = statsResponse.Stats;

            var embed = new EmbedBuilder()
                .WithOkColor()
                .WithTitle(Strings.OsuProfileTitle(ctx.Guild.Id, $"Gatari {modeStr}", user))
                .WithThumbnailUrl($"https://a.gatari.pw/{userStats.Id}")
                .WithDescription($"https://osu.gatari.pw/u/{userStats.Id}")
                .AddField(Strings.OsuOfficialRank(ctx.Guild.Id), $"#{userStats.Rank}", true)
                .AddField(Strings.OsuCountryRank(ctx.Guild.Id),
                    $"#{userStats.CountryRank} :flag_{userData.Country.ToLower()}:", true)
                .AddField(Strings.OsuTotalPp(ctx.Guild.Id), userStats.Pp, true)
                .AddField(Strings.OsuAccuracy(ctx.Guild.Id), $"{Math.Round(userStats.AvgAccuracy, 2)}%", true)
                .AddField(Strings.OsuPlaycount(ctx.Guild.Id), userStats.Playcount, true)
                .AddField(Strings.OsuLevel(ctx.Guild.Id), userStats.Level, true);

            await ctx.Interaction.FollowupAsync(embed: embed.Build()).ConfigureAwait(false);
        }

        /// <summary>
        ///     Retrieves the top 5 osu! plays for a user.
        /// </summary>
        /// <param name="user">The osu! username to retrieve plays for.</param>
        /// <param name="mode">The game mode to retrieve plays for.</param>
        [SlashCommand("osu5", "Show the top 5 osu! plays of a user")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task Osu5([Summary("user", "The osu! username")] string user,
            [Summary("mode", "The game mode")] OsuMode mode = OsuMode.Standard)
        {
            if (string.IsNullOrWhiteSpace(creds.OsuApiKey))
            {
                await ErrorAsync(Strings.OsuApiKeyRequired(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (string.IsNullOrWhiteSpace(user))
            {
                await ErrorAsync(Strings.OsuUsernameRequired(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await DeferAsync().ConfigureAwait(false);

            using var http = factory.CreateClient();
            var m = (int)mode;

            var reqString =
                $"https://osu.ppy.sh/api/get_user_best?k={creds.OsuApiKey}&u={Uri.EscapeDataString(user)}&type=string&limit=5&m={m}";

            var resString = await http.GetStringAsync(reqString).ConfigureAwait(false);
            var obj = JsonSerializer.Deserialize<List<OsuUserBests>>(resString);

            var mapTasks = obj.Select(async item =>
            {
                var mapReqString = $"https://osu.ppy.sh/api/get_beatmaps?k={creds.OsuApiKey}&b={item.BeatmapId}";

                var mapResString = await http.GetStringAsync(mapReqString).ConfigureAwait(false);
                var map = JsonSerializer.Deserialize<List<OsuMapData>>(mapResString).FirstOrDefault();
                if (map is null)
                    return default;
                var pp = Math.Round(item.Pp, 2);
                var acc = CalculateAcc(item, m);
                var mods = ResolveMods(item.EnabledMods);

                var title = $"{map.Artist}-{map.Title} ({map.Version})";
                var desc = $"""
                            [/b/{item.BeatmapId}](https://osu.ppy.sh/b/{item.BeatmapId})
                            {$"{pp}pp",-7} | {$"{acc}%",-7}

                            """;
                if (mods != "+") desc += Format.Bold(mods);

                return (title, desc);
            });

            var eb = new EmbedBuilder()
                .WithOkColor()
                .WithTitle(Strings.OsuTopPlays(ctx.Guild.Id, user));

            var mapData = await Task.WhenAll(mapTasks).ConfigureAwait(false);
            foreach (var (title, desc) in mapData.Where(x => x != default)) eb.AddField(title, desc);

            await ctx.Interaction.FollowupAsync(embed: eb.Build()).ConfigureAwait(false);
        }

        private static double CalculateAcc(OsuUserBests play, int mode)
        {
            double hitPoints;
            double totalHits;
            switch (mode)
            {
                case 0:
                    hitPoints = play.Count50 * 50 +
                                play.Count100 * 100 +
                                play.Count300 * 300;
                    totalHits = play.Count50 + play.Count100 +
                                play.Count300 + play.Countmiss;
                    totalHits *= 300;
                    break;
                case 1:
                    hitPoints = play.Countmiss * 0 + play.Count100 * 0.5 + play.Count300;
                    totalHits = (play.Countmiss + play.Count100 + play.Count300) * 300;
                    hitPoints *= 300;
                    break;
                case 2:
                    hitPoints = play.Count50 + play.Count100 + play.Count300;
                    totalHits = play.Countmiss + play.Count50 + play.Count100 + play.Count300 +
                                play.Countkatu;
                    break;
                default:
                    hitPoints = play.Count50 * 50 +
                                play.Count100 * 100 +
                                play.Countkatu * 200 +
                                (play.Count300 + play.Countgeki) * 300;

                    totalHits = (play.Countmiss + play.Count50 + play.Count100 +
                                 play.Countkatu + play.Count300 + play.Countgeki) * 300;
                    break;
            }

            return Math.Round(hitPoints / totalHits * 100, 2);
        }

        private static string ResolveGameMode(int mode)
        {
            return mode switch
            {
                0 => "Standard",
                1 => "Taiko",
                2 => "Catch",
                3 => "Mania",
                _ => "Standard"
            };
        }

        private static string ResolveMods(int mods)
        {
            var modString = "+";

            if (IsBitSet(mods, 0))
                modString += "NF";
            if (IsBitSet(mods, 1))
                modString += "EZ";
            if (IsBitSet(mods, 8))
                modString += "HT";

            if (IsBitSet(mods, 3))
                modString += "HD";
            if (IsBitSet(mods, 4))
                modString += "HR";
            if (IsBitSet(mods, 6) && !IsBitSet(mods, 9))
                modString += "DT";
            if (IsBitSet(mods, 9))
                modString += "NC";
            if (IsBitSet(mods, 10))
                modString += "FL";

            if (IsBitSet(mods, 5))
                modString += "SD";
            if (IsBitSet(mods, 14))
                modString += "PF";

            if (IsBitSet(mods, 7))
                modString += "RX";
            if (IsBitSet(mods, 11))
                modString += "AT";
            if (IsBitSet(mods, 12))
                modString += "SO";
            return modString;
        }

        private static bool IsBitSet(int mods, int pos)
        {
            return (mods & (1 << pos)) != 0;
        }
    }

    /// <summary>
    ///     Slash commands for translating text and managing auto-translation settings within a guild.
    /// </summary>
    /// <param name="google">The Google API service providing the supported language list.</param>
    [Group("translate", "Translate text and manage auto translation")]
    public class SlashSearchTranslate(IGoogleApiService google) : MewdekoSlashSubmodule<SearchesService>
    {
        /// <summary>
        ///     Translates text from one language to another specified by the user.
        /// </summary>
        /// <param name="langs">The language pair in the format 'from>to'.</param>
        /// <param name="text">The text to be translated.</param>
        [SlashCommand("translate", "Translate text between two languages")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task Translate(
            [Summary("languages", "Language pair in the form from>to, e.g. en>fr")]
            string langs,
            [Summary("text", "The text to translate")]
            string text)
        {
            await DeferAsync().ConfigureAwait(false);
            try
            {
                var translation = await SearchesService.Translate(langs, text).ConfigureAwait(false);
                await ctx.Interaction.SendConfirmAsync($"{Strings.Translation(ctx.Guild.Id)} {langs}", translation)
                    .ConfigureAwait(false);
            }
            catch
            {
                await ReplyErrorAsync(Strings.BadInputFormat(ctx.Guild.Id)).ConfigureAwait(false);
            }
        }

        /// <summary>
        ///     Toggles auto-translation for the current channel, optionally enabling auto-deletion of the original message.
        /// </summary>
        /// <param name="autoDelete">Whether to delete the original message after translating it.</param>
        [SlashCommand("auto-translate", "Toggle automatic translation in this channel")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task AutoTranslate(
            [Summary("auto-delete", "Delete the original message after translating")]
            bool autoDelete = false)
        {
            var channel = (ITextChannel)ctx.Channel;

            if (autoDelete)
            {
                Service.TranslatedChannels.AddOrUpdate(channel.Id, true, (_, _) => true);
                await ReplyConfirmAsync(Strings.AtlAdStarted(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (Service.TranslatedChannels.TryRemove(channel.Id, out _))
            {
                await ReplyConfirmAsync(Strings.AtlStopped(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            if (Service.TranslatedChannels.TryAdd(channel.Id, autoDelete))
                await ReplyConfirmAsync(Strings.AtlStarted(ctx.Guild.Id)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets or removes the preferred language pair for auto-translation for the user in the current channel.
        /// </summary>
        /// <param name="langs">The language pair in the format 'from>to'. Leave empty to remove the setting.</param>
        [SlashCommand("auto-lang", "Set your auto translation languages in this channel")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task AutoTransLang(
            [Summary("languages", "Language pair in the form from>to, leave empty to remove")]
            string? langs = null)
        {
            var ucp = (ctx.User.Id, ctx.Channel.Id);

            if (string.IsNullOrWhiteSpace(langs))
            {
                if (Service.UserLanguages.TryRemove(ucp, out langs))
                    await ReplyConfirmAsync(Strings.AtlRemoved(ctx.Guild.Id)).ConfigureAwait(false);
                else
                    await ReplyErrorAsync(Strings.InvalidLang(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var langarr = langs.ToLowerInvariant().Split('>');
            if (langarr.Length != 2)
            {
                await ReplyErrorAsync(Strings.BadInputFormat(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            var from = langarr[0];
            var to = langarr[1];

            if (!google.Languages.Contains(from) || !google.Languages.Contains(to))
            {
                await ReplyErrorAsync(Strings.InvalidLang(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            Service.UserLanguages.AddOrUpdate(ucp, langs, (_, _) => langs);

            await ReplyConfirmAsync(Strings.AtlSet(ctx.Guild.Id, from, to)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Lists all available languages for translation.
        /// </summary>
        [SlashCommand("langs", "List the languages available for translation")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        public async Task Translangs()
        {
            var i = 0;
            var table = $"""
                         ```css
                         {string.Join("\n", google.Languages.GroupBy(_ => i++ / 3)
                             .Select(ig => string.Concat(ig.Select(str => $"{str,-15}"))))}
                         ```
                         """;
            await ctx.Interaction.RespondAsync(table).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Slash commands for managing RSS feeds in the guild.
    /// </summary>
    /// <param name="interactivity">The interactive service used for pagination.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    [Group("feed", "Manage RSS feed subscriptions")]
    public class SlashSearchFeed(InteractiveService interactivity, ILogger<SlashSearchFeed> logger)
        : MewdekoSlashSubmodule<FeedsService>
    {
        /// <summary>
        ///     Adds a new RSS feed to the guild's feed list.
        /// </summary>
        /// <param name="url">The URL of the RSS feed.</param>
        /// <param name="channel">The channel where feed updates will be sent. Defaults to the current channel.</param>
        [SlashCommand("add", "Subscribe to an RSS feed")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageMessages)]
        public async Task FeedAdd([Summary("url", "The RSS feed url")] string url,
            [Summary("channel", "Where updates are posted")]
            ITextChannel? channel = null)
        {
            await DeferAsync().ConfigureAwait(false);

            var success = Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                          (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
            if (success)
            {
                channel ??= (ITextChannel)ctx.Channel;
                try
                {
                    await FeedReader.ReadAsync(url).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    logger.LogInformation(ex, "Unable to get feeds from that url");
                    success = false;
                }
            }

            if (success)
            {
                success = await Service.AddFeed(ctx.Guild.Id, channel.Id, url);
                if (success)
                {
                    await ReplyConfirmAsync(Strings.FeedAdded(ctx.Guild.Id)).ConfigureAwait(false);
                    return;
                }
            }

            await ReplyErrorAsync(Strings.FeedNotValid(ctx.Guild.Id)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Removes an RSS feed from the guild's feed list.
        /// </summary>
        /// <param name="index">The 1-based index of the feed in the list.</param>
        [SlashCommand("remove", "Unsubscribe from an RSS feed")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageMessages)]
        public async Task FeedRemove([Summary("index", "The feed number from the list")] int index)
        {
            if (await Service.RemoveFeed(ctx.Guild.Id, --index))
                await ReplyConfirmAsync(Strings.FeedRemoved(ctx.Guild.Id)).ConfigureAwait(false);
            else
                await ReplyErrorAsync(Strings.FeedOutOfRange(ctx.Guild.Id)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Sets a custom message format for feed updates of a specific feed.
        /// </summary>
        /// <param name="index">The 1-based index of the feed in the list.</param>
        /// <param name="message">The custom message format.</param>
        [SlashCommand("message", "Set a custom message for a feed")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageMessages)]
        public async Task FeedMessage([Summary("index", "The feed number from the list")] int index,
            [Summary("message", "The message format")]
            string message)
        {
            if (await Service.AddFeedMessage(ctx.Guild.Id, --index, message).ConfigureAwait(false))
                await ReplyConfirmAsync(Strings.FeedMsgUpdated(ctx.Guild.Id)).ConfigureAwait(false);
            else
                await ReplyErrorAsync(Strings.FeedOutOfRange(ctx.Guild.Id)).ConfigureAwait(false);
        }

        /// <summary>
        ///     Tests the retrieval of an RSS feed by posting its latest item in the current channel.
        /// </summary>
        /// <param name="index">The 1-based index of the feed in the list.</param>
        [SlashCommand("test", "Post the latest item of a feed to test it")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageMessages)]
        public async Task RssTest([Summary("index", "The feed number from the list")] int index)
        {
            await DeferAsync().ConfigureAwait(false);

            var feeds = await Service.GetFeeds(ctx.Guild.Id);
            if (index < 1 || index > feeds.Count || feeds.ElementAt(index - 1) is null)
            {
                await ReplyErrorAsync(Strings.FeedOutOfRange(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            await Service.TestRss(feeds.ElementAt(index - 1), ctx.Channel as ITextChannel).ConfigureAwait(false);
            await ReplyConfirmAsync(Strings.FeedTestSent(ctx.Guild.Id, ((ITextChannel)ctx.Channel).Mention))
                .ConfigureAwait(false);
        }

        /// <summary>
        ///     Lists all RSS feeds added to the guild.
        /// </summary>
        [SlashCommand("list", "List the subscribed RSS feeds")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.ManageMessages)]
        public async Task FeedList()
        {
            await DeferAsync().ConfigureAwait(false);

            var feeds = await Service.GetFeeds(ctx.Guild.Id);

            if (feeds.Count == 0)
            {
                await ctx.Interaction.FollowupAsync(embed: new EmbedBuilder()
                        .WithOkColor()
                        .WithDescription(Strings.FeedNoFeed(ctx.Guild.Id)).Build())
                    .ConfigureAwait(false);
                return;
            }

            var paginator = new LazyPaginatorBuilder()
                .AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(feeds.Count / 10)
                .WithDefaultEmotes()
                .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                .Build();

            await interactivity.SendPaginatorAsync(paginator, (ctx.Interaction as SocketInteraction)!,
                TimeSpan.FromMinutes(60)).ConfigureAwait(false);

            async Task<PageBuilder> PageFactory(int page)
            {
                await Task.CompletedTask.ConfigureAwait(false);
                var embed = new PageBuilder()
                    .WithOkColor();
                var i = 0;
                var fs = string.Join("\n", feeds.Skip(page * 10)
                    .Take(10)
                    .Select(x => $"`{page * 10 + ++i}.` <#{x.ChannelId}> {x.Url}"));

                return embed.WithDescription(fs);
            }
        }
    }
}