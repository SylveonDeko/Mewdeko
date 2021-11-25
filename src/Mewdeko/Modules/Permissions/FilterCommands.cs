using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Mewdeko._Extensions;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using Mewdeko.Common.Collections;
using Mewdeko.Common.Extensions.Interactive;
using Mewdeko.Common.Extensions.Interactive.Entities.Page;
using Mewdeko.Common.Extensions.Interactive.Pagination;
using Mewdeko.Common.Extensions.Interactive.Pagination.Lazy;
using Mewdeko.Services;
using Mewdeko.Services.Database.Models;
using Mewdeko.Modules.Permissions.Services;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Modules.Permissions
{
    public partial class Permissions
    {
        [Group]
        public class FilterCommands : MewdekoSubmodule<FilterService>
        {
            private readonly DbService _db;
            private readonly InteractiveService Interactivity;

            public FilterCommands(DbService db, InteractiveService serv)
            {
                Interactivity = serv;
                _db = db;
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [UserPerm(GuildPermission.Administrator)]
            [RequireContext(ContextType.Guild)]
            public async Task AutoBanWord(string word)
            {
                if (Service._blacklist.Count(x => x.Word == word && x.GuildId == ctx.Guild.Id) == 1)
                {
                    Service.UnBlacklist(word, ctx.Guild.Id);
                    await ctx.Channel.SendConfirmAsync($"Removed {Format.Code(word)} from the auto bans word list!");
                }
                else
                {
                    Service.WordBlacklist(word, ctx.Guild.Id);
                    await ctx.Channel.SendConfirmAsync($"Added {Format.Code(word)} to the auto ban words list!");
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [UserPerm(GuildPermission.Administrator)]
            [RequireContext(ContextType.Guild)]
            public async Task AutoBanWordList(int page = 0)
            {
                var words = Service._blacklist.Where(x => x.GuildId == ctx.Guild.Id);
                if (!words.Any())
                {
                    await ctx.Channel.SendErrorAsync("No AutoBanWords set.");
                }
                else
                {
                    var paginator = new LazyPaginatorBuilder()
                        .AddUser(ctx.User)
                        .WithPageFactory(PageFactory)
                        .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                        .WithMaxPageIndex(words.Count() / 10)
                        .WithDefaultEmotes()
                        .Build();

                    await Interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60));

                    Task<PageBuilder> PageFactory(int page)
                    {
                        return Task.FromResult(new PageBuilder()
                            .WithTitle("AutoBanWords")
                            .WithDescription(string.Join("\n", words.Select(x => x.Word).Skip(page * 10).Take(10)))
                            .WithOkColor());
                    }
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [UserPerm(GuildPermission.Administrator)]
            [RequireContext(ContextType.Guild)]
            public async Task FWarn(string yesnt)
            {
                await Service.fwarn(ctx.Guild, yesnt.Substring(0, 1).ToLower());
                var t = Service.GetFW(ctx.Guild.Id);
                switch (t)
                {
                    case 1:
                        await ctx.Channel.SendConfirmAsync("Warn on filtered word is now enabled!");
                        break;
                    case 0:
                        await ctx.Channel.SendConfirmAsync("Warn on filtered word is now disabled!");
                        break;
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [UserPerm(GuildPermission.Administrator)]
            [RequireContext(ContextType.Guild)]
            public async Task InvWarn(string yesnt)
            {
                await Service.InvWarn(ctx.Guild, yesnt.Substring(0, 1).ToLower());
                var t = Service.GetInvWarn(ctx.Guild.Id);
                switch (t)
                {
                    case 1:
                        await ctx.Channel.SendConfirmAsync("Warn on invite post is now enabled!");
                        break;
                    case 0:
                        await ctx.Channel.SendConfirmAsync("Warn on invite post is now disabled!");
                        break;
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            [UserPerm(GuildPermission.Administrator)]
            public async Task FwClear()
            {
                Service.ClearFilteredWords(ctx.Guild.Id);
                await ReplyConfirmLocalizedAsync("fw_cleared").ConfigureAwait(false);
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
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
                    Service.InviteFilteringServers.Add(channel.Guild.Id);
                    await ReplyConfirmLocalizedAsync("invite_filter_server_on").ConfigureAwait(false);
                }
                else
                {
                    Service.InviteFilteringServers.TryRemove(channel.Guild.Id);
                    await ReplyConfirmLocalizedAsync("invite_filter_server_off").ConfigureAwait(false);
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task ChnlFilterInv()
            {
                var channel = (ITextChannel)ctx.Channel;

                FilterChannelId removed;
                using (var uow = _db.GetDbContext())
                {
                    var config = uow.GuildConfigs.ForId(channel.Guild.Id,
                        set => set.Include(gc => gc.FilterInvitesChannelIds));
                    var match = new FilterChannelId
                    {
                        ChannelId = channel.Id
                    };
                    removed = config.FilterInvitesChannelIds.FirstOrDefault(fc => fc.Equals(match));

                    if (removed == null)
                        config.FilterInvitesChannelIds.Add(match);
                    else
                        uow._context.Remove(removed);
                    await uow.SaveChangesAsync();
                }

                if (removed == null)
                {
                    Service.InviteFilteringChannels.Add(channel.Id);
                    await ReplyConfirmLocalizedAsync("invite_filter_channel_on").ConfigureAwait(false);
                }
                else
                {
                    Service.InviteFilteringChannels.TryRemove(channel.Id);
                    await ReplyConfirmLocalizedAsync("invite_filter_channel_off").ConfigureAwait(false);
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
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
                    Service.LinkFilteringServers.Add(channel.Guild.Id);
                    await ReplyConfirmLocalizedAsync("link_filter_server_on").ConfigureAwait(false);
                }
                else
                {
                    Service.LinkFilteringServers.TryRemove(channel.Guild.Id);
                    await ReplyConfirmLocalizedAsync("link_filter_server_off").ConfigureAwait(false);
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task ChnlFilterLin()
            {
                var channel = (ITextChannel)ctx.Channel;

                FilterLinksChannelId removed;
                using (var uow = _db.GetDbContext())
                {
                    var config = uow.GuildConfigs.ForId(channel.Guild.Id,
                        set => set.Include(gc => gc.FilterLinksChannelIds));
                    var match = new FilterLinksChannelId
                    {
                        ChannelId = channel.Id
                    };
                    removed = config.FilterLinksChannelIds.FirstOrDefault(fc => fc.Equals(match));

                    if (removed == null)
                        config.FilterLinksChannelIds.Add(match);
                    else
                        uow._context.Remove(removed);
                    await uow.SaveChangesAsync();
                }

                if (removed == null)
                {
                    Service.LinkFilteringChannels.Add(channel.Id);
                    await ReplyConfirmLocalizedAsync("link_filter_channel_on").ConfigureAwait(false);
                }
                else
                {
                    Service.LinkFilteringChannels.TryRemove(channel.Id);
                    await ReplyConfirmLocalizedAsync("link_filter_channel_off").ConfigureAwait(false);
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
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
                    Service.WordFilteringServers.Add(channel.Guild.Id);
                    await ReplyConfirmLocalizedAsync("word_filter_server_on").ConfigureAwait(false);
                }
                else
                {
                    Service.WordFilteringServers.TryRemove(channel.Guild.Id);
                    await ReplyConfirmLocalizedAsync("word_filter_server_off").ConfigureAwait(false);
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task ChnlFilterWords()
            {
                var channel = (ITextChannel)ctx.Channel;

                FilterChannelId removed;
                using (var uow = _db.GetDbContext())
                {
                    var config = uow.GuildConfigs.ForId(channel.Guild.Id,
                        set => set.Include(gc => gc.FilterWordsChannelIds));

                    var match = new FilterChannelId
                    {
                        ChannelId = channel.Id
                    };
                    removed = config.FilterWordsChannelIds.FirstOrDefault(fc => fc.Equals(match));
                    if (removed == null)
                        config.FilterWordsChannelIds.Add(match);
                    else
                        uow._context.Remove(removed);
                    await uow.SaveChangesAsync();
                }

                if (removed == null)
                {
                    Service.WordFilteringChannels.Add(channel.Id);
                    await ReplyConfirmLocalizedAsync("word_filter_channel_on").ConfigureAwait(false);
                }
                else
                {
                    Service.WordFilteringChannels.TryRemove(channel.Id);
                    await ReplyConfirmLocalizedAsync("word_filter_channel_off").ConfigureAwait(false);
                }
            }

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task FilterWord([Remainder] string word)
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
                        config.FilteredWords.Add(new FilteredWord { Word = word });
                    else
                        uow._context.Remove(removed);

                    await uow.SaveChangesAsync();
                }

                var filteredWords =
                    Service.ServerFilteredWords.GetOrAdd(channel.Guild.Id, new ConcurrentHashSet<string>());

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

            [MewdekoCommand]
            [Usage]
            [Description]
            [Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task LstFilterWords(int page = 1)
            {
                page--;
                if (page < 0)
                    return;

                var channel = (ITextChannel)ctx.Channel;

                Service.ServerFilteredWords.TryGetValue(channel.Guild.Id, out var fwHash);

                var fws = fwHash.ToArray();

                var paginator = new LazyPaginatorBuilder()
                    .AddUser(ctx.User)
                    .WithPageFactory(PageFactory)
                    .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                    .WithMaxPageIndex(fws.Length / 10)
                    .WithDefaultEmotes()
                    .Build();

                await Interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60));

                Task<PageBuilder> PageFactory(int page)
                {
                    return Task.FromResult(new PageBuilder()
                        .WithTitle(GetText("filter_word_list"))
                        .WithDescription(string.Join("\n", fws.Skip(page * 10).Take(10)))
                        .WithOkColor());
                }
            }
        }
    }
}