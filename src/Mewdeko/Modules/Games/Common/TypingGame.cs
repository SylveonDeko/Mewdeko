using System.Diagnostics;
using System.Threading.Tasks;
using CommandLine;
using Mewdeko.Modules.Games.Services;
using Serilog;

namespace Mewdeko.Modules.Games.Common;

public class TypingGame
{
    public const float WordValue = 4.5f;
    private readonly DiscordSocketClient client;
    private readonly GamesService games;
    private readonly Options options;
    private readonly string? prefix;
    private readonly List<ulong> finishedUserIds;
    private readonly Stopwatch sw;

    public TypingGame(GamesService games, DiscordSocketClient client, ITextChannel channel,
        string? prefix, Options options)
    {
        this.games = games;
        this.client = client;
        this.prefix = prefix;
        this.options = options;

        Channel = channel;
        IsActive = false;
        sw = new Stopwatch();
        finishedUserIds = new List<ulong>();
    }

    public ITextChannel? Channel { get; }
    public string? CurrentSentence { get; private set; }
    public bool IsActive { get; private set; }

    public async Task<bool> Stop()
    {
        if (!IsActive) return false;
        client.MessageReceived -= AnswerReceived;
        finishedUserIds.Clear();
        IsActive = false;
        sw.Stop();
        sw.Reset();
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
        var i = (int)(CurrentSentence.Length / WordValue * 1.7f);
        try
        {
            await Channel
                .SendConfirmAsync(
                    $@":clock2: Next contest will last for {i} seconds. Type the bolded text as fast as you can.")
                .ConfigureAwait(false);

            var time = options.StartTime;

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
            sw.Start();
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
        if (games.TypingArticles.Count > 0)
            return games.TypingArticles[new MewdekoRandom().Next(0, games.TypingArticles.Count)].Text;
        return $"No typing articles found. Use {prefix}typeadd command to add a new article for typing.";
    }

    private void HandleAnswers() => client.MessageReceived += AnswerReceived;

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
                if (decision && !finishedUserIds.Contains(msg.Author.Id))
                {
                    var elapsed = sw.Elapsed;
                    var wpm = CurrentSentence.Length / WordValue / elapsed.TotalSeconds * 60;
                    finishedUserIds.Add(msg.Author.Id);
                    await Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                            .WithTitle($"{msg.Author} finished the race!")
                            .AddField(efb =>
                                efb.WithName("Place").WithValue($"#{finishedUserIds.Count}").WithIsInline(true))
                            .AddField(efb =>
                                efb.WithName("WPM").WithValue($"{wpm:F1} *[{elapsed.TotalSeconds:F2}sec]*")
                                    .WithIsInline(true))
                            .AddField(efb =>
                                efb.WithName("Errors").WithValue(distance.ToString()).WithIsInline(true)))
                        .ConfigureAwait(false);
                    if (finishedUserIds.Count % 4 == 0)
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