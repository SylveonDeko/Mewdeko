using System.Net;
using DataModel;
using Discord;
using Mewdeko.Modules.Automations.Common;

namespace Mewdeko.Tests;

[TestFixture]
public class AutomationConditionTests
{
    private static AutomationEventData Message(string? content = "hello world", ulong channel = 10,
        ulong category = 5, ulong user = 1, ulong[]? roles = null, string[]? attachments = null, bool reply = false,
        bool known = true) => new()
    {
        Trigger = AutomationTriggerType.MessageSent,
        GuildId = 1,
        ChannelId = channel,
        CategoryId = category,
        UserId = user,
        RoleIds = roles ?? [],
        MessageKnown = known,
        Content = content,
        AttachmentTypes = attachments ?? [],
        IsReply = reply
    };

    private static bool Passes(AutomationNode condition, AutomationEventData data) =>
        AutomationConditionEvaluator.Passes(condition, data);

    [Test]
    public void InChannelsMatchesChannelOrCategory()
    {
        var condition = AutomationNode.Create("InChannels", ("channelIds", new ulong[] { 10, 99 }));
        Assert.That(Passes(condition, Message(channel: 10)), Is.True);
        Assert.That(Passes(condition, Message(channel: 11)), Is.False);

        var category = AutomationNode.Create("InChannels", ("channelIds", new ulong[] { 5 }));
        Assert.That(Passes(category, Message(channel: 11, category: 5)), Is.True);
    }

    [Test]
    public void InChannelsMatchesThreadParent()
    {
        var data = new AutomationEventData
        {
            ChannelId = 100, ParentChannelId = 10
        };

        Assert.That(Passes(AutomationNode.Create("InChannels", ("channelIds", new ulong[] { 10 })), data), Is.True);
    }

    [Test]
    public void RoleConditions()
    {
        var data = Message(roles: [1, 2]);
        var roles = new ulong[] { 2, 3 };

        Assert.That(Passes(AutomationNode.Create("HasAnyRole", ("roleIds", roles)), data), Is.True);
        Assert.That(Passes(AutomationNode.Create("HasAllRoles", ("roleIds", roles)), data), Is.False);
        Assert.That(Passes(AutomationNode.Create("HasAllRoles", ("roleIds", new ulong[] { 1, 2 })), data), Is.True);
        Assert.That(Passes(AutomationNode.Create("HasNoneOfRoles", ("roleIds", roles)), data), Is.False);
        Assert.That(Passes(AutomationNode.Create("HasNoneOfRoles", ("roleIds", new ulong[] { 7 })), data), Is.True);
        Assert.That(Passes(AutomationNode.Create("LacksAnyRole", ("roleIds", roles)), data), Is.True);
        Assert.That(Passes(AutomationNode.Create("LacksAnyRole", ("roleIds", new ulong[] { 1, 2 })), data), Is.False);
    }

    [Test]
    public void FromUsers()
    {
        var condition = AutomationNode.Create("FromUsers", ("userIds", new ulong[] { 1, 2 }));
        Assert.That(Passes(condition, Message(user: 2)), Is.True);
        Assert.That(Passes(condition, Message(user: 3)), Is.False);
    }

    [Test]
    public void ContainsIgnoresCaseUnlessAsked()
    {
        var loose = AutomationNode.Create("MessageContains", ("text", "WORLD"));
        var strict = AutomationNode.Create("MessageContains", ("text", "WORLD"), ("caseSensitive", true));
        Assert.That(Passes(loose, Message()), Is.True);
        Assert.That(Passes(strict, Message()), Is.False);
    }

    [Test]
    public void EqualsComparesWholeText()
    {
        var condition = AutomationNode.Create("MessageEquals", ("text", "Hello World"));
        Assert.That(Passes(condition, Message("hello world")), Is.True);
        Assert.That(Passes(condition, Message("hello world!")), Is.False);
    }

    [Test]
    public void DoesNotContain()
    {
        var condition = AutomationNode.Create("MessageDoesNotContain", ("text", "spam"));
        Assert.That(Passes(condition, Message("fine")), Is.True);
        Assert.That(Passes(condition, Message("SPAM here")), Is.False);
    }

    [Test]
    public void RegexMatches()
    {
        var condition = AutomationNode.Create("MessageMatchesRegex", ("pattern", @"^\d{3}$"));
        Assert.That(Passes(condition, Message("123")), Is.True);
        Assert.That(Passes(condition, Message("1234")), Is.False);
    }

    [Test]
    public void BrokenRegexNeverMatches()
    {
        var condition = AutomationNode.Create("MessageMatchesRegex", ("pattern", "("));
        Assert.That(Passes(condition, Message("(")), Is.False);
    }

    [Test]
    public void SlowRegexTimesOutInsteadOfHanging()
    {
        var condition = AutomationNode.Create("MessageMatchesRegex", ("pattern", "^(a+)+$"));
        Assert.That(Passes(condition, Message(new string('a', 40) + "!")), Is.False);
    }

    [Test]
    public void AttachmentTypes()
    {
        var condition = AutomationNode.Create("HasAttachment", ("types", new[] { "Image", "Video" }));
        Assert.That(Passes(condition, Message(attachments: ["image"])), Is.True);
        Assert.That(Passes(condition, Message(attachments: ["Audio"])), Is.False);
        Assert.That(Passes(condition, Message()), Is.False);
    }

    [Test]
    public void TextAndReplyConditions()
    {
        Assert.That(Passes(AutomationNode.Create("HasText"), Message("hi")), Is.True);
        Assert.That(Passes(AutomationNode.Create("HasText"), Message("  ")), Is.False);
        Assert.That(Passes(AutomationNode.Create("IsReply"), Message(reply: true)), Is.True);
        Assert.That(Passes(AutomationNode.Create("IsReply"), Message()), Is.False);
        Assert.That(Passes(AutomationNode.Create("IsNotReply"), Message()), Is.True);
        Assert.That(Passes(AutomationNode.Create("IsNotReply"), Message(reply: true)), Is.False);
    }

    [Test]
    public void UnknownMessageFailsEveryMessageCondition()
    {
        var data = Message(null, known: false);
        Assert.That(Passes(AutomationNode.Create("MessageDoesNotContain", ("text", "x")), data), Is.False);
        Assert.That(Passes(AutomationNode.Create("IsNotReply"), data), Is.False);
        Assert.That(Passes(AutomationNode.Create("HasText"), data), Is.False);
    }

    [Test]
    public void ReactionEmojiMatchesUnicodeAndCustom()
    {
        var data = new AutomationEventData
        {
            EmojiKey = "⭐"
        };

        Assert.That(Passes(AutomationNode.Create("ReactionEmojiIn", ("emojis", new[] { "⭐", "👍" })), data), Is.True);
        Assert.That(Passes(AutomationNode.Create("ReactionEmojiIn", ("emojis", new[] { "👍" })), data), Is.False);

        var custom = new AutomationEventData
        {
            EmojiKey = "123456789012345678"
        };

        Assert.That(
            Passes(AutomationNode.Create("ReactionEmojiIn", ("emojis", new[] { "<:star:123456789012345678>" })), custom),
            Is.True);
    }

    [TestCase("GreaterOrEqual", 3, 3, true)]
    [TestCase("GreaterOrEqual", 3, 2, false)]
    [TestCase("LessOrEqual", 3, 3, true)]
    [TestCase("LessOrEqual", 3, 4, false)]
    [TestCase("Equal", 3, 3, true)]
    [TestCase("Equal", 3, 4, false)]
    public void ReactionCount(string comparison, int count, int actual, bool expected)
    {
        var condition = AutomationNode.Create("ReactionCount", ("comparison", comparison), ("count", count));
        Assert.That(Passes(condition, new AutomationEventData
        {
            ReactionCount = actual
        }), Is.EqualTo(expected));
    }

    [Test]
    public void ButtonCustomIdIsExact()
    {
        var condition = AutomationNode.Create("ButtonCustomIdIs", ("customId", "role-1"));
        Assert.That(Passes(condition, new AutomationEventData
        {
            ButtonCustomId = "role-1"
        }), Is.True);
        Assert.That(Passes(condition, new AutomationEventData
        {
            ButtonCustomId = "Role-1"
        }), Is.False);
        Assert.That(Passes(condition, new AutomationEventData()), Is.False);
    }

    [Test]
    public void TriggerRoleIs()
    {
        var condition = AutomationNode.Create("TriggerRoleIs", ("roleIds", new ulong[] { 8 }));
        Assert.That(Passes(condition, new AutomationEventData
        {
            ChangedRoleId = 8
        }), Is.True);
        Assert.That(Passes(condition, new AutomationEventData()), Is.False);
    }

    [Test]
    public void FirstFailureReportsThePositionOrMinusOne()
    {
        var data = Message("hello");
        AutomationNode[] conditions =
        [
            AutomationNode.Create("HasText"),
            AutomationNode.Create("MessageContains", ("text", "nope")),
            AutomationNode.Create("IsReply")
        ];

        Assert.That(AutomationConditionEvaluator.FirstFailure(conditions, data), Is.EqualTo(1));
        Assert.That(AutomationConditionEvaluator.FirstFailure(conditions.Take(1).ToList(), data), Is.EqualTo(-1));
        Assert.That(AutomationConditionEvaluator.FirstFailure([], data), Is.EqualTo(-1));
    }

    [Test]
    public void UnknownConditionNeverPasses()
    {
        Assert.That(Passes(AutomationNode.Create("Nonsense"), Message()), Is.False);
    }
}

[TestFixture]
public class AutomationValidationTests
{
    private static List<AutomationValidationError> Validate(AutomationTriggerType trigger,
        AutomationNode[]? conditions = null, AutomationNode[]? actions = null, string name = "Test",
        int cooldown = 0) =>
        AutomationValidator.Validate(name, trigger, conditions ?? [],
            actions ?? [AutomationNode.Create("SendMessage", ("text", "hi"))], cooldown);

    [Test]
    public void SimpleAutomationIsValid()
    {
        Assert.That(Validate(AutomationTriggerType.MessageSent), Is.Empty);
    }

    [Test]
    public void NameIsRequired()
    {
        Assert.That(Validate(AutomationTriggerType.MessageSent, name: " ").Select(x => x.Path), Does.Contain("name"));
        Assert.That(Validate(AutomationTriggerType.MessageSent, name: new string('x', 101)).Select(x => x.Path),
            Does.Contain("name"));
    }

    [Test]
    public void AtLeastOneActionIsRequired()
    {
        var errors = AutomationValidator.Validate("Test", AutomationTriggerType.MessageSent, [], [], 0);
        Assert.That(errors.Select(x => x.Path), Does.Contain("actions"));
    }

    [Test]
    public void TooManyActionsAndConditionsAreRefused()
    {
        var actions = Enumerable.Range(0, AutomationLimits.MaxActions + 1)
            .Select(_ => AutomationNode.Create("PinMessage")).ToArray();
        var conditions = Enumerable.Range(0, AutomationLimits.MaxConditions + 1)
            .Select(_ => AutomationNode.Create("HasText")).ToArray();

        var errors = Validate(AutomationTriggerType.MessageSent, conditions, actions);
        Assert.That(errors.Select(x => x.Path), Does.Contain("actions").And.Contain("conditions"));
    }

    [Test]
    public void CooldownIsBounded()
    {
        Assert.That(Validate(AutomationTriggerType.MessageSent, cooldown: -1), Is.Not.Empty);
        Assert.That(Validate(AutomationTriggerType.MessageSent, cooldown: AutomationLimits.MaxCooldownSeconds + 1),
            Is.Not.Empty);
        Assert.That(Validate(AutomationTriggerType.MessageSent, cooldown: 60), Is.Empty);
    }

    [Test]
    public void UnknownTypesAndFieldsAreRefused()
    {
        var errors = Validate(AutomationTriggerType.MessageSent, [AutomationNode.Create("Nope")],
            [AutomationNode.Create("SendMessage", ("text", "hi"), ("typo", 1))]);

        Assert.That(errors.Select(x => x.Path), Does.Contain("conditions[0].type").And.Contain("actions[0].typo"));
    }

    [Test]
    public void ConditionsNeedWhatTheTriggerProvides()
    {
        var errors = Validate(AutomationTriggerType.MemberJoined,
            [AutomationNode.Create("MessageContains", ("text", "x"))]);

        Assert.That(errors.Select(x => x.Path), Does.Contain("conditions[0].type"));
        Assert.That(Validate(AutomationTriggerType.ReactionAdded,
            [AutomationNode.Create("ReactionCount", ("comparison", "Equal"), ("count", 3))]), Is.Empty);
    }

    [Test]
    public void ActionsNeedWhatTheTriggerProvides()
    {
        var errors = Validate(AutomationTriggerType.MemberJoined, actions: [AutomationNode.Create("DeleteMessage")]);
        Assert.That(errors.Select(x => x.Path), Does.Contain("actions[0].type"));
    }

    [Test]
    public void SendMessageNeedsContentAndAChannelWhenTheTriggerHasNone()
    {
        var empty = Validate(AutomationTriggerType.MessageSent, actions: [AutomationNode.Create("SendMessage")]);
        Assert.That(empty.Select(x => x.Path), Does.Contain("actions[0]"));

        var noChannel = Validate(AutomationTriggerType.MemberJoined,
            actions: [AutomationNode.Create("SendMessage", ("text", "hi"))]);
        Assert.That(noChannel.Select(x => x.Path), Does.Contain("actions[0].channelId"));

        var channel = Validate(AutomationTriggerType.MemberJoined,
            actions: [AutomationNode.Create("SendMessage", ("text", "hi"), ("channelId", 5UL))]);
        Assert.That(channel, Is.Empty);
    }

    [Test]
    public void EphemeralOnlyForButtonReplies()
    {
        var message = Validate(AutomationTriggerType.MessageSent,
            actions: [AutomationNode.Create("SendMessage", ("text", "hi"), ("ephemeral", true))]);
        Assert.That(message.Select(x => x.Path), Does.Contain("actions[0].ephemeral"));

        var button = Validate(AutomationTriggerType.ButtonClicked,
            actions: [AutomationNode.Create("SendMessage", ("text", "hi"), ("ephemeral", true))]);
        Assert.That(button, Is.Empty);

        var withChannel = Validate(AutomationTriggerType.ButtonClicked,
            actions:
            [
                AutomationNode.Create("SendMessage", ("text", "hi"), ("ephemeral", true), ("channelId", 5UL))
            ]);
        Assert.That(withChannel, Is.Not.Empty);
    }

    [Test]
    public void ThreadActionsNeedAThread()
    {
        var before = Validate(AutomationTriggerType.MessageSent,
            actions: [AutomationNode.Create("SendInThread", ("text", "hi"))]);
        Assert.That(before, Is.Not.Empty);

        var after = Validate(AutomationTriggerType.MessageSent,
            actions:
            [
                AutomationNode.Create("CreateThread", ("name", "talk")),
                AutomationNode.Create("SendInThread", ("text", "hi"))
            ]);
        Assert.That(after, Is.Empty);

        var owned = Validate(AutomationTriggerType.ThreadCreated,
            actions: [AutomationNode.Create("AddThreadMembers", ("addTriggerUser", true))]);
        Assert.That(owned, Is.Empty);
    }

    [Test]
    public void WaitsAreCapped()
    {
        var tooLong = Validate(AutomationTriggerType.MessageSent,
            actions: [AutomationNode.Create("Wait", ("seconds", AutomationLimits.MaxWaitSeconds + 1))]);
        Assert.That(tooLong, Is.Not.Empty);

        var tooMany = Validate(AutomationTriggerType.MessageSent,
            actions: Enumerable.Range(0, 3).Select(_ => AutomationNode.Create("Wait", ("seconds", 60))).ToArray());
        Assert.That(tooMany.Select(x => x.Path), Does.Contain("actions"));
    }

    [Test]
    public void AmountsAreBounded()
    {
        Assert.That(Validate(AutomationTriggerType.MessageSent,
            actions: [AutomationNode.Create("GiveCoins", ("amount", 0))]), Is.Not.Empty);
        Assert.That(Validate(AutomationTriggerType.MessageSent,
            actions: [AutomationNode.Create("GiveXp", ("amount", AutomationLimits.MaxRewardAmount + 1))]), Is.Not.Empty);
        Assert.That(Validate(AutomationTriggerType.MessageSent,
            actions: [AutomationNode.Create("GiveXp", ("amount", 50))]), Is.Empty);
    }

    [Test]
    public void MessageAuthorTargetNeedsAMessage()
    {
        var errors = Validate(AutomationTriggerType.MemberJoined,
            actions: [AutomationNode.Create("GiveCoins", ("amount", 5), ("target", "MessageAuthor"))]);
        Assert.That(errors.Select(x => x.Path), Does.Contain("actions[0].target"));

        Assert.That(Validate(AutomationTriggerType.ReactionAdded,
            actions: [AutomationNode.Create("GiveCoins", ("amount", 5), ("target", "MessageAuthor"))]), Is.Empty);
    }

    [Test]
    public void ListsMustNotBeEmptyOrHaveBadItems()
    {
        Assert.That(Validate(AutomationTriggerType.MessageSent,
            [AutomationNode.Create("HasAnyRole", ("roleIds", Array.Empty<ulong>()))]), Is.Not.Empty);
        Assert.That(Validate(AutomationTriggerType.MessageSent,
            [AutomationNode.Create("HasAnyRole", ("roleIds", new[] { "abc" }))]), Is.Not.Empty);
        Assert.That(Validate(AutomationTriggerType.MessageSent,
            [AutomationNode.Create("HasAnyRole", ("roleIds", new[] { "123" }))]), Is.Empty);
    }

    [Test]
    public void RegexMustCompile()
    {
        Assert.That(Validate(AutomationTriggerType.MessageSent,
            [AutomationNode.Create("MessageMatchesRegex", ("pattern", "("))]), Is.Not.Empty);
        Assert.That(Validate(AutomationTriggerType.MessageSent,
            [AutomationNode.Create("MessageMatchesRegex", ("pattern", new string('a', 201)))]), Is.Not.Empty);
        Assert.That(Validate(AutomationTriggerType.MessageSent,
            [AutomationNode.Create("MessageMatchesRegex", ("pattern", "^hi"))]), Is.Empty);
    }

    [Test]
    public void EmojisAreChecked()
    {
        Assert.That(Validate(AutomationTriggerType.ReactionAdded,
            actions: [AutomationNode.Create("AddReactions", ("emojis", new[] { "👍", "<:a:123456789012345678>" }))]),
            Is.Empty);
        Assert.That(Validate(AutomationTriggerType.ReactionAdded,
            actions: [AutomationNode.Create("AddReactions", ("emojis", new[] { "not an emoji" }))]), Is.Not.Empty);
    }

    [Test]
    public void ButtonsAreChecked()
    {
        object[] Buttons(params (string Id, string Label, string Style)[] items) =>
            items.Select(x => (object)new Dictionary<string, string>
            {
                ["customId"] = x.Id, ["label"] = x.Label, ["style"] = x.Style
            }).ToArray();

        AutomationNode[] Send(object[] buttons) =>
            [AutomationNode.Create("SendMessage", ("text", "hi"), ("buttons", buttons))];

        Assert.That(Validate(AutomationTriggerType.MessageSent, actions: Send(Buttons(("a", "A", "Primary")))), Is.Empty);
        Assert.That(Validate(AutomationTriggerType.MessageSent, actions: Send(Buttons(("a b", "A", "Primary")))),
            Is.Not.Empty);
        Assert.That(Validate(AutomationTriggerType.MessageSent, actions: Send(Buttons(("a", "", "Primary")))),
            Is.Not.Empty);
        Assert.That(Validate(AutomationTriggerType.MessageSent, actions: Send(Buttons(("a", "A", "Pink")))),
            Is.Not.Empty);
        Assert.That(Validate(AutomationTriggerType.MessageSent,
            actions: Send(Buttons(("a", "A", "Primary"), ("a", "B", "Primary")))), Is.Not.Empty);
        Assert.That(Validate(AutomationTriggerType.MessageSent,
            actions: Send(Enumerable.Range(0, 6).Select(i => Buttons(($"b{i}", "B", "Primary"))[0]).ToArray())),
            Is.Not.Empty);
    }

    [Test]
    public void EmbedJsonMustParse()
    {
        Assert.That(Validate(AutomationTriggerType.MessageSent,
            actions: [AutomationNode.Create("SendMessage", ("embed", "not json"))]), Is.Not.Empty);
        Assert.That(Validate(AutomationTriggerType.MessageSent,
            actions: [AutomationNode.Create("SendMessage", ("embed", "{\"embed\":{\"title\":\"Hi\"}}"))]), Is.Empty);
    }

    [Test]
    public void EveryTemplateListsWhatItNeeds()
    {
        foreach (var template in AutomationTemplates.All)
            Assert.That(template.Parameters, Is.Not.Empty, template.Id);
    }

    [Test]
    public void EverySchemaEntryDescribesItself()
    {
        foreach (var spec in AutomationSchema.Conditions.Concat(AutomationSchema.Actions))
        {
            Assert.That(spec.Name, Is.Not.Empty, spec.Type);
            Assert.That(spec.Description, Is.Not.Empty, spec.Type);
            Assert.That(spec.Fields.Select(x => x.Name), Is.Unique, spec.Type);
            Assert.That(spec.Fields.All(x => x.Description.Length > 0), Is.True, spec.Type);
        }

        Assert.That(AutomationSchema.Triggers.Select(x => x.Type), Is.EquivalentTo(Enum.GetValues<AutomationTriggerType>()));
    }
}

[TestFixture]
public class AutomationSupportTests
{
    [Test]
    public void NodesRoundTripThroughJson()
    {
        var nodes = new List<AutomationNode>
        {
            AutomationNode.Create("GiveRoles", ("roleIds", new ulong[] { 1, 2 }), ("target", "TriggerUser"))
        };

        var parsed = AutomationNode.ParseList(AutomationNode.ToJson(nodes));
        Assert.That(parsed, Has.Count.EqualTo(1));
        Assert.That(parsed[0].Type, Is.EqualTo("GiveRoles"));
        Assert.That(parsed[0].GetIds("roleIds"), Is.EqualTo(new ulong[] { 1, 2 }));
        Assert.That(parsed[0].GetString("target"), Is.EqualTo("TriggerUser"));
    }

    [Test]
    public void FieldNamesAreCaseInsensitiveAndIdsMayBeStrings()
    {
        var parsed = AutomationNode.ParseList("[{\"type\":\"GiveRoles\",\"RoleIds\":[\"123\",456]}]");
        Assert.That(parsed[0].GetIds("roleIds"), Is.EqualTo(new ulong[] { 123, 456 }));
    }

    [Test]
    public void BrokenJsonGivesNoNodes()
    {
        Assert.That(AutomationNode.ParseList("{"), Is.Empty);
        Assert.That(AutomationNode.ParseList(null), Is.Empty);
    }

    [TestCase("⭐", "⭐")]
    [TestCase("<:star:123456789012345678>", "123456789012345678")]
    [TestCase("<a:dance:123456789012345678>", "123456789012345678")]
    [TestCase("star:123456789012345678", "123456789012345678")]
    [TestCase("123456789012345678", "123456789012345678")]
    public void EmojisNormalize(string input, string expected)
    {
        Assert.That(AutomationEmoji.Normalize(input), Is.EqualTo(expected));
    }

    [TestCase(HttpStatusCode.TooManyRequests, null, true)]
    [TestCase(HttpStatusCode.BadGateway, null, true)]
    [TestCase(HttpStatusCode.InternalServerError, null, true)]
    [TestCase(HttpStatusCode.Forbidden, null, false)]
    [TestCase(HttpStatusCode.Unauthorized, null, false)]
    [TestCase(HttpStatusCode.NotFound, null, false)]
    [TestCase(HttpStatusCode.BadRequest, null, false)]
    [TestCase(HttpStatusCode.InternalServerError, DiscordErrorCode.MissingPermissions, false)]
    public void OnlyTransientFailuresAreRetried(HttpStatusCode status, DiscordErrorCode? code, bool expected)
    {
        Assert.That(AutomationRetry.IsTransient(status, code), Is.EqualTo(expected));
    }

    [Test]
    public void ActionExceptionsAreNeverRetried()
    {
        Assert.That(AutomationRetry.IsTransient(new AutomationActionException("no")), Is.False);
        Assert.That(AutomationRetry.IsTransient(new TimeoutException()), Is.True);
        Assert.That(AutomationRetry.IsTransient(new InvalidOperationException()), Is.False);
    }

    [Test]
    public void BackoffGrowsAndIsCapped()
    {
        Assert.That(AutomationRetry.Backoff(1), Is.EqualTo(TimeSpan.FromSeconds(1)));
        Assert.That(AutomationRetry.Backoff(2), Is.EqualTo(TimeSpan.FromSeconds(2)));
        Assert.That(AutomationRetry.Backoff(10), Is.EqualTo(TimeSpan.FromSeconds(8)));
    }
}
