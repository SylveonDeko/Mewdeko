using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Humanizer;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.Utility.Common;
using Mewdeko.Modules.Utility.Services;
using Mewdeko.Services.Impl;
using Mewdeko.Services.Settings;
using Serilog;
using StringExtensions = Mewdeko.Extensions.StringExtensions;

namespace Mewdeko.Modules.Utility;

public partial class Utility : MewdekoModuleBase<UtilityService>
{
    private static readonly SemaphoreSlim Sem = new(1, 1);
    private readonly DiscordSocketClient client;
    private readonly IBotCredentials creds;
    private readonly IStatsService stats;
    private readonly DownloadTracker tracker;
    private readonly InteractiveService interactivity;
    private readonly ICoordinator coordinator;
    private readonly GuildSettingsService guildSettings;
    private readonly HttpClient httpClient;
    private readonly BotConfigService config;
    private readonly DbService db;

    public Utility(
        DiscordSocketClient client,
        IStatsService stats, IBotCredentials creds, DownloadTracker tracker, InteractiveService serv, ICoordinator coordinator,
        GuildSettingsService guildSettings,
        HttpClient httpClient,
        BotConfigService config, DbService db)
    {
        this.coordinator = coordinator;
        this.guildSettings = guildSettings;
        this.httpClient = httpClient;
        this.config = config;
        this.db = db;
        interactivity = serv;
        this.client = client;
        this.stats = stats;
        this.creds = creds;
        this.tracker = tracker;
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.ManageMessages)]
    public async Task SaveChat(StoopidTime time, ITextChannel? channel = null)
    {
        var curTime = DateTime.UtcNow.Subtract(time.Time);
        if (!Directory.Exists(creds.ChatSavePath))
        {
            await ctx.Channel.SendErrorAsync("Chat save directory does not exist. Please create it.").ConfigureAwait(false);
            return;
        }

        var secureString = StringExtensions.GenerateSecureString(16);
        try
        {
            Directory.CreateDirectory($"{creds.ChatSavePath}/{ctx.Guild.Id}/{secureString}");
        }
        catch (Exception ex)
        {
            await ctx.Channel.SendErrorAsync($"Failed to create directory. {ex.Message}").ConfigureAwait(false);
            return;
        }

        if (time.Time.Days > 3)
        {
            await ctx.Channel.SendErrorAsync("Max time to grab messages is 3 days. This will be increased in the near future.").ConfigureAwait(false);
            return;
        }

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            Arguments =
                $"../ChatExporter/DiscordChatExporter.Cli.dll export -t {creds.Token} -c {channel?.Id ?? ctx.Channel.Id} --after {curTime:yyyy-MM-ddTHH:mm:ssZ} --output \"{creds.ChatSavePath}/{ctx.Guild.Id}/{secureString}/{ctx.Guild.Name.Replace(" ", "-")}-{(channel?.Name ?? ctx.Channel.Name).Replace(" ", "-")}-{curTime:yyyy-MM-ddTHH-mm-ssZ}.html\" --media true",
            FileName = "dotnet",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };
        using (ctx.Channel.EnterTypingState())
        {
            process.Start();
            await ctx.Channel.SendConfirmAsync($"{config.Data.LoadingEmote} Saving chat log, this may take some time...");
        }

        await process.WaitForExitAsync().ConfigureAwait(false);
        if (creds.ChatSavePath.Contains("/usr/share/nginx/cdn"))
            await ctx.User.SendConfirmAsync(
                    $"Your chat log is here: https://cdn.mewdeko.tech/chatlogs/{ctx.Guild.Id}/{secureString}/{ctx.Guild.Name.Replace(" ", "-")}-{(channel?.Name ?? ctx.Channel.Name).Replace(" ", "-")}-{curTime:yyyy-MM-ddTHH-mm-ssZ}.html")
                .ConfigureAwait(false);
        else
            await ctx.Channel.SendConfirmAsync($"Your chat log is here: {creds.ChatSavePath}/{ctx.Guild.Id}/{secureString}").ConfigureAwait(false);
    }

    [Cmd, Aliases]
    public async Task EmoteList([Remainder] string? emotetype = null)
    {
        var emotes = emotetype switch
        {
            "animated" => ctx.Guild.Emotes.Where(x => x.Animated).ToArray(),
            "nonanimated" => ctx.Guild.Emotes.Where(x => !x.Animated).ToArray(),
            _ => ctx.Guild.Emotes.ToArray()
        };

        if (emotes.Length == 0)
        {
            await ctx.Channel.SendErrorAsync("No emotes found!").ConfigureAwait(false);
            return;
        }

        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(emotes.Length / 10)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await interactivity.SendPaginatorAsync(paginator, Context.Channel,
            TimeSpan.FromMinutes(60)).ConfigureAwait(false);

        async Task<PageBuilder> PageFactory(int page)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            var titleText = emotetype switch
            {
                "animated" => $"{emotes.Length} Animated Emotes",
                "nonanimated" => $"{emotes.Length} Non Animated Emotes",
                _ =>
                    $"{emotes.Count(x => x.Animated)} Animated Emotes | {emotes.Count(x => !x.Animated)} Non Animated Emotes"
            };

            return new PageBuilder()
                .WithTitle(titleText)
                .WithDescription(string.Join("\n",
                    emotes.OrderBy(x => x.Name).Skip(10 * page).Take(10)
                        .Select(x => $"{x} `{x.Name}` [Link]({x.Url})")))
                .WithOkColor();
        }
    }

    [Cmd, Aliases]
    public async Task Invite()
    {
        var eb = new EmbedBuilder()
            .AddField("Invite Link (IOS shows an error so use the browser)",
                $"[Click Here](https://discord.com/oauth2/authorize?client_id={ctx.Client.CurrentUser.Id}&scope=bot&permissions=66186303&scope=bot%20applications.commands)")
            .AddField("Website/Docs", "https://mewdeko.tech")
            .AddField("Support Server", "https://discord.gg/mewdeko")
            .WithOkColor();
        await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
    }

    [Cmd, Aliases]
    public async Task TestSite(string url)
    {
        var response = await httpClient.GetAsync(url).ConfigureAwait(false);

        await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var statusCode = response.StatusCode;
        if (statusCode.ToString() == "Forbidden")
            await ctx.Channel.SendErrorAsync("Sites down m8").ConfigureAwait(false);
        else
            await ctx.Channel.SendConfirmAsync("Sites ok m8").ConfigureAwait(false);
    }

    [Cmd, Aliases, UserPerm(GuildPermission.ManageChannels)]
    public async Task ReactChannel(ITextChannel? chan = null)
    {
        var e = await Service.GetReactChans(ctx.Guild.Id);
        if (chan == null)
        {
            if (e == 0) return;
            await Service.SetReactChan(ctx.Guild, 0).ConfigureAwait(false);
            await ctx.Channel.SendConfirmAsync("React Channel Disabled!").ConfigureAwait(false);
        }
        else
        {
            if (e == 0)
            {
                await Service.SetReactChan(ctx.Guild, chan.Id).ConfigureAwait(false);
                await ctx.Channel.SendConfirmAsync($"Your React Channel has been set to {chan.Mention}!").ConfigureAwait(false);
            }
            else
            {
                var chan2 = await ctx.Guild.GetTextChannelAsync(e).ConfigureAwait(false);
                if (e == chan.Id)
                {
                    await ctx.Channel.SendErrorAsync("This is already your React Channel!").ConfigureAwait(false);
                }
                else
                {
                    await Service.SetReactChan(ctx.Guild, chan.Id).ConfigureAwait(false);
                    await ctx.Channel.SendConfirmAsync(
                        $"Your React Channel has been switched from {chan2.Mention} to {chan.Mention}!").ConfigureAwait(false);
                }
            }
        }
    }

    [Cmd, Aliases, UserPerm(GuildPermission.Administrator),
     RequireContext(ContextType.Guild)]
    public async Task SnipeSet(PermissionAction value)
    {
        await Service.SnipeSet(ctx.Guild, value.Value).ConfigureAwait(false);
        var t = await Service.GetSnipeSet(ctx.Guild.Id);
        await ReplyConfirmLocalizedAsync("snipe_set", t ? "Enabled" : "Disabled").ConfigureAwait(false);
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task Snipe()
    {
        if (!await Service.GetSnipeSet(ctx.Guild.Id))
        {
            await ReplyErrorLocalizedAsync("snipe_not_enabled", await guildSettings.GetPrefix(ctx.Guild)).ConfigureAwait(false);
            return;
        }

        await Service.GetSnipes(ctx.Guild.Id).ConfigureAwait(false);
        var msg = (await Service.GetSnipes(ctx.Guild.Id).ConfigureAwait(false)).LastOrDefault(x => x.ChannelId == ctx.Channel.Id && !x.Edited);
        if (msg is null)
        {
            await ctx.Channel.SendErrorAsync("There is nothing to snipe here!").ConfigureAwait(false);
            return;
        }

        var user = await ctx.Channel.GetUserAsync(msg.UserId).ConfigureAwait(false) ?? await client.Rest.GetUserAsync(msg.UserId).ConfigureAwait(false);

        var em = new EmbedBuilder
        {
            Author = new EmbedAuthorBuilder
            {
                IconUrl = user.GetAvatarUrl(), Name = $"{user} said:"
            },
            Description = msg.Message,
            Footer = new EmbedFooterBuilder
            {
                IconUrl = ctx.User.GetAvatarUrl(),
                Text =
                    GetText("snipe_request", ctx.User.ToString(), (DateTime.UtcNow - msg.DateAdded).Humanize())
            },
            Color = Mewdeko.OkColor
        };

        if (msg.ReferenceMessage is not null)
            em.AddField("Replied To", msg.ReferenceMessage);
        await ctx.Channel.SendMessageAsync(embed: em.Build(),
            components: config.Data.ShowInviteButton
                ? new ComponentBuilder()
                    .WithButton(style: ButtonStyle.Link,
                        url:
                        "https://discord.com/oauth2/authorize?client_id=752236274261426212&permissions=8&response_type=code&redirect_uri=https%3A%2F%2Fmewdeko.tech&scope=bot%20applications.commands",
                        label: "Invite Me!",
                        emote: "<a:HaneMeow:968564817784877066>".ToIEmote()).Build()
                : null).ConfigureAwait(false);
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task SnipeList(int amount = 5)
    {
        if (!await Service.GetSnipeSet(ctx.Guild.Id))
        {
            await ReplyErrorLocalizedAsync("snipe_not_enabled", await guildSettings.GetPrefix(ctx.Guild)).ConfigureAwait(false);
            return;
        }

        var msgs = (await Service.GetSnipes(ctx.Guild.Id).ConfigureAwait(false)).Where(x => x.ChannelId == ctx.Channel.Id && !x.Edited);
        {
            var snipeStores = msgs as SnipeStore[] ?? msgs.ToArray();
            if (snipeStores.Length == 0)
            {
                await ReplyErrorLocalizedAsync("no_snipes").ConfigureAwait(false);
                return;
            }

            var msg = snipeStores.OrderByDescending(d => d.DateAdded).Where(x => !x.Edited).Take(amount);
            var paginator = new LazyPaginatorBuilder()
                .AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(msg.Count() - 1)
                .WithDefaultEmotes()
                .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                .Build();

            await interactivity.SendPaginatorAsync(paginator, Context.Channel,
                TimeSpan.FromMinutes(60)).ConfigureAwait(false);

            async Task<PageBuilder> PageFactory(int page)
            {
                var msg1 = msg.Skip(page).FirstOrDefault();
                var user = await ctx.Channel.GetUserAsync(msg1.UserId).ConfigureAwait(false) ??
                           await client.Rest.GetUserAsync(msg1.UserId).ConfigureAwait(false);

                var builder = new PageBuilder()
                    .WithOkColor()
                    .WithAuthor(
                        new EmbedAuthorBuilder()
                            .WithIconUrl(user.RealAvatarUrl().AbsoluteUri)
                            .WithName($"{user} said:"))
                    .WithDescription($"{msg1.Message}")
                    .WithFooter($"\n\nMessage deleted {(DateTime.UtcNow - msg1.DateAdded).Humanize()} ago");

                if (msg1.ReferenceMessage is not null)
                    builder.AddField("Replied To", msg1.ReferenceMessage);

                return builder;
            }
        }
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task EditSnipeList(int amount = 5)
    {
        if (!await Service.GetSnipeSet(ctx.Guild.Id))
        {
            await ReplyErrorLocalizedAsync("snipe_not_enabled", await guildSettings.GetPrefix(ctx.Guild)).ConfigureAwait(false);
            return;
        }

        var msgs = (await Service.GetSnipes(ctx.Guild.Id).ConfigureAwait(false)).Where(x => x.ChannelId == ctx.Channel.Id && x.Edited);
        {
            var snipeStores = msgs as SnipeStore[] ?? msgs.ToArray();
            if (snipeStores.Length == 0)
            {
                await ReplyErrorLocalizedAsync("no_snipes").ConfigureAwait(false);
                return;
            }

            var msg = snipeStores.OrderByDescending(d => d.DateAdded).Where(x => x.Edited).Take(amount);
            var paginator = new LazyPaginatorBuilder()
                .AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(msg.Count() - 1)
                .WithDefaultEmotes()
                .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                .Build();

            await interactivity.SendPaginatorAsync(paginator, Context.Channel,
                TimeSpan.FromMinutes(60)).ConfigureAwait(false);

            async Task<PageBuilder> PageFactory(int page)
            {
                var msg1 = msg.Skip(page).FirstOrDefault();
                var user = await ctx.Channel.GetUserAsync(msg1.UserId).ConfigureAwait(false) ??
                           await client.Rest.GetUserAsync(msg1.UserId).ConfigureAwait(false);

                var builder = new PageBuilder()
                    .WithOkColor()
                    .WithAuthor(
                        new EmbedAuthorBuilder()
                            .WithIconUrl(user.RealAvatarUrl().AbsoluteUri)
                            .WithName($"{user} originally said:"))
                    .WithDescription($"{msg1.Message}")
                    .WithFooter($"\n\nMessage edited {(DateTime.UtcNow - msg1.DateAdded).Humanize()} ago");

                if (msg1.ReferenceMessage is not null)
                    builder.AddField("Replied To", msg1.ReferenceMessage);

                return builder;
            }
        }
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild), Priority(1)]
    public async Task Snipe(IUser user1)
    {
        if (!await Service.GetSnipeSet(ctx.Guild.Id))
        {
            await ReplyErrorLocalizedAsync("snipe_not_enabled", await guildSettings.GetPrefix(ctx.Guild)).ConfigureAwait(false);
            return;
        }

        var msg = (await Service.GetSnipes(ctx.Guild.Id).ConfigureAwait(false))
            .Find(x => x.ChannelId == ctx.Channel.Id && x.UserId == user1.Id && !x.Edited);
        if (msg is null)
        {
            await ctx.Channel.SendErrorAsync("There is nothing to snipe for this user!").ConfigureAwait(false);
            return;
        }

        var user = await ctx.Channel.GetUserAsync(msg.UserId).ConfigureAwait(false) ?? await client.Rest.GetUserAsync(msg.UserId).ConfigureAwait(false);
        var em = new EmbedBuilder
        {
            Author = new EmbedAuthorBuilder
            {
                IconUrl = user.GetAvatarUrl(), Name = $"{user} said:"
            },
            Description = msg.Message,
            Footer = new EmbedFooterBuilder
            {
                IconUrl = ctx.User.GetAvatarUrl(),
                Text =
                    GetText("snipe_request", ctx.User.ToString(), (DateTime.UtcNow - msg.DateAdded).Humanize())
            },
            Color = Mewdeko.OkColor
        };

        if (msg.ReferenceMessage is not null)
            em.AddField("Replied To", msg.ReferenceMessage);

        await ctx.Channel.SendMessageAsync(embed: em.Build(),
            components: config.Data.ShowInviteButton
                ? new ComponentBuilder()
                    .WithButton(style: ButtonStyle.Link,
                        url:
                        "https://discord.com/oauth2/authorize?client_id=752236274261426212&permissions=8&response_type=code&redirect_uri=https%3A%2F%2Fmewdeko.tech&scope=bot%20applications.commands",
                        label: "Invite Me!",
                        emote: "<a:HaneMeow:968564817784877066>".ToIEmote()).Build()
                : null).ConfigureAwait(false);
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild), Priority(2)]
    public async Task VCheck([Remainder] string? url = null)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            await ctx.Channel.SendErrorAsync("You didn't specify a url").ConfigureAwait(false);
        }
        else
        {
            var result = await UtilityService.UrlChecker(url).ConfigureAwait(false);
            var eb = new EmbedBuilder();
            eb.WithOkColor();
            eb.WithDescription(result.Permalink);
            eb.AddField("Virus Positives", result.Positives, true);
            eb.AddField("Number of scans", result.Total, true);
            await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
        }
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild), Priority(2)]
    public async Task Snipe(ITextChannel chan)
    {
        if (!await Service.GetSnipeSet(ctx.Guild.Id))
        {
            await ReplyErrorLocalizedAsync("snipe_not_enabled", await guildSettings.GetPrefix(ctx.Guild)).ConfigureAwait(false);
            return;
        }

        var msg = (await Service.GetSnipes(ctx.Guild.Id).ConfigureAwait(false)).Where(x => !x.Edited)
            .LastOrDefault(x => x.ChannelId == chan.Id);
        if (msg == null)
        {
            await ReplyErrorLocalizedAsync("no_snipes").ConfigureAwait(false);
            return;
        }

        var user = await ctx.Channel.GetUserAsync(msg.UserId).ConfigureAwait(false) ?? await client.Rest.GetUserAsync(msg.UserId).ConfigureAwait(false);

        var em = new EmbedBuilder
        {
            Author = new EmbedAuthorBuilder
            {
                IconUrl = user.GetAvatarUrl(), Name = $"{user} said:"
            },
            Description = msg.Message,
            Footer = new EmbedFooterBuilder
            {
                IconUrl = ctx.User.GetAvatarUrl(),
                Text =
                    GetText("snipe_request", ctx.User.ToString(), (DateTime.UtcNow - msg.DateAdded).Humanize())
            },
            Color = Mewdeko.OkColor
        };

        if (msg.ReferenceMessage is not null)
            em.AddField("Replied To", msg.ReferenceMessage);

        await ctx.Channel.SendMessageAsync(embed: em.Build(),
            components: config.Data.ShowInviteButton
                ? new ComponentBuilder()
                    .WithButton(style: ButtonStyle.Link,
                        url:
                        "https://discord.com/oauth2/authorize?client_id=752236274261426212&permissions=8&response_type=code&redirect_uri=https%3A%2F%2Fmewdeko.tech&scope=bot%20applications.commands",
                        label: "Invite Me!",
                        emote: "<a:HaneMeow:968564817784877066>".ToIEmote()).Build()
                : null).ConfigureAwait(false);
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild), Priority(2)]
    public async Task Snipe(ITextChannel chan, IUser user1)
    {
        if (!await Service.GetSnipeSet(ctx.Guild.Id))
        {
            await ReplyErrorLocalizedAsync("snipe_not_enabled", await guildSettings.GetPrefix(ctx.Guild)).ConfigureAwait(false);
            return;
        }

        var msg = (await Service.GetSnipes(ctx.Guild.Id).ConfigureAwait(false)).Where(x => !x.Edited)
            .LastOrDefault(x => x.ChannelId == chan.Id && x.UserId == user1.Id);
        {
            if (msg == null)
            {
                await ctx.Channel.SendErrorAsync("There's nothing to snipe for that channel and user!").ConfigureAwait(false);
                return;
            }

            var user = await ctx.Channel.GetUserAsync(msg.UserId).ConfigureAwait(false) ?? await client.Rest.GetUserAsync(msg.UserId).ConfigureAwait(false);

            var em = new EmbedBuilder
            {
                Author = new EmbedAuthorBuilder
                {
                    IconUrl = user.GetAvatarUrl(), Name = $"{user} said:"
                },
                Description = msg.Message,
                Footer = new EmbedFooterBuilder
                {
                    IconUrl = ctx.User.GetAvatarUrl(),
                    Text =
                        GetText("snipe_request", ctx.User.ToString(), (DateTime.UtcNow - msg.DateAdded).Humanize())
                },
                Color = Mewdeko.OkColor
            };

            if (msg.ReferenceMessage is not null)
                em.AddField("Replied To", msg.ReferenceMessage);

            await ctx.Channel.SendMessageAsync(embed: em.Build(),
                components: config.Data.ShowInviteButton
                    ? new ComponentBuilder()
                        .WithButton(style: ButtonStyle.Link,
                            url:
                            "https://discord.com/oauth2/authorize?client_id=752236274261426212&permissions=8&response_type=code&redirect_uri=https%3A%2F%2Fmewdeko.tech&scope=bot%20applications.commands",
                            label: "Invite Me!",
                            emote: "<a:HaneMeow:968564817784877066>".ToIEmote()).Build()
                    : null).ConfigureAwait(false);
        }
    }

    [Cmd, Aliases, UserPerm(GuildPermission.Administrator),
     RequireContext(ContextType.Guild)]
    public async Task PreviewLinks(string yesnt)
    {
        await Service.PreviewLinks(ctx.Guild, yesnt[..1].ToLower()).ConfigureAwait(false);
        switch (await Service.GetPLinks(ctx.Guild.Id))
        {
            case 1:
                await ctx.Channel.SendConfirmAsync("Link previews are now enabled!").ConfigureAwait(false);
                break;
            case 0:
                await ctx.Channel.SendConfirmAsync("Link Previews are now disabled!").ConfigureAwait(false);
                break;
        }
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task EditSnipe()
    {
        if (!await Service.GetSnipeSet(ctx.Guild.Id))
        {
            await ReplyErrorLocalizedAsync("snipe_not_enabled", await guildSettings.GetPrefix(ctx.Guild)).ConfigureAwait(false);
            return;
        }

        {
            var msg = (await Service.GetSnipes(ctx.Guild.Id).ConfigureAwait(false))
                .Where(x => x.Edited)
                .LastOrDefault(x => x.ChannelId == ctx.Channel.Id);
            if (msg == null)
            {
                await ctx.Channel.SendErrorAsync("There's nothing to snipe!").ConfigureAwait(false);
                return;
            }

            var user = await ctx.Channel.GetUserAsync(msg.UserId).ConfigureAwait(false) ?? await client.Rest.GetUserAsync(msg.UserId).ConfigureAwait(false);

            var em = new EmbedBuilder
            {
                Author = new EmbedAuthorBuilder
                {
                    IconUrl = user.GetAvatarUrl(), Name = $"{user} originally said:"
                },
                Description = msg.Message,
                Footer = new EmbedFooterBuilder
                {
                    IconUrl = ctx.User.GetAvatarUrl(),
                    Text =
                        GetText("snipe_request", ctx.User.ToString(), (DateTime.UtcNow - msg.DateAdded).Humanize())
                },
                Color = Mewdeko.OkColor
            };

            if (msg.ReferenceMessage is not null)
                em.AddField("Replied To", msg.ReferenceMessage);

            await ctx.Channel.SendMessageAsync(embed: em.Build(),
                components: config.Data.ShowInviteButton
                    ? new ComponentBuilder()
                        .WithButton(style: ButtonStyle.Link,
                            url:
                            "https://discord.com/oauth2/authorize?client_id=752236274261426212&permissions=8&response_type=code&redirect_uri=https%3A%2F%2Fmewdeko.tech&scope=bot%20applications.commands",
                            label: "Invite Me!",
                            emote: "<a:HaneMeow:968564817784877066>".ToIEmote()).Build()
                    : null).ConfigureAwait(false);
        }
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild), Priority(1)]
    public async Task EditSnipe(IUser user1)
    {
        if (!await Service.GetSnipeSet(ctx.Guild.Id))
        {
            await ReplyErrorLocalizedAsync("snipe_not_enabled", await guildSettings.GetPrefix(ctx.Guild)).ConfigureAwait(false);
            return;
        }

        {
            var msg = (await Service.GetSnipes(ctx.Guild.Id).ConfigureAwait(false))
                .Where(x => x.Edited)
                .LastOrDefault(x => x.ChannelId == ctx.Channel.Id && x.UserId == user1.Id);
            if (msg == null)
            {
                await ReplyErrorLocalizedAsync("no_snipes").ConfigureAwait(false);
                return;
            }

            var user = await ctx.Channel.GetUserAsync(msg.UserId).ConfigureAwait(false) ?? await client.Rest.GetUserAsync(msg.UserId).ConfigureAwait(false);

            var em = new EmbedBuilder
            {
                Author = new EmbedAuthorBuilder
                {
                    IconUrl = user.GetAvatarUrl(), Name = $"{user} originally said:"
                },
                Description = msg.Message,
                Footer = new EmbedFooterBuilder
                {
                    IconUrl = ctx.User.GetAvatarUrl(),
                    Text =
                        GetText("snipe_request", ctx.User.ToString(), (DateTime.UtcNow - msg.DateAdded).Humanize())
                },
                Color = Mewdeko.OkColor
            };

            if (msg.ReferenceMessage is not null)
                em.AddField("Replied To", msg.ReferenceMessage);

            await ctx.Channel.SendMessageAsync(embed: em.Build(),
                components: config.Data.ShowInviteButton
                    ? new ComponentBuilder()
                        .WithButton(style: ButtonStyle.Link,
                            url:
                            "https://discord.com/oauth2/authorize?client_id=752236274261426212&permissions=8&response_type=code&redirect_uri=https%3A%2F%2Fmewdeko.tech&scope=bot%20applications.commands",
                            label: "Invite Me!",
                            emote: "<a:HaneMeow:968564817784877066>".ToIEmote()).Build()
                    : null).ConfigureAwait(false);
        }
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild), Priority(1)]
    public async Task EditSnipe(ITextChannel chan)
    {
        if (!await Service.GetSnipeSet(ctx.Guild.Id))
        {
            await ReplyErrorLocalizedAsync("snipe_not_enabled", await guildSettings.GetPrefix(ctx.Guild)).ConfigureAwait(false);
            return;
        }

        {
            var msg = (await Service.GetSnipes(ctx.Guild.Id).ConfigureAwait(false))
                .Where(x => x.Edited)
                .LastOrDefault(x => x.ChannelId == chan.Id);
            if (msg == null)
            {
                await ReplyErrorLocalizedAsync("no_snipes").ConfigureAwait(false);
                return;
            }

            var user = await ctx.Channel.GetUserAsync(msg.UserId).ConfigureAwait(false) ?? await client.Rest.GetUserAsync(msg.UserId).ConfigureAwait(false);

            var em = new EmbedBuilder
            {
                Author = new EmbedAuthorBuilder
                {
                    IconUrl = user.GetAvatarUrl(), Name = $"{user} originally said:"
                },
                Description = msg.Message,
                Footer = new EmbedFooterBuilder
                {
                    IconUrl = ctx.User.GetAvatarUrl(),
                    Text =
                        GetText("snipe_request", ctx.User.ToString(), (DateTime.UtcNow - msg.DateAdded).Humanize())
                },
                Color = Mewdeko.OkColor
            };

            if (msg.ReferenceMessage is not null)
                em.AddField("Replied To", msg.ReferenceMessage);


            await ctx.Channel.SendMessageAsync(embed: em.Build(),
                components: config.Data.ShowInviteButton
                    ? new ComponentBuilder()
                        .WithButton(style: ButtonStyle.Link,
                            url:
                            "https://discord.com/oauth2/authorize?client_id=752236274261426212&permissions=8&response_type=code&redirect_uri=https%3A%2F%2Fmewdeko.tech&scope=bot%20applications.commands",
                            label: "Invite Me!",
                            emote: "<a:HaneMeow:968564817784877066>".ToIEmote()).Build()
                    : null).ConfigureAwait(false);
        }
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild), Priority(1)]
    public async Task EditSnipe(ITextChannel chan, IUser user1)
    {
        if (!await Service.GetSnipeSet(ctx.Guild.Id))
        {
            await ReplyErrorLocalizedAsync("snipe_not_enabled", await guildSettings.GetPrefix(ctx.Guild)).ConfigureAwait(false);
            return;
        }

        {
            var msg = (await Service.GetSnipes(ctx.Guild.Id).ConfigureAwait(false))
                .Where(x => x.Edited)
                .LastOrDefault(x => x.ChannelId == chan.Id && x.UserId == user1.Id);
            if (msg == null)
            {
                await ReplyErrorLocalizedAsync("no_snipes").ConfigureAwait(false);
                return;
            }

            var user = await ctx.Channel.GetUserAsync(msg.UserId).ConfigureAwait(false) ?? await client.Rest.GetUserAsync(msg.UserId).ConfigureAwait(false);

            var em = new EmbedBuilder
            {
                Author = new EmbedAuthorBuilder
                {
                    IconUrl = user.GetAvatarUrl(), Name = $"{user} originally said:"
                },
                Description = msg.Message,
                Footer = new EmbedFooterBuilder
                {
                    IconUrl = ctx.User.GetAvatarUrl(),
                    Text =
                        GetText("snipe_request", ctx.User.ToString(), (DateTime.UtcNow - msg.DateAdded).Humanize())
                },
                Color = Mewdeko.OkColor
            };

            if (msg.ReferenceMessage is not null)
                em.AddField("Replied To", msg.ReferenceMessage);


            await ctx.Channel.SendMessageAsync(embed: em.Build(),
                components: config.Data.ShowInviteButton
                    ? new ComponentBuilder()
                        .WithButton(style: ButtonStyle.Link,
                            url:
                            "https://discord.com/oauth2/authorize?client_id=752236274261426212&permissions=8&response_type=code&redirect_uri=https%3A%2F%2Fmewdeko.tech&scope=bot%20applications.commands",
                            label: "Invite Me!",
                            emote: "<a:HaneMeow:968564817784877066>".ToIEmote()).Build()
                    : null).ConfigureAwait(false);
        }
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task WhosPlaying([Remainder] string? game)
    {
        game = game?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(game))
            return;

        if (ctx.Guild is not SocketGuild socketGuild)
        {
            Log.Warning("Can't cast guild to socket guild.");
            return;
        }

        var rng = new MewdekoRandom();
        var arr = await Task.Run(() => socketGuild.Users
            .Where(x => x.Activities.Any())
            .Where(u => u.Activities.FirstOrDefault().Name.ToUpperInvariant().Contains(game))
            .OrderBy(_ => rng.Next())
            .ToArray()).ConfigureAwait(false);

        var i = 0;
        if (arr.Length == 0)
        {
            await ReplyErrorLocalizedAsync("nobody_playing_game").ConfigureAwait(false);
        }
        else
        {
            var paginator = new LazyPaginatorBuilder()
                .AddUser(ctx.User)
                .WithPageFactory(PageFactory)
                .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                .WithMaxPageIndex(arr.Length / 20)
                .WithDefaultEmotes()
                .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                .Build();

            await interactivity.SendPaginatorAsync(paginator, Context.Channel,
                TimeSpan.FromMinutes(60)).ConfigureAwait(false);

            async Task<PageBuilder> PageFactory(int page)
            {
                await Task.CompletedTask.ConfigureAwait(false);
                var pagebuilder = new PageBuilder().WithOkColor()
                    .WithDescription(string.Join("\n",
                        arr.Skip(page * 20).Take(20).Select(x =>
                            $"{(i++) + 1}. {x.Username}#{x.Discriminator} `{x.Id}`: `{(x.Activities.FirstOrDefault() is CustomStatusGame cs ? cs.State : x.Activities.FirstOrDefault().Name)}`")));
                return pagebuilder;
            }
        }
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task Vote() =>
        await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                .WithDescription(
                    "Vote here for Mewdeko!\n[Vote Link](https://top.gg/bot/752236274261426212)\nMake sure to join the support server! \n[Link](https://mewdeko.tech/support)"))
            .ConfigureAwait(false);

    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task InRole([Remainder] IRole role)
    {
        await Context.Channel.TriggerTypingAsync().ConfigureAwait(false);
        await tracker.EnsureUsersDownloadedAsync(ctx.Guild).ConfigureAwait(false);

        var users = await ctx.Guild.GetUsersAsync().ConfigureAwait(false);
        var roleUsers = users
            .Where(u => u.RoleIds.Contains(role.Id))
            .ToArray();

        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(roleUsers.Length / 20)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await interactivity.SendPaginatorAsync(paginator, Context.Channel,
            TimeSpan.FromMinutes(60)).ConfigureAwait(false);

        async Task<PageBuilder> PageFactory(int page)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            return new PageBuilder().WithOkColor()
                .WithTitle(
                    $"{Format.Bold(GetText("inrole_list", Format.Bold(role.Name)))} - {roleUsers.Length}")
                .WithDescription(string.Join("\n",
                    roleUsers.Skip(page * 20).Take(20)
                        .Select(x => $"{x} `{x.Id}`"))).AddField("User Stats",
                    $"<:online:914548119730024448> {roleUsers.Count(x => x.Status == UserStatus.Online)}\n<:dnd:914548634178187294> {roleUsers.Count(x => x.Status == UserStatus.DoNotDisturb)}\n<:idle:914548262424412172> {roleUsers.Count(x => x.Status == UserStatus.Idle)}\n<:offline:914548368037003355> {roleUsers.Count(x => x.Status == UserStatus.Offline)}");
        }
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task InRoles(IRole role, IRole role2)
    {
        await Context.Channel.TriggerTypingAsync().ConfigureAwait(false);
        await tracker.EnsureUsersDownloadedAsync(ctx.Guild).ConfigureAwait(false);
        var users = await ctx.Guild.GetUsersAsync().ConfigureAwait(false);
        var roleUsers = users
            .Where(u => u.RoleIds.Contains(role.Id) && u.RoleIds.Contains(role2.Id))
            .Select(u => u.ToString())
            .ToArray();

        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(roleUsers.Length / 20)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await interactivity.SendPaginatorAsync(paginator, Context.Channel,
            TimeSpan.FromMinutes(60)).ConfigureAwait(false);

        async Task<PageBuilder> PageFactory(int page)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            return new PageBuilder().WithOkColor()
                .WithTitle(Format.Bold(
                    $"Users in the roles: {role.Name} | {role2.Name} - {roleUsers.Length}"))
                .WithDescription(string.Join("\n",
                    roleUsers.Skip(page * 20).Take(20)));
        }
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task UserId([Remainder] IGuildUser? target = null)
    {
        var usr = target ?? ctx.User;
        await ReplyConfirmLocalizedAsync("userid", "🆔", Format.Bold(usr.ToString()),
            Format.Code(usr.Id.ToString())).ConfigureAwait(false);
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task RoleId([Remainder] IRole role) =>
        await ReplyConfirmLocalizedAsync("roleid", "🆔", Format.Bold(role.ToString()),
            Format.Code(role.Id.ToString())).ConfigureAwait(false);

    [Cmd, Aliases]
    public async Task ChannelId() =>
        await ReplyConfirmLocalizedAsync("channelid", "🆔", Format.Code(ctx.Channel.Id.ToString()))
            .ConfigureAwait(false);

    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task ServerId() =>
        await ReplyConfirmLocalizedAsync("serverid", "🆔", Format.Code(ctx.Guild.Id.ToString()))
            .ConfigureAwait(false);

    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task Roles(IGuildUser? target = null)
    {
        var channel = (ITextChannel)ctx.Channel;
        var guild = channel.Guild;

        if (target != null)
        {
            var roles = target.GetRoles().Except(new[]
            {
                guild.EveryoneRole
            }).OrderBy(r => -r.Position);
            if (!roles.Any())
            {
                await ReplyErrorLocalizedAsync("no_roles_on_page").ConfigureAwait(false);
            }
            else
            {
                var paginator = new LazyPaginatorBuilder()
                    .AddUser(ctx.User)
                    .WithPageFactory(PageFactory)
                    .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                    .WithMaxPageIndex(roles.Count() / 10)
                    .WithDefaultCanceledPage()
                    .WithDefaultEmotes()
                    .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                    .Build();
                await interactivity.SendPaginatorAsync(paginator, ctx.Channel, TimeSpan.FromMinutes(60));

                async Task<PageBuilder> PageFactory(int page)
                {
                    await Task.CompletedTask;
                    return new PageBuilder().WithOkColor().WithTitle($"Roles List for {target}")
                        .WithDescription(string.Join("\n", roles.Skip(page * 10).Take(10).Select(x => $"{x.Mention} | {x.Id}")));
                }
            }
        }
        else
        {
            var roles = guild.Roles.Except(new[]
            {
                guild.EveryoneRole
            }).OrderBy(r => -r.Position);
            if (!roles.Any())
            {
                await ReplyErrorLocalizedAsync("no_roles_on_page").ConfigureAwait(false);
            }
            else
            {
                var paginator = new LazyPaginatorBuilder()
                    .AddUser(ctx.User)
                    .WithPageFactory(PageFactory)
                    .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                    .WithMaxPageIndex(roles.Count() / 10)
                    .WithDefaultCanceledPage()
                    .WithDefaultEmotes()
                    .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                    .Build();
                await interactivity.SendPaginatorAsync(paginator, ctx.Channel, TimeSpan.FromMinutes(60));

                async Task<PageBuilder> PageFactory(int page)
                {
                    await Task.CompletedTask;
                    return new PageBuilder().WithOkColor().WithTitle("Guild Roles List")
                        .WithDescription(string.Join("\n", roles.Skip(page * 10).Take(10).Select(x => $"{x.Mention} | {x.Id}")));
                }
            }
        }
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task ChannelTopic([Remainder] ITextChannel? channel = null)
    {
        channel ??= (ITextChannel)ctx.Channel;

        var topic = channel.Topic;
        if (string.IsNullOrWhiteSpace(topic))
            await ReplyErrorLocalizedAsync("no_topic_set").ConfigureAwait(false);
        else
            await ctx.Channel.SendConfirmAsync(GetText("channel_topic"), topic).ConfigureAwait(false);
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.ManageMessages), Priority(1)]
    public async Task Say(ITextChannel channel, [Remainder] string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;
        var canMention = ((IGuildUser)ctx.User).GuildPermissions.MentionEveryone;
        var anyAttachments = false;
        List<FileAttachment> attachments = new();
        if (ctx.Message.Attachments.Any())
        {
            anyAttachments = true;
            foreach (var i in ctx.Message.Attachments)
            {
                using var sr = await httpClient.GetAsync(i.Url, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                var imgData = await sr.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                var imgStream = imgData.ToStream();
                attachments.Add(new FileAttachment(imgStream, i.Filename));
            }
        }

        var rep = new ReplacementBuilder()
            .WithDefault(ctx.User, channel, (SocketGuild)ctx.Guild, (DiscordSocketClient)ctx.Client)
            .Build();

        if (SmartEmbed.TryParse(rep.Replace(message), ctx.Guild?.Id, out var embedData, out var plainText, out var components))
        {
            if (anyAttachments)
            {
                await channel.SendFilesAsync(attachments: attachments, plainText, embeds: embedData, components: components?.Build(),
                        allowedMentions: !canMention ? new AllowedMentions(AllowedMentionTypes.Users) : AllowedMentions.All)
                    .ConfigureAwait(false);
                _ = attachments.Select(async x => await x.Stream.DisposeAsync());
            }
            else
                await channel.SendMessageAsync(plainText, embeds: embedData, components: components?.Build(),
                        allowedMentions: !canMention ? new AllowedMentions(AllowedMentionTypes.Users) : AllowedMentions.All)
                    .ConfigureAwait(false);
        }
        else
        {
            var msg = rep.Replace(message);
            if (!string.IsNullOrWhiteSpace(msg))
                if (anyAttachments)
                {
                    await channel.SendFilesAsync(attachments, msg, allowedMentions: !canMention ? new AllowedMentions(AllowedMentionTypes.Users) : AllowedMentions.All)
                        .ConfigureAwait(false);
                    _ = attachments.Select(async x => await x.Stream.DisposeAsync());
                }
                else
                    await channel.SendConfirmAsync(msg).ConfigureAwait(false);
        }
    }

    [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.ManageMessages), Priority(0)]
    public async Task Say([Remainder] string? message) => await Say((ITextChannel)ctx.Channel, message);

    [Cmd, Aliases]
    public async Task Stats()
    {
        await using var uow = db.GetDbContext();
        var time = DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(5));
        var commandStats = uow.CommandStats.Count(x => x.DateAdded.Value >= time);
        var user = await client.Rest.GetUserAsync(280835732728184843).ConfigureAwait(false);
        await ctx.Channel.EmbedAsync(
                new EmbedBuilder().WithOkColor()
                    .WithAuthor($"{client.CurrentUser.Username} v{StatsService.BotVersion}", client.CurrentUser.GetAvatarUrl(), config.Data.SupportServer)
                    .AddField(GetText("author"), $"{user.Mention} | {user.Username}#{user.Discriminator}")
                    .AddField(GetText("commands_ran"), $"{commandStats}/5s")
                    .AddField("Library", stats.Library)
                    .AddField(GetText("owner_ids"), string.Join("\n", creds.OwnerIds.Select(x => $"<@{x}>")))
                    .AddField(GetText("shard"), $"#{client.ShardId} / {creds.TotalShards}")
                    .AddField(GetText("memory"), $"{stats.Heap} MB")
                    .AddField(GetText("uptime"), stats.GetUptimeString("\n"))
                    .AddField("Servers", $"{coordinator.GetGuildCount()} Servers"))
            .ConfigureAwait(false);
    }

    [Cmd, Aliases]
    public async Task Showemojis([Remainder] string _)
    {
        var tags = ctx.Message.Tags.Where(t => t.Type == TagType.Emoji).Select(t => (Emote)t.Value);

        var result = string.Join("\n", tags.Select(m => GetText("showemojis", m, m.Url)));

        if (string.IsNullOrWhiteSpace(result))
            await ReplyErrorLocalizedAsync("showemojis_none").ConfigureAwait(false);
        else
            await ctx.Channel.SendMessageAsync(result.TrimTo(2000)).ConfigureAwait(false);
    }

    [Cmd, Ratelimit(30)]
    public async Task Ping()
    {
        await Sem.WaitAsync(5000).ConfigureAwait(false);
        try
        {
            var sw = Stopwatch.StartNew();
            var msg = await ctx.Channel.SendMessageAsync("🏓").ConfigureAwait(false);
            sw.Stop();
            msg.DeleteAfter(0);

            await ctx.Channel
                .SendConfirmAsync(
                    $"Bot Ping {(int)sw.Elapsed.TotalMilliseconds}ms\nBot Latency {((DiscordSocketClient)ctx.Client).Latency}ms")
                .ConfigureAwait(false);
        }
        finally
        {
            Sem.Release();
        }
    }

    [Cmd, Aliases]
    public async Task Roll([Remainder] string roll)
    {
        RollResult result;
        try
        {
            result = RollCommandService.ParseRoll(roll);
        }
        catch (ArgumentException ex)
        {
            await ReplyErrorLocalizedAsync("roll_fail_new_dm", GetText(ex.Message)).ConfigureAwait(false);
            return;
        }

        async Task<PageBuilder> PageFactory(int page)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            return new PageBuilder()
                .WithOkColor()
                .WithFields(result.Results.Skip(page * 10)
                    .Take(10)
                    .Select(x => new EmbedFieldBuilder()
                        .WithName(x.Key.ToString())
                        .WithValue(string.Join(',', x.Value))).ToArray())
                .WithDescription(result.InacurateTotal
                    // hide possible int rollover errors
                    ? GetText("roll_fail_too_large")
                    : result.ToString());
        }

        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(result.Results.Count / 10)
            .WithDefaultCanceledPage()
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await interactivity.SendPaginatorAsync(paginator, ctx.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);
    }

    [Cmd, Aliases]
    public async Task OwoIfy([Remainder] string input)
        => await ctx.Channel.SendMessageAsync(OwoServices.OwoIfy(input).SanitizeMentions(true)).ConfigureAwait(false);
}