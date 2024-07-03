﻿using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using Discord.Commands;
using Discord.Interactions;
using Discord.Net;
using Discord.Rest;
using Fergun.Interactive;
using Figgle;
using Lavalink4NET;
using Lavalink4NET.Extensions;
using MartineApiNet;
using Mewdeko.Common.Configs;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Common.PubSub;
using Mewdeko.Common.TypeReaders;
using Mewdeko.Common.TypeReaders.Interactions;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Modules.Currency.Services;
using Mewdeko.Modules.Currency.Services.Impl;
using Mewdeko.Modules.Nsfw;
using Mewdeko.Modules.Searches.Services;
using Mewdeko.Services.Impl;
using Mewdeko.Services.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NekosBestApiNet;
using Newtonsoft.Json;
using NsfwSpyNS;
using Serilog;
using StackExchange.Redis;
using ZiggyCreatures.Caching.Fusion;
using RunMode = Discord.Commands.RunMode;
using TypeReader = Discord.Commands.TypeReader;

namespace Mewdeko;

/// <summary>
///     The main class for Mewdeko, responsible for initializing services, handling events, and managing the bot's
///     lifecycle.
/// </summary>
public class Mewdeko
{

    /// <summary>
    ///     Initializes a new instance of the Mewdeko bot with a specific shard ID.
    /// </summary>
    /// <param name="shardId">
    ///     The ID of the shard this instance will operate on. If set to nothing it will act as if its
    ///     unsharded.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the shard ID is negative.</exception>
    public Mewdeko(int shardId)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(shardId);

        Credentials = new BotCredentials();
        Cache = new RedisCache(Credentials, shardId);
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed


        Client = new DiscordShardedClient(new DiscordSocketConfig
        {
            MessageCacheSize = 15,
            LogLevel = LogSeverity.Critical,
            ConnectionTimeout = int.MaxValue,
            AlwaysDownloadUsers = true,
            GatewayIntents = GatewayIntents.All,
            FormatUsersInBidirectionalUnicode = false,
            LogGatewayIntentWarnings = false,
            DefaultRetryMode = RetryMode.RetryRatelimit
        });
        CommandService = new CommandService(new CommandServiceConfig
        {
            CaseSensitiveCommands = false, DefaultRunMode = RunMode.Async
        });
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

    private CommandService CommandService { get; }

    /// <summary>
    ///     Gets the color used for successful operations.
    /// </summary>
    public static Color OkColor { get; set; }

    /// <summary>
    ///     Gets the color used for error operations.
    /// </summary>
    public static Color ErrorColor { get; set; }

    /// <summary>
    ///     Used to tell other services in the bot if its done initializing.
    /// </summary>
    public TaskCompletionSource<bool> Ready { get; } = new();

    private IServiceProvider Services { get; set; }
    private IDataCache Cache { get; }

    /// <summary>
    ///     Occurs when the bot joins a guild.
    /// </summary>
    public event Func<GuildConfig, Task> JoinedGuild = delegate { return Task.CompletedTask; };


    private async Task AddServices()
    {
        if (!Uri.TryCreate(Credentials.LavalinkUrl, UriKind.Absolute, out _))
        {
            Log.Error("The Lavalink URL is invalid! Please check the Lavalink URL in the configuration");
            Helpers.ReadErrorAndExit(5);
        }

        var sw = Stopwatch.StartNew();
        var s = new ServiceCollection();

        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        s.AddScoped<INsfwSpy, NsfwSpy>()
            .AddSingleton<FontProvider>()
            .AddSingleton<IBotCredentials>(Credentials)
            //Wahoo
            .AddPooledDbContextFactory<MewdekoContext>(builder => builder
                .UseNpgsql(Credentials.PsqlConnectionString, x => x.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
            .EnableDetailedErrors()
            .EnableSensitiveDataLogging())
            .AddSingleton<DbContextProvider>()
            .AddSingleton(Client)
            .AddScoped<EventHandler>()
            .AddSingleton(CommandService)
            .AddSingleton(this)
            .AddSingleton(Cache)
            .AddSingleton(new MartineApi())
            .AddSingleton(Cache.Redis)
            .AddTransient<ISeria, JsonSeria>()
            .AddTransient<IPubSub, RedisPubSub>()
            .AddTransient<IConfigSeria, YamlSeria>()
            .AddSingleton(new InteractiveService(Client, new InteractiveConfig
            {
                ReturnAfterSendingPaginator = true
            }))
            .AddSingleton(new NekosBestApi())
            .AddSingleton(new InteractionService(Client.Rest))
            .AddSingleton<Localization>()
            .AddSingleton<BotConfigService>()
            .AddSingleton<BotConfig>()
            .AddConfigServices()
            .AddBotStringsServices(Credentials.TotalShards)
            .AddMemoryCache()
            .AddLavalink()
            .ConfigureLavalink(x =>
            {
                x.Passphrase = "Hope4a11";
                x.BaseAddress = new Uri(Credentials.LavalinkUrl);
            })
            .AddScoped<ISearchImagesService, SearchImagesService>()
            .AddSingleton<ToneTagService>()
            .AddTransient<GuildSettingsService>();
        s.AddFusionCache().TryWithAutoSetup();
        if (Credentials.UseGlobalCurrency)
        {
            s.AddTransient<ICurrencyService, GlobalCurrencyService>();
        }
        else
        {
            s.AddTransient<ICurrencyService, GuildCurrencyService>();
        }


        Log.Information("Passed Singletons");

        s.AddHttpClient();
        s.AddHttpClient("memelist").ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AllowAutoRedirect = false
        });


        s.Scan(scan => scan.FromAssemblyOf<IReadyExecutor>()
            .AddClasses(classes => classes.AssignableToAny(
                // services
                typeof(INService),
                // behaviours
                typeof(IEarlyBehavior),
                typeof(ILateBlocker),
                typeof(IInputTransformer),
                typeof(ILateExecutor)))
            .AsSelfWithInterfaces()
            .WithScopedLifetime()
        );

        Log.Information("Passed Interface Scanner");
        //initialize Services
        Services = s.BuildServiceProvider();
        var commandHandler = Services.GetService<CommandHandler>();
        commandHandler.AddServices(s);
        _ = Task.Run(() => LoadTypeReaders(typeof(Mewdeko).Assembly));
        var cache = Services.GetService<IDataCache>();
        var audioService = Services.GetService<IAudioService>();
        try
        {
            await audioService.StartAsync();
        }
        catch (Exception e)
        {
            Log.Error("Unable to start audio service: {Message}", e.Message);
        }

        sw.Stop();
        Log.Information($"All services loaded in {sw.Elapsed.TotalSeconds:F2}s");
    }

    private IEnumerable<object> LoadTypeReaders(Assembly assembly)
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
            Log.Warning(ex.LoaderExceptions[0], "Error getting types");
            return Enumerable.Empty<object>();
        }

        var filteredTypes = allTypes
            .Where(x => x.IsSubclassOf(typeof(TypeReader))
                        && x.BaseType.GetGenericArguments().Length > 0
                        && !x.IsAbstract);

        var toReturn = new List<object>();
        foreach (var ft in filteredTypes)
        {
            var x = (TypeReader)ActivatorUtilities.CreateInstance(Services, ft);
            var baseType = ft.BaseType;
            var typeArgs = baseType?.GetGenericArguments();
            if (typeArgs != null)
                CommandService.AddTypeReader(typeArgs[0], x);
            toReturn.Add(x);
        }

        CommandService.AddTypeReaders<IEmote>(
            new TryParseTypeReader<Emote>(Emote.TryParse),
            new TryParseTypeReader<Emoji>(Emoji.TryParse));

        interactionService.AddTypeConverter<TimeSpan>(new TimeSpanConverter());
        interactionService.AddTypeConverter(typeof(IRole[]), new RoleArrayConverter());
        interactionService.AddTypeConverter(typeof(IUser[]), new UserArrayConverter());
        interactionService.AddTypeConverter<StatusRolesTable>(new StatusRolesTypeConverter());

        sw.Stop();
        Log.Information("TypeReaders loaded in {ElapsedTotalSeconds}s", sw.Elapsed.TotalSeconds);
        return toReturn;
    }

    private async Task LoginAsync(string token)
    {
        Client.Log += Client_Log;
        var clientReady = new TaskCompletionSource<bool>();

        Task SetClientReady(DiscordSocketClient unused)
        {
            ReadyCount++;
            Log.Information($"Shard {unused.ShardId} is ready");
            if (ReadyCount != Client.Shards.Count)
                return Task.CompletedTask;
            _ = Task.Run(() => clientReady.TrySetResult(true));
            return Task.CompletedTask;
        }

        //connect
        Log.Information("Logging in...");
        try
        {
            await Client.LoginAsync(TokenType.Bot, token).ConfigureAwait(false);
            await Client.StartAsync().ConfigureAwait(false);
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
        Log.Information("Logged in.");
        Log.Information("Logged in as:");
        Console.WriteLine(FiggleFonts.Digital.Render(Client.CurrentUser.Username));
    }

    private Task Client_LeftGuild(SocketGuild arg)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var chan = await Client.Rest.GetChannelAsync(Credentials.GuildJoinsChannelId).ConfigureAwait(false);
                await ((RestTextChannel)chan).SendErrorAsync($"Left server: {arg.Name} [{arg.Id}]", new BotConfig(),
                [
                    new EmbedFieldBuilder().WithName("Total Guilds")
                        .WithValue(Client.Guilds.Count)
                ]).ConfigureAwait(false);
            }
            catch
            {
                //ignored
            }

            Log.Information("Left server: {0} [{1}]", arg.Name, arg.Id);
        });
        return Task.CompletedTask;
    }

    private Task Client_JoinedGuild(SocketGuild arg)
    {
        _ = Task.Run(async () =>
        {
            var dbContext = Services.GetRequiredService<MewdekoContext>();
            await arg.DownloadUsersAsync().ConfigureAwait(false);
            Log.Information("Joined server: {0} [{1}]", arg.Name, arg.Id);

            var gc = await dbContext.ForGuildId(arg.Id);

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

    private async Task RunAsync()
    {
        var sw = Stopwatch.StartNew();

        await LoginAsync(Credentials.Token).ConfigureAwait(false);

        Log.Information("Loading Services...");
        try
        {
            await AddServices();
            var dbProvider = Services.GetRequiredService<DbContextProvider>();
            await using var dbContext = await dbProvider.GetContextAsync();
            await dbContext.EnsureUserCreated(Client.CurrentUser.Id, Client.CurrentUser.Username, Client.CurrentUser.Discriminator, Client.CurrentUser.AvatarId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error adding services");
            Helpers.ReadErrorAndExit(9);
        }

        sw.Stop();
        Log.Information("Connected in {Elapsed:F2}s", sw.Elapsed.TotalSeconds);
        var commandService = Services.GetService<CommandService>();
        var interactionService = Services.GetRequiredService<InteractionService>();
        await commandService.AddModulesAsync(GetType().GetTypeInfo().Assembly, Services)
            .ConfigureAwait(false);
        await interactionService.AddModulesAsync(GetType().GetTypeInfo().Assembly, Services)
            .ConfigureAwait(false);
#if !DEBUG
            await interactionService.RegisterCommandsGloballyAsync().ConfigureAwait(false);
#endif
#if DEBUG
        if (Client.Guilds.Select(x => x.Id).Contains(Credentials.DebugGuildId))
            await interactionService.RegisterCommandsToGuildAsync(Credentials.DebugGuildId);
#endif


        _ = Task.Run(HandleStatusChanges);
        _ = Task.Run(async () => await ExecuteReadySubscriptions());
        Ready.TrySetResult(true);
        Log.Information("Ready.");
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
                Log.Error(ex, "Failed running OnReadyAsync method on {Type} type: {Message}", toExec.GetType().Name,
                    ex.Message);
            }
        });
        await tasks.WhenAll();
    }

    private static Task Client_Log(LogMessage arg)
    {
        if (arg.Exception != null)
            Log.Warning(arg.Exception, arg.Source + " | " + arg.Message);
        else
            Log.Information(arg.Source + " | " + arg.Message);

        return Task.CompletedTask;
    }

    /// <summary>
    ///     Runs the bot and blocks the calling thread until the bot is stopped.
    /// </summary>
    public async Task RunAndBlockAsync()
    {
        await RunAsync().ConfigureAwait(false);
        await Task.Delay(-1).ConfigureAwait(false);
    }

    private void HandleStatusChanges()
    {
        var sub = Services.GetService<IDataCache>().Redis.GetSubscriber();
        // ReSharper disable once AsyncVoidLambda
        sub.Subscribe($"{Client.CurrentUser.Id}_status.game_set", async (_, game) =>
        {
            try
            {
                var obj = new
                {
                    Name = default(string), Activity = ActivityType.Playing
                };
                obj = JsonConvert.DeserializeAnonymousType(game, obj);
                await Client.SetGameAsync(obj.Name, type: obj.Activity).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error setting game");
            }
        }, CommandFlags.FireAndForget);

        // ReSharper disable once AsyncVoidLambda
        sub.Subscribe($"{Client.CurrentUser.Id}_status.stream_set", async (_, streamData) =>
        {
            try
            {
                var obj = new
                {
                    Name = "", Url = ""
                };
                obj = JsonConvert.DeserializeAnonymousType(streamData, obj);
                await Client.SetGameAsync(obj?.Name, obj!.Url, ActivityType.Streaming).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error setting stream");
            }
        }, CommandFlags.FireAndForget);
    }

    /// <summary>
    ///     Sets the bot's status to the specified game.
    /// </summary>
    /// <param name="game">The name of the game to set.</param>
    /// <param name="type">The type of activity.</param>
    public async Task SetGameAsync(string? game, ActivityType type)
    {
        var obj = new
        {
            Name = game, Activity = type
        };
        var sub = Services.GetService<IDataCache>().Redis.GetSubscriber();
        await sub.PublishAsync($"{Client.CurrentUser.Id}_status.game_set", JsonConvert.SerializeObject(obj))
            .ConfigureAwait(false);
    }
}