using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using Mewdeko.Modules.Xp.Models;
using Mewdeko.Modules.Xp.Services;

namespace Mewdeko.Modules.Import.Common;

/// <summary>
///     Downloads leaderboards and balances from other bots' public APIs.
/// </summary>
/// <param name="http">The HTTP client to download with.</param>
public sealed class ImportApiReader(HttpClient http)
{
    private const int MaxPages = 2000;
    private const int MaxRetries = 5;

    private const string BrowserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";

    /// <summary>
    ///     Reads a server's whole MEE6 leaderboard and its level role rewards. The leaderboard has to be public.
    /// </summary>
    /// <param name="guildId">The server to read.</param>
    /// <param name="progress">Called with the number of members read so far.</param>
    /// <param name="token">Cancels the download.</param>
    public async Task<ImportDataset> ReadMee6Async(ulong guildId, Action<int> progress, CancellationToken token)
    {
        var members = new List<ImportedMember>();
        var rewards = new List<ImportedRoleReward>();

        for (var page = 0; page < MaxPages; page++)
        {
            using var document = await GetJsonAsync(
                $"https://mee6.xyz/api/plugins/levels/leaderboard/{guildId}?page={page}&limit=1000", null, true,
                token);

            var root = document.RootElement;
            if (page == 0 && root.TryGetProperty("role_rewards", out var roleRewards) &&
                roleRewards.ValueKind == JsonValueKind.Array)
            {
                foreach (var reward in roleRewards.EnumerateArray())
                {
                    if (reward.TryGetProperty("rank", out var rank) && rank.TryGetInt32(out var level) &&
                        reward.TryGetProperty("role", out var role) && role.ValueKind == JsonValueKind.Object &&
                        role.TryGetProperty("id", out var roleIdElement) &&
                        ulong.TryParse(roleIdElement.ToString(), out var roleId))
                        rewards.Add(new ImportedRoleReward(level, roleId));
                }
            }

            if (!root.TryGetProperty("players", out var players) || players.ValueKind != JsonValueKind.Array ||
                players.GetArrayLength() == 0)
                break;

            foreach (var player in players.EnumerateArray())
            {
                if (!player.TryGetProperty("id", out var idElement) ||
                    !ulong.TryParse(idElement.ToString(), out var userId))
                    continue;

                members.Add(new ImportedMember(
                    userId,
                    Long(player, "xp"),
                    (int?)Long(player, "level"),
                    Long(player, "message_count"),
                    null,
                    null,
                    player.TryGetProperty("username", out var name) ? name.GetString() : null));
            }

            progress(members.Count);

            if (players.GetArrayLength() < 1000)
                break;

            await Task.Delay(500, token);
        }

        return Dataset(members, rewards, XpCurveType.Mee6);
    }

    /// <summary>
    ///     Reads a server's whole Amari leaderboard with an Amari API key.
    /// </summary>
    /// <param name="guildId">The server to read.</param>
    /// <param name="apiKey">The Amari API key.</param>
    /// <param name="progress">Called with the number of members read so far.</param>
    /// <param name="token">Cancels the download.</param>
    public async Task<ImportDataset> ReadAmariAsync(ulong guildId, string apiKey, Action<int> progress,
        CancellationToken token)
    {
        var members = new List<ImportedMember>();

        for (var page = 1; page <= MaxPages; page++)
        {
            using var document = await GetJsonAsync(
                $"https://amaribot.com/api/v1/guild/raw/leaderboard/{guildId}?page={page}&limit=1000", apiKey, false,
                token);

            if (!document.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array ||
                data.GetArrayLength() == 0)
                break;

            var before = members.Count;
            foreach (var entry in data.EnumerateArray())
            {
                if (!entry.TryGetProperty("id", out var idElement) ||
                    !ulong.TryParse(idElement.ToString(), out var userId))
                    continue;

                members.Add(new ImportedMember(userId, Long(entry, "exp"), (int?)Long(entry, "level"), null, null,
                    null, entry.TryGetProperty("username", out var name) ? name.GetString() : null));
            }

            progress(members.Count);

            var total = document.RootElement.TryGetProperty("count", out var count) && count.TryGetInt32(out var c)
                ? c
                : 0;
            if (data.GetArrayLength() < 1000 || members.Count >= total || members.Count == before)
                break;

            await Task.Delay(1000, token);
        }

        return Dataset(ImportFileReader.Deduplicate(members), [], XpCurveType.Amari);
    }

    /// <summary>
    ///     Reads a server's whole Tatsu leaderboard with a Tatsu API key.
    /// </summary>
    /// <param name="guildId">The server to read.</param>
    /// <param name="apiKey">The Tatsu API key.</param>
    /// <param name="progress">Called with the number of members read so far.</param>
    /// <param name="token">Cancels the download.</param>
    public async Task<ImportDataset> ReadTatsuAsync(ulong guildId, string apiKey, Action<int> progress,
        CancellationToken token)
    {
        var members = new List<ImportedMember>();
        var offset = 0;

        for (var page = 0; page < MaxPages; page++)
        {
            using var document = await GetJsonAsync(
                $"https://api.tatsu.gg/v1/guilds/{guildId}/rankings/all?offset={offset}", apiKey, false, token);

            if (!document.RootElement.TryGetProperty("rankings", out var rankings) ||
                rankings.ValueKind != JsonValueKind.Array || rankings.GetArrayLength() == 0)
                break;

            foreach (var entry in rankings.EnumerateArray())
            {
                if (!entry.TryGetProperty("user_id", out var idElement) ||
                    !ulong.TryParse(idElement.ToString(), out var userId))
                    continue;

                members.Add(new ImportedMember(userId, Long(entry, "score"), null, null, null, null, null));
            }

            offset += rankings.GetArrayLength();
            progress(members.Count);
            await Task.Delay(1100, token);
        }

        return Dataset(ImportFileReader.Deduplicate(members), [], null);
    }

    /// <summary>
    ///     Reads every member's UnbelievaBoat balance with an UnbelievaBoat application token that has been authorized
    ///     on the server.
    /// </summary>
    /// <param name="guildId">The server to read.</param>
    /// <param name="token">The UnbelievaBoat application token.</param>
    /// <param name="progress">Called with the number of members read so far.</param>
    /// <param name="cancellation">Cancels the download.</param>
    public async Task<ImportDataset> ReadUnbelievaBoatAsync(ulong guildId, string token, Action<int> progress,
        CancellationToken cancellation)
    {
        var members = new List<ImportedMember>();

        for (var page = 1; page <= MaxPages; page++)
        {
            using var document = await GetJsonAsync(
                $"https://unbelievaboat.com/api/v1/guilds/{guildId}/users?page={page}&limit=1000&sort=total", token,
                false, cancellation);

            var root = document.RootElement;
            var users = root.ValueKind == JsonValueKind.Array
                ? root
                : root.TryGetProperty("users", out var list)
                    ? list
                    : default;

            if (users.ValueKind != JsonValueKind.Array || users.GetArrayLength() == 0)
                break;

            foreach (var entry in users.EnumerateArray())
            {
                if (!entry.TryGetProperty("user_id", out var idElement) ||
                    !ulong.TryParse(idElement.ToString(), out var userId))
                    continue;

                members.Add(new ImportedMember(userId, null, null, null, Long(entry, "cash"), Long(entry, "bank"),
                    null));
            }

            progress(members.Count);

            var totalPages = root.ValueKind == JsonValueKind.Object &&
                             root.TryGetProperty("total_pages", out var pages) && pages.TryGetInt32(out var p)
                ? p
                : page;
            if (page >= totalPages)
                break;

            await Task.Delay(300, cancellation);
        }

        return new ImportDataset
        {
            Kind = ImportKind.Currency, Members = ImportFileReader.Deduplicate(members)
        };
    }

    private static ImportDataset Dataset(List<ImportedMember> members, List<ImportedRoleReward> rewards,
        XpCurveType? native)
    {
        return new ImportDataset
        {
            Kind = ImportKind.Xp,
            Members = members,
            RoleRewards = rewards.DistinctBy(x => x.Level).ToList(),
            NativeCurve = native,
            SourceXpForLevel = native is { } curve ? level => XpCalculator.CalculateXpForLevel(level, curve) : null
        };
    }

    private static long? Long(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
            return null;

        return value.ValueKind == JsonValueKind.Number
            ? value.TryGetInt64(out var n) ? n : (long)Math.Clamp(value.GetDouble(), 0, long.MaxValue / 2)
            : ImportFileReader.ParseLong(value.ToString());
    }

    private async Task<JsonDocument> GetJsonAsync(string url, string? authorization, bool browserAgent,
        CancellationToken token)
    {
        for (var attempt = 0;; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Accept.ParseAdd("application/json");
            if (browserAgent)
                request.Headers.UserAgent.ParseAdd(BrowserAgent);
            if (authorization is not null)
                request.Headers.TryAddWithoutValidation("Authorization", authorization.Trim());

            HttpResponseMessage response;
            try
            {
                response = await http.SendAsync(request, token);
            }
            catch (HttpRequestException)
            {
                if (attempt >= MaxRetries)
                    throw new ImportException(ImportError.SourceFailed);
                await Task.Delay(TimeSpan.FromSeconds(2 << attempt), token);
                continue;
            }

            using (response)
            {
                switch (response.StatusCode)
                {
                    case HttpStatusCode.TooManyRequests:
                    case HttpStatusCode.BadGateway:
                    case HttpStatusCode.ServiceUnavailable:
                    case HttpStatusCode.GatewayTimeout:
                        if (attempt >= MaxRetries)
                            throw new ImportException(response.StatusCode == HttpStatusCode.TooManyRequests
                                ? ImportError.RateLimited
                                : ImportError.SourceFailed);
                        var wait = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(2 << attempt);
                        await Task.Delay(wait > TimeSpan.FromMinutes(1) ? TimeSpan.FromMinutes(1) : wait, token);
                        continue;
                    case HttpStatusCode.Unauthorized when authorization is null:
                    case HttpStatusCode.Forbidden when authorization is null:
                        throw new ImportException(ImportError.LeaderboardPrivate);
                    case HttpStatusCode.Unauthorized:
                    case HttpStatusCode.Forbidden:
                        throw new ImportException(ImportError.BadKey);
                    case HttpStatusCode.NotFound:
                        throw new ImportException(ImportError.NotFound);
                }

                if (!response.IsSuccessStatusCode)
                    throw new ImportException(ImportError.SourceFailed);

                await using var stream = await response.Content.ReadAsStreamAsync(token);
                try
                {
                    return await JsonDocument.ParseAsync(stream, cancellationToken: token);
                }
                catch (JsonException)
                {
                    throw new ImportException(ImportError.SourceFailed);
                }
            }
        }
    }
}
