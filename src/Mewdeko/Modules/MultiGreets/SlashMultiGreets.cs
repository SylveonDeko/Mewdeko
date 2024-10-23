using System.Net.Http;
using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Humanizer;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.MultiGreets.Services;

namespace Mewdeko.Modules.MultiGreets;

/// <summary>
///     Slash commands for MultiGreets.
/// </summary>
[Group("multigreets", "Set or manage MultiGreets.")]
public class SlashMultiGreets : MewdekoSlashModuleBase<MultiGreetService>
{
    /// <summary>
    ///     The types of MultiGreets.
    /// </summary>
    public enum MultiGreetTypes
    {
        /// <summary>
        ///     Executes all MultiGreets.
        /// </summary>
        MultiGreet,

        /// <summary>
        ///     Executes a random MultiGreet.
        /// </summary>
        RandomGreet,

        /// <summary>
        ///     Disables MultiGreets.
        /// </summary>
        Off
    }

    private readonly InteractiveService interactivity;

    /// <summary>
    ///     Initializes a new instance of <see cref="SlashMultiGreets" />.
    /// </summary>
    /// <param name="interactivity">Service used for embed pagination</param>
    public SlashMultiGreets(InteractiveService interactivity)
    {
        this.interactivity = interactivity;
    }

    /// <summary>
    ///     Adds a MultiGreet channel.
    /// </summary>
    /// <param name="channel">The channel to add</param>
    [SlashCommand("add", "Add a channel to MultiGreets.")]
    [SlashUserPerm(GuildPermission.Administrator)]
    [CheckPermissions]
    public async Task MultiGreetAdd(ITextChannel? channel = null)
    {
        channel ??= ctx.Channel as ITextChannel;
        var added = await Service.AddMultiGreet(ctx.Guild.Id, channel.Id);
        switch (added)
        {
            case true:
                await ctx.Interaction.SendConfirmAsync($"Added {channel.Mention} as a MultiGreet channel!");
                break;
            case false:
                await ctx.Interaction.SendErrorAsync(
                    "Seems like you have reached your 5 greets per channel limit or your 30 greets per guild limit! Remove a MultiGreet and try again",
                    Config);
                break;
        }

        ;
    }

    /// <summary>
    ///     Removes a MultiGreet channel.
    /// </summary>
    /// <param name="id">The id of the MultiGreet to remove</param>
    [SlashCommand("remove", "Remove a channel from MultiGreets")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    [CheckPermissions]
    public async Task MultiGreetRemove(int id)
    {
        var greet = (await Service.GetGreets(ctx.Guild.Id)).ElementAt(id - 1);
        if (greet is null)
        {
            await ctx.Interaction.SendErrorAsync("No greet with that ID found!", Config).ConfigureAwait(false);
            return;
        }

        await Service.RemoveMultiGreetInternal(greet).ConfigureAwait(false);
        await ctx.Interaction.SendConfirmAsync("MultiGreet removed!").ConfigureAwait(false);
    }

    /// <summary>
    ///     Removes all MultiGreets from a channel.
    /// </summary>
    /// <param name="channel">The channel to remove MultiGreets from</param>
    [SlashCommand("removechannel", "Removes all MultiGreets on that channel.")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    [CheckPermissions]
    public async Task MultiGreetRemove(ITextChannel channel)
    {
        await ctx.Interaction.DeferAsync().ConfigureAwait(false);
        var greet = (await Service.GetGreets(ctx.Guild.Id)).Where(x => x.ChannelId == channel.Id);
        if (!greet.Any())
        {
            await ctx.Interaction.SendErrorFollowupAsync("There are no greets in that channel!", Config)
                .ConfigureAwait(false);
            return;
        }

        if (await PromptUserConfirmAsync(
                    new EmbedBuilder().WithOkColor()
                        .WithDescription("Are you sure you want to remove all MultiGreets for this channel?"),
                    ctx.User.Id)
                .ConfigureAwait(false))
        {
            await Service.MultiRemoveMultiGreetInternal(greet.ToArray()).ConfigureAwait(false);
            await ctx.Interaction.SendConfirmFollowupAsync("MultiGreets removed!").ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Changes the delete time for a MultiGreet message.
    /// </summary>
    /// <param name="id">The id of the MultiGreet to change</param>
    /// <param name="howlong">The time to delete the message after</param>
    [SlashCommand("delete", "Set how long it takes for a greet to delete")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    [RequireBotPermission(GuildPermission.ManageMessages)]
    [CheckPermissions]
    public async Task MultiGreetDelete(int id,
        [Summary("Seconds", "After how long in seconds it should delete.")]
        int howlong)
    {
        var greet = (await Service.GetGreets(ctx.Guild.Id)).ElementAt(id - 1);
        if (greet is null)
        {
            await ctx.Interaction.SendErrorAsync("No MultiGreet found for that Id!", Config).ConfigureAwait(false);
            return;
        }

        await Service.ChangeMgDelete(greet, howlong).ConfigureAwait(false);
        if (howlong > 0)
            await ctx.Interaction.SendConfirmAsync(
                    $"Successfully updated MultiGreet #{id} to delete after {TimeSpan.FromSeconds(howlong).Humanize()}.")
                .ConfigureAwait(false);
        else
            await ctx.Interaction.SendConfirmAsync($"MultiGreet #{id} will no longer delete.").ConfigureAwait(false);
    }

    /// <summary>
    ///     Disables a MultiGreet.
    /// </summary>
    /// <param name="num">The id of the MultiGreet to disable</param>
    /// <param name="enabled">Whether to disable the MultiGreet</param>
    [SlashCommand("disable", "Disable a MultiGreet using its Id")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    [CheckPermissions]
    public async Task MultiGreetDisable(int num, bool enabled)
    {
        var greet = (await Service.GetGreets(ctx.Guild.Id)).ElementAt(num - 1);
        if (greet is null)
        {
            await ctx.Interaction.SendErrorAsync("That MultiGreet does not exist!", Config).ConfigureAwait(false);
            return;
        }

        await Service.MultiGreetDisable(greet, enabled).ConfigureAwait(false);
        await ctx.Interaction.SendConfirmAsync($"MultiGreet {num} set to {(enabled ? "Enabled" : "Disabled")}").ConfigureAwait(false);
    }

    /// <summary>
    ///     Changes the type of MultiGreet.
    /// </summary>
    /// <param name="types">The type of MultiGreet to set</param>
    [SlashCommand("type", "Enable RandomGreet, MultiGreet, or turn off the entire system.")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    [CheckPermissions]
    public async Task MultiGreetType(MultiGreetTypes types)
    {
        switch (types)
        {
            case MultiGreetTypes.MultiGreet:
                await Service.SetMultiGreetType(ctx.Guild, 0).ConfigureAwait(false);
                await ctx.Interaction.SendConfirmAsync("Regular MultiGreet enabled!").ConfigureAwait(false);
                break;
            case MultiGreetTypes.RandomGreet:
                await Service.SetMultiGreetType(ctx.Guild, 1).ConfigureAwait(false);
                await ctx.Interaction.SendConfirmAsync("RandomGreet enabled!").ConfigureAwait(false);
                break;
            case MultiGreetTypes.Off:
                await Service.SetMultiGreetType(ctx.Guild, 3).ConfigureAwait(false);
                await ctx.Interaction.SendConfirmAsync("MultiGreets Disabled!").ConfigureAwait(false);
                break;
        }
    }

    /// <summary>
    ///     Changes the webhook for a MultiGreet.
    /// </summary>
    /// <param name="id">The id of the MultiGreet to change</param>
    /// <param name="name">The name of the webhook</param>
    /// <param name="avatar">The avatar of the webhook</param>
    [SlashCommand("webhook", "Set a custom name and avatar to use for each MultiGreet")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    [RequireBotPermission(GuildPermission.ManageWebhooks)]
    [CheckPermissions]
    public async Task MultiGreetWebhook(int id, string? name = null, string? avatar = null)
    {
        var greet = (await Service.GetGreets(ctx.Guild.Id)).ElementAt(id - 1);
        if (greet is null)
        {
            await ctx.Interaction.SendErrorAsync("No MultiGreet found for that Id!", Config).ConfigureAwait(false);
            return;
        }

        if (name is null)
        {
            await Service.ChangeMgWebhook(greet, null).ConfigureAwait(false);
            await ctx.Interaction.SendConfirmAsync($"Webhook disabled for MultiGreet #{id}!").ConfigureAwait(false);
            return;
        }

        var channel = await ctx.Guild.GetTextChannelAsync(greet.ChannelId).ConfigureAwait(false);
        if (avatar is not null)
        {
            if (!Uri.IsWellFormedUriString(avatar, UriKind.Absolute))
            {
                await ctx.Interaction.SendErrorAsync(
                        "The avatar url used is not a direct url or is invalid! Please use a different url.", Config)
                    .ConfigureAwait(false);
                return;
            }

            var http = new HttpClient();
            using var sr = await http.GetAsync(avatar, HttpCompletionOption.ResponseHeadersRead)
                .ConfigureAwait(false);
            var imgData = await sr.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            var imgStream = imgData.ToStream();
            await using var _ = imgStream.ConfigureAwait(false);
            var webhook = await channel.CreateWebhookAsync(name, imgStream).ConfigureAwait(false);
            await Service.ChangeMgWebhook(greet, $"https://discord.com/api/webhooks/{webhook.Id}/{webhook.Token}")
                .ConfigureAwait(false);
            await ctx.Interaction.SendConfirmAsync("Webhook set!").ConfigureAwait(false);
        }
        else
        {
            var webhook = await channel.CreateWebhookAsync(name).ConfigureAwait(false);
            await Service.ChangeMgWebhook(greet, $"https://discord.com/api/webhooks/{webhook.Id}/{webhook.Token}")
                .ConfigureAwait(false);
            await ctx.Interaction.SendConfirmAsync("Webhook set!").ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Changes the message for a MultiGreet.
    /// </summary>
    /// <param name="id">The id of the MultiGreet to change</param>
    /// <param name="message">The message to set</param>
    [SlashCommand("message",
        "Set a custom message for each MultiGreet. https://mewdeko.tech/placeholders https://eb.mewdeko.tech")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    [CheckPermissions]
    public async Task MultiGreetMessage(int id, string? message = null)
    {
        await ctx.Interaction.DeferAsync().ConfigureAwait(false);
        var greet = (await Service.GetGreets(ctx.Guild.Id)).ElementAt(id - 1);
        if (greet is null)
        {
            await ctx.Interaction.SendErrorFollowupAsync("No MultiGreet found for that Id!", Config)
                .ConfigureAwait(false);
            return;
        }

        if (message is null)
        {
            var components = new ComponentBuilder().WithButton("Preview", "preview").WithButton("Regular", "regular");
            var msg = await ctx.Interaction.SendConfirmFollowupAsync(
                "Would you like to view this as regular text or would you like to preview how it actually looks?",
                components).ConfigureAwait(false);
            var response = await GetButtonInputAsync(ctx.Interaction.Id, msg.Id, ctx.User.Id).ConfigureAwait(false);
            switch (response)
            {
                case "preview":
                    await msg.DeleteAsync().ConfigureAwait(false);
                    var replacer = new ReplacementBuilder().WithUser(ctx.User)
                        .WithClient(ctx.Client as DiscordShardedClient)
                        .WithServer(ctx.Client as DiscordShardedClient, ctx.Guild as SocketGuild).Build();
                    var content = replacer.Replace(greet.Message);
                    if (SmartEmbed.TryParse(content, ctx.Guild.Id, out var embedData, out var plainText,
                            out var components2))
                    {
                        await ctx.Interaction
                            .FollowupAsync(plainText, embedData, components: components2.Build())
                            .ConfigureAwait(false);
                    }
                    else
                    {
                        await ctx.Interaction.FollowupAsync(content).ConfigureAwait(false);
                    }

                    break;
                case "regular":
                    await msg.DeleteAsync().ConfigureAwait(false);
                    await ctx.Interaction.SendConfirmFollowupAsync(greet.Message).ConfigureAwait(false);
                    break;
            }
        }

        await Service.ChangeMgMessage(greet, message).ConfigureAwait(false);
        await ctx.Interaction.SendConfirmFollowupAsync($"MultiGreet Message for MultiGreet #{id} set!")
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Lists all MultiGreets.
    /// </summary>
    [SlashCommand("list", "Lists all current MultiGreets")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    [CheckPermissions]
    public async Task MultiGreetList()
    {
        var greets = await Service.GetGreets(ctx.Guild.Id);
        if (!greets.Any())
        {
            await ctx.Interaction.SendErrorAsync("No MultiGreets setup!", Config).ConfigureAwait(false);
        }

        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(greets.Length - 1)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await interactivity.SendPaginatorAsync(paginator, (ctx.Interaction as SocketInteraction)!,
            TimeSpan.FromMinutes(60)).ConfigureAwait(false);

        async Task<PageBuilder> PageFactory(int page)
        {
            var curgreet = greets.Skip(page).FirstOrDefault();
            return new PageBuilder().WithDescription(
                    $"#{Array.IndexOf(greets, curgreet) + 1}\n`Channel:` {((await ctx.Guild.GetTextChannelAsync(curgreet.ChannelId).ConfigureAwait(false))?.Mention == null ? "Deleted" : (await ctx.Guild.GetTextChannelAsync(curgreet.ChannelId).ConfigureAwait(false))?.Mention)} {curgreet.ChannelId}\n`Delete After:` {curgreet.DeleteTime}s\n`Webhook:` {curgreet.WebhookUrl != null}\n`Greet Bots:` {curgreet.GreetBots}\n`Message:` {curgreet.Message.TrimTo(1000)}")
                .WithOkColor();
        }
    }
}