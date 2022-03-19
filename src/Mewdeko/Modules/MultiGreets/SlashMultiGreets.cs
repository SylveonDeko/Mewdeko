using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Humanizer;
using Mewdeko._Extensions;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using Mewdeko.Common.Replacements;
using Mewdeko.Database.Extensions;
using Mewdeko.Modules.MultiGreets.Services;
using System.Net.Http;

namespace Mewdeko.Modules.MultiGreets;
[Group("multigreets", "Set or manage MultiGreets.")]
public class SlashMultiGreets : MewdekoSlashModuleBase<MultiGreetService>
{
    private InteractiveService interactivity;
    public SlashMultiGreets(InteractiveService interactivity) => this.interactivity = interactivity;

    public enum MultiGreetTypes
    {
        MultiGreet,
        RandomGreet,
        Off
    }

    [SlashCommand("add","Add a channel to MultiGreets."), SlashUserPerm(GuildPermission.Administrator), CheckPermissions, BlacklistCheck]
    public async Task MultiGreetAdd(ITextChannel? channel = null)
    {
        channel ??= ctx.Channel as ITextChannel;
        var added = Service.AddMultiGreet(ctx.Guild.Id, channel.Id);
        switch (added)
        {
            case true:
                await ctx.Interaction.SendConfirmAsync($"Added {channel.Mention} as a MultiGreet channel!");
                break;
            case false:
                await ctx.Interaction.SendErrorAsync(
                    "Seems like you have reached your 5 greets per channel limit or your 30 greets per guild limit! Remove a MultiGreet and try again");
                break;
        }
    }

    [SlashCommand("remove","Remove a channel from MultiGreets"), RequireContext(ContextType.Guild), SlashUserPerm(GuildPermission.Administrator), CheckPermissions, BlacklistCheck]
    public async Task MultiGreetRemove(int id)
    {
        var greet = Service.GetGreets(ctx.Guild.Id).ElementAt(id - 1);
        if (greet is null)
        {
            await ctx.Interaction.SendErrorAsync("No greet with that ID found!");
            return;
        }

        await Service.RemoveMultiGreetInternal(greet);
        await ctx.Interaction.SendConfirmAsync("MultiGreet removed!");
    }

    [SlashCommand("removechannel","Removes all MultiGreets on that channel."),  RequireContext(ContextType.Guild), SlashUserPerm(GuildPermission.Administrator), CheckPermissions, BlacklistCheck]
    public async Task MultiGreetRemove(ITextChannel channel)
    {
        await ctx.Interaction.DeferAsync();
        var greet = Service.GetGreets(ctx.Guild.Id).Where(x => x.ChannelId == channel.Id);
        if (!greet.Any())
        {
            await ctx.Interaction.SendErrorFollowupAsync("There are no greets in that channel!");
            return;
        }

        if (await PromptUserConfirmAsync(new EmbedBuilder().WithOkColor().WithDescription("Are you sure you want to remove all MultiGreets for this channel?"), ctx.User.Id))
        {
            await Service.MultiRemoveMultiGreetInternal(greet.ToArray());
            await ctx.Interaction.SendConfirmFollowupAsync("MultiGreets removed!");
        }
    }

    [SlashCommand("delete","Set how long it takes for a greet to delete"),  RequireContext(ContextType.Guild), SlashUserPerm(GuildPermission.Administrator),
     RequireBotPermission(GuildPermission.ManageMessages), CheckPermissions, BlacklistCheck]
    public async Task MultiGreetDelete(int id, [Summary("Seconds", "After how long in seconds it should delete.")] ulong howlong)
    {
        var greet = Service.GetGreets(ctx.Guild.Id).ElementAt(id - 1);
        if (greet is null)
        {
            await ctx.Interaction.SendErrorAsync("No MultiGreet found for that Id!");
            return;
        }

        await Service.ChangeMgDelete(greet, howlong);
        if (howlong > 0)
            await ctx.Interaction.SendConfirmAsync(
                $"Successfully updated MultiGreet #{id} to delete after {TimeSpan.FromSeconds(howlong).Humanize()}.");
        else
            await ctx.Interaction.SendConfirmAsync($"MultiGreet #{id} will no longer delete.");

    }

    [SlashCommand("type","Enable RandomGreet, MultiGreet, or turn off the entire system."),  RequireContext(ContextType.Guild), SlashUserPerm(GuildPermission.Administrator), CheckPermissions, BlacklistCheck]
    public async Task MultiGreetType(MultiGreetTypes types)
    {
        switch (types)
        {
            case MultiGreetTypes.MultiGreet:
                await Service.SetMultiGreetType(ctx.Guild, 0);
                await ctx.Interaction.SendConfirmAsync("Regular MultiGreet enabled!");
                break;
            case MultiGreetTypes.RandomGreet:
                await Service.SetMultiGreetType(ctx.Guild, 1);
                await ctx.Interaction.SendConfirmAsync("RandomGreet enabled!");
                break;
            case MultiGreetTypes.Off:
                await Service.SetMultiGreetType(ctx.Guild, 3);
                await ctx.Interaction.SendConfirmAsync("MultiGreets Disabled!");
                break;
        }
    }

    [SlashCommand("webhook","Set a custom name and avatar to use for each MultiGreet"), RequireContext(ContextType.Guild), SlashUserPerm(GuildPermission.Administrator),
     RequireBotPermission(GuildPermission.ManageWebhooks), CheckPermissions, BlacklistCheck]
    public async Task MultiGreetWebhook(int id, string? name = null, string? avatar = null)
    {
        var greet = Service.GetGreets(ctx.Guild.Id).ElementAt(id - 1);
        if (greet is null)
        {
            await ctx.Interaction.SendErrorAsync("No MultiGreet found for that Id!");
            return;
        }

        if (name is null)
        {
            await Service.ChangeMgWebhook(greet, null);
            await ctx.Interaction.SendConfirmAsync($"Webhook disabled for MultiGreet #{id}!");
            return;
        }
        var channel = await ctx.Guild.GetTextChannelAsync(greet.ChannelId);
        if (avatar is not null)
        {
            if (!Uri.IsWellFormedUriString(avatar, UriKind.Absolute))
            {
                await ctx.Interaction.SendErrorAsync(
                    "The avatar url used is not a direct url or is invalid! Please use a different url.");
                return;
            }
            var http = new HttpClient();
            using var sr = await http.GetAsync(avatar, HttpCompletionOption.ResponseHeadersRead)
                                     .ConfigureAwait(false);
            var imgData = await sr.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            await using var imgStream = imgData.ToStream();
            var webhook = await channel.CreateWebhookAsync(name, imgStream);
            await Service.ChangeMgWebhook(greet, $"https://discord.com/api/webhooks/{webhook.Id}/{webhook.Token}");
            await ctx.Interaction.SendConfirmAsync("Webhook set!");
        }
        else
        {
            var webhook = await channel.CreateWebhookAsync(name);
            await Service.ChangeMgWebhook(greet, $"https://discord.com/api/webhooks/{webhook.Id}/{webhook.Token}");
            await ctx.Interaction.SendConfirmAsync("Webhook set!");
        }
    }

    [SlashCommand("message","Set a custom message for each MultiGreet. https://mewdeko.tech/placeholders https://eb.mewdeko.tech"),  RequireContext(ContextType.Guild), SlashUserPerm(GuildPermission.Administrator), CheckPermissions, BlacklistCheck]
    public async Task MultiGreetMessage(int id, string? message = null)
    {
        await ctx.Interaction.DeferAsync();
        var greet = Service.GetGreets(ctx.Guild.Id).ElementAt(id - 1);
        if (greet is null)
        {
            await ctx.Interaction.SendErrorFollowupAsync("No MultiGreet found for that Id!");
            return;
        }
        if (message is null)
        {
            var components = new ComponentBuilder().WithButton("Preview", "preview").WithButton("Regular", "regular");
            var msg = await ctx.Interaction.SendConfirmFollowupAsync(
                "Would you like to view this as regular text or would you like to preview how it actually looks?", components);
            var response = await GetButtonInputAsync(ctx.Interaction.Id, msg.Id, ctx.User.Id);
            switch (response)
            {
                case "preview":
                    await msg.DeleteAsync();
                    var replacer = new ReplacementBuilder().WithUser(ctx.User).WithClient(ctx.Client as DiscordSocketClient).WithServer(ctx.Client as DiscordSocketClient, ctx.Guild as SocketGuild).Build();
                    var content = replacer.Replace(greet.Message);
                    if (SmartEmbed.TryParse(content, out var embedData, out var plainText))
                    {
                        await ctx.Interaction.FollowupAsync(plainText, embed: embedData?.Build());
                    }
                    else
                    {
                        await ctx.Interaction.FollowupAsync(content);
                    }

                    break;
                case "regular":
                    await msg.DeleteAsync();
                    await ctx.Interaction.SendConfirmFollowupAsync(greet.Message);
                    break;
            }
        }
        await Service.ChangeMgMessage(greet, message);
        await ctx.Interaction.SendConfirmFollowupAsync($"MultiGreet Message for MultiGreet #{id} set!");
    }

    [SlashCommand("list","Lists all current MultiGreets"),  RequireContext(ContextType.Guild), SlashUserPerm(GuildPermission.Administrator), CheckPermissions, BlacklistCheck]
    public async Task MultiGreetList()
    {
        var greets = Service.GetGreets(ctx.Guild.Id);
        if (!greets.Any())
        {
            await ctx.Interaction.SendErrorAsync("No MultiGreets setup!");
        }
        var paginator = new LazyPaginatorBuilder()
                        .AddUser(ctx.User)
                        .WithPageFactory(PageFactory)
                        .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                        .WithMaxPageIndex(greets.Length - 1)
                        .WithDefaultEmotes()
                        .Build();

        await interactivity.SendPaginatorAsync(paginator, (ctx.Interaction as SocketInteraction)!,
            TimeSpan.FromMinutes(60));

        async Task<PageBuilder> PageFactory(int page)
        {
            var curgreet = greets.Skip(page).FirstOrDefault();
            return new PageBuilder().WithDescription(
                                        $"#{Array.IndexOf(greets, curgreet) + 1}\n`Channel:` {(await ctx.Guild.GetTextChannelAsync(curgreet.ChannelId)).Mention} {curgreet.ChannelId}\n`Delete After:` {curgreet.DeleteTime}s\n`Webhook:` {curgreet.WebhookUrl != null}\n`Message:` {curgreet.Message.TrimTo(1000)}")
                                    .WithOkColor();
        }

    }
}