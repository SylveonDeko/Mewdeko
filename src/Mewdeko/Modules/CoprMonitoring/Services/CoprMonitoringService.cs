using System.IO;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Modules.CoprMonitoring.Common;
using Mewdeko.Services.Settings;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using AmqpConnection = RabbitMQ.Client.IConnection;
using AmqpChannel = RabbitMQ.Client.IChannel;

namespace Mewdeko.Modules.CoprMonitoring.Services;

/// <summary>
///     Service for monitoring COPR builds via Fedora Messaging and posting notifications to Discord.
/// </summary>
public class CoprMonitoringService : INService, IReadyExecutor, IDisposable
{
    private readonly DiscordShardedClient client;
    private readonly BotConfigService config;
    private readonly IDataConnectionFactory dbFactory;
    private readonly ILogger<CoprMonitoringService> logger;

    // In-memory cache: Key = (owner, project), Value = List of monitors for that project
    private readonly ConcurrentDictionary<(string Owner, string Project), List<CoprMonitor>> monitors = new();
    private readonly Lock monitorsLock = new();
    private AmqpChannel? amqpChannel;

    private AmqpConnection? amqpConnection;
    private bool isDisposed;
    private bool isInitialized;

    /// <summary>
    ///     Initializes a new instance of the <see cref="CoprMonitoringService" /> class.
    /// </summary>
    /// <param name="dbFactory">The database factory.</param>
    /// <param name="client">The Discord sharded client.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    /// <param name="config">The bot configuration service.</param>
    public CoprMonitoringService(
        IDataConnectionFactory dbFactory,
        DiscordShardedClient client,
        ILogger<CoprMonitoringService> logger,
        BotConfigService config)
    {
        this.dbFactory = dbFactory;
        this.client = client;
        this.logger = logger;
        this.config = config;
    }

    /// <summary>
    ///     Releases all resources used by the <see cref="CoprMonitoringService" />.
    /// </summary>
    public void Dispose()
    {
        if (isDisposed)
            return;

        isDisposed = true;

        try
        {
            if (amqpChannel != null)
            {
                amqpChannel.CloseAsync().GetAwaiter().GetResult();
                amqpChannel.Dispose();
            }

            if (amqpConnection != null)
            {
                amqpConnection.CloseAsync().GetAwaiter().GetResult();
                amqpConnection.Dispose();
            }

            logger.LogInformation("COPR Monitoring Service disposed");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error disposing COPR Monitoring Service");
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Handles initialization when the bot is ready.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task OnReadyAsync()
    {
        if (isInitialized)
            return;

        logger.LogInformation("Initializing COPR Monitoring Service");

        try
        {
            // Load all monitors from database into memory
            await LoadMonitorsFromDatabase();

            // Connect to Fedora Messaging
            await ConnectToFedoraMessaging();

            isInitialized = true;
            lock (monitorsLock)
            {
                logger.LogInformation("COPR Monitoring Service initialized successfully with {Count} monitors",
                    monitors.Sum(x => x.Value.Count));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize COPR Monitoring Service");
        }
    }

    /// <summary>
    ///     Loads all COPR monitors from the database into memory.
    /// </summary>
    private async Task LoadMonitorsFromDatabase()
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var allMonitors = await db.CoprMonitors.ToListAsync();

        lock (monitorsLock)
        {
            monitors.Clear();

            foreach (var monitor in allMonitors)
            {
                var key = (monitor.CoprOwner, monitor.CoprProject);

                if (!monitors.ContainsKey(key))
                    monitors[key] = new List<CoprMonitor>();

                monitors[key].Add(monitor);
            }
        }

        logger.LogInformation("Loaded {Count} COPR monitors from database", allMonitors.Count);
    }

    /// <summary>
    ///     Adds a new COPR monitor to the database and in-memory cache.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="channelId">The ID of the channel where notifications will be posted.</param>
    /// <param name="owner">The COPR project owner.</param>
    /// <param name="project">The COPR project name.</param>
    /// <param name="packageFilter">Optional comma-separated list of packages to monitor.</param>
    /// <returns>The created monitor, or null if it already exists.</returns>
    public async Task<CoprMonitor?> AddMonitor(ulong guildId, ulong channelId, string owner, string project,
        string? packageFilter = null)
    {
        var key = (owner, project);

        // Check in-memory cache first
        lock (monitorsLock)
        {
            if (monitors.TryGetValue(key, out var existingMonitors))
            {
                var duplicate = existingMonitors.FirstOrDefault(x =>
                    x.GuildId == guildId &&
                    x.ChannelId == channelId &&
                    x.PackageFilter == packageFilter);

                if (duplicate != null)
                    return null;
            }
        }

        var monitor = new CoprMonitor
        {
            GuildId = guildId,
            ChannelId = channelId,
            CoprOwner = owner,
            CoprProject = project,
            PackageFilter = packageFilter,
            IsEnabled = true,
            NotifyOnSucceeded = true,
            NotifyOnFailed = true,
            NotifyOnCanceled = false,
            NotifyOnPending = false,
            NotifyOnRunning = false,
            NotifyOnSkipped = false,
            DateAdded = DateTime.UtcNow
        };

        // Write to database
        await using var db = await dbFactory.CreateConnectionAsync();
        monitor.Id = await db.InsertWithInt32IdentityAsync(monitor);

        // Update in-memory cache
        lock (monitorsLock)
        {
            if (!monitors.ContainsKey(key))
                monitors[key] = new List<CoprMonitor>();

            monitors[key].Add(monitor);
        }

        return monitor;
    }

    /// <summary>
    ///     Removes a COPR monitor from the database and in-memory cache.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="owner">The COPR project owner.</param>
    /// <param name="project">The COPR project name.</param>
    /// <param name="channelId">Optional channel ID to remove a specific monitor.</param>
    /// <returns>True if monitor was removed, false otherwise.</returns>
    public async Task<bool> RemoveMonitor(ulong guildId, string owner, string project, ulong? channelId = null)
    {
        var key = (owner, project);
        List<CoprMonitor> toRemove;

        // Find monitors to remove from in-memory cache
        lock (monitorsLock)
        {
            if (!monitors.TryGetValue(key, out var projectMonitors))
                return false;

            toRemove = projectMonitors
                .Where(x => x.GuildId == guildId &&
                            (!channelId.HasValue || x.ChannelId == channelId.Value))
                .ToList();

            if (toRemove.Count == 0)
                return false;
        }

        // Delete from database
        await using var db = await dbFactory.CreateConnectionAsync();
        foreach (var monitor in toRemove)
        {
            await db.CoprMonitors
                .Where(x => x.Id == monitor.Id)
                .DeleteAsync();
        }

        // Update in-memory cache
        lock (monitorsLock)
        {
            if (monitors.TryGetValue(key, out var projectMonitors))
            {
                foreach (var monitor in toRemove)
                {
                    projectMonitors.Remove(monitor);
                }

                if (projectMonitors.Count == 0)
                    monitors.TryRemove(key, out _);
            }
        }

        return true;
    }

    /// <summary>
    ///     Gets all monitors for a guild from in-memory cache.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <returns>A list of COPR monitors.</returns>
    public Task<List<CoprMonitor>> GetMonitors(ulong guildId)
    {
        lock (monitorsLock)
        {
            var result = monitors.Values
                .SelectMany(list => list)
                .Where(x => x.GuildId == guildId)
                .ToList();

            return Task.FromResult(result);
        }
    }

    /// <summary>
    ///     Gets a specific monitor from in-memory cache.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="owner">The COPR project owner.</param>
    /// <param name="project">The COPR project name.</param>
    /// <param name="channelId">Optional channel ID.</param>
    /// <returns>The monitor if found, null otherwise.</returns>
    public Task<CoprMonitor?> GetMonitor(ulong guildId, string owner, string project, ulong? channelId = null)
    {
        var key = (owner, project);

        lock (monitorsLock)
        {
            if (!monitors.TryGetValue(key, out var projectMonitors))
                return Task.FromResult<CoprMonitor?>(null);

            var monitor = projectMonitors.FirstOrDefault(x =>
                x.GuildId == guildId &&
                (!channelId.HasValue || x.ChannelId == channelId.Value));

            return Task.FromResult(monitor);
        }
    }

    /// <summary>
    ///     Toggles a monitor's enabled state.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="owner">The COPR project owner.</param>
    /// <param name="project">The COPR project name.</param>
    /// <returns>The new enabled state, or null if monitor not found.</returns>
    public async Task<bool?> ToggleMonitor(ulong guildId, string owner, string project)
    {
        var key = (owner, project);
        CoprMonitor? monitor;

        // Find monitor in cache
        lock (monitorsLock)
        {
            if (!monitors.TryGetValue(key, out var projectMonitors))
                return null;

            monitor = projectMonitors.FirstOrDefault(x => x.GuildId == guildId);
            if (monitor == null)
                return null;

            monitor.IsEnabled = !monitor.IsEnabled;
        }

        // Update database
        await using var db = await dbFactory.CreateConnectionAsync();
        await db.UpdateAsync(monitor);

        return monitor.IsEnabled;
    }

    /// <summary>
    ///     Sets the notification preference for a specific build status.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="owner">The COPR project owner.</param>
    /// <param name="project">The COPR project name.</param>
    /// <param name="status">The build status to configure.</param>
    /// <param name="enabled">Whether to enable notifications for this status.</param>
    /// <returns>True if updated successfully, false otherwise.</returns>
    public async Task<bool> SetStatusNotification(ulong guildId, string owner, string project, CoprBuildStatus status,
        bool enabled)
    {
        var key = (owner, project);
        CoprMonitor? monitor;

        // Find and update in cache
        lock (monitorsLock)
        {
            if (!monitors.TryGetValue(key, out var projectMonitors))
                return false;

            monitor = projectMonitors.FirstOrDefault(x => x.GuildId == guildId);
            if (monitor == null)
                return false;

            switch (status)
            {
                case CoprBuildStatus.Succeeded:
                    monitor.NotifyOnSucceeded = enabled;
                    break;
                case CoprBuildStatus.Failed:
                    monitor.NotifyOnFailed = enabled;
                    break;
                case CoprBuildStatus.Canceled:
                    monitor.NotifyOnCanceled = enabled;
                    break;
                case CoprBuildStatus.Pending:
                    monitor.NotifyOnPending = enabled;
                    break;
                case CoprBuildStatus.Running:
                    monitor.NotifyOnRunning = enabled;
                    break;
                case CoprBuildStatus.Skipped:
                    monitor.NotifyOnSkipped = enabled;
                    break;
                default:
                    return false;
            }
        }

        // Update database
        await using var db = await dbFactory.CreateConnectionAsync();
        await db.UpdateAsync(monitor);

        return true;
    }

    /// <summary>
    ///     Sets the custom message for a specific build status.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="owner">The COPR project owner.</param>
    /// <param name="project">The COPR project name.</param>
    /// <param name="status">The build status to set the message for.</param>
    /// <param name="message">The custom message or embed code.</param>
    /// <returns>True if updated successfully, false otherwise.</returns>
    public async Task<bool> SetStatusMessage(ulong guildId, string owner, string project, CoprBuildStatus status,
        string message)
    {
        var key = (owner, project);
        CoprMonitor? monitor;

        // Find and update in cache
        lock (monitorsLock)
        {
            if (!monitors.TryGetValue(key, out var projectMonitors))
                return false;

            monitor = projectMonitors.FirstOrDefault(x => x.GuildId == guildId);
            if (monitor == null)
                return false;

            switch (status)
            {
                case CoprBuildStatus.Succeeded:
                    monitor.SucceededMessage = message;
                    break;
                case CoprBuildStatus.Failed:
                    monitor.FailedMessage = message;
                    break;
                case CoprBuildStatus.Canceled:
                    monitor.CanceledMessage = message;
                    break;
                case CoprBuildStatus.Pending:
                    monitor.PendingMessage = message;
                    break;
                case CoprBuildStatus.Running:
                    monitor.RunningMessage = message;
                    break;
                case CoprBuildStatus.Skipped:
                    monitor.SkippedMessage = message;
                    break;
                default:
                    monitor.DefaultMessage = message;
                    break;
            }
        }

        // Update database
        await using var db = await dbFactory.CreateConnectionAsync();
        await db.UpdateAsync(monitor);

        return true;
    }

    /// <summary>
    ///     Sets the package filter for a monitor.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="owner">The COPR project owner.</param>
    /// <param name="project">The COPR project name.</param>
    /// <param name="packages">Comma-separated list of packages, or null for all.</param>
    /// <returns>True if updated successfully, false otherwise.</returns>
    public async Task<bool> SetPackageFilter(ulong guildId, string owner, string project, string? packages)
    {
        var key = (owner, project);
        CoprMonitor? monitor;

        // Find and update in cache
        lock (monitorsLock)
        {
            if (!monitors.TryGetValue(key, out var projectMonitors))
                return false;

            monitor = projectMonitors.FirstOrDefault(x => x.GuildId == guildId);
            if (monitor == null)
                return false;

            monitor.PackageFilter = packages;
        }

        // Update database
        await using var db = await dbFactory.CreateConnectionAsync();
        await db.UpdateAsync(monitor);
        return true;
    }

    /// <summary>
    ///     Sets the default message for a monitor.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="owner">The COPR project owner.</param>
    /// <param name="project">The COPR project name.</param>
    /// <param name="message">The default message or embed code.</param>
    /// <returns>True if updated successfully, false otherwise.</returns>
    public async Task<bool> SetDefaultMessage(ulong guildId, string owner, string project, string message)
    {
        var key = (owner, project);
        CoprMonitor? monitor;

        // Find and update in cache
        lock (monitorsLock)
        {
            if (!monitors.TryGetValue(key, out var projectMonitors))
                return false;

            monitor = projectMonitors.FirstOrDefault(x => x.GuildId == guildId);
            if (monitor == null)
                return false;

            monitor.DefaultMessage = message;
        }

        // Update database
        await using var db = await dbFactory.CreateConnectionAsync();
        await db.UpdateAsync(monitor);
        return true;
    }

    /// <summary>
    ///     Connects to Fedora Messaging AMQP broker and subscribes to COPR build events.
    /// </summary>
    private async Task ConnectToFedoraMessaging()
    {
        try
        {
            // Certificate paths - these files should be in the certs directory
            // Download from: https://github.com/fedora-infra/fedora-messaging/tree/develop/configs
            const string caCertPath = "certs/cacert.pem";
            const string clientCertPath = "certs/fedora-cert.pem";
            const string clientKeyPath = "certs/fedora-key.pem";

            // Check if certificate files exist
            if (!File.Exists(caCertPath))
            {
                logger.LogError(
                    "Fedora CA certificate not found at {Path}. Download from: https://github.com/fedora-infra/fedora-messaging/tree/develop/configs",
                    caCertPath);
                return;
            }

            if (!File.Exists(clientCertPath))
            {
                logger.LogError(
                    "Fedora client certificate not found at {Path}. Download from: https://github.com/fedora-infra/fedora-messaging/tree/develop/configs",
                    clientCertPath);
                return;
            }

            if (!File.Exists(clientKeyPath))
            {
                logger.LogError(
                    "Fedora client key not found at {Path}. Download from: https://github.com/fedora-infra/fedora-messaging/tree/develop/configs",
                    clientKeyPath);
                return;
            }

            // Load the client certificate + private key for mutual TLS authentication
            var clientCert = X509Certificate2.CreateFromPemFile(clientCertPath, clientKeyPath);

            // Create a collection for the client certificate
            var clientCerts = new X509CertificateCollection
            {
                clientCert
            };

            // Load the Fedora CA certificate to validate the server
            var caCertBytes = await File.ReadAllBytesAsync(caCertPath);
            var caCert = X509CertificateLoader.LoadCertificate(caCertBytes);

            var factory = new ConnectionFactory
            {
                HostName = "rabbitmq.fedoraproject.org",
                Port = 5671,
                VirtualHost = "/public_pubsub",

                // Use EXTERNAL mechanism for certificate-based authentication
                AuthMechanisms = new List<IAuthMechanismFactory>
                {
                    new ExternalMechanismFactory()
                },
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
                RequestedHeartbeat = TimeSpan.FromSeconds(60),
                Ssl = new SslOption
                {
                    Enabled = true,
                    ServerName = "rabbitmq.fedoraproject.org",
                    Version = SslProtocols.Tls12,

                    // Provide the client certificate for mutual TLS
                    Certs = clientCerts,

                    // Custom validation callback to trust Fedora CA
                    CertificateValidationCallback = (_, serverCertificate, _, sslPolicyErrors) =>
                    {
                        if (sslPolicyErrors == SslPolicyErrors.None)
                            return true;

                        if (serverCertificate is null)
                            return false;

                        try
                        {
                            var customChain = new X509Chain();
                            customChain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                            customChain.ChainPolicy.CustomTrustStore.Add(caCert);
                            customChain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;

                            var serverCert2 = new X509Certificate2(serverCertificate);
                            return customChain.Build(serverCert2);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Failed to build custom certificate chain for Fedora Messaging");

                            return false;
                        }
                    }
                }
            };

            amqpConnection = await factory.CreateConnectionAsync("Mewdeko-COPR-Monitor");
            amqpChannel = await amqpConnection.CreateChannelAsync();

            var queueDeclareResult = await amqpChannel.QueueDeclareAsync(
                "",
                false,
                true,
                true,
                null);
            var queueName = queueDeclareResult.QueueName;

            await amqpChannel.QueueBindAsync(
                queueName,
                "amq.topic",
                "org.fedoraproject.prod.copr.build.end");

            var consumer = new AsyncEventingBasicConsumer(amqpChannel);
            consumer.ReceivedAsync += async (_, ea) =>
            {
                try
                {
                    await ProcessCoprMessage(ea.Body.ToArray());
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error processing COPR message");
                }
            };

            await amqpChannel.BasicConsumeAsync(queueName, true, consumer);

            logger.LogInformation("Connected to Fedora Messaging and subscribed to COPR build events");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to connect to Fedora Messaging - COPR monitoring will be unavailable");
            // Don't throw - allow bot to continue running even if Fedora Messaging is unavailable
        }
    }

    /// <summary>
    ///     Processes an incoming COPR build message from Fedora Messaging.
    /// </summary>
    /// <param name="messageBody">The raw message body.</param>
    private async Task ProcessCoprMessage(byte[] messageBody)
    {
        try
        {
            var json = Encoding.UTF8.GetString(messageBody);
            var message = JsonSerializer.Deserialize<FedoraMessage>(json);

            if (message?.Body == null)
                return;

            var buildData = message.Body;
            var status = CoprExtensions.ParseStatus(buildData.Status);

            logger.LogDebug("Received COPR build event: {Owner}/{Project} - {Package} - {Status}",
                buildData.Owner, buildData.Project, buildData.Package, buildData.Status);

            var key = (buildData.Owner, buildData.Project);
            List<CoprMonitor> matchingMonitors;

            // Get matching monitors from in-memory cache
            lock (monitorsLock)
            {
                if (!monitors.TryGetValue(key, out var projectMonitors))
                    return;

                matchingMonitors = projectMonitors
                    .Where(x => x.IsEnabled)
                    .ToList();
            }

            foreach (var monitor in matchingMonitors)
            {
                if (!ShouldNotify(monitor, status, buildData.Package))
                    continue;

                await SendNotification(monitor, buildData, status);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing COPR message");
        }
    }

    /// <summary>
    ///     Determines if a notification should be sent for this build.
    /// </summary>
    /// <param name="monitor">The monitor configuration.</param>
    /// <param name="status">The build status.</param>
    /// <param name="packageName">The package name.</param>
    /// <returns>True if notification should be sent, false otherwise.</returns>
    private bool ShouldNotify(CoprMonitor monitor, CoprBuildStatus status, string packageName)
    {
        // Check package filter
        if (!string.IsNullOrWhiteSpace(monitor.PackageFilter))
        {
            var packages = monitor.PackageFilter.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim());

            if (!packages.Contains(packageName, StringComparer.OrdinalIgnoreCase))
                return false;
        }

        // Check status filter
        return status switch
        {
            CoprBuildStatus.Succeeded => monitor.NotifyOnSucceeded,
            CoprBuildStatus.Failed => monitor.NotifyOnFailed,
            CoprBuildStatus.Canceled => monitor.NotifyOnCanceled,
            CoprBuildStatus.Pending => monitor.NotifyOnPending,
            CoprBuildStatus.Running => monitor.NotifyOnRunning,
            CoprBuildStatus.Skipped => monitor.NotifyOnSkipped,
            _ => false
        };
    }

    /// <summary>
    ///     Sends a Discord notification for a COPR build.
    /// </summary>
    /// <param name="monitor">The monitor configuration.</param>
    /// <param name="buildData">The build data from COPR.</param>
    /// <param name="status">The parsed build status.</param>
    private async Task SendNotification(CoprMonitor monitor, CoprBuildMessage buildData, CoprBuildStatus status)
    {
        try
        {
            var guild = client.GetGuild(monitor.GuildId);
            if (guild == null)
            {
                logger.LogWarning("Guild {GuildId} not found for COPR notification", monitor.GuildId);
                return;
            }

            var channel = guild.GetTextChannel(monitor.ChannelId);
            if (channel == null)
            {
                logger.LogWarning("Channel {ChannelId} not found for COPR notification", monitor.ChannelId);
                return;
            }

            var customMessage = GetCustomMessageForStatus(monitor, status);

            if (string.IsNullOrWhiteSpace(customMessage) || customMessage == "-")
            {
                // Use default message
                await SendDefaultNotification(channel, buildData, status);
            }
            else
            {
                // Use custom message with replacements
                await SendCustomNotification(channel, buildData, status, guild, customMessage);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending COPR notification");
        }
    }

    /// <summary>
    ///     Gets the custom message for the given status.
    /// </summary>
    /// <param name="monitor">The monitor configuration.</param>
    /// <param name="status">The build status.</param>
    /// <returns>The custom message, or null if not set.</returns>
    private string? GetCustomMessageForStatus(CoprMonitor monitor, CoprBuildStatus status)
    {
        return status switch
        {
            CoprBuildStatus.Succeeded => monitor.SucceededMessage,
            CoprBuildStatus.Failed => monitor.FailedMessage,
            CoprBuildStatus.Canceled => monitor.CanceledMessage,
            CoprBuildStatus.Pending => monitor.PendingMessage,
            CoprBuildStatus.Running => monitor.RunningMessage,
            CoprBuildStatus.Skipped => monitor.SkippedMessage,
            _ => monitor.DefaultMessage
        };
    }

    /// <summary>
    ///     Sends a default-formatted build notification.
    /// </summary>
    /// <param name="channel">The channel to send to.</param>
    /// <param name="buildData">The build data.</param>
    /// <param name="status">The build status.</param>
    private async Task SendDefaultNotification(ITextChannel channel, CoprBuildMessage buildData, CoprBuildStatus status)
    {
        var emote = GetEmoteForStatus(status);
        var statusText = status.ToDisplayString();
        var buildUrl =
            $"https://copr.fedorainfracloud.org/coprs/{buildData.Owner}/{buildData.Project}/build/{buildData.Build}/";

        var embed = new EmbedBuilder()
            .WithTitle($"{emote} COPR Build {statusText}")
            .WithDescription($"**Project:** {buildData.Owner}/{buildData.Project}\n" +
                             $"**Package:** {buildData.Package}\n" +
                             $"**Build ID:** {buildData.Build}\n" +
                             $"**Chroot:** {buildData.Chroot}")
            .WithUrl(buildUrl)
            .WithColor(GetColorForStatus(status))
            .WithTimestamp(DateTimeOffset.UtcNow)
            .Build();

        var components = config.Data.ShowInviteButton
            ? new ComponentBuilder()
                .WithButton(style: ButtonStyle.Link, url: buildUrl, label: "View Build")
                .Build()
            : null;

        await channel.SendMessageAsync(embed: embed, components: components);
    }

    /// <summary>
    ///     Sends a custom-formatted build notification.
    /// </summary>
    /// <param name="channel">The channel to send to.</param>
    /// <param name="buildData">The build data.</param>
    /// <param name="status">The build status.</param>
    /// <param name="guild">The guild.</param>
    /// <param name="customMessage">The custom message template.</param>
    private async Task SendCustomNotification(ITextChannel channel, CoprBuildMessage buildData, CoprBuildStatus status,
        IGuild guild, string customMessage)
    {
        var buildUrl =
            $"https://copr.fedorainfracloud.org/coprs/{buildData.Owner}/{buildData.Project}/build/{buildData.Build}/";

        var replacer = new ReplacementBuilder()
            .WithServer(client, guild as SocketGuild)
            .WithOverride("%copr.owner%", () => buildData.Owner)
            .WithOverride("%copr.project%", () => buildData.Project)
            .WithOverride("%copr.package%", () => buildData.Package)
            .WithOverride("%copr.buildid%", () => buildData.Build.ToString())
            .WithOverride("%copr.status%", () => status.ToDisplayString())
            .WithOverride("%copr.chroot%", () => buildData.Chroot)
            .WithOverride("%copr.url%", () => buildUrl)
            .WithOverride("%copr.version%", () => buildData.Version ?? "N/A")
            .WithOverride("%copr.user%", () => buildData.User ?? "N/A")
            .WithOverride("%copr.emote%", () => GetEmoteForStatus(status))
            .Build();

        var content = replacer.Replace(customMessage);

        if (SmartEmbed.TryParse(content, guild.Id, out var embeds, out var plainText, out var components))
        {
            await channel.SendMessageAsync(plainText, embeds: embeds, components: components?.Build());
        }
        else
        {
            await channel.SendMessageAsync(content.SanitizeMentions(true));
        }
    }

    /// <summary>
    ///     Gets the appropriate emote for the build status.
    /// </summary>
    /// <param name="status">The build status.</param>
    /// <returns>The emote string.</returns>
    private string GetEmoteForStatus(CoprBuildStatus status)
    {
        return status switch
        {
            CoprBuildStatus.Succeeded => config.Data.SuccessEmote,
            CoprBuildStatus.Failed => config.Data.ErrorEmote,
            CoprBuildStatus.Canceled => "⚠️",
            CoprBuildStatus.Running => config.Data.LoadingEmote,
            _ => "ℹ️"
        };
    }

    /// <summary>
    ///     Gets the appropriate color for the build status.
    /// </summary>
    /// <param name="status">The build status.</param>
    /// <returns>The Discord color.</returns>
    private Color GetColorForStatus(CoprBuildStatus status)
    {
        return status switch
        {
            CoprBuildStatus.Succeeded => Mewdeko.OkColor,
            CoprBuildStatus.Failed => Mewdeko.ErrorColor,
            CoprBuildStatus.Canceled => Color.Orange,
            CoprBuildStatus.Running => Color.Blue,
            _ => Color.LightGrey
        };
    }
}