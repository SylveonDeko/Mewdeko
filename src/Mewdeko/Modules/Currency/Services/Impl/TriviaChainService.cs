using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using Mewdeko.Database.Enums;
using Mewdeko.Modules.Currency.Models;
using Mewdeko.Services.Strings;

namespace Mewdeko.Modules.Currency.Services.Impl;

/// <summary>
///     Service implementation for managing trivia chain games.
/// </summary>
/// <param name="strings">The localized strings service.</param>
public class TriviaChainService(GeneratedBotStrings strings) : ITriviaChainService
{
    private static readonly Dictionary<ulong, TriviaChainState> TriviaChainStates = new();

    private static readonly Timer StateCleanupTimer =
        new(CleanupExpiredStates, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));


    /// <summary>
    ///     Gets the trivia chain state for a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <returns>The trivia chain state if it exists, otherwise null.</returns>
    public TriviaChainState? GetTriviaChainState(ulong userId)
    {
        TriviaChainStates.TryGetValue(userId, out var state);
        return state;
    }

    /// <summary>
    ///     Creates a new trivia chain game for a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="betAmount">The bet amount.</param>
    /// <param name="category">The trivia category.</param>
    /// <returns>The initial trivia chain state with the first question.</returns>
    public async Task<TriviaChainState> StartTriviaChainAsync(ulong userId, ulong guildId, long betAmount,
        string category)
    {
        // Generate the first question
        var question = await GenerateTriviaQuestion(category);
        var currentMultiplier = 1.5; // Starting multiplier for first question

        // Create initial trivia chain state
        var chainState = new TriviaChainState(
            userId,
            guildId,
            betAmount,
            category,
            0, // Chain length starts at 0
            currentMultiplier,
            0, // No winnings yet
            question.question,
            question.options,
            question.correctAnswer,
            DateTime.UtcNow
        );

        TriviaChainStates[userId] = chainState;
        return chainState;
    }

    /// <summary>
    ///     Processes a trivia answer and updates the game state.
    /// </summary>
    /// <param name="ctx">The interaction context.</param>
    /// <param name="answerIndex">The selected answer index.</param>
    /// <param name="chainState">The current trivia chain state.</param>
    /// <param name="currencyService">The currency service.</param>
    /// <returns>The result of processing the answer.</returns>
    public async Task<TriviaAnswerResult> ProcessTriviaAnswerAsync(IInteractionContext ctx, string answerIndex,
        TriviaChainState chainState, ICurrencyService currencyService)
    {
        var guild = ctx.Guild;
        var user = ctx.User;

        if (!int.TryParse(answerIndex, out var selectedIndex) || selectedIndex < 0 ||
            selectedIndex >= chainState.CurrentOptions.Length)
        {
            return new TriviaAnswerResult(
                false, false, true, null,
                strings.TriviaChainInvalidAnswer(guild.Id),
                null, null);
        }

        var selectedAnswer = chainState.CurrentOptions[selectedIndex];
        var isCorrect = selectedAnswer.Equals(chainState.CorrectAnswer, StringComparison.OrdinalIgnoreCase);

        if (isCorrect)
        {
            var newChainLength = chainState.ChainLength + 1;
            var newMultiplier = 1.0 + 0.5 * newChainLength;
            var potentialWin = (long)(chainState.BetAmount * newMultiplier);

            if (newChainLength == 5)
            {
                // Game completed successfully
                await currencyService.CreditAsync(user.Id, potentialWin,
                    strings.TriviaChainTransactionCompleted(guild.Id), CurrencyCategory.GamePayout, guild.Id,
                    "triviachain");

                var completeEmbed = new EmbedBuilder()
                    .WithTitle(strings.TriviaChainTitle(guild.Id))
                    .WithDescription(strings.TriviaChainCompleted(guild.Id, potentialWin,
                        await currencyService.GetCurrencyEmote(guild.Id)))
                    .WithColor(Color.Gold);

                TriviaChainStates.Remove(user.Id);

                return new TriviaAnswerResult(
                    true, true, false, null,
                    strings.TriviaChainCompleted(guild.Id, potentialWin,
                        await currencyService.GetCurrencyEmote(guild.Id)),
                    completeEmbed.Build(), null);
            }

            // Continue to next question
            var updatedState = chainState with
            {
                ChainLength = newChainLength, CurrentMultiplier = newMultiplier, TotalWinnings = potentialWin
            };

            var nextQuestionResult = await PresentNextTriviaQuestion(ctx, updatedState, currencyService);
            TriviaChainStates[user.Id] = nextQuestionResult.UpdatedState!;

            return nextQuestionResult;
        }


        var failEmbed = new EmbedBuilder()
            .WithTitle(strings.TriviaChainTitle(guild.Id))
            .WithDescription(strings.TriviaChainFailed(guild.Id, chainState.CorrectAnswer, chainState.BetAmount,
                await currencyService.GetCurrencyEmote(guild.Id)))
            .WithColor(Color.Red);

        TriviaChainStates.Remove(user.Id);

        return new TriviaAnswerResult(
            false, false, true, null,
            strings.TriviaChainWrongAnswer(guild.Id, chainState.BetAmount),
            failEmbed.Build(), null);
    }

    /// <summary>
    ///     Removes the trivia chain state for a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    public void RemoveTriviaChainState(ulong userId)
    {
        TriviaChainStates.Remove(userId);
    }

    private async Task<TriviaAnswerResult> PresentNextTriviaQuestion(IInteractionContext ctx,
        TriviaChainState chainState, ICurrencyService currencyService)
    {
        var question = await GenerateTriviaQuestion(chainState.Category);

        var updatedState = chainState with
        {
            CurrentQuestion = question.question,
            CurrentOptions = question.options,
            CorrectAnswer = question.correctAnswer
        };

        var newMultiplier = 1.0 + 0.5 * (chainState.ChainLength + 1);
        var potentialWin = (long)(chainState.BetAmount * newMultiplier);

        var eb = new EmbedBuilder()
            .WithTitle(strings.TriviaChainTitle(ctx.Guild.Id))
            .WithColor(Color.Blue)
            .WithDescription(question.question)
            .AddField(strings.TriviaChainQuestion(ctx.Guild.Id, chainState.ChainLength + 1, question.question), "_ _",
                true)
            .AddField(strings.TriviaChainMultiplier(ctx.Guild.Id, $"{newMultiplier:F1}x"), "_ _", true)
            .AddField(
                strings.TriviaChainPotentialWin(ctx.Guild.Id, potentialWin,
                    await currencyService.GetCurrencyEmote(ctx.Guild.Id)), "_ _", true)
            .AddField(
                strings.TriviaChainCurrentWinnings(ctx.Guild.Id, chainState.TotalWinnings,
                    await currencyService.GetCurrencyEmote(ctx.Guild.Id)), "_ _", true);

        var selectMenuBuilder = new SelectMenuBuilder()
            .WithPlaceholder(strings.TriviaChainChooseAnswer(ctx.Guild.Id))
            .WithCustomId($"triviachain_answer_{ctx.User.Id}_{chainState.ChainLength}");

        for (var i = 0; i < question.options.Length; i++)
        {
            selectMenuBuilder.AddOption(question.options[i], i.ToString(),
                strings.TriviaChainOption(ctx.Guild.Id, i + 1));
        }

        var componentBuilder = new ComponentBuilder()
            .WithSelectMenu(selectMenuBuilder);

        if (chainState.ChainLength > 0)
        {
            componentBuilder.WithButton(strings.TriviaChainCashOut(ctx.Guild.Id),
                $"triviachain_cashout_{ctx.User.Id}_{chainState.TotalWinnings}", ButtonStyle.Success);
        }

        return new TriviaAnswerResult(
            true, false, false, updatedState,
            strings.TriviaChainNextQuestion(ctx.Guild.Id),
            eb.Build(), componentBuilder.Build());
    }

    private static void CleanupExpiredStates(object? state)
    {
        var expiredStates = TriviaChainStates
            .Where(kvp => DateTime.UtcNow - kvp.Value.CreatedAt > TimeSpan.FromMinutes(10)).ToList();
        foreach (var expiredState in expiredStates)
        {
            TriviaChainStates.Remove(expiredState.Key);
        }
    }

    private async Task<(string question, string[] options, string correctAnswer, string difficulty)>
        GenerateTriviaQuestion(string category)
    {
        try
        {
            using var httpClient = new HttpClient();

            // Map category names to OpenTDB category IDs
            var categoryId = category switch
            {
                "general" => "9", // General Knowledge
                "science" => "17", // Science & Nature
                "history" => "23", // History
                "sports" => "21", // Sports
                "entertainment" => "11", // Entertainment: Film
                _ => "9" // Default to General Knowledge
            };

            var rand = new Random();
            var difficulty = new[]
            {
                "easy", "medium", "hard"
            }[rand.Next(3)];
            var url =
                $"https://opentdb.com/api.php?amount=1&category={categoryId}&difficulty={difficulty}&type=multiple";

            var response = await httpClient.GetStringAsync(url);
            var triviaResponse = JsonSerializer.Deserialize<OpenTDBResponse>(response);

            if (triviaResponse is { response_code: 0, results.Length: > 0 })
            {
                var question = triviaResponse.results[0];

                // Decode HTML entities (default encoding from OpenTDB)
                var decodedQuestion = WebUtility.HtmlDecode(question.question);
                var decodedCorrectAnswer = WebUtility.HtmlDecode(question.correct_answer);
                var decodedIncorrectAnswers = question.incorrect_answers.Select(WebUtility.HtmlDecode).ToArray();

                var allOptions = new List<string>
                {
                    decodedCorrectAnswer
                };
                allOptions.AddRange(decodedIncorrectAnswers);
                var shuffledOptions = allOptions.OrderBy(x => rand.Next()).ToArray();

                return (decodedQuestion, shuffledOptions, decodedCorrectAnswer, question.difficulty);
            }
        }
        catch (Exception ex)
        {
            // Log the error but continue with fallback
            Console.WriteLine($"Open Trivia DB API error: {ex.Message}");
        }

        // Fallback to original method
        return GenerateFallbackTriviaQuestion(category);
    }

    private (string question, string[] options, string correctAnswer, string difficulty) GenerateFallbackTriviaQuestion(
        string category)
    {
        var rand = new Random();

        var questions = category switch
        {
            "science" => new[]
            {
                ("What is the chemical symbol for gold?", [
                    "Au", "Ag", "Fe", "Cu"
                ], "Au"),
                ("How many bones are in the human body?", [
                    "206", "204", "208", "210"
                ], "206"),
                ("What is the speed of light?", new[]
                {
                    "299,792,458 m/s", "300,000,000 m/s", "299,000,000 m/s", "298,000,000 m/s"
                }, "299,792,458 m/s")
            },
            "history" => new[]
            {
                ("In which year did World War II end?", [
                    "1945", "1944", "1946", "1943"
                ], "1945"),
                ("Who was the first president of the United States?", [
                    "George Washington", "John Adams", "Thomas Jefferson", "Benjamin Franklin"
                ], "George Washington"),
                ("Which empire was ruled by Julius Caesar?", new[]
                {
                    "Roman Empire", "Greek Empire", "Byzantine Empire", "Ottoman Empire"
                }, "Roman Empire")
            },
            "sports" => new[]
            {
                ("How many players are on a basketball team on the court?", [
                    "5", "6", "7", "4"
                ], "5"),
                ("In which sport would you perform a slam dunk?", [
                    "Basketball", "Tennis", "Football", "Baseball"
                ], "Basketball"),
                ("How often are the Summer Olympics held?", new[]
                {
                    "Every 4 years", "Every 2 years", "Every 6 years", "Every 3 years"
                }, "Every 4 years")
            },
            "entertainment" => new[]
            {
                ("Who directed the movie 'Jaws'?", [
                    "Steven Spielberg", "George Lucas", "Martin Scorsese", "Francis Ford Coppola"
                ], "Steven Spielberg"),
                ("Which movie features the song 'My Heart Will Go On'?", [
                    "Titanic", "The Bodyguard", "Ghost", "Pretty Woman"
                ], "Titanic"),
                ("What is the highest-grossing film of all time?", new[]
                {
                    "Avatar", "Avengers: Endgame", "Titanic", "Star Wars"
                }, "Avatar")
            },
            _ => new[]
            {
                ("What is the capital of France?", [
                    "Paris", "London", "Berlin", "Madrid"
                ], "Paris"),
                ("Which planet is known as the Red Planet?", [
                    "Mars", "Venus", "Jupiter", "Saturn"
                ], "Mars"),
                ("What is 2 + 2?", new[]
                {
                    "4", "3", "5", "6"
                }, "4")
            }
        };

        var selectedQuestion = questions[rand.Next(questions.Length)];
        return (selectedQuestion.Item1, selectedQuestion.Item2, selectedQuestion.Item3, "medium");
    }
}