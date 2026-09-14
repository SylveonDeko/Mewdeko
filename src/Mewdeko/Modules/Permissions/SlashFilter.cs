using DataModel;
using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Humanizer;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.Autocompleters;
using Mewdeko.Modules.Permissions.Services;

namespace Mewdeko.Modules.Permissions;

/// <summary>
///     Slash commands for managing word, link and invite filters, auto ban words and command cooldowns.
/// </summary>
[Group("filter", "Manage word, link and invite filters and command cooldowns")]
public class SlashFilter(
    IDataConnectionFactory dbFactory,
    InteractiveService interactivity,
    GuildSettingsService gss,
    CmdCdService cmdCdService)
    : MewdekoSlashModuleBase<FilterService>
{
    /// <summary>
    ///     Toggles a word on or off the automatic ban list for the current guild.
    /// </summary>
    /// <param name="word">The word to toggle on the auto ban list.</param>
    [SlashCommand("auto-ban-word", "Toggles a word on or off the automatic ban list")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task AutoBanWord([Summary("word", "The word to toggle on the auto ban list")] string word)
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var exists = await db.AutoBanWords
            .AnyAsync(x => x.Word == word && x.GuildId == ctx.Guild.Id);

        if (exists)
        {
            await Service.UnBlacklist(word, ctx.Guild.Id);
            await ConfirmAsync(Strings.AutobanWordRemoved(ctx.Guild.Id, Format.Code(word))).ConfigureAwait(false);
        }
        else
        {
            await Service.WordBlacklist(word, ctx.Guild.Id);
            await ConfirmAsync(Strings.AutobanWordAdded(ctx.Guild.Id, Format.Code(word))).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Displays a paginated list of all words on the automatic ban list for the current guild.
    /// </summary>
    [SlashCommand("auto-ban-word-list", "Shows the list of auto ban words")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task AutoBanWordList()
    {
        await using var db = await dbFactory.CreateConnectionAsync();

        var words = db.AutoBanWords.Where(x => x.GuildId == ctx.Guild.Id);
        var count = await words.CountAsync();

        if (count == 0)
        {
            await ErrorAsync(Strings.NoAutobanWordsSet(ctx.Guild.Id)).ConfigureAwait(false);
            return;
        }

        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(count / 10)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await interactivity.SendPaginatorAsync(paginator, (ctx.Interaction as SocketInteraction)!,
            TimeSpan.FromMinutes(60)).ConfigureAwait(false);

        async Task<PageBuilder> PageFactory(int page)
        {
            var wordList = await words
                .Select(x => x.Word)
                .Skip(page * 10)
                .Take(10)
                .ToListAsync();

            return new PageBuilder().WithTitle(Strings.AutobanWordsTitle(ctx.Guild.Id))
                .WithDescription(string.Join("\n", wordList))
                .WithOkColor();
        }
    }

    /// <summary>
    ///     Enables or disables warnings for filtered words in the current guild.
    /// </summary>
    /// <param name="enabled">Whether to warn users when they use a filtered word.</param>
    [SlashCommand("warn-words", "Enables or disables warnings for filtered words")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task FWarn([Summary("enabled", "Whether to warn on filtered words")] bool enabled)
    {
        await Service.SetFwarn(ctx.Guild, enabled ? "y" : "n").ConfigureAwait(false);
        switch (await Service.GetFw(ctx.Guild.Id))
        {
            case 1:
                await ConfirmAsync(Strings.WarnFilteredWordEnabled(ctx.Guild.Id)).ConfigureAwait(false);
                break;
            case 0:
                await ConfirmAsync(Strings.WarnFilteredWordDisabled(ctx.Guild.Id)).ConfigureAwait(false);
                break;
        }
    }

    /// <summary>
    ///     Enables or disables warnings for invite links posted in the current guild.
    /// </summary>
    /// <param name="enabled">Whether to warn users when they post an invite link.</param>
    [SlashCommand("warn-invites", "Enables or disables warnings for invite links")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task InvWarn([Summary("enabled", "Whether to warn on invite links")] bool enabled)
    {
        await Service.InvWarn(ctx.Guild, enabled ? "y" : "n").ConfigureAwait(false);
        switch (await Service.GetInvWarn(ctx.Guild.Id))
        {
            case 1:
                await ConfirmAsync(Strings.WarnInviteEnabled(ctx.Guild.Id)).ConfigureAwait(false);
                break;
            case 0:
                await ConfirmAsync(Strings.WarnInviteDisabled(ctx.Guild.Id)).ConfigureAwait(false);
                break;
        }
    }

    /// <summary>
    ///     Clears all filtered words for the current guild.
    /// </summary>
    [SlashCommand("clear-words", "Clears all filtered words")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task FwClear()
    {
        await Service.ClearFilteredWords(ctx.Guild.Id);
        await ReplyConfirmAsync(Strings.FwCleared(ctx.Guild.Id)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Toggles the server-wide invite link filter on or off.
    /// </summary>
    [SlashCommand("server-invites", "Toggles the server wide invite link filter")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task SrvrFilterInv()
    {
        var config = await gss.GetGuildConfig(ctx.Guild.Id);
        config.FilterInvites = !config.FilterInvites;
        await gss.UpdateGuildConfig(ctx.Guild.Id, config).ConfigureAwait(false);

        if (config.FilterInvites)
            await ReplyConfirmAsync(Strings.InviteFilterServerOn(ctx.Guild.Id)).ConfigureAwait(false);
        else
            await ReplyConfirmAsync(Strings.InviteFilterServerOff(ctx.Guild.Id)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Toggles the invite link filter for a specific channel on or off.
    /// </summary>
    /// <param name="channel">The channel to toggle. Defaults to the current channel.</param>
    [SlashCommand("channel-invites", "Toggles the invite link filter for a channel")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task ChnlFilterInv(
        [Summary("channel", "The channel to toggle, defaults to the current one")]
        ITextChannel? channel = null)
    {
        channel ??= (ITextChannel)ctx.Channel;
        var guildId = ctx.Guild.Id;
        var channelId = channel.Id;

        await using var db = await dbFactory.CreateConnectionAsync();

        var exists = await db.FilterInvitesChannelIds
            .AnyAsync(fc => fc.GuildId == guildId && fc.ChannelId == channelId);

        if (!exists)
        {
            await db.InsertAsync(new FilterInvitesChannelId
            {
                GuildId = guildId, ChannelId = channelId
            });

            await ReplyConfirmAsync(Strings.InviteFilterChannelOn(guildId)).ConfigureAwait(false);
        }
        else
        {
            await db.FilterInvitesChannelIds
                .Where(fc => fc.GuildId == guildId && fc.ChannelId == channelId)
                .DeleteAsync();

            await ReplyConfirmAsync(Strings.InviteFilterChannelOff(guildId)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Toggles the server-wide link filter on or off.
    /// </summary>
    [SlashCommand("server-links", "Toggles the server wide link filter")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task SrvrFilterLin()
    {
        var config = await gss.GetGuildConfig(ctx.Guild.Id);
        config.FilterLinks = !config.FilterLinks;
        await gss.UpdateGuildConfig(ctx.Guild.Id, config).ConfigureAwait(false);

        if (config.FilterLinks)
            await ReplyConfirmAsync(Strings.LinkFilterServerOn(ctx.Guild.Id)).ConfigureAwait(false);
        else
            await ReplyConfirmAsync(Strings.LinkFilterServerOff(ctx.Guild.Id)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Toggles the link filter for a specific channel on or off.
    /// </summary>
    /// <param name="channel">The channel to toggle. Defaults to the current channel.</param>
    [SlashCommand("channel-links", "Toggles the link filter for a channel")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task ChnlFilterLin(
        [Summary("channel", "The channel to toggle, defaults to the current one")]
        ITextChannel? channel = null)
    {
        channel ??= (ITextChannel)ctx.Channel;
        var guildId = ctx.Guild.Id;
        var channelId = channel.Id;

        await using var db = await dbFactory.CreateConnectionAsync();

        var exists = await db.FilterLinksChannelIds
            .AnyAsync(fc => fc.GuildId == guildId && fc.ChannelId == channelId);

        if (!exists)
        {
            await db.InsertAsync(new FilterLinksChannelId
            {
                GuildId = guildId, ChannelId = channelId
            });

            await ReplyConfirmAsync(Strings.LinkFilterChannelOn(guildId)).ConfigureAwait(false);
        }
        else
        {
            await db.FilterLinksChannelIds
                .Where(fc => fc.GuildId == guildId && fc.ChannelId == channelId)
                .DeleteAsync();

            await ReplyConfirmAsync(Strings.LinkFilterChannelOff(guildId)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Toggles the server-wide word filter on or off.
    /// </summary>
    [SlashCommand("server-words", "Toggles the server wide word filter")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task SrvrFilterWords()
    {
        var config = await gss.GetGuildConfig(ctx.Guild.Id);
        config.FilterWords = !config.FilterWords;
        await gss.UpdateGuildConfig(ctx.Guild.Id, config).ConfigureAwait(false);

        if (config.FilterWords)
            await ReplyConfirmAsync(Strings.WordFilterServerOn(ctx.Guild.Id)).ConfigureAwait(false);
        else
            await ReplyConfirmAsync(Strings.WordFilterServerOff(ctx.Guild.Id)).ConfigureAwait(false);
    }

    /// <summary>
    ///     Toggles the word filter for a specific channel on or off.
    /// </summary>
    /// <param name="channel">The channel to toggle. Defaults to the current channel.</param>
    [SlashCommand("channel-words", "Toggles the word filter for a channel")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task ChnlFilterWords(
        [Summary("channel", "The channel to toggle, defaults to the current one")]
        ITextChannel? channel = null)
    {
        channel ??= (ITextChannel)ctx.Channel;
        var guildId = ctx.Guild.Id;
        var channelId = channel.Id;

        await using var db = await dbFactory.CreateConnectionAsync();

        var exists = await db.FilterWordsChannelIds
            .AnyAsync(fc => fc.GuildId == guildId && fc.ChannelId == channelId);

        if (!exists)
        {
            await db.InsertAsync(new FilterWordsChannelId
            {
                GuildId = guildId, ChannelId = channelId
            });

            await ReplyConfirmAsync(Strings.WordFilterChannelOn(guildId)).ConfigureAwait(false);
        }
        else
        {
            await db.FilterWordsChannelIds
                .Where(fc => fc.GuildId == guildId && fc.ChannelId == channelId)
                .DeleteAsync();

            await ReplyConfirmAsync(Strings.WordFilterChannelOff(guildId)).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Adds or removes a word from the filtered words list in the current guild.
    /// </summary>
    /// <param name="word">The word to toggle on the filtered words list.</param>
    [SlashCommand("word", "Adds or removes a word from the filtered words list")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task FilterWord([Summary("word", "The word to toggle on the filtered words list")] string word)
    {
        var guildId = ctx.Guild.Id;

        word = word.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(word))
        {
            await ErrorAsync(Strings.FilterWordEmpty(guildId)).ConfigureAwait(false);
            return;
        }

        await using var db = await dbFactory.CreateConnectionAsync();

        var exists = await db.FilteredWords
            .AnyAsync(fw => fw.GuildId == guildId &&
                            fw.Word.Trim().ToLowerInvariant() == word);

        if (!exists)
        {
            await db.InsertAsync(new FilteredWord
            {
                GuildId = guildId, Word = word
            });

            await ReplyConfirmAsync(Strings.FilterWordAdd(guildId, Format.Code(word))).ConfigureAwait(false);
        }
        else
        {
            await db.FilteredWords
                .Where(fw => fw.GuildId == guildId &&
                             fw.Word.Trim().ToLowerInvariant() == word)
                .DeleteAsync();

            await ReplyConfirmAsync(Strings.FilterWordRemove(guildId, Format.Code(word))).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Lists all words currently on the filtered words list for the current guild.
    /// </summary>
    [SlashCommand("list", "Shows the list of filtered words")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task LstFilterWords()
    {
        var guildId = ctx.Guild.Id;

        await using var db = await dbFactory.CreateConnectionAsync();

        var words = await db.FilteredWords
            .Where(fw => fw.GuildId == guildId)
            .Select(fw => fw.Word)
            .ToArrayAsync();

        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(words.Length / 10)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await interactivity.SendPaginatorAsync(paginator, (ctx.Interaction as SocketInteraction)!,
            TimeSpan.FromMinutes(60)).ConfigureAwait(false);

        async Task<PageBuilder> PageFactory(int page)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            return new PageBuilder().WithTitle(Strings.FilterWordList(guildId))
                .WithDescription(string.Join("\n", words.Skip(page * 10).Take(10)))
                .WithOkColor();
        }
    }

    /// <summary>
    ///     Sets or clears the cooldown for a specified command in the guild.
    /// </summary>
    /// <param name="command">The command to set the cooldown for.</param>
    /// <param name="time">The duration of the cooldown. Omit to clear the cooldown.</param>
    [SlashCommand("cooldown", "Sets or clears the cooldown for a command")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task CmdCooldown(
        [Summary("command", "The command to set the cooldown for")] [Autocomplete(typeof(GenericCommandAutocompleter))]
        string command,
        [Summary("time", "The cooldown duration, for example 30s. Omit to clear")]
        TimeSpan? time = null)
    {
        var cooldown = time ?? TimeSpan.Zero;
        var guildId = ctx.Guild.Id;

        if (cooldown.TotalSeconds is < 0 or > 90000)
        {
            await ReplyErrorAsync(Strings.InvalidSecondParamBetween(guildId, 0, 90000)).ConfigureAwait(false);
            return;
        }

        var name = command.ToLowerInvariant();

        await using var db = await dbFactory.CreateConnectionAsync();

        var existingCooldown = await db.CommandCooldowns
            .FirstOrDefaultAsync(cc => cc.GuildId == guildId && cc.CommandName == name);

        if (existingCooldown != null)
            await db.CommandCooldowns
                .Where(cc => cc.Id == existingCooldown.Id)
                .DeleteAsync();

        if (cooldown.TotalSeconds != 0)
        {
            await db.InsertAsync(new CommandCooldown
            {
                GuildId = guildId, CommandName = name, Seconds = Convert.ToInt32(cooldown.TotalSeconds)
            });
        }

        if (cooldown.TotalSeconds == 0)
        {
            var activeCds = cmdCdService.ActiveCooldowns.GetOrAdd(guildId, []);
            activeCds.RemoveWhere(ac => ac.Command == name);
            await ReplyConfirmAsync(Strings.CmdcdCleared(guildId, Format.Bold(name))).ConfigureAwait(false);
        }
        else
        {
            await ReplyConfirmAsync(Strings.CmdcdAdd(guildId, Format.Bold(name), Format.Bold(cooldown.Humanize())))
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Displays all commands with active cooldowns in the guild.
    /// </summary>
    [SlashCommand("cooldowns-list", "Lists all commands with their cooldowns")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task AllCmdCooldowns()
    {
        var guildId = ctx.Guild.Id;

        await using var db = await dbFactory.CreateConnectionAsync();

        var commandCooldowns = await db.CommandCooldowns
            .Where(cc => cc.GuildId == guildId)
            .ToListAsync();

        if (commandCooldowns.Count == 0)
        {
            await ReplyConfirmAsync(Strings.CmdcdNone(guildId)).ConfigureAwait(false);
            return;
        }

        var i = 0;
        var rows = commandCooldowns
            .Select(c => $"{c.CommandName}: {c.Seconds}{Strings.Sec(guildId)}")
            .GroupBy(_ => i++ / 2)
            .Select(ig => string.Concat(ig.Select(s => $"{s,-30}")));

        await ctx.Interaction.RespondAsync($"```css\n{string.Join("\n", rows)}\n```").ConfigureAwait(false);
    }
}