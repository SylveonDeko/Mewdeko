using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using DataModel;
using Discord.Commands;
using Discord.Interactions;
using Discord.Net;
using Discord.Rest;
using Figgle.Fonts;
using Lavalink4NET;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Common.TypeReaders;
using Mewdeko.Common.TypeReaders.Interactions;
using Mewdeko.Services.Impl;
using Mewdeko.Services.Settings;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;
using StackExchange.Redis;
using TypeReader = Discord.Commands.TypeReader;

namespace Mewdeko;

/// <summary>
///     The main class for the Mewdeko bot, responsible for initializing services, handling events, and managing the bot's
///     lifecycle.
/// </summary>
public class Mewdeko : IDisposable
{
    // Cached JsonSerializerOptions for performance
    private static readonly JsonSerializerOptions CachedJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly BotConfigService bss;

    private readonly ILogger<Mewdeko> logger;
    private bool disposed;

    /// <summary>
    ///     Initializes a new instance of the Mewdeko class.
    /// </summary>
    /// <param name="services">The service provider for dependency injection.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    public Mewdeko(IServiceProvider services, ILogger<Mewdeko> logger)
    {
        Services = services;
        this.logger = logger;
        Credentials = Services.GetRequiredService<BotCredentials>();
        Services.GetRequiredService<IDataCache>();
        Client = Services.GetRequiredService<DiscordShardedClient>();
        CommandService = Services.GetRequiredService<CommandService>();
        GuildSettingsService = Services.GetRequiredService<GuildSettingsService>();
        bss = Services.GetRequiredService<BotConfigService>();
    }

    /// <summary>
    ///     Gets the credentials used by the bot.
    /// </summary>
    public BotCredentials Credentials { get; }

    private int ReadyCount { get; set; }

    /// <summary>
    ///     Gets the Discord client used by the bot.
    /// </summary>
    public DiscordShardedClient Client { get; }

    private GuildSettingsService GuildSettingsService { get; }

    private CommandService CommandService { get; }

    /// <summary>
    ///     Gets or sets the color used for successful operations.
    /// </summary>
    public static Color OkColor { get; set; }

    /// <summary>
    ///     Gets or sets the color used for error operations.
    /// </summary>
    public static Color ErrorColor { get; set; }

    /// <summary>
    ///     Gets a TaskCompletionSource that completes when the bot is ready.
    /// </summary>
    public TaskCompletionSource<bool> Ready { get; } = new();

    private IServiceProvider Services { get; }

    /// <summary>
    ///     Disposes the Mewdeko instance and unsubscribes from events.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Event that occurs when the bot joins a guild.
    /// </summary>
    public event Func<GuildConfig, Task> JoinedGuild = delegate { return Task.CompletedTask; };

    /// <summary>
    ///     Loads type readers from the specified assembly.
    /// </summary>
    /// <param name="assembly">The assembly to load type readers from.</param>
    private void LoadTypeReaders(Assembly assembly)
    {
        var sw = new Stopwatch();
        sw.Start();
        var interactionService = Services.GetService<InteractionService>();
        Type[] allTypes;
        try
        {
            allTypes = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            logger.LogWarning(ex.LoaderExceptions[0], "Error getting types");
            return;
        }

        var filteredTypes = allTypes
            .Where(x => x.IsSubclassOf(typeof(TypeReader))
                        && x.BaseType.GetGenericArguments().Length > 0
                        && !x.IsAbstract);

        foreach (var ft in filteredTypes)
        {
            var x = (TypeReader)ActivatorUtilities.CreateInstance(Services, ft);
            var baseType = ft.BaseType;
            var typeArgs = baseType?.GetGenericArguments();
            if (typeArgs != null)
                CommandService.AddTypeReader(typeArgs[0], x);
        }

        CommandService.AddTypeReaders<IEmote>(
            new TryParseTypeReader<Emote>(Emote.TryParse),
            new TryParseTypeReader<Emoji>(Emoji.TryParse));

        interactionService.AddTypeConverter<TimeSpan>(new TimeSpanConverter());
        interactionService.AddTypeConverter<IRole[]>(new RoleArrayConverter());
        interactionService.AddTypeConverter<IUser[]>(new UserArrayConverter());
        interactionService.AddTypeConverter<StatusRole>(new StatusRolesTypeConverter());


        sw.Stop();
        logger.LogInformation("TypeReaders loaded in {ElapsedTotalSeconds}s", sw.Elapsed.TotalSeconds);
    }

    private async Task LoginAsync(string token)
    {
        Client.Log += Client_Log;
        var clientReady = new TaskCompletionSource<bool>();

        logger.LogInformation("Logging in...");
        try
        {
            // Login but don't start shards yet
            await Client.LoginAsync(TokenType.Bot, token.Trim()).ConfigureAwait(false);
            var gw = await Client.GetBotGatewayAsync();

            var maxConcurrency = gw.SessionStartLimit.MaxConcurrency;

            // Start shards in rate-limited batches according to max concurrency
            var totalShards = Client.Shards.Count;
            logger.LogInformation("Starting {TotalShards} shards with max concurrency of {MaxConcurrency}", totalShards,
                maxConcurrency);

            // Group shards by their rate limit bucket using the formula from Discord docs
            var shardGroups = Client.Shards
                .GroupBy(shard => shard.ShardId % maxConcurrency)
                .OrderBy(group => group.Key)
                .ToList();

            // Start each batch of shards
            foreach (var group in shardGroups)
            {
                var tasks = group.Select(shard => shard.StartAsync()).ToList();
                logger.LogInformation("Starting shard bucket {BucketKey} with {ShardCount} shards", group.Key,
                    tasks.Count);
                await Task.WhenAll(tasks);
            }
        }
        catch (HttpException ex)
        {
            LoginErrorHandler.Handle(ex);
            Helpers.ReadErrorAndExit(3);
        }
        catch (Exception ex)
        {
            LoginErrorHandler.Handle(ex);
            Helpers.ReadErrorAndExit(4);
        }

        Client.ShardReady += SetClientReady;
        await clientReady.Task.ConfigureAwait(false);
        Client.ShardReady -= SetClientReady;
        Client.JoinedGuild += Client_JoinedGuild;
        Client.LeftGuild += Client_LeftGuild;
        logger.LogInformation("Logged in.");
        logger.LogInformation("Logged in as:");
        Console.WriteLine(FiggleFonts.Digital.Render(Client.CurrentUser.Username));
        return;

        Task SetClientReady(DiscordSocketClient unused)
        {
            ReadyCount++;
            logger.LogInformation("Shard {ShardId} is ready", unused.ShardId);
            logger.LogInformation("{ReadyCount}/{TotalShards} shards connected", ReadyCount, Client.Shards.Count);
            if (ReadyCount != Client.Shards.Count)
                return Task.CompletedTask;
            _ = Task.Run(() => clientReady.TrySetResult(true));
            return Task.CompletedTask;
        }
    }

    private Task Client_LeftGuild(SocketGuild arg)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var chan = await Client.Rest.GetChannelAsync(Credentials.GuildJoinsChannelId).ConfigureAwait(false);
                await ((RestTextChannel)chan).SendErrorAsync($"Left server: {arg.Name} [{arg.Id}]", bss.Data,
                [
                    new EmbedFieldBuilder().WithName("Total Guilds")
                        .WithValue(Client.Guilds.Count)
                ]).ConfigureAwait(false);
            }
            catch
            {
                //ignored
            }

            logger.LogInformation("Left server: {0} [{1}]", arg.Name, arg.Id);
        });
        return Task.CompletedTask;
    }

    private Task Client_JoinedGuild(SocketGuild arg)
    {
        _ = Task.Run(async () =>
        {
            logger.LogInformation("Joined server: {0} [{1}]", arg.Name, arg.Id);

            var gc = await GuildSettingsService.GetGuildConfig(arg.Id).ConfigureAwait(false);

            await JoinedGuild.Invoke(gc).ConfigureAwait(false);
            var chan =
                await Client.Rest.GetChannelAsync(Credentials.GuildJoinsChannelId).ConfigureAwait(false) as
                    RestTextChannel;
            var eb = new EmbedBuilder();
            eb.WithTitle($"Joined {Format.Bold(arg.Name)} {arg.Id}");
            eb.AddField("Members", arg.MemberCount);
            eb.AddField("Boosts", arg.PremiumSubscriptionCount);
            eb.AddField("Owner", $"Name: {arg.Owner}\nID: {arg.OwnerId}");
            eb.AddField("Text Channels", arg.TextChannels.Count);
            eb.AddField("Voice Channels", arg.VoiceChannels.Count);
            eb.AddField("Total Guilds", Client.Guilds.Count);
            eb.WithThumbnailUrl(arg.IconUrl);
            eb.WithColor(OkColor);
            await chan.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
        });
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Runs the bot, initializing all necessary components and services.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task RunAsync()
    {
        var sw = Stopwatch.StartNew();


        await LoginAsync(Credentials.Token).ConfigureAwait(false);

        logger.LogInformation("Loading Services...");
        try
        {
            LoadTypeReaders(typeof(Mewdeko).Assembly);
            var audioService = Services.GetService<IAudioService>();
            try
            {
                await audioService.StartAsync();
            }
            catch (Exception e)
            {
                logger.LogError("Unable to start audio service: {Message}", e.Message);
            }

            var dbProvider = Services.GetRequiredService<IDataConnectionFactory>();
            await using var dbContext = await dbProvider.CreateConnectionAsync();
            await dbContext.EnsureUserCreated(Client.CurrentUser.Id, Client.CurrentUser.Username,
                Client.CurrentUser.AvatarId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error adding services");
            Helpers.ReadErrorAndExit(9);
        }


        sw.Stop();
        logger.LogInformation("Connected in {Elapsed:F2}s", sw.Elapsed.TotalSeconds);
        var commandService = Services.GetService<CommandService>();
        commandService.Log += LogCommandsService;
        var interactionService = Services.GetRequiredService<InteractionService>();
        try
        {
            await commandService.AddModulesAsync(Assembly.GetExecutingAssembly(), Services);
            await interactionService.AddModulesAsync(Assembly.GetExecutingAssembly(), Services);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to add modules to command/interaction service");
            throw;
        }
#if !DEBUG
        await interactionService.RegisterCommandsGloballyAsync().ConfigureAwait(false);
#endif
#if DEBUG
        if (Client.Guilds.Select(x => x.Id).Contains(Credentials.DebugGuildId))
            await interactionService.RegisterCommandsToGuildAsync(Credentials.DebugGuildId);
#endif

        _ = Task.Run(HandleStatusChanges);
        _ = Task.Run(ExecuteReadySubscriptions);
        var performanceMonitor = Services.GetRequiredService<PerformanceMonitorService>();
        performanceMonitor.Initialize(typeof(Mewdeko).Assembly, "Mewdeko");
        Ready.TrySetResult(true);
        logger.LogInformation("Ready.");
    }

    private Task LogCommandsService(LogMessage arg)
    {
        logger.LogInformation(arg.ToString());
        return Task.CompletedTask;
    }

    private async Task ExecuteReadySubscriptions()
    {
        var readyExecutors = Services.GetServices<IReadyExecutor>();
        var tasks = readyExecutors.Select(async toExec =>
        {
            try
            {
                await toExec.OnReadyAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed running OnReadyAsync method on {Type} type: {Message}",
                    toExec.GetType().Name,
                    ex.Message);
            }
        });
        await Task.WhenAll(tasks);
    }

    private async Task Client_Log(LogMessage arg)
    {
        var severity = arg.Severity switch
        {
            LogSeverity.Critical => LogEventLevel.Fatal,
            LogSeverity.Error => LogEventLevel.Error,
            LogSeverity.Warning => LogEventLevel.Warning,
            LogSeverity.Info => LogEventLevel.Information,
            LogSeverity.Verbose => LogEventLevel.Verbose,
            LogSeverity.Debug => LogEventLevel.Debug,
            _ => LogEventLevel.Information
        };
        Log.Write(severity, arg.Exception, "[{Source}] {Message}", arg.Source, arg.Message);
        await Task.CompletedTask;
    }

    private void HandleStatusChanges()
    {
        var sub = Services.GetService<IDataCache>().Redis.GetSubscriber();

        sub.Subscribe(RedisChannel.Literal($"{Client.CurrentUser.Id}_status.game_set"), async void (_, game) =>
        {
            try
            {
                var status = JsonSerializer.Deserialize<GameStatus>((string)game, CachedJsonOptions);
                await Client.SetGameAsync(status?.Name, type: status?.Activity ?? ActivityType.Playing)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error setting game");
            }
        }, CommandFlags.FireAndForget);

        sub.Subscribe(RedisChannel.Literal($"{Client.CurrentUser.Id}_status.stream_set"), async void (_, streamData) =>
        {
            try
            {
                var stream = JsonSerializer.Deserialize<StreamStatus>((string)streamData, CachedJsonOptions);
                await Client.SetGameAsync(stream?.Name, stream?.Url, ActivityType.Streaming)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error setting stream");
            }
        }, CommandFlags.FireAndForget);
    }

    /// <summary>
    ///     Sets the bot's game status.
    /// </summary>
    /// <param name="game">The name of the game to set.</param>
    /// <param name="type">The type of activity.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SetGameAsync(string? game, ActivityType type)
    {
        var obj = new
        {
            Name = game, Activity = type
        };
        var sub = Services.GetService<IDataCache>().Redis.GetSubscriber();
        await sub.PublishAsync(RedisChannel.Literal($"{Client.CurrentUser.Id}_status.game_set"),
                JsonSerializer.Serialize(obj))
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Protected implementation of Dispose pattern.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (disposed)
            return;

        if (disposing)
        {
            // Unsubscribe from events to prevent memory leaks
            if (Client != null)
            {
                Client.Log -= Client_Log;
                Client.JoinedGuild -= Client_JoinedGuild;
                Client.LeftGuild -= Client_LeftGuild;
            }

            try
            {
                var commandService = Services.GetService<CommandService>();
                if (commandService != null)
                {
                    commandService.Log -= LogCommandsService;
                }
            }
            catch (ObjectDisposedException)
            {
            }
        }

        disposed = true;
    }
}