using System.Text;
using System.Text.Json.Nodes;
using Mewdeko.AuthHandlers;
using Mewdeko.Controllers;
using Mewdeko.Controllers.Common.DashboardAccess;
using Mewdeko.Modules.OwnerOnly.Services;
using Mewdeko.Services;
using Mewdeko.Services.Impl;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Mewdeko.Tests;

/// <summary>
///     Covers the custom bot rules that need neither Docker nor Ko-fi.
/// </summary>
[TestFixture]
public class CustomBotTests
{
    private const string Sample = """
                                  {
                                    "verification_token": "abc-123",
                                    "message_id": "m1",
                                    "timestamp": "2026-10-01T12:00:00Z",
                                    "type": "Subscription",
                                    "email": "Buyer@Example.com",
                                    "amount": "3.00",
                                    "is_subscription_payment": true,
                                    "is_first_subscription_payment": true,
                                    "kofi_transaction_id": "tx-1",
                                    "shop_items": null
                                  }
                                  """;

    private static KofiPayload Parse(string json)
    {
        var payload = KofiPayload.TryParse(json);
        Assert.That(payload, Is.Not.Null);
        return payload!;
    }

    /// <summary>
    ///     A subscription payload is read field by field.
    /// </summary>
    [Test]
    public void Parse_ReadsFields()
    {
        var payload = Parse(Sample);

        Assert.Multiple(() =>
        {
            Assert.That(payload.TransactionId, Is.EqualTo("tx-1"));
            Assert.That(payload.Email, Is.EqualTo("Buyer@Example.com"));
            Assert.That(payload.Type, Is.EqualTo("Subscription"));
            Assert.That(payload.IsFirstSubscriptionPayment, Is.True);
            Assert.That(payload.PaidAt, Is.EqualTo(new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc)));
        });
    }

    /// <summary>
    ///     Garbage and payloads without a transaction id or email are refused.
    /// </summary>
    [TestCase(null)]
    [TestCase("")]
    [TestCase("not json")]
    [TestCase("[]")]
    [TestCase("""{"type":"Subscription","email":"a@b.c"}""")]
    [TestCase("""{"type":"Subscription","kofi_transaction_id":"x"}""")]
    public void Parse_RejectsBadPayloads(string? json)
    {
        Assert.That(KofiPayload.TryParse(json), Is.Null);
    }

    /// <summary>
    ///     Only the exact token verifies, and an unset token never does.
    /// </summary>
    [Test]
    public void VerifyToken_ExactMatchOnly()
    {
        Assert.Multiple(() =>
        {
            Assert.That(KofiPayload.VerifyToken("abc-123", "abc-123"), Is.True);
            Assert.That(KofiPayload.VerifyToken("abc-124", "abc-123"), Is.False);
            Assert.That(KofiPayload.VerifyToken("", ""), Is.False);
            Assert.That(KofiPayload.VerifyToken("abc", null), Is.False);
            Assert.That(KofiPayload.VerifyToken(null, "abc"), Is.False);
        });
    }

    /// <summary>
    ///     Subscriptions extend a slot, shop orders create slots, donations grant nothing.
    /// </summary>
    [Test]
    public void Grant_ByType()
    {
        var subscription = Parse(Sample);
        var donation = Parse(Sample.Replace("\"Subscription\"", "\"Donation\"")
            .Replace("\"is_subscription_payment\": true", "\"is_subscription_payment\": false"));
        var order = Parse("""
                          {"type":"Shop Order","email":"a@b.c","kofi_transaction_id":"o1",
                           "shop_items":[{"direct_link_code":"bot","quantity":2},{"direct_link_code":"other","quantity":1}]}
                          """);

        Assert.Multiple(() =>
        {
            Assert.That(subscription.Grant(""), Is.EqualTo((true, 0)));
            Assert.That(donation.Grant(""), Is.EqualTo((false, 0)));
            Assert.That(order.Grant(""), Is.EqualTo((false, 3)));
            Assert.That(order.Grant("bot"), Is.EqualTo((false, 2)));
            Assert.That(order.Grant("nothing"), Is.EqualTo((false, 0)));
        });
    }

    /// <summary>
    ///     Time left on a slot is kept when a payment extends it, and an old slot restarts from the payment.
    /// </summary>
    [Test]
    public void Extend_KeepsRemainingTime()
    {
        var paid = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

        Assert.Multiple(() =>
        {
            Assert.That(CustomBotExpiry.Extend(null, paid, 31), Is.EqualTo(paid.AddDays(31)));
            Assert.That(CustomBotExpiry.Extend(paid.AddDays(10), paid, 31), Is.EqualTo(paid.AddDays(41)));
            Assert.That(CustomBotExpiry.Extend(paid.AddDays(-10), paid, 31), Is.EqualTo(paid.AddDays(31)));
        });
    }

    /// <summary>
    ///     Ko-fi slots run until the grace period counted from the payment ends, manual slots stop on their date,
    ///     and a slot without a date never stops.
    /// </summary>
    [Test]
    public void ShouldStop_UsesGraceForKofiOnly()
    {
        var expires = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);

        Assert.Multiple(() =>
        {
            Assert.That(CustomBotExpiry.StopAt(expires, "kofi", 35, 31), Is.EqualTo(expires.AddDays(4)));
            Assert.That(CustomBotExpiry.StopAt(expires, "manual", 35, 31), Is.EqualTo(expires));
            Assert.That(CustomBotExpiry.StopAt(null, "kofi", 35, 31), Is.Null);
            Assert.That(CustomBotExpiry.ShouldStop(expires, "kofi", 35, 31, expires.AddDays(3)), Is.False);
            Assert.That(CustomBotExpiry.ShouldStop(expires, "kofi", 35, 31, expires.AddDays(4)), Is.True);
            Assert.That(CustomBotExpiry.ShouldStop(expires, "manual", 35, 31, expires), Is.True);
            Assert.That(CustomBotExpiry.ShouldStop(null, "manual", 35, 31, expires.AddYears(5)), Is.False);
        });
    }

    /// <summary>
    ///     A stopped bot is kept for the retention period and then removed.
    /// </summary>
    [Test]
    public void ShouldRemove_AfterRetention()
    {
        var stopped = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);

        Assert.Multiple(() =>
        {
            Assert.That(CustomBotExpiry.ShouldRemove(null, stopped.AddYears(1)), Is.False);
            Assert.That(CustomBotExpiry.ShouldRemove(stopped, stopped.AddDays(13)), Is.False);
            Assert.That(CustomBotExpiry.ShouldRemove(stopped, stopped.AddDays(14)), Is.True);
        });
    }

    /// <summary>
    ///     The lowest free port is chosen, gaps are reused, and a full range yields nothing.
    /// </summary>
    [Test]
    public void Pick_LowestFreePort()
    {
        Assert.Multiple(() =>
        {
            Assert.That(CustomBotPorts.Pick(5100, 5102, []), Is.EqualTo(5100));
            Assert.That(CustomBotPorts.Pick(5100, 5102, [5100, 5102]), Is.EqualTo(5101));
            Assert.That(CustomBotPorts.Pick(5100, 5102, [5099, 5100]), Is.EqualTo(5101));
            Assert.That(CustomBotPorts.Pick(5100, 5101, [5100, 5101]), Is.Null);
        });
    }

    /// <summary>
    ///     Known secrets, token-shaped strings and connection string passwords are masked.
    /// </summary>
    [Test]
    public void Redact_MasksSecrets()
    {
        var token = string.Join('.', "MTIzNDU2Nzg5MDEyMzQ1Njc4", "GabcDE", "abcdefghijklmnopqrstuvwxyz0123456789");

        var line = CustomBotRedactor.Redact(
            $"login {token} key=hunter2hunter2 Host=db;Password=s3cretpw;Port=5432 plain text",
            ["hunter2hunter2"]);

        Assert.Multiple(() =>
        {
            Assert.That(line, Does.Not.Contain(token));
            Assert.That(line, Does.Not.Contain("hunter2hunter2"));
            Assert.That(line, Does.Not.Contain("s3cretpw"));
            Assert.That(line, Does.Contain("plain text"));
            Assert.That(line, Does.Contain("Port=5432"));
        });
    }

    /// <summary>
    ///     The invite asks for the bot and slash command scopes and the main bot's permissions.
    /// </summary>
    [Test]
    public void Invite_Url()
    {
        Assert.That(CustomBotInvite.Url(123),
            Is.EqualTo(
                "https://discord.com/oauth2/authorize?client_id=123&scope=bot%20applications.commands&permissions=66186303"));
    }

    /// <summary>
    ///     A token survives a round trip, a different key cannot read it, and the stored text hides it.
    /// </summary>
    [Test]
    public void Crypto_RoundTrip()
    {
        var key = CustomBotCrypto.DeriveKey("one");
        var stored = CustomBotCrypto.Encrypt("the-token-value", key);

        Assert.Multiple(() =>
        {
            Assert.That(stored, Does.Not.Contain("the-token-value"));
            Assert.That(CustomBotCrypto.Decrypt(stored, key), Is.EqualTo("the-token-value"));
            Assert.That(CustomBotCrypto.Decrypt(stored, CustomBotCrypto.DeriveKey("two")), Is.Null);
            Assert.That(CustomBotCrypto.Decrypt("garbage", key), Is.Null);
        });
    }

    /// <summary>
    ///     A path inside the container is mapped through the mount that contains it.
    /// </summary>
    [Test]
    public void MapHostPath_UsesLongestMount()
    {
        (string, string)[] mounts = [("/srv/bot", "/app"), ("/srv/custom", "/app/custombots")];

        Assert.Multiple(() =>
        {
            Assert.That(CustomBotService.MapHostPath("/app/custombots", mounts), Is.EqualTo("/srv/custom"));
            Assert.That(CustomBotService.MapHostPath("/app/other/x", mounts), Is.EqualTo("/srv/bot/other/x"));
            Assert.That(CustomBotService.MapHostPath("/elsewhere", mounts), Is.EqualTo("/elsewhere"));
        });
    }

    /// <summary>
    ///     Without a dashboard JWT nobody may act for a user, with one only that user or an owner may.
    /// </summary>
    [Test]
    public void CanActFor_DeniesWithoutJwt()
    {
        Func<ulong, bool> isOwner = id => id == 9;

        Assert.Multiple(() =>
        {
            Assert.That(DashboardCaller.CanActFor(null, 5, isOwner), Is.False);
            Assert.That(DashboardCaller.CanActFor(5, 5, isOwner), Is.True);
            Assert.That(DashboardCaller.CanActFor(6, 5, isOwner), Is.False);
            Assert.That(DashboardCaller.CanActFor(9, 5, isOwner), Is.True);
            Assert.That(DashboardCaller.IsOwner(null, isOwner), Is.False);
            Assert.That(DashboardCaller.IsOwner(6, isOwner), Is.False);
            Assert.That(DashboardCaller.IsOwner(9, isOwner), Is.True);
        });
    }

    /// <summary>
    ///     Only plain host names and IP addresses are accepted as an instance host.
    /// </summary>
    [TestCase(null, true)]
    [TestCase("", true)]
    [TestCase("localhost", true)]
    [TestCase("mewdeko-custom-3", true)]
    [TestCase("bot.example.com", true)]
    [TestCase("10.0.0.5", true)]
    [TestCase("fe80::1", true)]
    [TestCase("http://evil.example", false)]
    [TestCase("evil.example/path", false)]
    [TestCase("evil.example:80", false)]
    [TestCase("user@evil.example", false)]
    [TestCase("evil example", false)]
    [TestCase("-bad.example", false)]
    public void IsValidHost_AcceptsOnlyHosts(string? host, bool expected)
    {
        Assert.That(InstanceManagementService.IsValidHost(host), Is.EqualTo(expected));
    }

    /// <summary>
    ///     A host that is too long is refused.
    /// </summary>
    [Test]
    public void IsValidHost_RefusesLongHosts()
    {
        Assert.That(InstanceManagementService.IsValidHost(new string('a', 254)), Is.False);
    }

    /// <summary>
    ///     A child's credentials hold only the allowlisted keys and the values meant for that bot.
    /// </summary>
    [Test]
    public void ChildCredentials_UseAllowlist()
    {
        var master = JsonNode.Parse("""
                                    {
                                      "Token": "main-token",
                                      "VotesToken": "votes",
                                      "SentryDsn": "dsn",
                                      "DiscordClientSecret": "oauth",
                                      "KofiVerificationToken": "kofi",
                                      "CustomBotEncryptionKey": "key",
                                      "TwitchEventSubSecret": "eventsub",
                                      "LavalinkUrl": "http://lava:2333",
                                      "LavalinkPassword": "lavapw",
                                      "GoogleApiKey": "google"
                                    }
                                    """)!.AsObject();

        var child = CustomBotChildCredentials.Build(master, new CustomBotChildInputs("child-token", 5101,
            "mewdeko-custom-1", "child-api", "jwt-secret", "Host=childdb", "childredis:6379", [1UL, 2UL],
            "https://dash.example"));

        Assert.Multiple(() =>
        {
            Assert.That(child["Token"]!.GetValue<string>(), Is.EqualTo("child-token"));
            Assert.That(child["ApiKey"]!.GetValue<string>(), Is.EqualTo("child-api"));
            Assert.That(child["PsqlConnectionString"]!.GetValue<string>(), Is.EqualTo("Host=childdb"));
            Assert.That(child["RedisConnections"]!.GetValue<string>(), Is.EqualTo("childredis:6379"));
            Assert.That(child["IsMasterInstance"]!.GetValue<bool>(), Is.False);
            Assert.That(child["ApiPort"]!.GetValue<int>(), Is.EqualTo(5101));
            Assert.That(child["LavalinkPassword"]!.GetValue<string>(), Is.EqualTo("lavapw"));
            Assert.That(child["GoogleApiKey"]!.GetValue<string>(), Is.EqualTo("google"));
            Assert.That(child.ContainsKey("VotesToken"), Is.False);
            Assert.That(child.ContainsKey("SentryDsn"), Is.False);
            Assert.That(child.ContainsKey("DiscordClientSecret"), Is.False);
            Assert.That(child.ContainsKey("KofiVerificationToken"), Is.False);
            Assert.That(child.ContainsKey("CustomBotEncryptionKey"), Is.False);
            Assert.That(child.ContainsKey("TwitchEventSubSecret"), Is.False);
        });
    }

    /// <summary>
    ///     The dedicated connection setting wins when set and the master's own is used otherwise.
    /// </summary>
    [Test]
    public void ChildCredentials_PickDedicatedConnection()
    {
        Assert.Multiple(() =>
        {
            Assert.That(CustomBotChildCredentials.Pick("", "master"), Is.EqualTo("master"));
            Assert.That(CustomBotChildCredentials.Pick(null, "master"), Is.EqualTo("master"));
            Assert.That(CustomBotChildCredentials.Pick("limited", "master"), Is.EqualTo("limited"));
        });
    }

    /// <summary>
    ///     Hosts, user names and credentials inside connection strings and URLs are masked.
    /// </summary>
    [Test]
    public void Redact_MasksConnectionHostsAndUsers()
    {
        var hosts = CustomBotRedactor.ConnectionHosts(
            ["Host=pg.internal;Username=mewdeko;Password=pw12345", "redis.internal:6379,password=abc12345"]);
        var line = CustomBotRedactor.Redact(
            "failed Host=pg.internal;Username=mewdeko via redis://user:pw@redis.internal:6379 on redis.internal",
            hosts);

        Assert.Multiple(() =>
        {
            Assert.That(hosts, Does.Contain("pg.internal"));
            Assert.That(hosts, Does.Contain("redis.internal"));
            Assert.That(line, Does.Not.Contain("pg.internal"));
            Assert.That(line, Does.Not.Contain("redis.internal"));
            Assert.That(line, Does.Not.Contain("mewdeko"));
            Assert.That(line, Does.Not.Contain("user:pw"));
        });
    }

    /// <summary>
    ///     Rows written with the old derived key can still be read, and the fallback does not apply to garbage.
    /// </summary>
    [Test]
    public void DecryptWithFallback_ReadsLegacyRows()
    {
        var legacy = CustomBotCrypto.LegacyKey("api", "jwt");
        var current = CustomBotCrypto.DeriveKey("configured");
        var oldRow = CustomBotCrypto.Encrypt("tok", legacy);
        var newRow = CustomBotCrypto.Encrypt("tok", current);

        Assert.Multiple(() =>
        {
            Assert.That(CustomBotCrypto.DecryptWithFallback(oldRow, current, legacy), Is.EqualTo(("tok", true)));
            Assert.That(CustomBotCrypto.DecryptWithFallback(newRow, current, legacy), Is.EqualTo(("tok", false)));
            Assert.That(CustomBotCrypto.DecryptWithFallback(oldRow, current, null).Plain, Is.Null);
            Assert.That(CustomBotCrypto.DecryptWithFallback("garbage", current, legacy).Plain, Is.Null);
        });
    }

    /// <summary>
    ///     A user gets a fixed number of claim attempts per window.
    /// </summary>
    [Test]
    public void ClaimLimiter_CapsAttempts()
    {
        var limiter = new CustomBotClaimLimiter(2, TimeSpan.FromMinutes(10));
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        Assert.Multiple(() =>
        {
            Assert.That(limiter.TryAttempt(1, start), Is.True);
            Assert.That(limiter.TryAttempt(1, start.AddMinutes(1)), Is.True);
            Assert.That(limiter.TryAttempt(1, start.AddMinutes(2)), Is.False);
            Assert.That(limiter.TryAttempt(2, start.AddMinutes(2)), Is.True);
            Assert.That(limiter.TryAttempt(1, start.AddMinutes(11)), Is.True);
        });
    }

    /// <summary>
    ///     API keys are compared exactly and empty keys never match.
    /// </summary>
    [Test]
    public void ApiKeyMatches_ExactOnly()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ApiKeyValidation.Matches("secret", "secret"), Is.True);
            Assert.That(ApiKeyValidation.Matches("secret", "Secret"), Is.False);
            Assert.That(ApiKeyValidation.Matches("secret", "secre"), Is.False);
            Assert.That(ApiKeyValidation.Matches("", ""), Is.False);
            Assert.That(ApiKeyValidation.Matches(null, "x"), Is.False);
            Assert.That(ApiKeyValidation.Matches("x", null), Is.False);
        });
    }

    /// <summary>
    ///     A token minted for the master verifies with the shared secret and fails with another one.
    /// </summary>
    [Test]
    public async Task MintToken_VerifiesWithSharedSecret()
    {
        const string secret = "0123456789abcdef0123456789abcdef";
        var token = DashJwtConstants.MintToken(secret, 42, TimeSpan.FromMinutes(1));
        var handler = new JsonWebTokenHandler();

        TokenValidationParameters Parameters(string key)
        {
            return new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = DashJwtConstants.Issuer,
                ValidateAudience = true,
                ValidAudience = DashJwtConstants.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                ValidateLifetime = true
            };
        }

        var good = await handler.ValidateTokenAsync(token, Parameters(secret));
        var bad = await handler.ValidateTokenAsync(token, Parameters("fedcba9876543210fedcba9876543210"));

        Assert.Multiple(() =>
        {
            Assert.That(good.IsValid, Is.True);
            Assert.That(good.Claims[DashJwtConstants.UserIdClaim], Is.EqualTo("42"));
            Assert.That(good.Claims[DashJwtConstants.ScopeClaim], Is.EqualTo(DashJwtConstants.BackendScope));
            Assert.That(bad.IsValid, Is.False);
        });
    }

    /// <summary>
    ///     Generated child keys are long, non-empty and different every time.
    /// </summary>
    [Test]
    public void GenerateApiKey_IsRandomAndNonEmpty()
    {
        var keys = Enumerable.Range(0, 20).Select(_ => CustomBotCrypto.GenerateApiKey()).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(keys, Has.All.Not.Empty);
            Assert.That(keys, Has.All.Length.GreaterThanOrEqualTo(64));
            Assert.That(keys.Distinct().Count(), Is.EqualTo(keys.Count));
        });
    }

    /// <summary>
    ///     A child with no stored key, or an unreadable one, is called with the master key, and one with a key
    ///     is called with its own.
    /// </summary>
    [Test]
    public void ResolveApiKey_FallsBackToMasterKey()
    {
        var key = CustomBotCrypto.DeriveKey("configured");
        var legacy = CustomBotCrypto.LegacyKey("api", "jwt");
        var stored = CustomBotCrypto.Encrypt("child-key", key);

        Assert.Multiple(() =>
        {
            Assert.That(CustomBotCrypto.ResolveApiKey(null, key, legacy, "master"), Is.EqualTo("master"));
            Assert.That(CustomBotCrypto.ResolveApiKey(stored, null, legacy, "master"), Is.EqualTo("master"));
            Assert.That(CustomBotCrypto.ResolveApiKey("garbage", key, legacy, "master"), Is.EqualTo("master"));
            Assert.That(CustomBotCrypto.ResolveApiKey(stored, key, legacy, "master"), Is.EqualTo("child-key"));
        });
    }

    /// <summary>
    ///     The key exchange answers only the master, and only a request with the header and no user token.
    /// </summary>
    [Test]
    public void CanExchangeKeys_RefusesEveryoneButTheDashboardServer()
    {
        Assert.Multiple(() =>
        {
            Assert.That(InstanceManagementController.CanExchangeKeys(true, false, "1"), Is.True);
            Assert.That(InstanceManagementController.CanExchangeKeys(false, false, "1"), Is.False);
            Assert.That(InstanceManagementController.CanExchangeKeys(true, true, "1"), Is.False);
            Assert.That(InstanceManagementController.CanExchangeKeys(true, false, null), Is.False);
            Assert.That(InstanceManagementController.CanExchangeKeys(true, false, "0"), Is.False);
        });
    }
}
