using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
        return KeyValuePattern().Replace(line, m => m.Value[..(m.Value.IndexOfAny(['=', ':']) + 1)] + Mask);
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
}
