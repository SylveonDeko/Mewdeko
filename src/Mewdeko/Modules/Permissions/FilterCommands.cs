using System.Threading.Tasks;
using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.Collections;
using Mewdeko.Modules.Permissions.Services;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Modules.Permissions;

public partial class Permissions
{
    [Group]
    public class FilterCommands : MewdekoSubmodule<FilterService>
    {
        private readonly DbService db;
        private readonly InteractiveService interactivity;

        public FilterCommands(DbService db, InteractiveService serv)
        {
            interactivity = serv;
            this.db = db;
        }

        [Cmd, Aliases, UserPerm(GuildPermission.Administrator), RequireContext(ContextType.Guild)]
        public async Task AutoBanWord([Remainder] string word)
        {
            if (Service.Blacklist.Count(x => x.Word == word && x.GuildId == ctx.Guild.Id) == 1)
            {
                Service.UnBlacklist(word, ctx.Guild.Id);
                await ctx.Channel.SendConfirmAsync($"Removed {Format.Code(word)} from the auto bans word list!").ConfigureAwait(false);
            }
            else
            {
                Service.WordBlacklist(word, ctx.Guild.Id);
                await ctx.Channel.SendConfirmAsync($"Added {Format.Code(word)} to the auto ban words list!").ConfigureAwait(false);
            }
        }

        [Cmd, Aliases, UserPerm(GuildPermission.Administrator), RequireContext(ContextType.Guild)]
        public async Task AutoBanWordList()
        {
            var words = Service.Blacklist.Where(x => x.GuildId == ctx.Guild.Id);
            if (!words.Any())
            {
                await ctx.Channel.SendErrorAsync("No AutoBanWords set.").ConfigureAwait(false);
            }
            else
            {
                var paginator = new LazyPaginatorBuilder()
                    .AddUser(ctx.User)
                    .WithPageFactory(PageFactory)
                    .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                    .WithMaxPageIndex(words.Count() / 10)
                    .WithDefaultEmotes()
                    .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                    .Build();

                await interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

                async Task<PageBuilder> PageFactory(int page)
                {
                    await Task.CompletedTask.ConfigureAwait(false);
                    return new PageBuilder().WithTitle("AutoBanWords")
                        .WithDescription(string.Join("\n",
                            words.Select(x => x.Word).Skip(page * 10).Take(10)))
                        .WithOkColor();
                }
            }
        }

        [Cmd, Aliases, UserPerm(GuildPermission.Administrator), RequireContext(ContextType.Guild)]
        public async Task FWarn(string yesnt)
        {
            await Service.SetFwarn(ctx.Guild, yesnt[..1].ToLower()).ConfigureAwait(false);
            switch (await Service.GetFw(ctx.Guild.Id))
            {
                case 1:
                    await ctx.Channel.SendConfirmAsync("Warn on filtered word is now enabled!").ConfigureAwait(false);
                    break;
                case 0:
                    await ctx.Channel.SendConfirmAsync("Warn on filtered word is now disabled!").ConfigureAwait(false);
                    break;
            }
        }

        [Cmd, Aliases, UserPerm(GuildPermission.Administrator), RequireContext(ContextType.Guild)]
        public async Task InvWarn(string yesnt)
        {
            await Service.InvWarn(ctx.Guild, yesnt[..1].ToLower()).ConfigureAwait(false);
            switch (await Service.GetInvWarn(ctx.Guild.Id))
            {
                case 1:
                    await ctx.Channel.SendConfirmAsync("Warn on invite post is now enabled!").ConfigureAwait(false);
                    break;
                case 0:
                    await ctx.Channel.SendConfirmAsync("Warn on invite post is now disabled!").ConfigureAwait(false);
                    break;
            }
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild),
         UserPerm(GuildPermission.Administrator)]
        public async Task FwClear()
        {
            await Service.ClearFilteredWords(ctx.Guild.Id);
            await ReplyConfirmLocalizedAsync("fw_cleared").ConfigureAwait(false);
        }

        [Cmd, Aliases, RequireContext(ContextType.Guild)]
        public async Task SrvrFilterInv()
        {
            var channel = (ITextChannel)ctx.Channel;

            bool enabled;
            var uow = db.GetDbContext();
            await using (uow.ConfigureAwait(false))
            {
                var config = await uow.ForGuildId(channel.Guild.Id, set => set);
                enabled = config.FilterInvites = !config.FilterInvites;
                await uow.SaveChangesAsync().ConfigureAwait(false);
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

        [Cmd, Aliases, RequireContext(ContextType.Guild)]
        public async Task ChnlFilterInv()
        {
            var channel = (ITextChannel)ctx.Channel;

            FilterChannelId removed;
            var uow = db.GetDbContext();
            await using (uow.ConfigureAwait(false))
            {
                var config = await uow.ForGuildId(channel.Guild.Id,
                    set => set.Include(gc => gc.FilterInvitesChannelIds));
                var match = new FilterChannelId
                {
                    ChannelId = channel.Id
                };
                removed = config.FilterInvitesChannelIds.FirstOrDefault(fc => fc.Equals(match));

                if (removed == null)
                    config.FilterInvitesChannelIds.Add(match);
                else
                    uow.Remove(removed);
                await uow.SaveChangesAsync().ConfigureAwait(false);
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

        [Cmd, Aliases, RequireContext(ContextType.Guild)]
        public async Task SrvrFilterLin()
        {
            var channel = (ITextChannel)ctx.Channel;

            bool enabled;
            var uow = db.GetDbContext();
            await using (uow.ConfigureAwait(false))
            {
                var config = await uow.ForGuildId(channel.Guild.Id, set => set);
                enabled = config.FilterLinks = !config.FilterLinks;
                await uow.SaveChangesAsync().ConfigureAwait(false);
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

        [Cmd, Aliases, RequireContext(ContextType.Guild)]
        public async Task ChnlFilterLin()
        {
            var channel = (ITextChannel)ctx.Channel;

            FilterLinksChannelId removed;
            var uow = db.GetDbContext();
            await using (uow.ConfigureAwait(false))
            {
                var config = await uow.ForGuildId(channel.Guild.Id,
                    set => set.Include(gc => gc.FilterLinksChannelIds));
                var match = new FilterLinksChannelId
                {
                    ChannelId = channel.Id
                };
                removed = config.FilterLinksChannelIds.FirstOrDefault(fc => fc.Equals(match));

                if (removed == null)
                    config.FilterLinksChannelIds.Add(match);
                else
                    uow.Remove(removed);
                await uow.SaveChangesAsync().ConfigureAwait(false);
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

        [Cmd, Aliases, RequireContext(ContextType.Guild)]
        public async Task SrvrFilterWords()
        {
            var channel = (ITextChannel)ctx.Channel;

            bool enabled;
            var uow = db.GetDbContext();
            await using (uow.ConfigureAwait(false))
            {
                var config = await uow.ForGuildId(channel.Guild.Id, set => set);
                enabled = config.FilterWords = !config.FilterWords;
                await uow.SaveChangesAsync().ConfigureAwait(false);
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

        [Cmd, Aliases, RequireContext(ContextType.Guild)]
        public async Task ChnlFilterWords()
        {
            var channel = (ITextChannel)ctx.Channel;

            FilterChannelId removed;
            var uow = db.GetDbContext();
            await using (uow.ConfigureAwait(false))
            {
                var config = await uow.ForGuildId(channel.Guild.Id,
                    set => set.Include(gc => gc.FilterWordsChannelIds));

                var match = new FilterChannelId
                {
                    ChannelId = channel.Id
                };
                removed = config.FilterWordsChannelIds.FirstOrDefault(fc => fc.Equals(match));
                if (removed == null)
                    config.FilterWordsChannelIds.Add(match);
                else
                    uow.Remove(removed);
                await uow.SaveChangesAsync().ConfigureAwait(false);
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

        [Cmd, Aliases, RequireContext(ContextType.Guild)]
        public async Task FilterWord([Remainder] string? word)
        {
            var channel = (ITextChannel)ctx.Channel;

            word = word?.Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(word))
                return;

            FilteredWord removed;
            var uow = db.GetDbContext();
            await using (uow.ConfigureAwait(false))
            {
                var config = await uow.ForGuildId(channel.Guild.Id, set => set.Include(gc => gc.FilteredWords));

                removed = config.FilteredWords.FirstOrDefault(fw => fw.Word.Trim().ToLowerInvariant() == word);

                if (removed == null)
                    config.FilteredWords.Add(new FilteredWord
                    {
                        Word = word
                    });
                else
                    uow.Remove(removed);

                await uow.SaveChangesAsync().ConfigureAwait(false);
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

        [Cmd, Aliases, RequireContext(ContextType.Guild)]
        public async Task LstFilterWords()
        {
            var channel = (ITextChannel)ctx.Channel;

            Service.ServerFilteredWords.TryGetValue(channel.Guild.Id, out var fwHash);

            var fws = fwHash.ToArray();

            var paginator = new LazyPaginatorBuilder()
                .AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(fws.Length / 10)
                .WithDefaultEmotes()
                .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                .Build();

            await interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

            async Task<PageBuilder> PageFactory(int page)
            {
                await Task.CompletedTask.ConfigureAwait(false);
                return new PageBuilder().WithTitle(GetText("filter_word_list"))
                    .WithDescription(
                        string.Join("\n", fws.Skip(page * 10).Take(10)))
                    .WithOkColor();
            }
        }
    }
}