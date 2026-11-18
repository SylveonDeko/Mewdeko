using System.Buffers.Binary;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Mewdeko.Modules.OwnerOnly.Services;
using Mewdeko.Modules.Utility.Common;
using Mewdeko.Modules.Utility.Services;
using Mewdeko.Services;

namespace Mewdeko.Tests;

/// <summary>
///     Pins down the run command: the container request, the language file, input parsing, output handling and
///     which Docker host runs go to.
/// </summary>
[TestFixture]
public class CodeRunTests
{
    private static readonly CodeRunLanguage Python = new()
    {
        Name = "python",
        Aliases = ["py"],
        Image = "python:3.13-alpine",
        FileName = "main.py",
        Run = "python3 main.py",
        Env = new Dictionary<string, string>
        {
            ["PYTHONUNBUFFERED"] = "1"
        }
    };

    private static readonly CodeRunLanguage C = new()
    {
        Name = "c",
        Image = "gcc:14",
        FileName = "main.c",
        Compile = "gcc -O1 -o main main.c -lm",
        Run = "./main"
    };

    private static readonly CodeRunOptions Options = new(1234567890123456789UL, "");

    private static CodeRunLanguages Languages
    {
        get
        {
            return CodeRunLanguages.Parse(JsonSerializer.Serialize(new[]
            {
                Python, C
            }));
        }
    }

    private static JsonNode Request(CodeRunLanguage language, string code, CodeRunOptions? options = null)
    {
        var request = CodeRunRequestBuilder.Build(language, code, options ?? Options);
        return JsonNode.Parse(JsonSerializer.Serialize(request))!;
    }

    private static string FindDataFile()
    {
        var dir = Directory.GetCurrentDirectory();
        while (!Directory.Exists(Path.Combine(dir, "src", "Mewdeko", "Modules")))
            dir = Directory.GetParent(dir)?.FullName ?? throw new DirectoryNotFoundException("No project root");

        return Path.Combine(dir, "src", "Mewdeko", "data", "coderun.json");
    }

    [Test]
    public void Request_DisablesNetwork()
    {
        var json = Request(Python, "print(1)");

        Assert.That(json["NetworkDisabled"]!.GetValue<bool>(), Is.True);
        Assert.That(json["HostConfig"]!["NetworkMode"]!.GetValue<string>(), Is.EqualTo("none"));
    }

    [Test]
    public void Request_RunsAsNobody()
    {
        Assert.That(Request(Python, "x")["User"]!.GetValue<string>(), Is.EqualTo("65534:65534"));
    }

    [Test]
    public void Request_LocksDownTheHost()
    {
        var host = Request(Python, "x")["HostConfig"]!;

        Assert.That(host["ReadonlyRootfs"]!.GetValue<bool>(), Is.True);
        Assert.That(host["CapDrop"]!.AsArray().Select(n => n!.GetValue<string>()), Is.EqualTo(new[] { "ALL" }));
        Assert.That(host["SecurityOpt"]!.AsArray().Select(n => n!.GetValue<string>()),
            Is.EqualTo(new[] { "no-new-privileges" }));
        Assert.That(host["Privileged"]!.GetValue<bool>(), Is.False);
        Assert.That(host["IpcMode"]!.GetValue<string>(), Is.EqualTo("none"));
        Assert.That(host["AutoRemove"]!.GetValue<bool>(), Is.False);
        Assert.That(host["RestartPolicy"]!["Name"]!.GetValue<string>(), Is.EqualTo("no"));
    }

    [Test]
    public void Request_OmitsEverythingThatWouldWidenAccess()
    {
        var json = Request(Python, "x");
        var host = json["HostConfig"]!.AsObject();

        foreach (var key in new[]
                 {
                     "CapAdd", "Binds", "Devices", "Mounts", "Volumes", "VolumesFrom", "PidMode", "UTSMode",
                     "UsernsMode", "OomKillDisable", "Links", "ExtraHosts", "Dns", "PortBindings", "PublishAllPorts",
                     "CgroupParent", "DeviceCgroupRules", "ContainerIDFile"
                 })
        {
            Assert.That(host.ContainsKey(key), Is.False, $"HostConfig.{key} must not be set");
        }

        Assert.That(json.AsObject().ContainsKey("Volumes"), Is.False);
        Assert.That(json.AsObject().ContainsKey("ExposedPorts"), Is.False);
        Assert.That(host["SecurityOpt"]!.ToJsonString(), Does.Not.Contain("unconfined"));
    }

    [Test]
    public void Request_SetsProcessMemoryAndCpuLimits()
    {
        var host = Request(Python, "x")["HostConfig"]!;

        Assert.That(host["PidsLimit"]!.GetValue<long>(), Is.EqualTo(64));
        Assert.That(host["Memory"]!.GetValue<long>(), Is.EqualTo(256L * 1024 * 1024));
        Assert.That(host["MemorySwap"]!.GetValue<long>(), Is.EqualTo(host["Memory"]!.GetValue<long>()));
        Assert.That(host["NanoCpus"]!.GetValue<long>(), Is.EqualTo(1_000_000_000));
    }

    [Test]
    public void Request_SetsUlimits()
    {
        var ulimits = Request(Python, "x")["HostConfig"]!["Ulimits"]!.AsArray()
            .ToDictionary(n => n!["Name"]!.GetValue<string>(),
                n => (Soft: n!["Soft"]!.GetValue<long>(), Hard: n["Hard"]!.GetValue<long>()));

        Assert.That(ulimits, Has.Count.EqualTo(3));
        Assert.That(ulimits["nofile"], Is.EqualTo((256L, 256L)));
        Assert.That(ulimits["fsize"], Is.EqualTo((16L * 1024 * 1024, 16L * 1024 * 1024)));
        Assert.That(ulimits["core"], Is.EqualTo((0L, 0L)));
    }

    [Test]
    public void Request_MountsOnlyTwoTmpfs()
    {
        var json = Request(Python, "x");
        var tmpfs = json["HostConfig"]!["Tmpfs"]!.AsObject();

        Assert.That(tmpfs.Select(p => p.Key), Is.EquivalentTo(new[] { "/work", "/tmp" }));
        foreach (var (_, options) in tmpfs)
            Assert.That(options!.GetValue<string>(), Is.EqualTo("rw,exec,nosuid,nodev,size=32m,mode=1777"));

        Assert.That(json["WorkingDir"]!.GetValue<string>(), Is.EqualTo("/work"));
    }

    [Test]
    public void Request_PointsHomeIntoTheWorkDirectory()
    {
        var env = Request(Python, "x")["Env"]!.AsArray().Select(n => n!.GetValue<string>()).ToList();

        Assert.That(env, Does.Contain("HOME=/work"));
        Assert.That(env, Does.Contain("XDG_CACHE_HOME=/work/.cache"));
        Assert.That(env, Does.Contain("TMPDIR=/tmp"));
        Assert.That(env, Does.Contain("PYTHONUNBUFFERED=1"));
    }

    [Test]
    public void Request_LabelsTheContainerWithTheUser()
    {
        var labels = Request(Python, "x")["Labels"]!.AsObject();

        Assert.That(labels["mewdeko.coderun"]!.GetValue<string>(), Is.EqualTo("1"));
        Assert.That(labels["mewdeko.coderun.user"]!.GetValue<string>(), Is.EqualTo("1234567890123456789"));
    }

    [Test]
    public void Request_UsesTheConfiguredRuntimeOnlyWhenSet()
    {
        Assert.That(Request(Python, "x")["HostConfig"]!.AsObject().ContainsKey("Runtime"), Is.False);
        Assert.That(Request(Python, "x", Options with { Runtime = "  " })["HostConfig"]!.AsObject()
            .ContainsKey("Runtime"), Is.False);
        Assert.That(Request(Python, "x", Options with { Runtime = "runsc" })["HostConfig"]!["Runtime"]!
            .GetValue<string>(), Is.EqualTo("runsc"));
    }

    [Test]
    public void Request_BoundsTheDaemonLog()
    {
        var log = Request(Python, "x")["HostConfig"]!["LogConfig"]!;

        Assert.That(log["Type"]!.GetValue<string>(), Is.EqualTo("json-file"));
        Assert.That(log["Config"]!["max-size"]!.GetValue<string>(), Is.EqualTo("1m"));
        Assert.That(log["Config"]!["max-file"]!.GetValue<string>(), Is.EqualTo("1"));
    }

    [Test]
    public void Request_NeverPutsMemberCodeInTheCommand()
    {
        const string hostile = "'; rm -rf / #\n$(reboot) `id` \"$X\"";
        var request = CodeRunRequestBuilder.Build(Python, hostile, Options);
        var command = string.Join("\n", request.Entrypoint.Concat(request.Cmd));

        Assert.That(command, Does.Not.Contain("rm -rf"));
        Assert.That(command, Does.Not.Contain("reboot"));
        Assert.That(request.Image, Is.EqualTo("python:3.13-alpine"));
        var code = request.Env.Single(e => e.StartsWith("MEWDEKO_CODE=", StringComparison.Ordinal));
        Assert.That(Encoding.UTF8.GetString(Convert.FromBase64String(code["MEWDEKO_CODE=".Length..])),
            Is.EqualTo(hostile));
        Assert.That(request.Env.Count(e => e.Contains("rm -rf")), Is.EqualTo(0));
    }

    [Test]
    public void Request_InterpretedAndCompiledScripts()
    {
        Assert.That(CodeRunRequestBuilder.BuildScript(Python),
            Is.EqualTo("set -e\nprintf '%s' \"$MEWDEKO_CODE\" | base64 -d > main.py\npython3 main.py"));
        Assert.That(CodeRunRequestBuilder.BuildScript(C),
            Is.EqualTo(
                "set -e\nprintf '%s' \"$MEWDEKO_CODE\" | base64 -d > main.c\ngcc -O1 -o main main.c -lm\n./main"));
        Assert.That(Python.DeadlineSeconds, Is.EqualTo(10));
        Assert.That(C.DeadlineSeconds, Is.EqualTo(20));
    }

    [Test]
    public void Request_PrintForReview()
    {
        TestContext.Out.WriteLine("INTERPRETED " + Request(Python, "print(\"hi\")").ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true
        }));
        TestContext.Out.WriteLine("COMPILED " + Request(C, "int main(){return 0;}").ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true
        }));
        Assert.Pass();
    }

    [Test]
    public void Languages_RealFileLoadsWithPinnedImages()
    {
        var languages = CodeRunLanguages.LoadFile(FindDataFile());

        Assert.That(languages.All.Select(l => l.Name), Is.EquivalentTo(new[]
        {
            "python", "javascript", "typescript", "bash", "c", "cpp", "java", "go", "rust", "ruby", "php", "perl"
        }));
        foreach (var language in languages.All)
        {
            var tag = language.Image[(language.Image.LastIndexOf(':') + 1)..];
            Assert.That(language.Image, Does.Contain(':'), language.Name);
            Assert.That(tag, Is.Not.EqualTo("latest").And.Not.Empty, language.Name);
        }
    }

    [Test]
    public void Languages_RejectLatestAndMissingTags()
    {
        string Json(string image)
        {
            return JsonSerializer.Serialize(new[]
            {
                new CodeRunLanguage
                {
                    Name = "x", Image = image, FileName = "a.x", Run = "x a.x"
                }
            });
        }

        Assert.Throws<InvalidDataException>(() => CodeRunLanguages.Parse(Json("python:latest")));
        Assert.Throws<InvalidDataException>(() => CodeRunLanguages.Parse(Json("python:LATEST")));
        Assert.Throws<InvalidDataException>(() => CodeRunLanguages.Parse(Json("python")));
        Assert.Throws<InvalidDataException>(() => CodeRunLanguages.Parse(Json("python:3 --privileged")));
        Assert.DoesNotThrow(() => CodeRunLanguages.Parse(Json("python:3.13-alpine")));
    }

    [Test]
    public void Languages_RejectBadEntries()
    {
        Assert.Throws<InvalidDataException>(() => CodeRunLanguages.Parse("not json"));
        Assert.Throws<InvalidDataException>(() => CodeRunLanguages.Parse("[]"));
        Assert.Throws<InvalidDataException>(() => CodeRunLanguages.Parse(JsonSerializer.Serialize(new[]
        {
            Python, Python
        })));
        Assert.Throws<InvalidDataException>(() => CodeRunLanguages.Parse(JsonSerializer.Serialize(new[]
        {
            new CodeRunLanguage
            {
                Name = "x", Image = "a:1", FileName = "../escape", Run = "x"
            }
        })));
        Assert.Throws<InvalidDataException>(() => CodeRunLanguages.Parse(JsonSerializer.Serialize(new[]
        {
            new CodeRunLanguage
            {
                Name = "x", Image = "a:1", FileName = "a", Run = ""
            }
        })));
    }

    [Test]
    public void Languages_FindByNameAndAliasIgnoringCase()
    {
        var languages = Languages;

        Assert.That(languages.Find("python")?.Name, Is.EqualTo("python"));
        Assert.That(languages.Find("PY")?.Name, Is.EqualTo("python"));
        Assert.That(languages.Find(" c ")?.Name, Is.EqualTo("c"));
        Assert.That(languages.Find("cobol"), Is.Null);
        Assert.That(languages.Find("python:latest"), Is.Null);
        Assert.That(languages.Find(""), Is.Null);
        Assert.That(languages.Find(null), Is.Null);
    }

    [Test]
    public void Parser_LanguageThenCode()
    {
        var parsed = CodeRunParser.Parse("python print('hi')", Languages);

        Assert.That(parsed.Status, Is.EqualTo(CodeRunParseStatus.Ok));
        Assert.That(parsed.Language!.Name, Is.EqualTo("python"));
        Assert.That(parsed.Code, Is.EqualTo("print('hi')"));
    }

    [Test]
    public void Parser_LanguageThenNewlineThenCode()
    {
        var parsed = CodeRunParser.Parse("py\nx = 1\nprint(x)", Languages);

        Assert.That(parsed.Status, Is.EqualTo(CodeRunParseStatus.Ok));
        Assert.That(parsed.Code, Is.EqualTo("x = 1\nprint(x)"));
    }

    [Test]
    public void Parser_FencedBlockUsesTheFenceLanguage()
    {
        var parsed = CodeRunParser.Parse("```py\nprint(\"hi\")\n```", Languages);

        Assert.That(parsed.Status, Is.EqualTo(CodeRunParseStatus.Ok));
        Assert.That(parsed.Language!.Name, Is.EqualTo("python"));
        Assert.That(parsed.Code, Is.EqualTo("print(\"hi\")"));
    }

    [Test]
    public void Parser_LanguageWordWinsOverTheFence()
    {
        var parsed = CodeRunParser.Parse("c ```py\nint main(){}\n```", Languages);

        Assert.That(parsed.Status, Is.EqualTo(CodeRunParseStatus.Ok));
        Assert.That(parsed.Language!.Name, Is.EqualTo("c"));
        Assert.That(parsed.Code, Is.EqualTo("int main(){}"));
    }

    [Test]
    public void Parser_FenceWithoutLanguageAsksForOne()
    {
        Assert.That(CodeRunParser.Parse("```\nprint(1)\n```", Languages).Status,
            Is.EqualTo(CodeRunParseStatus.MissingLanguage));
        Assert.That(CodeRunParser.Parse("   ", Languages).Status, Is.EqualTo(CodeRunParseStatus.MissingLanguage));
        Assert.That(CodeRunParser.Parse(null, Languages).Status, Is.EqualTo(CodeRunParseStatus.MissingLanguage));
    }

    [Test]
    public void Parser_UnknownLanguage()
    {
        var parsed = CodeRunParser.Parse("cobol display 1", Languages);

        Assert.That(parsed.Status, Is.EqualTo(CodeRunParseStatus.UnknownLanguage));
        Assert.That(parsed.LanguageText, Is.EqualTo("cobol"));
        Assert.That(CodeRunParser.Parse("```cobol\nx\n```", Languages).Status,
            Is.EqualTo(CodeRunParseStatus.UnknownLanguage));
    }

    [Test]
    public void Parser_NoCode()
    {
        Assert.That(CodeRunParser.Parse("python", Languages).Status, Is.EqualTo(CodeRunParseStatus.NoCode));
        Assert.That(CodeRunParser.Parse("python ```\n```", Languages).Status, Is.EqualTo(CodeRunParseStatus.NoCode));
    }

    [Test]
    public void Parser_LanguagesKeyword()
    {
        Assert.That(CodeRunParser.Parse("languages", Languages).Status, Is.EqualTo(CodeRunParseStatus.ListLanguages));
        Assert.That(CodeRunParser.Parse(" Languages ", Languages).Status,
            Is.EqualTo(CodeRunParseStatus.ListLanguages));
    }

    [Test]
    public void Parser_CodeLengthLimit()
    {
        var atLimit = "python " + new string('x', CodeRunParser.MaxCodeLength);
        var over = "python " + new string('x', CodeRunParser.MaxCodeLength + 1);

        Assert.That(CodeRunParser.Parse(atLimit, Languages).Status, Is.EqualTo(CodeRunParseStatus.Ok));
        Assert.That(CodeRunParser.Parse(over, Languages).Status, Is.EqualTo(CodeRunParseStatus.TooLong));
        Assert.That(CodeRunParser.Parse($"```py\n{new string('x', 4001)}\n```", Languages).Status,
            Is.EqualTo(CodeRunParseStatus.TooLong));
    }

    [Test]
    public void Parser_StripsInlineBackticks()
    {
        Assert.That(CodeRunParser.Parse("python `print(1)`", Languages).Code, Is.EqualTo("print(1)"));
    }

    private static byte[] Frame(byte stream, string text)
    {
        var payload = Encoding.UTF8.GetBytes(text);
        var frame = new byte[8 + payload.Length];
        frame[0] = stream;
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(4, 4), (uint)payload.Length);
        payload.CopyTo(frame, 8);
        return frame;
    }

    [Test]
    public async Task Demultiplex_JoinsStdoutAndStderrInOrder()
    {
        var bytes = Frame(1, "out\n").Concat(Frame(2, "err\n")).Concat(Frame(1, "more\n")).ToArray();

        var (data, cut) = await CodeRunOutput.ReadDemultiplexedAsync(new MemoryStream(bytes), 1024);

        Assert.That(Encoding.UTF8.GetString(data), Is.EqualTo("out\nerr\nmore\n"));
        Assert.That(cut, Is.False);
    }

    [Test]
    public async Task Demultiplex_StopsAtTheReadCap()
    {
        var bytes = Frame(1, new string('a', 100)).Concat(Frame(1, new string('b', 100))).ToArray();

        var (data, cut) = await CodeRunOutput.ReadDemultiplexedAsync(new MemoryStream(bytes), 150);

        Assert.That(data, Has.Length.EqualTo(150));
        Assert.That(cut, Is.True);
    }

    [Test]
    public async Task Demultiplex_ExactlyAtTheCapIsNotCut()
    {
        var bytes = Frame(1, new string('a', 100));

        var (data, cut) = await CodeRunOutput.ReadDemultiplexedAsync(new MemoryStream(bytes), 100);

        Assert.That(data, Has.Length.EqualTo(100));
        Assert.That(cut, Is.False);
    }

    [Test]
    public async Task Demultiplex_ReadsAtMostSixtyFourKilobytes()
    {
        var bytes = Frame(1, new string('a', 100_000));

        var (data, cut) = await CodeRunOutput.ReadDemultiplexedAsync(new MemoryStream(bytes), CodeRunOutput.ReadLimit);

        Assert.That(data, Has.Length.EqualTo(64 * 1024));
        Assert.That(cut, Is.True);
    }

    [Test]
    public async Task Demultiplex_TruncatedStreamKeepsWhatArrived()
    {
        var full = Frame(1, "hello world");
        var bytes = full[..^5];

        var (data, cut) = await CodeRunOutput.ReadDemultiplexedAsync(new MemoryStream(bytes), 1024);

        Assert.That(Encoding.UTF8.GetString(data), Is.EqualTo("hello "));
        Assert.That(cut, Is.False);
    }

    [Test]
    public async Task Demultiplex_EmptyStream()
    {
        var (data, cut) = await CodeRunOutput.ReadDemultiplexedAsync(new MemoryStream(), 1024);

        Assert.That(data, Is.Empty);
        Assert.That(cut, Is.False);
    }

    [Test]
    public void Output_EmptyOutputSaysSo()
    {
        Assert.That(CodeRunOutput.Format([], false, "[cut]", "No output."), Is.EqualTo("No output."));
        Assert.That(CodeRunOutput.Format("  \n\n"u8.ToArray(), false, "[cut]", "No output."),
            Is.EqualTo("No output."));
    }

    [Test]
    public void Output_NeutralisesTripleBackticks()
    {
        foreach (var input in new[] { "```", "````", "``````", "a```b", "```\n```", "`` ` ``" })
        {
            var text = CodeRunOutput.Format(Encoding.UTF8.GetBytes(input), false, "[cut]", "empty");
            Assert.That(text.Contains("```", StringComparison.Ordinal), Is.False, input);
        }

        Assert.That(CodeRunOutput.Neutralise("a```b"), Is.EqualTo("a`​`​`b"));
        Assert.That(CodeRunOutput.Neutralise("`` `"), Is.EqualTo("`` `"));
    }

    [Test]
    public void Output_CutsLongOutputAndMarksIt()
    {
        var text = CodeRunOutput.Format(Encoding.UTF8.GetBytes(new string('x', 5000)), false, "[cut]", "empty");

        Assert.That(text, Does.EndWith("\n[cut]"));
        Assert.That(text.Length, Is.EqualTo(CodeRunOutput.DisplayLimit + "\n[cut]".Length));
    }

    [Test]
    public void Output_CutsManyLines()
    {
        var input = string.Join('\n', Enumerable.Range(0, 100).Select(i => i.ToString()));

        var text = CodeRunOutput.Format(Encoding.UTF8.GetBytes(input), false, "[cut]", "empty");

        Assert.That(text.Split('\n'), Has.Length.EqualTo(CodeRunOutput.DisplayLines + 1));
        Assert.That(text, Does.EndWith("[cut]"));
    }

    [Test]
    public void Output_MarksReadCapEvenWhenShort()
    {
        Assert.That(CodeRunOutput.Format("hi"u8.ToArray(), true, "[cut]", "empty"), Is.EqualTo("hi\n[cut]"));
        Assert.That(CodeRunOutput.Format("hi"u8.ToArray(), false, "[cut]", "empty"), Is.EqualTo("hi"));
    }

    [Test]
    public void Output_DropsControlCharacters()
    {
        var text = CodeRunOutput.Format("a\u001b[31mred\u0000\r\nb\tc"u8.ToArray(), false, "[cut]", "empty");

        Assert.That(text, Is.EqualTo("a[31mred\nb\tc"));
    }

    [Test]
    public void Reply_ComposesHeaderAndBlock()
    {
        Assert.That(CodeRunReply.Compose("Ran python in 0.1s.", null, null, "hi"),
            Is.EqualTo("Ran python in 0.1s.\n```\nhi\n```"));
        Assert.That(CodeRunReply.Compose("Ran c in 1.0s.", "Exit code 2.", "Too long.", "x"),
            Is.EqualTo("Ran c in 1.0s. Exit code 2. Too long.\n```\nx\n```"));
    }

    [TestCase("unix:///var/run/docker.sock", DockerEndpointKind.Unix, "/var/run/docker.sock", false)]
    [TestCase("tcp://127.0.0.1:2375", DockerEndpointKind.Tcp, "http://127.0.0.1:2375/", false)]
    [TestCase("tcp://localhost:2375/", DockerEndpointKind.Tcp, "http://localhost:2375/", false)]
    [TestCase("tcp://[::1]:2375", DockerEndpointKind.Tcp, "http://[::1]:2375/", false)]
    [TestCase("tcp://10.0.0.5:2376", DockerEndpointKind.TcpTls, "https://10.0.0.5:2376/", true)]
    [TestCase("tcp://sandbox.example.com:2376", DockerEndpointKind.TcpTls, "https://sandbox.example.com:2376/", true)]
    public void Endpoint_Accepted(string host, DockerEndpointKind kind, string address, bool tls)
    {
        var endpoint = DockerTransport.ParseEndpoint(host, tls);

        Assert.That(endpoint.Problem, Is.Null);
        Assert.That(endpoint.Kind, Is.EqualTo(kind));
        Assert.That(endpoint.Address, Is.EqualTo(address));
    }

    [TestCase("tcp://10.0.0.5:2375")]
    [TestCase("tcp://sandbox.example.com:2375")]
    [TestCase("tcp://192.168.1.10:2375")]
    [TestCase("tcp://[2001:db8::1]:2375")]
    [TestCase("tcp://127.0.0.1.evil.example:2375")]
    public void Endpoint_RefusesPlainTcpToNonLoopback(string host)
    {
        var endpoint = DockerTransport.ParseEndpoint(host, false);

        Assert.That(endpoint.Problem, Is.Not.Null);
        Assert.That(DockerTransport.Create(host, null).Client, Is.Null);
        Assert.That(DockerTransport.Create(host, new DockerTlsSettings("", "", "")).Client, Is.Null);
    }

    [TestCase("")]
    [TestCase("http://127.0.0.1:2375")]
    [TestCase("npipe://./pipe/docker")]
    [TestCase("unix://")]
    [TestCase("tcp://")]
    [TestCase("tcp://host:2375/path")]
    public void Endpoint_RefusesOtherSchemesAndMalformedAddresses(string host)
    {
        Assert.That(DockerTransport.ParseEndpoint(host, true).Problem, Is.Not.Null);
    }

    [Test]
    public void Endpoint_TlsSettingsNeedBothFiles()
    {
        Assert.That(new DockerTlsSettings("a.pfx", "", "ca.pem").IsConfigured, Is.True);
        Assert.That(new DockerTlsSettings("a.pfx", "", "").IsConfigured, Is.False);
        Assert.That(new DockerTlsSettings("", "", "ca.pem").IsConfigured, Is.False);
    }

    [Test]
    public void Endpoint_MissingTlsFilesGiveAProblemNotAnException()
    {
        var created = DockerTransport.Create("tcp://10.0.0.5:2376",
            new DockerTlsSettings("/nonexistent/client.pfx", "", "/nonexistent/ca.pem"));

        Assert.That(created.Client, Is.Null);
        Assert.That(created.Problem, Is.Not.Null);
    }

    [Test]
    public void Choose_EmptyHostUsesTheBotConnection()
    {
        var tls = new DockerTlsSettings("", "", "");

        Assert.That(CodeRunDockerClient.Choose("", true, tls).Source, Is.EqualTo(CodeRunEndpointSource.BotConnection));
        Assert.That(CodeRunDockerClient.Choose("  ", true, tls).Source,
            Is.EqualTo(CodeRunEndpointSource.BotConnection));
    }

    [Test]
    public void Choose_EmptyHostWithoutConnectionIsUnavailable()
    {
        var choice = CodeRunDockerClient.Choose("", false, new DockerTlsSettings("", "", ""));

        Assert.That(choice.Source, Is.EqualTo(CodeRunEndpointSource.Unavailable));
        Assert.That(choice.Problem, Is.Not.Null);
    }

    [Test]
    public void Choose_ConfiguredHostWinsEvenWithoutABotConnection()
    {
        var tls = new DockerTlsSettings("", "", "");

        Assert.That(CodeRunDockerClient.Choose("tcp://127.0.0.1:2375", false, tls).Source,
            Is.EqualTo(CodeRunEndpointSource.Configured));
        Assert.That(CodeRunDockerClient.Choose("unix:///run/sandbox.sock", true, tls).Source,
            Is.EqualTo(CodeRunEndpointSource.Configured));
    }

    [Test]
    public void Choose_ConfiguredHostNeverFallsBackToTheBotConnection()
    {
        var choice = CodeRunDockerClient.Choose("tcp://10.0.0.5:2375", true, new DockerTlsSettings("", "", ""));

        Assert.That(choice.Source, Is.EqualTo(CodeRunEndpointSource.Unavailable));
        Assert.That(choice.Problem, Does.Contain("loopback"));
    }

    [Test]
    public void Choose_RemoteTcpWithTlsIsConfigured()
    {
        var choice = CodeRunDockerClient.Choose("tcp://10.0.0.5:2376", false,
            new DockerTlsSettings("c.pfx", "", "ca.pem"));

        Assert.That(choice.Source, Is.EqualTo(CodeRunEndpointSource.Configured));
    }

    private static X509Certificate2 CreateAuthority(string name)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest($"CN={name}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        using var created = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        return X509CertificateLoader.LoadCertificate(created.Export(X509ContentType.Cert));
    }

    private static (X509Certificate2 Authority, X509Certificate2 Server) CreateAuthorityAndServer()
    {
        using var authorityKey = RSA.Create(2048);
        var authorityRequest = new CertificateRequest("CN=Test CA", authorityKey, HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        authorityRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        authorityRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        using var authority = authorityRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(1));

        using var serverKey = RSA.Create(2048);
        var serverRequest = new CertificateRequest("CN=sandbox", serverKey, HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        using var server = serverRequest.Create(authority, DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddHours(12), RandomNumberGenerator.GetBytes(8));
        return (X509CertificateLoader.LoadCertificate(authority.Export(X509ContentType.Cert)),
            X509CertificateLoader.LoadCertificate(server.Export(X509ContentType.Cert)));
    }

    [Test]
    public void ServerCertificate_AcceptedOnlyWhenItChainsToTheConfiguredAuthority()
    {
        var (authority, server) = CreateAuthorityAndServer();
        var other = CreateAuthority("Other CA");

        Assert.That(DockerTransport.ValidateServerCertificate(server, SslPolicyErrors.RemoteCertificateChainErrors,
            [authority]), Is.True);
        Assert.That(DockerTransport.ValidateServerCertificate(server, SslPolicyErrors.RemoteCertificateChainErrors,
            [other]), Is.False);
        Assert.That(DockerTransport.ValidateServerCertificate(server, SslPolicyErrors.RemoteCertificateNameMismatch,
            [authority]), Is.False);
        Assert.That(DockerTransport.ValidateServerCertificate(null, SslPolicyErrors.None, [authority]), Is.False);
    }

    private static (string CertPem, string KeyPem, byte[] Pfx) CreateClientMaterial()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=bot", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        return (cert.ExportCertificatePem(), key.ExportPkcs8PrivateKeyPem(), cert.Export(X509ContentType.Pfx, "pw"));
    }

    [Test]
    public void InlinePem_LoadsClientCertificateWithItsKey()
    {
        var (certPem, keyPem, _) = CreateClientMaterial();
        var tls = new DockerTlsSettings("", "", "", certPem, keyPem, CreateAuthority("CA").ExportCertificatePem());

        using var cert = DockerTransport.LoadClientCertificate(tls);

        Assert.That(tls.IsConfigured, Is.True);
        Assert.That(tls.HasInlineClientCertificate, Is.True);
        Assert.That(cert.HasPrivateKey, Is.True);
        Assert.That(cert.Subject, Is.EqualTo("CN=bot"));
        Assert.That(DockerTransport.LoadAuthorities(tls), Has.Count.EqualTo(1));
    }

    [Test]
    public void InlinePem_NeedsBothCertificateAndKeyAndAnAuthority()
    {
        var (certPem, keyPem, _) = CreateClientMaterial();
        var ca = CreateAuthority("CA").ExportCertificatePem();

        Assert.That(new DockerTlsSettings("", "", "", certPem, "", ca).IsConfigured, Is.False);
        Assert.That(new DockerTlsSettings("", "", "", "", keyPem, ca).IsConfigured, Is.False);
        Assert.That(new DockerTlsSettings("", "", "", certPem, keyPem, "").IsConfigured, Is.False);
        Assert.That(new DockerTlsSettings("", "", "", certPem, keyPem, ca).IsConfigured, Is.True);
    }

    [Test]
    public void InlinePem_BadTextIsAnErrorNotAnAuthority()
    {
        Assert.Throws<IOException>(() => DockerTransport.LoadAuthorities(new DockerTlsSettings("", "", "", "", "",
            "not a certificate")));
        var created = DockerTransport.Create("tcp://10.0.0.5:2376",
            new DockerTlsSettings("", "", "", "bad", "bad", "bad"));
        Assert.That(created.Client, Is.Null);
        Assert.That(created.Problem, Is.Not.Null);
    }

    [Test]
    public void InlinePem_WorksForARemoteHost()
    {
        var (certPem, keyPem, _) = CreateClientMaterial();
        var tls = new DockerTlsSettings("", "", "", certPem, keyPem, CreateAuthority("CA").ExportCertificatePem());

        var created = DockerTransport.Create("tcp://10.0.0.5:2376", tls);

        Assert.That(created.Problem, Is.Null);
        Assert.That(created.Client, Is.Not.Null);
        created.Client!.Dispose();
    }

    [Test]
    public void ExportPem_ConvertsFilesToInlinePem()
    {
        var (_, _, pfx) = CreateClientMaterial();
        var directory = Directory.CreateTempSubdirectory("coderun-tests");
        try
        {
            var pfxFile = Path.Combine(directory.FullName, "client.pfx");
            var caFile = Path.Combine(directory.FullName, "ca.pem");
            File.WriteAllBytes(pfxFile, pfx);
            File.WriteAllText(caFile, CreateAuthority("CA").ExportCertificatePem());

            var pem = DockerTransport.ExportPem(new DockerTlsSettings(pfxFile, "pw", caFile));

            Assert.That(pem, Is.Not.Null);
            var inline = new DockerTlsSettings("", "", "", pem!.Value.CertPem, pem.Value.KeyPem, pem.Value.CaPem);
            using var cert = DockerTransport.LoadClientCertificate(inline);
            Assert.That(cert.HasPrivateKey, Is.True);
            Assert.That(DockerTransport.LoadAuthorities(inline), Has.Count.EqualTo(1));
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [Test]
    public void ExportPem_PassesInlineThroughAndRejectsMissingFiles()
    {
        var (certPem, keyPem, _) = CreateClientMaterial();
        var ca = CreateAuthority("CA").ExportCertificatePem();

        var pem = DockerTransport.ExportPem(new DockerTlsSettings("", "", "", certPem, keyPem, ca));

        Assert.That(pem, Is.EqualTo((certPem, keyPem, ca)));
        Assert.That(DockerTransport.ExportPem(new DockerTlsSettings("/nope.pfx", "", "/nope.pem")), Is.Null);
        Assert.That(DockerTransport.ExportPem(new DockerTlsSettings("", "", "")), Is.Null);
    }

    private static JsonObject Child(CustomBotCodeRun? codeRun)
    {
        return CustomBotChildCredentials.Build(null, new CustomBotChildInputs("token", 5101, "mewdeko-custom-1",
            "api", "jwt", "Host=db", "redis:6379", [1UL], null, codeRun));
    }

    private static readonly string[] CodeRunKeys =
    [
        "CodeRunEnabled", "CodeRunRuntime", "CodeRunDockerHost", "CodeRunDockerCertFile", "CodeRunDockerCertPassword",
        "CodeRunDockerCaFile", "CodeRunDockerCertPem", "CodeRunDockerKeyPem", "CodeRunDockerCaPem",
        "CodeRunDockerHostForCustomBots"
    ];

    [Test]
    public void ChildCredentials_FeatureOffGivesNothing()
    {
        var (certPem, keyPem, _) = CreateClientMaterial();
        var tls = new DockerTlsSettings("", "", "", certPem, keyPem, "ca");

        var codeRun = CustomBotCodeRun.For(false, "runsc", "tcp://sandbox:2376", tls);
        var child = Child(codeRun);

        Assert.That(codeRun, Is.Null);
        foreach (var key in CodeRunKeys)
            Assert.That(child.ContainsKey(key), Is.False, key);
    }

    [Test]
    public void ChildCredentials_EmptyHostForCustomBotsGivesNothing()
    {
        var (certPem, keyPem, _) = CreateClientMaterial();
        var tls = new DockerTlsSettings("", "", "", certPem, keyPem, "ca");

        foreach (var host in new[] { "", "  " })
        {
            var codeRun = CustomBotCodeRun.For(true, "runsc", host, tls);
            var child = Child(codeRun);

            Assert.That(codeRun, Is.Null);
            foreach (var key in CodeRunKeys)
                Assert.That(child.ContainsKey(key), Is.False, key);
        }
    }

    [Test]
    public void ChildCredentials_EnabledWithHostCarriesTheSettingsAndInlinePem()
    {
        var (certPem, keyPem, _) = CreateClientMaterial();
        var ca = CreateAuthority("CA").ExportCertificatePem();
        var tls = new DockerTlsSettings("/host/only/client.pfx", "secret", "/host/only/ca.pem", certPem, keyPem, ca);

        var child = Child(CustomBotCodeRun.For(true, "runsc", " tcp://sandbox.internal:2376 ", tls));

        Assert.That(child["CodeRunEnabled"]!.GetValue<bool>(), Is.True);
        Assert.That(child["CodeRunRuntime"]!.GetValue<string>(), Is.EqualTo("runsc"));
        Assert.That(child["CodeRunDockerHost"]!.GetValue<string>(), Is.EqualTo("tcp://sandbox.internal:2376"));
        Assert.That(child["CodeRunDockerCertPem"]!.GetValue<string>(), Is.EqualTo(certPem));
        Assert.That(child["CodeRunDockerKeyPem"]!.GetValue<string>(), Is.EqualTo(keyPem));
        Assert.That(child["CodeRunDockerCaPem"]!.GetValue<string>(), Is.EqualTo(ca));
        foreach (var key in new[] { "CodeRunDockerCertFile", "CodeRunDockerCertPassword", "CodeRunDockerCaFile",
                     "CodeRunDockerHostForCustomBots" })
            Assert.That(child.ContainsKey(key), Is.False, key);
        Assert.That(child.ToJsonString(), Does.Not.Contain("/host/only"));
        Assert.That(child.ToJsonString(), Does.Not.Contain("secret"));
    }

    [Test]
    public void ChildCredentials_ReadsMasterFilesIntoInlinePem()
    {
        var (_, _, pfx) = CreateClientMaterial();
        var ca = CreateAuthority("CA").ExportCertificatePem();
        var directory = Directory.CreateTempSubdirectory("coderun-tests");
        try
        {
            var pfxFile = Path.Combine(directory.FullName, "client.pfx");
            var caFile = Path.Combine(directory.FullName, "ca.pem");
            File.WriteAllBytes(pfxFile, pfx);
            File.WriteAllText(caFile, ca);

            var child = Child(CustomBotCodeRun.For(true, "", "tcp://sandbox.internal:2376",
                new DockerTlsSettings(pfxFile, "pw", caFile)));

            Assert.That(child["CodeRunDockerCertPem"]!.GetValue<string>(), Does.Contain("BEGIN CERTIFICATE"));
            Assert.That(child["CodeRunDockerKeyPem"]!.GetValue<string>(), Does.Contain("PRIVATE KEY"));
            Assert.That(child["CodeRunDockerCaPem"]!.GetValue<string>(), Is.EqualTo(ca));
            Assert.That(child["CodeRunRuntime"]!.GetValue<string>(), Is.EqualTo(""));
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [Test]
    public void ChildCredentials_NoTlsGivesEmptyPemAndTheChildRefusesRemotePlainTcp()
    {
        var codeRun = CustomBotCodeRun.For(true, "", "tcp://10.0.0.5:2375", new DockerTlsSettings("", "", ""));

        Assert.That(codeRun!.CertPem, Is.Empty);
        Assert.That(DockerTransport.Create(codeRun.Host,
            new DockerTlsSettings("", "", "", codeRun.CertPem, codeRun.KeyPem, codeRun.CaPem)).Client, Is.Null);
    }

    [Test]
    public void ChildCredentials_ParentKeysNeverLeakThroughTheAllowlist()
    {
        var master = JsonNode.Parse(
            """{"CodeRunEnabled": true, "CodeRunDockerHost": "tcp://127.0.0.1:2375", "CodeRunDockerKeyPem": "k", "CodeRunDockerHostForCustomBots": "tcp://x:1"}""")!.AsObject();

        var child = CustomBotChildCredentials.Build(master, new CustomBotChildInputs("t", 5101, "c", "a", "j", "d",
            "r", [1UL], null));

        foreach (var key in CodeRunKeys)
            Assert.That(child.ContainsKey(key), Is.False, key);
    }
}
