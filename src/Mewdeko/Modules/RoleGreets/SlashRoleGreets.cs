using System.Net.Http;
using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Humanizer;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.RoleGreets.Services;

namespace Mewdeko.Modules.RoleGreets;

/// <summary>
/// Provides slash commands for managing role greet messages in a Discord guild. Role greets are messages automatically sent when a user receives a specific role.
/// </summary>
[Group("rolegreets", "Set or manage RoleGreets.")]
public class SlashRoleGreets(InteractiveService interactivity, HttpClient httpClient)
    : MewdekoSlashModuleBase<RoleGreetService>
{
    /// <summary>
    /// Adds a greet message for a specific role. Optionally, specify a channel where the greet message will be sent.
    /// </summary>
    /// <param name="role">The role to set the greet for.</param>
    /// <param name="channel">The channel where the greet message will be sent. Defaults to the current channel if not specified.</param>
    [SlashCommand("add", "Add a role to RoleGreets."), SlashUserPerm(GuildPermission.Administrator),
     RequireContext(ContextType.Guild), CheckPermissions]
    public async Task RoleGreetAdd(IRole role, ITextChannel? channel = null)
    {
        channel ??= ctx.Channel as ITextChannel;
        switch (await Service.AddRoleGreet(ctx.Guild.Id, channel.Id, role.Id))
        {
            case true:
                await ctx.Interaction.SendConfirmAsync($"Added {role.Mention} to greet in {channel.Mention}!")
                    .ConfigureAwait(false);
                break;
            case false:
                await ctx.Interaction.SendErrorAsync(
                        "Seems like you have reached your max of 10 RoleGreets! Please remove one to add another one.",
                        Config)
                    .ConfigureAwait(false);
                break;
        }
    }

    /// <summary>
    /// Sets whether bots will receive greet messages.
    /// </summary>
    /// <param name="num">The ID of the RoleGreet to modify.</param>
    /// <param name="enabled">Whether to greet bots or not.</param>
    [SlashCommand("greetbots", "Set whether to greet bots when triggered."), RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.Administrator), CheckPermissions]
    public async Task RoleGreetGreetBots(int num, bool enabled)
    {
        var greet = (await Service.GetGreets(ctx.Guild.Id)).ElementAt(num - 1);
        if (greet is null)
        {
            await ctx.Interaction.SendErrorAsync("That RoleGreet does not exist!", Config).ConfigureAwait(false);
            return;
        }

        await Service.ChangeRgGb(greet, enabled).ConfigureAwait(false);
        await ctx.Interaction.SendConfirmAsync($"RoleGreet {num} GreetBots set to {enabled}").ConfigureAwait(false);
    }

    /// <summary>
    /// Removes a greet message by its ID.
    /// </summary>
    /// <param name="id">The ID of the RoleGreet to remove.</param>
    [SlashCommand("remove", "Remove a channel from RoleGreets"), RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.Administrator), CheckPermissions]
    public async Task RoleGreetRemove(int id)
    {
        var greet = (await Service.GetGreets(ctx.Guild.Id)).ElementAt(id - 1);
        if (greet is null)
        {
            await ctx.Interaction.SendErrorAsync("No greet with that ID found!", Config).ConfigureAwait(false);
            return;
        }

        await Service.RemoveRoleGreetInternal(greet).ConfigureAwait(false);
        await ctx.Interaction.SendConfirmAsync("RoleGreet removed!").ConfigureAwait(false);
    }

    /// <summary>
    /// Removes all greet messages for a specific role.
    /// </summary>
    /// <param name="role">The role for which to remove all greet messages.</param>
    [SlashCommand("removerole", "Removes all RoleGreets on that channel."), RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.Administrator), CheckPermissions]
    public async Task RoleGreetRemove(IRole role)
    {
        await ctx.Interaction.DeferAsync().ConfigureAwait(false);
        var greet = (await Service.GetGreets(ctx.Guild.Id)).Where(x => x.RoleId == role.Id);
        if (!greet.Any())
        {
            await ctx.Interaction.SendErrorFollowupAsync("There are no greets for that role!", Config)
                .ConfigureAwait(false);
            return;
        }

        if (await PromptUserConfirmAsync(
                    new EmbedBuilder().WithOkColor()
                        .WithDescription("Are you sure you want to remove all RoleGreets for this role?"), ctx.User.Id)
                .ConfigureAwait(false))
        {
            await Service.MultiRemoveRoleGreetInternal(greet.ToArray()).ConfigureAwait(false);
            await ctx.Interaction.SendConfirmFollowupAsync("RoleGreets removed!").ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Sets the duration after which a greet message will be automatically deleted.
    /// </summary>
    /// <param name="id">The ID of the RoleGreet to modify.</param>
    /// <param name="howlong">The time in seconds after which the message will be deleted.</param>
    [SlashCommand("delete", "Set how long it takes for a greet to delete"), RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.Administrator),
     RequireBotPermission(GuildPermission.ManageMessages), CheckPermissions]
    public async Task RoleGreetDelete(int id,
        [Summary("Seconds", "After how long in seconds it should delete.")]
        int howlong)
    {
        var greet = (await Service.GetGreets(ctx.Guild.Id)).ElementAt(id - 1);
        if (greet is null)
        {
            await ctx.Interaction.SendErrorAsync("No RoleGreet found for that Id!", Config).ConfigureAwait(false);
            return;
        }

        await Service.ChangeRgDelete(greet, howlong).ConfigureAwait(false);
        if (howlong > 0)
        {
            await ctx.Interaction.SendConfirmAsync(
                    $"Successfully updated RoleGreet #{id} to delete after {TimeSpan.FromSeconds(howlong).Humanize()}.")
                .ConfigureAwait(false);
        }
        else
        {
            await ctx.Interaction.SendConfirmAsync($"RoleGreet #{id} will no longer delete.").ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Enables or disables a RoleGreet.
    /// </summary>
    /// <param name="num">The ID of the RoleGreet to modify.</param>
    /// <param name="enabled">Whether to enable or disable the greet.</param>
    [SlashCommand("disable", "Disable a RoleGreet using its Id"), RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.Administrator), CheckPermissions]
    public async Task RoleGreetDisable(int num, bool enabled)
    {
        var greet = (await Service.GetGreets(ctx.Guild.Id)).ElementAt(num - 1);
        if (greet is null)
        {
            await ctx.Interaction.SendErrorAsync("That RoleGreet does not exist!", Config).ConfigureAwait(false);
            return;
        }

        await Service.RoleGreetDisable(greet, enabled).ConfigureAwait(false);
        await ctx.Interaction.SendConfirmAsync($"RoleGreet {num} set to {enabled}").ConfigureAwait(false);
    }

    /// <summary>
    /// Configures a webhook for a RoleGreet, allowing for custom name and avatar. Pass null for name to disable.
    /// </summary>
    /// <param name="id">The ID of the RoleGreet to configure.</param>
    /// <param name="name">The custom name for the webhook. Null to disable.</param>
    /// <param name="avatar">The URL for the custom avatar for the webhook.</param>
    [SlashCommand("webhook", "Set a custom name and avatar to use for each RoleGreet"),
     RequireContext(ContextType.Guild), SlashUserPerm(GuildPermission.Administrator),
     RequireBotPermission(GuildPermission.ManageWebhooks), CheckPermissions]
    public async Task RoleGreetWebhook(int id, string? name = null, string? avatar = null)
    {
        var greet = (await Service.GetGreets(ctx.Guild.Id)).ElementAt(id - 1);
        if (greet is null)
        {
            await ctx.Interaction.SendErrorAsync("No RoleGreet found for that Id!", Config).ConfigureAwait(false);
            return;
        }

        if (name is null)
        {
            await Service.ChangeMgWebhook(greet, null).ConfigureAwait(false);
            await ctx.Interaction.SendConfirmAsync($"Webhook disabled for RoleGreet #{id}!").ConfigureAwait(false);
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

            using var sr = await httpClient.GetAsync(avatar, HttpCompletionOption.ResponseHeadersRead)
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
    /// Sets a custom message for a RoleGreet. If no message is specified, options to preview the current message are presented.
    /// </summary>
    /// <param name="id">The ID of the RoleGreet to modify.</param>
    /// <param name="message">The custom message. Null to present preview options.</param>
    [SlashCommand("message",
         "Set a custom message for each RoleGreet. https://mewdeko.tech/placeholders https://eb.mewdeko.tech"),
     RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.Administrator), CheckPermissions]
    public async Task RoleGreetMessage(int id, string? message = null)
    {
        await ctx.Interaction.DeferAsync().ConfigureAwait(false);
        var greet = (await Service.GetGreets(ctx.Guild.Id)).ElementAt(id - 1);
        if (greet is null)
        {
            await ctx.Interaction.SendErrorFollowupAsync("No RoleGreet found for that Id!", Config)
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
                    if (SmartEmbed.TryParse(content, ctx.Guild?.Id, out var embedData, out var plainText, out var cb))
                    {
                        await ctx.Interaction.FollowupAsync(plainText, embeds: embedData, components: cb.Build())
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
        await ctx.Interaction.SendConfirmFollowupAsync($"RoleGreet Message for RoleGreet #{id} set!")
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Lists all current RoleGreets, providing details such as the role, channel, deletion timer, and message.
    /// </summary>
    [SlashCommand("list", "Lists all current RoleGreets"), RequireContext(ContextType.Guild),
     SlashUserPerm(GuildPermission.Administrator), CheckPermissions]
    public async Task RoleGreetList()
    {
        var greets = await Service.GetGreets(ctx.Guild.Id);
        if (greets.Length == 0)
        {
            await ctx.Interaction.SendErrorAsync("No RoleGreets setup!", Config).ConfigureAwait(false);
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
                    $"#{Array.IndexOf(greets, curgreet) + 1}\n`Role:` {((await ctx.Guild.GetTextChannelAsync(curgreet.RoleId).ConfigureAwait(false))?.Mention == null ? "Deleted" : (await ctx.Guild.GetTextChannelAsync(curgreet.RoleId).ConfigureAwait(false))?.Mention)} `{curgreet.RoleId}`\n`Channel:` {((await ctx.Guild.GetTextChannelAsync(curgreet.ChannelId).ConfigureAwait(false))?.Mention == null ? "Deleted" : (await ctx.Guild.GetTextChannelAsync(curgreet.ChannelId).ConfigureAwait(false))?.Mention)} {curgreet.ChannelId}\n`Delete After:` {curgreet.DeleteTime}s\n`Disabled:` {curgreet.Disabled}\n`Webhook:` {curgreet.WebhookUrl != null}\n`Greet Bots:` {curgreet.GreetBots}\n`Message:` {curgreet.Message.TrimTo(1000)}")
                .WithOkColor();
        }
    }
}