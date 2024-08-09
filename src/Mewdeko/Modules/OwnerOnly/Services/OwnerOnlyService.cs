using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using LinqToDB.EntityFrameworkCore;
using Mewdeko.Common.Configs;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Services.Settings;
using Mewdeko.Services.strings;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Octokit;
using Serilog;
using StackExchange.Redis;
using Embed = Discord.Embed;
using Image = Discord.Image;

namespace Mewdeko.Modules.OwnerOnly.Services;

/// <summary>
/// Service for owner-only commands.
/// </summary>
public class OwnerOnlyService : ILateExecutor, IReadyExecutor, INService
{
    private readonly Mewdeko bot;
    private readonly BotConfigService bss;

    private readonly IDataCache cache;
    private int currentStatusNum;
    private readonly DiscordShardedClient client;
    private readonly CommandHandler cmdHandler;
    private readonly IBotCredentials creds;
    private readonly DbContextProvider dbProvider;
    private readonly IHttpClientFactory httpFactory;
    private readonly Replacer rep;
    private readonly IBotStrings strings;
    private readonly GuildSettingsService guildSettings;
    private static readonly Dictionary<ulong, Conversation> UserConversations = new Dictionary<ulong, Conversation>();

#pragma warning disable CS8714
    private ConcurrentDictionary<ulong?, ConcurrentDictionary<int, Timer>> autoCommands =
#pragma warning restore CS8714
        new();

    private ImmutableDictionary<ulong, IDMChannel> ownerChannels =
        new Dictionary<ulong, IDMChannel>().ToImmutableDictionary();

    /// <summary>
    /// Initializes a new instance of the <see cref="OwnerOnlyService"/> class.
    /// This service handles owner-only commands and functionalities for the bot.
    /// </summary>
    /// <param name="client">The Discord client used for interacting with the Discord API.</param>
    /// <param name="cmdHandler">Handles command processing and execution.</param>
    /// <param name="db">Provides access to the database for data persistence.</param>
    /// <param name="strings">Provides access to localized bot strings.</param>
    /// <param name="creds">Contains the bot's credentials and configuration.</param>
    /// <param name="cache">Provides caching functionalities.</param>
    /// <param name="factory">Factory for creating instances of <see cref="HttpClient"/>.</param>
    /// <param name="bss">Service for accessing bot configuration settings.</param>
    /// <param name="phProviders">A collection of providers for placeholder values.</param>
    /// <param name="bot">Reference to the main bot instance.</param>
    /// <param name="guildSettings">Service for accessing guild-specific settings.</param>
    /// <param name="handler">Event handler for subscribing to bot events.</param>
    /// <remarks>
    /// The constructor subscribes to message received events and sets up periodic tasks for rotating statuses
    /// and checking for updates. It also listens for commands to leave guilds or reload images via Redis subscriptions.
    /// </remarks>
    public OwnerOnlyService(DiscordShardedClient client, CommandHandler cmdHandler, DbContextProvider dbProvider,
        IBotStrings strings, IBotCredentials creds, IDataCache cache, IHttpClientFactory factory,
        BotConfigService bss, IEnumerable<IPlaceholderProvider> phProviders, Mewdeko bot,
        GuildSettingsService guildSettings, EventHandler handler)
    {
        var redis = cache.Redis;
        this.cmdHandler = cmdHandler;
        this.dbProvider = dbProvider;
        this.strings = strings;
        this.client = client;
        this.creds = creds;
        this.cache = cache;
        this.bot = bot;
        this.guildSettings = guildSettings;
        var imgs = cache.LocalImages;
        httpFactory = factory;
        this.bss = bss;
        handler.MessageReceived += OnMessageReceived;
        rep = new ReplacementBuilder()
            .WithClient(client)
            .WithProviders(phProviders)
            .Build();

        _ = Task.Run(RotatingStatuses);

        var sub = redis.GetSubscriber();
        sub.Subscribe($"{this.creds.RedisKey()}_reload_images",
            delegate { imgs.Reload(); }, CommandFlags.FireAndForget);

        sub.Subscribe($"{this.creds.RedisKey()}_leave_guild", async (_, v) =>
        {
            try
            {
                var guildStr = v.ToString()?.Trim().ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(guildStr))
                    return;
                var server = this.client.Guilds.FirstOrDefault(g => g.Id.ToString() == guildStr) ??
                             this.client.Guilds.FirstOrDefault(g => g.Name.Trim().ToUpperInvariant() == guildStr);

                if (server == null) return;

                if (server.OwnerId != this.client.CurrentUser.Id)
                {
                    await server.LeaveAsync().ConfigureAwait(false);
                    Log.Information("Left server {ServerName} [{ServerId}]", server.Name, server.Id);
                }
                else
                {
                    await server.DeleteAsync().ConfigureAwait(false);
                    Log.Information("Deleted server {ServerName} [{ServerId}]", server.Name, server.Id);
                }
            }
            catch
            {
                // ignored
            }
        }, CommandFlags.FireAndForget);

        _ = CheckUpdateTimer();
        handler.GuildMemberUpdated += QuarantineCheck;
    }

    private async Task QuarantineCheck(Cacheable<SocketGuildUser, ulong> args, SocketGuildUser arsg2)
    {
        if (!args.HasValue)
            return;

        if (args.Id != client.CurrentUser.Id)
            return;

        var value = args.Value;

        if (value.Roles is null)
            return;


        if (!bss.Data.QuarantineNotification)
            return;

        if (!Equals(value.Roles, arsg2.Roles))
        {
            var quarantineRole = value.Guild.Roles.FirstOrDefault(x => x.Name == "Quarantine");
            if (quarantineRole is null)
                return;

            if (value.Roles.All(x => x.Id != quarantineRole.Id) && arsg2.Roles.Any(x => x.Id == quarantineRole.Id))
            {
                if (bss.Data.ForwardToAllOwners)
                {
                    foreach (var i in creds.OwnerIds)
                    {
                        var user = await client.Rest.GetUserAsync(i);
                        if (user is null) continue;
                        var channel = await user.CreateDMChannelAsync();
                        await channel.SendMessageAsync(
                            $"Quarantined in {value.Guild.Name} [{value.Guild.Id}]");
                    }
                }
                else
                {
                    var user = await client.Rest.GetUserAsync(creds.OwnerIds[0]);
                    if (user is not null)
                    {
                        var channel = await user.CreateDMChannelAsync();
                        await channel.SendMessageAsync(
                            $"Quarantined in {value.Guild.Name} [{value.Guild.Id}]");
                    }
                }
            }
        }
    }

    private async Task CheckUpdateTimer()
    {
        var interval = bss.Data.CheckUpdateInterval;
        if (interval < 1)
            return;
        using var timer = new PeriodicTimer(TimeSpan.FromHours(interval));
        do
        {
            var github = new GitHubClient(new ProductHeaderValue("Mewdeko"));
            var redis = cache.Redis.GetDatabase();
            switch (bss.Data.CheckForUpdates)
            {
                case UpdateCheckType.Release:
                    var latestRelease = await github.Repository.Release.GetLatest("SylveonDeko", "Mewdeko");
                    var eb = new EmbedBuilder()
                        .WithAuthor($"New Release found: {latestRelease.TagName}",
                            "https://seeklogo.com/images/G/github-logo-5F384D0265-seeklogo.com.png",
                            latestRelease.HtmlUrl)
                        .WithDescription(
                            $"- If on Windows, you can download the new release [here]({latestRelease.Assets[0].BrowserDownloadUrl})\n" +
                            $"- If running source just run the `{bss.Data.Prefix}update command and the bot will do the rest for you.`")
                        .WithOkColor();
                    var list = await redis.StringGetAsync($"{creds.RedisKey()}_ReleaseList");
                    if (!list.HasValue)
                    {
                        await redis.StringSetAsync($"{creds.RedisKey()}_ReleaseList",
                            JsonConvert.SerializeObject(latestRelease));
                        Log.Information("Setting latest release to {ReleaseTag}", latestRelease.TagName);
                    }
                    else
                    {
                        var release = JsonConvert.DeserializeObject<Release>(list);
                        if (release.TagName != latestRelease.TagName)
                        {
                            Log.Information("New release found: {ReleaseTag}", latestRelease.TagName);
                            await redis.StringSetAsync($"{creds.RedisKey()}_ReleaseList",
                                JsonConvert.SerializeObject(latestRelease));
                            if (bss.Data.ForwardToAllOwners)
                            {
                                foreach (var i in creds.OwnerIds)
                                {
                                    var user = await client.Rest.GetUserAsync(i);
                                    if (user is null) continue;
                                    var channel = await user.CreateDMChannelAsync();
                                    await channel.SendMessageAsync(embed: eb.Build());
                                }
                            }
                            else
                            {
                                var user = await client.Rest.GetUserAsync(creds.OwnerIds[0]);
                                if (user is not null)
                                {
                                    var channel = await user.CreateDMChannelAsync();
                                    await channel.SendMessageAsync(embed: eb.Build());
                                }
                            }
                        }
                    }

                    break;
                case UpdateCheckType.Commit:
                    var latestCommit =
                        await github.Repository.Commit.Get("SylveonDeko", "Mewdeko", bss.Data.UpdateBranch);
                    if (latestCommit is null)
                    {
                        Log.Warning(
                            "Failed to get latest commit, make sure you have the correct branch set in bot.yml");
                        break;
                    }

                    var redisCommit = await redis.StringGetAsync($"{creds.RedisKey()}_CommitList");
                    if (!redisCommit.HasValue)
                    {
                        await redis.StringSetAsync($"{creds.RedisKey()}_CommitList",
                            latestCommit.Sha);
                        Log.Information("Setting latest commit to {CommitSha}", latestCommit.Sha);
                    }
                    else
                    {
                        if (redisCommit.ToString() != latestCommit.Sha)
                        {
                            Log.Information("New commit found: {CommitSha}", latestCommit.Sha);
                            await redis.StringSetAsync($"{creds.RedisKey()}_CommitList",
                                latestCommit.Sha);
                            if (bss.Data.ForwardToAllOwners)
                            {
                                foreach (var i in creds.OwnerIds)
                                {
                                    var user = await client.Rest.GetUserAsync(i);
                                    if (user is null) continue;
                                    var channel = await user.CreateDMChannelAsync();
                                    await channel.SendMessageAsync(
                                        $"New commit found: {latestCommit.Sha}\n{latestCommit.HtmlUrl}");
                                }
                            }
                            else
                            {
                                var user = await client.Rest.GetUserAsync(creds.OwnerIds[0]);
                                if (user is not null)
                                {
                                    var channel = await user.CreateDMChannelAsync();
                                    await channel.SendMessageAsync(
                                        $"New commit found: {latestCommit.Sha}\n{latestCommit.HtmlUrl}");
                                }
                            }
                        }
                    }

                    break;
                case UpdateCheckType.None:
                    break;
                default:
                    Log.Error("Invalid UpdateCheckType {UpdateCheckType}", bss.Data.CheckForUpdates);
                    break;
            }
        } while (await timer.WaitForNextTickAsync());
    }

    private async Task OnMessageReceived(SocketMessage args)
    {
        if (args.Channel is not IGuildChannel guildChannel)
            return;
        var prefix = await guildSettings.GetPrefix(guildChannel.GuildId);
        if (args.Content.StartsWith(prefix))
            return;
        if (bss.Data.ChatGptKey is null or "" || bss.Data.ChatGptChannel is 0)
            return;
        if (args.Author.IsBot)
            return;
        if (args.Channel.Id != bss.Data.ChatGptChannel)
            return;
        if (args is not IUserMessage usrMsg)
            return;
        // try
        // {
        if (args.Content is "deletesession")
        {
            if (UserConversations.TryGetValue(args.Author.Id, out _))
            {
                ClearConversation(args.Author.Id);
                await args.Channel.SendConfirmAsync("Conversation deleted.");
                return;
            }

            await args.Channel.SendErrorAsync("You dont have a conversation saved.", bss.Data);
            return;
        }

        await using var dbContext = await dbProvider.GetContextAsync();

        (Database.Models.OwnerOnly actualItem, bool added) toUpdate = dbContext.OwnerOnly.Any()
            ? (await dbContext.OwnerOnly.FirstOrDefaultAsync(), false)
            : (new Database.Models.OwnerOnly
            {
                GptTokensUsed = 0
            }, true);

        var loadingMsg = await usrMsg.Channel.SendConfirmAsync($"{bss.Data.LoadingEmote} Awaiting response...");
        await StreamResponseAndUpdateEmbedAsync(bss.Data.ChatGptKey, bss.Data.ChatGptModel,
            bss.Data.ChatGptInitPrompt +
            $"The users name is {args.Author}, you are in the discord server {guildChannel.Guild} and in the channel {guildChannel} and there are {(await guildChannel.GetUsersAsync().FlattenAsync()).Count()} users that can see this channel.",
            loadingMsg, toUpdate, args.Author, args.Content);
        //}
        // catch (Exception e)
        // {
        //     Log.Warning(e, "Error in ChatGPT");
        //     await usrMsg.SendErrorReplyAsync("Something went wrong, please try again later.");
        // }
    }

    private static void ClearConversation(ulong userId)
    {
        UserConversations.Remove(userId);
    }

    private async Task StreamResponseAndUpdateEmbedAsync(string apiKey, string model, string systemPrompt,
        IUserMessage loadingMsg,
        (Database.Models.OwnerOnly actualItem, bool added) toUpdate, SocketUser author, string userPrompt)
    {
        using var httpClient = httpFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

        // Get or create conversation for this user
        if (!UserConversations.TryGetValue(author.Id, out var conversation))
        {
            conversation = new Conversation();
            conversation.Messages.Add(new Message
            {
                Role = "system", Content = systemPrompt
            });
            UserConversations[author.Id] = conversation;
        }

        // Add user message to conversation
        conversation.Messages.Add(new Message
        {
            Role = "user", Content = userPrompt
        });

        var requestBody = new
        {
            model,
            messages = conversation.Messages.Select(m => new
            {
                role = m.Role, content = m.Content
            }).ToArray(),
            stream = true,
            user = author.Id.ToString()
        };

        var content = new StringContent(System.Text.Json.JsonSerializer.Serialize(requestBody), Encoding.UTF8,
            "application/json");

        using var response = await httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        var responseBuilder = new StringBuilder();
        var lastUpdate = DateTimeOffset.UtcNow;
        var totalTokens = 0;

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (string.IsNullOrEmpty(line) || line == "data: [DONE]") continue;

            if (!line.StartsWith("data: ")) continue;
            var json = line[6..];
            var chatResponse = JsonConvert.DeserializeObject<ChatCompletionChunkResponse>(json);

            if (chatResponse?.Choices is not { Count: > 0 }) continue;
            var conversationContent = chatResponse.Choices[0].Delta?.Content;
            if (string.IsNullOrEmpty(conversationContent)) continue;
            responseBuilder.Append(conversationContent);
            if (!((DateTimeOffset.UtcNow - lastUpdate).TotalSeconds >= 1)) continue;
            lastUpdate = DateTimeOffset.UtcNow;
            var embeds = BuildEmbeds(responseBuilder.ToString(), author,
                toUpdate.actualItem.GptTokensUsed + totalTokens);
            await loadingMsg.ModifyAsync(m => m.Embeds = embeds.ToArray());
        }

        // Add assistant's response to the conversation
        conversation.Messages.Add(new Message
        {
            Role = "assistant", Content = responseBuilder.ToString()
        });

        // Trim conversation history if it gets too long
        if (conversation.Messages.Count > 10)
        {
            conversation.Messages = conversation.Messages.Skip(conversation.Messages.Count - 10).ToList();
        }

        await using var dbContext = await dbProvider.GetContextAsync();
        toUpdate.actualItem.GptTokensUsed += totalTokens;

        if (toUpdate.added)
            dbContext.OwnerOnly.Add(toUpdate.actualItem);
        else
            dbContext.OwnerOnly.Update(toUpdate.actualItem);
        await dbContext.SaveChangesAsync();

        var finalEmbeds = BuildEmbeds(responseBuilder.ToString(), author, toUpdate.actualItem.GptTokensUsed);
        await loadingMsg.ModifyAsync(m => m.Embeds = finalEmbeds.ToArray());
    }

    private static List<Embed> BuildEmbeds(string response, IUser requester, int totalTokensUsed)
    {
        var embeds = new List<Embed>();
        var partIndex = 0;
        while (partIndex < response.Length)
        {
            var length = Math.Min(4096, response.Length - partIndex);
            var description = response.Substring(partIndex, length);
            var embedBuilder = new EmbedBuilder()
                .WithDescription(description)
                .WithOkColor();

            if (partIndex == 0)
                embedBuilder.WithAuthor("ChatGPT",
                    "https://seeklogo.com/images/C/chatgpt-logo-02AFA704B5-seeklogo.com.png");

            if (partIndex + length == response.Length)
                embedBuilder.WithFooter(
                    $"Requested by {requester.Username} | Total Tokens Used: {totalTokensUsed}");

            embeds.Add(embedBuilder.Build());
            partIndex += length;
        }

        return embeds;
    }


    /// <summary>
    /// Resets the count of used GPT tokens to zero in the database. This is typically called to clear the token usage count at the start of a new billing period or when manually resetting the token count.
    /// </summary>
    public async Task ClearUsedTokens()
    {
        await using var dbContext = await dbProvider.GetContextAsync();
        var val = await dbContext.OwnerOnly.FirstOrDefaultAsync();
        if (val is null)
            return;
        val.GptTokensUsed = 0;
        dbContext.OwnerOnly.Update(val);
        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Forwards direct messages (DMs) received by the bot to the owners' DMs. This allows bot owners to monitor and respond to user messages directly.
    /// </summary>
    /// <param name="DiscordShardedClient">The Discord client through which the message was received.</param>
    /// <param name="guild">The guild associated with the message, if any.</param>
    /// <param name="msg">The message that was received and is to be forwarded.</param>
    /// <remarks>
    /// The method checks if the message was sent in a DM channel and forwards it to all owners if the setting is enabled.
    /// Attachments are also forwarded. Errors in sending messages to any owner are logged but not thrown.
    /// </remarks>
    public async Task LateExecute(DiscordShardedClient DiscordShardedClient, IGuild guild, IUserMessage msg)
    {
        var bs = bss.Data;
        if (msg.Channel is IDMChannel && bss.Data.ForwardMessages && ownerChannels.Count > 0)
        {
            var title = $"{strings.GetText("dm_from")} [{msg.Author}]({msg.Author.Id})";

            var attachamentsTxt = strings.GetText("attachments");

            var toSend = msg.Content;

            if (msg.Attachments.Count > 0)
            {
                toSend +=
                    $"\n\n{Format.Code(attachamentsTxt)}:\n{string.Join("\n", msg.Attachments.Select(a => a.ProxyUrl))}";
            }

            if (bs.ForwardToAllOwners)
            {
                var allOwnerChannels = ownerChannels.Values;

                foreach (var ownerCh in allOwnerChannels.Where(ch => ch.Recipient.Id != msg.Author.Id))
                {
                    try
                    {
                        await ownerCh.SendConfirmAsync(title, toSend).ConfigureAwait(false);
                    }
                    catch
                    {
                        Log.Warning("Can't contact owner with id {0}", ownerCh.Recipient.Id);
                    }
                }
            }
            else
            {
                var firstOwnerChannel = ownerChannels.Values.First();
                if (firstOwnerChannel.Recipient.Id != msg.Author.Id)
                {
                    try
                    {
                        await firstOwnerChannel.SendConfirmAsync(title, toSend).ConfigureAwait(false);
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }
        }
    }

    /// <summary>
    /// Initializes required services and loads configurations when the bot is ready. This includes setting up automatic commands based on their configured intervals and creating direct message channels for the bot owners.
    /// </summary>
    /// <remarks>
    /// This method is typically called once when the bot starts and is ready to receive and process messages. It prepares the bot for operation by loading necessary configurations and establishing connections.
    /// </remarks>
    public async Task OnReadyAsync()
    {
        Log.Information($"Starting {this.GetType()} Cache");

        await using var dbContext = await dbProvider.GetContextAsync();


        autoCommands =
            (await dbContext.AutoCommands
                .AsNoTracking()
                .ToListAsyncEF())
            .Where(x => x.Interval >= 5)
            .AsEnumerable()
            .GroupBy(x => x.GuildId)
            .ToDictionary(x => x.Key,
                y => y.ToDictionary(x => x.Id, TimerFromAutoCommand)
                    .ToConcurrent())
            .ToConcurrent();

        foreach (var cmd in dbContext.AutoCommands.AsNoTracking().Where(x => x.Interval == 0))
        {
            try
            {
                await ExecuteCommand(cmd).ConfigureAwait(false);
            }
            catch
            {
                // ignored
            }
        }

        var channels = await Task.WhenAll(creds.OwnerIds.Select(id =>
        {
            var user = client.GetUser(id);
            return user == null ? Task.FromResult<IDMChannel?>(null) : user.CreateDMChannelAsync();
        })).ConfigureAwait(false);

        ownerChannels = channels.Where(x => x is not null)
            .ToDictionary(x => x.Recipient.Id, x => x)
            .ToImmutableDictionary();

        if (ownerChannels.Count == 0)
        {
            Log.Warning(
                "No owner channels created! Make sure you've specified the correct OwnerId in the credentials.json file and invited the bot to a Discord server");
        }
        else
        {
            Log.Information(
                $"Created {ownerChannels.Count} out of {creds.OwnerIds.Length} owner message channels.");
        }
    }

    private async Task RotatingStatuses()
    {
        var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync())
        {
            try
            {
                await using var dbContext = await dbProvider.GetContextAsync();

                if (!bss.Data.RotateStatuses) continue;

                IReadOnlyList<RotatingPlayingStatus> rotatingStatuses =
                    await dbContext.RotatingStatus.AsNoTracking().OrderBy(x => x.Id).ToListAsyncEF();

                if (rotatingStatuses.Count == 0)
                    continue;

                var playingStatus = currentStatusNum >= rotatingStatuses.Count
                    ? rotatingStatuses[currentStatusNum = 0]
                    : rotatingStatuses[currentStatusNum++];

                var statusText = rep.Replace(playingStatus.Status);
                await bot.SetGameAsync(statusText, playingStatus.Type).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Rotating playing status errored: {ErrorMessage}", ex.Message);
            }
        }
    }

    /// <summary>
    /// Removes a playing status from the rotating statuses list based on its index.
    /// </summary>
    /// <param name="index">The zero-based index of the status to remove.</param>
    /// <returns>The status that was removed, or null if the index was out of bounds.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the <paramref name="index"/> is less than 0.</exception>
    public async Task<string?> RemovePlayingAsync(int index)
    {
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(index));

        await using var dbContext = await dbProvider.GetContextAsync();


        var toRemove = await dbContext.RotatingStatus
            .AsQueryable()
            .AsNoTracking()
            .Skip(index)
            .FirstOrDefaultAsync().ConfigureAwait(false);

        if (toRemove is null)
            return null;

        dbContext.Remove(toRemove);
        await dbContext.SaveChangesAsync().ConfigureAwait(false);
        return toRemove.Status;
    }

    /// <summary>
    /// Adds a new playing status to the list of rotating statuses.
    /// </summary>
    /// <param name="t">The type of activity for the status (e.g., playing, streaming).</param>
    /// <param name="status">The text of the status to display.</param>
    /// <returns>A task that represents the asynchronous add operation.</returns>
    public async Task AddPlaying(ActivityType t, string status)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        var toAdd = new RotatingPlayingStatus
        {
            Status = status, Type = t
        };
        dbContext.Add(toAdd);
        await dbContext.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Toggles the rotation of playing statuses on or off.
    /// </summary>
    /// <returns>True if rotation is enabled after the toggle, false otherwise.</returns>
    public bool ToggleRotatePlaying()
    {
        var enabled = false;
        bss.ModifyConfig(bs => enabled = bs.RotateStatuses = !bs.RotateStatuses);
        return enabled;
    }

    /// <summary>
    /// Retrieves the current list of rotating playing statuses.
    /// </summary>
    /// <returns>A read-only list of <see cref="RotatingPlayingStatus"/> representing the current rotating statuses.</returns>
    public async Task<IReadOnlyList<RotatingPlayingStatus>> GetRotatingStatuses()
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        return await dbContext.RotatingStatus.AsNoTracking().ToListAsync();
    }

    private Timer TimerFromAutoCommand(AutoCommand x) =>
        new(async obj => await ExecuteCommand((AutoCommand)obj).ConfigureAwait(false),
            x,
            x.Interval * 1000,
            x.Interval * 1000);

    private async Task ExecuteCommand(AutoCommand cmd)
    {
        try
        {
            if (cmd.GuildId is null)
                return;
            var guildShard = (int)((cmd.GuildId.Value >> 22) % (ulong)creds.TotalShards);
            var prefix = await guildSettings.GetPrefix(cmd.GuildId.Value);
            //if someone already has .die as their startup command, ignore it
            if (cmd.CommandText.StartsWith($"{prefix}die", StringComparison.InvariantCulture))
                return;
            await cmdHandler.ExecuteExternal(cmd.GuildId, cmd.ChannelId, cmd.CommandText).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error in SelfService ExecuteCommand");
        }
    }

    /// <summary>
    /// Adds a new auto command to the database and schedules it if necessary.
    /// </summary>
    /// <param name="cmd">The auto command to be added.</param>
    /// <remarks>
    /// If the command's interval is 5 seconds or more, it's also scheduled to be executed periodically according to its interval.
    /// </remarks>
    public async Task AddNewAutoCommand(AutoCommand cmd)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        dbContext.AutoCommands.Add(cmd);
        await dbContext.SaveChangesAsync();

        if (cmd.Interval >= 5)
        {
            var autos = autoCommands.GetOrAdd(cmd.GuildId, new ConcurrentDictionary<int, Timer>());
            autos.AddOrUpdate(cmd.Id, _ => TimerFromAutoCommand(cmd), (_, old) =>
            {
                old.Change(Timeout.Infinite, Timeout.Infinite);
                return TimerFromAutoCommand(cmd);
            });
        }
    }

    /// <summary>
    /// Sets the default prefix for bot commands.
    /// </summary>
    /// <param name="prefix">The new prefix to be set.</param>
    /// <returns>The newly set prefix.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="prefix"/> is null or whitespace.</exception>
    public string SetDefaultPrefix(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            throw new ArgumentNullException(nameof(prefix));

        bss.ModifyConfig(bs => bs.Prefix = prefix);

        return prefix;
    }

    /// <summary>
    /// Retrieves a list of auto commands set to execute at bot startup (interval of 0).
    /// </summary>
    /// <returns>A list of startup auto commands.</returns>
    public async Task<IEnumerable<AutoCommand>> GetStartupCommands()
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        return
            dbContext.AutoCommands
                .AsNoTracking()
                .Where(x => x.Interval == 0)
                .OrderBy(x => x.Id)
                .ToList();
    }

    /// <summary>
    /// Retrieves a list of auto commands with an interval of 5 seconds or more.
    /// </summary>
    /// <returns>A list of auto commands set to execute periodically.</returns>
    public async Task<IEnumerable<AutoCommand>> GetAutoCommands()
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        return
            dbContext.AutoCommands
                .AsNoTracking()
                .Where(x => x.Interval >= 5)
                .OrderBy(x => x.Id)
                .ToList();
    }

    /// <summary>
    /// Instructs the bot to leave a guild based on the guild's identifier or name.
    /// </summary>
    /// <param name="guildStr">The guild identifier or name.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task LeaveGuild(string guildStr)
    {
        var sub = cache.Redis.GetSubscriber();
        return sub.PublishAsync($"{creds.RedisKey()}_leave_guild", guildStr);
    }

    /// <summary>
    /// Attempts to restart the bot using the configured restart command.
    /// </summary>
    /// <returns>True if the command to restart the bot is not null or whitespace and the bot is restarted; otherwise, false.</returns>
    public bool RestartBot()
    {
        var cmd = creds.RestartCommand;
        if (string.IsNullOrWhiteSpace(cmd.Cmd)) return false;

        Restart();
        return true;
    }

    /// <summary>
    /// Removes a startup command (a command with an interval of 0) at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the startup command to remove.</param>
    /// <param name="cmd">Out parameter that returns the removed auto command if the operation succeeds.</param>
    /// <returns>True if a command was found and removed; otherwise, false.</returns>
    public async Task<bool> RemoveStartupCommand(int index)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        var cmd = await dbContext.AutoCommands
            .AsNoTracking()
            .Where(x => x.Interval == 0)
            .Skip(index)
            .FirstOrDefaultAsync();

        if (cmd == null) return false;
        dbContext.Remove(cmd);
        await dbContext.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Removes an auto command based on its index in the collection of commands with an interval of 5 seconds or more.
    /// </summary>
    /// <param name="index">The zero-based index of the command to remove.</param>
    /// <param name="cmd">Outputs the removed <see cref="AutoCommand"/> if the method returns true.</param>
    /// <returns>True if a command was successfully found and removed; otherwise, false.</returns>
    public async Task<bool> RemoveAutoCommand(int index)
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        var cmd = await dbContext.AutoCommands
            .AsNoTracking()
            .Where(x => x.Interval >= 5)
            .Skip(index)
            .FirstOrDefaultAsync();

        if (cmd == null) return false;
        dbContext.Remove(cmd);
        if (autoCommands.TryGetValue(cmd.GuildId, out var autos))
        {
            if (autos.TryRemove(cmd.Id, out var timer))
                timer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        dbContext.SaveChanges();
        return true;
    }

    /// <summary>
    /// Sets a new avatar for the bot by downloading an image from a specified URL.
    /// </summary>
    /// <param name="img">The URL of the image to set as the new avatar.</param>
    /// <returns>True if the avatar was successfully updated; otherwise, false.</returns>
    public async Task<bool> SetAvatar(string img)
    {
        if (string.IsNullOrWhiteSpace(img))
            return false;

        if (!Uri.IsWellFormedUriString(img, UriKind.Absolute))
            return false;

        var uri = new Uri(img);

        using var http = httpFactory.CreateClient();
        using var sr = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        if (!sr.IsImage())
            return false;

        var imgData = await sr.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
        var imgStream = imgData.ToStream();
        await using var _ = imgStream.ConfigureAwait(false);
        await client.CurrentUser.ModifyAsync(u => u.Avatar = new Image(imgStream)).ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// Clears all startup commands from the database.
    /// </summary>
    public async Task ClearStartupCommands()
    {
        await using var dbContext = await dbProvider.GetContextAsync();

        var toRemove = dbContext.AutoCommands
            .AsNoTracking()
            .Where(x => x.Interval == 0);

        dbContext.AutoCommands.RemoveRange(toRemove);
        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Reloads images from a source, typically used for refreshing local or cached resources.
    /// </summary>
    public void ReloadImages()
    {
        var sub = cache.Redis.GetSubscriber();
        sub.Publish($"{creds.RedisKey()}_reload_images", "");
    }

    /// <summary>
    /// Instructs the bot to shut down.
    /// </summary>
    public void Die()
    {
        var sub = cache.Redis.GetSubscriber();
        sub.Publish($"{creds.RedisKey()}_die", "", CommandFlags.FireAndForget);
    }

    /// <summary>
    /// Restarts the bot by invoking a system command.
    /// </summary>
    public void Restart()
    {
        Process.Start(creds.RestartCommand.Cmd, creds.RestartCommand.Args);
        var sub = cache.Redis.GetSubscriber();
        sub.Publish($"{creds.RedisKey()}_die", "", CommandFlags.FireAndForget);
    }

    /// <summary>
    /// Restarts a specific bot shard.
    /// </summary>
    /// <param name="shardId">The ID of the shard to restart.</param>
    /// <returns>True if the shard ID is valid and the shard is restarted; otherwise, false.</returns>
    public bool RestartShard(int shardId)
    {
        if (shardId < 0 || shardId >= creds.TotalShards)
            return false;

        var pub = cache.Redis.GetSubscriber();
        pub.Publish($"{creds.RedisKey()}_shardcoord_stop",
            JsonConvert.SerializeObject(shardId),
            CommandFlags.FireAndForget);

        return true;
    }

    /// <summary>
    /// Toggles the bot's message forwarding feature.
    /// </summary>
    /// <returns>True if message forwarding is enabled after the toggle; otherwise, false.</returns>
    public bool ForwardMessages()
    {
        var isForwarding = false;
        bss.ModifyConfig(config => isForwarding = config.ForwardMessages = !config.ForwardMessages);

        return isForwarding;
    }

    /// <summary>
    /// Toggles whether the bot forwards messages to all owners or just the primary owner.
    /// </summary>
    /// <returns>True if forwarding to all owners is enabled after the toggle; otherwise, false.</returns>
    public bool ForwardToAll()
    {
        var isToAll = false;
        bss.ModifyConfig(config => isToAll = config.ForwardToAllOwners = !config.ForwardToAllOwners);
        return isToAll;
    }

    private class Choice
    {
        [JsonProperty("index", NullValueHandling = NullValueHandling.Ignore)]
        public int? Index;

        [JsonProperty("delta", NullValueHandling = NullValueHandling.Ignore)]
        public Delta Delta;

        [JsonProperty("logprobs", NullValueHandling = NullValueHandling.Ignore)]
        public object Logprobs;

        [JsonProperty("finish_reason", NullValueHandling = NullValueHandling.Ignore)]
        public object FinishReason;
    }

    private class Delta
    {
        [JsonProperty("content", NullValueHandling = NullValueHandling.Ignore)]
        public string Content;
    }

    private class ChatCompletionChunkResponse
    {
        [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
        public string Id;

        [JsonProperty("object", NullValueHandling = NullValueHandling.Ignore)]
        public string Object;

        [JsonProperty("created", NullValueHandling = NullValueHandling.Ignore)]
        public int? Created;

        [JsonProperty("model", NullValueHandling = NullValueHandling.Ignore)]
        public string Model;

        [JsonProperty("system_fingerprint", NullValueHandling = NullValueHandling.Ignore)]
        public string SystemFingerprint;

        [JsonProperty("choices", NullValueHandling = NullValueHandling.Ignore)]
        public List<Choice> Choices;
    }

    private class Conversation
    {
        public List<Message> Messages { get; set; } = new List<Message>();
    }

    private class Message
    {
        public string Role { get; set; }
        public string Content { get; set; }
    }
}