using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Mewdeko.Modules.Searches.Common.StreamNotifications.Models;
using Mewdeko.Modules.Searches.Common.StreamNotifications.Providers;
using Mewdeko.Services.Database.Models;
using Newtonsoft.Json;
using Serilog;
using StackExchange.Redis;

#nullable enable
namespace Mewdeko.Modules.Searches.Common.StreamNotifications;

public class NotifChecker
{
    private readonly string _key;
    private readonly ConnectionMultiplexer _multi;
    private readonly HashSet<(FollowedStream.FType, string)> _offlineBuffer;

    private readonly Dictionary<FollowedStream.FType, Provider> _streamProviders;

    public NotifChecker(IHttpClientFactory httpClientFactory, ConnectionMultiplexer multi, string uniqueCacheKey,
        bool isMaster)
    {
        _multi = multi;
        _key = $"{uniqueCacheKey}_followed_streams_data";
        _streamProviders = new Dictionary<FollowedStream.FType, Provider>
        {
            {FollowedStream.FType.Twitch, new TwitchProvider(httpClientFactory)},
            {FollowedStream.FType.Picarto, new PicartoProvider(httpClientFactory)}
        };
        _offlineBuffer = new HashSet<(FollowedStream.FType, string)>();
        if (isMaster) CacheClearAllData();
    }

    public event Func<List<StreamData>, Task> OnStreamsOffline = _ => Task.CompletedTask;
    public event Func<List<StreamData>, Task> OnStreamsOnline = _ => Task.CompletedTask;

    // gets all streams which have been failing for more than the provided timespan
    public IEnumerable<StreamDataKey> GetFailingStreams(TimeSpan duration, bool remove = false)
    {
        var toReturn = _streamProviders.SelectMany(prov => prov.Value
                .FailingStreams2
                .Where(fs => DateTime.UtcNow - fs.ErroringSince > duration)
                .Select(fs => new StreamDataKey(prov.Value.Platform, fs.Item1)))
            .ToList();

        if (remove)
            foreach (var toBeRemoved in toReturn)
                _streamProviders[toBeRemoved.Type].ClearErrorsFor(toBeRemoved.Name);

        return toReturn;
    }

    public Task RunAsync() =>
        Task.Run(async () =>
        {
            while (true)
                try
                {
                    var allStreamData = CacheGetAllData();

                    var oldStreamDataDict = allStreamData
                                            // group by type
                                            .GroupBy(entry => entry.Key.Type)
                                            .ToDictionary(
                                                entry => entry.Key,
                                                entry => entry.AsEnumerable().ToDictionary(x => x.Key.Name, x => x.Value)
                                            );

                    var newStreamData = await Task.WhenAll(oldStreamDataDict
                        .Select(x =>
                        {
                            // get all stream data for the streams of this type
                            if (_streamProviders.TryGetValue(x.Key, out var provider))
                                return provider.GetStreamDataAsync(x.Value.Select(entry => entry.Key).ToList());

                            // this means there's no provider for this stream data, (and there was before?)
                            return Task.FromResult(new List<StreamData>());
                        }));

                    var newlyOnline = new List<StreamData>();
                    var newlyOffline = new List<StreamData>();
                    // go through all new stream data, compare them with the old ones
                    foreach (var newData in newStreamData.SelectMany(x => x))
                    {
                        // update cached data
                        var key = newData.CreateKey();
                        CacheAddData(key, newData, true);

                        // compare old data with new data
                        var oldData = oldStreamDataDict[key.Type][key.Name];

                        // this is the first pass
                        if (oldData is null)
                            continue;

                        // if the stream is offline, we need to check if it was
                        // marked as offline once previously
                        // if it was, that means this is second time we're getting offline
                        // status for that stream -> notify subscribers
                        // Note: This is done because twitch api will sometimes return an offline status
                        //       shortly after the stream is already online, which causes duplicate notifications.
                        //       (stream is online -> stream is offline -> stream is online again (and stays online))
                        //       This offlineBuffer will make it so that the stream has to be marked as offline TWICE
                        //       before it sends an offline notification to the subscribers.
                        var streamId = (key.Type, key.Name);
                        if (!newData.IsLive && _offlineBuffer.Remove(streamId))
                        {
                            newlyOffline.Add(newData);
                        }
                        else if (newData.IsLive != oldData.IsLive)
                        {
                            if (newData.IsLive)
                            {
                                _offlineBuffer.Remove(streamId);
                                newlyOnline.Add(newData);
                            }
                            else
                            {
                                _offlineBuffer.Add(streamId);
                                // newlyOffline.Add(newData);
                            }
                        }
                    }

                    var tasks = new List<Task>
                    {
                        Task.Delay(30_000)
                    };

                    if (newlyOnline.Count > 0) tasks.Add(OnStreamsOnline(newlyOnline));

                    if (newlyOffline.Count > 0) tasks.Add(OnStreamsOffline(newlyOffline));

                    await Task.WhenAll(tasks);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Error getting stream notifications: {ex.Message}");
                }
        });

    public bool CacheAddData(StreamDataKey key, StreamData? data, bool replace)
    {
        var db = _multi.GetDatabase();
        return db.HashSet(
            _key,
            JsonConvert.SerializeObject(key),
            JsonConvert.SerializeObject(data),
            replace ? When.Always : When.NotExists);
    }

    public void CacheDeleteData(StreamDataKey key)
    {
        var db = _multi.GetDatabase();
        db.HashDelete(_key, JsonConvert.SerializeObject(key));
    }

    public void CacheClearAllData()
    {
        var db = _multi.GetDatabase();
        db.KeyDelete(_key);
    }

    public Dictionary<StreamDataKey, StreamData?> CacheGetAllData()
    {
        var db = _multi.GetDatabase();
        if (!db.KeyExists(_key)) return new Dictionary<StreamDataKey, StreamData?>();

        return db.HashGetAll(_key)
            .ToDictionary(
                entry => JsonConvert.DeserializeObject<StreamDataKey>(entry.Name),
                entry => entry.Value.IsNullOrEmpty
                    ? default
                    : JsonConvert.DeserializeObject<StreamData>(entry.Value));
    }

    public async Task<StreamData?> GetStreamDataByUrlAsync(string url)
    {
        // loop through all providers and see which regex matches
        foreach (var (_, provider) in _streamProviders)
        {
            var isValid = await provider.IsValidUrl(url);
            if (!isValid)
                continue;
            // if it's not a valid url, try another provider
            var data = await provider.GetStreamDataByUrlAsync(url);
            return data;
        }

        // if no provider found, return null
        return null;
    }

    /// <summary>
    ///     Return currently available stream data, get new one if none available, and start tracking the stream.
    /// </summary>
    /// <param name="url">Url of the stream</param>
    /// <returns>Stream data, if any</returns>
    public async Task<StreamData?> TrackStreamByUrlAsync(string url)
    {
        var data = await GetStreamDataByUrlAsync(url);
        EnsureTracked(data);
        return data;
    }

    /// <summary>
    ///     Make sure a stream is tracked using its stream data.
    /// </summary>
    /// <param name="data">Data to try to track if not already tracked</param>
    /// <returns>Whether it's newly added</returns>
    private bool EnsureTracked(StreamData? data)
    {
        // something failed, don't add anything to cache
        if (data is null)
            return false;

        // if stream is found, add it to the cache for tracking only if it doesn't already exist
        // because stream will be checked and events will fire in a loop. We don't want to override old state
        return CacheAddData(data.CreateKey(), data, false);
    }

    public void UntrackStreamByKey(in StreamDataKey key) => CacheDeleteData(key);
}