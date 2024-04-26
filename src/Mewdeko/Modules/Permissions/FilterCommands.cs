using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using LinqToDB.EntityFrameworkCore;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Permissions.Services;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Modules.Permissions;

public partial class Permissions
{
    /// <summary>
    /// Provides commands for managing word filters and automatic bans within guilds.
    /// </summary>
    [Group]
    public class FilterCommands(DbService db, InteractiveService serv, GuildSettingsService gss)
        : MewdekoSubmodule<FilterService>
    {
        /// <summary>
        /// Toggles a word on or off the automatic ban list for the current guild.
        /// </summary>
        /// <param name="word">The word to toggle on the auto ban list.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        /// If the word is currently on the list, this command removes it, effectively unblacklisting the word.
        /// If the word is not on the list, it adds the word, automatically banning any user who uses it.
        /// Requires Administrator permission to execute.
        /// </remarks>
        /// <example>
        /// .AutoBanWord "example" - Toggles the word "example" on or off the auto ban list.
        /// </example>
        [Cmd]
        [Aliases]
        [UserPerm(GuildPermission.Administrator)]
        [RequireContext(ContextType.Guild)]
        public async Task AutoBanWord([Remainder] string word)
        {
            await using var uow = db.GetDbContext();
            var blacklist = uow.AutoBanWords;
            if (blacklist.Count(x => x.Word == word && x.GuildId == ctx.Guild.Id) == 1)
            {
                Service.UnBlacklist(word, ctx.Guild.Id);
                await ctx.Channel.SendConfirmAsync($"Removed {Format.Code(word)} from the auto bans word list!")
                    .ConfigureAwait(false);
            }
            else
            {
                Service.WordBlacklist(word, ctx.Guild.Id);
                await ctx.Channel.SendConfirmAsync($"Added {Format.Code(word)} to the auto ban words list!")
                    .ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Displays a paginated list of all words on the automatic ban list for the current guild.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        /// Requires Administrator permission to execute.
        /// Uses an interactive paginator to navigate through the list of banned words.
        /// </remarks>
        /// <example>
        /// .AutoBanWordList - Shows the paginated list of auto ban words.
        /// </example>
        [Cmd]
        [Aliases]
        [UserPerm(GuildPermission.Administrator)]
        [RequireContext(ContextType.Guild)]
        public async Task AutoBanWordList()
        {
            await using var uow = db.GetDbContext();
            var words = uow.AutoBanWords.ToLinqToDB().Where(x => x.GuildId == ctx.Guild.Id);
            if (!words.Any())
            {
                await ctx.Channel.SendErrorAsync("No AutoBanWords set.", Config).ConfigureAwait(false);
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

                await serv.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60))
                    .ConfigureAwait(false);

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

        /// <summary>
        /// Enables or disables warnings for filtered words in the current guild.
        /// </summary>
        /// <param name="yesnt">A string indicating whether to enable ("y") or disable ("n") the warning.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        /// Requires Administrator permission to execute.
        /// </remarks>
        /// <example>
        /// .FWarn "y" - Enables warnings for filtered words.
        /// .FWarn "n" - Disables warnings for filtered words.
        /// </example>
        [Cmd]
        [Aliases]
        [UserPerm(GuildPermission.Administrator)]
        [RequireContext(ContextType.Guild)]
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

        /// <summary>
        /// Enables or disables warnings for invite links posted in the current guild.
        /// </summary>
        /// <param name="yesnt">A string indicating whether to enable ("y") or disable ("n") the warning.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        /// Requires Administrator permission to execute.
        /// </remarks>
        /// <example>
        /// .InvWarn "y" - Enables warnings for invite links.
        /// .InvWarn "n" - Disables warnings for invite links.
        /// </example>
        [Cmd]
        [Aliases]
        [UserPerm(GuildPermission.Administrator)]
        [RequireContext(ContextType.Guild)]
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

        /// <summary>
        /// Clears all filtered words for the current guild.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        /// This command removes all words from the filtered words list, effectively disabling word filtering until new words are added.
        /// Requires Administrator permission to execute.
        /// </remarks>
        /// <example>
        /// .FwClear - Clears the filtered words list.
        /// </example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task FwClear()
        {
            await Service.ClearFilteredWords(ctx.Guild.Id);
            await ReplyConfirmLocalizedAsync("fw_cleared").ConfigureAwait(false);
        }

        /// <summary>
        /// Toggles the server-wide invite link filter on or off.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        /// When enabled, posting invite links to other Discord servers will be automatically blocked.
        /// Requires Administrator permission to execute.
        /// </remarks>
        /// <example>
        /// .SrvrFilterInv - Toggles the server-wide invite filter.
        /// </example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task SrvrFilterInv()
        {
            var channel = (ITextChannel)ctx.Channel;

            var uow = db.GetDbContext();
            await using var disposable = uow.ConfigureAwait(false);
            var config = await uow.ForGuildId(channel.Guild.Id, set => set);
            config.FilterInvites = !config.FilterInvites;
            await uow.SaveChangesAsync().ConfigureAwait(false);
            await gss.UpdateGuildConfig(ctx.Guild.Id, config).ConfigureAwait(false);

            if (config.FilterInvites)
            {
                await ReplyConfirmLocalizedAsync("invite_filter_server_on").ConfigureAwait(false);
            }
            else
            {
                await ReplyConfirmLocalizedAsync("invite_filter_server_off").ConfigureAwait(false);
            }
        }


        /// <summary>
        /// Toggles the invite link filter for a specific channel on or off.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        /// This command allows you to enable or disable invite link filtering on a per-channel basis.
        /// Requires Administrator permission to execute.
        /// </remarks>
        /// <example>
        /// .ChnlFilterInv - Toggles the invite filter for the current channel.
        /// </example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task ChnlFilterInv()
        {
            var channel = (ITextChannel)ctx.Channel;

            FilterInvitesChannelIds removed;
            var uow = db.GetDbContext();
            await using (uow.ConfigureAwait(false))
            {
                var config = await uow.ForGuildId(channel.Guild.Id,
                    set => set.Include(gc => gc.FilterInvitesChannelIds));
                var match = new FilterInvitesChannelIds
                {
                    ChannelId = channel.Id
                };
                removed = config.FilterInvitesChannelIds.FirstOrDefault(fc => fc.Equals(match));

                if (removed == null)
                    config.FilterInvitesChannelIds.Add(match);
                else
                    uow.Remove(removed);
                await uow.SaveChangesAsync().ConfigureAwait(false);
                await gss.UpdateGuildConfig(ctx.Guild.Id, config).ConfigureAwait(false);
            }

            if (removed == null)
            {
                await ReplyConfirmLocalizedAsync("invite_filter_channel_on").ConfigureAwait(false);
            }
            else
            {
                await ReplyConfirmLocalizedAsync("invite_filter_channel_off").ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Toggles the server-wide link filter on or off.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        /// When enabled, posting any links will be automatically blocked server-wide.
        /// Requires Administrator permission to execute.
        /// </remarks>
        /// <example>
        /// .SrvrFilterLin - Toggles the server-wide link filter.
        /// </example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task SrvrFilterLin()
        {
            var channel = (ITextChannel)ctx.Channel;
            var uow = db.GetDbContext();
            await using var disposable = uow.ConfigureAwait(false);
            var config = await uow.ForGuildId(channel.Guild.Id, set => set);
            config.FilterLinks = !config.FilterLinks;
            await uow.SaveChangesAsync().ConfigureAwait(false);
            await gss.UpdateGuildConfig(ctx.Guild.Id, config).ConfigureAwait(false);

            if (config.FilterLinks)
            {
                await ReplyConfirmLocalizedAsync("link_filter_server_on").ConfigureAwait(false);
            }
            else
            {
                await ReplyConfirmLocalizedAsync("link_filter_server_off").ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Toggles the link filter for a specific channel on or off.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        /// This command allows you to enable or disable link filtering on a per-channel basis.
        /// Requires Administrator permission to execute.
        /// </remarks>
        /// <example>
        /// .ChnlFilterLin - Toggles the link filter for the current channel.
        /// </example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
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
                await gss.UpdateGuildConfig(ctx.Guild.Id, config).ConfigureAwait(false);
            }

            if (removed == null)
            {
                await ReplyConfirmLocalizedAsync("link_filter_channel_on").ConfigureAwait(false);
            }
            else
            {
                await ReplyConfirmLocalizedAsync("link_filter_channel_off").ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Toggles the server-wide word filter on or off.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        /// When enabled, specified words will be automatically blocked server-wide.
        /// Requires Administrator permission to execute.
        /// </remarks>
        /// <example>
        /// .SrvrFilterWords - Toggles the server-wide word filter.
        /// </example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task SrvrFilterWords()
        {
            var channel = (ITextChannel)ctx.Channel;

            await using var uow = db.GetDbContext();
            var config = await uow.ForGuildId(channel.Guild.Id, set => set);
            config.FilterWords = !config.FilterWords;
            await uow.SaveChangesAsync().ConfigureAwait(false);
            await gss.UpdateGuildConfig(ctx.Guild.Id, config).ConfigureAwait(false);

            if (config.FilterWords)
            {
                await ReplyConfirmLocalizedAsync("word_filter_server_on").ConfigureAwait(false);
            }
            else
            {
                await ReplyConfirmLocalizedAsync("word_filter_server_off").ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Toggles the word filter for a specific channel on or off.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        /// This command allows you to enable or disable word filtering on a per-channel basis.
        /// Requires Administrator permission to execute.
        /// </remarks>
        /// <example>
        /// .ChnlFilterWords - Toggles the word filter for the current channel.
        /// </example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task ChnlFilterWords()
        {
            var channel = (ITextChannel)ctx.Channel;

            FilterWordsChannelIds removed;
            var uow = db.GetDbContext();
            await using (uow.ConfigureAwait(false))
            {
                var config = await uow.ForGuildId(channel.Guild.Id,
                    set => set.Include(gc => gc.FilterWordsChannelIds));

                var match = new FilterWordsChannelIds
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
                await ReplyConfirmLocalizedAsync("word_filter_channel_on").ConfigureAwait(false);
            }
            else
            {
                await ReplyConfirmLocalizedAsync("word_filter_channel_off").ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Adds or removes a word from the filtered words list in the current guild.
        /// </summary>
        /// <param name="word">The word to toggle on the filtered words list.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        /// If the word is currently on the list, this command removes it, effectively unfiltering the word.
        /// If the word is not on the list, it adds the word to the list.
        /// Requires Administrator permission to execute.
        /// </remarks>
        /// <example>
        /// .FilterWord "example" - Toggles the word "example" on or off the filtered words list.
        /// </example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
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
                await gss.UpdateGuildConfig(ctx.Guild.Id, config).ConfigureAwait(false);
            }

            if (removed == null)
            {
                await ReplyConfirmLocalizedAsync("filter_word_add", Format.Code(word)).ConfigureAwait(false);
            }
            else
            {
                await ReplyConfirmLocalizedAsync("filter_word_remove", Format.Code(word)).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Lists all words currently on the filtered words list for the current guild.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        /// Uses an interactive paginator to navigate through the list of filtered words.
        /// Requires Administrator permission to execute.
        /// </remarks>
        /// <example>
        /// .LstFilterWords - Shows the paginated list of filtered words.
        /// </example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task LstFilterWords()
        {
            var channel = (ITextChannel)ctx.Channel;

            var config = await gss.GetGuildConfig(channel.Guild.Id);
            var fwHash = config.FilteredWords.Select(x => x.Word);

            var fws = fwHash.ToArray();

            var paginator = new LazyPaginatorBuilder()
                .AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(fws.Length / 10)
                .WithDefaultEmotes()
                .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                .Build();

            await serv.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);

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