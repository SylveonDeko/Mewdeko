﻿#nullable enable
using System.Net.Http;
using Discord.Commands;
using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Genius;
using Genius.Models.Song;
using HtmlAgilityPack;
using Lavalink4NET;
using Lavalink4NET.Artwork;
using Lavalink4NET.DiscordNet;
using Lavalink4NET.Player;
using Lavalink4NET.Rest;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.Music.Common;
using Mewdeko.Modules.Music.Services;
using Mewdeko.Services.Settings;
using ContextType = Discord.Interactions.ContextType;

namespace Mewdeko.Modules.Music;

/// <summary>
/// Slash commands for music.
/// </summary>
/// <param name="lava">The Lavalink service</param>
/// <param name="interactive">The service used for embed pagination</param>
/// <param name="dbService">The database service</param>
/// <param name="client">The Discord client</param>
/// <param name="guildSettings">The guild settings service</param>
/// <param name="config">The bot configuration service</param>
/// <param name="creds">The bot credentials</param>
[Discord.Interactions.Group("music", "Play Music!")]
public class SlashMusic(
    LavalinkNode lava,
    InteractiveService interactive,
    DbService dbService,
    DiscordSocketClient client,
    GuildSettingsService guildSettings,
    BotConfigService config,
    IBotCredentials creds)
    : MewdekoSlashModuleBase<MusicService>
{
    /// <summary>
    /// Represents actions that can be performed on a playlist.
    /// </summary>
    public enum PlaylistAction
    {
        /// <summary>
        /// Show the playlist.
        /// </summary>
        Show,

        /// <summary>
        /// Delete the playlist.
        /// </summary>
        Delete,

        /// <summary>
        /// Create a new playlist.
        /// </summary>
        Create,

        /// <summary>
        /// Remove an item from the playlist.
        /// </summary>
        Remove,

        /// <summary>
        /// Add an item to the playlist.
        /// </summary>
        Add,

        /// <summary>
        /// Load a playlist.
        /// </summary>
        Load,

        /// <summary>
        /// Save the playlist.
        /// </summary>
        Save,

        /// <summary>
        /// Default action for the playlist.
        /// </summary>
        Default
    }

    /// <summary>
    /// Command to remove a song from the playlist.
    /// </summary>
    /// <param name="songNum">The number of the song to remove from the playlist.</param>
    [SlashCommand("remove", "Removes a song from the queue using its number"),
     Discord.Interactions.RequireContext(ContextType.Guild),
     CheckPermissions]
    public async Task SongRemove(int songNum)
    {
        var player = lava.GetPlayer<MusicPlayer>(ctx.Guild.Id);
        if (player is not null)
        {
            var voiceChannel = await ctx.Guild.GetVoiceChannelAsync(player.VoiceChannelId.Value).ConfigureAwait(false);
            var chanUsers = await voiceChannel.GetUsersAsync().FlattenAsync().ConfigureAwait(false);
            if (!chanUsers.Contains(ctx.User as IGuildUser))
            {
                await ctx.Interaction.SendErrorAsync("You are not in the bots music channel!", Config)
                    .ConfigureAwait(false);
                return;
            }

            if (await Service.RemoveSong(ctx.Guild, songNum).ConfigureAwait(false))
            {
                await ctx.Interaction.SendConfirmAsync($"Track {songNum} removed.").ConfigureAwait(false);
            }
            else
            {
                await ctx.Interaction
                    .SendErrorAsync("Seems like that track doesn't exist or you have nothing in queue.", Config)
                    .ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Command to set auto-disconnect behavior for the bot.
    /// </summary>
    /// <param name="disconnect">The auto-disconnect behavior to set.</param>
    [SlashCommand("autodisconnect", "Set the autodisconnect type"),
     Discord.Interactions.RequireContext(ContextType.Guild), CheckPermissions]
    public async Task AutoDisconnect(AutoDisconnect disconnect)
    {
        await Service.ModifySettingsInternalAsync(ctx.Guild.Id,
            (settings, _) => settings.AutoDisconnect = disconnect, disconnect).ConfigureAwait(false);
        await ctx.Interaction.SendConfirmAsync(
            $"Successfully set AutoDisconnect to {Format.Code(disconnect.ToString())}").ConfigureAwait(false);
    }

    /// <summary>
    /// Command to display the user's playlists.
    /// </summary>
    [SlashCommand("playlists", "Lists your playlists"), Discord.Interactions.RequireContext(ContextType.Guild)]
    public async Task Playlists()
    {
        var plists = Service.GetPlaylists(ctx.User);
        if (!plists.Any())
        {
            await ctx.Interaction.SendErrorAsync("You dont have any saved playlists!", Config).ConfigureAwait(false);
            return;
        }

        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(plists.Count() / 15)
            .WithDefaultCanceledPage()
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();
        await interactive
            .SendPaginatorAsync(paginator, (ctx.Interaction as SocketInteraction)!, TimeSpan.FromMinutes(60))
            .ConfigureAwait(false);

        async Task<PageBuilder> PageFactory(int page)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            var e = 1;
            return new PageBuilder().WithOkColor()
                .WithDescription(string.Join("\n",
                    plists.Skip(page).Take(15).Select(x =>
                        $"{e++}. {x.Name} - {x.Songs.Count()} songs")));
        }
    }

    /// <summary>
    /// Command to manage playlists.
    /// </summary>
    /// <param name="action">The action to perform on the playlist.</param>
    /// <param name="playlistOrSongName">The name of the playlist or song to use.</param>
    [SlashCommand("playlist", "Create or manage your playlists"),
     Discord.Interactions.RequireContext(ContextType.Guild), CheckPermissions]
    public async Task Playlist(PlaylistAction action, string? playlistOrSongName = null)
    {
        await ctx.Interaction.DeferAsync().ConfigureAwait(false);
        var plists = Service.GetPlaylists(ctx.User);
        switch (action)
        {
            case PlaylistAction.Show:
                MusicPlaylist? plist;
                if (playlistOrSongName is null)
                {
                    if (await Service.GetDefaultPlaylist(ctx.User) is not null)
                    {
                        plist = await Service.GetDefaultPlaylist(ctx.User);
                    }
                    else
                    {
                        await ctx.Interaction.SendErrorFollowupAsync(
                                "You have not specified a playlist name and do not have a default playlist set, there's nothing to show!",
                                Config)
                            .ConfigureAwait(false);
                        return;
                    }
                }
                else
                {
                    plist = Service.GetPlaylists(ctx.User)
                        .FirstOrDefault(x =>
                            string.Equals(x.Name, playlistOrSongName, StringComparison.CurrentCultureIgnoreCase))!;
                }

                var songcount = 1;
                if (plist is null)
                {
                    await ctx.Interaction.SendErrorFollowupAsync("This is not a valid playlist!", Config)
                        .ConfigureAwait(false);
                    return;
                }

                if (!plist.Songs.Any())
                {
                    await ctx.Interaction.SendErrorFollowupAsync("This playlist has no songs!", Config)
                        .ConfigureAwait(false);
                    return;
                }

                var paginator = new LazyPaginatorBuilder().AddUser(ctx.User).WithPageFactory(PageFactory)
                    .WithFooter(
                        PaginatorFooter.PageNumber | PaginatorFooter.Users)
                    .WithMaxPageIndex(plist.Songs.Count() / 15)
                    .WithDefaultCanceledPage().WithDefaultEmotes()
                    .WithActionOnCancellation(ActionOnStop.DeleteMessage).Build();
                await interactive
                    .SendPaginatorAsync(paginator, (ctx.Interaction as SocketInteraction)!, TimeSpan.FromMinutes(60))
                    .ConfigureAwait(false);

                async Task<PageBuilder> PageFactory(int page)
                {
                    await Task.CompletedTask.ConfigureAwait(false);
                    return new PageBuilder().WithOkColor().WithDescription(string.Join("\n",
                        plist.Songs.Select(x =>
                            $"`{songcount++}.` [{x.Title.TrimTo(45)}]({x.Query}) `{x.Provider}`")));
                }

                break;
            case PlaylistAction.Delete:
                var plist1 = plists.FirstOrDefault(x => x.Name.ToLower() == playlistOrSongName?.ToLower());
                if (plist1 == null)
                {
                    await ctx.Interaction.SendErrorFollowupAsync("Playlist with that name could not be found!", Config)
                        .ConfigureAwait(false);
                    return;
                }

                if (await PromptUserConfirmAsync("Are you sure you want to delete this playlist", ctx.User.Id)
                        .ConfigureAwait(false))
                {
                    var uow = dbService.GetDbContext();
                    await using var _ = uow.ConfigureAwait(false);
                    uow.MusicPlaylists.Remove(plist1);
                    await uow.SaveChangesAsync().ConfigureAwait(false);
                    await ctx.Interaction.SendConfirmFollowupAsync("Playlist deleted.").ConfigureAwait(false);
                }

                break;

            case PlaylistAction.Create:
                if (playlistOrSongName is null)
                {
                    await ctx.Interaction.SendErrorFollowupAsync("You need to specify a playlist name!", Config)
                        .ConfigureAwait(false);
                }

                if (Service.GetPlaylists(ctx.User).Select(x => x.Name.ToLower())
                    .Contains(playlistOrSongName?.ToLower()))
                {
                    await ctx.Interaction.SendErrorFollowupAsync("You already have a playlist with this name!", Config)
                        .ConfigureAwait(false);
                }
                else
                {
                    var toadd = new MusicPlaylist
                    {
                        Author = ctx.User.ToString(),
                        AuthorId = ctx.User.Id,
                        Name = playlistOrSongName,
                        Songs = new List<PlaylistSong>()
                    };
                    var uow = dbService.GetDbContext();
                    await using var _ = uow.ConfigureAwait(false);
                    uow.MusicPlaylists.Add(toadd);
                    await uow.SaveChangesAsync().ConfigureAwait(false);
                    await ctx.Interaction.SendConfirmFollowupAsync(
                        $"Successfully created playlist with name `{playlistOrSongName}`!").ConfigureAwait(false);
                }

                break;
            case PlaylistAction.Load:
                if (!string.IsNullOrEmpty(playlistOrSongName))
                {
                    var vstate = ctx.User as IVoiceState;
                    if (vstate?.VoiceChannel is null)
                    {
                        await ctx.Interaction.SendErrorFollowupAsync("You must be in a channel to use this!", Config)
                            .ConfigureAwait(false);
                        return;
                    }

                    if (!lava.HasPlayer(ctx.Guild))
                    {
                        try
                        {
                            await lava.JoinAsync(() => new MusicPlayer(client, Service, Config), ctx.Guild.Id,
                                vstate.VoiceChannel.Id).ConfigureAwait(false);
                            if (vstate.VoiceChannel is IStageChannel chan)
                            {
                                await chan.BecomeSpeakerAsync().ConfigureAwait(false);
                            }
                        }
                        catch (Exception)
                        {
                            await ctx.Interaction
                                .SendErrorFollowupAsync("Seems I may not have permission to join...", Config)
                                .ConfigureAwait(false);
                            return;
                        }
                    }

                    var plist3 = Service.GetPlaylists(ctx.User).Where(x => x.Name.ToLower() == playlistOrSongName);
                    var musicPlaylists = plist3 as MusicPlaylist?[] ?? plist3.ToArray();
                    if (musicPlaylists.Length == 0)
                    {
                        await ctx.Interaction.SendErrorFollowupAsync("A playlist with that name wasnt found!", Config)
                            .ConfigureAwait(false);
                        return;
                    }

                    var songs3 = musicPlaylists.Select(x => x.Songs).FirstOrDefault();
                    var msg = await ctx.Interaction.SendConfirmFollowupAsync(
                            $"Queueing {songs3!.Count()} songs from {musicPlaylists.FirstOrDefault()?.Name}...")
                        .ConfigureAwait(false);
                    foreach (var i in songs3!)
                    {
                        var search = await lava.LoadTracksAsync(i.Query).ConfigureAwait(false);
                        var platform = Platform.Youtube;
                        if (search.LoadType != TrackLoadType.NoMatches)
                        {
                            platform = i.Provider switch
                            {
                                "Spotify" => Platform.Spotify,
                                "Soundcloud" => Platform.Soundcloud,
                                "Direct Url / File" => Platform.Url,
                                "Youtube" => Platform.Youtube,
                                _ => platform
                            };

                            await Service.Enqueue(ctx.Guild.Id, ctx.User, search.Tracks.FirstOrDefault(), platform)
                                .ConfigureAwait(false);
                        }

                        var player = lava.GetPlayer<MusicPlayer>(ctx.Guild);
                        if (player.State == PlayerState.Playing) continue;
                        await player.PlayAsync(search.Tracks.FirstOrDefault()).ConfigureAwait(false);
                        await player
                            .SetVolumeAsync(await Service.GetVolume(ctx.Guild.Id).ConfigureAwait(false) / 100.0F)
                            .ConfigureAwait(false);
                    }

                    await msg.ModifyAsync(x => x.Embed = new EmbedBuilder()
                        .WithOkColor()
                        .WithDescription(
                            $"Successfully loaded {songs3.Count()} songs from {musicPlaylists.FirstOrDefault()?.Name}!")
                        .Build()).ConfigureAwait(false);
                    return;
                }

                if (await Service.GetDefaultPlaylist(ctx.User) is not null && !string.IsNullOrEmpty(playlistOrSongName))
                {
                    var vstate = ctx.User as IVoiceState;
                    if (vstate?.VoiceChannel is null)
                    {
                        await ctx.Interaction.SendErrorFollowupAsync("You must be in a channel to use this!", Config)
                            .ConfigureAwait(false);
                        return;
                    }

                    var uow = dbService.GetDbContext();
                    await using var _ = uow.ConfigureAwait(false);
                    var plist2 = await uow.MusicPlaylists.GetDefaultPlaylist(ctx.User.Id);
                    var songs2 = plist2.Songs;
                    if (!songs2.Any())
                    {
                        await ctx.Interaction.SendErrorFollowupAsync(
                                "Your default playlist has no songs! Please add songs and try again.", Config)
                            .ConfigureAwait(false);
                        return;
                    }

                    if (!lava.HasPlayer(ctx.Guild))
                    {
                        try
                        {
                            await lava.JoinAsync(() => new MusicPlayer(client, Service, Config), ctx.Guild.Id,
                                vstate.VoiceChannel.Id).ConfigureAwait(false);
                            if (vstate.VoiceChannel is IStageChannel chan)
                            {
                                await chan.BecomeSpeakerAsync().ConfigureAwait(false);
                            }
                        }
                        catch (Exception)
                        {
                            await ctx.Interaction
                                .SendErrorFollowupAsync("Seems I may not have permission to join...", Config)
                                .ConfigureAwait(false);
                            return;
                        }
                    }

                    var msg = await ctx.Interaction.SendConfirmFollowupAsync(
                        $"Queueing {songs2.Count()} songs from {plist2.Name}...").ConfigureAwait(false);
                    foreach (var i in songs2)
                    {
                        var search = await lava.LoadTracksAsync(i.Query).ConfigureAwait(false);
                        if (search.LoadType != TrackLoadType.NoMatches)
                            await Service.Enqueue(ctx.Guild.Id, ctx.User, search.Tracks.FirstOrDefault())
                                .ConfigureAwait(false);
                        var player = lava.GetPlayer<MusicPlayer>(ctx.Guild);
                        if (player.State == PlayerState.Playing) continue;
                        await player.PlayAsync(search.Tracks.FirstOrDefault()).ConfigureAwait(false);
                        await player
                            .SetVolumeAsync(await Service.GetVolume(ctx.Guild.Id).ConfigureAwait(false) / 100.0F)
                            .ConfigureAwait(false);
                    }

                    await msg.ModifyAsync(x => x.Embed = new EmbedBuilder()
                        .WithOkColor()
                        .WithDescription(
                            $"Successfully loaded {songs2.Count()} songs from {plist2.Name}!")
                        .Build()).ConfigureAwait(false);
                    return;
                }

                if (await Service.GetDefaultPlaylist(ctx.User) is null && string.IsNullOrEmpty(playlistOrSongName))
                {
                    await ctx.Interaction.SendErrorFollowupAsync(
                            "You don't have a default playlist set and have not specified a playlist name!", Config)
                        .ConfigureAwait(false);
                }

                break;
            case PlaylistAction.Save:
                var queue = Service.GetQueue(ctx.Guild.Id);
                var plists5 = Service.GetPlaylists(ctx.User);
                if (!plists5.Any())
                {
                    await ctx.Interaction.SendErrorFollowupAsync("You do not have any playlists!", Config)
                        .ConfigureAwait(false);
                    return;
                }

                var trysearch = queue.Where(x => x.Title.ToLower().Contains(playlistOrSongName?.ToLower() ?? ""))
                    .Take(20);
                var advancedLavaTracks = trysearch as LavalinkTrack[] ?? trysearch.ToArray();

                if (advancedLavaTracks.Length == 1)
                {
                    var msg = await ctx.Interaction.SendConfirmFollowupAsync(
                        "Please type the name of the playlist you wanna save this to!").ConfigureAwait(false);
                    var nmsg = await NextMessageAsync(ctx.Interaction.Id, ctx.User.Id).ConfigureAwait(false);
                    var plists6 = plists5.FirstOrDefault(x =>
                        string.Equals(x.Name, nmsg, StringComparison.CurrentCultureIgnoreCase));
                    if (plists6 is not null)
                    {
                        var currentContext =
                            advancedLavaTracks.FirstOrDefault().Context as AdvancedTrackContext;
                        var toadd = new PlaylistSong
                        {
                            Title = advancedLavaTracks.FirstOrDefault()?.Title,
                            ProviderType = currentContext.QueuedPlatform,
                            Provider = currentContext.QueuedPlatform.ToString(),
                            Query = advancedLavaTracks.FirstOrDefault()!.Uri.AbsoluteUri
                        };
                        var newsongs = plists6.Songs.ToList();
                        newsongs.Add(toadd);
                        var toupdate = new MusicPlaylist
                        {
                            Id = plists6.Id,
                            AuthorId = plists6.AuthorId,
                            Author = plists6.Author,
                            DateAdded = plists6.DateAdded,
                            IsDefault = plists6.IsDefault,
                            Name = plists6.Name,
                            Songs = newsongs
                        };
                        var uow = dbService.GetDbContext();
                        await using var _ = uow.ConfigureAwait(false);
                        uow.MusicPlaylists.Update(toupdate);
                        await uow.SaveChangesAsync().ConfigureAwait(false);
                        await msg.DeleteAsync().ConfigureAwait(false);
                        await ctx.Interaction.SendConfirmFollowupAsync(
                                $"Added {advancedLavaTracks.FirstOrDefault()?.Title} to {plists6.Name}.")
                            .ConfigureAwait(false);
                    }
                    else
                    {
                        await ctx.Interaction
                            .SendErrorFollowupAsync("Please make sure you put in the right playlist name.", Config)
                            .ConfigureAwait(false);
                    }
                }
                else
                {
                    var components = new ComponentBuilder().WithButton("Save All", "all")
                        .WithButton("Choose", "choose");
                    var msg = await ctx.Interaction.SendConfirmFollowupAsync(
                        "I found more than one result for that name. Would you like me to save all or choose from 10?",
                        components).ConfigureAwait(false);
                    switch (await GetButtonInputAsync(ctx.Interaction.Id, msg.Id, ctx.User.Id).ConfigureAwait(false))
                    {
                        case "all":
                            msg = await ctx.Interaction.SendConfirmFollowupAsync(
                                "Please type the name of the playlist you wanna save this to!").ConfigureAwait(false);
                            var nmsg1 = await NextMessageAsync(ctx.Interaction.Id, ctx.User.Id).ConfigureAwait(false);
                            var plists7 = plists5.FirstOrDefault(x =>
                                string.Equals(x.Name, nmsg1, StringComparison.CurrentCultureIgnoreCase));
                            if (plists7 is not null)
                            {
                                var toadd = advancedLavaTracks.Select(x => new PlaylistSong
                                {
                                    Title = x.Title,
                                    ProviderType = (x.Context as AdvancedTrackContext).QueuedPlatform,
                                    Provider = (x.Context as AdvancedTrackContext).QueuedPlatform.ToString(),
                                    Query = x.Uri.AbsolutePath
                                });
                                var newsongs = plists7.Songs.ToList();
                                newsongs.AddRange(toadd);
                                var toupdate = new MusicPlaylist
                                {
                                    Id = plists7.Id,
                                    AuthorId = plists7.AuthorId,
                                    Author = plists7.Author,
                                    DateAdded = plists7.DateAdded,
                                    IsDefault = plists7.IsDefault,
                                    Name = plists7.Name,
                                    Songs = newsongs
                                };
                                var uow = dbService.GetDbContext();
                                await using var _ = uow.ConfigureAwait(false);
                                uow.MusicPlaylists.Update(toupdate);
                                await uow.SaveChangesAsync().ConfigureAwait(false);
                                await msg.DeleteAsync().ConfigureAwait(false);
                                await ctx.Interaction
                                    .SendConfirmFollowupAsync($"Added {toadd.Count()} tracks to {plists7.Name}.")
                                    .ConfigureAwait(false);
                            }
                            else
                            {
                                await ctx.Interaction.SendErrorFollowupAsync(
                                        "Please make sure you put in the right playlist name.", Config)
                                    .ConfigureAwait(false);
                            }

                            break;

                        case "choose":
                            var components1 = new ComponentBuilder();
                            var count1 = 1;
                            var count = 1;
                            foreach (var _ in advancedLavaTracks)
                            {
                                if (count1 >= 6)
                                {
                                    components1.WithButton(count1++.ToString(), count1.ToString(),
                                        row: 1);
                                }
                                else
                                {
                                    components1.WithButton(count1++.ToString(), count1.ToString());
                                }
                            }

                            await msg.DeleteAsync().ConfigureAwait(false);
                            var msg2 = await ctx.Interaction.SendConfirmFollowupAsync(
                                string.Join("\n",
                                    advancedLavaTracks.Select(x => $"{count++}. {x.Title.TrimTo(140)} - {x.Author}")),
                                components1).ConfigureAwait(false);
                            var response = await GetButtonInputAsync(ctx.Interaction.Id, msg2.Id, ctx.User.Id)
                                .ConfigureAwait(false);
                            var track = advancedLavaTracks.ElementAt(int.Parse(response) - 2);
                            msg = await ctx.Interaction.SendConfirmFollowupAsync(
                                "Please type the name of the playlist you wanna save this to!").ConfigureAwait(false);
                            var nmsg = await NextMessageAsync(ctx.Interaction.Id, ctx.User.Id).ConfigureAwait(false);
                            var plists6 = plists5.FirstOrDefault(x =>
                                string.Equals(x.Name, nmsg, StringComparison.CurrentCultureIgnoreCase));
                            if (plists6 is not null)
                            {
                                var currentContext = track.Context as AdvancedTrackContext;
                                var toadd = new PlaylistSong
                                {
                                    Title = track.Title,
                                    ProviderType = currentContext.QueuedPlatform,
                                    Provider = currentContext.QueuedPlatform.ToString(),
                                    Query = track.Uri.AbsoluteUri
                                };
                                var newsongs = plists6.Songs.ToList();
                                newsongs.Add(toadd);
                                var toupdate = new MusicPlaylist
                                {
                                    Id = plists6.Id,
                                    AuthorId = plists6.AuthorId,
                                    Author = plists6.Author,
                                    DateAdded = plists6.DateAdded,
                                    IsDefault = plists6.IsDefault,
                                    Name = plists6.Name,
                                    Songs = newsongs
                                };
                                var uow = dbService.GetDbContext();
                                await using var _ = uow.ConfigureAwait(false);
                                uow.MusicPlaylists.Update(toupdate);
                                await uow.SaveChangesAsync().ConfigureAwait(false);
                                await msg.DeleteAsync().ConfigureAwait(false);
                                await ctx.Interaction
                                    .SendConfirmFollowupAsync($"Added {track.Title} to {plists6.Name}.")
                                    .ConfigureAwait(false);
                            }
                            else
                            {
                                await ctx.Interaction.SendErrorFollowupAsync(
                                        "Please make sure you put in the right playlist name.", Config)
                                    .ConfigureAwait(false);
                            }

                            break;
                    }
                }

                break;
            case PlaylistAction.Default:
                var defaultplaylist = await Service.GetDefaultPlaylist(ctx.User);
                if (string.IsNullOrEmpty(playlistOrSongName) && defaultplaylist is not null)
                {
                    await ctx.Interaction
                        .SendConfirmFollowupAsync($"Your current default playlist is {defaultplaylist.Name}")
                        .ConfigureAwait(false);
                    return;
                }

                if (string.IsNullOrEmpty(playlistOrSongName) && defaultplaylist is null)
                {
                    await ctx.Interaction.SendErrorFollowupAsync("You do not have a default playlist set.", Config)
                        .ConfigureAwait(false);
                    return;
                }

                if (!string.IsNullOrEmpty(playlistOrSongName) && defaultplaylist is not null)
                {
                    var plist4 = Service.GetPlaylists(ctx.User)
                        .FirstOrDefault(x =>
                            string.Equals(x.Name, playlistOrSongName, StringComparison.CurrentCultureIgnoreCase));
                    if (plist4 is null)
                    {
                        await ctx.Interaction.SendErrorFollowupAsync(
                                "Playlist by that name wasn't found. Please try another name!", Config)
                            .ConfigureAwait(false);
                        return;
                    }

                    if (plist4.Name == defaultplaylist.Name)
                    {
                        await ctx.Interaction.SendErrorFollowupAsync("This is already your default playlist!", Config)
                            .ConfigureAwait(false);
                        return;
                    }

                    if (await PromptUserConfirmAsync("Are you sure you want to switch default playlists?", ctx.User.Id)
                            .ConfigureAwait(false))
                    {
                        await Service.UpdateDefaultPlaylist(ctx.User, plist4).ConfigureAwait(false);
                        await ctx.Interaction.SendConfirmFollowupAsync("Default Playlist Updated.")
                            .ConfigureAwait(false);
                    }
                }

                if (!string.IsNullOrEmpty(playlistOrSongName) && defaultplaylist is null)
                {
                    var plist4 = Service.GetPlaylists(ctx.User)
                        .FirstOrDefault(x =>
                            string.Equals(x.Name, playlistOrSongName, StringComparison.CurrentCultureIgnoreCase));
                    if (plist4 is null)
                    {
                        await ctx.Interaction.SendErrorFollowupAsync(
                                "Playlist by that name wasn't found. Please try another name!", Config)
                            .ConfigureAwait(false);
                        return;
                    }

                    await Service.UpdateDefaultPlaylist(ctx.User, plist4).ConfigureAwait(false);
                    await ctx.Interaction.SendConfirmFollowupAsync("Default Playlist Set.").ConfigureAwait(false);
                }

                break;
        }
    }

    /// <summary>
    /// Gets the lyrics of a song.
    /// </summary>
    /// <param name="name">The name of the song to get the lyrics of. Leave blank to get the lyrics of the current song</param>
    [SlashCommand("lyrics", "Get lyrics for the currently playing or mentioned song"),
     Discord.Interactions.RequireContext(ContextType.Guild)]
    public async Task Lyrics([Remainder] string? name = null)
    {
        if (string.IsNullOrEmpty(creds.GeniusKey))
        {
            await ctx.Channel.SendErrorAsync("Genius API key is not set up.", Config).ConfigureAwait(false);
            return;
        }

        var api = new GeniusClient(creds.GeniusKey);
        if (api is null)
        {
            await ctx.Channel.SendErrorAsync("Wrong genius key.", Config);
            return;
        }

        Song song;
        if (name is null)
        {
            var player = lava.GetPlayer<MusicPlayer>(ctx.Guild.Id);
            if (player is null)
            {
                await ctx.Channel.SendErrorAsync("Theres nothing playing.", Config).ConfigureAwait(false);
                return;
            }

            var search = await api.SearchClient.Search($"{player.CurrentTrack.Author} {player.CurrentTrack.Title}")
                .ConfigureAwait(false);
            if (!search.Response.Hits.Any())
            {
                await ctx.Channel.SendErrorAsync("No lyrics found for this song.", Config).ConfigureAwait(false);
                return;
            }

            song = search.Response.Hits.First().Result;
        }
        else
        {
            var search = await api.SearchClient.Search(name).ConfigureAwait(false);
            if (!search.Response.Hits.Any())
            {
                await ctx.Channel.SendErrorAsync("No lyrics found for this song.", Config).ConfigureAwait(false);
                return;
            }

            song = search.Response.Hits.First().Result;
        }

        var httpClient = new HttpClient();
        var songPage = await httpClient.GetStringAsync($"{song.Url}?bagon=1");
        var htmlDoc = new HtmlDocument();
        htmlDoc.LoadHtml(songPage);
        var lyricsDiv =
            htmlDoc.DocumentNode.SelectSingleNode("//*[contains(@class, 'Lyrics__Container-sc-1ynbvzw-5')]");
        if (lyricsDiv is null)
        {
            await ctx.Channel.SendErrorAsync("Could not find lyrics for this song.", Config).ConfigureAwait(false);
            return;
        }

        var htmlWithLineBreaks = lyricsDiv.InnerHtml.Replace("<br>", "\n").Replace("<p>", "\n");
        var htmlDocWithLineBreaks = new HtmlDocument();
        htmlDocWithLineBreaks.LoadHtml(htmlWithLineBreaks);

        var fullLyrics = htmlDocWithLineBreaks.DocumentNode.InnerText.Trim();
        var lyricsPages = new List<string>();
        for (var i = 0; i < fullLyrics.Length; i += 4000)
        {
            lyricsPages.Add(fullLyrics.Substring(i, Math.Min(4000, fullLyrics.Length - i)));
        }

        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.Users | PaginatorFooter.PageNumber)
            .WithMaxPageIndex(lyricsPages.Count - 1)
            .WithDefaultCanceledPage()
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await interactive.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60))
            .ConfigureAwait(false);

        async Task<PageBuilder> PageFactory(int page)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            return new PageBuilder()
                .WithTitle($"{song.PrimaryArtist.Name} - {song.Title}")
                .WithDescription(lyricsPages[page])
                .WithOkColor();
        }
    }

    /// <summary>
    /// Command to make the bot join the user's voice channel.
    /// </summary>
    [SlashCommand("join", "Join your current voice channel."), Discord.Interactions.RequireContext(ContextType.Guild),
     CheckPermissions]
    public async Task Join()
    {
        var currentUser = await ctx.Guild.GetUserAsync(Context.Client.CurrentUser.Id).ConfigureAwait(false);
        if (lava.GetPlayer<MusicPlayer>(Context.Guild) != null && currentUser.VoiceChannel != null)
        {
            await ctx.Interaction.SendErrorAsync("I'm already connected to a voice channel!", Config)
                .ConfigureAwait(false);
            return;
        }

        var voiceState = Context.User as IVoiceState;
        if (voiceState?.VoiceChannel == null)
        {
            await ctx.Interaction.SendErrorAsync("You must be connected to a voice channel!", Config)
                .ConfigureAwait(false);
            return;
        }

        await lava.JoinAsync(() => new MusicPlayer(client, Service, Config), ctx.Guild.Id, voiceState.VoiceChannel.Id)
            .ConfigureAwait(false);
        if (voiceState.VoiceChannel is IStageChannel chan)
        {
            try
            {
                await chan.BecomeSpeakerAsync().ConfigureAwait(false);
            }
            catch
            {
                //
            }
        }

        await ctx.Interaction.SendConfirmAsync($"Joined {voiceState.VoiceChannel.Name}!").ConfigureAwait(false);
    }

    /// <summary>
    /// Command to make the bot leave the voice channel it's currently connected to.
    /// </summary>
    [SlashCommand("leave", "Leave your current voice channel"), Discord.Interactions.RequireContext(ContextType.Guild),
     CheckPermissions]
    public async Task Leave()
    {
        var player = lava.GetPlayer<MusicPlayer>(ctx.Guild.Id);
        if (player == null)
        {
            await ctx.Interaction.SendErrorAsync("I'm not connected to any voice channels!", Config)
                .ConfigureAwait(false);
            return;
        }

        var voiceChannel = (Context.User as IVoiceState)?.VoiceChannel.Id ?? player.VoiceChannelId;
        if (voiceChannel == null)
        {
            await ctx.Interaction.SendErrorAsync("Not sure which voice channel to disconnect from.", Config)
                .ConfigureAwait(false);
            return;
        }

        await player.StopAsync(true).ConfigureAwait(false);
        await ctx.Interaction.SendConfirmAsync("I've left the channel and cleared the queue!").ConfigureAwait(false);
        await Service.QueueClear(ctx.Guild.Id).ConfigureAwait(false);
    }

    /// <summary>
    /// Command to play a song from the queue.
    /// </summary>
    /// <param name="number">The number of the song in the queue to play.</param>
    [SlashCommand("queueplay", "Plays a song from a number in queue"),
     Discord.Interactions.RequireContext(ContextType.Guild), CheckPermissions]
    public async Task Play(int number)
    {
        var player = lava.GetPlayer<MusicPlayer>(ctx.Guild.Id);
        var queue = Service.GetQueue(ctx.Guild.Id);
        if (player is null)
        {
            var vc = ctx.User as IVoiceState;
            if (vc?.VoiceChannel is null)
            {
                await ctx.Interaction
                    .SendErrorAsync("Looks like both you and the bot are not in a voice channel.", Config)
                    .ConfigureAwait(false);
                return;
            }
        }

        if (queue.Count > 0)
        {
            var track = queue.ElementAt(number - 1);
            if (track.Uri is null)
            {
                await Play($"{number}").ConfigureAwait(false);
                return;
            }

            await player.PlayAsync(track).ConfigureAwait(false);
            using var artworkService = new ArtworkService();
            var e = await artworkService.ResolveAsync(track).ConfigureAwait(false);
            var eb = new EmbedBuilder()
                .WithDescription($"Playing {track.Title}")
                .WithFooter(
                    $"Track {queue.IndexOf(track) + 1} | {track.Duration:hh\\:mm\\:ss} | {((AdvancedTrackContext)track.Context).QueueUser}")
                .WithThumbnailUrl(e.AbsoluteUri)
                .WithOkColor();
            await ctx.Interaction.RespondAsync(embed: eb.Build()).ConfigureAwait(false);
        }
        else
        {
            await Play($"{number}").ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Play a song using a search query or a URL.
    /// </summary>
    /// <param name="searchQuery">The search query or URL to play.</param>
    [SlashCommand("play", "play a song using the name or a link"),
     Discord.Interactions.RequireContext(ContextType.Guild), CheckPermissions]
    // ReSharper disable once MemberCanBePrivate.Global
    public async Task Play(string? searchQuery)
    {
        await ctx.Interaction.DeferAsync().ConfigureAwait(false);
        int count;
        if (!lava.HasPlayer(Context.Guild))
        {
            var vc = ctx.User as IVoiceState;
            if (vc?.VoiceChannel is null)
            {
                await ctx.Interaction
                    .SendErrorAsync("Looks like both you and the bot are not in a voice channel.", Config)
                    .ConfigureAwait(false);
                return;
            }

            try
            {
                await lava.JoinAsync(() => new MusicPlayer(client, Service, Config), ctx.Guild.Id, vc.VoiceChannel.Id)
                    .ConfigureAwait(false);
                if (vc.VoiceChannel is SocketStageChannel chan)
                {
                    try
                    {
                        await chan.BecomeSpeakerAsync().ConfigureAwait(false);
                    }
                    catch
                    {
                        await ctx.Interaction.SendErrorAsync(
                                "I tried to join as a speaker but I'm unable to! Please drag me to the channel manually.",
                                Config)
                            .ConfigureAwait(false);
                    }
                }
            }
            catch
            {
                await ctx.Interaction.SendErrorAsync("Seems I'm unable to join the channel! Check permissions!", Config)
                    .ConfigureAwait(false);
                return;
            }
        }

        if ((!config.Data.YoutubeSupport && searchQuery.Contains("youtube.com")) ||
            (!config.Data.YoutubeSupport && searchQuery.Contains("youtu.be")))
        {
            var eb = new EmbedBuilder().WithErrorColor()
                .WithTitle("YouTube support on Public Mewdeko has been disabled.")
                .WithDescription(Format.Bold(
                    "YouTube support has been disabled due to unfair unverification from Discord that is targetting smaller bots that use YouTube for music.\n\n This does not mean Mewdeko is going premium. You have options below."))
                .AddField("Donate for a Selfhost", Format.Bold("https://ko-fi.com/mewdeko"))
                .AddField("Host on yourself", Format.Bold("https://github.com/Pusheon/Mewdeko"))
                .AddField("More Info", Format.Bold("https://youtu.be/fOpEdS3JVYQ"))
                .AddField("Support Server", Format.Bold(config.Data.SupportServer))
                .Build();
            await ctx.Interaction.FollowupAsync(embed: eb);
            return;
        }

        var player = lava.GetPlayer<MusicPlayer>(ctx.Guild);
        if (!Uri.IsWellFormedUriString(searchQuery, UriKind.RelativeOrAbsolute)
            || searchQuery.Contains("youtube.com") || searchQuery.Contains("youtu.be") ||
            searchQuery.Contains("soundcloud.com") || searchQuery.Contains("soundcloud.com") ||
            searchQuery.Contains("twitch.tv") || searchQuery.CheckIfMusicUrl())
        {
            if (player is null)
            {
                await Service.ModifySettingsInternalAsync(ctx.Guild.Id,
                        (settings, _) => settings.MusicChannelId = ctx.Interaction.Id, ctx.Interaction.Id)
                    .ConfigureAwait(false);
            }

            var searchResponse = await lava.LoadTracksAsync(searchQuery,
                    !config.Data.YoutubeSupport ? SearchMode.SoundCloud : SearchMode.None)
                .ConfigureAwait(false);
            var platform = Platform.Youtube;
            if (client.CurrentUser.Id == 752236274261426212)
                platform = Platform.Soundcloud;
            if (searchQuery.Contains("soundcloud.com"))
                platform = Platform.Soundcloud;
            if (searchQuery.CheckIfMusicUrl())
                platform = Platform.Url;
            if (searchQuery.Contains("twitch.tv"))
                platform = Platform.Twitch;
            await Service.Enqueue(ctx.Guild.Id, ctx.User, searchResponse.Tracks, platform).ConfigureAwait(false);
            count = Service.GetQueue(ctx.Guild.Id).Count;
            if (searchResponse.PlaylistInfo.Name is not null)
            {
                var eb = new EmbedBuilder()
                    .WithOkColor()
                    .WithDescription(
                        $"Queued {searchResponse.Tracks.Length} tracks from {searchResponse.PlaylistInfo.Name}")
                    .WithFooter($"{count} songs now in the queue");
                await ctx.Interaction.FollowupAsync(embed: eb.Build(),
                    components: config.Data.ShowInviteButton
                        ? new ComponentBuilder()
                            .WithButton(style: ButtonStyle.Link,
                                url:
                                "https://discord.com/oauth2/authorize?client_id=752236274261426212&permissions=8&response_type=code&redirect_uri=https%3A%2F%2Fmewdeko.tech&scope=bot%20applications.commands",
                                label: "Invite Me!",
                                emote: "<a:HaneMeow:968564817784877066>".ToIEmote()).Build()
                        : null).ConfigureAwait(false);
                if (player.State != PlayerState.Playing)
                    await player.PlayAsync(searchResponse.Tracks.FirstOrDefault()).ConfigureAwait(false);
                await player.SetVolumeAsync(await Service.GetVolume(ctx.Guild.Id).ConfigureAwait(false) / 100.0F)
                    .ConfigureAwait(false);
                return;
            }
            else
            {
                var artworkService = new ArtworkService();
                var art = new Uri(null);
                try
                {
                    art = await artworkService.ResolveAsync(searchResponse.Tracks.FirstOrDefault())
                        .ConfigureAwait(false);
                }
                catch
                {
                    //ignored
                }

                var eb = new EmbedBuilder()
                    .WithOkColor()
                    .WithThumbnailUrl(art?.AbsoluteUri)
                    .WithDescription(
                        $"Queued {searchResponse.Tracks.FirstOrDefault().Title} by {searchResponse.Tracks.FirstOrDefault().Author}!");
                await ctx.Interaction.FollowupAsync(embed: eb.Build(),
                    components: config.Data.ShowInviteButton
                        ? new ComponentBuilder()
                            .WithButton(style: ButtonStyle.Link,
                                url:
                                "https://discord.com/oauth2/authorize?client_id=752236274261426212&permissions=8&response_type=code&redirect_uri=https%3A%2F%2Fmewdeko.tech&scope=bot%20applications.commands",
                                label: "Invite Me!",
                                emote: "<a:HaneMeow:968564817784877066>".ToIEmote()).Build()
                        : null).ConfigureAwait(false);
                if (player.State != PlayerState.Playing)
                    await player.PlayAsync(searchResponse.Tracks.FirstOrDefault()).ConfigureAwait(false);
                await player.SetVolumeAsync(await Service.GetVolume(ctx.Guild.Id).ConfigureAwait(false) / 100.0F)
                    .ConfigureAwait(false);
                return;
            }
        }

        if (searchQuery.Contains("spotify"))
        {
            await Service.SpotifyQueue(ctx.Guild, ctx.User, ctx.Channel as ITextChannel, player, searchQuery)
                .ConfigureAwait(false);
            return;
        }

        var searchResponse2 = await lava.GetTracksAsync(searchQuery,
                !config.Data.YoutubeSupport ? SearchMode.SoundCloud : SearchMode.YouTube)
            .ConfigureAwait(false);
        if (!searchResponse2.Any())
        {
            await ctx.Interaction
                .SendErrorFollowupAsync("Seems like I can't find that video, please try again.", Config)
                .ConfigureAwait(false);
            return;
        }

        var components = new ComponentBuilder().WithButton("Play All", "all").WithButton("Select", "select")
            .WithButton("Play First", "pf").WithButton("Cancel", "cancel", ButtonStyle.Danger);
        var eb12 = new EmbedBuilder()
            .WithOkColor()
            .WithTitle("Would you like me to:")
            .WithDescription("Play all that I found\n" +
                             "Let you select from the top 5\n" +
                             "Just play the first thing I found");
        var msg = await ctx.Interaction.FollowupAsync(embed: eb12.Build(), components: components.Build())
            .ConfigureAwait(false);
        var button = await GetButtonInputAsync(ctx.Channel.Id, msg.Id, ctx.User.Id, true).ConfigureAwait(false);
        switch (button)
        {
            case "all":
                await Service.Enqueue(ctx.Guild.Id, ctx.User, searchResponse2.ToArray()).ConfigureAwait(false);
                count = Service.GetQueue(ctx.Guild.Id).Count;
                var track = searchResponse2.FirstOrDefault();
                var e = new ArtworkService();
                var info = await e.ResolveAsync(track).ConfigureAwait(false);
                var eb1 = new EmbedBuilder()
                    .WithOkColor()
                    .WithDescription($"Added {track.Title} along with {searchResponse2.Count()} other tracks.")
                    .WithThumbnailUrl(info.AbsoluteUri)
                    .WithFooter($"{count} songs in queue");
                if (player.State != PlayerState.Playing)
                {
                    await player.PlayAsync(track).ConfigureAwait(false);
                    await player.SetVolumeAsync(await Service.GetVolume(ctx.Guild.Id).ConfigureAwait(false) / 100.0F)
                        .ConfigureAwait(false);
                    await Service.ModifySettingsInternalAsync(ctx.Guild.Id,
                            (settings, _) => settings.MusicChannelId = ctx.Interaction.Id, ctx.Interaction.Id)
                        .ConfigureAwait(false);
                }

                await msg.ModifyAsync(x =>
                {
                    x.Components = null;
                    x.Embed = eb1.Build();
                }).ConfigureAwait(false);
                break;
            case "select":
                var tracks = searchResponse2.Take(5).ToArray();
                var count1 = 1;
                var eb = new EmbedBuilder()
                    .WithDescription(string.Join("\n", tracks.Select(x => $"{count1++}. {x.Title} by {x.Author}")))
                    .WithOkColor()
                    .WithTitle("Pick which one!");
                count1 = 0;
                var components1 = new ComponentBuilder();
                foreach (var _ in tracks)
                {
                    var component =
                        new ButtonBuilder(customId: (count1 + 1).ToString(), label: (count1 + 1).ToString());
                    count1++;
                    components1.WithButton(component);
                }

                await msg.ModifyAsync(x =>
                {
                    x.Components = components1.Build();
                    x.Embed = eb.Build();
                }).ConfigureAwait(false);
                var input = await GetButtonInputAsync(ctx.Channel.Id, msg.Id, ctx.User.Id).ConfigureAwait(false);
                var chosen = tracks[int.Parse(input) - 1];
                var e1 = new ArtworkService();
                var info1 = await e1.ResolveAsync(chosen).ConfigureAwait(false);
                await Service.Enqueue(ctx.Guild.Id, ctx.User, chosen).ConfigureAwait(false);
                count = Service.GetQueue(ctx.Guild.Id).Count;
                eb1 = new EmbedBuilder()
                    .WithOkColor()
                    .WithDescription($"Added {chosen.Title} by {chosen.Author} to the queue.")
                    .WithThumbnailUrl(info1.AbsoluteUri)
                    .WithFooter($"{count} songs in queue");
                if (player.State != PlayerState.Playing)
                {
                    await player.PlayAsync(chosen).ConfigureAwait(false);
                    await player.SetVolumeAsync(await Service.GetVolume(ctx.Guild.Id).ConfigureAwait(false) / 100.0F)
                        .ConfigureAwait(false);
                    await Service.ModifySettingsInternalAsync(ctx.Guild.Id,
                            (settings, _) => settings.MusicChannelId = ctx.Interaction.Id, ctx.Interaction.Id)
                        .ConfigureAwait(false);
                }

                await msg.ModifyAsync(x =>
                {
                    x.Components = null;
                    x.Embed = eb1.Build();
                }).ConfigureAwait(false);
                break;
            case "pf":
                track = searchResponse2.FirstOrDefault();
                await Service.Enqueue(ctx.Guild.Id, ctx.User, track).ConfigureAwait(false);
                var a = new ArtworkService();
                var info2 = await a.ResolveAsync(track).ConfigureAwait(false);
                count = Service.GetQueue(ctx.Guild.Id).Count;
                eb1 = new EmbedBuilder()
                    .WithOkColor()
                    .WithDescription($"Added {track.Title} by {track.Author} to the queue.")
                    .WithThumbnailUrl(info2.AbsoluteUri)
                    .WithFooter($"{count} songs in queue");
                await msg.ModifyAsync(x =>
                {
                    x.Embed = eb1.Build();
                    x.Components = null;
                }).ConfigureAwait(false);
                if (player.State != PlayerState.Playing)
                {
                    await player.PlayAsync(track).ConfigureAwait(false);
                    await player.SetVolumeAsync(await Service.GetVolume(ctx.Guild.Id).ConfigureAwait(false) / 100.0F)
                        .ConfigureAwait(false);
                    await Service.ModifySettingsInternalAsync(ctx.Guild.Id,
                            (settings, _) => settings.MusicChannelId = ctx.Interaction.Id, ctx.Interaction.Id)
                        .ConfigureAwait(false);
                }

                break;
            case "cancel":
                var eb13 = new EmbedBuilder()
                    .WithDescription("Cancelled.")
                    .WithErrorColor();
                await msg.ModifyAsync(x =>
                {
                    x.Embed = eb13.Build();
                    x.Components = null;
                }).ConfigureAwait(false);
                break;
        }
    }

    /// <summary>
    /// Command to pause the music player.
    /// </summary>
    [SlashCommand("pause", "Pauses the current track"), Discord.Interactions.RequireContext(ContextType.Guild),
     CheckPermissions]
    public async Task Pause()
    {
        var player = lava.GetPlayer<MusicPlayer>(ctx.Guild.Id);
        if (player is null)
        {
            await ctx.Interaction.SendErrorAsync("I'm not connected to a voice channel.", Config).ConfigureAwait(false);
            return;
        }

        if (player.State != PlayerState.Playing)
        {
            await player.ResumeAsync().ConfigureAwait(false);
            await ctx.Interaction.SendConfirmAsync("Resumed player.").ConfigureAwait(false);
            return;
        }

        await player.PauseAsync().ConfigureAwait(false);
        await ctx.Interaction
            .SendConfirmAsync($"Paused player. Do {await guildSettings.GetPrefix(ctx.Guild.Id)}pause again to resume.")
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Command to shuffle the music queue.
    /// </summary>
    [SlashCommand("shuffle", "Shuffles the current queue"), Discord.Interactions.RequireContext(ContextType.Guild),
     CheckPermissions]
    public async Task Shuffle()
    {
        var player = lava.GetPlayer<MusicPlayer>(ctx.Guild.Id);
        if (player is null)
        {
            await ctx.Interaction.SendErrorAsync("I'm not even playing anything.", Config).ConfigureAwait(false);
            return;
        }

        if (Service.GetQueue(ctx.Guild.Id).Count == 0)
        {
            await ctx.Interaction.SendErrorAsync("There's nothing in queue.", Config).ConfigureAwait(false);
            return;
        }

        if (Service.GetQueue(ctx.Guild.Id).Count == 1)
        {
            await ctx.Interaction.SendErrorAsync("... There's literally only one thing in queue.", Config)
                .ConfigureAwait(false);
            return;
        }

        Service.Shuffle(ctx.Guild);
        await ctx.Interaction.SendConfirmAsync("Successfully shuffled the queue!").ConfigureAwait(false);
    }

    /// <summary>
    /// Command to stop the music player and clear the queue.
    /// </summary>
    [SlashCommand("stop", "Stops the player and clears all current songs"),
     Discord.Interactions.RequireContext(ContextType.Guild), CheckPermissions]
    public async Task Stop()
    {
        var player = lava.GetPlayer<MusicPlayer>(ctx.Guild.Id);
        if (player is null)
        {
            await ctx.Interaction.SendErrorAsync("I'm not connected to a channel!", Config).ConfigureAwait(false);
            return;
        }

        await player.StopAsync().ConfigureAwait(false);
        await Service.QueueClear(ctx.Guild.Id).ConfigureAwait(false);
        await ctx.Interaction.SendConfirmAsync("Stopped the player and cleared the queue!").ConfigureAwait(false);
    }

    /// <summary>
    /// Command to skip a specified number of songs in the queue.
    /// </summary>
    /// <param name="num">The number of songs to skip. Default is 1.</param>
    [SlashCommand("skip", "Skip to the next song, if there is one"),
     Discord.Interactions.RequireContext(ContextType.Guild), CheckPermissions]
    public async Task Skip(int num = 1)
    {
        if (num < 1)
        {
            await ctx.Interaction.SendErrorAsync("You can only skip ahead.", Config).ConfigureAwait(false);
            return;
        }

        var player = lava.GetPlayer<MusicPlayer>(ctx.Guild.Id);
        if (player is null)
        {
            await ctx.Interaction.SendErrorAsync("I'm not connected to a voice channel.", Config).ConfigureAwait(false);
            return;
        }

        await Service.Skip(ctx.Guild, ctx.Channel as ITextChannel, player, ctx, num).ConfigureAwait(false);
    }

    /// <summary>
    /// Command to seek to a specific position in the currently playing track.
    /// </summary>
    /// <param name="input">The position to seek to.</param>
    [SlashCommand("seek", "Seek to a certain time in the current song"),
     Discord.Interactions.RequireContext(ContextType.Guild), CheckPermissions]
    public async Task Seek(string input)
    {
        StoopidTime time;
        try
        {
            time = StoopidTime.FromInput(input);
        }
        catch
        {
            await ctx.Interaction.SendErrorAsync("Invalid time.", Config).ConfigureAwait(false);
            return;
        }

        var player = lava.GetPlayer<MusicPlayer>(ctx.Guild.Id);
        if (player is null)
        {
            await ctx.Interaction.SendErrorAsync("I'm not connected to a voice channel.", Config).ConfigureAwait(false);
            return;
        }

        if (player.State != PlayerState.Playing)
        {
            await ctx.Interaction.SendErrorAsync("Woaaah there, I can't seek when nothing is playing.", Config)
                .ConfigureAwait(false);
            return;
        }

        if (time.Time > player.CurrentTrack.Duration)
            await ctx.Channel.SendErrorAsync("That's longer than the song lol, try again.", Config)
                .ConfigureAwait(false);
        await player.SeekPositionAsync(time.Time).ConfigureAwait(false);
        await ctx.Channel.SendConfirmAsync($"I've seeked `{player.CurrentTrack.Title}` to {time.Time}.")
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Command to clear the music queue.
    /// </summary>
    [SlashCommand("clearqueue", "Clears the current queue"), Discord.Interactions.RequireContext(ContextType.Guild),
     CheckPermissions]
    public async Task ClearQueue()
    {
        var player = lava.GetPlayer<MusicPlayer>(ctx.Guild.Id);
        if (player is null)
        {
            await ctx.Interaction.SendErrorAsync("I'm not connected to a voice channel.", Config).ConfigureAwait(false);
            return;
        }

        await player.StopAsync().ConfigureAwait(false);
        await Service.QueueClear(ctx.Guild.Id).ConfigureAwait(false);
        await ctx.Interaction.SendConfirmAsync("Cleared the queue!").ConfigureAwait(false);
    }

    /// <summary>
    /// Command to set the music channel for receiving music events.
    /// </summary>
    [SlashCommand("channel", "Set the channel where music events go"),
     Discord.Interactions.RequireContext(ContextType.Guild),
     CheckPermissions]
    public async Task SetMusicChannel()
    {
        var user = await ctx.Guild.GetUserAsync(ctx.Client.CurrentUser.Id).ConfigureAwait(false);
        if (!user.GetPermissions(ctx.Channel as ITextChannel).EmbedLinks)
            return;
        await Service.ModifySettingsInternalAsync(ctx.Guild.Id,
            (settings, _) => settings.MusicChannelId = ctx.Interaction.Id, ctx.Interaction.Id).ConfigureAwait(false);
        await ctx.Interaction.SendConfirmAsync("Set this channel to recieve music events.").ConfigureAwait(false);
    }

    /// <summary>
    /// Command to set the autoplay behavior for adding songs to the queue.
    /// </summary>
    /// <param name="autoPlayNum">The number of songs to attempt to add to the queue when the last song is reached. Use 0 to disable autoplay.</param>
    [SlashCommand("autoplay", "Set the amount of songs to add at the end of the queue."),
     Discord.Interactions.RequireContext(ContextType.Guild), CheckPermissions]
    public async Task AutoPlay(int autoPlayNum)
    {
        await Service
            .ModifySettingsInternalAsync(ctx.Guild.Id, (settings, _) => settings.AutoPlay = autoPlayNum, autoPlayNum)
            .ConfigureAwait(false);
        switch (autoPlayNum)
        {
            case > 0 and < 6:
                await ctx.Interaction.SendConfirmAsync(
                    $"When the last song is reached autoplay will attempt to add `{autoPlayNum}` songs to the queue.");
                break;
            case > 5:
                await ctx.Interaction.SendErrorAsync("I can only do so much. Keep it to a maximum of 5 please.",
                    Config);
                break;
            case 0:
                await ctx.Interaction.SendConfirmAsync("Autoplay has been disabled.");
                break;
        }
    }

    /// <summary>
    /// Command to set the loop type for the music player.
    /// </summary>
    /// <param name="reptype">The loop type to set. Default is PlayerRepeatType.None.</param>
    [SlashCommand("loop", "Sets the loop type"), Discord.Interactions.RequireContext(ContextType.Guild),
     CheckPermissions]
    public async Task Loop(PlayerRepeatType reptype = PlayerRepeatType.None)
    {
        await Service.ModifySettingsInternalAsync(ctx.Guild.Id, (settings, _) => settings.PlayerRepeat = reptype,
            reptype).ConfigureAwait(false);
        await ctx.Interaction.SendConfirmAsync($"Loop has now been set to {reptype}").ConfigureAwait(false);
    }


    /// <summary>
    /// Command to set the volume of the music player.
    /// </summary>
    /// <param name="volume">The volume level to set. Should be between 0 and 100.</param>
    [SlashCommand("volume", "Sets the current volume"), Discord.Interactions.RequireContext(ContextType.Guild),
     CheckPermissions]
    public async Task Volume(int volume)
    {
        var player = lava.GetPlayer<MusicPlayer>(ctx.Guild.Id);
        if (player is null)
        {
            await ctx.Interaction.SendErrorAsync("I'm not connected to a voice channel.", Config).ConfigureAwait(false);
            return;
        }

        if (volume > 100)
        {
            await ctx.Interaction.SendErrorAsync("Max is 100 m8", Config).ConfigureAwait(false);
            return;
        }

        await player.SetVolumeAsync(volume / 100.0F).ConfigureAwait(false);
        await Service.ModifySettingsInternalAsync(ctx.Guild.Id, (settings, _) => settings.Volume = volume, volume)
            .ConfigureAwait(false);
        await ctx.Interaction.SendConfirmAsync($"Set the volume to {volume}").ConfigureAwait(false);
    }

    /// <summary>
    /// Command to display information about the currently playing track.
    /// </summary>
    [SlashCommand("nowplaying", "Shows the currently playing song"),
     Discord.Interactions.RequireContext(ContextType.Guild), CheckPermissions]
    public async Task NowPlaying()
    {
        var player = lava.GetPlayer<MusicPlayer>(ctx.Guild.Id);
        if (player is null)
        {
            await ctx.Interaction.SendErrorAsync("I'm not connected to a voice channel.", Config).ConfigureAwait(false);
            return;
        }

        if (player.State != PlayerState.Playing)
        {
            await ctx.Interaction.SendErrorAsync("Woaaah there, I'm not playing any tracks.", Config)
                .ConfigureAwait(false);
            return;
        }

        var qcount = Service.GetQueue(ctx.Guild.Id);
        var track = player.CurrentTrack;
        var artService = new ArtworkService();
        var info = await artService.ResolveAsync(track).ConfigureAwait(false);
        var eb = new EmbedBuilder()
            .WithOkColor()
            .WithTitle($"Track #{qcount.IndexOf(track) + 1}")
            .WithDescription($"Now Playing {track.Title} by {track.Author}")
            .WithThumbnailUrl(info?.AbsoluteUri)
            .WithFooter(
                await Service.GetPrettyInfo(player, ctx.Guild).ConfigureAwait(false));
        await ctx.Interaction.RespondAsync(embed: eb.Build()).ConfigureAwait(false);
    }

    /// <summary>
    /// Command to display the current music queue.
    /// </summary>
    [SlashCommand("queue", "Lists all songs"), Discord.Interactions.RequireContext(ContextType.Guild), CheckPermissions]
    public async Task Queue()
    {
        var player = lava.GetPlayer<MusicPlayer>(ctx.Guild.Id);
        if (player is null)
        {
            await ctx.Interaction.SendErrorAsync("I am not playing anything at the moment!", Config)
                .ConfigureAwait(false);
            return;
        }

        var queue = Service.GetQueue(ctx.Guild.Id);
        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(queue.Count / 10)
            .WithDefaultCanceledPage()
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await interactive.SendPaginatorAsync(paginator, ctx.Interaction as SocketInteraction, TimeSpan.FromMinutes(60))
            .ConfigureAwait(false);

        async Task<PageBuilder> PageFactory(int page)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            var tracks = queue.OrderBy(x => queue.IndexOf(x)).Skip(page * 10).Take(10);
            return new PageBuilder()
                .WithDescription(string.Join("\n", tracks.Select(x =>
                    $"`{queue.IndexOf(x) + 1}.` [{x.Title}]({x.Uri})\n`{x.Duration:mm\\:ss} {GetContext(x).QueueUser} {GetContext(x).QueuedPlatform}`")))
                .WithOkColor();
        }
    }

    private static AdvancedTrackContext GetContext(LavalinkTrack track)
        => track.Context as AdvancedTrackContext;
}