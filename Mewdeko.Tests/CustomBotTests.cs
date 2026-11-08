using Mewdeko.Services;

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
}
