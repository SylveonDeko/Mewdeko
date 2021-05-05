using Discord;
using Discord.Commands;
using Microsoft.EntityFrameworkCore;
using NadekoBot.Extensions;
using NadekoBot.Core.Services;
using System.Linq;
using System.Threading.Tasks;
using NadekoBot.Common.Attributes;
using NadekoBot.Common.Collections;
using NadekoBot.Modules.Permissions.Services;
using NadekoBot.Core.Services.Database.Models;

namespace NadekoBot.Modules.Permissions
{
    public partial class Permissions
    {
        [Group]
        public class FilterCommands : NadekoSubmodule<FilterService>
        {
            private readonly DbService _db;

            public FilterCommands(DbService db)
            {
                _db = db;
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPerm.Administrator)]
            public async Task FwClear()
            {
                _service.ClearFilteredWords(ctx.Guild.Id);
                await ReplyConfirmLocalizedAsync("fw_cleared").ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task SrvrFilterInv()
            {
                var channel = (ITextChannel)ctx.Channel;

                bool enabled;
                using (var uow = _db.GetDbContext())
                {
                    var config = uow.GuildConfigs.ForId(channel.Guild.Id, set => set);
                    enabled = config.FilterInvites = !config.FilterInvites;
                    await uow.SaveChangesAsync();
                }

                if (enabled)
                {
                    _service.InviteFilteringServers.Add(channel.Guild.Id);
                    await ReplyConfirmLocalizedAsync("invite_filter_server_on").ConfigureAwait(false);
                }
                else
                {
                    _service.InviteFilteringServers.TryRemove(channel.Guild.Id);
                    await ReplyConfirmLocalizedAsync("invite_filter_server_off").ConfigureAwait(false);
                }
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task ChnlFilterInv()
            {
                var channel = (ITextChannel)ctx.Channel;

                FilterChannelId removed;
                using (var uow = _db.GetDbContext())
                {
                    var config = uow.GuildConfigs.ForId(channel.Guild.Id, set => set.Include(gc => gc.FilterInvitesChannelIds));
                    var match = new FilterChannelId()
                    {
                        ChannelId = channel.Id
                    };
                    removed = config.FilterInvitesChannelIds.FirstOrDefault(fc => fc.Equals(match));

                    if (removed == null)
                    {
                        config.FilterInvitesChannelIds.Add(match);
                    }
                    else
                    {
                        uow._context.Remove(removed);
                    }
                    await uow.SaveChangesAsync();
                }

                if (removed == null)
                {
                    _service.InviteFilteringChannels.Add(channel.Id);
                    await ReplyConfirmLocalizedAsync("invite_filter_channel_on").ConfigureAwait(false);
                }
                else
                {
                    _service.InviteFilteringChannels.TryRemove(channel.Id);
                    await ReplyConfirmLocalizedAsync("invite_filter_channel_off").ConfigureAwait(false);
                }
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task SrvrFilterLin()
            {
                var channel = (ITextChannel)ctx.Channel;

                bool enabled;
                using (var uow = _db.GetDbContext())
                {
                    var config = uow.GuildConfigs.ForId(channel.Guild.Id, set => set);
                    enabled = config.FilterLinks = !config.FilterLinks;
                    await uow.SaveChangesAsync();
                }

                if (enabled)
                {
                    _service.LinkFilteringServers.Add(channel.Guild.Id);
                    await ReplyConfirmLocalizedAsync("link_filter_server_on").ConfigureAwait(false);
                }
                else
                {
                    _service.LinkFilteringServers.TryRemove(channel.Guild.Id);
                    await ReplyConfirmLocalizedAsync("link_filter_server_off").ConfigureAwait(false);
                }
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task ChnlFilterLin()
            {
                var channel = (ITextChannel)ctx.Channel;

                FilterLinksChannelId removed;
                using (var uow = _db.GetDbContext())
                {
                    var config = uow.GuildConfigs.ForId(channel.Guild.Id, set => set.Include(gc => gc.FilterLinksChannelIds));
                    var match = new FilterLinksChannelId()
                    {
                        ChannelId = channel.Id
                    };
                    removed = config.FilterLinksChannelIds.FirstOrDefault(fc => fc.Equals(match));

                    if (removed == null)
                    {
                        config.FilterLinksChannelIds.Add(match);
                    }
                    else
                    {
                        uow._context.Remove(removed);
                    }
                    await uow.SaveChangesAsync();
                }

                if (removed == null)
                {
                    _service.LinkFilteringChannels.Add(channel.Id);
                    await ReplyConfirmLocalizedAsync("link_filter_channel_on").ConfigureAwait(false);
                }
                else
                {
                    _service.LinkFilteringChannels.TryRemove(channel.Id);
                    await ReplyConfirmLocalizedAsync("link_filter_channel_off").ConfigureAwait(false);
                }
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task SrvrFilterWords()
            {
                var channel = (ITextChannel)ctx.Channel;

                bool enabled;
                using (var uow = _db.GetDbContext())
                {
                    var config = uow.GuildConfigs.ForId(channel.Guild.Id, set => set);
                    enabled = config.FilterWords = !config.FilterWords;
                    await uow.SaveChangesAsync();
                }

                if (enabled)
                {
                    _service.WordFilteringServers.Add(channel.Guild.Id);
                    await ReplyConfirmLocalizedAsync("word_filter_server_on").ConfigureAwait(false);
                }
                else
                {
                    _service.WordFilteringServers.TryRemove(channel.Guild.Id);
                    await ReplyConfirmLocalizedAsync("word_filter_server_off").ConfigureAwait(false);
                }
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task ChnlFilterWords()
            {
                var channel = (ITextChannel)ctx.Channel;

                FilterChannelId removed;
                using (var uow = _db.GetDbContext())
                {
                    var config = uow.GuildConfigs.ForId(channel.Guild.Id, set => set.Include(gc => gc.FilterWordsChannelIds));

                    var match = new FilterChannelId()
                    {
                        ChannelId = channel.Id
                    };
                    removed = config.FilterWordsChannelIds.FirstOrDefault(fc => fc.Equals(match));
                    if (removed == null)
                    {
                        config.FilterWordsChannelIds.Add(match);
                    }
                    else
                    {
                        uow._context.Remove(removed);
                    }
                    await uow.SaveChangesAsync();
                }

                if (removed == null)
                {
                    _service.WordFilteringChannels.Add(channel.Id);
                    await ReplyConfirmLocalizedAsync("word_filter_channel_on").ConfigureAwait(false);
                }
                else
                {
                    _service.WordFilteringChannels.TryRemove(channel.Id);
                    await ReplyConfirmLocalizedAsync("word_filter_channel_off").ConfigureAwait(false);
                }
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task FilterWord([Leftover] string word)
            {
                var channel = (ITextChannel)ctx.Channel;

                word = word?.Trim().ToLowerInvariant();

                if (string.IsNullOrWhiteSpace(word))
                    return;

                FilteredWord removed;
                using (var uow = _db.GetDbContext())
                {
                    var config = uow.GuildConfigs.ForId(channel.Guild.Id, set => set.Include(gc => gc.FilteredWords));

                    removed = config.FilteredWords.FirstOrDefault(fw => fw.Word.Trim().ToLowerInvariant() == word);

                    if (removed == null)
                        config.FilteredWords.Add(new FilteredWord() { Word = word });
                    else
                    {
                        uow._context.Remove(removed);
                    }

                    await uow.SaveChangesAsync();
                }

                var filteredWords = _service.ServerFilteredWords.GetOrAdd(channel.Guild.Id, new ConcurrentHashSet<string>());

                if (removed == null)
                {
                    filteredWords.Add(word);
                    await ReplyConfirmLocalizedAsync("filter_word_add", Format.Code(word)).ConfigureAwait(false);
                }
                else
                {
                    filteredWords.TryRemove(word);
                    await ReplyConfirmLocalizedAsync("filter_word_remove", Format.Code(word)).ConfigureAwait(false);
                }
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task LstFilterWords(int page = 1)
            {
                page--;
                if (page < 0)
                    return;

                var channel = (ITextChannel)ctx.Channel;

                _service.ServerFilteredWords.TryGetValue(channel.Guild.Id, out var fwHash);

                var fws = fwHash.ToArray();

                await ctx.SendPaginatedConfirmAsync(page,
                    (curPage) => new EmbedBuilder()
                        .WithTitle(GetText("filter_word_list"))
                        .WithDescription(string.Join("\n", fws.Skip(curPage * 10).Take(10)))
                        .WithOkColor()
                , fws.Length, 10).ConfigureAwait(false);
            }
        }
    }
}
