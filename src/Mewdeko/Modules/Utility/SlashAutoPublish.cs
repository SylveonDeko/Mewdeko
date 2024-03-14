using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.Utility.Services;

namespace Mewdeko.Modules.Utility;

[Group("autopublish", "Auto publish stuff in announcement channels!")]
public class SlashAutoPublish(InteractiveService interactiveService) : MewdekoSlashSubmodule<AutoPublishService>
{
    [SlashCommand("add", "Adds a channel to be used with auto publish"), RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.Administrator)]
    public async Task AddAutoPublish(INewsChannel channel)
    {
        if (!await Service.PermCheck(channel))
        {
            await ReplyErrorLocalizedAsync("missing_managed_messages");
            return;
        }

        var added = await Service.AddAutoPublish(ctx.Guild.Id, channel.Id);
        if (!added)
            await ReplyErrorLocalizedAsync("auto_publish_already_set", channel.Mention);
        else
            await ReplyConfirmLocalizedAsync("auto_publish_set", channel.Mention);
    }

    [SlashCommand("blacklist-user", "Blacklist a user from getting their message auto published"),
     RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.Administrator)]
    public async Task AddPublishBlacklist(IUser user, INewsChannel channel)
    {
        if (await Service.CheckIfExists(channel.Id))
        {
            await ReplyErrorLocalizedAsync("channel_not_auto_publish");
            return;
        }

        if (!await Service.PermCheck(channel))
        {
            await ReplyErrorLocalizedAsync("missing_managed_messages");
            return;
        }

        var added = await Service.AddUserToBlacklist(channel.Id, user.Id);
        if (!added)
            await ReplyErrorLocalizedAsync("user_already_blacklisted_autopub", user.Mention);
        else
            await ReplyConfirmLocalizedAsync("user_publish_blacklisted", user.Mention, channel.Mention);
    }

    [SlashCommand("blacklist-word", "Blacklist a word to stop a message containing this word getting auto published"),
     RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.Administrator)]
    public async Task AddPublishBlacklist(string word, INewsChannel channel)
    {
        if (await Service.CheckIfExists(channel.Id))
        {
            await ReplyErrorLocalizedAsync("channel_not_auto_publish");
            return;
        }

        if (!await Service.PermCheck(channel))
        {
            await ReplyErrorLocalizedAsync("missing_managed_messages");
            return;
        }

        if (word.Length > 40)
        {
            await ReplyErrorLocalizedAsync("word_publish_max_length");
            return;
        }

        var added = await Service.AddWordToBlacklist(channel.Id, word);
        if (!added)
            await ReplyErrorLocalizedAsync("word_already_blacklisted_autopub", word);
        else
            await ReplyConfirmLocalizedAsync("word_publish_blacklisted", word, channel.Mention);
    }

    [SlashCommand("unblacklist-user", "Removes a user from the blacklist"), RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.Administrator)]
    public async Task RemovePublishBlacklist(IUser user, INewsChannel channel)
    {
        if (await Service.CheckIfExists(channel.Id))
        {
            await ReplyErrorLocalizedAsync("channel_not_auto_publish");
            return;
        }

        var removed = await Service.RemoveUserFromBlacklist(channel.Id, user.Id);

        if (!removed)
            await ReplyErrorLocalizedAsync("user_not_blacklisted_autopub", user.Mention);
        else
            await ReplyConfirmLocalizedAsync("user_publish_unblacklisted", user.Mention, channel.Mention);
    }

    [SlashCommand("unblacklist-word", "Removes a word from the blacklist"), RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.Administrator)]
    public async Task RemovePublishBlacklist(string word, INewsChannel channel)
    {
        if (await Service.CheckIfExists(channel.Id))
        {
            await ReplyErrorLocalizedAsync("channel_not_auto_publish");
            return;
        }

        var removed = await Service.RemoveWordFromBlacklist(channel.Id, word.ToLower());

        if (!removed)
            await ReplyErrorLocalizedAsync("word_not_blacklisted_autopub", word.ToLower());
        else
            await ReplyConfirmLocalizedAsync("user_publish_unblacklisted", word.ToLower(), channel.Mention);
    }


    [SlashCommand("remove", "Removes a channel from auto publish"), RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.Administrator)]
    public async Task RemoveAutoPublish(INewsChannel channel)
    {
        var removed = await Service.RemoveAutoPublish(ctx.Guild.Id, channel.Id);
        if (!removed)
            await ReplyErrorLocalizedAsync("auto_publish_not_set", channel.Mention);
        else
            await ReplyConfirmLocalizedAsync("auto_publish_removed", channel.Mention);
    }

    [SlashCommand("list", "Lists all auto publish channels and settings"), RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.Administrator)]
    public async Task GetAutoPublishes()
    {
        var autoPublishes = await Service.GetAutoPublishes(ctx.Guild.Id);
        if (autoPublishes.Count == 0)
        {
            await ReplyErrorLocalizedAsync("auto_publish_not_enabled");
            return;
        }

        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(autoPublishes.Count - 1)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await interactiveService.SendPaginatorAsync(paginator, Context.Interaction, TimeSpan.FromMinutes(60))
            .ConfigureAwait(false);

        async Task<PageBuilder> PageFactory(int page)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            var (autoPublish, userBlacklists, wordBlacklists) = autoPublishes[page];
            var channel = await ctx.Guild.GetChannelAsync(autoPublish.ChannelId);

            var eb = new PageBuilder()
                .WithTitle($"Auto Publish - {channel.Name.TrimTo(20)}");

            if (userBlacklists.Any())
                eb.AddField(GetText("blacklisted_users"),
                    string.Join(",", userBlacklists.Select(x => $"<@{x.User}>")));
            else
                eb.AddField("blacklisted_users", GetText("none"));

            if (wordBlacklists.Any())
                eb.AddField(GetText("blacklisted_words"),
                    string.Join("\n", wordBlacklists.Select(x => x.Word.ToLower())));
            else
                eb.AddField(GetText("blacklisted_words"), GetText("none"));
            return eb;
        }
    }
}