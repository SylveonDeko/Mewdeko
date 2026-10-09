using Mewdeko.Modules.Import.Common;
using Mewdeko.Modules.Import.Services;
using Mewdeko.Modules.Xp.Models;
using Mewdeko.Modules.Xp.Services;

namespace Mewdeko.Tests;

/// <summary>
///     Verifies the imported bots' level curves, the export file readers, and how imported XP is converted between
///     curves.
/// </summary>
[TestFixture]
public class DataImportTests
{
    /// <summary>
    ///     A real MEE6 member: level 86 with 25,130 of 41,380 XP into the level and 1,258,155 XP in total.
    /// </summary>
    [Test]
    public void Mee6CurveMatchesMee6()
    {
        Assert.Multiple(() =>
        {
            Assert.That(XpCalculator.CalculateLevel(1258155, XpCurveType.Mee6), Is.EqualTo(86));
            Assert.That(XpCalculator.CalculateXpForLevel(86, XpCurveType.Mee6), Is.EqualTo(1258155 - 25130));
            Assert.That(XpCalculator.StepCurveIncrement(86, XpCurveType.Mee6), Is.EqualTo(41380));
            Assert.That(XpCalculator.CalculateLevel(99, XpCurveType.Mee6), Is.EqualTo(0));
            Assert.That(XpCalculator.CalculateLevel(100, XpCurveType.Mee6), Is.EqualTo(1));
        });
    }

    [TestCase(XpCurveType.Mee6)]
    [TestCase(XpCurveType.Lurkr)]
    [TestCase(XpCurveType.Amari)]
    public void StepCurvesRoundTrip(XpCurveType curve)
    {
        for (var level = 0; level < 300; level++)
        {
            var xp = XpCalculator.CalculateXpForLevel(level, curve);
            Assert.That(XpCalculator.CalculateLevel(xp, curve), Is.EqualTo(level));
            if (level > 0)
                Assert.That(XpCalculator.CalculateLevel(xp - 1, curve), Is.EqualTo(level - 1));
        }
    }

    [Test]
    public void ReadsLurkrExport()
    {
        const string file = """
                            { "levels": [
                              { "messageCount": 10, "user": { "username": "a" }, "userId": "111111111111111111", "xp": 500, "level": 3 },
                              { "messageCount": 2, "user": { "username": "b" }, "userId": "222222222222222222", "xp": 20, "level": 0 }
                            ] }
                            """;

        var data = ImportFileReader.Read(ImportSource.Lurkr, file);

        Assert.Multiple(() =>
        {
            Assert.That(data.Kind, Is.EqualTo(ImportKind.Xp));
            Assert.That(data.Members, Has.Count.EqualTo(2));
            Assert.That(data.Members[0].UserId, Is.EqualTo(111111111111111111UL));
            Assert.That(data.Members[0].Xp, Is.EqualTo(500));
            Assert.That(data.Members[0].Messages, Is.EqualTo(10));
            Assert.That(data.Members[0].Name, Is.EqualTo("a"));
            Assert.That(data.NativeCurve, Is.EqualTo(XpCurveType.Lurkr));
        });
    }

    [Test]
    public void ReadsPolarisFormats()
    {
        const string json = """
                            [
                            	{ "id": "111111111111111111", "xp": 1200 },
                            	{ "id": "222222222222222222", "xp": 50 }
                            ]
                            """;
        const string csv = "ID,Total XP\n111111111111111111,1200\n222222222222222222,50";
        const string text = "111111111111111111 - 1200\n222222222222222222 - 50";
        const string everything = """
                                  { "settings": { "curve": { "3": 0, "2": 0, "1": 100 }, "rounding": 1,
                                    "rewards": [ { "id": "333333333333333333", "level": 5 } ] },
                                    "users": { "111111111111111111": { "xp": 1200 }, "222222222222222222": { "xp": 50 } } }
                                  """;

        foreach (var file in new[] { json, csv, text })
        {
            var data = ImportFileReader.Read(ImportSource.Polaris, file);
            Assert.That(data.Members.Select(x => x.Xp), Is.EquivalentTo(new long?[] { 1200, 50 }));
            Assert.That(data.SourceXpForLevel!(1), Is.EqualTo(200));
        }

        var full = ImportFileReader.Read(ImportSource.Polaris, everything);
        Assert.Multiple(() =>
        {
            Assert.That(full.Members, Has.Count.EqualTo(2));
            Assert.That(full.RoleRewards.Single().RoleId, Is.EqualTo(333333333333333333UL));
            Assert.That(full.SourceXpForLevel!(12), Is.EqualTo(1200));
            Assert.That(DataImportService.LevelFor(1200, full.SourceXpForLevel), Is.EqualTo(12));
        });
    }

    [Test]
    public void ReadsArcaneAndGenericFiles()
    {
        const string arcane = """
                              [ { "avatar": "x", "level": 14, "messageCount": 300, "tag": "c", "userId": "111111111111111111", "xp": 90 } ]
                              """;
        const string csv = "user_id;level\n111111111111111111;7\n111111111111111111;9";
        const string balances = "User ID,Cash,Bank\n111111111111111111,\"1,000\",250";

        var arcaneData = ImportFileReader.Read(ImportSource.Arcane, arcane);
        var csvData = ImportFileReader.Read(ImportSource.File, csv);
        var balanceData = ImportFileReader.Read(ImportSource.File, balances);

        Assert.Multiple(() =>
        {
            Assert.That(arcaneData.Members.Single().Level, Is.EqualTo(14));
            Assert.That(arcaneData.SourceXpForLevel, Is.Null);
            Assert.That(csvData.Members.Single().Level, Is.EqualTo(9));
            Assert.That(balanceData.Kind, Is.EqualTo(ImportKind.Currency));
            Assert.That(balanceData.Members.Single().Cash, Is.EqualTo(1000));
            Assert.That(balanceData.Members.Single().Bank, Is.EqualTo(250));
        });
    }

    [Test]
    public void RejectsUnreadableFiles()
    {
        Assert.Multiple(() =>
        {
            Assert.That(() => ImportFileReader.Read(ImportSource.File, "hello there"),
                Throws.TypeOf<ImportException>());
            Assert.That(() => ImportFileReader.Read(ImportSource.File, "{ \"nothing\": 1 }"),
                Throws.TypeOf<ImportException>());
            Assert.That(() => ImportFileReader.Read(ImportSource.File, "[]"), Throws.TypeOf<ImportException>());
        });
    }

    /// <summary>
    ///     A MEE6 member converted onto another curve keeps their level and how far through it they were.
    /// </summary>
    [TestCase(XpCurveType.Standard)]
    [TestCase(XpCurveType.Linear)]
    [TestCase(XpCurveType.Accelerated)]
    [TestCase(XpCurveType.Decelerated)]
    [TestCase(XpCurveType.Legacy)]
    [TestCase(XpCurveType.Lurkr)]
    public void ConversionKeepsLevelAndProgress(XpCurveType target)
    {
        var data = new ImportDataset
        {
            NativeCurve = XpCurveType.Mee6,
            SourceXpForLevel = level => XpCalculator.CalculateXpForLevel(level, XpCurveType.Mee6)
        };

        var halfway = XpCalculator.CalculateXpForLevel(20, XpCurveType.Mee6) +
                      XpCalculator.StepCurveIncrement(20, XpCurveType.Mee6) / 2;
        var member = new ImportedMember(1, halfway, null, null, null, null, null);

        var converted = DataImportService.ConvertXp(member, data, target, false);
        var low = DataImportService.MinXpForLevel(20, target);
        var high = DataImportService.MinXpForLevel(21, target);

        Assert.Multiple(() =>
        {
            Assert.That(XpCalculator.CalculateLevel(converted, target), Is.EqualTo(20));
            Assert.That((double)(converted - low) / (high - low), Is.EqualTo(0.5).Within(0.02));
            Assert.That(DataImportService.ConvertXp(member, data, XpCurveType.Mee6, true), Is.EqualTo(halfway));
        });
    }

    [Test]
    public void LevelOnlyMembersLandOnTheirLevel()
    {
        var data = new ImportDataset();
        var member = new ImportedMember(1, null, 14, null, null, null, null);
        var raw = new ImportedMember(2, 5000, null, null, null, null, null);

        Assert.Multiple(() =>
        {
            Assert.That(XpCalculator.CalculateLevel(DataImportService.ConvertXp(member, data, XpCurveType.Standard,
                false), XpCurveType.Standard), Is.EqualTo(14));
            Assert.That(DataImportService.ConvertXp(raw, data, XpCurveType.Standard, false), Is.EqualTo(5000));
        });
    }

    [Test]
    public void TranslatesMee6Placeholders()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Mee6Text.Translate("Welcome {user} to {server.name}, member #{server.member_count}"),
                Is.EqualTo("Welcome %user.mention% to %server.name%, member #%server.members%"));
            Assert.That(Mee6Text.Translate("GG {user.name}, you hit {level}!", Mee6TextContext.LevelUp),
                Is.EqualTo("GG %xp.user.name%, you hit %xp.level.new%!"));
            Assert.That(Mee6Text.Translate("{streamer} is live on {link}", Mee6TextContext.Stream),
                Is.EqualTo("%stream.name% is live on %stream.url%"));
            Assert.That(Mee6Text.Translate("You said {...} and {unknown}"),
                Is.EqualTo("You said %target% and {unknown}"));
        });
    }

    [Test]
    public void ResolvesEmojiAndChannelShorthand()
    {
        var emojis = new Dictionary<string, string> { ["Wave"] = "<:Wave:1>" };
        var channels = new Dictionary<string, ulong> { ["general"] = 2 };

        Assert.That(Mee6Text.ResolveMentions("Hi :Wave: see #general, keep <:Wave:1> and :Missing:", emojis, channels),
            Is.EqualTo("Hi <:Wave:1> see <#2>, keep <:Wave:1> and :Missing:"));
    }

    [Test]
    public void ReadsMee6SettingsExport()
    {
        const string export = """
                              { "guildId": "10", "responses": {
                                "plugins/welcome/config/10": { "status": 200, "body": { "enabled": true,
                                  "public_welcome_enabled": true, "public_welcome_channel_id": "20",
                                  "public_welcome_message": "Hi {user}", "public_welcome_in_embed": false,
                                  "roles_enabled": true, "roles": ["30"], "goodbye_enabled": false } },
                                "plugins/levels/config/10": { "status": 200, "body": { "enabled": true,
                                  "level_up_announcement_type": 3, "level_up_announcement_channel": "40",
                                  "level_up_announcement_message": "GG {player}", "xp_rate": 1.5, "role_rewards_type": 1,
                                  "banned_channels": ["50"], "role_rewards": [ { "rank": 5, "role": "60" } ] } },
                                "plugins/moderator/config/10": { "status": 200, "body": { "enabled": true,
                                  "check_bad_words": { "sanction": 3, "bad_words": ["Bad", "bad", "worse"] },
                                  "check_invites": { "sanction": 1 }, "check_mass_mentions": { "sanction": 2, "threshold": 4 },
                                  "automated_actions": [ { "threshold": 3, "type": "mute", "duration": 600 } ] } },
                                "plugins/twitch/config/10": { "status": 200, "body": { "enabled": true } },
                                "plugins/twitch/guilds/10/streamers": { "status": 200, "body": { "streamers": [
                                  { "display_name": "Someone", "announcement_channel_id": "70", "announcement_message": "{streamer} live" } ] } },
                                "plugins/commands/guilds/10/commands": { "status": 200, "body": { "commands": [
                                  { "id": "Rules", "enabled": true, "actions": [ { "type": "respond", "direct_response": true,
                                    "messages": [ { "content": "Read them {user}", "embeds": [] } ] } ] } ] } },
                                "plugins/reaction_roles/guilds/10/messages": { "status": 200, "body": [
                                  { "status": "published", "kind": "reaction", "channel_id": "80", "message_id": "90", "name": "Pick",
                                    "reactions": [ { "emoji_id": null, "emoji_name": "🍎", "roles": ["100"] } ] },
                                  { "status": "draft", "kind": "button", "channel_id": "80", "buttons": [] } ] },
                                "plugins/economy/config/10": { "status": 403, "body": "nope" }
                              } }
                              """;

        Assert.That(Mee6SettingsReader.IsExport(export), Is.True);
        var plan = Mee6SettingsReader.Read(export);

        Assert.Multiple(() =>
        {
            Assert.That(plan.GuildId, Is.EqualTo(10UL));
            Assert.That(plan.Welcome!.ChannelMessage, Is.EqualTo("Hi %user.mention%"));
            Assert.That(plan.Welcome.JoinRoles, Is.EqualTo(new[] { 30UL }));
            Assert.That(plan.Levels!.Message, Is.EqualTo("GG %xp.user.mention%"));
            Assert.That(plan.Levels.XpRate, Is.EqualTo(1.5));
            Assert.That(plan.Levels.RemovePreviousRewards, Is.True);
            Assert.That(plan.Levels.RoleRewards.Single(), Is.EqualTo(new ImportedRoleReward(5, 60)));
            Assert.That(plan.AutoMod!.BadWords, Is.EqualTo(new[] { "bad", "worse" }));
            Assert.That(plan.AutoMod.WarnOnWords, Is.True);
            Assert.That(plan.AutoMod.WarnOnInvites, Is.False);
            Assert.That(plan.AutoMod.MentionThreshold, Is.EqualTo(4));
            Assert.That(plan.AutoMod.Punishments.Single(), Is.EqualTo(new Mee6Punishment(3, "mute", 10)));
            Assert.That(plan.Streams.Single(), Is.EqualTo(new Mee6StreamPlan("someone", 70, "%stream.name% live")));
            Assert.That(plan.Commands.Single().Name, Is.EqualTo("rules"));
            Assert.That(plan.Commands.Single().Private, Is.True);
            Assert.That(plan.RoleMessages.Single().Options.Single().EmojiName, Is.EqualTo("🍎"));
            Assert.That(plan.Economy, Is.Null);
            Assert.That(plan.Birthdays, Is.Null);
        });
    }
}
