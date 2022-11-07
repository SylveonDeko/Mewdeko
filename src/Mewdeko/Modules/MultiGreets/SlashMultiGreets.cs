using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Humanizer;
using Mewdeko.Modules.MultiGreets.Services;
using System.Net.Http;
using System.Threading.Tasks;
using Mewdeko.Common.Attributes.InteractionCommands;

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

    [SlashCommand("add","Add a channel to MultiGreets."), SlashUserPerm(GuildPermission.Administrator), CheckPermissions]
    public async Task MultiGreetAdd(ITextChannel? channel = null)
    {
        channel ??= ctx.Channel as ITextChannel;
        var added = Service.AddMultiGreet(ctx.Guild.Id, channel.Id);
        switch (added)
        {
            case true:
                await ctx.Interaction.SendConfirmAsync($"Added {channel.Mention} as a MultiGreet channel!").ConfigureAwait(false);
                break;
            case false:
                await ctx.Interaction.SendErrorAsync(
                    "Seems like you have reached your 5 greets per channel limit or your 30 greets per guild limit! Remove a MultiGreet and try again").ConfigureAwait(false);
                break;
        }
    }

    [SlashCommand("remove","Remove a channel from MultiGreets"), RequireContext(ContextType.Guild), SlashUserPerm(GuildPermission.Administrator), CheckPermissions]
    public async Task MultiGreetRemove(int id)
    {
        var greet = Service.GetGreets(ctx.Guild.Id).ElementAt(id - 1);
        if (greet is null)
        {
            await ctx.Interaction.SendErrorAsync("No greet with that ID found!").ConfigureAwait(false);
            return;
        }

        await Service.RemoveMultiGreetInternal(greet).ConfigureAwait(false);
        await ctx.Interaction.SendConfirmAsync("MultiGreet removed!").ConfigureAwait(false);
    }

    [SlashCommand("removechannel","Removes all MultiGreets on that channel."),  RequireContext(ContextType.Guild), SlashUserPerm(GuildPermission.Administrator), CheckPermissions]
    public async Task MultiGreetRemove(ITextChannel channel)
    {
        await ctx.Interaction.DeferAsync().ConfigureAwait(false);
        var greet = Service.GetGreets(ctx.Guild.Id).Where(x => x.ChannelId == channel.Id);
        if (!greet.Any())
        {
            await ctx.Interaction.SendErrorFollowupAsync("There are no greets in that channel!").ConfigureAwait(false);
            return;
        }

        if (await PromptUserConfirmAsync(new EmbedBuilder().WithOkColor().WithDescription("Are you sure you want to remove all MultiGreets for this channel?"), ctx.User.Id).ConfigureAwait(false))
        {
            await Service.MultiRemoveMultiGreetInternal(greet.ToArray()).ConfigureAwait(false);
            await ctx.Interaction.SendConfirmFollowupAsync("MultiGreets removed!").ConfigureAwait(false);
        }
    }

    [SlashCommand("delete","Set how long it takes for a greet to delete"),  RequireContext(ContextType.Guild), SlashUserPerm(GuildPermission.Administrator),
     RequireBotPermission(GuildPermission.ManageMessages), CheckPermissions]
    public async Task MultiGreetDelete(int id, [Summary("Seconds", "After how long in seconds it should delete.")] int howlong)
    {
        var greet = Service.GetGreets(ctx.Guild.Id).ElementAt(id - 1);
        if (greet is null)
        {
            await ctx.Interaction.SendErrorAsync("No MultiGreet found for that Id!").ConfigureAwait(false);
            return;
        }

        await Service.ChangeMgDelete(greet, howlong).ConfigureAwait(false);
        if (howlong > 0)
            await ctx.Interaction.SendConfirmAsync(
                $"Successfully updated MultiGreet #{id} to delete after {TimeSpan.FromSeconds(howlong).Humanize()}.").ConfigureAwait(false);
        else
            await ctx.Interaction.SendConfirmAsync($"MultiGreet #{id} will no longer delete.").ConfigureAwait(false);

    }

    [SlashCommand("disable", "Disable a MultiGreet using its Id"), RequireContext(ContextType.Guild), SlashUserPerm(GuildPermission.Administrator), CheckPermissions]
    public async Task RoleGreetDisable(int num, bool enabled)
    {
        var greet = Service.GetGreets(ctx.Guild.Id).ElementAt(num - 1);
        if (greet is null)
        {
            await ctx.Interaction.SendErrorAsync("That MultiGreet does not exist!").ConfigureAwait(false);
            return;
        }
        await Service.MultiGreetDisable(greet, enabled).ConfigureAwait(false);
        await ctx.Interaction.SendConfirmAsync($"MultiGreet {num} set to {enabled}").ConfigureAwait(false);
    }

    [SlashCommand("type","Enable RandomGreet, MultiGreet, or turn off the entire system."),  RequireContext(ContextType.Guild), SlashUserPerm(GuildPermission.Administrator), CheckPermissions]
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

    [SlashCommand("webhook","Set a custom name and avatar to use for each MultiGreet"), RequireContext(ContextType.Guild), SlashUserPerm(GuildPermission.Administrator),
     RequireBotPermission(GuildPermission.ManageWebhooks), CheckPermissions]
    public async Task MultiGreetWebhook(int id, string? name = null, string? avatar = null)
    {
        var greet = Service.GetGreets(ctx.Guild.Id).ElementAt(id - 1);
        if (greet is null)
        {
            await ctx.Interaction.SendErrorAsync("No MultiGreet found for that Id!").ConfigureAwait(false);
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
                    "The avatar url used is not a direct url or is invalid! Please use a different url.").ConfigureAwait(false);
                return;
            }
            var http = new HttpClient();
            using var sr = await http.GetAsync(avatar, HttpCompletionOption.ResponseHeadersRead)
                                     .ConfigureAwait(false);
            var imgData = await sr.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            var imgStream = imgData.ToStream();
            await using var _ = imgStream.ConfigureAwait(false);
            var webhook = await channel.CreateWebhookAsync(name, imgStream).ConfigureAwait(false);
            await Service.ChangeMgWebhook(greet, $"https://discord.com/api/webhooks/{webhook.Id}/{webhook.Token}").ConfigureAwait(false);
            await ctx.Interaction.SendConfirmAsync("Webhook set!").ConfigureAwait(false);
        }
        else
        {
            var webhook = await channel.CreateWebhookAsync(name).ConfigureAwait(false);
            await Service.ChangeMgWebhook(greet, $"https://discord.com/api/webhooks/{webhook.Id}/{webhook.Token}").ConfigureAwait(false);
            await ctx.Interaction.SendConfirmAsync("Webhook set!").ConfigureAwait(false);
        }
    }

    [SlashCommand("message","Set a custom message for each MultiGreet. https://mewdeko.tech/placeholders https://eb.mewdeko.tech"),  RequireContext(ContextType.Guild), SlashUserPerm(GuildPermission.Administrator), CheckPermissions]
    public async Task MultiGreetMessage(int id, string? message = null)
    {
        await ctx.Interaction.DeferAsync().ConfigureAwait(false);
        var greet = Service.GetGreets(ctx.Guild.Id).ElementAt(id - 1);
        if (greet is null)
        {
            await ctx.Interaction.SendErrorFollowupAsync("No MultiGreet found for that Id!").ConfigureAwait(false);
            return;
        }
        if (message is null)
        {
            var components = new ComponentBuilder().WithButton("Preview", "preview").WithButton("Regular", "regular");
            var msg = await ctx.Interaction.SendConfirmFollowupAsync(
                "Would you like to view this as regular text or would you like to preview how it actually looks?", components).ConfigureAwait(false);
            var response = await GetButtonInputAsync(ctx.Interaction.Id, msg.Id, ctx.User.Id).ConfigureAwait(false);
            switch (response)
            {
                case "preview":
                    await msg.DeleteAsync().ConfigureAwait(false);
                    var replacer = new ReplacementBuilder().WithUser(ctx.User).WithClient(ctx.Client as DiscordSocketClient).WithServer(ctx.Client as DiscordSocketClient, ctx.Guild as SocketGuild).Build();
                    var content = replacer.Replace(greet.Message);
                    if (SmartEmbed.TryParse(content , ctx.Guild.Id, out var embedData, out var plainText, out var components2))
                    {
                        await ctx.Interaction.FollowupAsync(plainText, embeds: embedData, components: components2.Build()).ConfigureAwait(false);
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
        await ctx.Interaction.SendConfirmFollowupAsync($"MultiGreet Message for MultiGreet #{id} set!").ConfigureAwait(false);
    }

    [SlashCommand("list","Lists all current MultiGreets"),  RequireContext(ContextType.Guild), SlashUserPerm(GuildPermission.Administrator), CheckPermissions]
    public async Task MultiGreetList()
    {
        var greets = Service.GetGreets(ctx.Guild.Id);
        if (!greets.Any())
        {
            await ctx.Interaction.SendErrorAsync("No MultiGreets setup!").ConfigureAwait(false);
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