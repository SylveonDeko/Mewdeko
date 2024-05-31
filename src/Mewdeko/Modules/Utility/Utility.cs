using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Humanizer;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.JsonSettings;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.Utility.Common;
using Mewdeko.Modules.Utility.Services;
using Mewdeko.Services.Impl;
using Mewdeko.Services.Settings;
using Newtonsoft.Json;
using Serilog;
using StringExtensions = Mewdeko.Extensions.StringExtensions;

namespace Mewdeko.Modules.Utility;

/// <summary>
/// Contains various utility commands like sniping deleted messages, getting a list of roles with specific permissions, etc.
/// </summary>
/// <param name="client"></param>
/// <param name="stats"></param>
/// <param name="creds"></param>
/// <param name="tracker"></param>
/// <param name="serv"></param>
/// <param name="coordinator"></param>
/// <param name="guildSettings"></param>
/// <param name="httpClient"></param>
/// <param name="config"></param>
/// <param name="db"></param>
public partial class Utility(
    DiscordSocketClient client,
    IStatsService stats,
    IBotCredentials creds,
    DownloadTracker tracker,
    CommandService cmdServ,
    InteractiveService serv,
    ICoordinator coordinator,
    GuildSettingsService guildSettings,
    HttpClient httpClient,
    BotConfigService config,
    DbService db,
    IDataCache cache, CryptoService cryptoService)
    : MewdekoModuleBase<UtilityService>
{
    /// <summary>
    /// Parses the type of permission search.
    /// </summary>
    public enum PermissionType
    {
        /// <summary>
        /// Searches for roles that have all the specified permissions.
        /// </summary>
        And,

        /// <summary>
        /// Searches for roles that have any of the specified permissions.
        /// </summary>
        Or
    }

    private static readonly SemaphoreSlim Sem = new(1, 1);

    /// <summary>
    /// Crypto command to generate a chart with the given days
    /// </summary>
    /// <param name="cryptoName"></param>
    /// <param name="time"></param>
    [Cmd, Aliases]
    public async Task Crypto(string cryptoName, StoopidTime? time = null)
    {
        var (image, embed) = await cryptoService.GenerateCryptoPriceChartAsync(cryptoName, time is null ? TimeSpan.FromDays(1).Days : time.Time.Days);
        await ctx.Channel.SendFileAsync(stream: image, "cryptopricechart.png", embed: embed);
    }


    /// <summary>
    /// Debug command to test parsing of embeds.
    /// </summary>
    /// <param name="embedText">The text to parse as an embed.</param>
    [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.Administrator)]
    public async Task DebugEmbed([Remainder] string embedText)
    {
        var watch = Stopwatch.StartNew();
        try
        {
            SmartEmbed.TryParse(embedText, ctx.Guild.Id, out var embeds, out var plainText, out var components);
            var comps = components?.Build();
            watch.Stop();
            var eb = new EmbedBuilder()
                .WithTitle("Embed Successfully Parsed")
                .WithOkColor()
                .WithDescription($"`PlainText Length:` ***{plainText.Length}***\n" +
                                 $"`Embed Count:` ***{embeds?.Length}***\n" +
                                 $"`Component Count:` ***{comps?.Components.Count}")
                .WithFooter($"Execution Time: {watch.Elapsed}");
            await ctx.Channel.SendMessageAsync(plainText, embeds: embeds, components: comps);
            await ctx.Channel.SendMessageAsync(embed: eb.Build());
        }
        catch (Exception e)
        {
            var eb = new EmbedBuilder()
                .WithTitle("Error Parsing Embed")
                .WithDescription(e.ToString());
            await ctx.Channel.SendMessageAsync(embed: eb.Build());
        }
    }

    /// <summary>
    /// Lists all roles that have the specified permissions.
    /// </summary>
    /// <param name="searchType">The type of permission search (And or Or).</param>
    /// <param name="perms">The permissions to search for.</param>
    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task RolePermList(PermissionType searchType = PermissionType.And, params GuildPermission[] perms)
    {
        List<IRole> rolesWithPerms;
        var rolesWithMatchedPerms = new Dictionary<IRole, List<GuildPermission>>();

        if (searchType == PermissionType.And)
        {
            rolesWithPerms = (from role in ctx.Guild.Roles
                let hasAllPerms = perms.All(perm => role.Permissions.Has(perm))
                where hasAllPerms
                select role).ToList();
        }
        else // PermissionType.Or
        {
            rolesWithPerms = (from role in ctx.Guild.Roles
                let matchedPerms = perms.Where(perm => role.Permissions.Has(perm)).ToList()
                where matchedPerms.Any()
                select role).ToList();

            foreach (var role in rolesWithPerms)
            {
                rolesWithMatchedPerms[role] = perms.Where(perm => role.Permissions.Has(perm)).ToList();
            }
        }

        if (!rolesWithPerms.Any() && !rolesWithMatchedPerms.Any())
        {
            await ctx.Channel.SendErrorAsync("No roles with the specified permissions were found.", Config);
            return;
        }

        var paginator = new LazyPaginatorBuilder()
            .WithUsers(ctx.User)
            .WithMaxPageIndex(searchType == PermissionType.Or
                ? (rolesWithMatchedPerms.Count - 1) / 6
                : (rolesWithPerms.Count - 1) / 6)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber)
            .WithDefaultEmotes()
            .Build();

        await serv.SendPaginatorAsync(paginator, ctx.Channel, TimeSpan.FromMinutes(5));

        async Task<PageBuilder> PageFactory(int pagenum)
        {
            var embed = new PageBuilder()
                .WithOkColor()
                .WithTitle("Roles with the specified permissions");

            if (searchType == PermissionType.And)
            {
                foreach (var role in rolesWithPerms.Skip(pagenum * 6).Take(6))
                {
                    embed.AddField(role.Name,
                        $"`Id`: {role.Id}\n`Mention`: {role.Mention}\n`Users`: {(await role.GetMembersAsync()).Count()}");
                }
            }
            else // PermissionType.Or
            {
                foreach (var role in rolesWithMatchedPerms.Skip(pagenum * 6).Take(6))
                {
                    embed.AddField(role.Key.Name,
                        $"`Id`: {role.Key.Id}\n`Mention`: {role.Key.Mention}\n`Users`: {(await role.Key.GetMembersAsync()).Count()}\n`Matched Permissions`: {string.Join(", ", role.Value)}");
                }
            }

            return embed;
        }
    }

    /// <summary>
    /// Lists all roles that have the specified permissions. Default search type is And.
    /// </summary>
    /// <param name="perms">The permissions to search for.</param>
    /// <returns></returns>
    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public Task RolePermList(params GuildPermission[] perms)
        => RolePermList(PermissionType.And, perms);

    /// <summary>
    /// Gets the mewdeko specific json of a message.
    /// </summary>
    /// <param name="id">The id of the message to get the json of.</param>
    /// <param name="channel">The channel of the message.</param>
    [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.ManageMessages)]
    public async Task GetJson(ulong id, ITextChannel channel = null)
    {
        channel ??= ctx.Channel as ITextChannel;
        var settings = new JsonSerializerSettings
        {
            ContractResolver = new LowercaseContractResolver(), NullValueHandling = NullValueHandling.Ignore
        };

        var message = await channel.GetMessageAsync(id);
        var serialized = JsonConvert.SerializeObject(message.GetNewEmbedSource(), Formatting.Indented, settings);
        using var ms = new MemoryStream();
        await using var writer = new StreamWriter(ms);
        await writer.WriteAsync(serialized);
        await writer.FlushAsync();
        ms.Position = 0;
        await ctx.Channel.SendFileAsync(ms, "EmbedJson.txt");
        await ms.DisposeAsync();
        await writer.DisposeAsync();
    }

    /// <summary>
    /// Gets the mewdeko specific json of a message.
    /// </summary>
    /// <param name="channel">The channel of the message.</param>
    /// <param name="messageId">The id of the message to get the json of.</param>
    /// <returns></returns>
    [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.ManageMessages)]
    public Task GetJson(ITextChannel channel, ulong messageId)
        => GetJson(messageId, channel);

    /// <summary>
    /// Saves the chat log of a channel. Public mewdeko saves this to the nginx cdn then sends you a link to display it on the cdn
    /// </summary>
    /// <param name="time"></param>
    /// <param name="channel"></param>
    [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.ManageMessages)]
    public async Task SaveChat(StoopidTime time, ITextChannel? channel = null)
    {
        var curTime = DateTime.UtcNow.Subtract(time.Time);
        if (!Directory.Exists(creds.ChatSavePath))
        {
            await ctx.Channel.SendErrorAsync("Chat save directory does not exist. Please create it.", Config)
                .ConfigureAwait(false);
            return;
        }

        var secureString = StringExtensions.GenerateSecureString(16);
        try
        {
            Directory.CreateDirectory($"{creds.ChatSavePath}/{ctx.Guild.Id}/{secureString}");
        }
        catch (Exception ex)
        {
            await ctx.Channel.SendErrorAsync($"Failed to create directory. {ex.Message}", Config).ConfigureAwait(false);
            return;
        }

        if (time.Time.Days > 3)
        {
            await ctx.Channel
                .SendErrorAsync("Max time to grab messages is 3 days. This will be increased in the near future.",
                    Config)
                .ConfigureAwait(false);
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
            await ctx.Channel.SendConfirmAsync(
                $"{config.Data.LoadingEmote} Saving chat log, this may take some time...");
        }

        await process.WaitForExitAsync().ConfigureAwait(false);
        if (creds.ChatSavePath.Contains("/usr/share/nginx/cdn"))
            await ctx.User.SendConfirmAsync(
                    $"Your chat log is here: https://cdn.mewdeko.tech/chatlogs/{ctx.Guild.Id}/{secureString}/{ctx.Guild.Name.Replace(" ", "-")}-{(channel?.Name ?? ctx.Channel.Name).Replace(" ", "-")}-{curTime:yyyy-MM-ddTHH-mm-ssZ}.html")
                .ConfigureAwait(false);
        else
            await ctx.Channel
                .SendConfirmAsync($"Your chat log is here: {creds.ChatSavePath}/{ctx.Guild.Id}/{secureString}")
                .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the emotes in a guild. If an emote type is specified, only the emotes of that type will be listed.
    /// </summary>
    /// <param name="emotetype">The type of emotes to list (animated or nonanimated).</param>
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
            await ctx.Channel.SendErrorAsync("No emotes found!", Config).ConfigureAwait(false);
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

        await serv.SendPaginatorAsync(paginator, Context.Channel,
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

    /// <summary>
    /// Gets the bots invite link. As well as showing the website, docs, and support server.
    /// </summary>
    [Cmd, Aliases]
    public async Task Invite()
    {
        var eb = new EmbedBuilder()
            .AddField("Invite Link",
                "[Invite Link](https://discord.com/oauth2/authorize?client_id=752236274261426212&scope=bot&permissions=66186303)")
            .AddField("Website/Docs", "https://mewdeko.tech")
            .AddField("Support Server", config.Data.SupportServer)
            .WithOkColor();
        await ctx.Channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
    }

    /// <summary>
    /// Enables or disables sniping of deleted messages. Default is disabled.
    /// </summary>
    /// <param name="value">The value to set.</param>
    [Cmd, Aliases, UserPerm(GuildPermission.Administrator),
     RequireContext(ContextType.Guild)]
    public async Task SnipeSet(PermissionAction value)
    {
        await Service.SnipeSet(ctx.Guild, value.Value).ConfigureAwait(false);
        var t = await Service.GetSnipeSet(ctx.Guild.Id);
        await ReplyConfirmLocalizedAsync("snipe_set", t ? "Enabled" : "Disabled").ConfigureAwait(false);
    }

    /// <summary>
    /// Snipes the last deleted message in the channel.
    /// </summary>
    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task Snipe()
    {
        if (!await Service.GetSnipeSet(ctx.Guild.Id))
        {
            await ReplyErrorLocalizedAsync("snipe_not_enabled", await guildSettings.GetPrefix(ctx.Guild))
                .ConfigureAwait(false);
            return;
        }

        await Service.GetSnipes(ctx.Guild.Id).ConfigureAwait(false);
        var msg = (await Service.GetSnipes(ctx.Guild.Id).ConfigureAwait(false)).LastOrDefault(x =>
            x.ChannelId == ctx.Channel.Id && !x.Edited);
        if (msg is null)
        {
            await ctx.Channel.SendErrorAsync("There is nothing to snipe here!", Config).ConfigureAwait(false);
            return;
        }

        var user = await ctx.Channel.GetUserAsync(msg.UserId).ConfigureAwait(false) ??
                   await client.Rest.GetUserAsync(msg.UserId).ConfigureAwait(false);

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

    /// <summary>
    /// Gets the last x amount of deleted messages in a channel.
    /// </summary>
    /// <param name="amount">The amount of messages to get.</param>
    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task SnipeList(int amount = 5)
    {
        if (!await Service.GetSnipeSet(ctx.Guild.Id))
        {
            await ReplyErrorLocalizedAsync("snipe_not_enabled", await guildSettings.GetPrefix(ctx.Guild))
                .ConfigureAwait(false);
            return;
        }

        var msgs = (await Service.GetSnipes(ctx.Guild.Id).ConfigureAwait(false)).Where(x =>
            x.ChannelId == ctx.Channel.Id && !x.Edited);
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

            await serv.SendPaginatorAsync(paginator, Context.Channel,
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

    /// <summary>
    /// Gets the last x amount of deleted messages by a user in a channel.
    /// </summary>
    /// <param name="user">The user to get the messages of.</param>
    /// <param name="amount">The amount of messages to get.</param>
    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task SnipeList(IUser user, int amount = 5)
    {
        if (!await Service.GetSnipeSet(ctx.Guild.Id))
        {
            await ReplyErrorLocalizedAsync("snipe_not_enabled", await guildSettings.GetPrefix(ctx.Guild))
                .ConfigureAwait(false);
            return;
        }

        var msgs = (await Service.GetSnipes(ctx.Guild.Id).ConfigureAwait(false)).Where(x =>
            x.ChannelId == ctx.Channel.Id && x.UserId == user.Id && !x.Edited);
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

            await serv.SendPaginatorAsync(paginator, Context.Channel,
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

    /// <summary>
    /// Gets the last x amount of deleted messages in a channel.
    /// </summary>
    /// <param name="channel">The channel to get the messages of.</param>
    /// <param name="amount">The amount of messages to get.</param>
    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task SnipeList(ITextChannel channel, int amount = 5)
    {
        if (!await Service.GetSnipeSet(ctx.Guild.Id))
        {
            await ReplyErrorLocalizedAsync("snipe_not_enabled", await guildSettings.GetPrefix(ctx.Guild))
                .ConfigureAwait(false);
            return;
        }

        var msgs = (await Service.GetSnipes(ctx.Guild.Id).ConfigureAwait(false)).Where(x =>
            x.ChannelId == channel.Id && !x.Edited);
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

            await serv.SendPaginatorAsync(paginator, Context.Channel,
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

    /// <summary>
    /// Gets the last x amount of deleted messages by a user in a specified channel.
    /// </summary>
    /// <param name="channel">The channel to get the messages of.</param>
    /// <param name="user">The user to get the messages of.</param>
    /// <param name="amount">The amount of messages to get.</param>
    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task SnipeList(ITextChannel channel, IUser user, int amount = 5)
    {
        if (!await Service.GetSnipeSet(ctx.Guild.Id))
        {
            await ReplyErrorLocalizedAsync("snipe_not_enabled", await guildSettings.GetPrefix(ctx.Guild))
                .ConfigureAwait(false);
            return;
        }

        var msgs = (await Service.GetSnipes(ctx.Guild.Id).ConfigureAwait(false)).Where(x =>
            x.UserId == user.Id && x.ChannelId == channel.Id && !x.Edited);
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

            await serv.SendPaginatorAsync(paginator, Context.Channel,
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

    /// <summary>
    /// Gets the last x amount of edited messages in the current channel.
    /// </summary>
    /// <param name="amount">The amount of messages to get.</param>
    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task EditSnipeList(int amount = 5)
    {
        if (!await Service.GetSnipeSet(ctx.Guild.Id))
        {
            await ReplyErrorLocalizedAsync("snipe_not_enabled", await guildSettings.GetPrefix(ctx.Guild))
                .ConfigureAwait(false);
            return;
        }

        var msgs = (await Service.GetSnipes(ctx.Guild.Id).ConfigureAwait(false)).Where(x =>
            x.ChannelId == ctx.Channel.Id && x.Edited);
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

            await serv.SendPaginatorAsync(paginator, Context.Channel,
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

    /// <summary>
    /// Gets the last x amount of edited messages in a channel by a user.
    /// </summary>
    /// <param name="user">The user to get the messages of.</param>
    /// <param name="amount">The amount of messages to get.</param>
    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task EditSnipeList(IUser user, int amount = 5)
    {
        if (!await Service.GetSnipeSet(ctx.Guild.Id))
        {
            await ReplyErrorLocalizedAsync("snipe_not_enabled", await guildSettings.GetPrefix(ctx.Guild))
                .ConfigureAwait(false);
            return;
        }

        var msgs = (await Service.GetSnipes(ctx.Guild.Id).ConfigureAwait(false)).Where(x =>
            x.ChannelId == ctx.Channel.Id && x.UserId == user.Id && x.Edited);
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

            await serv.SendPaginatorAsync(paginator, Context.Channel,
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

    /// <summary>
    /// Gets the last x amount of edited messages in a channel.
    /// </summary>
    /// <param name="channel">The channel to get the messages of.</param>
    /// <param name="amount">The amount of messages to get.</param>
    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task EditSnipeList(ITextChannel channel, int amount = 5)
    {
        if (!await Service.GetSnipeSet(ctx.Guild.Id))
        {
            await ReplyErrorLocalizedAsync("snipe_not_enabled", await guildSettings.GetPrefix(ctx.Guild))
                .ConfigureAwait(false);
            return;
        }

        var msgs = (await Service.GetSnipes(ctx.Guild.Id).ConfigureAwait(false)).Where(x =>
            x.ChannelId == channel.Id && x.Edited);
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

            await serv.SendPaginatorAsync(paginator, Context.Channel,
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

    /// <summary>
    /// Gets the last x amount of edited messages by a user in a specified channel.
    /// </summary>
    /// <param name="channel">The channel to get the messages of.</param>
    /// <param name="user">The user to get the messages of.</param>
    /// <param name="amount">The amount of messages to get.</param>
    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task EditSnipeList(ITextChannel channel, IUser user, int amount = 5)
    {
        if (!await Service.GetSnipeSet(ctx.Guild.Id))
        {
            await ReplyErrorLocalizedAsync("snipe_not_enabled", await guildSettings.GetPrefix(ctx.Guild))
                .ConfigureAwait(false);
            return;
        }

        var msgs = (await Service.GetSnipes(ctx.Guild.Id).ConfigureAwait(false)).Where(x =>
            x.UserId == user.Id && x.ChannelId == channel.Id && x.Edited);
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

            await serv.SendPaginatorAsync(paginator, Context.Channel,
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


    /// <summary>
    /// Snipes the last deleted message by a user in the current channel.
    /// </summary>
    /// <param name="user1">The user to get the message of.</param>
    [Cmd, Aliases, RequireContext(ContextType.Guild), Priority(1)]
    public async Task Snipe(IUser user1)
    {
        if (!await Service.GetSnipeSet(ctx.Guild.Id))
        {
            await ReplyErrorLocalizedAsync("snipe_not_enabled", await guildSettings.GetPrefix(ctx.Guild))
                .ConfigureAwait(false);
            return;
        }

        var msg = (await Service.GetSnipes(ctx.Guild.Id).ConfigureAwait(false))
            .Find(x => x.ChannelId == ctx.Channel.Id && x.UserId == user1.Id && !x.Edited);
        if (msg is null)
        {
            await ctx.Channel.SendErrorAsync("There is nothing to snipe for this user!", Config).ConfigureAwait(false);
            return;
        }

        var user = await ctx.Channel.GetUserAsync(msg.UserId).ConfigureAwait(false) ??
                   await client.Rest.GetUserAsync(msg.UserId).ConfigureAwait(false);
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

    /// <summary>
    /// Checks a url for viruses using the virustotal api.
    /// </summary>
    /// <param name="url">The url to check.</param>
    [Cmd, Aliases, RequireContext(ContextType.Guild), Priority(2)]
    public async Task VCheck([Remainder] string? url = null)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            await ctx.Channel.SendErrorAsync("You didn't specify a url", Config).ConfigureAwait(false);
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

    /// <summary>
    /// Snipes the last deleted message in a specified channel.
    /// </summary>
    /// <param name="chan">The channel to get the message of.</param>
    [Cmd, Aliases, RequireContext(ContextType.Guild), Priority(2)]
    public async Task Snipe(ITextChannel chan)
    {
        if (!await Service.GetSnipeSet(ctx.Guild.Id))
        {
            await ReplyErrorLocalizedAsync("snipe_not_enabled", await guildSettings.GetPrefix(ctx.Guild))
                .ConfigureAwait(false);
            return;
        }

        var msg = (await Service.GetSnipes(ctx.Guild.Id).ConfigureAwait(false)).Where(x => !x.Edited)
            .LastOrDefault(x => x.ChannelId == chan.Id);
        if (msg == null)
        {
            await ReplyErrorLocalizedAsync("no_snipes").ConfigureAwait(false);
            return;
        }

        var user = await ctx.Channel.GetUserAsync(msg.UserId).ConfigureAwait(false) ??
                   await client.Rest.GetUserAsync(msg.UserId).ConfigureAwait(false);

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

    /// <summary>
    /// Snipes the last deleted message by a user in a specified channel.
    /// </summary>
    /// <param name="chan">The channel to get the message of.</param>
    /// <param name="user1">The user to get the message of.</param>
    [Cmd, Aliases, RequireContext(ContextType.Guild), Priority(2)]
    public async Task Snipe(ITextChannel chan, IUser user1)
    {
        if (!await Service.GetSnipeSet(ctx.Guild.Id))
        {
            await ReplyErrorLocalizedAsync("snipe_not_enabled", await guildSettings.GetPrefix(ctx.Guild))
                .ConfigureAwait(false);
            return;
        }

        var msg = (await Service.GetSnipes(ctx.Guild.Id).ConfigureAwait(false)).Where(x => !x.Edited)
            .LastOrDefault(x => x.ChannelId == chan.Id && x.UserId == user1.Id);
        {
            if (msg == null)
            {
                await ctx.Channel.SendErrorAsync("There's nothing to snipe for that channel and user!", Config)
                    .ConfigureAwait(false);
                return;
            }

            var user = await ctx.Channel.GetUserAsync(msg.UserId).ConfigureAwait(false) ??
                       await client.Rest.GetUserAsync(msg.UserId).ConfigureAwait(false);

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

    /// <summary>
    /// Snipes the last edited message in the current channel.
    /// </summary>
    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task EditSnipe()
    {
        if (!await Service.GetSnipeSet(ctx.Guild.Id))
        {
            await ReplyErrorLocalizedAsync("snipe_not_enabled", await guildSettings.GetPrefix(ctx.Guild))
                .ConfigureAwait(false);
            return;
        }

        var snipes = await Service.GetSnipes(ctx.Guild.Id).ConfigureAwait(false);
        var msg = snipes
            .Where(x => x.Edited)
            .LastOrDefault(x => x.ChannelId == ctx.Channel.Id);
        if (msg == null)
        {
            await ctx.Channel.SendErrorAsync("There's nothing to snipe!", Config).ConfigureAwait(false);
            return;
        }

        var user = await ctx.Channel.GetUserAsync(msg.UserId).ConfigureAwait(false) ??
                   await client.Rest.GetUserAsync(msg.UserId).ConfigureAwait(false);

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

    /// <summary>
    /// Snipes the last edited message by a user in the current channel.
    /// </summary>
    /// <param name="user1">The user to get the message of.</param>
    [Cmd, Aliases, RequireContext(ContextType.Guild), Priority(1)]
    public async Task EditSnipe(IUser user1)
    {
        if (!await Service.GetSnipeSet(ctx.Guild.Id))
        {
            await ReplyErrorLocalizedAsync("snipe_not_enabled", await guildSettings.GetPrefix(ctx.Guild))
                .ConfigureAwait(false);
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

            var user = await ctx.Channel.GetUserAsync(msg.UserId).ConfigureAwait(false) ??
                       await client.Rest.GetUserAsync(msg.UserId).ConfigureAwait(false);

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

    /// <summary>
    /// Snipes the last edited message in a specified channel.
    /// </summary>
    /// <param name="chan">The channel to get the message of.</param>
    [Cmd, Aliases, RequireContext(ContextType.Guild), Priority(1)]
    public async Task EditSnipe(ITextChannel chan)
    {
        if (!await Service.GetSnipeSet(ctx.Guild.Id))
        {
            await ReplyErrorLocalizedAsync("snipe_not_enabled", await guildSettings.GetPrefix(ctx.Guild))
                .ConfigureAwait(false);
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

            var user = await ctx.Channel.GetUserAsync(msg.UserId).ConfigureAwait(false) ??
                       await client.Rest.GetUserAsync(msg.UserId).ConfigureAwait(false);

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

    /// <summary>
    /// Snipes the last edited message by a user in a specified channel.
    /// </summary>
    /// <param name="chan">The channel to get the message of.</param>
    /// <param name="user1">The user to get the message of.</param>
    [Cmd, Aliases, RequireContext(ContextType.Guild), Priority(1)]
    public async Task EditSnipe(ITextChannel chan, IUser user1)
    {
        if (!await Service.GetSnipeSet(ctx.Guild.Id))
        {
            await ReplyErrorLocalizedAsync("snipe_not_enabled", await guildSettings.GetPrefix(ctx.Guild))
                .ConfigureAwait(false);
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

            var user = await ctx.Channel.GetUserAsync(msg.UserId).ConfigureAwait(false) ??
                       await client.Rest.GetUserAsync(msg.UserId).ConfigureAwait(false);

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

    /// <summary>
    /// Shows a list of users playing a specified game.
    /// </summary>
    /// <param name="game">The game to search for.</param>
    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task WhosPlaying([Remainder] string? game)
    {
        game = game?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(game))
            return;

        if (ctx.Guild is not SocketGuild socketGuild)
        {
            Log.Warning("Can't cast guild to socket guild");
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

            await serv.SendPaginatorAsync(paginator, Context.Channel,
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

    /// <summary>
    /// Shows a link to vote for mewdeko.
    /// </summary>
    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task Vote() =>
        await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                .WithDescription(
                    "Vote here for Mewdeko!\n[Vote Link](https://top.gg/bot/752236274261426212)\nMake sure to join the support server! \n[Link](https://mewdeko.tech/support)"))
            .ConfigureAwait(false);

    /// <summary>
    /// Shows a list of users in a specified role.
    /// </summary>
    /// <param name="role">The role to search for.</param>
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

        await serv.SendPaginatorAsync(paginator, Context.Channel,
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

    /// <summary>
    /// Shows a list of users in the specified roles.
    /// </summary>
    /// <param name="role">The first role to search for.</param>
    /// <param name="role2">The second role to search for.</param>
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

        await serv.SendPaginatorAsync(paginator, Context.Channel,
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

    /// <summary>
    /// Gets the user id of a specified user.
    /// </summary>
    /// <param name="target">The user to get the id of.</param>
    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task UserId([Remainder] IGuildUser? target = null)
    {
        var usr = target ?? ctx.User;
        await ReplyConfirmLocalizedAsync("userid", "🆔", Format.Bold(usr.ToString()),
            Format.Code(usr.Id.ToString())).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the role id of a specified role.
    /// </summary>
    /// <param name="role">The role to get the id of.</param>
    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task RoleId([Remainder] IRole role) =>
        await ReplyConfirmLocalizedAsync("roleid", "🆔", Format.Bold(role.ToString()),
            Format.Code(role.Id.ToString())).ConfigureAwait(false);

    /// <summary>
    /// Gets the channel id of the current channel.
    /// </summary>
    [Cmd, Aliases]
    public async Task ChannelId() =>
        await ReplyConfirmLocalizedAsync("channelid", "🆔", Format.Code(ctx.Channel.Id.ToString()))
            .ConfigureAwait(false);

    /// <summary>
    /// Gets the server id of the current server.
    /// </summary>
    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task ServerId() =>
        await ReplyConfirmLocalizedAsync("serverid", "🆔", Format.Code(ctx.Guild.Id.ToString()))
            .ConfigureAwait(false);

    /// <summary>
    /// Gets a list of roles in the current server. Shows a user's roles if a user is specified.
    /// </summary>
    /// <param name="target">The user to get the roles of.</param>
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
                await serv.SendPaginatorAsync(paginator, ctx.Channel, TimeSpan.FromMinutes(60));

                async Task<PageBuilder> PageFactory(int page)
                {
                    await Task.CompletedTask;
                    return new PageBuilder().WithOkColor().WithTitle($"Roles List for {target}")
                        .WithDescription(string.Join("\n",
                            roles.Skip(page * 10).Take(10).Select(x =>
                                $"{x.Mention} | {x.Id} | {x.GetMembersAsync().GetAwaiter().GetResult().Count()} Members")));
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
                await serv.SendPaginatorAsync(paginator, ctx.Channel, TimeSpan.FromMinutes(60));

                async Task<PageBuilder> PageFactory(int page)
                {
                    await Task.CompletedTask;
                    return new PageBuilder().WithOkColor().WithTitle("Guild Roles List")
                        .WithDescription(string.Join("\n",
                            roles.Skip(page * 10).Take(10).Select(x => x as SocketRole)
                                .Select(x =>
                                    $"{x.Mention} | {x.Id} | {x.GetMembersAsync().GetAwaiter().GetResult().Count()}")));
                }
            }
        }
    }

    /// <summary>
    /// Gets the topic of the current channel. Shows the topic of a specified channel if one is specified.
    /// </summary>
    /// <param name="channel">The channel to get the topic of.</param>
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

    /// <summary>
    /// Used to say or embed a message as the bot.
    /// </summary>
    /// <param name="channel">The channel to send the message to.</param>
    /// <param name="message">The message to send.</param>
    [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.ManageMessages), Priority(1)]
    public async Task Say(ITextChannel channel, [Remainder] string? message)
    {
        var isTextInAttachments = ctx.Message.Attachments.Any(x => x.Filename.EndsWith("txt"));

        if (string.IsNullOrWhiteSpace(message) && !isTextInAttachments)
            return;

        var canMention = ((IGuildUser)ctx.User).GuildPermissions.MentionEveryone;

        var (attachments, processedMessage, streams) = await HandleAttachmentsAsync(isTextInAttachments, message);

        var rep = new ReplacementBuilder()
            .WithDefault(ctx.User, channel, (SocketGuild)ctx.Guild, (DiscordSocketClient)ctx.Client)
            .Build();

        var msg = rep.Replace(processedMessage);

        if (SmartEmbed.TryParse(msg, ctx.Guild?.Id, out var embedData, out var plainText, out var components))
        {
            if (attachments.Any())
            {
                try
                {
                    await channel.SendFilesAsync(attachments: attachments, plainText, embeds: embedData,
                            components: components?.Build(),
                            allowedMentions: !canMention
                                ? new AllowedMentions(AllowedMentionTypes.Users)
                                : AllowedMentions.All)
                        .ConfigureAwait(false);
                    foreach (var i in streams)
                        await i.DisposeAsync();
                }
                catch (Exception ex)
                {
                    await ctx.Channel.SendErrorAsync(GetText("embed_failed"), Config);
                    Log.Error("Error sending message: {Message}", ex.Message);
                }
            }
            else
                try
                {
                    await channel.SendMessageAsync(plainText, embeds: embedData, components: components?.Build(),
                            allowedMentions: !canMention
                                ? new AllowedMentions(AllowedMentionTypes.Users)
                                : AllowedMentions.All)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    await ctx.Channel.SendErrorAsync(GetText("embed_failed"), Config);
                    Log.Error("Error sending message: {Message}", ex.Message);
                }
        }
        else if (!string.IsNullOrWhiteSpace(msg))
        {
            if (attachments.Any())
            {
                try
                {
                    await channel.SendFilesAsync(attachments, msg,
                            allowedMentions: !canMention
                                ? new AllowedMentions(AllowedMentionTypes.Users)
                                : AllowedMentions.All)
                        .ConfigureAwait(false);
                    foreach (var i in streams)
                        await i.DisposeAsync();
                }
                catch
                {
                    await ctx.Channel.SendErrorAsync(GetText("embed_failed"), Config);
                }
            }
            else
                try
                {
                    await channel.SendMessageAsync(msg, allowedMentions: !canMention
                        ? new AllowedMentions(AllowedMentionTypes.Users)
                        : AllowedMentions.All);
                }
                catch (Exception ex)
                {
                    await ctx.Channel.SendErrorAsync(GetText("embed_failed"), Config);
                    Log.Error("Error sending message: {Message}", ex.Message);
                }
        }
    }

    private async Task<(List<FileAttachment> attachments, string? message, List<MemoryStream> streams)>
        HandleAttachmentsAsync(bool isTextInAttachments, string? message)
    {
        var attachments = new List<FileAttachment>();
        var streams = new List<MemoryStream>();
        if (!ctx.Message.Attachments.Any()) return (attachments, message, streams);

        var userAttachments = new List<IAttachment>(ctx.Message.Attachments);

        if (isTextInAttachments &&
            await PromptUserConfirmAsync("Do you want to use the text file in your attachments as the message?",
                ctx.User.Id))
        {
            var txtAttachment = userAttachments.First(x => x.Filename.EndsWith("txt"));
            message = await httpClient.GetStringAsync(txtAttachment.Url);
            userAttachments.Remove(txtAttachment);
        }

        foreach (var i in userAttachments)
        {
            using var sr = await httpClient.GetAsync(i.Url, HttpCompletionOption.ResponseHeadersRead)
                .ConfigureAwait(false);
            var imgData = await sr.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            var imgStream = new MemoryStream(imgData);
            attachments.Add(new FileAttachment(imgStream, i.Filename));
            streams.Add(imgStream);
        }

        return (attachments, message, streams);
    }


    /// <summary>
    /// Used to say or embed a message as the bot.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    [Cmd, Aliases, RequireContext(ContextType.Guild), UserPerm(GuildPermission.ManageMessages), Priority(0)]
    public Task Say([Remainder] string? message = null) => Say((ITextChannel)ctx.Channel, message);

    /// <summary>
    /// Shows the bot's stats.
    /// </summary>
    [Cmd, Aliases]
    public async Task Stats()
    {
        await using var uow = db.GetDbContext();
        var time = DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(5));
        var commandStats = uow.CommandStats.Count(x => x.DateAdded.Value >= time);
        var users = new[]
        {
            await client.Rest.GetUserAsync(280835732728184843).ConfigureAwait(false),
            await client.Rest.GetUserAsync(786375627892064257).ConfigureAwait(false),
        };
        await ctx.Channel.EmbedAsync(
                new EmbedBuilder().WithOkColor()
                    .WithAuthor($"{client.CurrentUser.Username} v{StatsService.BotVersion}",
                        client.CurrentUser.GetAvatarUrl(), config.Data.SupportServer)
                    .AddField(GetText("authors"),
                        $"[{users[0]}](https://github.com/SylveonDeko)\n[{users[1]}](https://github.com/CottageDwellingCat)")
                    .AddField(GetText("commands_ran"), $"{commandStats}/5s")
                    .AddField(GetText("command_count"), cmdServ.Commands.DistinctBy(x => x.Name).Count())
                    .AddField("Library", stats.Library)
                    .AddField(GetText("owner_ids"), string.Join("\n", creds.OwnerIds.Select(x => $"<@{x}>")))
                    .AddField(GetText("shard"), $"#{client.ShardId} / {creds.TotalShards}")
                    .AddField(GetText("memory"), $"{stats.Heap} MB")
                    .AddField(GetText("uptime"), stats.GetUptimeString("\n"))
                    .AddField("Servers", $"{coordinator.GetGuildCount()} Servers"))
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Enlarges one or more specified emojis.
    /// </summary>
    /// <param name="_"></param>
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

    /// <summary>
    /// Shows the bot's ping.
    /// </summary>
    [Cmd, Ratelimit(30)]
    public async Task Ping()
    {
        await Sem.WaitAsync(5000).ConfigureAwait(false);
        try
        {
            var redisPing = await cache.Redis.GetDatabase().PingAsync();
            await using var uow = db.GetDbContext();
            var sw = Stopwatch.StartNew();
            var msg = await ctx.Channel.SendMessageAsync("🏓").ConfigureAwait(false);
            sw.Stop();
            msg.DeleteAfter(0);

            await ctx.Channel
                .SendConfirmAsync(
                    $"Bot Ping {(int)sw.Elapsed.TotalMilliseconds}ms" +
                    $"\nBot Latency {((DiscordSocketClient)ctx.Client).Latency}ms" +
                    $"\nRedis Ping {redisPing.Nanoseconds}ns")
                .ConfigureAwait(false);
        }
        finally
        {
            Sem.Release();
        }
    }

    /// <summary>
    /// Rolls a dice with the specified number of sides. Dnd dice are supported.
    /// </summary>
    /// <param name="roll">The roll to make.</param>
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
                    ? GetText("roll_fail_too_large")!
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

        await serv.SendPaginatorAsync(paginator, ctx.Channel, TimeSpan.FromMinutes(60)).ConfigureAwait(false);
    }

    /// <summary>
    /// O-OwoIfy WoIfy's the specified input.
    /// </summary>
    /// <param name="input">The input to owoify woify.</param>
    [Cmd, Aliases]
    public async Task OwoIfy([Remainder] string input)
        => await ctx.Channel.SendMessageAsync(OwoServices.OwoIfy(input).SanitizeMentions(true)).ConfigureAwait(false);
}