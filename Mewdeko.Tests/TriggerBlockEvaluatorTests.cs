using Mewdeko.Modules.Chat_Triggers.Common;

namespace Mewdeko.Tests;

[TestFixture]
public class TriggerBlockEvaluatorTests
{
    private static string Eval(string input)
    {
        return new TriggerBlockEvaluator(new Random(1)).Evaluate(input);
    }

    [TestCase("plain text", "plain text")]
    [TestCase("{if(1==1):yes|no}", "yes")]
    [TestCase("{if(1==2):yes|no}", "no")]
    [TestCase("{if(a!=b):yes|no}", "yes")]
    [TestCase("{if(10>9):yes|no}", "yes")]
    [TestCase("{if(10<9):yes|no}", "no")]
    [TestCase("{if(5>=5):yes|no}", "yes")]
    [TestCase("{if(5<=4):yes|no}", "no")]
    [TestCase("{if(Hello World contains world):yes|no}", "yes")]
    [TestCase("{if(abc==ABC):yes|no}", "yes")]
    [TestCase("{if(1==2):yes}", "")]
    public void IfBlock(string input, string expected)
    {
        Assert.That(Eval(input), Is.EqualTo(expected));
    }

    [Test]
    public void IfComparesNumbersNumerically()
    {
        Assert.That(Eval("{if(10>9):num|str}"), Is.EqualTo("num"));
        Assert.That(Eval("{if(1.0==1):same|diff}"), Is.EqualTo("same"));
    }

    [Test]
    public void VariablesLiveForOneEvaluator()
    {
        Assert.That(Eval("{set(x):5}a{get(x)}b{get(missing)}c"), Is.EqualTo("a5bc"));
    }

    [Test]
    public void BlocksNest()
    {
        Assert.That(Eval("{set(n):4}{if({math:{get(n)}*2}==8):{upper:ok}|{lower:NO}}"), Is.EqualTo("OK"));
    }

    [TestCase("{math:1+2*3}", "7")]
    [TestCase("{math:(1+2)*3}", "9")]
    [TestCase("{math:10/4}", "2.5")]
    [TestCase("{math:10%4}", "2")]
    [TestCase("{math:-3+5}", "2")]
    [TestCase("{math:1/0}", "{math:1/0}")]
    [TestCase("{math:1+}", "{math:1+}")]
    [TestCase("{math:abc}", "{math:abc}")]
    public void MathBlock(string input, string expected)
    {
        Assert.That(Eval(input), Is.EqualTo(expected));
    }

    [Test]
    public void TextBlocks()
    {
        Assert.That(Eval("{upper:abc} {lower:ABC} {length:hello}"), Is.EqualTo("ABC abc 5"));
        Assert.That(Eval("{replace(a,b):banana}"), Is.EqualTo("bbnbnb"));
    }

    [Test]
    public void ChooseReturnsOneOption()
    {
        for (var i = 0; i < 20; i++)
            Assert.That(new[] { "a", "b", "c" }, Does.Contain(Eval("{choose:a|b|c}")));
    }

    [TestCase("{unknown:text}")]
    [TestCase("{if(1==1):yes")]
    [TestCase("{if:no parens}")]
    [TestCase("{\"json\": true}")]
    [TestCase("{set(bad name):x}")]
    public void MalformedBlocksStayPlain(string input)
    {
        Assert.That(Eval(input), Is.EqualTo(input));
    }

    [Test]
    public void CommandBlocksAreQueuedAndCapped()
    {
        var evaluator = new TriggerBlockEvaluator();
        var result = evaluator.Evaluate("a{cmd:one x}b{cmd:two}{cmd:three}{cmd:four}c");

        Assert.That(result, Is.EqualTo("abc"));
        Assert.That(evaluator.Commands, Is.EqualTo(new[] { "one x", "two", "three" }));
    }

    [Test]
    public void DeepNestingIsLeftAsText()
    {
        var input = string.Concat(Enumerable.Repeat("{upper:", 40)) + "x" + new string('}', 40);
        var result = Eval(input);

        Assert.That(result, Does.Contain("{UPPER:").IgnoreCase);
        Assert.That(result, Does.EndWith("X" + new string('}', 40 - TriggerBlockEvaluator.MaxDepth)));
    }

    [Test]
    public void OutputIsCapped()
    {
        var big = new string('a', 7000);
        var result = Eval($"{{replace(a,bbbbbbbbbb):{big}}}{big}{big}");

        Assert.That(result.Length, Is.LessThanOrEqualTo(TriggerBlockEvaluator.MaxOutputLength));
    }

    [Test]
    public void ManyBlocksDoNotHang()
    {
        var input = string.Concat(Enumerable.Repeat("{upper:a}", 5000));

        Assert.That(Eval(input).Length, Is.LessThanOrEqualTo(TriggerBlockEvaluator.MaxOutputLength));
    }

    private static TriggerGateContext Gate(bool staff = false, params ulong[] memberRoles)
    {
        return new TriggerGateContext(
            new Dictionary<ulong, string> { [10] = "Mods", [11] = "Members", [12] = "VIP" },
            new Dictionary<ulong, string> { [20] = "general", [21] = "bots" },
            memberRoles.ToHashSet(), new HashSet<ulong> { 20 }, staff);
    }

    private static TriggerBlockEvaluator Gated(TriggerGateContext gate)
    {
        return new TriggerBlockEvaluator(new Random(1), gate);
    }

    [Test]
    public void RequirePassesWhenEveryRoleIsHeld()
    {
        var evaluator = Gated(Gate(false, 10, 11));

        Assert.That(evaluator.Evaluate("{require(Mods,11):no}hello"), Is.EqualTo("hello"));
        Assert.That(evaluator.Stopped, Is.False);
    }

    [Test]
    public void RequireStopsWithTheMessageWhenARoleIsMissing()
    {
        var evaluator = Gated(Gate(false, 10));

        Assert.That(evaluator.Evaluate("{require(10,VIP):VIP only}hello"), Is.EqualTo("VIP only"));
        Assert.That(evaluator.Stopped, Is.True);
    }

    [TestCase("{require(general):no}ok", "ok")]
    [TestCase("{require(#bots):wrong channel}ok", "wrong channel")]
    [TestCase("{require(21,20):no}ok", "ok")]
    public void RequireChecksChannels(string input, string expected)
    {
        Assert.That(Gated(Gate()).Evaluate(input), Is.EqualTo(expected));
    }

    [Test]
    public void RequireFailsClosedOnUnknownItems()
    {
        Assert.That(Gated(Gate(false, 10)).Evaluate("{require(Mods,Nonexistent):denied}ok"), Is.EqualTo("denied"));
    }

    [Test]
    public void BlacklistStopsOnAHeldRoleOrCurrentChannel()
    {
        Assert.That(Gated(Gate(false, 12)).Evaluate("{blacklist(VIP):banned}ok"), Is.EqualTo("banned"));
        Assert.That(Gated(Gate()).Evaluate("{blacklist(general):not here}ok"), Is.EqualTo("not here"));
        Assert.That(Gated(Gate(false, 10)).Evaluate("{blacklist(VIP,bots):banned}ok"), Is.EqualTo("ok"));
    }

    [Test]
    public void BlacklistIgnoresUnknownItems()
    {
        Assert.That(Gated(Gate()).Evaluate("{blacklist(Nonexistent):banned}ok"), Is.EqualTo("ok"));
    }

    [Test]
    public void OverrideLetsStaffThroughButNotMembers()
    {
        const string input = "{override}{require(VIP):VIP only}ok";

        Assert.That(Gated(Gate(true)).Evaluate(input), Is.EqualTo("ok"));
        Assert.That(Gated(Gate(false)).Evaluate(input), Is.EqualTo("VIP only"));
    }

    [Test]
    public void OverrideWorksFromAfterTheGate()
    {
        Assert.That(Gated(Gate(true)).Evaluate("{require(VIP):VIP only}ok{override}"), Is.EqualTo("ok"));
    }

    [Test]
    public void StoppingDropsQueuedCommands()
    {
        var evaluator = Gated(Gate());
        var result = evaluator.Evaluate("{cmd:ping}{require(VIP):no}");

        Assert.That(result, Is.EqualTo("no"));
        Assert.That(evaluator.Commands, Is.Empty);
    }

    [Test]
    public void GatesAreSkippedWithoutAContext()
    {
        var evaluator = new TriggerBlockEvaluator();

        Assert.That(evaluator.Evaluate("{require(VIP):no}ok"), Is.EqualTo("ok"));
        Assert.That(evaluator.Stopped, Is.False);
    }

    [Test]
    public void GateListsAreCappedAndMalformedGatesStayPlain()
    {
        var items = string.Join(",", Enumerable.Repeat("Mods", TriggerBlockEvaluator.MaxGateItems + 5)) + ",VIP";

        Assert.That(Gated(Gate(false, 10)).Evaluate($"{{require({items}):no}}ok"), Is.EqualTo("ok"));
        Assert.That(Eval("{require():no}"), Is.EqualTo("{require():no}"));
        Assert.That(Eval("{require(VIP)}"), Is.EqualTo("{require(VIP)}"));
        Assert.That(Eval("{override:x}"), Is.EqualTo("{override:x}"));
    }
}
