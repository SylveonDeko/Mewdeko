using System.IO;
using System.Net.Http;
using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using IF.Lastfm.Core.Api.Enums;
using Mewdeko.Common.Attributes.TextCommands;
using SkiaSharp;

namespace Mewdeko.Modules.Music;

public partial class Music
{
    /// <summary>
    ///     Gets the linked Last.fm username for a user, sending an appropriate error if they
    ///     don't have one linked.
    /// </summary>
    private async Task<string?> GetLinkedUsernameOrErrorAsync(IUser target)
    {
        var lastFmUser = await lastFmStats.GetLinkedUserAsync(target.Id);
        if (lastFmUser is not null)
            return lastFmUser.Username;

        if (target.Id == ctx.User.Id)
        {
            var prefix = await guildSettingsService.GetPrefix(ctx.Guild);
            await ReplyErrorAsync(Strings.LastfmNotLinked(ctx.Guild.Id, prefix)).ConfigureAwait(false);
        }
        else
        {
            await ReplyErrorAsync(Strings.LastfmNotLinkedTarget(ctx.Guild.Id, target.Mention)).ConfigureAwait(false);
        }

        return null;
    }

    /// <summary>
    ///     Resolves an artist name, falling back to the given user's most recent scrobble if none
    ///     was supplied.
    /// </summary>
    private async Task<string?> ResolveArtistAsync(string? artist, string username)
    {
        if (!string.IsNullOrWhiteSpace(artist))
            return artist;

        var recent = await lastFmStats.GetRecentTracksAsync(username, 1);
        if (recent.Success && recent.Content.Count > 0)
            return recent.Content[0].ArtistName;

        await ReplyErrorAsync(Strings.LastfmNoArtistContext(ctx.Guild.Id)).ConfigureAwait(false);
        return null;
    }

    /// <summary>
    ///     Shows the current or most recently played track for the given user, from their linked
    ///     Last.fm account. Falls back to their Discord Spotify status if they don't have an
    ///     account linked or have no scrobble history.
    /// </summary>
    /// <param name="user">The user to check. Defaults to the command invoker.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task Fm(IGuildUser? user = null)
    {
        user ??= (IGuildUser)ctx.User;

        var lastFmUser = await lastFmStats.GetLinkedUserAsync(user.Id);
        if (lastFmUser is not null)
        {
            var recent = await lastFmStats.GetRecentTracksAsync(lastFmUser.Username, 1);
            if (recent.Success && recent.Content.Count > 0)
            {
                var track = recent.Content[0];
                var isNowPlaying = track.IsNowPlaying ?? false;

                var trackDisplay = new TextDisplayBuilder(Strings.LastfmNowPlayingTrack(ctx.Guild.Id, track.Name,
                    track.Url?.ToString() ?? "", track.ArtistName));

                var imageUrl = track.Images?.Large?.ToString();
                IMessageComponentBuilder trackComponent = trackDisplay;
                if (!string.IsNullOrEmpty(imageUrl))
                {
                    trackComponent = new SectionBuilder()
                        .WithComponents([trackDisplay])
                        .WithAccessory(new ThumbnailBuilder()
                            .WithMedia(new UnfurledMediaItemProperties
                            {
                                Url = imageUrl
                            }));
                }

                var userInfo = await lastFmStats.GetUserInfoAsync(lastFmUser.Username);
                var title = isNowPlaying
                    ? Strings.LastfmNowPlayingTitle(ctx.Guild.Id, user.DisplayName)
                    : Strings.LastfmLastPlayedTitle(ctx.Guild.Id, user.DisplayName);

                var components = new ComponentBuilderV2()
                    .WithContainer([
                        new TextDisplayBuilder($"# {title}")
                    ], Mewdeko.OkColor)
                    .WithSeparator()
                    .WithContainer(trackComponent);

                if (userInfo.Success)
                {
                    components.WithSeparator()
                        .WithContainer(new TextDisplayBuilder(
                            Strings.LastfmNowPlayingScrobbles(ctx.Guild.Id,
                                userInfo.Content.Playcount.ToString("N0"))));
                }

                await ctx.Channel.SendMessageAsync(components: components.Build(),
                    flags: MessageFlags.ComponentsV2, allowedMentions: AllowedMentions.None).ConfigureAwait(false);
                return;
            }
        }

        var spotify = user.Activities?.OfType<SpotifyGame>().FirstOrDefault();
        if (spotify is not null)
        {
            var trackDisplay = new TextDisplayBuilder(Strings.LastfmNowPlayingTrack(ctx.Guild.Id, spotify.TrackTitle,
                spotify.TrackUrl ?? "", string.Join(", ", spotify.Artists)));

            IMessageComponentBuilder trackComponent = trackDisplay;
            if (!string.IsNullOrEmpty(spotify.AlbumArtUrl))
            {
                trackComponent = new SectionBuilder()
                    .WithComponents([trackDisplay])
                    .WithAccessory(new ThumbnailBuilder()
                        .WithMedia(new UnfurledMediaItemProperties
                        {
                            Url = spotify.AlbumArtUrl
                        }));
            }

            var prefix = await guildSettingsService.GetPrefix(ctx.Guild);
            var components = new ComponentBuilderV2()
                .WithContainer([
                    new TextDisplayBuilder($"# {Strings.LastfmSpotifyStatusTitle(ctx.Guild.Id, user.DisplayName)}")
                ], Mewdeko.OkColor)
                .WithSeparator()
                .WithContainer(trackComponent)
                .WithSeparator()
                .WithContainer(new TextDisplayBuilder(Strings.LastfmSpotifyStatusNote(ctx.Guild.Id, prefix)));

            await ctx.Channel.SendMessageAsync(components: components.Build(),
                flags: MessageFlags.ComponentsV2, allowedMentions: AllowedMentions.None).ConfigureAwait(false);
            return;
        }

        await ReplyErrorAsync(Strings.LastfmNothingPlaying(ctx.Guild.Id, user.Mention)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Shows a user's recently played tracks from Last.fm.
    /// </summary>
    /// <param name="user">The user to check. Defaults to the command invoker.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task FmRecent(IGuildUser? user = null)
    {
        user ??= (IGuildUser)ctx.User;
        var username = await GetLinkedUsernameOrErrorAsync(user);
        if (username is null)
            return;

        var recent = await lastFmStats.GetRecentTracksAsync(username, 25);
        if (!recent.Success || recent.Content.Count == 0)
        {
            await ReplyErrorAsync(Strings.LastfmRecentEmpty(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var tracks = recent.Content.ToList();
        const int perPage = 10;
        var totalPages = Math.Max(1, (int)Math.Ceiling(tracks.Count / (double)perPage));

        var paginator = new ComponentPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(GeneratePage)
            .WithPageCount(totalPages)
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .WithActionOnTimeout(ActionOnStop.DisableInput)
            .Build();

        await interactiveService.SendPaginatorAsync(paginator, ctx.Channel, TimeSpan.FromMinutes(5));
        return;

        IPage GeneratePage(IComponentPaginator p)
        {
            var pageTracks = tracks.Skip(p.CurrentPageIndex * perPage).Take(perPage).ToList();
            var lines = pageTracks.Select((t, i) => Strings.LastfmRecentEntry(ctx.Guild.Id,
                p.CurrentPageIndex * perPage + i + 1, t.Name, t.Url?.ToString() ?? "", t.ArtistName,
                t.IsNowPlaying ?? false ? Strings.LastfmNowPlayingTag(ctx.Guild.Id) : ""));

            var container = new ContainerBuilder()
                .WithComponents([
                    new TextDisplayBuilder($"# {Strings.LastfmRecentTitle(ctx.Guild.Id, username)}"),
                    new SeparatorBuilder(),
                    new TextDisplayBuilder(string.Join('\n', lines)),
                    new SeparatorBuilder(),
                    new TextDisplayBuilder($"Page {p.CurrentPageIndex + 1}/{p.PageCount}"),
                    new ActionRowBuilder()
                        .AddPreviousButton(p, style: ButtonStyle.Secondary)
                        .AddNextButton(p, style: ButtonStyle.Secondary)
                        .AddStopButton(p)
                ])
                .WithAccentColor(Mewdeko.OkColor);

            return new PageBuilder()
                .WithComponents(new ComponentBuilderV2().AddComponent(container).Build())
                .Build();
        }
    }

    /// <summary>
    ///     Shows a user's top artists on Last.fm for a given time period.
    /// </summary>
    /// <param name="period">
    ///     The time period: week, month, quarter, half, year, or overall (default).
    /// </param>
    /// <param name="user">The user to check. Defaults to the command invoker.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task FmTopArtists(string? period = null, IGuildUser? user = null)
    {
        user ??= (IGuildUser)ctx.User;
        var username = await GetLinkedUsernameOrErrorAsync(user);
        if (username is null)
            return;

        var (span, _, displayName) = lastFmStats.ParsePeriod(period);
        var topArtists = await lastFmStats.GetTopArtistsAsync(username, span, 25);
        if (!topArtists.Success || topArtists.Content.Count == 0)
        {
            await ReplyErrorAsync(Strings.LastfmTopEmpty(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var artists = topArtists.Content.ToList();
        await SendTopListAsync(
            Strings.LastfmTopArtistsTitle(ctx.Guild.Id, username, displayName),
            artists.Select((a, i) => Strings.LastfmTopArtistEntry(ctx.Guild.Id, i + 1, a.Name,
                a.Url?.ToString() ?? "", (a.PlayCount ?? 0).ToString("N0"))).ToList());
    }

    /// <summary>
    ///     Shows a user's top albums on Last.fm for a given time period.
    /// </summary>
    /// <param name="period">
    ///     The time period: week, month, quarter, half, year, or overall (default).
    /// </param>
    /// <param name="user">The user to check. Defaults to the command invoker.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task FmTopAlbums(string? period = null, IGuildUser? user = null)
    {
        user ??= (IGuildUser)ctx.User;
        var username = await GetLinkedUsernameOrErrorAsync(user);
        if (username is null)
            return;

        var (span, _, displayName) = lastFmStats.ParsePeriod(period);
        var topAlbums = await lastFmStats.GetTopAlbumsAsync(username, span, 25);
        if (!topAlbums.Success || topAlbums.Content.Count == 0)
        {
            await ReplyErrorAsync(Strings.LastfmTopEmpty(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var albums = topAlbums.Content.ToList();
        await SendTopListAsync(
            Strings.LastfmTopAlbumsTitle(ctx.Guild.Id, username, displayName),
            albums.Select((a, i) => Strings.LastfmTopAlbumEntry(ctx.Guild.Id, i + 1, a.Name,
                a.Url?.ToString() ?? "", a.ArtistName, (a.PlayCount ?? 0).ToString("N0"))).ToList());
    }

    /// <summary>
    ///     Shows a user's top tracks on Last.fm for a given time period.
    /// </summary>
    /// <param name="period">
    ///     The time period: week, month, quarter, half, year, or overall (default).
    /// </param>
    /// <param name="user">The user to check. Defaults to the command invoker.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task FmTopTracks(string? period = null, IGuildUser? user = null)
    {
        user ??= (IGuildUser)ctx.User;
        var username = await GetLinkedUsernameOrErrorAsync(user);
        if (username is null)
            return;

        var (_, rawPeriod, displayName) = lastFmStats.ParsePeriod(period);
        var topTracks = await lastFmStats.GetTopTracksAsync(username, rawPeriod, 25);
        if (topTracks.Count == 0)
        {
            await ReplyErrorAsync(Strings.LastfmTopEmpty(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await SendTopListAsync(
            Strings.LastfmTopTracksTitle(ctx.Guild.Id, username, displayName),
            topTracks.Select((t, i) => Strings.LastfmTopTrackEntry(ctx.Guild.Id, i + 1, t.Name, t.Url, t.Artist,
                t.Playcount.ToString("N0"))).ToList());
    }

    /// <summary>
    ///     Sends a paginated list of top-item lines built by one of the FmTop* commands.
    /// </summary>
    private async Task SendTopListAsync(string title, List<string> lines)
    {
        const int perPage = 10;
        var totalPages = Math.Max(1, (int)Math.Ceiling(lines.Count / (double)perPage));

        var paginator = new ComponentPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(GeneratePage)
            .WithPageCount(totalPages)
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .WithActionOnTimeout(ActionOnStop.DisableInput)
            .Build();

        await interactiveService.SendPaginatorAsync(paginator, ctx.Channel, TimeSpan.FromMinutes(5));
        return;

        IPage GeneratePage(IComponentPaginator p)
        {
            var pageLines = lines.Skip(p.CurrentPageIndex * perPage).Take(perPage);

            var container = new ContainerBuilder()
                .WithComponents([
                    new TextDisplayBuilder($"# {title}"),
                    new SeparatorBuilder(),
                    new TextDisplayBuilder(string.Join('\n', pageLines)),
                    new SeparatorBuilder(),
                    new TextDisplayBuilder($"Page {p.CurrentPageIndex + 1}/{p.PageCount}"),
                    new ActionRowBuilder()
                        .AddPreviousButton(p, style: ButtonStyle.Secondary)
                        .AddNextButton(p, style: ButtonStyle.Secondary)
                        .AddStopButton(p)
                ])
                .WithAccentColor(Mewdeko.OkColor);

            return new PageBuilder()
                .WithComponents(new ComponentBuilderV2().AddComponent(container).Build())
                .Build();
        }
    }

    /// <summary>
    ///     Shows Last.fm info for an artist, along with your play count for them.
    /// </summary>
    /// <param name="artist">The artist name. Defaults to your currently/last playing artist.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task FmArtist([Remainder] string? artist = null)
    {
        var username = await GetLinkedUsernameOrErrorAsync(ctx.User);
        if (username is null)
            return;

        artist = await ResolveArtistAsync(artist, username);
        if (artist is null)
            return;

        var info = await lastFmStats.GetArtistInfoAsync(artist);
        if (!info.Success)
        {
            await ReplyErrorAsync(Strings.LastfmArtistNotFound(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var userPlaycount = await lastFmStats.GetArtistUserPlaycountAsync(info.Content.Name, username);

        var components = new ComponentBuilderV2()
            .WithContainer([
                new TextDisplayBuilder($"# {Strings.LastfmArtistTitle(ctx.Guild.Id, info.Content.Name)}")
            ], Mewdeko.OkColor)
            .WithSeparator()
            .WithContainer(new TextDisplayBuilder(
                Strings.LastfmArtistInfo(ctx.Guild.Id, (info.Content.Stats?.Plays ?? 0).ToString("N0"),
                    (info.Content.Stats?.Listeners ?? 0).ToString("N0"), username, userPlaycount.ToString("N0"))));

        await ctx.Channel.SendMessageAsync(components: components.Build(),
            flags: MessageFlags.ComponentsV2, allowedMentions: AllowedMentions.None).ConfigureAwait(false);
    }

    /// <summary>
    ///     Shows Last.fm info for an album, along with your play count for it.
    /// </summary>
    /// <param name="query">
    ///     The album to look up, formatted as `artist | album`. Defaults to your currently/last
    ///     playing album.
    /// </param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task FmAlbum([Remainder] string? query = null)
    {
        var username = await GetLinkedUsernameOrErrorAsync(ctx.User);
        if (username is null)
            return;

        string artist, album;
        if (string.IsNullOrWhiteSpace(query))
        {
            var recent = await lastFmStats.GetRecentTracksAsync(username, 1);
            if (!recent.Success || recent.Content.Count == 0 || string.IsNullOrEmpty(recent.Content[0].AlbumName))
            {
                await ReplyErrorAsync(Strings.LastfmQueryMissing(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            artist = recent.Content[0].ArtistName;
            album = recent.Content[0].AlbumName;
        }
        else
        {
            var parts = query.Split('|', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2)
            {
                await ReplyErrorAsync(Strings.LastfmQueryMissing(ctx.Guild.Id)).ConfigureAwait(false);
                return;
            }

            artist = parts[0];
            album = parts[1];
        }

        var info = await lastFmStats.GetAlbumInfoAsync(artist, album, username);
        if (!info.Success)
        {
            await ReplyErrorAsync(Strings.LastfmAlbumNotFound(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var components = new ComponentBuilderV2()
            .WithContainer([
                new TextDisplayBuilder($"# {Strings.LastfmAlbumTitle(ctx.Guild.Id, info.Content.Name)}")
            ], Mewdeko.OkColor)
            .WithSeparator()
            .WithContainer(new TextDisplayBuilder(
                Strings.LastfmAlbumInfo(ctx.Guild.Id, info.Content.ArtistName,
                    (info.Content.PlayCount ?? 0).ToString("N0"), username,
                    (info.Content.UserPlayCount ?? 0).ToString("N0"))));

        await ctx.Channel.SendMessageAsync(components: components.Build(),
            flags: MessageFlags.ComponentsV2, allowedMentions: AllowedMentions.None).ConfigureAwait(false);
    }

    /// <summary>
    ///     Ranks every linked server member by their play count for an artist.
    /// </summary>
    /// <param name="artist">The artist to check. Defaults to your currently/last playing artist.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task FmWhoKnows([Remainder] string? artist = null)
    {
        var username = await GetLinkedUsernameOrErrorAsync(ctx.User);
        if (username is null)
            return;

        artist = await ResolveArtistAsync(artist, username);
        if (artist is null)
            return;

        var members = await lastFmStats.GetLinkedGuildMembersAsync(ctx.Guild);
        if (members.Count == 0)
        {
            await ReplyErrorAsync(Strings.LastfmWhoknowsNoUsers(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await ctx.Channel.TriggerTypingAsync().ConfigureAwait(false);
        var ranked = await lastFmStats.GetArtistPlaycountsAsync(artist, members);
        if (ranked.Count == 0)
        {
            await ReplyErrorAsync(Strings.LastfmWhoknowsNoPlays(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var lines = ranked.Take(15).Select((r, i) =>
            Strings.LastfmWhoknowsEntry(ctx.Guild.Id, i + 1, r.Member.Member.Mention, r.Playcount.ToString("N0")));

        var components = new ComponentBuilderV2()
            .WithContainer([
                new TextDisplayBuilder($"# {Strings.LastfmWhoknowsArtistTitle(ctx.Guild.Id, artist)}")
            ], Mewdeko.OkColor)
            .WithSeparator()
            .WithContainer(new TextDisplayBuilder(string.Join('\n', lines)));

        await ctx.Channel.SendMessageAsync(components: components.Build(),
            flags: MessageFlags.ComponentsV2, allowedMentions: AllowedMentions.None).ConfigureAwait(false);
    }

    /// <summary>
    ///     Ranks every linked server member by their play count for an album.
    /// </summary>
    /// <param name="query">The album to check, formatted as `artist | album`.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task FmWhoKnowsAlbum([Remainder] string query)
    {
        var parts = query.Split('|', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            await ReplyErrorAsync(Strings.LastfmQueryMissing(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var artist = parts[0];
        var album = parts[1];

        var members = await lastFmStats.GetLinkedGuildMembersAsync(ctx.Guild);
        if (members.Count == 0)
        {
            await ReplyErrorAsync(Strings.LastfmWhoknowsNoUsers(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await ctx.Channel.TriggerTypingAsync().ConfigureAwait(false);
        var ranked = await lastFmStats.GetAlbumPlaycountsAsync(artist, album, members);
        if (ranked.Count == 0)
        {
            await ReplyErrorAsync(Strings.LastfmWhoknowsNoPlays(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var lines = ranked.Take(15).Select((r, i) =>
            Strings.LastfmWhoknowsEntry(ctx.Guild.Id, i + 1, r.Member.Member.Mention, r.Playcount.ToString("N0")));

        var components = new ComponentBuilderV2()
            .WithContainer([
                new TextDisplayBuilder($"# {Strings.LastfmWhoknowsAlbumTitle(ctx.Guild.Id, album, artist)}")
            ], Mewdeko.OkColor)
            .WithSeparator()
            .WithContainer(new TextDisplayBuilder(string.Join('\n', lines)));

        await ctx.Channel.SendMessageAsync(components: components.Build(),
            flags: MessageFlags.ComponentsV2, allowedMentions: AllowedMentions.None).ConfigureAwait(false);
    }

    /// <summary>
    ///     Ranks every linked server member by their play count for a track.
    /// </summary>
    /// <param name="query">The track to check, formatted as `artist | track`.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task FmWhoKnowsTrack([Remainder] string query)
    {
        var parts = query.Split('|', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            await ReplyErrorAsync(Strings.LastfmQueryMissing(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var artist = parts[0];
        var track = parts[1];

        var members = await lastFmStats.GetLinkedGuildMembersAsync(ctx.Guild);
        if (members.Count == 0)
        {
            await ReplyErrorAsync(Strings.LastfmWhoknowsNoUsers(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await ctx.Channel.TriggerTypingAsync().ConfigureAwait(false);
        var ranked = await lastFmStats.GetTrackPlaycountsAsync(artist, track, members);
        if (ranked.Count == 0)
        {
            await ReplyErrorAsync(Strings.LastfmWhoknowsNoPlays(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var lines = ranked.Take(15).Select((r, i) =>
            Strings.LastfmWhoknowsEntry(ctx.Guild.Id, i + 1, r.Member.Member.Mention, r.Playcount.ToString("N0")));

        var components = new ComponentBuilderV2()
            .WithContainer([
                new TextDisplayBuilder($"# {Strings.LastfmWhoknowsTrackTitle(ctx.Guild.Id, track, artist)}")
            ], Mewdeko.OkColor)
            .WithSeparator()
            .WithContainer(new TextDisplayBuilder(string.Join('\n', lines)));

        await ctx.Channel.SendMessageAsync(components: components.Build(),
            flags: MessageFlags.ComponentsV2, allowedMentions: AllowedMentions.None).ConfigureAwait(false);
    }

    /// <summary>
    ///     Shows who "owns" (has the most plays for) an artist in this server.
    /// </summary>
    /// <param name="artist">The artist to check. Defaults to your currently/last playing artist.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task FmCrowns([Remainder] string? artist = null)
    {
        var username = await GetLinkedUsernameOrErrorAsync(ctx.User);
        if (username is null)
            return;

        artist = await ResolveArtistAsync(artist, username);
        if (artist is null)
            return;

        var members = await lastFmStats.GetLinkedGuildMembersAsync(ctx.Guild);
        if (members.Count == 0)
        {
            await ReplyErrorAsync(Strings.LastfmWhoknowsNoUsers(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        await ctx.Channel.TriggerTypingAsync().ConfigureAwait(false);
        var ranked = await lastFmStats.GetArtistPlaycountsAsync(artist, members);
        if (ranked.Count == 0)
        {
            await ReplyErrorAsync(Strings.LastfmCrownNone(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var winner = ranked[0];
        var body = ranked.Count > 1
            ? Strings.LastfmCrownOwnerMargin(ctx.Guild.Id, winner.Member.Member.Mention,
                winner.Playcount.ToString("N0"), winner.Playcount - ranked[1].Playcount,
                ranked[1].Member.Member.Mention)
            : Strings.LastfmCrownOwnerSole(ctx.Guild.Id, winner.Member.Member.Mention,
                winner.Playcount.ToString("N0"));

        var components = new ComponentBuilderV2()
            .WithContainer([
                new TextDisplayBuilder($"# {Strings.LastfmCrownTitle(ctx.Guild.Id, artist)}")
            ], Mewdeko.OkColor)
            .WithSeparator()
            .WithContainer(new TextDisplayBuilder(body));

        await ctx.Channel.SendMessageAsync(components: components.Build(),
            flags: MessageFlags.ComponentsV2, allowedMentions: AllowedMentions.None).ConfigureAwait(false);
    }

    /// <summary>
    ///     Compares your top artists against another user's to see how much your taste overlaps.
    /// </summary>
    /// <param name="user">The user to compare against.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task FmTaste(IGuildUser user)
    {
        var usernameA = await GetLinkedUsernameOrErrorAsync(ctx.User);
        if (usernameA is null)
            return;

        var usernameB = await GetLinkedUsernameOrErrorAsync(user);
        if (usernameB is null)
            return;

        const int sampleSize = 50;
        var topA = await lastFmStats.GetTopArtistsAsync(usernameA, LastStatsTimeSpan.Overall, sampleSize);
        var topB = await lastFmStats.GetTopArtistsAsync(usernameB, LastStatsTimeSpan.Overall, sampleSize);

        var namesA = (topA.Success ? topA.Content : []).Select(a => a.Name).ToList();
        var namesB = (topB.Success ? topB.Content : [])
            .Select(a => a.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var shared = namesA.Where(a => namesB.Contains(a)).ToList();

        if (shared.Count == 0)
        {
            await ReplyErrorAsync(Strings.LastfmTasteNone(ctx.Guild.Id, sampleSize)).ConfigureAwait(false);
            return;
        }

        var smallerSampleSize = Math.Min(namesA.Count, namesB.Count);
        var overlap = smallerSampleSize == 0 ? 0 : shared.Count / (double)smallerSampleSize;

        var components = new ComponentBuilderV2()
            .WithContainer([
                new TextDisplayBuilder(
                    $"# {Strings.LastfmTasteTitle(ctx.Guild.Id, ((IGuildUser)ctx.User).DisplayName, user.DisplayName)}")
            ], Mewdeko.OkColor)
            .WithSeparator()
            .WithContainer(new TextDisplayBuilder(
                Strings.LastfmTasteResult(ctx.Guild.Id, shared.Count, shared.Count == 1 ? "" : "s", user.DisplayName,
                    sampleSize, overlap.ToString("P0"))))
            .WithSeparator()
            .WithContainer(new TextDisplayBuilder(
                Strings.LastfmTasteArtists(ctx.Guild.Id, string.Join(", ", shared.Take(15)))));

        await ctx.Channel.SendMessageAsync(components: components.Build(),
            flags: MessageFlags.ComponentsV2, allowedMentions: AllowedMentions.None).ConfigureAwait(false);
    }

    /// <summary>
    ///     Generates an image grid of a user's top albums on Last.fm.
    /// </summary>
    /// <param name="size">The grid dimensions (size x size). Must be between 2 and 5. Defaults to 3.</param>
    /// <param name="period">
    ///     The time period: week, month, quarter, half, year, or overall (default).
    /// </param>
    /// <param name="user">The user to check. Defaults to the command invoker.</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task FmChart(int size = 3, string? period = null, IGuildUser? user = null)
    {
        if (size is < 2 or > 5)
        {
            await ReplyErrorAsync(Strings.LastfmChartInvalidSize(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        user ??= (IGuildUser)ctx.User;
        var username = await GetLinkedUsernameOrErrorAsync(user);
        if (username is null)
            return;

        var (span, _, displayName) = lastFmStats.ParsePeriod(period);
        var itemCount = size * size;
        var topAlbums = await lastFmStats.GetTopAlbumsAsync(username, span, itemCount);

        var albumsWithArt = topAlbums.Success
            ? topAlbums.Content.Where(a => a.Images?.Large is not null).Take(itemCount).ToList()
            : [];

        if (albumsWithArt.Count < 2)
        {
            await ReplyErrorAsync(Strings.LastfmChartNoImages(ctx.Guild.Id, "albums")).ConfigureAwait(false);
            return;
        }

        await ctx.Channel.TriggerTypingAsync().ConfigureAwait(false);

        var imageBytes = await BuildChartImageAsync(
            albumsWithArt.Select(a => a.Images!.Large!.ToString()).ToList(), size);

        if (imageBytes is null)
        {
            await ReplyErrorAsync(Strings.LastfmChartNoImages(ctx.Guild.Id, "albums")).ConfigureAwait(false);
            return;
        }

        using var stream = new MemoryStream(imageBytes);
        var title = Strings.LastfmChartTitle(ctx.Guild.Id, username, "Albums", displayName);
        await ctx.Channel.SendFileAsync(stream, "chart.png", title).ConfigureAwait(false);
    }

    /// <summary>
    ///     Downloads a set of images and composites them into a square grid PNG.
    /// </summary>
    private async Task<byte[]?> BuildChartImageAsync(List<string> imageUrls, int gridSize)
    {
        const int cellSize = 300;
        var httpClient = httpClientFactory.CreateClient();

        var downloadTasks = imageUrls.Select(async url =>
        {
            try
            {
                var bytes = await httpClient.GetByteArrayAsync(url);
                return SKBitmap.Decode(bytes);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to download chart image from {Url}", url);
                return null;
            }
        });

        var bitmaps = (await Task.WhenAll(downloadTasks)).Where(b => b is not null).ToList();
        if (bitmaps.Count == 0)
            return null;

        var canvasSize = cellSize * gridSize;
        using var surface = SKSurface.Create(new SKImageInfo(canvasSize, canvasSize));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Black);

        for (var i = 0; i < bitmaps.Count && i < gridSize * gridSize; i++)
        {
            using var bitmap = bitmaps[i];
            var x = i % gridSize * cellSize;
            var y = i / gridSize * cellSize;
            var destRect = new SKRect(x, y, x + cellSize, y + cellSize);
            canvas.DrawBitmap(bitmap, destRect, new SKSamplingOptions(SKFilterMode.Linear));
        }

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 90);
        return data.ToArray();
    }
}