using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.Net;
using Discord.Rest;
using Discord.WebSocket;
using KSoftNet;
using Mewdeko._Extensions;
using Mewdeko.Common;
using Mewdeko.Common.Configs;
using Mewdeko.Common.Extensions;
using Mewdeko.Common.Extensions.Interactive;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Common.PubSub;
using Mewdeko.Modules.CustomReactions.Services;
using Mewdeko.Modules.Gambling.Services;
using Mewdeko.Modules.Gambling.Services.Impl;
using Mewdeko.Modules.OwnerOnly.Services;
using Mewdeko.Services.Database.Models;
using Mewdeko.Services.Impl;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Serilog;
using StackExchange.Redis;
using Victoria;
using RunMode = Discord.Commands.RunMode;

namespace Mewdeko.Services;

public class Mewdeko
{
    private readonly DbService _db;
    private readonly string _token = "95dd4f5d54692fc533bd1da43f1cab773c71d894";

    public Mewdeko(int shardId)
    {
        if (shardId < 0)
            throw new ArgumentOutOfRangeException(nameof(shardId));


        Credentials = new BotCredentials();
        Cache = new RedisCache(Credentials, shardId);
        _db = new DbService(Credentials);

        if (shardId == 0) _db.Setup();

        Client = new DiscordSocketClient(new DiscordSocketConfig
        {
            MessageCacheSize = 15,
            LogLevel = LogSeverity.Warning,
            ConnectionTimeout = int.MaxValue,
            TotalShards = Credentials.TotalShards,
            ShardId = shardId,
            AlwaysDownloadUsers = true,
            GatewayIntents = GatewayIntents.All
        });
        ;

        CommandService = new CommandService(new CommandServiceConfig
        {
            CaseSensitiveCommands = false,
            DefaultRunMode = RunMode.Async
        });
        Client.Log += Client_Log;
    }

    public BotCredentials Credentials { get; }
    public DiscordSocketClient Client { get; }
    public CommandService CommandService { get; }
    public ImmutableArray<GuildConfig> AllGuildConfigs { get; private set; }

    /* Will have to be removed soon, it's been way too long */
    public static Color OkColor { get; set; }
    public static Color ErrorColor { get; set; }

    public TaskCompletionSource<bool> Ready { get; } = new();

    public IServiceProvider Services { get; private set; }
    public IDataCache Cache { get; }


    public event Func<GuildConfig, Task> JoinedGuild = delegate { return Task.CompletedTask; };


    public List<ulong> GetCurrentGuildIds()
    {
        return Client.Guilds.Select(x => x.Id).ToList();
    }

    private void AddServices()
    {
        var startingGuildIdList = GetCurrentGuildIds();
        var sw = Stopwatch.StartNew();
        var _bot = Client.CurrentUser;

        using (var uow = _db.GetDbContext())
        {
            uow.DiscordUsers.EnsureCreated(_bot.Id, _bot.Username, _bot.Discriminator, _bot.AvatarId);
            AllGuildConfigs = uow.GuildConfigs.GetAllGuildConfigs(startingGuildIdList).ToImmutableArray();
        }

        var s = new ServiceCollection()
            .AddSingleton<IBotCredentials>(Credentials)
            .AddSingleton(_db)
            .AddSingleton(Client)
            .AddSingleton(CommandService)
            .AddSingleton(this)
            .AddSingleton(Cache)
            .AddSingleton(new KSoftApi(_token))
            .AddSingleton(Cache.Redis)
            .AddSingleton<ISeria, JsonSeria>()
            .AddSingleton<IPubSub, RedisPubSub>()
            .AddSingleton<IConfigSeria, YamlSeria>()
            .AddSingleton<InteractiveService>()
            .AddSingleton<InteractionService>()
            .AddConfigServices()
            .AddBotStringsServices()
            .AddMemoryCache()
            .AddSingleton<LavaNode>()
            .AddSingleton<LavaConfig>()
            .AddSingleton<IShopService, ShopService>();
        s.AddLavaNode(x =>
        {
            x.SelfDeaf = true;
            x.Authorization = "Hope4a11";
            x.Port = 2333;
        });

        s.AddHttpClient();
        s.AddHttpClient("memelist").ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AllowAutoRedirect = false
        });
        if (Environment.GetEnvironmentVariable("MEWDEKO_IS_COORDINATED") != "1")
            s.AddSingleton<ICoordinator, SingleProcessCoordinator>();
        else
            s.AddSingleton<RemoteGrpcCoordinator>()
                .AddSingleton<ICoordinator>(x => x.GetRequiredService<RemoteGrpcCoordinator>())
                .AddSingleton<IReadyExecutor>(x => x.GetRequiredService<RemoteGrpcCoordinator>());

        s.LoadFrom(Assembly.GetAssembly(typeof(CommandHandler))!);

        s.AddSingleton<IReadyExecutor>(x => x.GetService<OwnerOnlyService>());
        s.AddSingleton<IReadyExecutor>(x => x.GetService<CustomReactionsService>());
        //initialize Services
        Services = s.BuildServiceProvider();
        var commandHandler = Services.GetService<CommandHandler>();

        //what the fluff
        commandHandler?.AddServices(s);
        _ = LoadTypeReaders(typeof(Mewdeko).Assembly);

        sw.Stop();
        Log.Information($"All services loaded in {sw.Elapsed.TotalSeconds:F2}s");
    }


    private IEnumerable<object> LoadTypeReaders(Assembly assembly)
    {
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
            var x = (TypeReader) Activator.CreateInstance(ft, Client, CommandService);
            var baseType = ft.BaseType;
            var typeArgs = baseType?.GetGenericArguments();
            if (typeArgs != null) CommandService.AddTypeReader(typeArgs[0], x);
            toReturn.Add(x);
        }

        return toReturn;
    }

    private async Task LoginAsync(string token)
    {
        var clientReady = new TaskCompletionSource<bool>();

        Task SetClientReady()
        {
            var _ = Task.Run(async () =>
            {
                clientReady.TrySetResult(true);
                try
                {
                    foreach (var chan in await Client.GetDMChannelsAsync().ConfigureAwait(false))
                        await chan.CloseAsync().ConfigureAwait(false);
                }
                catch
                {
                    // ignored
                }
            });
            return Task.CompletedTask;
        }

        //connect
        Log.Information("Shard {0} logging in ...", Client.ShardId);
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

        Client.Ready += SetClientReady;
        await clientReady.Task.ConfigureAwait(false);
        Client.Ready -= SetClientReady;
        Client.JoinedGuild += Client_JoinedGuild;
        Client.LeftGuild += Client_LeftGuild;
        Log.Information("Shard {0} logged in.", Client.ShardId);
    }

    private Task Client_LeftGuild(SocketGuild arg)
    {
        try
        {
            var chan = Client.Rest.GetChannelAsync(892789588739891250).Result as RestTextChannel;
            chan.SendErrorAsync($"Left server: {arg.Name} [{arg.Id}]");
        }
        catch
        {
            //ignored
        }

        Log.Information("Left server: {0} [{1}]", arg.Name, arg.Id);
        return Task.CompletedTask;
    }

    private Task Client_JoinedGuild(SocketGuild arg)
    {
        arg.DownloadUsersAsync();
        Log.Information("Joined server: {0} [{1}]", arg.Name, arg.Id);
        var _ = Task.Run(async () =>
        {
            GuildConfig gc;
            using (var uow = _db.GetDbContext())
            {
                gc = uow.GuildConfigs.ForId(arg.Id);
            }

            await JoinedGuild.Invoke(gc).ConfigureAwait(false);
        });
        try
        {
            arg.CurrentUser.ModifyAsync(x => x.Nickname = "Hanekawa");
        }
        catch
        {
            // ignored
        }

        var chan = Client.Rest.GetChannelAsync(892789588739891250).Result as RestTextChannel;
        var eb = new EmbedBuilder();
        eb.WithTitle($"Joined {Format.Bold(arg.Name)}");
        eb.AddField("Server ID", arg.Id);
        eb.AddField("Members", arg.MemberCount);
        eb.AddField("Boosts", arg.PremiumSubscriptionCount);
        eb.AddField("Owner", $"Name: {arg.Owner}\nID: {arg.OwnerId}");
        eb.AddField("Text Channels", arg.TextChannels.Count);
        eb.AddField("Voice Channels", arg.VoiceChannels.Count);
        eb.WithThumbnailUrl(arg.IconUrl);
        eb.WithColor(OkColor);
        chan.SendMessageAsync(embed: eb.Build());
        return Task.CompletedTask;
    }

    public async Task RunAsync()
    {
        var sw = Stopwatch.StartNew();

        await LoginAsync(Credentials.Token).ConfigureAwait(false);

        Log.Information("Shard {ShardId} loading services...", Client.ShardId);
        try
        {
            AddServices();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error adding services");
            Helpers.ReadErrorAndExit(9);
        }

        sw.Stop();
        Log.Information("Shard {ShardId} connected in {Elapsed:F2}s", Client.ShardId, sw.Elapsed.TotalSeconds);

        var stats = Services.GetService<IStatsService>();
        stats.Initialize();
        var commandHandler = Services.GetService<CommandHandler>();
        var CommandService = Services.GetService<CommandService>();
        var InteractionService = Services.GetRequiredService<InteractionService>();
        var lava = Services.GetRequiredService<LavaNode>();
        await lava.ConnectAsync();
        var a = await CommandService.AddModulesAsync(GetType().GetTypeInfo().Assembly, Services)
            .ConfigureAwait(false);
        var e = await InteractionService.AddModulesAsync(GetType().GetTypeInfo().Assembly, Services)
            .ConfigureAwait(false);
        foreach (var i in Client.Guilds)
            try
            {
                await InteractionService.RegisterCommandsToGuildAsync(i.Id);
            }
            catch (Exception s)
            {
                Console.WriteLine(s);
            }

        // start handling messages received in commandhandler
        await commandHandler.StartHandling().ConfigureAwait(false);


        HandleStatusChanges();
        Ready.TrySetResult(true);
        _ = Task.Run(ExecuteReadySubscriptions);
        Log.Information("Shard {ShardId} ready", Client.ShardId);
    }

    private Task ExecuteReadySubscriptions()
    {
        var readyExecutors = Services.GetServices<IReadyExecutor>();
        var tasks = readyExecutors.Select(async toExec =>
        {
            try
            {
                await toExec.OnReadyAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed running OnReadyAsync method on {Type} type: {Message}",
                    toExec.GetType().Name, ex.Message);
            }
        });

        return Task.WhenAll(tasks);
    }

    private Task Client_Log(LogMessage arg)
    {
        if (arg.Exception != null)
            Log.Warning(arg.Exception, arg.Source + " | " + arg.Message);
        else
            Log.Warning(arg.Source + " | " + arg.Message);

        return Task.CompletedTask;
    }

    public async Task RunAndBlockAsync()
    {
        await RunAsync().ConfigureAwait(false);
        await Task.Delay(-1).ConfigureAwait(false);
    }


    private void HandleStatusChanges()
    {
        var sub = Services.GetService<IDataCache>().Redis.GetSubscriber();
        sub.Subscribe(Client.CurrentUser.Id + "_status.game_set", async (ch, game) =>
        {
            try
            {
                var obj = new {Name = default(string), Activity = ActivityType.Playing};
                obj = JsonConvert.DeserializeAnonymousType(game, obj);
                await Client.SetGameAsync(obj.Name, type: obj.Activity).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error setting game");
            }
        }, CommandFlags.FireAndForget);

        sub.Subscribe(Client.CurrentUser.Id + "_status.stream_set", async (ch, streamData) =>
        {
            try
            {
                var obj = new {Name = "", Url = ""};
                obj = JsonConvert.DeserializeAnonymousType(streamData, obj);
                await Client.SetGameAsync(obj.Name, obj.Url, ActivityType.Streaming).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error setting stream");
            }
        }, CommandFlags.FireAndForget);
    }

    public Task SetGameAsync(string game, ActivityType type)
    {
        var obj = new {Name = game, Activity = type};
        var sub = Services.GetService<IDataCache>().Redis.GetSubscriber();
        return sub.PublishAsync(Client.CurrentUser.Id + "_status.game_set", JsonConvert.SerializeObject(obj));
    }
}