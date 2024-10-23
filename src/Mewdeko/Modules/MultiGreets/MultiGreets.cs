using System.Net.Http;
using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Humanizer;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.MultiGreets.Services;

namespace Mewdeko.Modules.MultiGreets;

/// <summary>
///     Module for MultiGreets.
/// </summary>
/// <param name="interactivity"></param>
public class MultiGreets(InteractiveService interactivity) : MewdekoModuleBase<MultiGreetService>
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

    /// <summary>
    ///     Adds a MultiGreet channel.
    /// </summary>
    /// <param name="channel">The channel to add</param>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    [RequireContext(ContextType.Guild)]
    public async Task MultiGreetAdd([Remainder] ITextChannel? channel = null)
    {
        channel ??= ctx.Channel as ITextChannel;
        var added = await Service.AddMultiGreet(ctx.Guild.Id, channel.Id);
        switch (added)
        {
            case true:
                await ctx.Channel.SendConfirmAsync($"Added {channel.Mention} as a MultiGreet channel!")
                    .ConfigureAwait(false);
                break;
            case false:
                await ctx.Channel.SendErrorAsync(
                        "Seems like you have reached your 5 greets per channel limit or your 30 greets per guild limit! Remove a MultiGreet and try again",
                        Config)
                    .ConfigureAwait(false);
                break;
        }
    }

    /// <summary>
    ///     Removes a MultiGreet channel.
    /// </summary>
    /// <param name="id">The id of the MultiGreet to remove</param>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    [RequireContext(ContextType.Guild)]
    public async Task MultiGreetRemove(int id)
    {
        var greet = (await Service.GetGreets(ctx.Guild.Id)).ElementAt(id - 1);
        if (greet is null)
        {
            await ctx.Channel.SendErrorAsync("No greet with that ID found!", Config).ConfigureAwait(false);
            return;
        }

        await Service.RemoveMultiGreetInternal(greet).ConfigureAwait(false);
        await ctx.Channel.SendConfirmAsync("MultiGreet removed!").ConfigureAwait(false);
    }

    /// <summary>
    ///     Removes all MultiGreets from a channel.
    /// </summary>
    /// <param name="channel">The channel to remove MultiGreets from</param>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    [RequireContext(ContextType.Guild)]
    public async Task MultiGreetRemove([Remainder] ITextChannel channel)
    {
        var greet = (await Service.GetGreets(ctx.Guild.Id)).Where(x => x.ChannelId == channel.Id);
        if (!greet.Any())
        {
            await ctx.Channel.SendErrorAsync("There are no greets in that channel!", Config).ConfigureAwait(false);
            return;
        }

        if (await PromptUserConfirmAsync(
                    new EmbedBuilder().WithOkColor()
                        .WithDescription("Are you sure you want to remove all MultiGreets for this channel?"),
                    ctx.User.Id)
                .ConfigureAwait(false))
        {
            await Service.MultiRemoveMultiGreetInternal(greet.ToArray()).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync("MultiGreets removed!").ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Changes the delete time for a MultiGreet message.
    /// </summary>
    /// <param name="id">The id of the MultiGreet to change</param>
    /// <param name="time">The time to delete the message after</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    [RequireBotPermission(GuildPermission.ManageMessages)]
    public async Task MultiGreetDelete(int id, StoopidTime time)
    {
        var greet = (await Service.GetGreets(ctx.Guild.Id)).ElementAt(id - 1);
        if (greet is null)
        {
            await ctx.Channel.SendErrorAsync("No MultiGreet found for that Id!", Config).ConfigureAwait(false);
            return;
        }

        await Service.ChangeMgDelete(greet, Convert.ToInt32(time.Time.TotalSeconds)).ConfigureAwait(false);
        await ctx.Channel.SendConfirmAsync(
            $"Successfully updated MultiGreet #{id} to delete after {time.Time.Humanize()}.").ConfigureAwait(false);
    }

    /// <summary>
    ///     Changes the delete time for a MultiGreet message in seconds.
    /// </summary>
    /// <param name="id">The id of the MultiGreet to change</param>
    /// <param name="howlong">The time to delete the message after in seconds</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    [RequireBotPermission(GuildPermission.ManageMessages)]
    public async Task MultiGreetDelete(int id, int howlong)
    {
        var greet = (await Service.GetGreets(ctx.Guild.Id)).ElementAt(id - 1);
        if (greet is null)
        {
            await ctx.Channel.SendErrorAsync("No MultiGreet found for that Id!", Config).ConfigureAwait(false);
            return;
        }

        await Service.ChangeMgDelete(greet, howlong).ConfigureAwait(false);
        if (howlong > 0)
            await ctx.Channel.SendConfirmAsync(
                    $"Successfully updated MultiGreet #{id} to delete after {TimeSpan.FromSeconds(howlong).Humanize()}.")
                .ConfigureAwait(false);
        else
            await ctx.Channel.SendConfirmAsync($"MultiGreet #{id} will no longer delete.").ConfigureAwait(false);
    }

    /// <summary>
    ///     Disables a MultiGreet.
    /// </summary>
    /// <param name="num">The id of the MultiGreet to disable</param>
    /// <param name="enabled">Whether to disable the MultiGreet</param>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    [RequireContext(ContextType.Guild)]
    public async Task MultiGreetDisable(int num, bool enabled)
    {
        var greet = (await Service.GetGreets(ctx.Guild.Id))?.ElementAt(num - 1);
        if (greet is null)
        {
            await ctx.Channel.SendErrorAsync("That MultiGreet does not exist!", Config).ConfigureAwait(false);
            return;
        }

        await Service.MultiGreetDisable(greet, enabled).ConfigureAwait(false);
        await ctx.Channel.SendConfirmAsync($"MultiGreet {num} set to {(enabled ? "Enabled" : "Disabled")}").ConfigureAwait(false);
    }

    /// <summary>
    ///     Changes the type of MultiGreet.
    /// </summary>
    /// <param name="types">The type of MultiGreet to set</param>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    [RequireContext(ContextType.Guild)]
    public async Task MultiGreetType(MultiGreetTypes types)
    {
        switch (types)
        {
            case MultiGreetTypes.MultiGreet:
                await Service.SetMultiGreetType(ctx.Guild, 0).ConfigureAwait(false);
                await ctx.Channel.SendConfirmAsync("Regular MultiGreet enabled!").ConfigureAwait(false);
                break;
            case MultiGreetTypes.RandomGreet:
                await Service.SetMultiGreetType(ctx.Guild, 1).ConfigureAwait(false);
                await ctx.Channel.SendConfirmAsync("RandomGreet enabled!").ConfigureAwait(false);
                break;
            case MultiGreetTypes.Off:
                await Service.SetMultiGreetType(ctx.Guild, 3).ConfigureAwait(false);
                await ctx.Channel.SendConfirmAsync("MultiGreets Disabled!").ConfigureAwait(false);
                break;
        }
    }

    /// <summary>
    ///     Changes whether a MultiGreet greets bots.
    /// </summary>
    /// <param name="num">The id of the MultiGreet to change</param>
    /// <param name="enabled">Whether to greet bots</param>
    [Cmd]
    [Alias]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    public async Task MultiGreetGreetBots(int num, bool enabled)
    {
        var greet = (await Service.GetGreets(ctx.Guild.Id)).ElementAt(num - 1);
        if (greet is null)
        {
            await ctx.Channel.SendErrorAsync("That MultiGreet does not exist!", Config).ConfigureAwait(false);
            return;
        }

        await Service.ChangeMgGb(greet, enabled).ConfigureAwait(false);
        await ctx.Channel.SendConfirmAsync($"MultiGreet {num} GreetBots set to {enabled}").ConfigureAwait(false);
    }

    /// <summary>
    ///     Changes the webhook for a MultiGreet.
    /// </summary>
    /// <param name="id">The id of the MultiGreet to change</param>
    /// <param name="name">The name of the webhook</param>
    /// <param name="avatar">The avatar of the webhook</param>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.Administrator)]
    [RequireBotPermission(GuildPermission.ManageWebhooks)]
    public async Task MultiGreetWebhook(int id, string? name = null, string? avatar = null)
    {
        var greet = (await Service.GetGreets(ctx.Guild.Id)).ElementAt(id - 1);
        if (greet is null)
        {
            await ctx.Channel.SendErrorAsync("No MultiGreet found for that Id!", Config).ConfigureAwait(false);
            return;
        }

        if (name is null)
        {
            await Service.ChangeMgWebhook(greet, null).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync($"Webhook disabled for MultiGreet #{id}!").ConfigureAwait(false);
            return;
        }

        var channel = await ctx.Guild.GetTextChannelAsync(greet.ChannelId).ConfigureAwait(false);
        if (avatar is not null)
        {
            if (!Uri.IsWellFormedUriString(avatar, UriKind.Absolute))
            {
                await ctx.Channel.SendErrorAsync(
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
            await ctx.Channel.SendConfirmAsync("Webhook set!").ConfigureAwait(false);
        }
        else
        {
            var webhook = await channel.CreateWebhookAsync(name).ConfigureAwait(false);
            await Service.ChangeMgWebhook(greet, $"https://discord.com/api/webhooks/{webhook.Id}/{webhook.Token}")
                .ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync("Webhook set!").ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Changes the message for a MultiGreet.
    /// </summary>
    /// <param name="id">The id of the MultiGreet to change</param>
    /// <param name="message">The message to set</param>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    [RequireContext(ContextType.Guild)]
    public async Task MultiGreetMessage(int id, [Remainder] string? message = null)
    {
        var greet = (await Service.GetGreets(ctx.Guild.Id)).ElementAt(id - 1);
        if (greet is null)
        {
            await ctx.Channel.SendErrorAsync("No MultiGreet found for that Id!", Config).ConfigureAwait(false);
            return;
        }

        if (message is null)
        {
            var components = new ComponentBuilder().WithButton("Preview", "preview").WithButton("Regular", "regular");
            var msg = await ctx.Channel.SendConfirmAsync(
                "Would you like to view this as regular text or would you like to preview how it actually looks?",
                components).ConfigureAwait(false);
            var response = await GetButtonInputAsync(ctx.Channel.Id, msg.Id, ctx.User.Id).ConfigureAwait(false);
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
                        await ctx.Channel
                            .SendMessageAsync(plainText, embeds: embedData, components: components2.Build())
                            .ConfigureAwait(false);
                    }
                    else
                    {
                        await ctx.Channel.SendMessageAsync(content).ConfigureAwait(false);
                        return;
                    }

                    break;
                case "regular":
                    await msg.DeleteAsync().ConfigureAwait(false);
                    await ctx.Channel.SendConfirmAsync(greet.Message).ConfigureAwait(false);
                    return;
            }
        }

        await Service.ChangeMgMessage(greet, message).ConfigureAwait(false);
        await ctx.Channel.SendConfirmAsync($"MultiGreet Message for MultiGreet #{id} set!").ConfigureAwait(false);
    }

    /// <summary>
    ///     Lists all MultiGreets.
    /// </summary>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    [RequireContext(ContextType.Guild)]
    public async Task MultiGreetList()
    {
        var greets = await Service.GetGreets(ctx.Guild.Id);
        if (!greets.Any())
        {
            await ctx.Channel.SendErrorAsync("No MultiGreets setup!", Config).ConfigureAwait(false);
        }

        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(greets.Length - 1)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await interactivity.SendPaginatorAsync(paginator, Context.Channel,
            TimeSpan.FromMinutes(60)).ConfigureAwait(false);

        async Task<PageBuilder> PageFactory(int page)
        {
            var curgreet = greets.Skip(page).FirstOrDefault();
            return new PageBuilder().WithDescription(
                    $"#{Array.IndexOf(greets, curgreet) + 1}\n`Channel:` {((await ctx.Guild.GetTextChannelAsync(curgreet.ChannelId).ConfigureAwait(false))?.Mention == null ? "Deleted" : (await ctx.Guild.GetTextChannelAsync(curgreet.ChannelId).ConfigureAwait(false))?.Mention)} {curgreet.ChannelId}\n`Delete After:` {curgreet.DeleteTime}s\n`Webhook:` {curgreet.WebhookUrl != null}\n`Greet Bots:` {curgreet.GreetBots}\n`Disabled`: {curgreet.Disabled}\n`Message:` {curgreet.Message.TrimTo(1000)}")
                .WithOkColor();
        }
    }
}