using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using Discord.Rest;
using Lavalink4NET;
using Lavalink4NET.Events;
using Lavalink4NET.Events.Players;
using LinqToDB.Data;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Database.Impl;
using Microsoft.Extensions.DependencyInjection;

namespace Mewdeko.Services.Analytics;

/// <summary>
///     Wires the collector into the Discord client, the REST client, linq2db, the Lavalink node and the process,
///     emitting the shard, fleet, REST, database, process and music metrics from the catalog.
/// </summary>
public sealed class AnalyticsInstrumentation : INService, IReadyExecutor, IDisposable
{
    private const string NodeLabel = "default";
    private const int UserCountEveryTicks = 5;
    private static readonly TimeSpan ProcessInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan FleetInterval = TimeSpan.FromSeconds(60);

    private readonly InteractionAckTracker ackTracker;
    private readonly DiscordShardedClient client;
    private readonly IAnalyticsCollector collector;
    private readonly IDataConnectionFactory dbFactory;
    private readonly ILogger<AnalyticsInstrumentation> logger;
    private readonly Process process = Process.GetCurrentProcess();
    private readonly IServiceProvider services;

    private IAudioService? audioService;
    private bool disposed;
    private int fleetTicks;
    private Timer? fleetTimer;
    private TimeSpan lastCpuTime;
    private long lastCpuTimestamp;
    private int nodeState;
    private Timer? processTimer;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AnalyticsInstrumentation" /> class.
    /// </summary>
    /// <param name="collector">The collector to emit into.</param>
    /// <param name="client">The Discord client.</param>
    /// <param name="dbFactory">The database connection factory, traced for query timings.</param>
    /// <param name="ackTracker">Tracks interaction acknowledgements observed on the REST client.</param>
    /// <param name="services">The service provider, used to find the optional audio service.</param>
    /// <param name="logger">Records hook failures.</param>
    public AnalyticsInstrumentation(IAnalyticsCollector collector, DiscordShardedClient client,
        IDataConnectionFactory dbFactory, InteractionAckTracker ackTracker, IServiceProvider services,
        ILogger<AnalyticsInstrumentation> logger)
    {
        this.collector = collector;
        this.client = client;
        this.dbFactory = dbFactory;
        this.ackTracker = ackTracker;
        this.services = services;
        this.logger = logger;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        processTimer?.Dispose();
        fleetTimer?.Dispose();
        process.Dispose();
    }

    /// <inheritdoc />
    public Task OnReadyAsync()
    {
        if (!collector.Enabled)
            return Task.CompletedTask;

        var eventHandler = services.GetRequiredService<EventHandler>();
        eventHandler.Subscribe("ShardConnected", "AnalyticsInstrumentation", OnShardConnected);
        eventHandler.Subscribe("ShardDisconnected", "AnalyticsInstrumentation", OnShardDisconnected);
        eventHandler.Subscribe("ShardReady", "AnalyticsInstrumentation", OnShardReady);
        eventHandler.Subscribe("ShardLatencyUpdated", "AnalyticsInstrumentation", OnShardLatencyUpdated);
        eventHandler.Subscribe("JoinedGuild", "AnalyticsInstrumentation", OnJoinedGuild);
        eventHandler.Subscribe("LeftGuild", "AnalyticsInstrumentation", OnLeftGuild);

        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        AttachRestHooks();
        AttachDatabaseTracing();
        AttachMusicHooks();

        foreach (var shard in client.Shards)
            collector.Gauge("shard.state", shard.ConnectionState == ConnectionState.Connected ? 1 : 0,
                ("shard", shard.ShardId.ToString()));

        lastCpuTime = process.TotalProcessorTime;
        lastCpuTimestamp = Stopwatch.GetTimestamp();
        processTimer = new Timer(_ => EmitProcess(), null, TimeSpan.Zero, ProcessInterval);
        fleetTimer = new Timer(_ => EmitFleet(), null, TimeSpan.Zero, FleetInterval);
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Replaces snowflakes and tokens in a REST endpoint with placeholders so requests group by route.
    /// </summary>
    /// <param name="endpoint">The endpoint as sent, without the base URL.</param>
    /// <returns>The templated route.</returns>
    public static string TemplateRoute(string endpoint)
    {
        var path = endpoint;
        var query = path.IndexOf('?');
        if (query >= 0)
            path = path[..query];

        var segments = path.Split('/');
        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];
            if (segment.Length is >= 17 and <= 20 && segment.All(char.IsAsciiDigit))
                segments[i] = "{id}";
            else if (segment.Length > 40)
                segments[i] = "{token}";
        }

        return string.Join('/', segments);
    }

    /// <summary>
    ///     Derives the operation label for a SQL statement from its first keyword.
    /// </summary>
    /// <param name="sql">The statement text.</param>
    /// <returns>select, insert, update, delete or other.</returns>
    public static string OperationOf(string? sql)
    {
        if (string.IsNullOrEmpty(sql))
            return "other";

        var index = 0;
        while (index < sql.Length)
        {
            while (index < sql.Length && char.IsWhiteSpace(sql[index]))
                index++;
            if (index + 1 < sql.Length && sql[index] == '-' && sql[index + 1] == '-')
            {
                while (index < sql.Length && sql[index] != '\n')
                    index++;
                continue;
            }

            break;
        }

        var end = index;
        while (end < sql.Length && char.IsLetter(sql[end]))
            end++;

        var keyword = sql.AsSpan(index, end - index);
        if (keyword.Equals("select", StringComparison.OrdinalIgnoreCase) ||
            keyword.Equals("with", StringComparison.OrdinalIgnoreCase))
            return "select";
        if (keyword.Equals("insert", StringComparison.OrdinalIgnoreCase))
            return "insert";
        if (keyword.Equals("update", StringComparison.OrdinalIgnoreCase))
            return "update";
        if (keyword.Equals("delete", StringComparison.OrdinalIgnoreCase))
            return "delete";
        return "other";
    }

    /// <summary>
    ///     Buckets a guild by member count.
    /// </summary>
    /// <param name="memberCount">The member count.</param>
    /// <returns>tiny, small, medium, large or huge.</returns>
    public static string SizeBucket(int memberCount)
    {
        return memberCount switch
        {
            < 50 => "tiny",
            < 250 => "small",
            < 1000 => "medium",
            < 10000 => "large",
            _ => "huge"
        };
    }

    private Task OnShardConnected(DiscordSocketClient shard)
    {
        collector.Gauge("shard.state", 1, ("shard", shard.ShardId.ToString()));
        return Task.CompletedTask;
    }

    private Task OnShardDisconnected(Exception exception, DiscordSocketClient shard)
    {
        var label = shard.ShardId.ToString();
        collector.Gauge("shard.state", 0, ("shard", label));
        collector.Counter("shard.reconnects", 1, ("shard", label));
        return Task.CompletedTask;
    }

    private Task OnShardReady(DiscordSocketClient shard)
    {
        collector.Gauge("shard.state", 1, ("shard", shard.ShardId.ToString()));
        return Task.CompletedTask;
    }

    private Task OnShardLatencyUpdated(int oldLatency, int newLatency, DiscordSocketClient shard)
    {
        collector.Gauge("shard.latency", newLatency, ("shard", shard.ShardId.ToString()));
        return Task.CompletedTask;
    }

    private Task OnJoinedGuild(SocketGuild guild)
    {
        collector.Counter("guild.join", 1, ("shard", client.GetShardIdFor(guild).ToString()),
            ("size", SizeBucket(guild.MemberCount)));
        return Task.CompletedTask;
    }

    private Task OnLeftGuild(SocketGuild guild)
    {
        collector.Counter("guild.leave", 1, ("shard", client.GetShardIdFor(guild).ToString()),
            ("size", SizeBucket(guild.MemberCount)));
        return Task.CompletedTask;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
    {
        if (args.ExceptionObject is Exception exception)
            collector.Error(exception, "AppDomain.UnhandledException");
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs args)
    {
        collector.Error(args.Exception, "TaskScheduler.UnobservedTaskException");
    }

    private void EmitProcess()
    {
        try
        {
            process.Refresh();
            var now = Stopwatch.GetTimestamp();
            var cpu = process.TotalProcessorTime;
            var wall = Stopwatch.GetElapsedTime(lastCpuTimestamp, now).TotalMilliseconds;
            if (wall > 0)
            {
                var percent = (cpu - lastCpuTime).TotalMilliseconds / (wall * Environment.ProcessorCount) * 100;
                collector.Gauge("proc.cpu", Math.Clamp(percent, 0, 100));
            }

            lastCpuTime = cpu;
            lastCpuTimestamp = now;

            collector.Gauge("proc.rss", process.WorkingSet64);
            collector.Gauge("proc.threads", process.Threads.Count);
            collector.Gauge("proc.gc0", GC.CollectionCount(0));
            collector.Gauge("proc.gc1", GC.CollectionCount(1));
            collector.Gauge("proc.gc2", GC.CollectionCount(2));
            collector.Gauge("proc.heap", GC.GetTotalMemory(false));
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Process metrics failed");
        }
    }

    private void EmitFleet()
    {
        try
        {
            var emitUsers = Interlocked.Increment(ref fleetTicks) % UserCountEveryTicks == 1;
            long users = 0;
            foreach (var shard in client.Shards)
            {
                collector.Gauge("guild.count", shard.Guilds.Count, ("shard", shard.ShardId.ToString()));
                if (!emitUsers)
                    continue;
                foreach (var guild in shard.Guilds)
                    users += guild.MemberCount;
            }

            if (emitUsers)
                collector.Gauge("user.count", users);

            if (audioService is null)
                return;

            collector.Gauge("music.players", audioService.Players.Players.Count(), ("node", NodeLabel));
            collector.Gauge("music.node_state", nodeState, ("node", NodeLabel));
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Fleet metrics failed");
        }
    }

    private void AttachRestHooks()
    {
        try
        {
            var apiClientProperty = typeof(BaseDiscordClient).GetProperty("ApiClient",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (apiClientProperty is null)
            {
                logger.LogWarning("REST analytics not attached: ApiClient property not found");
                return;
            }

            var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
            var clients = new List<BaseDiscordClient>
            {
                client, client.Rest
            };
            clients.AddRange(client.Shards);

            foreach (var target in clients)
            {
                if (apiClientProperty.GetValue(target) is not { } apiClient || !seen.Add(apiClient))
                    continue;

                var sentRequest = apiClient.GetType().GetEvent("SentRequest",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                sentRequest?.AddEventHandler(apiClient, new Func<string, string, double, Task>(OnSentRequest));

                var queue = apiClient.GetType().GetProperty("RequestQueue",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(apiClient);
                if (queue is null || !seen.Add(queue))
                    continue;

                var rateLimit = queue.GetType().GetEvent("RateLimitTriggered",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (rateLimit?.EventHandlerType is not { } handlerType)
                    continue;

                var parameters = handlerType.GetMethod("Invoke")!.GetParameters()
                    .Select(p => Expression.Parameter(p.ParameterType, p.Name)).ToArray();
                var method = typeof(AnalyticsInstrumentation).GetMethod(nameof(OnRateLimitTriggered),
                    BindingFlags.Instance | BindingFlags.NonPublic)!;
                var body = Expression.Call(Expression.Constant(this), method,
                    Expression.Convert(parameters[1], typeof(object)), parameters[2]);
                rateLimit.AddEventHandler(queue, Expression.Lambda(handlerType, body, parameters).Compile());
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "REST analytics not attached");
        }
    }

    private Task OnSentRequest(string method, string endpoint, double milliseconds)
    {
        var route = TemplateRoute(endpoint);
        collector.Counter("rest.count", 1, ("method", method), ("route", route), ("status", "2xx"));
        collector.Duration("rest.duration", milliseconds, ("route", route));

        if (endpoint.StartsWith("interactions/", StringComparison.Ordinal))
        {
            var start = "interactions/".Length;
            var end = endpoint.IndexOf('/', start);
            if (end > start && ulong.TryParse(endpoint.AsSpan(start, end - start), out var interactionId))
                ackTracker.Acknowledge(interactionId);
        }

        return Task.CompletedTask;
    }

    private Task OnRateLimitTriggered(object? info, string endpoint)
    {
        var space = endpoint.IndexOf(' ');
        var route = TemplateRoute(space >= 0 ? endpoint[(space + 1)..] : endpoint);
        var global = info is IRateLimitInfo { IsGlobal: true } ? "1" : "0";
        collector.Counter("ratelimit.hit", 1, ("route", route), ("global", global));
        return Task.CompletedTask;
    }

    private void AttachDatabaseTracing()
    {
        if (dbFactory is PostgreSqlConnectionFactory factory)
            factory.UseTracing(OnDatabaseTrace);
        else
            logger.LogWarning("Database analytics not attached: {Factory} does not support tracing",
                dbFactory.GetType().Name);
    }

    private void OnDatabaseTrace(TraceInfo info)
    {
        switch (info.TraceInfoStep)
        {
            case TraceInfoStep.AfterExecute when info.ExecutionTime is { } elapsed:
                collector.Duration("db.duration", elapsed.TotalMilliseconds, ("op", OperationOf(info.CommandText)));
                break;
            case TraceInfoStep.Error:
                collector.Counter("db.errors", 1, ("op", OperationOf(info.CommandText)));
                break;
        }
    }

    private void AttachMusicHooks()
    {
        audioService = services.GetService<IAudioService>();
        if (audioService is null)
            return;

        audioService.TrackStarted += OnTrackStarted;
        audioService.TrackEnded += OnTrackEnded;
        audioService.TrackException += OnTrackException;
        audioService.TrackStuck += OnTrackStuck;
        audioService.ConnectionReady += OnConnectionReady;
        audioService.ConnectionClosed += OnConnectionClosed;
    }

    private Task OnTrackStarted(object sender, TrackStartedEventArgs args)
    {
        collector.Counter("music.track_start", 1, ("node", NodeLabel),
            ("source", args.Track.SourceName ?? "unknown"));
        return Task.CompletedTask;
    }

    private Task OnTrackEnded(object sender, TrackEndedEventArgs args)
    {
        collector.Counter("music.track_end", 1, ("node", NodeLabel),
            ("reason", args.Reason.ToString().ToLowerInvariant()));
        return Task.CompletedTask;
    }

    private Task OnTrackException(object sender, TrackExceptionEventArgs args)
    {
        collector.Counter("music.errors", 1, ("node", NodeLabel), ("kind", "exception"));
        return Task.CompletedTask;
    }

    private Task OnTrackStuck(object sender, TrackStuckEventArgs args)
    {
        collector.Counter("music.errors", 1, ("node", NodeLabel), ("kind", "stuck"));
        return Task.CompletedTask;
    }

    private Task OnConnectionReady(object sender, ConnectionReadyEventArgs args)
    {
        nodeState = 1;
        collector.Gauge("music.node_state", 1, ("node", NodeLabel));
        return Task.CompletedTask;
    }

    private Task OnConnectionClosed(object sender, ConnectionClosedEventArgs args)
    {
        nodeState = 0;
        collector.Gauge("music.node_state", 0, ("node", NodeLabel));
        return Task.CompletedTask;
    }
}