using System.Buffers.Binary;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Mewdeko.Modules.Utility.Services;

/// <summary>
///     Reads and prepares the output of a run: demultiplexes Docker's log stream under a size cap and turns the
///     text into something safe to put in a code block.
/// </summary>
public static class CodeRunOutput
{
    private static readonly Regex BacktickRun = new("`{3,}", RegexOptions.Compiled);

    /// <summary>
    ///     The most output bytes read from a container.
    /// </summary>
    public const int ReadLimit = 64 * 1024;

    /// <summary>
    ///     The most characters of output shown in a reply.
    /// </summary>
    public const int DisplayLimit = 1500;

    /// <summary>
    ///     The most lines of output shown in a reply.
    /// </summary>
    public const int DisplayLines = 40;

    /// <summary>
    ///     Reads the frames Docker writes for a container without a terminal and joins stdout and stderr in the
    ///     order they arrived, stopping once the cap is reached.
    /// </summary>
    /// <param name="stream">The logs response body.</param>
    /// <param name="limit">The most payload bytes to keep.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The kept bytes, and whether the stream held more than the cap.</returns>
    public static async Task<(byte[] Data, bool Cut)> ReadDemultiplexedAsync(Stream stream, int limit,
        CancellationToken cancellationToken = default)
    {
        var result = new MemoryStream();
        var header = new byte[8];
        while (true)
        {
            if (await FillAsync(stream, header, cancellationToken) < header.Length)
                return (result.ToArray(), false);

            var length = (long)BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(4, 4));
            var room = limit - (int)result.Length;
            var take = (int)Math.Min(length, room);
            var chunk = new byte[take];
            var got = await FillAsync(stream, chunk, cancellationToken);
            result.Write(chunk, 0, got);
            if (got < take)
                return (result.ToArray(), false);

            if (length > take || (result.Length >= limit && await HasMoreAsync(stream, cancellationToken)))
                return (result.ToArray(), true);
        }
    }

    /// <summary>
    ///     Turns raw output into text for a code block: invalid bytes and control characters are dropped, triple
    ///     backticks are broken up so they cannot close the block, and long output is cut with a marker.
    /// </summary>
    /// <param name="data">The output bytes.</param>
    /// <param name="readCut">Whether the read cap already cut the output.</param>
    /// <param name="cutMarker">The text that marks a cut.</param>
    /// <param name="emptyText">The text shown when there is no output.</param>
    public static string Format(byte[] data, bool readCut, string cutMarker, string emptyText)
    {
        var text = Neutralise(Clean(Encoding.UTF8.GetString(data))).Trim('\n');
        if (text.Trim().Length == 0)
            return emptyText;

        var cut = readCut;
        var lines = text.Split('\n');
        if (lines.Length > DisplayLines)
        {
            text = string.Join('\n', lines.Take(DisplayLines));
            cut = true;
        }

        if (text.Length > DisplayLimit)
        {
            text = text[..DisplayLimit];
            cut = true;
        }

        return cut ? $"{text}\n{cutMarker}" : text;
    }

    /// <summary>
    ///     Breaks up every run of three backticks so output cannot close the reply's code block.
    /// </summary>
    /// <param name="text">The output text.</param>
    public static string Neutralise(string text)
    {
        return BacktickRun.Replace(text, match => string.Join('​', match.Value.ToCharArray()));
    }

    private static string Clean(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var c in text.Replace("\r\n", "\n"))
        {
            if (c is '\n' or '\t' || (!char.IsControl(c) && c != '�'))
                builder.Append(c);
        }

        return builder.ToString();
    }

    private static async Task<int> FillAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var read = 0;
        while (read < buffer.Length)
        {
            var count = await stream.ReadAsync(buffer.AsMemory(read), cancellationToken);
            if (count == 0)
                break;
            read += count;
        }

        return read;
    }

    private static async Task<bool> HasMoreAsync(Stream stream, CancellationToken cancellationToken)
    {
        var probe = new byte[1];
        return await stream.ReadAsync(probe, cancellationToken) > 0;
    }
}
