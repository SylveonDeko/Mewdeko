#nullable enable
using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Lavalink4NET;
using Lavalink4NET.Artwork;
using Lavalink4NET.DiscordNet;
using Lavalink4NET.Player;
using Lavalink4NET.Rest;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.Music.Common;
using Mewdeko.Modules.Music.Services;
using Mewdeko.Services.Settings;
using System.Threading.Tasks;
using Mewdeko.Common.Attributes.InteractionCommands;

namespace Mewdeko.Modules.Music;
[Group("music", "Play Music!")]
public class SlashMusic : MewdekoSlashModuleBase<MusicService>
{
    private readonly InteractiveService interactivity;
    private readonly LavalinkNode lavaNode;
    private readonly DbService db;
    private readonly DiscordSocketClient client;
    private readonly GuildSettingsService guildSettings;
    private readonly BotConfigService config;

    public SlashMusic(LavalinkNode lava, InteractiveService interactive, DbService dbService,
        DiscordSocketClient client,
        GuildSettingsService guildSettings,
        BotConfigService config)
    {
        db = dbService;
        this.client = client;
        this.guildSettings = guildSettings;
        this.config = config;
        interactivity = interactive;
        lavaNode = lava;
    }

    public enum PlaylistAction
    {
        Show,
        Delete,
        Create,
        Remove,
        Add,
        Load,
        Save,
        Default
    }

    [SlashCommand("remove", "Removes a song from the queue using its number"), RequireContext(ContextType.Guild),
     CheckPermissions]
    public async Task SongRemove(int songNum)
    {
        var player = lavaNode.GetPlayer<MusicPlayer>(ctx.Guild.Id);
        if (player is not null)
        {
            var voiceChannel = await ctx.Guild.GetVoiceChannelAsync(player.VoiceChannelId.Value).ConfigureAwait(false);
            var chanUsers = await voiceChannel.GetUsersAsync().FlattenAsync().ConfigureAwait(false);
            if (!chanUsers.Contains(ctx.User as IGuildUser))
            {
                await ctx.Interaction.SendErrorAsync("You are not in the bots music channel!").ConfigureAwait(false);
                return;
            }

            if (await Service.RemoveSong(ctx.Guild, songNum).ConfigureAwait(false))
            {
                await ctx.Interaction.SendConfirmAsync($"Track {songNum} removed.").ConfigureAwait(false);
            }
            else
            {
                await ctx.Interaction.SendErrorAsync("Seems like that track doesn't exist or you have nothing in queue.").ConfigureAwait(false);
            }
        }
    }
    [SlashCommand("autodisconnect", "Set the autodisconnect type"), RequireContext(ContextType.Guild), CheckPermissions]
    public async Task AutoDisconnect(AutoDisconnect disconnect)
    {
        await Service.ModifySettingsInternalAsync(ctx.Guild.Id,
            (settings, _) => settings.AutoDisconnect = disconnect, disconnect).ConfigureAwait(false);
        await ctx.Interaction.SendConfirmAsync(
            $"Successfully set AutoDisconnect to {Format.Code(disconnect.ToString())}").ConfigureAwait(false);
    }

    [SlashCommand("playlists", "Lists your playlists"), RequireContext(ContextType.Guild)]
    public async Task Playlists()
    {
        var plists = Service.GetPlaylists(ctx.User);
        if (!plists.Any())
        {
            await ctx.Interaction.SendErrorAsync("You dont have any saved playlists!").ConfigureAwait(false);
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
        await interactivity.SendPaginatorAsync(paginator, (ctx.Interaction as SocketInteraction)!, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

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

    [SlashCommand("playlist", "Create or manage your playlists"), RequireContext(ContextType.Guild), CheckPermissions]
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
                            "You have not specified a playlist name and do not have a default playlist set, there's nothing to show!").ConfigureAwait(false);
                        return;
                    }
                }
                else
                {
                    plist = Service.GetPlaylists(ctx.User)
                                   .FirstOrDefault(x => string.Equals(x.Name, playlistOrSongName, StringComparison.CurrentCultureIgnoreCase))!;
                }

                var songcount = 1;
                if (plist is null)
                {
                    await ctx.Interaction.SendErrorFollowupAsync("This is not a valid playlist!").ConfigureAwait(false);
                    return;
                }
                if (!plist.Songs.Any())
                {
                    await ctx.Interaction.SendErrorFollowupAsync("This playlist has no songs!").ConfigureAwait(false);
                    return;
                }

                var paginator = new LazyPaginatorBuilder().AddUser(ctx.User).WithPageFactory(PageFactory)
                                                          .WithFooter(
                                                              PaginatorFooter.PageNumber | PaginatorFooter.Users)
                                                          .WithMaxPageIndex(plist.Songs.Count() / 15)
                                                          .WithDefaultCanceledPage().WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage).Build();
                await interactivity.SendPaginatorAsync(paginator, (ctx.Interaction as SocketInteraction)!, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

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
                    await ctx.Interaction.SendErrorFollowupAsync("Playlist with that name could not be found!").ConfigureAwait(false);
                    return;
                }

                if (await PromptUserConfirmAsync("Are you sure you want to delete this playlist", ctx.User.Id).ConfigureAwait(false))
                {
                    var uow = db.GetDbContext();
                    await using var _ = uow.ConfigureAwait(false);
                    uow.MusicPlaylists.Remove(plist1);
                    await uow.SaveChangesAsync().ConfigureAwait(false);
                    await ctx.Interaction.SendConfirmFollowupAsync("Playlist deleted.").ConfigureAwait(false);
                }

                break;

            case PlaylistAction.Create:
                if (playlistOrSongName is null)
                {
                    await ctx.Interaction.SendErrorFollowupAsync("You need to specify a playlist name!").ConfigureAwait(false);
                }

                if (Service.GetPlaylists(ctx.User).Select(x => x.Name.ToLower()).Contains(playlistOrSongName?.ToLower()))
                {
                    await ctx.Interaction.SendErrorFollowupAsync("You already have a playlist with this name!").ConfigureAwait(false);
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
                    var uow = db.GetDbContext();
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
                        await ctx.Interaction.SendErrorFollowupAsync("You must be in a channel to use this!").ConfigureAwait(false);
                        return;
                    }

                    if (!lavaNode.HasPlayer(ctx.Guild))
                    {
                        try
                        {
                            await lavaNode.JoinAsync(() => new MusicPlayer(client, Service, config), ctx.Guild.Id, vstate.VoiceChannel.Id).ConfigureAwait(false);
                            if (vstate.VoiceChannel is IStageChannel chan)
                            {
                                await chan.BecomeSpeakerAsync().ConfigureAwait(false);
                            }
                        }
                        catch (Exception)
                        {
                            await ctx.Interaction.SendErrorFollowupAsync("Seems I may not have permission to join...").ConfigureAwait(false);
                            return;
                        }
                    }

                    var plist3 = Service.GetPlaylists(ctx.User).Where(x => x.Name.ToLower() == playlistOrSongName);
                    var musicPlaylists = plist3 as MusicPlaylist?[] ?? plist3.ToArray();
                    if (musicPlaylists.Length == 0)
                    {
                        await ctx.Interaction.SendErrorFollowupAsync("A playlist with that name wasnt found!").ConfigureAwait(false);
                        return;
                    }

                    var songs3 = musicPlaylists.Select(x => x.Songs).FirstOrDefault();
                    var msg = await ctx.Interaction.SendConfirmFollowupAsync(
                        $"Queueing {songs3!.Count()} songs from {musicPlaylists.FirstOrDefault()?.Name}...").ConfigureAwait(false);
                    foreach (var i in songs3!)
                    {
                        var search = await lavaNode.LoadTracksAsync(i.Query).ConfigureAwait(false);
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

                            await Service.Enqueue(ctx.Guild.Id, ctx.User, search.Tracks.FirstOrDefault(), platform).ConfigureAwait(false);
                        }

                        var player = lavaNode.GetPlayer<MusicPlayer>(ctx.Guild);
                        if (player.State == PlayerState.Playing) continue;
                        await player.PlayAsync(search.Tracks.FirstOrDefault()).ConfigureAwait(false);
                        await player.SetVolumeAsync(await Service.GetVolume(ctx.Guild.Id).ConfigureAwait(false) / 100.0F).ConfigureAwait(false);
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
                        await ctx.Interaction.SendErrorFollowupAsync("You must be in a channel to use this!").ConfigureAwait(false);
                        return;
                    }

                    var uow = db.GetDbContext();
                    await using var _ = uow.ConfigureAwait(false);
                    var plist2 = await uow.MusicPlaylists.GetDefaultPlaylist(ctx.User.Id);
                    var songs2 = plist2.Songs;
                    if (!songs2.Any())
                    {
                        await ctx.Interaction.SendErrorFollowupAsync(
                            "Your default playlist has no songs! Please add songs and try again.").ConfigureAwait(false);
                        return;
                    }

                    if (!lavaNode.HasPlayer(ctx.Guild))
                    {
                        try
                        {
                            await lavaNode.JoinAsync(() => new MusicPlayer(client, Service, config), ctx.Guild.Id, vstate.VoiceChannel.Id).ConfigureAwait(false);
                            if (vstate.VoiceChannel is IStageChannel chan)
                            {
                                await chan.BecomeSpeakerAsync().ConfigureAwait(false);
                            }
                        }
                        catch (Exception)
                        {
                            await ctx.Interaction.SendErrorFollowupAsync("Seems I may not have permission to join...").ConfigureAwait(false);
                            return;
                        }
                    }

                    var msg = await ctx.Interaction.SendConfirmFollowupAsync(
                        $"Queueing {songs2.Count()} songs from {plist2.Name}...").ConfigureAwait(false);
                    foreach (var i in songs2)
                    {
                        var search = await lavaNode.LoadTracksAsync(i.Query).ConfigureAwait(false);
                        if (search.LoadType != TrackLoadType.NoMatches)
                            await Service.Enqueue(ctx.Guild.Id, ctx.User, search.Tracks.FirstOrDefault()).ConfigureAwait(false);
                        var player = lavaNode.GetPlayer<MusicPlayer>(ctx.Guild);
                        if (player.State == PlayerState.Playing) continue;
                        await player.PlayAsync(search.Tracks.FirstOrDefault()).ConfigureAwait(false);
                        await player.SetVolumeAsync(await Service.GetVolume(ctx.Guild.Id).ConfigureAwait(false) / 100.0F).ConfigureAwait(false);
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
                        "You don't have a default playlist set and have not specified a playlist name!").ConfigureAwait(false);
                }

                break;
            case PlaylistAction.Save:
                var queue = Service.GetQueue(ctx.Guild.Id);
                var plists5 = Service.GetPlaylists(ctx.User);
                if (!plists5.Any())
                {
                    await ctx.Interaction.SendErrorFollowupAsync("You do not have any playlists!").ConfigureAwait(false);
                    return;
                }

                var trysearch = queue.Where(x => x.Title.ToLower().Contains(playlistOrSongName?.ToLower() ?? "")).Take(20);
                var advancedLavaTracks = trysearch as LavalinkTrack[] ?? trysearch.ToArray();

                if (advancedLavaTracks.Length == 1)
                {
                    var msg = await ctx.Interaction.SendConfirmFollowupAsync(
                        "Please type the name of the playlist you wanna save this to!").ConfigureAwait(false);
                    var nmsg = await NextMessageAsync(ctx.Interaction.Id, ctx.User.Id).ConfigureAwait(false);
                    var plists6 = plists5.FirstOrDefault(x => string.Equals(x.Name, nmsg, StringComparison.CurrentCultureIgnoreCase));
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
                        var uow = db.GetDbContext();
                        await using var _ = uow.ConfigureAwait(false);
                        uow.MusicPlaylists.Update(toupdate);
                        await uow.SaveChangesAsync().ConfigureAwait(false);
                        await msg.DeleteAsync().ConfigureAwait(false);
                        await ctx.Interaction.SendConfirmFollowupAsync(
                            $"Added {advancedLavaTracks.FirstOrDefault()?.Title} to {plists6.Name}.").ConfigureAwait(false);
                    }
                    else
                    {
                        await ctx.Interaction.SendErrorFollowupAsync("Please make sure you put in the right playlist name.").ConfigureAwait(false);
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
                            var plists7 = plists5.FirstOrDefault(x => string.Equals(x.Name, nmsg1, StringComparison.CurrentCultureIgnoreCase));
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
                                var uow = db.GetDbContext();
                                await using var _ = uow.ConfigureAwait(false);
                                uow.MusicPlaylists.Update(toupdate);
                                await uow.SaveChangesAsync().ConfigureAwait(false);
                                await msg.DeleteAsync().ConfigureAwait(false);
                                await ctx.Interaction.SendConfirmFollowupAsync($"Added {toadd.Count()} tracks to {plists7.Name}.").ConfigureAwait(false);
                            }
                            else
                            {
                                await ctx.Interaction.SendErrorFollowupAsync(
                                    "Please make sure you put in the right playlist name.").ConfigureAwait(false);
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
                                    components1.WithButton(count1++.ToString(), count1.ToString(), ButtonStyle.Primary,
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
                            var response = await GetButtonInputAsync(ctx.Interaction.Id, msg2.Id, ctx.User.Id).ConfigureAwait(false);
                            var track = advancedLavaTracks.ElementAt(int.Parse(response) - 2);
                            msg = await ctx.Interaction.SendConfirmFollowupAsync(
                                "Please type the name of the playlist you wanna save this to!").ConfigureAwait(false);
                            var nmsg = await NextMessageAsync(ctx.Interaction.Id, ctx.User.Id).ConfigureAwait(false);
                            var plists6 = plists5.FirstOrDefault(x => string.Equals(x.Name, nmsg, StringComparison.CurrentCultureIgnoreCase));
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
                                var uow = db.GetDbContext();
                                await using var _ = uow.ConfigureAwait(false);
                                uow.MusicPlaylists.Update(toupdate);
                                await uow.SaveChangesAsync().ConfigureAwait(false);
                                await msg.DeleteAsync().ConfigureAwait(false);
                                await ctx.Interaction.SendConfirmFollowupAsync($"Added {track.Title} to {plists6.Name}.").ConfigureAwait(false);
                            }
                            else
                            {
                                await ctx.Interaction.SendErrorFollowupAsync(
                                    "Please make sure you put in the right playlist name.").ConfigureAwait(false);
                            }

                            break;
                    }
                }

                break;
            case PlaylistAction.Default:
                var defaultplaylist = await Service.GetDefaultPlaylist(ctx.User);
                if (string.IsNullOrEmpty(playlistOrSongName) && defaultplaylist is not null)
                {
                    await ctx.Interaction.SendConfirmFollowupAsync($"Your current default playlist is {defaultplaylist.Name}").ConfigureAwait(false);
                    return;
                }

                if (string.IsNullOrEmpty(playlistOrSongName) && defaultplaylist is null)
                {
                    await ctx.Interaction.SendErrorFollowupAsync("You do not have a default playlist set.").ConfigureAwait(false);
                    return;
                }

                if (!string.IsNullOrEmpty(playlistOrSongName) && defaultplaylist is not null)
                {
                    var plist4 = Service.GetPlaylists(ctx.User)
                                        .FirstOrDefault(x => string.Equals(x.Name, playlistOrSongName, StringComparison.CurrentCultureIgnoreCase));
                    if (plist4 is null)
                    {
                        await ctx.Interaction.SendErrorFollowupAsync(
                            "Playlist by that name wasn't found. Please try another name!").ConfigureAwait(false);
                        return;
                    }

                    if (plist4.Name == defaultplaylist.Name)
                    {
                        await ctx.Interaction.SendErrorFollowupAsync("This is already your default playlist!").ConfigureAwait(false);
                        return;
                    }

                    if (await PromptUserConfirmAsync("Are you sure you want to switch default playlists?", ctx.User.Id).ConfigureAwait(false))
                    {
                        await Service.UpdateDefaultPlaylist(ctx.User, plist4).ConfigureAwait(false);
                        await ctx.Interaction.SendConfirmFollowupAsync("Default Playlist Updated.").ConfigureAwait(false);
                    }
                }

                if (!string.IsNullOrEmpty(playlistOrSongName) && defaultplaylist is null)
                {
                    var plist4 = Service.GetPlaylists(ctx.User)
                                        .FirstOrDefault(x => string.Equals(x.Name, playlistOrSongName, StringComparison.CurrentCultureIgnoreCase));
                    if (plist4 is null)
                    {
                        await ctx.Interaction.SendErrorFollowupAsync(
                            "Playlist by that name wasn't found. Please try another name!").ConfigureAwait(false);
                        return;
                    }

                    await Service.UpdateDefaultPlaylist(ctx.User, plist4).ConfigureAwait(false);
                    await ctx.Interaction.SendConfirmFollowupAsync("Default Playlist Set.").ConfigureAwait(false);
                }

                break;
        }
    }

    [SlashCommand("join", "Join your current voice channel."), RequireContext(ContextType.Guild), CheckPermissions]
    public async Task Join()
    {
        var currentUser = await ctx.Guild.GetUserAsync(Context.Client.CurrentUser.Id).ConfigureAwait(false);
        if (lavaNode.GetPlayer<MusicPlayer>(Context.Guild) != null && currentUser.VoiceChannel != null)
        {
            await ctx.Interaction.SendErrorAsync("I'm already connected to a voice channel!").ConfigureAwait(false);
            return;
        }

        var voiceState = Context.User as IVoiceState;
        if (voiceState?.VoiceChannel == null)
        {
            await ctx.Interaction.SendErrorAsync("You must be connected to a voice channel!").ConfigureAwait(false);
            return;
        }

        await lavaNode.JoinAsync(() => new MusicPlayer(client, Service, config), ctx.Guild.Id, voiceState.VoiceChannel.Id).ConfigureAwait(false);
        if (voiceState.VoiceChannel is IStageChannel chan)
        {
            try
            {
                await chan.BecomeSpeakerAsync().ConfigureAwait(false);
            }
            catch
            {//
            }
        }
        await ctx.Interaction.SendConfirmAsync($"Joined {voiceState.VoiceChannel.Name}!").ConfigureAwait(false);
    }

    [SlashCommand("leave", "Leave your current voice channel"), RequireContext(ContextType.Guild), CheckPermissions]
    public async Task Leave()
    {
        var player = lavaNode.GetPlayer<MusicPlayer>(ctx.Guild.Id);
        if (player == null)
        {
            await ctx.Interaction.SendErrorAsync("I'm not connected to any voice channels!").ConfigureAwait(false);
            return;
        }

        var voiceChannel = (Context.User as IVoiceState)?.VoiceChannel.Id ?? player.VoiceChannelId;
        if (voiceChannel == null)
        {
            await ctx.Interaction.SendErrorAsync("Not sure which voice channel to disconnect from.").ConfigureAwait(false);
            return;
        }

        await player.StopAsync(true).ConfigureAwait(false);
        await ctx.Interaction.SendConfirmAsync("I've left the channel and cleared the queue!").ConfigureAwait(false);
        await Service.QueueClear(ctx.Guild.Id).ConfigureAwait(false);
    }

    [SlashCommand("queueplay", "Plays a song from a number in queue"), RequireContext(ContextType.Guild), CheckPermissions]
    public async Task Play(int number)
    {
        var player = lavaNode.GetPlayer<MusicPlayer>(ctx.Guild.Id);
        var queue = Service.GetQueue(ctx.Guild.Id);
        if (player is null)
        {
            var vc = ctx.User as IVoiceState;
            if (vc?.VoiceChannel is null)
            {
                await ctx.Interaction.SendErrorAsync("Looks like both you and the bot are not in a voice channel.").ConfigureAwait(false);
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
                     .WithFooter($"Track {queue.IndexOf(track) + 1} | {track.Duration:hh\\:mm\\:ss} | {((AdvancedTrackContext)track.Context).QueueUser}")
                     .WithThumbnailUrl(e.AbsoluteUri)
                     .WithOkColor();
            await ctx.Interaction.RespondAsync(embed: eb.Build()).ConfigureAwait(false);
        }
        else
        {
            await Play($"{number}").ConfigureAwait(false);
        }
    }

    [SlashCommand("play", "play a song using the name or a link"), RequireContext(ContextType.Guild), CheckPermissions]
    // ReSharper disable once MemberCanBePrivate.Global
    public async Task Play(string? searchQuery)
    {
        await ctx.Interaction.DeferAsync().ConfigureAwait(false);
        int count;
        if (!lavaNode.HasPlayer(Context.Guild))
        {
            var vc = ctx.User as IVoiceState;
            if (vc?.VoiceChannel is null)
            {
                await ctx.Interaction.SendErrorAsync("Looks like both you and the bot are not in a voice channel.").ConfigureAwait(false);
                return;
            }

            try
            {
                await lavaNode.JoinAsync(() => new MusicPlayer(client, Service, config), ctx.Guild.Id, vc.VoiceChannel.Id).ConfigureAwait(false);
                if (vc.VoiceChannel is SocketStageChannel chan)
                {
                    try
                    {
                        await chan.BecomeSpeakerAsync().ConfigureAwait(false);
                    }
                    catch
                    {
                        await ctx.Interaction.SendErrorAsync(
                            "I tried to join as a speaker but I'm unable to! Please drag me to the channel manually.").ConfigureAwait(false);
                    }
                }
            }
            catch
            {
                await ctx.Interaction.SendErrorAsync("Seems I'm unable to join the channel! Check permissions!").ConfigureAwait(false);
                return;
            }
        }

        if ((client.CurrentUser.Id == 752236274261426212 && searchQuery.Contains("youtube.com")) || (client.CurrentUser.Id == 752236274261426212 && searchQuery.Contains("youtu.be")))
        {
            var eb = new EmbedBuilder().WithErrorColor()
                                       .WithTitle("YouTube support on Public Mewdeko has been disabled.")
                                       .WithDescription(Format.Bold("YouTube support has been disabled due to unfair unverification from Discord that is targetting smaller bots that use YouTube for music.\n\n This does not mean Mewdeko is going premium. You have options below."))
                                       .AddField("Donate for a Selfhost", Format.Bold("https://ko-fi.com/mewdeko"))
                                       .AddField("Host on yourself", Format.Bold("https://github.com/Pusheon/Mewdeko"))
                                       .AddField("More Info", Format.Bold("https://youtu.be/fOpEdS3JVYQ"))
                                       .AddField("Support Server", Format.Bold(config.Data.SupportServer))
                                       .Build();
            await ctx.Interaction.FollowupAsync(embed: eb);
            return;
        }
        var player = lavaNode.GetPlayer<MusicPlayer>(ctx.Guild);
        if (!Uri.IsWellFormedUriString(searchQuery, UriKind.RelativeOrAbsolute)
             || searchQuery.Contains("youtube.com") || searchQuery.Contains("youtu.be") || searchQuery.Contains("soundcloud.com") || searchQuery.Contains("soundcloud.com") || searchQuery.Contains("twitch.tv") || searchQuery.CheckIfMusicUrl())
        {
                if (player is null)
                {
                    await Service.ModifySettingsInternalAsync(ctx.Guild.Id,
                        (settings, _) => settings.MusicChannelId = ctx.Interaction.Id, ctx.Interaction.Id).ConfigureAwait(false);
                }

                var searchResponse = await lavaNode.LoadTracksAsync(searchQuery, client.CurrentUser.Id == 752236274261426212 ? SearchMode.SoundCloud : SearchMode.None).ConfigureAwait(false);
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
                        components: config.Data.ShowInviteButton ? new ComponentBuilder()
                                                                    .WithButton(style: ButtonStyle.Link,
                                                                        url: "https://discord.com/oauth2/authorize?client_id=752236274261426212&permissions=8&response_type=code&redirect_uri=https%3A%2F%2Fmewdeko.tech&scope=bot%20applications.commands",
                                                                        label: "Invite Me!",
                                                                        emote: "<a:HaneMeow:968564817784877066>".ToIEmote()).Build() : null).ConfigureAwait(false);
                    if (player.State != PlayerState.Playing)
                        await player.PlayAsync(searchResponse.Tracks.FirstOrDefault()).ConfigureAwait(false);
                    await player.SetVolumeAsync(await Service.GetVolume(ctx.Guild.Id).ConfigureAwait(false) / 100.0F).ConfigureAwait(false);
                    return;
                }
                else
                {
                    var artworkService = new ArtworkService();
                    var art = new Uri(null);
                    try
                    {
                        art = await artworkService.ResolveAsync(searchResponse.Tracks.FirstOrDefault()).ConfigureAwait(false);
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
                        components: config.Data.ShowInviteButton ? new ComponentBuilder()
                                                                    .WithButton(style: ButtonStyle.Link,
                                                                        url: "https://discord.com/oauth2/authorize?client_id=752236274261426212&permissions=8&response_type=code&redirect_uri=https%3A%2F%2Fmewdeko.tech&scope=bot%20applications.commands",
                                                                        label: "Invite Me!",
                                                                        emote: "<a:HaneMeow:968564817784877066>".ToIEmote()).Build() : null).ConfigureAwait(false);
                    if (player.State != PlayerState.Playing)
                        await player.PlayAsync(searchResponse.Tracks.FirstOrDefault()).ConfigureAwait(false);
                    await player.SetVolumeAsync(await Service.GetVolume(ctx.Guild.Id).ConfigureAwait(false) / 100.0F).ConfigureAwait(false);
                    return;
                }
        }

        if (searchQuery.Contains("spotify"))
        {
            await Service.SpotifyQueue(ctx.Guild, ctx.User, ctx.Channel as ITextChannel, player, searchQuery).ConfigureAwait(false);
            return;
        }

        var searchResponse2 = await lavaNode.GetTracksAsync(searchQuery, client.CurrentUser.Id == 752236274261426212 ? SearchMode.SoundCloud : SearchMode.YouTube).ConfigureAwait(false);
        if (!searchResponse2.Any())
        {
            await ctx.Interaction.SendErrorAsync("Seems like I can't find that video, please try again.").ConfigureAwait(false);
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
        var msg = await ctx.Interaction.FollowupAsync(embed: eb12.Build(), components: components.Build()).ConfigureAwait(false);
        var button = await GetButtonInputAsync(ctx.Channel.Id, msg.Id, ctx.User.Id).ConfigureAwait(false);
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
                    await player.SetVolumeAsync(await Service.GetVolume(ctx.Guild.Id).ConfigureAwait(false) / 100.0F).ConfigureAwait(false);
                    await Service.ModifySettingsInternalAsync(ctx.Guild.Id,
                        (settings, _) => settings.MusicChannelId = ctx.Interaction.Id, ctx.Interaction.Id).ConfigureAwait(false);
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
                    await player.SetVolumeAsync(await Service.GetVolume(ctx.Guild.Id).ConfigureAwait(false) / 100.0F).ConfigureAwait(false);
                    await Service.ModifySettingsInternalAsync(ctx.Guild.Id,
                        (settings, _) => settings.MusicChannelId = ctx.Interaction.Id, ctx.Interaction.Id).ConfigureAwait(false);
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
                    await player.SetVolumeAsync(await Service.GetVolume(ctx.Guild.Id).ConfigureAwait(false) / 100.0F).ConfigureAwait(false);
                    await Service.ModifySettingsInternalAsync(ctx.Guild.Id,
                        (settings, _) => settings.MusicChannelId = ctx.Interaction.Id, ctx.Interaction.Id).ConfigureAwait(false);
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

    [SlashCommand("pause", "Pauses the current track"), RequireContext(ContextType.Guild), CheckPermissions]
    public async Task Pause()
    {
        var player = lavaNode.GetPlayer<MusicPlayer>(ctx.Guild.Id);
        if (player is null)
        {
            await ctx.Interaction.SendErrorAsync("I'm not connected to a voice channel.").ConfigureAwait(false);
            return;
        }

        if (player.State != PlayerState.Playing)
        {
            await player.ResumeAsync().ConfigureAwait(false);
            await ctx.Interaction.SendConfirmAsync("Resumed player.").ConfigureAwait(false);
            return;
        }

        await player.PauseAsync().ConfigureAwait(false);
        await ctx.Interaction.SendConfirmAsync($"Paused player. Do {await guildSettings.GetPrefix(ctx.Guild.Id)}pause again to resume.").ConfigureAwait(false);
    }

    [SlashCommand("shuffle", "Shuffles the current queue"), RequireContext(ContextType.Guild), CheckPermissions]
    public async Task Shuffle()
    {
        var player = lavaNode.GetPlayer<MusicPlayer>(ctx.Guild.Id);
        if (player is null)
        {
            await ctx.Interaction.SendErrorAsync("I'm not even playing anything.").ConfigureAwait(false);
            return;
        }

        if (Service.GetQueue(ctx.Guild.Id).Count == 0)
        {
            await ctx.Interaction.SendErrorAsync("There's nothing in queue.").ConfigureAwait(false);
            return;
        }

        if (Service.GetQueue(ctx.Guild.Id).Count == 1)
        {
            await ctx.Interaction.SendErrorAsync("... There's literally only one thing in queue.").ConfigureAwait(false);
            return;
        }

        Service.Shuffle(ctx.Guild);
        await ctx.Interaction.SendConfirmAsync("Successfully shuffled the queue!").ConfigureAwait(false);
    }

    [SlashCommand("stop", "Stops the player and clears all current songs"), RequireContext(ContextType.Guild), CheckPermissions]
    public async Task Stop()
    {
        var player = lavaNode.GetPlayer<MusicPlayer>(ctx.Guild.Id);
        if (player is null)
        {
            await ctx.Interaction.SendErrorAsync("I'm not connected to a channel!").ConfigureAwait(false);
            return;
        }

        await player.StopAsync().ConfigureAwait(false);
        await Service.QueueClear(ctx.Guild.Id).ConfigureAwait(false);
        await ctx.Interaction.SendConfirmAsync("Stopped the player and cleared the queue!").ConfigureAwait(false);
    }

    [SlashCommand("skip", "Skip to the next song, if there is one"), RequireContext(ContextType.Guild), CheckPermissions]
    public async Task Skip(int num = 1)
    {
        if (num < 1)
        {
            await ctx.Interaction.SendErrorAsync("You can only skip ahead.").ConfigureAwait(false);
            return;
        }
        var player = lavaNode.GetPlayer<MusicPlayer>(ctx.Guild.Id);
        if (player is null)
        {
            await ctx.Interaction.SendErrorAsync("I'm not connected to a voice channel.").ConfigureAwait(false);
            return;
        }

        await Service.Skip(ctx.Guild, ctx.Channel as ITextChannel, player, ctx, num).ConfigureAwait(false);
    }

    [SlashCommand("seek", "Seek to a certain time in the current song"), RequireContext(ContextType.Guild), CheckPermissions]
    public async Task Seek(string input)
    {
        StoopidTime time;
        try
        {
            time = StoopidTime.FromInput(input);
        }
        catch
        {
            await ctx.Interaction.SendErrorAsync("Invalid time.").ConfigureAwait(false);
            return;
        }
        var player = lavaNode.GetPlayer<MusicPlayer>(ctx.Guild.Id);
        if (player is null)
        {
            await ctx.Interaction.SendErrorAsync("I'm not connected to a voice channel.").ConfigureAwait(false);
            return;
        }

        if (player.State != PlayerState.Playing)
        {
            await ctx.Interaction.SendErrorAsync("Woaaah there, I can't seek when nothing is playing.").ConfigureAwait(false);
            return;
        }

        if (time.Time > player.CurrentTrack.Duration)
            await ctx.Channel.SendErrorAsync("That's longer than the song lol, try again.").ConfigureAwait(false);
        await player.SeekPositionAsync(time.Time).ConfigureAwait(false);
        await ctx.Channel.SendConfirmAsync($"I've seeked `{player.CurrentTrack.Title}` to {time.Time}.").ConfigureAwait(false);
    }

    [SlashCommand("clearqueue", "Clears the current queue"), RequireContext(ContextType.Guild), CheckPermissions]
    public async Task ClearQueue()
    {
        var player = lavaNode.GetPlayer<MusicPlayer>(ctx.Guild.Id);
        if (player is null)
        {
            await ctx.Interaction.SendErrorAsync("I'm not connected to a voice channel.").ConfigureAwait(false);
            return;
        }

        await player.StopAsync().ConfigureAwait(false);
        await Service.QueueClear(ctx.Guild.Id).ConfigureAwait(false);
        await ctx.Interaction.SendConfirmAsync("Cleared the queue!").ConfigureAwait(false);
    }

    [SlashCommand("channel", "Set the channel where music events go"), RequireContext(ContextType.Guild),
     CheckPermissions]
    public async Task SetMusicChannel()
    {
        var user = await ctx.Guild.GetUserAsync(ctx.Client.CurrentUser.Id).ConfigureAwait(false);
        if (!user.GetPermissions(ctx.Channel as ITextChannel).EmbedLinks)
            return;
        await Service.ModifySettingsInternalAsync(ctx.Guild.Id, (settings, _) => settings.MusicChannelId = ctx.Interaction.Id, ctx.Interaction.Id).ConfigureAwait(false);
        await ctx.Interaction.SendConfirmAsync("Set this channel to recieve music events.").ConfigureAwait(false);
    }

    [SlashCommand("autoplay", "Set the amount of songs to add at the end of the queue."), RequireContext(ContextType.Guild), CheckPermissions]
    public async Task AutoPlay(int autoPlayNum)
    {
        await Service.ModifySettingsInternalAsync(ctx.Guild.Id, (settings, _) => settings.AutoPlay = autoPlayNum, autoPlayNum).ConfigureAwait(false);
        switch (autoPlayNum)
        {
            case > 0 and < 6:
                await ctx.Interaction.SendConfirmAsync($"When the last song is reached autoplay will attempt to add `{autoPlayNum}` songs to the queue.");
                break;
            case > 5:
                await ctx.Interaction.SendErrorAsync("I can only do so much. Keep it to a maximum of 5 please.");
                break;
            case 0:
                await ctx.Interaction.SendConfirmAsync("Autoplay has been disabled.");
                break;
        }
    }

    [SlashCommand("loop", "Sets the loop type"), RequireContext(ContextType.Guild), CheckPermissions]
    public async Task Loop(PlayerRepeatType reptype = PlayerRepeatType.None)
    {
        await Service.ModifySettingsInternalAsync(ctx.Guild.Id, (settings, _) => settings.PlayerRepeat = reptype,
            reptype).ConfigureAwait(false);
        await ctx.Interaction.SendConfirmAsync($"Loop has now been set to {reptype}").ConfigureAwait(false);
    }

    [SlashCommand("volume", "Sets the current volume"), RequireContext(ContextType.Guild), CheckPermissions]
    public async Task Volume(int volume)
    {
        var player = lavaNode.GetPlayer<MusicPlayer>(ctx.Guild.Id);
        if (player is null)
        {
            await ctx.Interaction.SendErrorAsync("I'm not connected to a voice channel.").ConfigureAwait(false);
            return;
        }

        if (volume > 100)
        {
            await ctx.Interaction.SendErrorAsync("Max is 100 m8").ConfigureAwait(false);
            return;
        }

        await player.SetVolumeAsync(volume / 100.0F).ConfigureAwait(false);
        await Service.ModifySettingsInternalAsync(ctx.Guild.Id, (settings, _) => settings.Volume = volume, volume).ConfigureAwait(false);
        await ctx.Interaction.SendConfirmAsync($"Set the volume to {volume}").ConfigureAwait(false);
    }

    [SlashCommand("nowplaying", "Shows the currently playing song"), RequireContext(ContextType.Guild), CheckPermissions]
    public async Task NowPlaying()
    {
        var player = lavaNode.GetPlayer<MusicPlayer>(ctx.Guild.Id);
        if (player is null)
        {
            await ctx.Interaction.SendErrorAsync("I'm not connected to a voice channel.").ConfigureAwait(false);
            return;
        }

        if (player.State != PlayerState.Playing)
        {
            await ctx.Interaction.SendErrorAsync("Woaaah there, I'm not playing any tracks.").ConfigureAwait(false);
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

    [SlashCommand("queue", "Lists all songs"), RequireContext(ContextType.Guild), CheckPermissions]
    public async Task Queue()
    {
        var player = lavaNode.GetPlayer<MusicPlayer>(ctx.Guild.Id);
        if (player is null)
        {
            await ctx.Interaction.SendErrorAsync("I am not playing anything at the moment!").ConfigureAwait(false);
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

        await interactivity.SendPaginatorAsync(paginator, ctx.Interaction as SocketInteraction, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

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