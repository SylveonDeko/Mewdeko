using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Discord.Net;
using Mewdeko.Services.strings;
using Serilog;

namespace Mewdeko.Modules.Games.Common.Trivia;

public class TriviaGame
{
    private readonly DiscordSocketClient client;
    private readonly GamesConfig config;
    private readonly ICurrencyService cs;
    private readonly SemaphoreSlim guessLock = new(1, 1);
    private readonly TriviaOptions options;

    private readonly TriviaQuestionPool questionPool;
    private readonly string? quitCommand;
    private readonly IBotStrings strings;
    private int timeoutCount;

    private CancellationTokenSource triviaCancelSource;

    public TriviaGame(IBotStrings strings, DiscordSocketClient client, GamesConfig config,
        IDataCache cache, ICurrencyService cs, IGuild guild, ITextChannel channel,
        TriviaOptions options, string? quitCommand)
    {
        questionPool = new TriviaQuestionPool(cache);
        this.strings = strings;
        this.client = client;
        this.config = config;
        this.cs = cs;
        this.options = options;
        this.quitCommand = quitCommand;

        Guild = guild;
        Channel = channel;
    }

    public IGuild Guild { get; }
    public ITextChannel Channel { get; }

    public TriviaQuestion CurrentQuestion { get; private set; }
    public HashSet<TriviaQuestion> OldQuestions { get; } = new();

    public ConcurrentDictionary<IGuildUser, int> Users { get; } = new();

    public bool GameActive { get; private set; }
    public bool ShouldStopGame { get; private set; }

    private string? GetText(string? key, params object?[] replacements) => strings.GetText(key, Channel.GuildId, replacements);

    public async Task StartGame()
    {
        var showHowToQuit = false;
        while (!ShouldStopGame)
        {
            // reset the cancellation source
            triviaCancelSource = new CancellationTokenSource();
            showHowToQuit = !showHowToQuit;

            // load question
            CurrentQuestion = questionPool.GetRandomQuestion(OldQuestions);
            if (string.IsNullOrWhiteSpace(CurrentQuestion.Answer) ||
                string.IsNullOrWhiteSpace(CurrentQuestion.Question))
            {
                await Channel.SendErrorAsync(GetText("trivia_game"), GetText("failed_loading_question"))
                    .ConfigureAwait(false);
                return;
            }

            OldQuestions.Add(CurrentQuestion); //add it to exclusion list so it doesn't show up again

            EmbedBuilder questionEmbed;
            IUserMessage questionMessage;
            try
            {
                questionEmbed = new EmbedBuilder().WithOkColor()
                    .WithTitle(GetText("trivia_game"))
                    .AddField(eab => eab.WithName(GetText("category")).WithValue(CurrentQuestion.Category))
                    .AddField(eab => eab.WithName(GetText("question")).WithValue(CurrentQuestion.Question));

                if (showHowToQuit)
                    questionEmbed.WithFooter(GetText("trivia_quit", quitCommand));

                if (Uri.IsWellFormedUriString(CurrentQuestion.ImageUrl, UriKind.Absolute))
                    questionEmbed.WithImageUrl(CurrentQuestion.ImageUrl);

                questionMessage = await Channel.EmbedAsync(questionEmbed).ConfigureAwait(false);
            }
            catch (HttpException ex) when (ex.HttpCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden or HttpStatusCode.BadRequest)
            {
                return;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error sending trivia embed");
                await Task.Delay(2000).ConfigureAwait(false);
                continue;
            }

            //receive messages
            try
            {
                client.MessageReceived += PotentialGuess;

                //allow people to guess
                GameActive = true;
                try
                {
                    //hint
                    await Task.Delay(options.QuestionTimer * 1000 / 2, triviaCancelSource.Token)
                        .ConfigureAwait(false);
                    if (!options.NoHint)
                    {
                        try
                        {
                            await questionMessage.ModifyAsync(m =>
                                    m.Embed = questionEmbed
                                        .WithFooter(efb => efb.WithText(CurrentQuestion.GetHint())).Build())
                                .ConfigureAwait(false);
                        }
                        catch (HttpException ex) when (ex.HttpCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden)
                        {
                            break;
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, "Error editing triva message");
                        }
                    }

                    //timeout
                    await Task.Delay(options.QuestionTimer * 1000 / 2, triviaCancelSource.Token)
                        .ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                    timeoutCount = 0;
                } //means someone guessed the answer
            }
            finally
            {
                GameActive = false;
                client.MessageReceived -= PotentialGuess;
            }

            if (!triviaCancelSource.IsCancellationRequested)
            {
                try
                {
                    var embed = new EmbedBuilder().WithErrorColor()
                        .WithTitle(GetText("trivia_game"))
                        .WithDescription(GetText("trivia_times_up", Format.Bold(CurrentQuestion.Answer)));
                    if (Uri.IsWellFormedUriString(CurrentQuestion.AnswerImageUrl, UriKind.Absolute))
                        embed.WithImageUrl(CurrentQuestion.AnswerImageUrl);

                    await Channel.EmbedAsync(embed).ConfigureAwait(false);

                    if (options.Timeout != 0 && ++timeoutCount >= options.Timeout)
                        await StopGame().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Error sending trivia time's up message");
                }
            }

            await Task.Delay(5000).ConfigureAwait(false);
        }
    }

    public async Task EnsureStopped()
    {
        ShouldStopGame = true;

        await Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
            .WithAuthor(eab => eab.WithName("Trivia Game Ended"))
            .WithTitle("Final Results")
            .WithDescription(GetLeaderboard())).ConfigureAwait(false);
    }

    public async Task StopGame()
    {
        var old = ShouldStopGame;
        ShouldStopGame = true;
        if (!old)
        {
            try
            {
                await Channel.SendConfirmAsync(GetText("trivia_game"), GetText("trivia_stopping"))
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error sending trivia stopping message");
            }
        }
    }

    private Task PotentialGuess(SocketMessage imsg)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                if (imsg.Author.IsBot)
                    return;

                var umsg = imsg as SocketUserMessage;

                if (umsg?.Channel is not ITextChannel textChannel || textChannel.Guild != Guild)
                    return;

                var guildUser = (IGuildUser)umsg.Author;

                var guess = false;
                await guessLock.WaitAsync().ConfigureAwait(false);
                try
                {
                    if (GameActive && CurrentQuestion.IsAnswerCorrect(umsg.Content) &&
                        !triviaCancelSource.IsCancellationRequested)
                    {
                        Users.AddOrUpdate(guildUser, 1, (_, old) => ++old);
                        guess = true;
                    }
                }
                finally
                {
                    guessLock.Release();
                }

                if (!guess) return;
                triviaCancelSource.Cancel();

                if (options.WinRequirement != 0 && Users[guildUser] == options.WinRequirement)
                {
                    ShouldStopGame = true;
                    try
                    {
                        var embedS = new EmbedBuilder().WithOkColor()
                            .WithTitle(GetText("trivia_game"))
                            .WithDescription(GetText("trivia_win",
                                guildUser.Mention,
                                Format.Bold(CurrentQuestion.Answer)));
                        if (Uri.IsWellFormedUriString(CurrentQuestion.AnswerImageUrl, UriKind.Absolute))
                            embedS.WithImageUrl(CurrentQuestion.AnswerImageUrl);
                        await Channel.EmbedAsync(embedS).ConfigureAwait(false);
                    }
                    catch
                    {
                        // ignored
                    }

                    var reward = config.Trivia.CurrencyReward;
                    if (reward > 0)
                        await cs.AddAsync(guildUser, "Won trivia", reward, true).ConfigureAwait(false);
                    return;
                }

                var embed = new EmbedBuilder().WithOkColor()
                    .WithTitle(GetText("trivia_game"))
                    .WithDescription(
                        GetText("trivia_guess", guildUser.Mention, Format.Bold(CurrentQuestion.Answer)));
                if (Uri.IsWellFormedUriString(CurrentQuestion.AnswerImageUrl, UriKind.Absolute))
                    embed.WithImageUrl(CurrentQuestion.AnswerImageUrl);
                await Channel.EmbedAsync(embed).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Warning(ex.ToString());
            }
        });
        return Task.CompletedTask;
    }

    public string? GetLeaderboard()
    {
        if (Users.Count == 0)
            return GetText("no_results");

        var sb = new StringBuilder();

        foreach (var kvp in Users.OrderByDescending(kvp => kvp.Value))
            sb.AppendLine(GetText("trivia_points", Format.Bold(kvp.Key.ToString()), kvp.Value).SnPl(kvp.Value));

        return sb.ToString();
    }
}