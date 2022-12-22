using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Serilog;
using StackExchange.Redis;

namespace Mewdeko.Services.Common;

public class ImageLoader
{
    private readonly ConnectionMultiplexer con;
    private readonly HttpClient http;

    private readonly List<Task<KeyValuePair<RedisKey, RedisValue>>> uriTasks = new();

    public ImageLoader(HttpClient http, ConnectionMultiplexer con, Func<string, RedisKey> getKey)
    {
        this.http = http;
        this.con = con;
        GetKey = getKey;
    }

    public Func<string, RedisKey> GetKey { get; }

    private IDatabase Db => con.GetDatabase();

    private async Task<byte[]>? GetImageData(Uri uri)
    {
        if (!uri.IsFile) return await http.GetByteArrayAsync(uri).ConfigureAwait(false);
        try
        {
            return await File.ReadAllBytesAsync(uri.LocalPath).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed reading image bytes");
            return null;
        }
    }

    private async Task? HandleJArray(JArray arr, string key)
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
                    Log.Error("Error retreiving image for key {Key}: {Data}", key, x);
                    return null;
                }
            });

        var vals = await Task.WhenAll(tasks).ConfigureAwait(false);
        if (vals.Any(x => x == null))
            vals = vals.Where(x => x != null).ToArray();

        await Db.KeyDeleteAsync(GetKey(key)).ConfigureAwait(false);
        await Db.ListRightPushAsync(GetKey(key),
            vals.Where(x => x != null)
                .Select(x => (RedisValue)x)
                .ToArray()).ConfigureAwait(false);

        if (arr.Count != vals.Length)
        {
            Log.Information(
                "{2}/{1} URIs for the key '{0}' have been loaded. Some of the supplied URIs are either unavailable or invalid.",
                key, arr.Count, vals.Length);
        }
    }

    private async Task<KeyValuePair<RedisKey, RedisValue>> HandleUri(Uri uri, string key)
    {
        try
        {
            RedisValue data = await GetImageData(uri).ConfigureAwait(false);
            return new KeyValuePair<RedisKey, RedisValue>(GetKey(key), data);
        }
        catch
        {
            Log.Information("Setting '{0}' image failed. The URI you provided is either unavailable or invalid.",
                key.ToLowerInvariant());
            return new KeyValuePair<RedisKey, RedisValue>("", "");
        }
    }

    private Task HandleJObject(JObject obj, string parent = "")
    {
        string GetParentString()
        {
            return string.IsNullOrWhiteSpace(parent) ? "" : $"{parent}_";
        }

        var tasks = new List<Task>();
        // go through all of the kvps in the object
        foreach (var kvp in obj)
        {
            Task t;
            switch (kvp.Value.Type)
            {
                // if it's a JArray, resole it using jarray method which will
                // return task<byte[][]> aka an array of all images' bytes
                case JTokenType.Array:
                    t = HandleJArray((JArray)kvp.Value, GetParentString() + kvp.Key);
                    tasks.Add(t);
                    break;
                case JTokenType.String:
                {
                    var uriTask = HandleUri((Uri)kvp.Value, GetParentString() + kvp.Key);
                    uriTasks.Add(uriTask);
                    break;
                }
                case JTokenType.Object:
                    t = HandleJObject((JObject)kvp.Value, GetParentString() + kvp.Key);
                    tasks.Add(t);
                    break;
            }
        }

        return Task.WhenAll(tasks);
    }

    public async Task LoadAsync(JObject obj)
    {
        await HandleJObject(obj).ConfigureAwait(false);
        var results = await Task.WhenAll(uriTasks).ConfigureAwait(false);
        await Db.StringSetAsync(results.Where(x => !string.IsNullOrEmpty(x.Key)).ToArray(), flags: CommandFlags.FireAndForget).ConfigureAwait(false);
    }
}