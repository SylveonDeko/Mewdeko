using System.Collections.Immutable;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Threading;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Services.Settings;
using Mewdeko.Services.strings;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using OpenAI_API;
using OpenAI_API.Chat;
using OpenAI_API.Images;
using OpenAI_API.Models;
using Serilog;
using StackExchange.Redis;
using TwitchLib.Api.Helix;
using Embed = Discord.Embed;
using Image = Discord.Image;

namespace Mewdeko.Modules.OwnerOnly.Services;

public class OwnerOnlyService : ILateExecutor, IReadyExecutor, INService
{
    private readonly Mewdeko bot;
    private readonly BotConfigService bss;

    private readonly IDataCache cache;
    private int currentStatusNum;
    private readonly DiscordSocketClient client;
    private readonly CommandHandler cmdHandler;
    private readonly IBotCredentials creds;
    private readonly DbService db;
    private readonly IHttpClientFactory httpFactory;
    private readonly Replacer rep;
    private readonly IBotStrings strings;
    private readonly GuildSettingsService guildSettings;
    private readonly ConcurrentDictionary<ulong, Conversation> conversations = new();

#pragma warning disable CS8714
    private ConcurrentDictionary<ulong?, ConcurrentDictionary<int, Timer>> autoCommands =
#pragma warning restore CS8714
        new();

    private ImmutableDictionary<ulong, IDMChannel> ownerChannels =
        new Dictionary<ulong, IDMChannel>().ToImmutableDictionary();

    public OwnerOnlyService(DiscordSocketClient client, CommandHandler cmdHandler, DbService db,
        IBotStrings strings, IBotCredentials creds, IDataCache cache, IHttpClientFactory factory,
        BotConfigService bss, IEnumerable<IPlaceholderProvider> phProviders, Mewdeko bot,
        GuildSettingsService guildSettings, EventHandler handler)
    {
        var redis = cache.Redis;
        this.cmdHandler = cmdHandler;
        this.db = db;
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
        if (client.ShardId == 0)
        {
            rep = new ReplacementBuilder()
                .WithClient(client)
                .WithProviders(phProviders)
                .Build();

            _ = Task.Run(async () => await RotatingStatuses());
        }

        var sub = redis.GetSubscriber();
        if (this.client.ShardId == 0)
        {
            sub.Subscribe($"{this.creds.RedisKey()}_reload_images",
                delegate { imgs.Reload(); }, CommandFlags.FireAndForget);
        }

        sub.Subscribe($"{this.creds.RedisKey()}_leave_guild", async (_, v) =>
        {
            try
            {
                var guildStr = v.ToString()?.Trim().ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(guildStr))
                    return;
                var server = this.client.Guilds.FirstOrDefault(g => g.Id.ToString() == guildStr) ??
                             this.client.Guilds.FirstOrDefault(g => g.Name.Trim().ToUpperInvariant() == guildStr);

                if (server == null)
                    return;

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
    }

    private async Task OnMessageReceived(SocketMessage args)
    {
        var isDebugMode = false;
        if (args.Channel is not IGuildChannel guildChannel)
            return;
        var prefix = await guildSettings.GetPrefix(guildChannel.GuildId);
        if (args.Content.StartsWith(prefix))
            return;
        if (bss.Data.ChatGptKey is null or "" || bss.Data.ChatGptChannel is 0)
            return;
        if (args.Author.IsBot)
            return;
        if (args.Channel.Id != bss.Data.ChatGptChannel && args.Channel.Id != bss.Data.ChatGptChannel2)
            return;
        if (args is not IUserMessage usrMsg)
            return;

        //bad hackfix to separate handling of nightly vs stable
#if DEBUG
        isDebugMode = true;
#endif

        try
        {
            var api = new OpenAIAPI(bss.Data.ChatGptKey);

            if (args.Content is ".deletesession" && !isDebugMode)
            {
                if (conversations.TryRemove(args.Author.Id, out _))
                {
                    await usrMsg.SendConfirmReplyAsync("Session deleted");
                    return;
                }
                else
                {
                    await usrMsg.SendConfirmReplyAsync("No session to delete");
                    return;
                }
            }
            else if (args.Content is ",deletesesssion" && isDebugMode)
            {
                if (conversations.TryRemove(args.Author.Id, out _))
                {
                    await usrMsg.SendConfirmReplyAsync("Session deleted");
                    return;
                }
                else
                {
                    await usrMsg.SendConfirmReplyAsync("No session to delete");
                    return;
                }
            }

            await using var uow = db.GetDbContext();
            (Database.Models.OwnerOnly actualItem, bool added) toUpdate = uow.OwnerOnly.Any()
                    ? (await uow.OwnerOnly.FirstOrDefaultAsync(), false)
                    : (new Database.Models.OwnerOnly
                    {
                        GptTokensUsed = 0
                    }, true);


            if (!args.Content.StartsWith("!frog") && !isDebugMode)
                return;

            if (!args.Content.StartsWith("#frog") && isDebugMode)
                return;


            Log.Information("ChatGPT request from {Author}: | ({AuthorId}): | {Content}", args.Author, args.Author.Id, args.Content);

            // lower any capitalization in message content
            var loweredContents = args.Content.ToLower();

            // Remove the prefix from the message content being sent to gpt
            var gptprompt = loweredContents.Substring("frog ".Length).Trim();

            // Split the message content into words and take only the first two for checking.
            var words = args.Content.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Take(2).ToList();
            var scannedWords = words.Select(w => w.ToLower()).ToList();

            if (scannedWords.Contains("image"))
            {
                try
                {
                    await usrMsg.Channel.SendMessageAsync("Dall-E disabled.");
                    return;
                }
                catch
                {
                    throw;
                }

                var authorName = args.Author.ToString();
                var prompt = args.Content.Substring("frog image ".Length).Trim();
                if (string.IsNullOrEmpty(prompt))
                {
                    await usrMsg.Channel.SendMessageAsync("Please provide a prompt for the image.");
                    return;
                }

                IUserMessage placeholderMessage = null;
                try
                {
                    // Send a placeholder message directly using the bot's client
                    placeholderMessage = await usrMsg.SendConfirmReplyAsync($"{bss.Data.LoadingEmote} Generating image...");

                    // Generate the image
                    var images = await api.ImageGenerations.CreateImageAsync(new ImageGenerationRequest
                    {
                        Prompt = prompt,  // prompt (text string)
                        NumOfImages = 1, // dall-e2 can provide multiple images, e3 does not support this currently
                        Size = ImageSize._1792x1024, // resolution of the generated images (256x256 | 512x512 | 1024x1024 | 1792x1024) dall-e3 cannot use images below 1024x1024
                        Model = Model.DALLE3, // model (model for this req. defaults to dall-e2
                        User = authorName, // user: author of post, this can be used to help openai detect abuse and rule breaking
                        ResponseFormat = ImageResponseFormat.Url // the format the images can be returned as. must be url or b64_json
                        // quality: by default images are generated at standard, but on e3 you can use HD
                    });

                    /*
                    // if dall-e3 ever supports more then 1 image can use this code block instead
                    // Update the placeholder message with the images
                    if (images.Data.Count > 0)
                    {
                        var embeds = images.Data.Select(image => new EmbedBuilder().WithImageUrl(image.Url).Build()).ToArray(); // Convert to array

                        await placeholderMessage.ModifyAsync(msg =>
                        {
                            msg.Content = ""; // Clearing the content
                            msg.Embeds = new Optional<Embed[]>(embeds); // Wrap the array in an Optional
                        });
                    }
                    else
                    {
                        await placeholderMessage.ModifyAsync(msg => msg.Content = "No images were generated.");
                    }
                    */

                    // Update the placeholder message with the image
                    if (images.Data.Count > 0)
                    {
                        var imageUrl = images.Data[0].Url; // Assuming images.Data[0] contains the URL
                        var embed = new EmbedBuilder()
                            .WithImageUrl(imageUrl)
                            .Build();
                        await placeholderMessage.ModifyAsync(msg =>
                        {
                            msg.Content = ""; // Clearing the content
                            msg.Embed = embed;
                        });
                    }
                    else
                    {
                        await placeholderMessage.ModifyAsync(msg => msg.Content = "No image generated.");
                    }
                }
                catch (HttpRequestException httpEx)
                {
                    var content = httpEx.Message; // This is not the response content, but the exception message.
                    Log.Information("Exception message: {Message}", content);

                    // Log the full exception details for debugging
                    Log.Error(httpEx, "HttpRequestException occurred while processing the request.");

                    // Clean up the placeholder message if it was assigned
                    if (placeholderMessage != null)
                    {
                        await placeholderMessage.DeleteAsync();
                    }

                    // Notify the user of a generic error message
                    await usrMsg.SendErrorReplyAsync("An error occurred while processing your request. Please try again later.");
                }
                catch (Exception ex)
                {
                    // Log the error
                    Log.Error(ex, "Error generating image");

                    // Clean up the placeholder message if it was assigned
                    if (placeholderMessage != null)
                    {
                        await placeholderMessage.DeleteAsync();
                    }
                    await usrMsg.SendErrorReplyAsync($"Failed to generate image due to an unexpected error. Please try again later. Error code: **{ex.HResult}**");
                }
                return;
            }

            if (!conversations.TryGetValue(args.Author.Id, out var conversation))
            {
                conversation = StartNewConversation(args.Author, api);
                conversations.TryAdd(args.Author.Id, conversation);
            }

            conversation.AppendUserInput(gptprompt);

            var loadingMsg = await usrMsg.SendConfirmReplyAsync($"{bss.Data.LoadingEmote} Awaiting response...");
            await StreamResponseAndUpdateEmbedAsync(conversation, loadingMsg, uow, toUpdate, args.Author);
        }
        catch (Exception e)
        {
            Log.Warning(e, "Error in ChatGPT");
            await usrMsg.SendErrorReplyAsync("Something went wrong, please try again later.");
        }
    }

    public class OpenAiErrorResponse
    {
        [JsonProperty("error")]
        public OpenAiError Error { get; set; }
    }

    public class OpenAiError
    {
        [JsonProperty("code")]
        public string Code { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }
    }

    private Conversation StartNewConversation(SocketUser user, IOpenAIAPI api, SocketMessage args = null)
    {
        var modelToUse = bss.Data.ChatGptModel switch
        {
            "gpt4-turbo" => Model.GPT4_Turbo,
            "gpt-4-0613" => Model.GPT4_32k_Context,
            "gpt4" or "gpt-4" => Model.GPT4,
            "gpt3" => Model.ChatGPTTurbo,
            _ => Model.ChatGPTTurbo
        };

        var chat = api.Chat.CreateConversation(new ChatRequest
        {
            MaxTokens = bss.Data.ChatGptMaxTokens,
            Temperature = bss.Data.ChatGptTemperature,
            Model = modelToUse
        });
        chat.AppendSystemMessage(bss.Data.ChatGptInitPrompt);
        chat.AppendSystemMessage($"The user's name is {user}.");
        return chat;
    }

    private static async Task StreamResponseAndUpdateEmbedAsync(Conversation conversation, IUserMessage loadingMsg,
        MewdekoContext uow, (Database.Models.OwnerOnly actualItem, bool added) toUpdate, SocketUser author)
    {
        var responseBuilder = new StringBuilder();
        var lastUpdate = DateTimeOffset.UtcNow;

        await conversation.StreamResponseFromChatbotAsync(async partialResponse =>
        {
            responseBuilder.Append(partialResponse);
            if (!((DateTimeOffset.UtcNow - lastUpdate).TotalSeconds >= 1))
                return;
            lastUpdate = DateTimeOffset.UtcNow;
            var embeds = BuildEmbeds(responseBuilder.ToString(), author, toUpdate.actualItem.GptTokensUsed,
                conversation);
            await loadingMsg.ModifyAsync(m => m.Embeds = embeds.ToArray());
        });

        var finalResponse = responseBuilder.ToString();
        if (conversation.MostRecentApiResult.Usage != null)
        {
            toUpdate.actualItem.GptTokensUsed += conversation.MostRecentApiResult.Usage.TotalTokens;
        }

        if (toUpdate.added)
            uow.OwnerOnly.Add(toUpdate.actualItem);
        else
            uow.OwnerOnly.Update(toUpdate.actualItem);
        await uow.SaveChangesAsync();

        var finalEmbeds = BuildEmbeds(finalResponse, author, toUpdate.actualItem.GptTokensUsed, conversation);
        await loadingMsg.ModifyAsync(m => m.Embeds = finalEmbeds.ToArray());
    }

    private static List<Embed> BuildEmbeds(string response, IUser requester, int totalTokensUsed,
        Conversation conversation)
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
                    $"Requested by {requester.Username}");
                    //$"Requested by {requester.Username} | Response Tokens: {conversation.MostRecentApiResult.Usage?.TotalTokens} | Total Used: {totalTokensUsed}");

            embeds.Add(embedBuilder.Build());
            partIndex += length;
        }

        return embeds;
    }

    public async Task ClearUsedTokens()
    {
        await using var uow = db.GetDbContext();
        var val = await uow.OwnerOnly.FirstOrDefaultAsync();
        if (val is null)
            return;
        val.GptTokensUsed = 0;
        uow.OwnerOnly.Update(val);
        await uow.SaveChangesAsync();
    }

    // forwards dms
    public async Task LateExecute(DiscordSocketClient discordSocketClient, IGuild guild, IUserMessage msg)
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

    public async Task OnReadyAsync()
    {
        await using var uow = db.GetDbContext();

        autoCommands =
            uow.AutoCommands
                .AsNoTracking()
                .Where(x => x.Interval >= 5)
                .AsEnumerable()
                .GroupBy(x => x.GuildId)
                .ToDictionary(x => x.Key,
                    y => y.ToDictionary(x => x.Id, TimerFromAutoCommand)
                        .ToConcurrent())
                .ToConcurrent();

        foreach (var cmd in uow.AutoCommands.AsNoTracking().Where(x => x.Interval == 0))
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

        if (client.ShardId == 0)
        {
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
    }

    private async Task RotatingStatuses()
    {
        var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync())
        {
            try
            {
                if (!bss.Data.RotateStatuses)
                    continue;

                IReadOnlyList<RotatingPlayingStatus> rotatingStatuses;
                var uow = db.GetDbContext();
                await using (uow.ConfigureAwait(false))
                {
                    rotatingStatuses = uow.RotatingStatus.AsNoTracking().OrderBy(x => x.Id).ToList();
                }

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

    public async Task<string?> RemovePlayingAsync(int index)
    {
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(index));

        await using var uow = db.GetDbContext();
        var toRemove = await uow.RotatingStatus
            .AsQueryable()
            .AsNoTracking()
            .Skip(index)
            .FirstOrDefaultAsync().ConfigureAwait(false);

        if (toRemove is null)
            return null;

        uow.Remove(toRemove);
        await uow.SaveChangesAsync().ConfigureAwait(false);
        return toRemove.Status;
    }

    public async Task AddPlaying(ActivityType t, string status)
    {
        await using var uow = db.GetDbContext();
        var toAdd = new RotatingPlayingStatus
        {
            Status = status,
            Type = t
        };
        uow.Add(toAdd);
        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    public bool ToggleRotatePlaying()
    {
        var enabled = false;
        bss.ModifyConfig(bs => enabled = bs.RotateStatuses = !bs.RotateStatuses);
        return enabled;
    }

    public IReadOnlyList<RotatingPlayingStatus> GetRotatingStatuses()
    {
        using var uow = db.GetDbContext();
        return uow.RotatingStatus.AsNoTracking().ToList();
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
            if (guildShard != client.ShardId)
                return;
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

    public void AddNewAutoCommand(AutoCommand cmd)
    {
        using (var uow = db.GetDbContext())
        {
            uow.AutoCommands.Add(cmd);
            uow.SaveChanges();
        }

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

    public string SetDefaultPrefix(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            throw new ArgumentNullException(nameof(prefix));

        bss.ModifyConfig(bs => bs.Prefix = prefix);

        return prefix;
    }

    public IEnumerable<AutoCommand> GetStartupCommands()
    {
        using var uow = db.GetDbContext();
        return
            uow.AutoCommands
                .AsNoTracking()
                .Where(x => x.Interval == 0)
                .OrderBy(x => x.Id)
                .ToList();
    }

    public IEnumerable<AutoCommand> GetAutoCommands()
    {
        using var uow = db.GetDbContext();
        return
            uow.AutoCommands
                .AsNoTracking()
                .Where(x => x.Interval >= 5)
                .OrderBy(x => x.Id)
                .ToList();
    }

    public Task LeaveGuild(string guildStr)
    {
        var sub = cache.Redis.GetSubscriber();
        return sub.PublishAsync($"{creds.RedisKey()}_leave_guild", guildStr);
    }

    public bool RestartBot()
    {
        var cmd = creds.RestartCommand;
        if (string.IsNullOrWhiteSpace(cmd.Cmd))
            return false;

        Restart();
        return true;
    }

    public bool RemoveStartupCommand(int index, out AutoCommand cmd)
    {
        using var uow = db.GetDbContext();
        cmd = uow.AutoCommands
            .AsNoTracking()
            .Where(x => x.Interval == 0)
            .Skip(index)
            .FirstOrDefault();

        if (cmd != null)
        {
            uow.Remove(cmd);
            uow.SaveChanges();
            return true;
        }

        return false;
    }

    public bool RemoveAutoCommand(int index, out AutoCommand cmd)
    {
        using var uow = db.GetDbContext();
        cmd = uow.AutoCommands
            .AsNoTracking()
            .Where(x => x.Interval >= 5)
            .Skip(index)
            .FirstOrDefault();

        if (cmd == null)
            return false;
        uow.Remove(cmd);
        if (autoCommands.TryGetValue(cmd.GuildId, out var autos))
        {
            if (autos.TryRemove(cmd.Id, out var timer))
                timer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        uow.SaveChanges();
        return true;
    }

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

        // i can't just do ReadAsStreamAsync because dicord.net's image poops itself
        var imgData = await sr.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
        var imgStream = imgData.ToStream();
        await using var _ = imgStream.ConfigureAwait(false);
        await client.CurrentUser.ModifyAsync(u => u.Avatar = new Image(imgStream)).ConfigureAwait(false);

        return true;
    }

    public void ClearStartupCommands()
    {
        using var uow = db.GetDbContext();
        var toRemove =
            uow.AutoCommands
                .AsNoTracking()
                .Where(x => x.Interval == 0);

        uow.AutoCommands.RemoveRange(toRemove);
        uow.SaveChanges();
    }

    public void ReloadImages()
    {
        var sub = cache.Redis.GetSubscriber();
        sub.Publish($"{creds.RedisKey()}_reload_images", "");
    }

    public void Die()
    {
        var sub = cache.Redis.GetSubscriber();
        sub.Publish($"{creds.RedisKey()}_die", "", CommandFlags.FireAndForget);
    }

    public void Restart()
    {
        Process.Start(creds.RestartCommand.Cmd, creds.RestartCommand.Args);
        var sub = cache.Redis.GetSubscriber();
        sub.Publish($"{creds.RedisKey()}_die", "", CommandFlags.FireAndForget);
    }

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

    public bool ForwardMessages()
    {
        var isForwarding = false;
        bss.ModifyConfig(config => isForwarding = config.ForwardMessages = !config.ForwardMessages);

        return isForwarding;
    }

    public bool ForwardToAll()
    {
        var isToAll = false;
        bss.ModifyConfig(config => isToAll = config.ForwardToAllOwners = !config.ForwardToAllOwners);
        return isToAll;
    }
}