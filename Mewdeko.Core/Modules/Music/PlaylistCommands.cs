using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Mewdeko.Common.Attributes;
using Mewdeko.Core.Services;
using Mewdeko.Core.Services.Database.Models;
using Mewdeko.Extensions;
using Mewdeko.Modules;
using Mewdeko.Modules.Music.Services;
using Serilog;

namespace Mewdeko.Core.Modules.Music
{
    public sealed partial class Music
    {
        [Group]
        public sealed class PlaylistCommands : MewdekoModule<IMusicService>
        {
            private static readonly SemaphoreSlim _playlistLock = new(1, 1);
            private readonly IBotCredentials _creds;
            private readonly DbService _db;

            public PlaylistCommands(DbService db, IBotCredentials creds)
            {
                _db = db;
                _creds = creds;
            }

            private async Task EnsureBotInVoiceChannelAsync(ulong voiceChannelId, IGuildUser botUser = null)
            {
                botUser ??= await ctx.Guild.GetCurrentUserAsync();
                await voiceChannelLock.WaitAsync();
                try
                {
                    if (botUser.VoiceChannel?.Id is null || !_service.TryGetMusicPlayer(Context.Guild.Id, out _))
                        await _service.JoinVoiceChannelAsync(ctx.Guild.Id, voiceChannelId);
                }
                finally
                {
                    voiceChannelLock.Release();
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task Playlists([Leftover] int num = 1)
            {
                if (num <= 0)
                    return;

                List<MusicPlaylist> playlists;

                using (var uow = _db.GetDbContext())
                {
                    playlists = uow.MusicPlaylists.GetPlaylistsOnPage(num);
                }

                var embed = new EmbedBuilder()
                    .WithAuthor(eab => eab.WithName(GetText("playlists_page", num)).WithMusicIcon())
                    .WithDescription(string.Join("\n", playlists.Select(r =>
                        GetText("playlists", r.Id, r.Name, r.Author, r.Songs.Count))))
                    .WithOkColor();
                await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task DeletePlaylist([Leftover] int id)
            {
                var success = false;
                try
                {
                    using (var uow = _db.GetDbContext())
                    {
                        var pl = uow.MusicPlaylists.GetById(id);

                        if (pl != null)
                            if (_creds.IsOwner(ctx.User) || pl.AuthorId == ctx.User.Id)
                            {
                                uow.MusicPlaylists.Remove(pl);
                                await uow.SaveChangesAsync();
                                success = true;
                            }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Error deleting playlist");
                }

                if (!success)
                    await ReplyErrorLocalizedAsync("playlist_delete_fail").ConfigureAwait(false);
                else
                    await ReplyConfirmLocalizedAsync("playlist_deleted").ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task PlaylistShow(int id, int page = 1)
            {
                if (page-- < 1)
                    return;

                MusicPlaylist mpl;
                using (var uow = _db.GetDbContext())
                {
                    mpl = uow.MusicPlaylists.GetWithSongs(id);
                }

                await ctx.SendPaginatedConfirmAsync(page, cur =>
                {
                    var i = 0;
                    var str = string.Join("\n", mpl.Songs
                        .Skip(cur * 20)
                        .Take(20)
                        .Select(x => $"`{++i}.` [{x.Title.TrimTo(45)}]({x.Query}) `{x.Provider}`"));
                    return new EmbedBuilder()
                        .WithTitle($"\"{mpl.Name}\" by {mpl.Author}")
                        .WithOkColor()
                        .WithDescription(str);
                }, mpl.Songs.Count, 20).ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task Save([Leftover] string name)
            {
                if (!_service.TryGetMusicPlayer(ctx.Guild.Id, out var mp))
                {
                    await ReplyErrorLocalizedAsync("no_player");
                    return;
                }

                var songs = mp.GetQueuedTracks()
                    .Select(s => new PlaylistSong
                    {
                        Provider = s.Platform.ToString(),
                        ProviderType = (MusicType) s.Platform,
                        Title = s.Title,
                        Query = s.Url
                    }).ToList();

                MusicPlaylist playlist;
                using (var uow = _db.GetDbContext())
                {
                    playlist = new MusicPlaylist
                    {
                        Name = name,
                        Author = ctx.User.Username,
                        AuthorId = ctx.User.Id,
                        Songs = songs.ToList()
                    };
                    uow.MusicPlaylists.Add(playlist);
                    await uow.SaveChangesAsync();
                }

                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                        .WithTitle(GetText("playlist_saved"))
                        .AddField(efb => efb.WithName(GetText("name")).WithValue(name))
                        .AddField(efb => efb.WithName(GetText("id")).WithValue(playlist.Id.ToString())))
                    .ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task Load([Leftover] int id)
            {
                // expensive action, 1 at a time
                await _playlistLock.WaitAsync();
                try
                {
                    var user = (IGuildUser) Context.User;
                    var voiceChannelId = user.VoiceChannel?.Id;

                    if (voiceChannelId is null)
                    {
                        await ReplyErrorLocalizedAsync("must_be_in_voice");
                        return;
                    }

                    _ = ctx.Channel.TriggerTypingAsync();

                    var botUser = await ctx.Guild.GetCurrentUserAsync();
                    await EnsureBotInVoiceChannelAsync(voiceChannelId!.Value, botUser);

                    if (botUser.VoiceChannel?.Id != voiceChannelId)
                    {
                        await ReplyErrorLocalizedAsync("not_with_bot_in_voice");
                        return;
                    }

                    var mp = await _service.GetOrCreateMusicPlayerAsync((ITextChannel) Context.Channel);
                    if (mp is null)
                    {
                        await ReplyErrorLocalizedAsync("no_player");
                        return;
                    }

                    MusicPlaylist mpl;
                    using (var uow = _db.GetDbContext())
                    {
                        mpl = uow.MusicPlaylists.GetWithSongs(id);
                    }

                    if (mpl == null)
                    {
                        await ReplyErrorLocalizedAsync("playlist_id_not_found").ConfigureAwait(false);
                        return;
                    }

                    IUserMessage msg = null;
                    try
                    {
                        msg = await ctx.Channel
                            .SendMessageAsync(GetText("attempting_to_queue", Format.Bold(mpl.Songs.Count.ToString())))
                            .ConfigureAwait(false);
                    }
                    catch (Exception)
                    {
                    }

                    await mp.EnqueueManyAsync(
                        mpl.Songs.Select(x => (x.Query, (MusicPlatform) x.ProviderType)),
                        ctx.User.ToString()
                    );

                    if (msg != null) await msg.ModifyAsync(m => m.Content = GetText("playlist_queue_complete"));
                }
                finally
                {
                    _playlistLock.Release();
                }
            }
        }
    }
}