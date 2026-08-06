using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Controllers.Common.Bot;
using Mewdeko.Services.Impl;

namespace Mewdeko.Modules.OwnerOnly.Services;

/// <summary>
///     Service responsible for managing and monitoring bot instances running on the local machine.
/// </summary>
public class InstanceManagementService : INService, IReadyExecutor
{
    // Cached JsonSerializerOptions for performance
    private static readonly JsonSerializerOptions CachedJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly string apiKey;
    private readonly DiscordShardedClient client;
    private readonly IDataConnectionFactory dbFactory;
    private readonly IHttpClientFactory factory;
    private readonly ILogger<InstanceManagementService> logger;

    /// <summary>
    ///     Initializes a new instance of the BotInstanceService.
    /// </summary>
    /// <param name="dbFactory">The database context provider.</param>
    /// <param name="factory">The HTTP client factory.</param>
    /// <param name="client">The sharded discord client</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    public InstanceManagementService(
        IDataConnectionFactory dbFactory,
        IHttpClientFactory factory, DiscordShardedClient client, ILogger<InstanceManagementService> logger)
    {
        var creds = new BotCredentials();
        apiKey = creds.ApiKey;
        this.dbFactory = dbFactory;
        this.factory = factory;
        this.client = client;
        this.logger = logger;
    }

    /// <summary>
    ///     Called when bot is ready. Registers itself if master instance.
    /// </summary>
    public async Task OnReadyAsync()
    {
        var creds = new BotCredentials();

        // If master instance, make sure we're registered
        if (creds.IsMasterInstance)
        {
            try
            {
                await using var db = await dbFactory.CreateConnectionAsync();
                var existing = await db.BotInstances.FirstOrDefaultAsync(x => x.Port == creds.ApiPort);

                if (existing is null)
                {
                    logger.LogInformation("Registering self as master instance on {Host}:{Port}",
                        creds.InstanceApiHost, creds.ApiPort);

                    await db.InsertAsync(new BotInstance
                    {
                        Port = creds.ApiPort,
                        Host = creds.InstanceApiHost,
                        BotId = client.CurrentUser.Id,
                        BotName = client.CurrentUser.Username,
                        BotAvatar = client.CurrentUser.GetAvatarUrl(),
                        IsActive = true,
                        LastStatusUpdate = DateTime.UtcNow
                    });
                }
                else
                {
                    existing.BotId = client.CurrentUser.Id;
                    existing.BotName = client.CurrentUser.Username;
                    existing.BotAvatar = client.CurrentUser.GetAvatarUrl() ?? "";
                    existing.Host = creds.InstanceApiHost;
                    existing.IsActive = true;
                    existing.LastStatusUpdate = DateTime.UtcNow;
                    await db.UpdateAsync(existing);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to register self as instance");
            }

            var periodic = new PeriodicTimer(TimeSpan.FromMinutes(1));
            do
            {
                await MonitorInstancesAsync();
            } while (await periodic.WaitForNextTickAsync());
        }
        else
        {
            logger.LogInformation("Not registering self as instance. Not marked as master instance");
        }
    }

    private HttpClient CreateAuthenticatedClient()
    {
        var httpClient = factory.CreateClient();
        httpClient.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        return httpClient;
    }

    /// <summary>
    ///     Looks up the hostname a registered instance is reachable on, falling back to "localhost"
    ///     for instances registered before hosts were tracked.
    /// </summary>
    /// <param name="port">The port number of the instance.</param>
    /// <returns>The hostname to use when calling the instance.</returns>
    private async Task<string> ResolveHostAsync(int port)
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var instance = await db.BotInstances.FirstOrDefaultAsync(x => x.Port == port);
        return string.IsNullOrWhiteSpace(instance?.Host) ? "localhost" : instance.Host;
    }

    /// <summary>
    ///     Registers a new bot instance with the specified port number.
    /// </summary>
    /// <param name="port">The TCP port number the bot instance is listening on.</param>
    /// <param name="host">
    ///     The hostname the instance is reachable on. Defaults to "localhost" for instances running
    ///     alongside this one.
    /// </param>
    /// <returns>
    ///     A tuple containing:
    ///     - Success: Whether the registration was successful
    ///     - Status: The bot's status information if successful, null otherwise
    ///     - Reason: The reason for failure if not successful, null otherwise
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when port number is invalid.</exception>
    /// <exception cref="InvalidOperationException">Thrown when not running on master instance.</exception>
    public async Task<(bool Success, BotStatusModel? Status, string? Reason)> AddInstanceAsync(int port,
        string? host = null)
    {
        if (!new BotCredentials().IsMasterInstance)
            throw new InvalidOperationException("Can only add instances from master bot.");

        if (port is < 1024 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(port), "Port must be between 1024 and 65535");

        var resolvedHost = string.IsNullOrWhiteSpace(host) ? "localhost" : host;

        await using var db = await dbFactory.CreateConnectionAsync();
        if (await db.BotInstances.AnyAsync(x => x.Port == port))
            return (false, null, "instance_already_exists");

        var status = await GetInstanceStatusAsync(port, resolvedHost);
        if (status == null)
            return (false, null, "instance_not_responding");

        await db.InsertAsync(new BotInstance
        {
            Port = port,
            Host = resolvedHost,
            BotId = status.BotId,
            BotName = status.BotName,
            BotAvatar = status.BotAvatar,
            IsActive = true,
            LastStatusUpdate = DateTime.UtcNow
        });

        return (true, status, null);
    }

    /// <summary>
    ///     Retrieves the current status of a bot instance.
    /// </summary>
    /// <param name="port">The port number of the bot instance.</param>
    /// <param name="host">
    ///     The hostname of the bot instance. When null, the host stored for that port is used, falling
    ///     back to "localhost".
    /// </param>
    /// <returns>The bot's status information if available, null if the instance is unreachable.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when port number is invalid.</exception>
    public async Task<BotStatusModel?> GetInstanceStatusAsync(int port, string? host = null)
    {
        if (port is < 1024 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(port), "Port must be between 1024 and 65535");

        var resolvedHost = string.IsNullOrWhiteSpace(host) ? await ResolveHostAsync(port) : host;

        try
        {
            using var httpClient = CreateAuthenticatedClient();
            var response = await httpClient.GetAsync($"http://{resolvedHost}:{port}/botapi/BotStatus");

            if (!response.IsSuccessStatusCode)
                return null;

            var actuResponse = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<BotStatusModel>(actuResponse, CachedJsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get status for instance on port {Port}", port);
            return null;
        }
    }

    /// <summary>
    ///     Continuously monitors the health of all registered bot instances.
    ///     Updates their active status and last status update timestamp.
    /// </summary>
    /// <returns>A task that completes when monitoring is stopped.</returns>
    private async Task MonitorInstancesAsync()
    {
        try
        {
            await using var db = await dbFactory.CreateConnectionAsync();

            // Actually retrieve the instances from the database
            var instances = await db.BotInstances.ToListAsync();

            foreach (var instance in instances)
            {
                var status = await GetInstanceStatusAsync(instance.Port, instance.Host);
                instance.IsActive = status != null;
                instance.LastStatusUpdate = DateTime.UtcNow;

                // Update each instance individually
                await db.UpdateAsync(instance);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during instance monitoring");
        }
    }

    /// <summary>
    ///     Gets all active bot instances.
    /// </summary>
    /// <returns>A list of active bot instances.</returns>
    public async Task<List<BotInstance>> GetActiveInstancesAsync()
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        return await db.BotInstances
            .Where(x => x.IsActive)
            .OrderBy(x => x.Port)
            .ToListAsync();
    }

    /// <summary>
    ///     Removes a bot instance from the registry.
    /// </summary>
    /// <param name="port">The port number of the instance to remove.</param>
    /// <returns>True if the instance was removed, false if it wasn't found.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when port number is invalid.</exception>
    /// <exception cref="InvalidOperationException">Thrown when not running on master instance.</exception>
    public async Task<bool> RemoveInstanceAsync(int port)
    {
        if (!new BotCredentials().IsMasterInstance)
            throw new InvalidOperationException("Can only remove instances from master bot.");

        if (port is < 1024 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(port), "Port must be between 1024 and 65535");

        await using var db = await dbFactory.CreateConnectionAsync();
        var instance = await db.BotInstances.FirstOrDefaultAsync(x => x.Port == port);

        if (instance == null)
            return false;

        await db.DeleteAsync(instance);
        return true;
    }

    /// <summary>
    ///     Executes an update on this instance (git pull + restart).
    ///     This method is called when this instance receives an update request.
    /// </summary>
    public async Task ExecuteUpdateAsync()
    {
        logger.LogInformation("Starting update process for this instance");

        try
        {
            // Execute git pull in a separate task to not block the API response
            await Task.Run(async () =>
            {
                // Wait a bit to allow the API response to be sent
                await Task.Delay(1000);

                var gitPullInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "pull",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var gitPullProcess = Process.Start(gitPullInfo);
                if (gitPullProcess != null)
                {
                    await gitPullProcess.WaitForExitAsync();
                    var output = await gitPullProcess.StandardOutput.ReadToEndAsync();
                    var error = await gitPullProcess.StandardError.ReadToEndAsync();

                    if (gitPullProcess.ExitCode == 0)
                    {
                        logger.LogInformation("Git pull successful: {Output}", output);

                        // After successful pull, restart the instance
                        await ExecuteRestartAsync();
                    }
                    else
                    {
                        logger.LogError("Git pull failed with exit code {ExitCode}. Output: {Output}, Error: {Error}",
                            gitPullProcess.ExitCode, output, error);
                    }
                }
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to execute update");
        }
    }

    /// <summary>
    ///     Executes a restart on this instance.
    ///     This method is called when this instance receives a restart request.
    /// </summary>
    public async Task ExecuteRestartAsync()
    {
        logger.LogWarning("Restarting this instance in 3 seconds...");

        try
        {
            // Execute restart in a separate task to not block the API response
            await Task.Run(async () =>
            {
                // Wait a bit to allow the API response to be sent
                await Task.Delay(3000);

                // Log out the Discord client gracefully
                await client.StopAsync();

                logger.LogInformation("Discord client stopped. Exiting process.");

                // Exit the process - assuming a process manager (systemd, docker, etc.) will restart it
                Environment.Exit(0);
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to execute restart");
        }
    }

    /// <summary>
    ///     Updates all registered bot instances by calling their update endpoints.
    /// </summary>
    /// <returns>A dictionary of port numbers to success status and messages.</returns>
    /// <exception cref="InvalidOperationException">Thrown when not running on master instance.</exception>
    public async Task<Dictionary<int, (bool Success, string Message)>> UpdateAllInstancesAsync()
    {
        if (!new BotCredentials().IsMasterInstance)
            throw new InvalidOperationException("Can only update all instances from master bot.");

        await using var db = await dbFactory.CreateConnectionAsync();
        var instances = await db.BotInstances.ToListAsync();

        var results = new Dictionary<int, (bool Success, string Message)>();

        foreach (var instance in instances)
        {
            var result = await UpdateInstanceAsync(instance.Port);
            results[instance.Port] = (result.Success, result.Message);
        }

        return results;
    }

    /// <summary>
    ///     Restarts all registered bot instances by calling their restart endpoints.
    /// </summary>
    /// <returns>A dictionary of port numbers to success status and messages.</returns>
    /// <exception cref="InvalidOperationException">Thrown when not running on master instance.</exception>
    public async Task<Dictionary<int, (bool Success, string Message)>> RestartAllInstancesAsync()
    {
        if (!new BotCredentials().IsMasterInstance)
            throw new InvalidOperationException("Can only restart all instances from master bot.");

        await using var db = await dbFactory.CreateConnectionAsync();
        var instances = await db.BotInstances.ToListAsync();

        var results = new Dictionary<int, (bool Success, string Message)>();

        foreach (var instance in instances)
        {
            var result = await RestartInstanceAsync(instance.Port);
            results[instance.Port] = (result.Success, result.Message);
        }

        return results;
    }

    /// <summary>
    ///     Updates a specific bot instance by calling its update endpoint.
    /// </summary>
    /// <param name="port">The port number of the instance to update.</param>
    /// <returns>A tuple containing success status and message.</returns>
    /// <exception cref="InvalidOperationException">Thrown when not running on master instance.</exception>
    public async Task<(bool Success, string Message)> UpdateInstanceAsync(int port)
    {
        if (!new BotCredentials().IsMasterInstance)
            throw new InvalidOperationException("Can only update instances from master bot.");

        try
        {
            var host = await ResolveHostAsync(port);
            using var httpClient = CreateAuthenticatedClient();
            var response =
                await httpClient.PostAsync($"http://{host}:{port}/botapi/InstanceManagement/update", null);

            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("Successfully triggered update on instance at port {Port}", port);
                return (true, "Update triggered successfully");
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            logger.LogError(
                "Failed to trigger update on instance at port {Port}. Status: {Status}, Response: {Response}",
                port, response.StatusCode, errorContent);
            return (false, $"Failed: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to call update endpoint for instance on port {Port}", port);
            return (false, $"Error: {ex.Message}");
        }
    }

    /// <summary>
    ///     Restarts a specific bot instance by calling its restart endpoint.
    /// </summary>
    /// <param name="port">The port number of the instance to restart.</param>
    /// <returns>A tuple containing success status and message.</returns>
    /// <exception cref="InvalidOperationException">Thrown when not running on master instance.</exception>
    public async Task<(bool Success, string Message)> RestartInstanceAsync(int port)
    {
        if (!new BotCredentials().IsMasterInstance)
            throw new InvalidOperationException("Can only restart instances from master bot.");

        try
        {
            var host = await ResolveHostAsync(port);
            using var httpClient = CreateAuthenticatedClient();
            var response =
                await httpClient.PostAsync($"http://{host}:{port}/botapi/InstanceManagement/restart", null);

            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("Successfully triggered restart on instance at port {Port}", port);
                return (true, "Restart triggered successfully");
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            logger.LogError(
                "Failed to trigger restart on instance at port {Port}. Status: {Status}, Response: {Response}",
                port, response.StatusCode, errorContent);
            return (false, $"Failed: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to call restart endpoint for instance on port {Port}", port);
            return (false, $"Error: {ex.Message}");
        }
    }
}