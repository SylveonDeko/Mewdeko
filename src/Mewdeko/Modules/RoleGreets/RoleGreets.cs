using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Humanizer;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.RoleGreets.Services;
using System.Globalization;
using System.Net.Http;

namespace Mewdeko.Modules.RoleGreets;

public class RoleGreets : MewdekoModuleBase<RoleGreetService>
{
    private readonly InteractiveService _interactivity;

    public RoleGreets(InteractiveService interactivity) => _interactivity = interactivity;

    [Cmd, Aliases, UserPerm(GuildPermission.Administrator), RequireContext(ContextType.Guild)]
    public async Task RoleGreetAdd(IRole role, [Remainder] ITextChannel? channel = null)
    {
        channel ??= ctx.Channel as ITextChannel;
        switch (Service.AddRoleGreet(ctx.Guild.Id, channel.Id, role.Id))
        {
            case true:
                await ctx.Channel.SendConfirmAsync($"Added {role.Mention} to greet in {channel.Mention}!");
                break;
            case false:
                await ctx.Channel.SendErrorAsync(
                    "Seems like you reached your maximum of 10 RoleGreets! Please remove one to continue.");
                break;
        }
    }

    [Cmd, Aliases, UserPerm(GuildPermission.Administrator), RequireContext(ContextType.Guild)]
    public async Task RoleGreetRemove(int id)
    {
        var greet = Service.GetListGreets(ctx.Guild.Id).ElementAt(id - 1);
        if (greet is null)
        {
            await ctx.Channel.SendErrorAsync("No greet with that ID found!");
            return;
        }

        await Service.RemoveRoleGreetInternal(greet);
        await ctx.Channel.SendConfirmAsync("RoleGreet removed!");
    }

    [Cmd, Aliases, UserPerm(GuildPermission.Administrator), RequireContext(ContextType.Guild)]
    public async Task RoleGreetRemove([Remainder] IRole role)
    {
        var greet = Service.GetListGreets(ctx.Guild.Id).Where(x => x.RoleId == role.Id);
        if (!greet.Any())
        {
            await ctx.Channel.SendErrorAsync("There are no greets for that role!");
            return;
        }

        if (await PromptUserConfirmAsync(new EmbedBuilder().WithOkColor().WithDescription("Are you sure you want to remove all RoleGreets for this role?"), ctx.User.Id))
        {
            await Service.MultiRemoveRoleGreetInternal(greet.ToArray());
            await ctx.Channel.SendConfirmAsync("RoleGreets removed!");
        }
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator),
     RequireBotPermission(GuildPermission.ManageMessages)]
    public async Task RoleGreetDelete(int id, StoopidTime time)
    {
        var greet = Service.GetListGreets(ctx.Guild.Id).ElementAt(id - 1);
        if (greet is null)
        {
            await ctx.Channel.SendErrorAsync("No RoleGreet found for that Id!");
            return;
        }

        await Service.ChangeRgDelete(greet, int.Parse(time.Time.TotalSeconds.ToString(CultureInfo.InvariantCulture)));
        await ctx.Channel.SendConfirmAsync(
            $"Successfully updated RoleGreet #{id} to delete after {time.Time.Humanize()}.");
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator),
     RequireBotPermission(GuildPermission.ManageMessages)]
    public async Task RoleGreetDelete(int id, int howlong)
    {
        var greet = Service.GetListGreets(ctx.Guild.Id).ElementAt(id - 1);
        if (greet is null)
        {
            await ctx.Channel.SendErrorAsync("No RoleGreet found for that Id!");
            return;
        }

        await Service.ChangeRgDelete(greet, howlong);
        if (howlong > 0)
        {
            await ctx.Channel.SendConfirmAsync(
                        $"Successfully updated RoleGreet #{id} to delete after {TimeSpan.FromSeconds(howlong).Humanize()}.");
        }
        else
        {
            await ctx.Channel.SendConfirmAsync($"RoleGreet #{id} will no longer delete.");
        }
    }

    [Cmd, Aliases, UserPerm(GuildPermission.Administrator), RequireContext(ContextType.Guild)]
    public async Task RoleGreetGreetBots(int num, bool enabled)
    {
        var greet = Service.GetListGreets(ctx.Guild.Id).ElementAt(num - 1);
        if (greet is null)
        {
            await ctx.Channel.SendErrorAsync("That RoleGreet does not exist!");
            return;
        }
        await Service.ChangeRgGb(greet, enabled);
        await ctx.Channel.SendConfirmAsync($"RoleGreet {num} GreetBots set to {enabled}");
    }
    [Cmd, Aliases, UserPerm(GuildPermission.Administrator), RequireContext(ContextType.Guild)]
    public async Task RoleGreetDisable(int num, bool enabled)
    {
        var greet = Service.GetListGreets(ctx.Guild.Id).ElementAt(num - 1);
        if (greet is null)
        {
            await ctx.Channel.SendErrorAsync("That RoleGreet does not exist!");
            return;
        }
        await Service.RoleGreetDisable(greet, enabled);
        await ctx.Channel.SendConfirmAsync($"RoleGreet {num} set to {enabled}");
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator),
     RequireBotPermission(GuildPermission.ManageWebhooks)]
    public async Task RoleGreetWebhook(int id, string? name = null, string? avatar = null)
    {
        var greet = Service.GetListGreets(ctx.Guild.Id).ElementAt(id - 1);
        if (greet is null)
        {
            await ctx.Channel.SendErrorAsync("No RoleGreet found for that Id!");
            return;
        }

        if (name is null)
        {
            await Service.ChangeMgWebhook(greet, null);
            await ctx.Channel.SendConfirmAsync($"Webhook disabled for RoleGreet #{id}!");
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
    public async Task RoleGreetMessage(int id, [Remainder] string? message = null)
    {
        var greet = Service.GetListGreets(ctx.Guild.Id).ElementAt(id - 1);
        if (greet is null)
        {
            await ctx.Channel.SendErrorAsync("No RoleGreet found for that Id!");
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
                    if (SmartEmbed.TryParse(content, ctx.Guild?.Id, out var embedData, out var plainText, out var cb))
                    {
                        await ctx.Channel.SendMessageAsync(plainText, embed: embedData?.Build(),
                            components: cb.Build());
                        return;
                    }
                    await ctx.Channel.SendMessageAsync(content);
                    return;
                case "regular":
                    await msg.DeleteAsync();
                    await ctx.Channel.SendConfirmAsync(greet.Message);
                    return;
            }
        }
        await Service.ChangeMgMessage(greet, message);
        await ctx.Channel.SendConfirmAsync($"RoleGreet Message for RoleGreet #{id} set!");
    }

    [Cmd, Aliases, UserPerm(GuildPermission.Administrator), RequireContext(ContextType.Guild)]
    public async Task RoleGreetList()
    {
        var greets = Service.GetListGreets(ctx.Guild.Id);
        if (greets.Length == 0)
        {
            await ctx.Channel.SendErrorAsync("No RoleGreets setup!");
        }
        var paginator = new LazyPaginatorBuilder()
                        .AddUser(ctx.User)
                        .WithPageFactory(PageFactory)
                        .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                        .WithMaxPageIndex(greets.Length - 1)
                        .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                        .Build();

        await _interactivity.SendPaginatorAsync(paginator, Context.Channel,
            TimeSpan.FromMinutes(60));

        async Task<PageBuilder> PageFactory(int page)
        {
            var curgreet = greets.Skip(page).FirstOrDefault();
            return new PageBuilder().WithDescription(
                                        $"#{Array.IndexOf(greets, curgreet) + 1}\n`Role:` {((ctx.Guild.GetRole(curgreet.RoleId))?.Mention == null ? "Deleted" : (ctx.Guild.GetRole(curgreet.RoleId))?.Mention)} `{curgreet.RoleId}`\n`Channel:` {((await ctx.Guild.GetTextChannelAsync(curgreet.ChannelId))?.Mention == null ? "Deleted" : (await ctx.Guild.GetTextChannelAsync(curgreet.ChannelId))?.Mention)} {curgreet.ChannelId}\n`Delete After:` {curgreet.DeleteTime}s\n`Disabled:` {curgreet.Disabled}\n`Webhook:` {curgreet.WebhookUrl != null}\n`Greet Bots:` {curgreet.GreetBots}\n`Message:` {curgreet.Message.TrimTo(1000)}")
                                                    .WithOkColor();
        }
    }
}