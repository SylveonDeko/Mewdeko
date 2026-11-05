using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using Discord;
using Discord.Net;
using Mewdeko.Common.Replacements;
using Mewdeko.Modules.Automations.Common;

namespace Mewdeko.Tests;

[TestFixture]
public class AutomationRunnerTests
{
    private static readonly Func<TimeSpan, Task> NoDelay = _ => Task.CompletedTask;

    private static AutomationNode Action(string type) => AutomationNode.Create(type);

    private static Replacer Replacer(params (string Key, string Value)[] values) =>
        new(values.Select(x => (x.Key, (Func<string>)(() => x.Value))), Array.Empty<(Regex, Func<Match, string>)>());

    [Test]
    public async Task ActionsRunInOrder()
    {
        var seen = new List<string>();
        var result = await AutomationPipeline.RunAsync([Action("A"), Action("B"), Action("C")], false, node =>
        {
            seen.Add(node.Type);
            return Task.CompletedTask;
        }, NoDelay);

        Assert.That(result.Success, Is.True);
        Assert.That(seen, Is.EqualTo(new[] { "A", "B", "C" }));
    }

    [Test]
    public async Task OnlyTheFirstFifteenActionsRun()
    {
        var count = 0;
        var actions = Enumerable.Range(0, AutomationLimits.MaxActions + 10).Select(_ => Action("A")).ToList();
        var result = await AutomationPipeline.RunAsync(actions, false, _ =>
        {
            count++;
            return Task.CompletedTask;
        }, NoDelay);

        Assert.That(result.Success, Is.True);
        Assert.That(count, Is.EqualTo(AutomationLimits.MaxActions));
    }

    [Test]
    public async Task ARunStopsAtTheFirstFailedAction()
    {
        var seen = new List<string>();
        var result = await AutomationPipeline.RunAsync([Action("A"), Action("B"), Action("C")], true, node =>
        {
            seen.Add(node.Type);
            return node.Type == "B"
                ? throw new AutomationActionException("Role is above mine.")
                : Task.CompletedTask;
        }, NoDelay);

        Assert.That(result.Success, Is.False);
        Assert.That(result.FailedIndex, Is.EqualTo(1));
        Assert.That(result.FailedType, Is.EqualTo("B"));
        Assert.That(result.Reason, Is.EqualTo("Role is above mine."));
        Assert.That(seen, Is.EqualTo(new[] { "A", "B" }));
    }

    [Test]
    public async Task TransientFailuresAreRetriedWhenAskedWithBackoff()
    {
        var attempts = 0;
        var delays = new List<TimeSpan>();
        var result = await AutomationPipeline.RunAsync([Action("A")], true, _ =>
        {
            attempts++;
            return attempts < 3 ? throw new TimeoutException() : Task.CompletedTask;
        }, delay =>
        {
            delays.Add(delay);
            return Task.CompletedTask;
        });

        Assert.That(result.Success, Is.True);
        Assert.That(attempts, Is.EqualTo(3));
        Assert.That(delays, Is.EqualTo(new[] { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2) }));
    }

    [Test]
    public async Task RetriesGiveUpAfterTheMaximumAttempts()
    {
        var attempts = 0;
        var result = await AutomationPipeline.RunAsync([Action("A")], true, _ =>
        {
            attempts++;
            throw new HttpRequestException();
        }, NoDelay);

        Assert.That(result.Success, Is.False);
        Assert.That(attempts, Is.EqualTo(AutomationLimits.MaxAttempts));
    }

    [Test]
    public async Task TransientFailuresAreNotRetriedUnlessAsked()
    {
        var attempts = 0;
        var result = await AutomationPipeline.RunAsync([Action("A")], false, _ =>
        {
            attempts++;
            throw new TimeoutException();
        }, NoDelay);

        Assert.That(result.Success, Is.False);
        Assert.That(attempts, Is.EqualTo(1));
    }

    [Test]
    public async Task PermissionAndSettingsErrorsAreNeverRetried()
    {
        var attempts = 0;
        var result = await AutomationPipeline.RunAsync([Action("A")], true, _ =>
        {
            attempts++;
            throw new AutomationActionException("I'm missing the Manage Roles permission.");
        }, NoDelay);

        Assert.That(result.Success, Is.False);
        Assert.That(attempts, Is.EqualTo(1));

        attempts = 0;
        result = await AutomationPipeline.RunAsync([Action("A")], true, _ =>
        {
            attempts++;
            throw new InvalidOperationException();
        }, NoDelay);

        Assert.That(result.Success, Is.False);
        Assert.That(attempts, Is.EqualTo(1));
    }

    [Test]
    public async Task AFailureAfterRetriesStillStopsTheRun()
    {
        var seen = new List<string>();
        var result = await AutomationPipeline.RunAsync([Action("A"), Action("B")], true, node =>
        {
            seen.Add(node.Type);
            return node.Type == "A" ? throw new TimeoutException() : Task.CompletedTask;
        }, NoDelay);

        Assert.That(result.FailedIndex, Is.EqualTo(0));
        Assert.That(seen, Does.Not.Contain("B"));
    }

    [Test]
    public void WaitsAreClampedToTheCap()
    {
        Assert.That(AutomationPipeline.NextWait(5, 0), Is.EqualTo(5));
        Assert.That(AutomationPipeline.NextWait(9999, 0), Is.EqualTo(AutomationLimits.MaxWaitSeconds));
        Assert.That(AutomationPipeline.NextWait(0, 0), Is.EqualTo(1));
    }

    [Test]
    public void WaitsStopWhenTheRunHasWaitedEnough()
    {
        Assert.That(AutomationPipeline.NextWait(60, 60), Is.EqualTo(60));
        Assert.That(() => AutomationPipeline.NextWait(1, AutomationLimits.MaxTotalWaitSeconds),
            Throws.TypeOf<AutomationActionException>());
    }

    [Test]
    public async Task WaitActionsFailTheRunOnceTheBudgetIsSpent()
    {
        var waited = 0;
        var slept = new List<TimeSpan>();
        var actions = Enumerable.Range(0, 4).Select(_ => AutomationNode.Create("Wait", ("seconds", 50))).ToList();
        var result = await AutomationPipeline.RunAsync(actions, false, node =>
        {
            var seconds = AutomationPipeline.NextWait(node.GetLong("seconds"), waited);
            waited += seconds;
            slept.Add(TimeSpan.FromSeconds(seconds));
            return Task.CompletedTask;
        }, NoDelay);

        Assert.That(result.Success, Is.False);
        Assert.That(result.FailedIndex, Is.EqualTo(2));
        Assert.That(slept, Has.Count.EqualTo(2));
    }

    [Test]
    public void TextPlaceholdersAreReplaced()
    {
        var node = AutomationNode.Create("SendMessage", ("text", "Welcome %user%!"));
        var (text, embeds) = AutomationPipeline.Compose(node, Replacer(("%user%", "Bob")), false, 1);

        Assert.That(text, Is.EqualTo("Welcome Bob!"));
        Assert.That(embeds, Is.Null);
    }

    [Test]
    public void EmbedJsonPlaceholdersAreReplacedAndStayValidJson()
    {
        var node = AutomationNode.Create("SendMessage",
            ("embed", "{\"embed\":{\"title\":\"Hi %user%\",\"description\":\"%message.content%\"}}"));
        var replacer = Replacer(("%user%", "Bob"), ("%message.content%", "she said \"hi\"\nthen left"));
        var (text, embeds) = AutomationPipeline.Compose(node, replacer, false, 1);

        Assert.That(text, Is.Null);
        Assert.That(embeds, Has.Length.EqualTo(1));
        Assert.That(embeds![0].Title, Is.EqualTo("Hi Bob"));
        Assert.That(embeds[0].Description, Is.EqualTo("she said \"hi\"\nthen left"));
    }

    [Test]
    public void EmbedJsonContentIsAddedToTheText()
    {
        var node = AutomationNode.Create("SendMessage", ("text", "A"),
            ("embed", "{\"content\":\"B %user%\",\"embed\":{\"title\":\"T\"}}"));
        var (text, _) = AutomationPipeline.Compose(node, Replacer(("%user%", "Bob")), false, 1);

        Assert.That(text, Is.EqualTo("A\nB Bob"));
    }

    [Test]
    public void MassMentionsAreDefusedUnlessAllowed()
    {
        var node = AutomationNode.Create("SendMessage", ("text", "hey @everyone and @here"));
        var blocked = AutomationPipeline.Compose(node, Replacer(), false, 1).Text;
        var allowed = AutomationPipeline.Compose(node, Replacer(), true, 1).Text;

        Assert.That(blocked, Does.Not.Contain("@everyone"));
        Assert.That(blocked, Does.Not.Contain("@here"));
        Assert.That(allowed, Is.EqualTo("hey @everyone and @here"));
    }

    [Test]
    public void EmptyAndInvalidMessagesFail()
    {
        Assert.That(() => AutomationPipeline.Compose(AutomationNode.Create("SendMessage", ("text", "%nothing%")),
            Replacer(("%nothing%", "")), false, 1), Throws.TypeOf<AutomationActionException>());
        Assert.That(() => AutomationPipeline.Compose(AutomationNode.Create("SendMessage", ("embed", "{nope")),
            Replacer(), false, 1), Throws.TypeOf<AutomationActionException>());
    }

    [Test]
    public void NonJsonEmbedTextIsReplacedAsPlainText()
    {
        Assert.That(AutomationPipeline.RenderJson("hello %user%", Replacer(("%user%", "Bob"))),
            Is.EqualTo("hello Bob"));
    }
}
