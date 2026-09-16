using System.Text;
using Mewdeko.Modules.OwnerOnly.Common;
using Mewdeko.Modules.OwnerOnly.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Mewdeko.Tests;

/// <summary>
///     Pins down the byte offset arithmetic the dashboard relies on to follow a pm2 log live: a tail must end on a
///     line boundary, the next read must pick up exactly where it left off, a half written line must wait until it
///     is finished, and a file that shrank must come back as a fresh tail.
/// </summary>
[TestFixture]
public class Pm2LogServiceTests
{
    [SetUp]
    public void Setup()
    {
        service = new Pm2LogService(NullLogger<Pm2LogService>.Instance);
        path = Path.Combine(Path.GetTempPath(), $"mewdeko-pm2-test-{Guid.NewGuid():N}.log");
    }

    [TearDown]
    public void TearDown()
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    private Pm2LogService service = null!;
    private string path = null!;

    private void Write(string content)
    {
        File.WriteAllText(path, content, new UTF8Encoding(false));
    }

    private void Append(string content)
    {
        File.AppendAllText(path, content, new UTF8Encoding(false));
    }

    [Test]
    public async Task Tail_ReturnsLastCompleteLines()
    {
        Write("one\ntwo\nthree\nfour\n");

        var chunk = await service.ReadTailAsync(path, Pm2LogStream.Out, 2);

        Assert.That(chunk, Is.Not.Null);
        Assert.That(chunk!.Lines, Is.EqualTo(new[]
        {
            "three", "four"
        }));
        Assert.That(chunk.End, Is.EqualTo(new FileInfo(path).Length));
        Assert.That(chunk.Start, Is.EqualTo(Encoding.UTF8.GetByteCount("one\ntwo\n")));
        Assert.That(chunk.Truncated, Is.False);
    }

    [Test]
    public async Task Tail_LeavesPartialLastLineForNextRead()
    {
        Write("one\ntwo\nthr");

        var chunk = await service.ReadTailAsync(path, Pm2LogStream.Out, 10);

        Assert.That(chunk!.Lines, Is.EqualTo(new[]
        {
            "one", "two"
        }));
        Assert.That(chunk.End, Is.EqualTo(Encoding.UTF8.GetByteCount("one\ntwo\n")));

        Append("ee\nfour\n");
        var next = await service.ReadAfterAsync(path, Pm2LogStream.Out, chunk.End, 10);

        Assert.That(next!.Lines, Is.EqualTo(new[]
        {
            "three", "four"
        }));
        Assert.That(next.Rotated, Is.False);
        Assert.That(next.End, Is.EqualTo(new FileInfo(path).Length));
    }

    [Test]
    public async Task After_ReturnsNothingWhenUnchanged()
    {
        Write("one\ntwo\n");
        var length = new FileInfo(path).Length;

        var chunk = await service.ReadAfterAsync(path, Pm2LogStream.Out, length, 10);

        Assert.That(chunk!.Lines, Is.Empty);
        Assert.That(chunk.Start, Is.EqualTo(length));
        Assert.That(chunk.End, Is.EqualTo(length));
    }

    [Test]
    public async Task After_StripsCarriageReturnsAndKeepsUnicode()
    {
        Write("");
        Append("héllo wörld\r\n[INF] ✓ done\r\n");

        var chunk = await service.ReadAfterAsync(path, Pm2LogStream.Out, 0, 10);

        Assert.That(chunk!.Lines, Is.EqualTo(new[]
        {
            "héllo wörld", "[INF] ✓ done"
        }));
    }

    [Test]
    public async Task After_FallsBackToTailWhenFileShrank()
    {
        Write("a long first incarnation of the log\nwith several lines\n");
        var oldLength = new FileInfo(path).Length;

        Write("fresh\n");
        var chunk = await service.ReadAfterAsync(path, Pm2LogStream.Out, oldLength, 10);

        Assert.That(chunk!.Rotated, Is.True);
        Assert.That(chunk.Lines, Is.EqualTo(new[]
        {
            "fresh"
        }));
        Assert.That(chunk.End, Is.EqualTo(new FileInfo(path).Length));
    }

    [Test]
    public void ParseJlist_ReadsProcessTableAroundNotices()
    {
        const string output = """
                              [PM2] Spawning PM2 daemon with pm2_home=/home/rootish/.pm2
                              [{"pid":4242,"name":"mewdeko","pm2_env":{"status":"online","pm_uptime":1757980800000,"restart_time":3,"exec_mode":"fork_mode","pm_exec_path":"/usr/bin/dotnet","pm_out_log_path":"/home/rootish/.pm2/logs/mewdeko-out.log","pm_err_log_path":"/home/rootish/.pm2/logs/mewdeko-error.log","pm_id":0},"pm_id":0,"monit":{"memory":734003200,"cpu":12.5}},{"pid":0,"name":"lavalink","pm2_env":{"status":"stopped","pm_uptime":0,"restart_time":0,"pm_out_log_path":"/home/rootish/.pm2/logs/lavalink-out.log","pm_err_log_path":"/home/rootish/.pm2/logs/lavalink-error.log","pm_id":1},"pm_id":1,"monit":{"memory":0,"cpu":0}}]
                              """;

        var list = service.ParseJlist(output);

        Assert.That(list, Is.Not.Null);
        Assert.That(list!.Source, Is.EqualTo(Pm2ListSource.Daemon));
        Assert.That(list.Processes, Has.Count.EqualTo(2));

        var bot = list.Processes[0];
        Assert.That(bot.PmId, Is.EqualTo(0));
        Assert.That(bot.Name, Is.EqualTo("mewdeko"));
        Assert.That(bot.Status, Is.EqualTo("online"));
        Assert.That(bot.Pid, Is.EqualTo(4242));
        Assert.That(bot.Cpu, Is.EqualTo(12.5));
        Assert.That(bot.MemoryBytes, Is.EqualTo(734003200));
        Assert.That(bot.Restarts, Is.EqualTo(3));
        Assert.That(bot.StartedAt, Is.EqualTo(new DateTime(2025, 9, 16, 0, 0, 0, DateTimeKind.Utc)));
        Assert.That(bot.OutLogPath, Is.EqualTo("/home/rootish/.pm2/logs/mewdeko-out.log"));
        Assert.That(bot.ErrorLogPath, Is.EqualTo("/home/rootish/.pm2/logs/mewdeko-error.log"));

        var stopped = list.Processes[1];
        Assert.That(stopped.Pid, Is.Null);
        Assert.That(stopped.StartedAt, Is.Null);
        Assert.That(stopped.Status, Is.EqualTo("stopped"));
    }

    [Test]
    public void ParseJlist_HandlesEmptyTableAndGarbage()
    {
        var empty = service.ParseJlist("[]\n");
        Assert.That(empty, Is.Not.Null);
        Assert.That(empty!.Processes, Is.Empty);
        Assert.That(empty.Message, Is.Not.Null);

        Assert.That(service.ParseJlist("pm2: command not found"), Is.Null);
    }

    [Test]
    public async Task Tail_ReportsMissingFile()
    {
        var chunk = await service.ReadTailAsync(path, Pm2LogStream.Out, 10);

        Assert.That(chunk, Is.Null);
    }

    [Test]
    public async Task Tail_SkipsPartialFirstLineWhenCapped()
    {
        var builder = new StringBuilder();
        for (var i = 0; i < 40000; i++)
            builder.Append("line ").Append(i).Append(" padding padding padding padding padding\n");
        Write(builder.ToString());

        var chunk = await service.ReadTailAsync(path, Pm2LogStream.Out, Pm2LogService.MaxLines);

        Assert.That(chunk!.Truncated, Is.False);
        Assert.That(chunk.Lines, Has.Count.EqualTo(Pm2LogService.MaxLines));
        Assert.That(chunk.Lines[0], Does.StartWith("line "));
        Assert.That(chunk.Lines[^1], Does.StartWith("line 39999"));
    }
}