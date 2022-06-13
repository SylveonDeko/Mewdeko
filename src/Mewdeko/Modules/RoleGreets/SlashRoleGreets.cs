using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Humanizer;
using Mewdeko.Common.Attributes.SlashCommands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.RoleGreets.Services;
using System.Net.Http;
using System.Threading.Tasks;

namespace Mewdeko.Modules.RoleGreets;
[Group("rolegreets", "Set or manage RoleGreets.")]
public class RoleRoleGreets : MewdekoSlashModuleBase<RoleGreetService>
{
    private InteractiveService interactivity;
    public RoleRoleGreets(InteractiveService interactivity) => this.interactivity = interactivity;

    [SlashCommand("add", "Add a role to RoleGreets."), SlashUserPerm(GuildPermission.Administrator), RequireContext(ContextType.Guild), CheckPermissions]
    public async Task RoleGreetAdd(IRole role, ITextChannel? channel = null)
    {
        channel ??= ctx.Channel as ITextChannel;
        switch (Service.AddRoleGreet(ctx.Guild.Id, channel.Id, role.Id))
        {
            case true:
                await ctx.Interaction.SendConfirmAsync($"Added {role.Mention} to greet in {channel.Mention}!");
                break;
            case false:
                await ctx.Interaction.SendErrorAsync(
                    "Seems like you have reached your max of 10 RoleGreets! Please remove one to add another one.");
                break;
        }
    }
    [SlashCommand("greetbots", "Set whether to greet bots when triggered."), RequireContext(ContextType.Guild), SlashUserPerm(GuildPermission.Administrator), CheckPermissions]
    public async Task RoleGreetGreetBots(int num, bool enabled)
    {
        var greet = Service.GetGreets(ctx.Guild.Id).ElementAt(num - 1);
        if (greet is null)
        {
            await ctx.Interaction.SendErrorAsync("That RoleGreet does not exist!");
            return;
        }
        await Service.ChangeRgGb(greet, enabled);
        await ctx.Interaction.SendConfirmAsync($"RoleGreet {num} GreetBots set to {enabled}");
    }
    [SlashCommand("remove", "Remove a channel from RoleGreets"), RequireContext(ContextType.Guild), SlashUserPerm(GuildPermission.Administrator), CheckPermissions]
    public async Task RoleGreetRemove(int id)
    {
        var greet = Service.GetGreets(ctx.Guild.Id).ElementAt(id - 1);
        if (greet is null)
        {
            await ctx.Interaction.SendErrorAsync("No greet with that ID found!");
            return;
        }

        await Service.RemoveRoleGreetInternal(greet);
        await ctx.Interaction.SendConfirmAsync("RoleGreet removed!");
    }

    [SlashCommand("removerole", "Removes all RoleGreets on that channel."), RequireContext(ContextType.Guild), SlashUserPerm(GuildPermission.Administrator), CheckPermissions]
    public async Task RoleGreetRemove(IRole role)
    {
        await ctx.Interaction.DeferAsync();
        var greet = Service.GetGreets(ctx.Guild.Id).Where(x => x.RoleId == role.Id);
        if (!greet.Any())
        {
            await ctx.Interaction.SendErrorFollowupAsync("There are no greets for that role!");
            return;
        }

        if (await PromptUserConfirmAsync(new EmbedBuilder().WithOkColor().WithDescription("Are you sure you want to remove all RoleGreets for this role?"), ctx.User.Id))
        {
            await Service.MultiRemoveRoleGreetInternal(greet.ToArray());
            await ctx.Interaction.SendConfirmFollowupAsync("RoleGreets removed!");
        }
    }

    [SlashCommand("delete", "Set how long it takes for a greet to delete"), RequireContext(ContextType.Guild), SlashUserPerm(GuildPermission.Administrator),
     RequireBotPermission(GuildPermission.ManageMessages), CheckPermissions]
    public async Task RoleGreetDelete(int id, [Summary("Seconds", "After how long in seconds it should delete.")] int howlong)
    {
        var greet = Service.GetGreets(ctx.Guild.Id).ElementAt(id - 1);
        if (greet is null)
        {
            await ctx.Interaction.SendErrorAsync("No RoleGreet found for that Id!");
            return;
        }

        await Service.ChangeRgDelete(greet, howlong);
        if (howlong > 0)
        {
            await ctx.Interaction.SendConfirmAsync(
                        $"Successfully updated RoleGreet #{id} to delete after {TimeSpan.FromSeconds(howlong).Humanize()}.");
        }
        else
        {
            await ctx.Interaction.SendConfirmAsync($"RoleGreet #{id} will no longer delete.");
        }
    }

    [SlashCommand("disable", "Disable a RoleGreet using its Id"), Aliases, RequireContext(ContextType.Guild), SlashUserPerm(GuildPermission.Administrator), CheckPermissions]
    public async Task RoleGreetDisable(int num, bool enabled)
    {
        var greet = Service.GetGreets(ctx.Guild.Id).ElementAt(num - 1);
        if (greet is null)
        {
            await ctx.Interaction.SendErrorAsync("That RoleGreet does not exist!");
            return;
        }
        await Service.RoleGreetDisable(greet, enabled);
        await ctx.Interaction.SendConfirmAsync($"RoleGreet {num} set to {enabled}");
    }
    [SlashCommand("webhook", "Set a custom name and avatar to use for each RoleGreet"), RequireContext(ContextType.Guild), SlashUserPerm(GuildPermission.Administrator),
     RequireBotPermission(GuildPermission.ManageWebhooks), CheckPermissions]
    public async Task RoleGreetWebhook(int id, string? name = null, string? avatar = null)
    {
        var greet = Service.GetGreets(ctx.Guild.Id).ElementAt(id - 1);
        if (greet is null)
        {
            await ctx.Interaction.SendErrorAsync("No RoleGreet found for that Id!");
            return;
        }

        if (name is null)
        {
            await Service.ChangeMgWebhook(greet, null);
            await ctx.Interaction.SendConfirmAsync($"Webhook disabled for RoleGreet #{id}!");
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

    [SlashCommand("message", "Set a custom message for each RoleGreet. https://mewdeko.tech/placeholders https://eb.mewdeko.tech"), RequireContext(ContextType.Guild), SlashUserPerm(GuildPermission.Administrator), CheckPermissions]
    public async Task RoleGreetMessage(int id, string? message = null)
    {
        await ctx.Interaction.DeferAsync();
        var greet = Service.GetGreets(ctx.Guild.Id).ElementAt(id - 1);
        if (greet is null)
        {
            await ctx.Interaction.SendErrorFollowupAsync("No RoleGreet found for that Id!");
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
                    if (SmartEmbed.TryParse(content, ctx.Guild?.Id, out var embedData, out var plainText, out var cb))
                    {
                        await ctx.Interaction.FollowupAsync(plainText, embed: embedData?.Build(), components:cb.Build());
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
        await ctx.Interaction.SendConfirmFollowupAsync($"RoleGreet Message for RoleGreet #{id} set!");
    }

    [SlashCommand("list", "Lists all current RoleGreets"), RequireContext(ContextType.Guild), SlashUserPerm(GuildPermission.Administrator), CheckPermissions]
    public async Task RoleGreetList()
    {
        var greets = Service.GetGreets(ctx.Guild.Id);
        if (greets.Length == 0)
        {
            await ctx.Interaction.SendErrorAsync("No RoleGreets setup!");
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
            TimeSpan.FromMinutes(60));

        async Task<PageBuilder> PageFactory(int page)
        {
            var curgreet = greets.Skip(page).FirstOrDefault();
            return new PageBuilder().WithDescription(
                                        $"#{Array.IndexOf(greets, curgreet) + 1}\n`Role:` {((await ctx.Guild.GetTextChannelAsync(curgreet.RoleId))?.Mention == null ? "Deleted" : (await ctx.Guild.GetTextChannelAsync(curgreet.RoleId))?.Mention)} `{curgreet.RoleId}`\n`Channel:` {((await ctx.Guild.GetTextChannelAsync(curgreet.ChannelId))?.Mention == null ? "Deleted" : (await ctx.Guild.GetTextChannelAsync(curgreet.ChannelId))?.Mention)} {curgreet.ChannelId}\n`Delete After:` {curgreet.DeleteTime}s\n`Disabled:` {curgreet.Disabled}\n`Webhook:` {curgreet.WebhookUrl != null}\n`Greet Bots:` {curgreet.GreetBots}\n`Message:` {curgreet.Message.TrimTo(1000)}")
                                    .WithOkColor();
        }
    }
}