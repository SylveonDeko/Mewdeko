using System.Net.Http;
using System.Threading;
using LinqToDB.EntityFrameworkCore;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Controllers;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Services.Impl;
using Newtonsoft.Json;
using Serilog;

namespace Mewdeko.Modules.OwnerOnly.Services;

/// <summary>
///     Service for managing instances via redis
/// </summary>
public class BotInstanceService(IDataCache cache, DbContextProvider provider, IHttpClientFactory factory)
    : INService, IReadyExecutor
{
    /// <inheritdoc />
    public async Task OnReadyAsync()
    {
        var creds = new BotCredentials();
        if (!creds.IsMasterInstance)
            return;
        var periodic = new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            await using var db = await provider.GetContextAsync();
            var toPush = new List<BotStatus.BotStatusModel>();
            var instances = await db.BotInstances.ToListAsyncLinqToDB();
            if (instances.Count == 0)
                return;

            foreach (var instance in instances)
            {
                using var client = factory.CreateClient();
                var response = await client.GetAsync($"{instance.BotUrl}/BotStatus");

                if (!response.IsSuccessStatusCode) continue;
                var actualResponse = await response.Content.ReadAsStringAsync();
                var botStatus = JsonConvert.DeserializeObject<BotStatus.BotStatusModel>(actualResponse);
                toPush.Add(botStatus);
            }

            if (toPush.Count == 0) continue;
            var redisDb = cache.Redis.GetDatabase();
            await redisDb.StringSetAsync("bot_api_instances", JsonConvert.SerializeObject(toPush));
        } while (await periodic.WaitForNextTickAsync(CancellationToken.None));
    }


    /// <summary>
    ///     Adds an instance to the cache/db
    /// </summary>
    /// <param name="instanceUrl"></param>
    /// <returns></returns>
    public async Task<(bool, BotStatus.BotStatusModel?, RejectionReason?)> AddInstanceAsync(Uri instanceUrl)
    {
        var creds = new BotCredentials();
        if (!creds.IsMasterInstance)
            return (false, null, RejectionReason.NotMaster);

        await using var db = await provider.GetContextAsync();
        var instances = await db.BotInstances.ToListAsyncLinqToDB();
        if (instances.Any(x => x.BotUrl == instanceUrl.ToString()))
            return (false, null, RejectionReason.InstanceAlreadyExists);

        var redisDb = cache.Redis.GetDatabase();
        List<BotStatus.BotStatusModel> instanceList;

        try
        {
            var redisStatus = await redisDb.StringGetAsync("bot_api_instances");
            instanceList = redisStatus.HasValue
                ? JsonConvert.DeserializeObject<List<BotStatus.BotStatusModel>>(redisStatus)
                : [];
        }
        catch (Exception e)
        {
            Log.Error(e, "Error deserializing the instance list from Redis.");
            return (false, null, RejectionReason.How);
        }

        var (botStatus, rejectionReason) = await GetBotStatusAsync(instanceUrl);
        if (botStatus == null)
            return (false, null, rejectionReason);

        instanceList.Add(botStatus);

        try
        {
            var serializedList = JsonConvert.SerializeObject(instanceList);
            await redisDb.StringSetAsync("bot_api_instances", serializedList);
        }
        catch (Exception e)
        {
            Log.Error(e, "Error serializing the instance list to Redis.");
            return (false, null, RejectionReason.How);
        }

        return (true, botStatus, null);
    }

    /// <summary>
    /// Removes an instance from the cache/db
    /// </summary>
    /// <param name="instanceUrl"></param>
    /// <returns></returns>
    public async Task<(bool, RejectionReason?)> RemoveInstanceAsync(Uri instanceUrl)
    {
        var creds = new BotCredentials();
        if (!creds.IsMasterInstance)
            return (false, RejectionReason.NotMaster);

        await using var db = await provider.GetContextAsync();
        var instances = await db.BotInstances.ToListAsyncLinqToDB();
        if (instances.All(x => x.BotUrl != instanceUrl.ToString()))
            return (false, RejectionReason.InstanceNotFound);

        var redisDb = cache.Redis.GetDatabase();
        List<BotStatus.BotStatusModel> instanceList;

        try
        {
            var redisStatus = await redisDb.StringGetAsync("bot_api_instances");
            if (!redisStatus.HasValue)
                return (false, RejectionReason.InstanceNotFoundInCache);

            instanceList = JsonConvert.DeserializeObject<List<BotStatus.BotStatusModel>>(redisStatus);
        }
        catch (Exception e)
        {
            Log.Error(e, "Error deserializing the instance list from Redis.");
            return (false, RejectionReason.How);
        }

        // Find and remove the instance
        var instanceToRemove = instanceList.FirstOrDefault(x => x.InstanceUrl == instanceUrl.ToString());
        if (instanceToRemove == null)
            return (false, RejectionReason.InstanceNotFoundInCache);

        instanceList.Remove(instanceToRemove);

        try
        {
            var serializedList = JsonConvert.SerializeObject(instanceList);
            await redisDb.StringSetAsync("bot_api_instances", serializedList);
        }
        catch (Exception e)
        {
            Log.Error(e, "Error serializing the instance list to Redis.");
            return (false, RejectionReason.How);
        }

        return (true, null);
    }


    private async Task<(BotStatus.BotStatusModel, RejectionReason?)> GetBotStatusAsync(Uri instanceUrl)
    {
        var client = factory.CreateClient();

        try
        {
            var response = await client.GetAsync(instanceUrl);
            if (!response.IsSuccessStatusCode)
                return (null, RejectionReason.InstanceNotFound);

            var content = await response.Content.ReadAsStringAsync();
            var botStatus = JsonConvert.DeserializeObject<BotStatus.BotStatusModel>(content);
            return (botStatus, null);
        }
        catch (JsonException)
        {
            return (null, RejectionReason.InstanceSentWrongData);
        }
        catch (Exception e)
        {
            Log.Error(e, "Unexpected error when fetching bot status.");
            return (null, RejectionReason.How);
        }
    }


    /// <summary>
    ///     The reason an instance was not added to the list.
    /// </summary>
    public enum RejectionReason
    {
        /// <summary>
        ///     The bot used to add an instance is not the master bot.
        /// </summary>
        NotMaster,

        /// <summary>
        ///     The instance is already in the list.
        /// </summary>
        InstanceAlreadyExists,

        /// <summary>
        ///     The instance is unresponsive or not found.
        /// </summary>
        InstanceNotFound,

        /// <summary>
        ///     When attempting to add, the instance sent the wrong data back.
        /// </summary>
        InstanceSentWrongData,

        /// <summary>
        /// Instance was not found in the redis cache
        /// </summary>
        InstanceNotFoundInCache,

        /// <summary>
        ///     Actually how did you get here????
        /// </summary>
        How
    }
}