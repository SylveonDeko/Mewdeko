using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Mewdeko.Services;

/// <summary>
///     One shop item on a Ko-fi shop order.
/// </summary>
/// <param name="Code">The item's direct link code.</param>
/// <param name="Quantity">How many were bought.</param>
public sealed record KofiShopItem(string Code, int Quantity);

/// <summary>
///     The parts of a Ko-fi webhook payload that custom bot slots depend on.
/// </summary>
/// <param name="VerificationToken">The token Ko-fi sends with each call.</param>
/// <param name="TransactionId">The unique id of the payment.</param>
/// <param name="Type">Donation, Subscription, Commission or Shop Order.</param>
/// <param name="Email">The payer's email.</param>
/// <param name="Amount">The amount as Ko-fi sent it.</param>
/// <param name="IsSubscriptionPayment">Whether the payment is a membership payment.</param>
/// <param name="IsFirstSubscriptionPayment">Whether it is the first payment of a membership.</param>
/// <param name="PaidAt">When the payment was made, in UTC.</param>
/// <param name="ShopItems">The items on a shop order.</param>
public sealed record KofiPayload(
    string VerificationToken,
    string TransactionId,
    string Type,
    string Email,
    string? Amount,
    bool IsSubscriptionPayment,
    bool IsFirstSubscriptionPayment,
    DateTime PaidAt,
    IReadOnlyList<KofiShopItem> ShopItems)
{
    /// <summary>
    ///     Parses the JSON Ko-fi posts in the <c>data</c> form field.
    /// </summary>
    /// <param name="json">The JSON text.</param>
    /// <returns>The payload, or null when it is not valid or lacks a transaction id or email.</returns>
    public static KofiPayload? TryParse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return null;

            var transactionId = ReadString(root, "kofi_transaction_id");
            var email = ReadString(root, "email");
            if (string.IsNullOrWhiteSpace(transactionId) || string.IsNullOrWhiteSpace(email))
                return null;

            var paidAt = DateTime.UtcNow;
            var stamp = ReadString(root, "timestamp");
            if (stamp != null && DateTimeOffset.TryParse(stamp, null, System.Globalization.DateTimeStyles.AssumeUniversal,
                    out var parsed))
                paidAt = parsed.UtcDateTime;

            var items = new List<KofiShopItem>();
            if (root.TryGetProperty("shop_items", out var shop) && shop.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in shop.EnumerateArray())
                {
                    var quantity = item.ValueKind == JsonValueKind.Object &&
                                   item.TryGetProperty("quantity", out var q) && q.TryGetInt32(out var n) && n > 0
                        ? n
                        : 1;
                    items.Add(new KofiShopItem(ReadString(item, "direct_link_code") ?? "", quantity));
                }
            }

            return new KofiPayload(
                ReadString(root, "verification_token") ?? "",
                transactionId.Trim(),
                ReadString(root, "type") ?? "",
                email.Trim(),
                ReadString(root, "amount"),
                ReadBool(root, "is_subscription_payment"),
                ReadBool(root, "is_first_subscription_payment"),
                paidAt,
                items);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    ///     Compares the supplied verification token with the configured one without leaking timing.
    /// </summary>
    /// <param name="supplied">The token from the payload.</param>
    /// <param name="expected">The configured token. Empty never matches.</param>
    public static bool VerifyToken(string? supplied, string? expected)
    {
        if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(supplied))
            return false;

        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(supplied),
            Encoding.UTF8.GetBytes(expected));
    }

    /// <summary>
    ///     Works out what the payment grants.
    /// </summary>
    /// <param name="shopItemCodes">The comma separated item codes that count. Empty means every shop order.</param>
    /// <returns>Whether it extends a membership slot and how many new slots it creates.</returns>
    public (bool ExtendsMembership, int NewSlots) Grant(string? shopItemCodes)
    {
        if (string.Equals(Type, "Subscription", StringComparison.OrdinalIgnoreCase) || IsSubscriptionPayment)
            return (true, 0);

        if (!string.Equals(Type, "Shop Order", StringComparison.OrdinalIgnoreCase))
            return (false, 0);

        var codes = (shopItemCodes ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (codes.Length == 0)
            return (false, Math.Max(1, ShopItems.Sum(x => x.Quantity)));

        var matching = ShopItems
            .Where(x => codes.Contains(x.Code, StringComparer.OrdinalIgnoreCase))
            .Sum(x => x.Quantity);
        return (false, matching);
    }

    private static string? ReadString(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value))
            return null;

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null
        };
    }

    private static bool ReadBool(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
            return false;

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.String => bool.TryParse(value.GetString(), out var b) && b,
            _ => false
        };
    }
}

/// <summary>
///     The date rules for custom bot slots.
/// </summary>
public static class CustomBotExpiry
{
    /// <summary>
    ///     How many days a stopped custom bot's container and token are kept.
    /// </summary>
    public const int RetentionDays = 14;

    /// <summary>
    ///     Slot source for Ko-fi purchases.
    /// </summary>
    public const string KofiSource = "kofi";

    /// <summary>
    ///     Slot source for slots an owner granted.
    /// </summary>
    public const string ManualSource = "manual";

    /// <summary>
    ///     Adds a payment's days to a slot.
    /// </summary>
    /// <param name="current">The slot's current expiry, or null.</param>
    /// <param name="paidAt">When the payment was made.</param>
    /// <param name="days">Days the payment grants.</param>
    public static DateTime Extend(DateTime? current, DateTime paidAt, int days)
    {
        var start = current is { } c && c > paidAt ? c : paidAt;
        return start.AddDays(days);
    }

    /// <summary>
    ///     Gets the moment a slot's bot is stopped.
    /// </summary>
    /// <param name="expiresAt">The slot's expiry, null for never.</param>
    /// <param name="source">The slot source.</param>
    /// <param name="graceDays">Days after a payment the bot keeps running.</param>
    /// <param name="daysPerPayment">Days one payment grants.</param>
    public static DateTime? StopAt(DateTime? expiresAt, string source, int graceDays, int daysPerPayment)
    {
        if (expiresAt is null)
            return null;

        var extra = source == KofiSource ? Math.Max(0, graceDays - daysPerPayment) : 0;
        return expiresAt.Value.AddDays(extra);
    }

    /// <summary>
    ///     Checks whether a slot's bot should be stopped.
    /// </summary>
    public static bool ShouldStop(DateTime? expiresAt, string source, int graceDays, int daysPerPayment, DateTime now)
    {
        var stop = StopAt(expiresAt, source, graceDays, daysPerPayment);
        return stop is { } s && now >= s;
    }

    /// <summary>
    ///     Checks whether a stopped bot has been kept long enough to be deleted.
    /// </summary>
    /// <param name="stoppedAt">When the expiry loop stopped it.</param>
    /// <param name="now">The current time.</param>
    public static bool ShouldRemove(DateTime? stoppedAt, DateTime now)
    {
        return stoppedAt is { } s && now >= s.AddDays(RetentionDays);
    }
}

/// <summary>
///     Picks the API port a custom bot listens on.
/// </summary>
public static class CustomBotPorts
{
    /// <summary>
    ///     Picks the lowest port in the range nobody uses.
    /// </summary>
    /// <param name="start">First port of the range.</param>
    /// <param name="end">Last port of the range.</param>
    /// <param name="used">Ports already taken.</param>
    /// <returns>The port, or null when the range is full.</returns>
    public static int? Pick(int start, int end, IEnumerable<int> used)
    {
        var taken = used.ToHashSet();
        for (var port = start; port <= end; port++)
        {
            if (!taken.Contains(port))
                return port;
        }

        return null;
    }
}

/// <summary>
///     Removes secrets from text that is shown to a customer.
/// </summary>
public static partial class CustomBotRedactor
{
    /// <summary>
    ///     What replaces a secret.
    /// </summary>
    public const string Mask = "[redacted]";

    [GeneratedRegex(@"[A-Za-z0-9_-]{23,30}\.[A-Za-z0-9_-]{6,7}\.[A-Za-z0-9_-]{25,}")]
    private static partial Regex TokenPattern();

    [GeneratedRegex(@"(?i)(password|pwd|apikey|api_key|secret)\s*[=:]\s*[^;\s,""']+")]
    private static partial Regex KeyValuePattern();

    [GeneratedRegex(@"(?i)\b(host|server|username|user id|userid|uid)\s*=\s*[^;\s,""']+")]
    private static partial Regex ConnectionPartPattern();

    [GeneratedRegex(@"(?i)\b[a-z][a-z0-9+.-]*://[^/\s:@]*:[^/\s@]*@[^/\s,;""']+")]
    private static partial Regex UrlCredentialPattern();

    [GeneratedRegex(@"(?i)\b(?:host|server)\s*=\s*([^;\s,""']+)")]
    private static partial Regex HostPartPattern();

    /// <summary>
    ///     Pulls the host names out of connection strings so they can be masked wherever they show up on their own.
    /// </summary>
    /// <param name="connectionStrings">Postgres style, URL style or Redis style connection strings.</param>
    /// <returns>The distinct host names, three characters or longer.</returns>
    public static List<string> ConnectionHosts(IEnumerable<string?> connectionStrings)
    {
        var hosts = new List<string>();
        foreach (var text in connectionStrings)
        {
            if (string.IsNullOrWhiteSpace(text))
                continue;

            foreach (Match match in HostPartPattern().Matches(text))
                hosts.Add(match.Groups[1].Value);

            foreach (var part in text.Split(';', ','))
            {
                var trimmed = part.Trim();
                if (trimmed.Contains('=') || trimmed.Length == 0)
                    continue;

                var withoutScheme = trimmed.Contains("://") ? trimmed[(trimmed.IndexOf("://", StringComparison.Ordinal) + 3)..] : trimmed;
                var at = withoutScheme.LastIndexOf('@');
                if (at >= 0)
                    withoutScheme = withoutScheme[(at + 1)..];

                var host = withoutScheme.Split('/', ':')[0];
                if (host.Length > 0)
                    hosts.Add(host);
            }
        }

        return hosts.Where(x => x.Length >= 3).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    ///     Masks every known secret, anything shaped like a Discord token and connection string passwords.
    /// </summary>
    /// <param name="line">The text.</param>
    /// <param name="secrets">Exact values to mask. Empty and very short values are skipped.</param>
    public static string Redact(string line, IEnumerable<string?> secrets)
    {
        foreach (var secret in secrets)
        {
            if (string.IsNullOrEmpty(secret) || secret.Length < 6)
                continue;

            line = line.Replace(secret, Mask, StringComparison.Ordinal);
        }

        line = TokenPattern().Replace(line, Mask);
        line = UrlCredentialPattern().Replace(line, Mask);
        line = KeyValuePattern().Replace(line, m => m.Value[..(m.Value.IndexOfAny(['=', ':']) + 1)] + Mask);
        return ConnectionPartPattern().Replace(line, m => m.Value[..(m.Value.IndexOf('=') + 1)] + Mask);
    }
}

/// <summary>
///     Builds the invite link for a custom bot.
/// </summary>
public static class CustomBotInvite
{
    /// <summary>
    ///     The permission set the main bot's invite asks for.
    /// </summary>
    public const ulong Permissions = 66186303;

    /// <summary>
    ///     Builds the invite URL.
    /// </summary>
    /// <param name="botId">The custom bot's application id.</param>
    public static string Url(ulong botId)
    {
        return
            $"https://discord.com/oauth2/authorize?client_id={botId}&scope=bot%20applications.commands&permissions={Permissions}";
    }
}

/// <summary>
///     Encrypts custom bot tokens for storage.
/// </summary>
public static class CustomBotCrypto
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    /// <summary>
    ///     Derives the 256 bit key from a secret.
    /// </summary>
    /// <param name="secret">The configured key or secret to derive from.</param>
    public static byte[] DeriveKey(string secret)
    {
        return SHA256.HashData(Encoding.UTF8.GetBytes("mewdeko-custombot:" + secret));
    }

    /// <summary>
    ///     Encrypts a value with AES-GCM.
    /// </summary>
    /// <param name="plain">The text to protect.</param>
    /// <param name="key">The key from <see cref="DeriveKey" />.</param>
    /// <returns>Base64 of nonce, tag and ciphertext.</returns>
    public static string Encrypt(string plain, byte[] key)
    {
        var data = Encoding.UTF8.GetBytes(plain);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipher = new byte[data.Length];
        var tag = new byte[TagSize];
        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, data, cipher, tag);
        return Convert.ToBase64String(nonce.Concat(tag).Concat(cipher).ToArray());
    }

    /// <summary>
    ///     Decrypts a value written by <see cref="Encrypt" />.
    /// </summary>
    /// <param name="stored">The stored text.</param>
    /// <param name="key">The key from <see cref="DeriveKey" />.</param>
    /// <returns>The plain text, or null when the value or key is wrong.</returns>
    public static string? Decrypt(string stored, byte[] key)
    {
        try
        {
            var raw = Convert.FromBase64String(stored);
            if (raw.Length < NonceSize + TagSize)
                return null;

            var nonce = raw.AsSpan(0, NonceSize);
            var tag = raw.AsSpan(NonceSize, TagSize);
            var cipher = raw.AsSpan(NonceSize + TagSize);
            var plain = new byte[cipher.Length];
            using var aes = new AesGcm(key, TagSize);
            aes.Decrypt(nonce, cipher, tag, plain);
            return Encoding.UTF8.GetString(plain);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            return null;
        }
    }

    /// <summary>
    ///     Derives the key the first version of this feature used when no encryption key was configured.
    ///     It exists only so rows written back then can still be read and re-encrypted.
    /// </summary>
    /// <param name="apiKey">The master API key.</param>
    /// <param name="jwtSecret">The shared JWT secret.</param>
    public static byte[] LegacyKey(string apiKey, string jwtSecret)
    {
        return DeriveKey($"{apiKey}:{jwtSecret}");
    }

    /// <summary>
    ///     Decrypts with the current key and, failing that, the legacy key.
    /// </summary>
    /// <param name="stored">The stored text.</param>
    /// <param name="key">The current key.</param>
    /// <param name="legacyKey">The legacy key, or null when none applies.</param>
    /// <returns>The plain text and whether the legacy key was needed, or a null text when neither key works.</returns>
    public static (string? Plain, bool UsedLegacy) DecryptWithFallback(string stored, byte[] key, byte[]? legacyKey)
    {
        var plain = Decrypt(stored, key);
        if (plain is not null)
            return (plain, false);

        if (legacyKey is null)
            return (null, false);

        plain = Decrypt(stored, legacyKey);
        return (plain, plain is not null);
    }

    /// <summary>
    ///     Picks the API key to use when calling a child: its own when one is stored and readable, the master's
    ///     otherwise, so a child made before per-child keys existed keeps working until it is recreated.
    /// </summary>
    /// <param name="stored">The child's encrypted key, or null when it has none.</param>
    /// <param name="key">The current encryption key, or null when none is configured.</param>
    /// <param name="legacyKey">The legacy encryption key.</param>
    /// <param name="masterKey">The master API key.</param>
    public static string ResolveApiKey(string? stored, byte[]? key, byte[]? legacyKey, string masterKey)
    {
        if (stored is null || key is null)
            return masterKey;

        return DecryptWithFallback(stored, key, legacyKey).Plain ?? masterKey;
    }

    /// <summary>
    ///     Makes a new random API key for a custom bot.
    /// </summary>
    public static string GenerateApiKey()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(48)).ToLowerInvariant();
    }
}

/// <summary>
///     Caps how often one user may try to claim a purchase, so an email and transaction id cannot be guessed.
/// </summary>
/// <param name="maxAttempts">How many attempts one user gets per window.</param>
/// <param name="window">How long an attempt counts.</param>
public sealed class CustomBotClaimLimiter(int maxAttempts, TimeSpan window)
{
    private readonly Dictionary<ulong, List<DateTime>> attempts = new();

    /// <summary>
    ///     Records an attempt and says whether it is allowed.
    /// </summary>
    /// <param name="userId">The user trying.</param>
    /// <param name="now">The current time in UTC.</param>
    /// <returns>False when the user already used up the window.</returns>
    public bool TryAttempt(ulong userId, DateTime now)
    {
        lock (attempts)
        {
            if (!attempts.TryGetValue(userId, out var times))
            {
                times = [];
                attempts[userId] = times;
            }

            times.RemoveAll(x => now - x >= window);
            if (times.Count >= maxAttempts)
                return false;

            times.Add(now);
            return true;
        }
    }
}

/// <summary>
///     Builds the credentials file a custom bot container starts with.
/// </summary>
/// <param name="Token">The customer's bot token.</param>
/// <param name="Port">The API port of the container.</param>
/// <param name="ContainerName">The container name, which is also its host name on the Docker network.</param>
/// <param name="ApiKey">The API key of this one bot.</param>
/// <param name="JwtSecret">The JWT secret shared with the dashboard.</param>
/// <param name="DbConnection">The Postgres connection string the bot gets.</param>
/// <param name="RedisConnection">The Redis connection string the bot gets.</param>
/// <param name="OwnerIds">The owners of the main bot.</param>
/// <param name="DashboardUrl">The dashboard address.</param>
public sealed record CustomBotChildInputs(
    string Token,
    int Port,
    string ContainerName,
    string ApiKey,
    string JwtSecret,
    string DbConnection,
    string RedisConnection,
    IEnumerable<ulong> OwnerIds,
    string? DashboardUrl);

/// <summary>
///     Builds a custom bot's credentials from an allowlist, so a key added to the master's file later never reaches a
///     customer's container unless it is added here on purpose.
/// </summary>
public static class CustomBotChildCredentials
{
    /// <summary>
    ///     The keys copied from the master's file when present. Everything else in that file stays behind.
    /// </summary>
    public static readonly string[] InheritedKeys =
    [
        "UseGlobalCurrency", "PostgresSetupCompleted", "LavalinkUrl", "LavalinkPassword", "LavalinkNodes",
        "LavalinkDefaultSearchSource", "GoogleApiKey", "SpotifyClientId", "SpotifyClientSecret", "LastFmApiKey",
        "LastFmApiSecret", "OsuApiKey", "Rule34ApiKey", "Rule34UserId", "OpenMeteoApiUrl", "TikTokRssBridgeUrl",
        "TurnstileKey", "GiveawayEntryUrl", "PatreonClientId", "PatreonClientSecret", "PatreonBaseUrl",
        "TwitchClientId", "TwitchClientSecret", "KickClientId", "KickClientSecret", "TrovoClientId"
    ];

    /// <summary>
    ///     Picks the value a child gets: the dedicated setting when set, the master's own otherwise.
    /// </summary>
    /// <param name="dedicated">The setting meant for custom bots.</param>
    /// <param name="fallback">The master's own value.</param>
    public static string Pick(string? dedicated, string fallback)
    {
        return string.IsNullOrWhiteSpace(dedicated) ? fallback : dedicated;
    }

    /// <summary>
    ///     Builds the credentials of one custom bot.
    /// </summary>
    /// <param name="master">The parsed master credentials file, or null when it could not be read.</param>
    /// <param name="inputs">The values that are specific to this bot.</param>
    public static JsonObject Build(JsonObject? master, CustomBotChildInputs inputs)
    {
        var node = new JsonObject();
        if (master is not null)
        {
            foreach (var key in InheritedKeys)
            {
                if (master.TryGetPropertyValue(key, out var value) && value is not null)
                    node[key] = value.DeepClone();
            }
        }

        node["Token"] = inputs.Token;
        node["PsqlConnectionString"] = inputs.DbConnection;
        node["RedisConnections"] = inputs.RedisConnection;
        node["ApiKey"] = inputs.ApiKey;
        node["JwtSecret"] = inputs.JwtSecret;
        node["OwnerIds"] = new JsonArray(inputs.OwnerIds.Select(x => (JsonNode)JsonValue.Create(x)).ToArray());
        if (!string.IsNullOrWhiteSpace(inputs.DashboardUrl))
            node["DashboardUrl"] = inputs.DashboardUrl;

        node["IsMasterInstance"] = false;
        node["IsApiEnabled"] = true;
        node["ApiPort"] = inputs.Port;
        node["InstanceApiHost"] = inputs.ContainerName;
        node["TotalShards"] = 1;
        return node;
    }
}
