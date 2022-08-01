using CommandLine;
using Mewdeko.Modules.Games.Services;
using Serilog;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Mewdeko.Modules.Games.Common;

public class TypingGame
{
    public const float WORD_VALUE = 4.5f;
    private readonly DiscordSocketClient _client;
    private readonly GamesService _games;
    private readonly Options _options;
    private readonly string? _prefix;
    private readonly List<ulong> _finishedUserIds;
    private readonly Stopwatch _sw;

    public TypingGame(GamesService games, DiscordSocketClient client, ITextChannel channel,
        string? prefix, Options options)
    {
        _games = games;
        _client = client;
        _prefix = prefix;
        _options = options;

        Channel = channel;
        IsActive = false;
        _sw = new Stopwatch();
        _finishedUserIds = new List<ulong>();
    }

    public ITextChannel? Channel { get; }
    public string? CurrentSentence { get; private set; }
    public bool IsActive { get; private set; }

    public async Task<bool> Stop()
    {
        if (!IsActive) return false;
        _client.MessageReceived -= AnswerReceived;
        _finishedUserIds.Clear();
        IsActive = false;
        _sw.Stop();
        _sw.Reset();
        try
        {
            await Channel.SendConfirmAsync("Typing contest stopped.").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Warning(ex.ToString());
        }

        return true;
    }

    public async Task Start()
    {
        if (IsActive) return; // can't start running game
        IsActive = true;
        CurrentSentence = GetRandomSentence();
        var i = (int)(CurrentSentence.Length / WORD_VALUE * 1.7f);
        try
        {
            await Channel
                .SendConfirmAsync(
                    $@":clock2: Next contest will last for {i} seconds. Type the bolded text as fast as you can.")
                .ConfigureAwait(false);

            var time = _options.StartTime;

            var msg = await Channel.SendMessageAsync($"Starting new typing contest in **{time}**...",
                options: new RequestOptions
                {
                    RetryMode = RetryMode.AlwaysRetry
                }).ConfigureAwait(false);

            do
            {
                await Task.Delay(2000).ConfigureAwait(false);
                time -= 2;
                try
                {
                    await msg.ModifyAsync(m => m.Content = $"Starting new typing contest in **{time}**..")
                        .ConfigureAwait(false);
                }
                catch
                {
                    // ignored
                }
            } while (time > 2);

            await msg.ModifyAsync(m => m.Content = CurrentSentence.Replace(" ", " \x200B", StringComparison.InvariantCulture)).ConfigureAwait(false);
            _sw.Start();
            HandleAnswers();

            while (i > 0)
            {
                await Task.Delay(1000).ConfigureAwait(false);
                i--;
                if (!IsActive)
                    return;
            }
        }
        catch
        {
            // ignored
        }
        finally
        {
            await Stop().ConfigureAwait(false);
        }
    }

    public string? GetRandomSentence()
    {
        if (_games.TypingArticles.Count > 0)
            return _games.TypingArticles[new MewdekoRandom().Next(0, _games.TypingArticles.Count)].Text;
        return $"No typing articles found. Use {_prefix}typeadd command to add a new article for typing.";
    }

    private void HandleAnswers() => _client.MessageReceived += AnswerReceived;

    private Task AnswerReceived(SocketMessage imsg)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                if (imsg.Author.IsBot)
                    return;
                if (imsg is not SocketUserMessage msg)
                    return;

                if (Channel == null || Channel.Id != msg.Channel.Id) return;

                var guess = msg.Content;

                var distance = CurrentSentence.LevenshteinDistance(guess);
                var decision = Judge(distance, guess.Length);
                if (decision && !_finishedUserIds.Contains(msg.Author.Id))
                {
                    var elapsed = _sw.Elapsed;
                    var wpm = CurrentSentence.Length / WORD_VALUE / elapsed.TotalSeconds * 60;
                    _finishedUserIds.Add(msg.Author.Id);
                    await Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                            .WithTitle($"{msg.Author} finished the race!")
                            .AddField(efb =>
                                efb.WithName("Place").WithValue($"#{_finishedUserIds.Count}").WithIsInline(true))
                            .AddField(efb =>
                                efb.WithName("WPM").WithValue($"{wpm:F1} *[{elapsed.TotalSeconds:F2}sec]*")
                                    .WithIsInline(true))
                            .AddField(efb =>
                                efb.WithName("Errors").WithValue(distance.ToString()).WithIsInline(true)))
                        .ConfigureAwait(false);
                    if (_finishedUserIds.Count % 4 == 0)
                    {
                        await Channel.SendConfirmAsync(
                                         $":exclamation: A lot of people finished, here is the text for those still typing:\n\n**{Format.Sanitize(CurrentSentence.Replace(" ", " \x200B", StringComparison.InvariantCulture)).SanitizeMentions(true)}**")
                            .ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex.ToString());
            }
        });
        return Task.CompletedTask;
    }

    private static bool Judge(int errors, int textLength) => errors <= textLength / 25;

    public class Options : IMewdekoCommandOptions
    {
        [Option('s', "start-time", Default = 5, Required = false,
            HelpText = "How long does it take for the race to start. Default 5.")]
        public int StartTime { get; set; } = 5;

        public void NormalizeOptions()
        {
            if (StartTime is < 3 or > 30)
                StartTime = 5;
        }
    }
}