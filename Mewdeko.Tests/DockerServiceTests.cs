using System.Buffers.Binary;
using System.Text;
using Mewdeko.Modules.OwnerOnly.Services;

namespace Mewdeko.Tests;

/// <summary>
///     Pins down how the daemon's log stream is turned into lines: frames are demultiplexed by stream, a line
///     split across frames is reassembled, the timestamp prefix is separated from the text, and a TTY container's
///     unframed stream is split on newlines alone.
/// </summary>
[TestFixture]
public class DockerServiceTests
{
    /// <summary>
    ///     Builds one frame as the daemon writes it: a stream byte, three zero bytes, a big endian length and the
    ///     payload.
    /// </summary>
    private static byte[] Frame(byte stream, string text)
    {
        var payload = Encoding.UTF8.GetBytes(text);
        var frame = new byte[8 + payload.Length];
        frame[0] = stream;
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(4, 4), (uint)payload.Length);
        payload.CopyTo(frame, 8);
        return frame;
    }

    private static byte[] Concat(params byte[][] frames)
    {
        return frames.SelectMany(f => f).ToArray();
    }

    [Test]
    public void Multiplexed_SeparatesStreamsAndTimestamps()
    {
        var payload = Concat(
            Frame(1, "2026-09-20T10:00:00.000000001Z hello\n"),
            Frame(2, "2026-09-20T10:00:00.000000002Z oh no\n"));

        var lines = DockerService.ParseMultiplexedLog(payload);

        Assert.That(lines, Has.Count.EqualTo(2));
        Assert.That(lines[0].Timestamp, Is.EqualTo("2026-09-20T10:00:00.000000001Z"));
        Assert.That(lines[0].Text, Is.EqualTo("hello"));
        Assert.That(lines[0].IsError, Is.False);
        Assert.That(lines[1].Text, Is.EqualTo("oh no"));
        Assert.That(lines[1].IsError, Is.True);
    }

    [Test]
    public void Multiplexed_ReassemblesLineSplitAcrossFrames()
    {
        var payload = Concat(
            Frame(1, "2026-09-20T10:00:00.000000001Z first ha"),
            Frame(1, "lf and second half\n"));

        var lines = DockerService.ParseMultiplexedLog(payload);

        Assert.That(lines, Has.Count.EqualTo(1));
        Assert.That(lines[0].Text, Is.EqualTo("first half and second half"));
    }

    [Test]
    public void Multiplexed_KeepsUnterminatedTail()
    {
        var payload = Frame(1, "2026-09-20T10:00:00.000000001Z done\n2026-09-20T10:00:00.000000002Z still writing");

        var lines = DockerService.ParseMultiplexedLog(payload);

        Assert.That(lines.Select(l => l.Text), Is.EqualTo(new[]
        {
            "done", "still writing"
        }));
    }

    [Test]
    public void Multiplexed_OrdersInterleavedStreamsByTimestamp()
    {
        var payload = Concat(
            Frame(2, "2026-09-20T10:00:00.000000003Z err\n"),
            Frame(1, "2026-09-20T10:00:00.000000001Z out\n"));

        var lines = DockerService.ParseMultiplexedLog(payload);

        Assert.That(lines.Select(l => l.Text), Is.EqualTo(new[]
        {
            "out", "err"
        }));
    }

    [Test]
    public void Multiplexed_TruncatedFrameDoesNotThrow()
    {
        var frame = Frame(1, "2026-09-20T10:00:00.000000001Z cut off here\n");
        var payload = frame[..(frame.Length - 5)];

        var lines = DockerService.ParseMultiplexedLog(payload);

        Assert.That(lines, Has.Count.EqualTo(1));
        Assert.That(lines[0].Text, Is.EqualTo("cut off "));
    }

    [Test]
    public void Tty_SplitsOnNewlinesWithoutHeaders()
    {
        var payload = Encoding.UTF8.GetBytes(
            "2026-09-20T10:00:00.000000001Z one\r\n2026-09-20T10:00:00.000000002Z two\n");

        var lines = DockerService.ParseTtyLog(payload);

        Assert.That(lines.Select(l => l.Text), Is.EqualTo(new[]
        {
            "one", "two"
        }));
        Assert.That(lines.All(l => !l.IsError), Is.True);
    }

    [Test]
    public void LineWithoutTimestamp_KeepsWholeText()
    {
        var lines = DockerService.ParseTtyLog(Encoding.UTF8.GetBytes("plain text line\n"));

        Assert.That(lines[0].Timestamp, Is.Empty);
        Assert.That(lines[0].Text, Is.EqualTo("plain text line"));
    }
}
