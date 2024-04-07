using System.Globalization;
using System.Net.Http;
using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Humanizer;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.RoleGreets.Services;

namespace Mewdeko.Modules.RoleGreets;

/// <summary>
/// The RoleGreets module provides commands for managing role greet messages within a Discord guild.
/// These messages are sent when a user receives a specific role, allowing for custom greetings or information to be shared automatically.
/// </summary>
public class RoleGreets(InteractiveService interactivity, HttpClient http) : MewdekoModuleBase<RoleGreetService>
{
    /// <summary>
    /// Adds a new role greet message for a specified role. If no channel is specified, the current channel is used.
    /// </summary>
    /// <param name="role">The role for which the greet message is being set.</param>
    /// <param name="channel">The text channel where the greet message will be sent. Optional.</param>
    [Cmd, Aliases, UserPerm(GuildPermission.Administrator), RequireContext(ContextType.Guild)]
    public async Task RoleGreetAdd(IRole role, [Remainder] ITextChannel? channel = null)
    {
        channel ??= ctx.Channel as ITextChannel;
        switch (await Service.AddRoleGreet(ctx.Guild.Id, channel.Id, role.Id))
        {
            case true:
                await ctx.Channel.SendConfirmAsync($"Added {role.Mention} to greet in {channel.Mention}!")
                    .ConfigureAwait(false);
                break;
            case false:
                await ctx.Channel.SendErrorAsync(
                        "Seems like you reached your maximum of 10 RoleGreets! Please remove one to continue.")
                    .ConfigureAwait(false);
                break;
        }
    }

    /// <summary>
    /// Removes a role greet message by its list ID.
    /// </summary>
    /// <param name="id">The ID of the role greet to remove, based on its position in the list of all role greets.</param>
    [Cmd, Aliases, UserPerm(GuildPermission.Administrator), RequireContext(ContextType.Guild)]
    public async Task RoleGreetRemove(int id)
    {
        var greet = Service.GetListGreets(ctx.Guild.Id)?.ElementAt(id - 1);
        if (greet is null)
        {
            await ctx.Channel.SendErrorAsync("No greet with that ID found!").ConfigureAwait(false);
            return;
        }

        await Service.RemoveRoleGreetInternal(greet).ConfigureAwait(false);
        await ctx.Channel.SendConfirmAsync("RoleGreet removed!").ConfigureAwait(false);
    }

    /// <summary>
    /// Updates a role greet message to be automatically deleted after a specified duration.
    /// </summary>
    /// <param name="id">The ID of the role greet to update, based on its position in the list of all role greets.</param>
    /// <param name="time">The duration after which the greet message will be deleted, specified in seconds or as a human-readable string.</param>
    [Cmd, Aliases, UserPerm(GuildPermission.Administrator), RequireContext(ContextType.Guild)]
    public async Task RoleGreetRemove([Remainder] IRole role)
    {
        var greet = Service.GetListGreets(ctx.Guild.Id).Where(x => x.RoleId == role.Id);
        if (!greet.Any())
        {
            await ctx.Channel.SendErrorAsync("There are no greets for that role!").ConfigureAwait(false);
            return;
        }

        if (await PromptUserConfirmAsync(
                    new EmbedBuilder().WithOkColor()
                        .WithDescription("Are you sure you want to remove all RoleGreets for this role?"), ctx.User.Id)
                .ConfigureAwait(false))
        {
            await Service.MultiRemoveRoleGreetInternal(greet.ToArray()).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync("RoleGreets removed!").ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Updates a role greet message to be automatically deleted after a specified duration.
    /// </summary>
    /// <param name="id">The ID of the role greet to update, based on its position in the list of all role greets.</param>
    /// <param name="time">The duration after which the greet message will be deleted, specified in seconds or as a human-readable string.</param>
    [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator),
     RequireBotPermission(GuildPermission.ManageMessages)]
    public async Task RoleGreetDelete(int id, StoopidTime time)
    {
        var greet = Service.GetListGreets(ctx.Guild.Id)?.ElementAt(id - 1);
        if (greet is null)
        {
            await ctx.Channel.SendErrorAsync("No RoleGreet found for that Id!").ConfigureAwait(false);
            return;
        }

        await Service.ChangeRgDelete(greet, int.Parse(time.Time.TotalSeconds.ToString(CultureInfo.InvariantCulture)))
            .ConfigureAwait(false);
        await ctx.Channel.SendConfirmAsync(
            $"Successfully updated RoleGreet #{id} to delete after {time.Time.Humanize()}.").ConfigureAwait(false);
    }

    /// <summary>
    /// Updates a role greet message to be automatically deleted after a specified duration.
    /// </summary>
    /// <param name="id">The ID of the role greet to update, based on its position in the list of all role greets.</param>
    /// <param name="howlong">The duration, in seconds, after which the greet message will be deleted.</param>
    [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator),
     RequireBotPermission(GuildPermission.ManageMessages)]
    public async Task RoleGreetDelete(int id, int howlong)
    {
        var greet = Service.GetListGreets(ctx.Guild.Id)?.ElementAt(id - 1);
        if (greet is null)
        {
            await ctx.Channel.SendErrorAsync("No RoleGreet found for that Id!").ConfigureAwait(false);
            return;
        }

        await Service.ChangeRgDelete(greet, howlong).ConfigureAwait(false);
        if (howlong > 0)
        {
            await ctx.Channel.SendConfirmAsync(
                    $"Successfully updated RoleGreet #{id} to delete after {TimeSpan.FromSeconds(howlong).Humanize()}.")
                .ConfigureAwait(false);
        }
        else
        {
            await ctx.Channel.SendConfirmAsync($"RoleGreet #{id} will no longer delete.").ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Enables or disables the greeting of bots by a specific role greet.
    /// </summary>
    /// <param name="num">The ID of the role greet to update, based on its position in the list of all role greets.</param>
    /// <param name="enabled">Whether to enable or disable greeting bots.</param>
    [Cmd, Aliases, UserPerm(GuildPermission.Administrator), RequireContext(ContextType.Guild)]
    public async Task RoleGreetGreetBots(int num, bool enabled)
    {
        var greet = Service.GetListGreets(ctx.Guild.Id)?.ElementAt(num - 1);
        if (greet is null)
        {
            await ctx.Channel.SendErrorAsync("That RoleGreet does not exist!").ConfigureAwait(false);
            return;
        }

        await Service.ChangeRgGb(greet, enabled).ConfigureAwait(false);
        await ctx.Channel.SendConfirmAsync($"RoleGreet {num} GreetBots set to {enabled}").ConfigureAwait(false);
    }

    /// <summary>
    /// Enables or disables a role greet message.
    /// </summary>
    /// <param name="num">The ID of the role greet to update, based on its position in the list of all role greets.</param>
    /// <param name="enabled">Whether to enable or disable the greet message.</param>
    [Cmd, Aliases, UserPerm(GuildPermission.Administrator), RequireContext(ContextType.Guild)]
    public async Task RoleGreetDisable(int num, bool enabled)
    {
        var greet = Service.GetListGreets(ctx.Guild.Id)?.ElementAt(num - 1);
        if (greet is null)
        {
            await ctx.Channel.SendErrorAsync("That RoleGreet does not exist!").ConfigureAwait(false);
            return;
        }

        await Service.RoleGreetDisable(greet, enabled).ConfigureAwait(false);
        await ctx.Channel.SendConfirmAsync($"RoleGreet {num} set to {enabled}").ConfigureAwait(false);
    }

    /// <summary>
    /// Configures a webhook for a role greet message, allowing for custom name and avatar. If no name is provided, the webhook is disabled.
    /// </summary>
    /// <param name="id">The ID of the role greet to configure the webhook for, based on its position in the list of all role greets.</param>
    /// <param name="name">The name of the webhook. Optional.</param>
    /// <param name="avatar">The URL of the avatar for the webhook. Optional.</param>
    [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator),
     RequireBotPermission(GuildPermission.ManageWebhooks)]
    public async Task RoleGreetWebhook(int id, string? name = null, string? avatar = null)
    {
        var greet = Service.GetListGreets(ctx.Guild.Id)?.ElementAt(id - 1);
        if (greet is null)
        {
            await ctx.Channel.SendErrorAsync("No RoleGreet found for that Id!").ConfigureAwait(false);
            return;
        }

        if (name is null)
        {
            await Service.ChangeMgWebhook(greet, null).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync($"Webhook disabled for RoleGreet #{id}!").ConfigureAwait(false);
            return;
        }

        var channel = await ctx.Guild.GetTextChannelAsync(greet.ChannelId).ConfigureAwait(false);
        if (avatar is not null)
        {
            if (!Uri.IsWellFormedUriString(avatar, UriKind.Absolute))
            {
                await ctx.Channel.SendErrorAsync(
                        "The avatar url used is not a direct url or is invalid! Please use a different url.")
                    .ConfigureAwait(false);
                return;
            }

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
    /// Updates the message content of a role greet. If no message is provided, presents options to preview the message as is or view it as regular text.
    /// </summary>
    /// <param name="id">The ID of the role greet to update, based on its position in the list of all role greets.</param>
    /// <param name="message">The new message content. Optional.</param>
    [Cmd, Aliases, UserPerm(GuildPermission.Administrator), RequireContext(ContextType.Guild)]
    public async Task RoleGreetMessage(int id, [Remainder] string? message = null)
    {
        var greet = Service.GetListGreets(ctx.Guild.Id)?.ElementAt(id - 1);
        if (greet is null)
        {
            await ctx.Channel.SendErrorAsync("No RoleGreet found for that Id!").ConfigureAwait(false);
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
                        .WithClient(ctx.Client as DiscordSocketClient)
                        .WithServer(ctx.Client as DiscordSocketClient, ctx.Guild as SocketGuild).Build();
                    var content = replacer.Replace(greet.Message);
                    if (SmartEmbed.TryParse(content, ctx.Guild?.Id, out var embedData, out var plainText, out var cb))
                    {
                        await ctx.Channel.SendMessageAsync(plainText, embeds: embedData,
                            components: cb.Build()).ConfigureAwait(false);
                        return;
                    }

                    await ctx.Channel.SendMessageAsync(content).ConfigureAwait(false);
                    return;
                case "regular":
                    await msg.DeleteAsync().ConfigureAwait(false);
                    await ctx.Channel.SendConfirmAsync(greet.Message).ConfigureAwait(false);
                    return;
            }
        }

        await Service.ChangeMgMessage(greet, message).ConfigureAwait(false);
        await ctx.Channel.SendConfirmAsync($"RoleGreet Message for RoleGreet #{id} set!").ConfigureAwait(false);
    }

    /// <summary>
    /// Lists all role greets set up in the guild, providing detailed information for each.
    /// </summary>
    [Cmd, Aliases, UserPerm(GuildPermission.Administrator), RequireContext(ContextType.Guild)]
    public async Task RoleGreetList()
    {
        var greets = Service.GetListGreets(ctx.Guild.Id);
        if (greets.Length == 0)
        {
            await ctx.Channel.SendErrorAsync("No RoleGreets setup!").ConfigureAwait(false);
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
                    $"#{Array.IndexOf(greets, curgreet) + 1}\n`Role:` {(ctx.Guild.GetRole(curgreet.RoleId)?.Mention == null ? "Deleted" : ctx.Guild.GetRole(curgreet.RoleId)?.Mention)} `{curgreet.RoleId}`\n`Channel:` {((await ctx.Guild.GetTextChannelAsync(curgreet.ChannelId).ConfigureAwait(false))?.Mention == null ? "Deleted" : (await ctx.Guild.GetTextChannelAsync(curgreet.ChannelId).ConfigureAwait(false))?.Mention)} {curgreet.ChannelId}\n`Delete After:` {curgreet.DeleteTime}s\n`Disabled:` {curgreet.Disabled}\n`Webhook:` {curgreet.WebhookUrl != null}\n`Greet Bots:` {curgreet.GreetBots}\n`Message:` {curgreet.Message.TrimTo(1000)}")
                .WithOkColor();
        }
    }
}