using Newtonsoft.Json.Linq;
using NLog;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace NadekoBot.Core.Services.Common
{
    public class ImageLoader
    {
        private readonly Logger _log;
        private readonly HttpClient _http;
        private readonly ConnectionMultiplexer _con;

        public Func<string, RedisKey> GetKey { get; }

        private IDatabase _db => _con.GetDatabase();

        private readonly List<Task<KeyValuePair<RedisKey, RedisValue>>> uriTasks = new List<Task<KeyValuePair<RedisKey, RedisValue>>>();

        public ImageLoader(HttpClient http, ConnectionMultiplexer con, Func<string, RedisKey> getKey)
        {
            _log = LogManager.GetCurrentClassLogger();
            _http = http;
            _con = con;
            GetKey = getKey;
        }

        private async Task<byte[]> GetImageData(Uri uri)
        {
            if (uri.IsFile)
            {
                try
                {
                    var bytes = await File.ReadAllBytesAsync(uri.LocalPath);
                    return bytes;
                }
                catch (Exception ex)
                {
                    _log.Warn(ex);
                    return null;
                }
            }
            else
            {
                return await _http.GetByteArrayAsync(uri);
            }
        }

        async Task HandleJArray(JArray arr, string key)
        {
            var tasks = arr.Where(x => x.Type == JTokenType.String)
                .Select(async x =>
                {
                    try
                    {
                        return await GetImageData((Uri)x).ConfigureAwait(false);
                    }
                    catch
                    {
                        _log.Error("Error retreiving image for key {0}: {1}", key, x);
                        return null;
                    }
                });

            byte[][] vals = Array.Empty<byte[]>();
            vals = await Task.WhenAll(tasks).ConfigureAwait(false);
            if (vals.Any(x => x == null))
                vals = vals.Where(x => x != null).ToArray();

            await _db.KeyDeleteAsync(GetKey(key)).ConfigureAwait(false);
            await _db.ListRightPushAsync(GetKey(key),
                vals.Where(x => x != null)
                    .Select(x => (RedisValue)x)
                    .ToArray()).ConfigureAwait(false);

            if (arr.Count != vals.Length)
            {
                _log.Info("{2}/{1} URIs for the key '{0}' have been loaded. Some of the supplied URIs are either unavailable or invalid.", key, arr.Count, vals.Count());
            }
        }

        async Task<KeyValuePair<RedisKey, RedisValue>> HandleUri(Uri uri, string key)
        {
            try
            {
                RedisValue data = await GetImageData(uri).ConfigureAwait(false);
                return new KeyValuePair<RedisKey, RedisValue>(GetKey(key), data);
            }
            catch
            {
                _log.Info("Setting '{0}' image failed. The URI you provided is either unavailable or invalid.", key.ToLowerInvariant());
                return new KeyValuePair<RedisKey, RedisValue>("", "");
            }
        }

        Task HandleJObject(JObject obj, string parent = "")
        {
            string GetParentString()
            {
                if (string.IsNullOrWhiteSpace(parent))
                    return "";
                else
                    return parent + "_";
            }
            List<Task> tasks = new List<Task>();
            Task t;
            // go through all of the kvps in the object
            foreach (var kvp in obj)
            {
                // if it's a JArray, resole it using jarray method which will
                // return task<byte[][]> aka an array of all images' bytes
                if (kvp.Value.Type == JTokenType.Array)
                {
                    t = HandleJArray((JArray)kvp.Value, GetParentString() + kvp.Key);
                    tasks.Add(t);
                }
                else if (kvp.Value.Type == JTokenType.String)
                {
                    var uriTask = HandleUri((Uri)kvp.Value, GetParentString() + kvp.Key);
                    uriTasks.Add(uriTask);
                }
                else if (kvp.Value.Type == JTokenType.Object)
                {
                    t = HandleJObject((JObject)kvp.Value, GetParentString() + kvp.Key);
                    tasks.Add(t);
                }
            }
            return Task.WhenAll(tasks);
        }

        public async Task LoadAsync(JObject obj)
        {
            await HandleJObject(obj).ConfigureAwait(false);
            var results = await Task.WhenAll(uriTasks).ConfigureAwait(false);
            await _db.StringSetAsync(results.Where(x => x.Key != "").ToArray()).ConfigureAwait(false);
        }
    }
}
