using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Humanizer;
using Mewdeko.Common;
using Mewdeko.Common.Attributes;
using Mewdeko.Common.Replacements;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Database.Extensions;
using Mewdeko.Extensions;
using Mewdeko.Modules.MultiGreets.Services;
using System.Net.Http;

namespace Mewdeko.Modules.MultiGreets;

public class MultiGreets : MewdekoModuleBase<MultiGreetService>
{
    private InteractiveService interactivity;

    public MultiGreets(InteractiveService interactivity) => this.interactivity = interactivity;

    public enum MultiGreetTypes
    {
        MultiGreet,
        RandomGreet,
        Off
    }
    
    [Cmd, Aliases, UserPerm(GuildPermission.Administrator), RequireContext(ContextType.Guild)]
    public async Task MultiGreetAdd ([Remainder] ITextChannel? channel = null)
    {
        channel ??= ctx.Channel as ITextChannel;
        var added = Service.AddMultiGreet(ctx.Guild.Id, channel.Id);
        switch (added)
        {
            case true:
                await ctx.Channel.SendConfirmAsync($"Added {channel.Mention} as a MultiGreet channel!");
                break;
            case false:
                await ctx.Channel.SendErrorAsync(
                    "Seems like you have reached your 5 greets per channel limit or your 30 greets per guild limit! Remove a MultiGreet and try again");
                break;
        }
    }

    [Cmd, Aliases, UserPerm(GuildPermission.Administrator), RequireContext(ContextType.Guild)]
    public async Task MultiGreetRemove (int id)
    {
        var greet = Service.GetGreets(ctx.Guild.Id).ElementAt(id-1);
        if (greet is null)
        {
            await ctx.Channel.SendErrorAsync("No greet with that ID found!");
            return;
        }

        await Service.RemoveMultiGreetInternal(greet);
        await ctx.Channel.SendConfirmAsync("MultiGreet removed!");
    }

    [Cmd, Aliases, UserPerm(GuildPermission.Administrator), RequireContext(ContextType.Guild)]
    public async Task MultiGreetRemove ([Remainder]ITextChannel channel)
    {
        var greet = Service.GetGreets(ctx.Guild.Id).Where(x => x.ChannelId == channel.Id);
        if (!greet.Any())
        {
            await ctx.Channel.SendErrorAsync("There are no greets in that channel!");
            return;
        }

        if (await PromptUserConfirmAsync(new EmbedBuilder().WithOkColor().WithDescription("Are you sure you want to remove all MultiGreets for this channel?"), ctx.User.Id))
        {
            await Service.MultiRemoveMultiGreetInternal(greet.ToArray());
            await ctx.Channel.SendConfirmAsync("MultiGreets removed!");
        }
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator),
     RequireBotPermission(GuildPermission.ManageMessages)]
    public async Task MultiGreetDelete (int id, StoopidTime time)
    {
        var greet = Service.GetGreets(ctx.Guild.Id).ElementAt(id-1);
        if (greet is null)
        {
            await ctx.Channel.SendErrorAsync("No MultiGreet found for that Id!");
            return;
        }

        await Service.ChangeMgDelete(greet, Convert.ToInt32(time.Time.TotalSeconds));
        await ctx.Channel.SendConfirmAsync(
            $"Successfully updated MultiGreet #{id} to delete after {time.Time.Humanize()}.");

    }

    [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator),
     RequireBotPermission(GuildPermission.ManageMessages)]
    public async Task MultiGreetDelete (int id, int howlong)
    {
        var greet = Service.GetGreets(ctx.Guild.Id).ElementAt(id-1);
        if (greet is null)
        {
            await ctx.Channel.SendErrorAsync("No MultiGreet found for that Id!");
            return;
        }
        
        await Service.ChangeMgDelete(greet, howlong);
        if (howlong > 0)
            await ctx.Channel.SendConfirmAsync(
                $"Successfully updated MultiGreet #{id} to delete after {TimeSpan.FromSeconds(howlong).Humanize()}.");
        else
            await ctx.Channel.SendConfirmAsync($"MultiGreet #{id} will no longer delete.");

    }

    [Cmd, Aliases, UserPerm(GuildPermission.Administrator), RequireContext(ContextType.Guild)]
    public async Task MultiGreetType(MultiGreetTypes types)
    {
        switch (types)
        {
            case MultiGreetTypes.MultiGreet:
                await Service.SetMultiGreetType(ctx.Guild, 0);
                await ctx.Channel.SendConfirmAsync("Regular MultiGreet enabled!");
                break;
            case MultiGreetTypes.RandomGreet:
                await Service.SetMultiGreetType(ctx.Guild, 1);
                await ctx.Channel.SendConfirmAsync("RandomGreet enabled!");
                break;
            case MultiGreetTypes.Off:
                await Service.SetMultiGreetType(ctx.Guild, 3);
                await ctx.Channel.SendConfirmAsync("MultiGreets Disabled!");
                break;
        }
    }

    [Cmd, Alias, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator)]
    public async Task MultiGreetGreetBots(int num, bool enabled)
    {
        var greet = Service.GetGreets(ctx.Guild.Id).ElementAt(num - 1);
        if (greet is null)
        {
            await ctx.Channel.SendErrorAsync("That MultiGreet does not exist!");
            return;
        }

        await Service.ChangeMgGb(greet, enabled);
        await ctx.Channel.SendConfirmAsync($"MultiGreet {num} GreetBots set to {enabled}");
    }
    [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator),
     RequireBotPermission(GuildPermission.ManageWebhooks)]
    public async Task MultiGreetWebhook(int id, string? name = null, string? avatar = null)
    {
        var greet = Service.GetGreets(ctx.Guild.Id).ElementAt(id-1);
        if (greet is null)
        {
            await ctx.Channel.SendErrorAsync("No MultiGreet found for that Id!");
            return;
        }

        if (name is null)
        {
            await Service.ChangeMgWebhook(greet, null);
            await ctx.Channel.SendConfirmAsync($"Webhook disabled for MultiGreet #{id}!");
            return;
        }
        var channel = await ctx.Guild.GetTextChannelAsync(greet.ChannelId);
        if (avatar is not null)
        {
            if (!Uri.IsWellFormedUriString(avatar, UriKind.Absolute))
            {
                await ctx.Channel.SendErrorAsync(
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
            await ctx.Channel.SendConfirmAsync("Webhook set!");
        }
        else
        {
            var webhook = await channel.CreateWebhookAsync(name);
            await Service.ChangeMgWebhook(greet, $"https://discord.com/api/webhooks/{webhook.Id}/{webhook.Token}");
            await ctx.Channel.SendConfirmAsync("Webhook set!");
        }
    }

    [Cmd, Aliases, UserPerm(GuildPermission.Administrator), RequireContext(ContextType.Guild)]
    public async Task MultiGreetMessage(int id, [Remainder]string? message = null)
    {
        var greet = Service.GetGreets(ctx.Guild.Id).ElementAt(id-1);
        if (greet is null)
        {
            await ctx.Channel.SendErrorAsync("No MultiGreet found for that Id!");
            return;
        }
        if (message is null)
        {
            var components = new ComponentBuilder().WithButton("Preview", "preview").WithButton("Regular", "regular");
            var msg = await ctx.Channel.SendConfirmAsync(
                "Would you like to view this as regular text or would you like to preview how it actually looks?", components);
            var response = await GetButtonInputAsync(ctx.Channel.Id, msg.Id, ctx.User.Id);
            switch (response)
            {
                case "preview":
                    await msg.DeleteAsync();
                    var replacer = new ReplacementBuilder().WithUser(ctx.User).WithClient(ctx.Client as DiscordSocketClient).WithServer(ctx.Client as DiscordSocketClient, ctx.Guild as SocketGuild).Build();
                    var content = replacer.Replace(greet.Message);
                    if (SmartEmbed.TryParse(content, out var embedData, out var plainText))
                    {
                        await ctx.Channel.SendMessageAsync(plainText,
                            embed: embedData?.Build());
                        return;
                    }
                    else
                    {
                        await ctx.Channel.SendMessageAsync(content);
                        return;
                    }
                case "regular":
                    await msg.DeleteAsync();
                    await ctx.Channel.SendConfirmAsync(greet.Message);
                    return;
            }
        }
        await Service.ChangeMgMessage(greet, message);
        await ctx.Channel.SendConfirmAsync($"MultiGreet Message for MultiGreet #{id} set!");
    }

    [Cmd, Aliases, UserPerm(GuildPermission.Administrator), RequireContext(ContextType.Guild)]
    public async Task MultiGreetList()
    {
        var greets = Service.GetGreets(ctx.Guild.Id);
        if (!greets.Any())
        {
            await ctx.Channel.SendErrorAsync("No MultiGreets setup!");
        }
        var paginator = new LazyPaginatorBuilder()
                        .AddUser(ctx.User)
                        .WithPageFactory(PageFactory)
                        .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                        .WithMaxPageIndex(greets.Length-1)
                        .WithDefaultEmotes()
                        .Build();

        await interactivity.SendPaginatorAsync(paginator, Context.Channel,
            TimeSpan.FromMinutes(60));

        async Task<PageBuilder> PageFactory(int page)
        {
            var curgreet = greets.Skip(page).FirstOrDefault();
            return new PageBuilder().WithDescription(
                                        $"#{Array.IndexOf(greets, curgreet) + 1}\n`Channel:` {(await ctx.Guild.GetTextChannelAsync(curgreet.ChannelId)).Mention} {curgreet.ChannelId}\n`Delete After:` {curgreet.DeleteTime}s\n`Webhook:` {curgreet.WebhookUrl != null}\n`Greet Bots:` {curgreet.GreetBots}\n`Message:` {curgreet.Message.TrimTo(1000)}")
                                                    .WithOkColor();
        }
        
    }
}