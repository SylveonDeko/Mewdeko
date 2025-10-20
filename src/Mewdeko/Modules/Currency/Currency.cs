using System.IO;
using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.Currency.Services;
using SkiaSharp;

namespace Mewdeko.Modules.Currency;

/// <summary>
///     Module for managing currency.
/// </summary>
/// <param name="interactive">The interactive service for handling user interactions.</param>
/// <param name="blackjackService">The blackjack game service.</param>
/// <param name="dailyChallengeService">The daily challenge service.</param>
public partial class Currency(
    InteractiveService interactive,
    BlackjackService blackjackService,
    DailyChallengeService dailyChallengeService)
    : MewdekoModuleBase<ICurrencyService>
{
    /// <summary>
    ///     Checks the current balance of the user.
    /// </summary>
    /// <example>.$</example>
    [Cmd]
    [Aliases]
    public async Task Cash()
    {
        var eb = new EmbedBuilder()
            .WithOkColor()
            .WithDescription(Strings.CashBalance(ctx.Guild.Id,
                await Service.GetUserBalanceAsync(ctx.User.Id, ctx.Guild.Id),
                await Service.GetCurrencyEmote(ctx.Guild.Id)));

        await ReplyAsync(embed: eb.Build());
    }

    /// <summary>
    ///     Allows the user to flip a coin with a specified bet amount and guess.
    /// </summary>
    /// <param name="betAmount">The amount to bet.</param>
    /// <param name="guess">The user's guess ("heads" or "tails").</param>
    /// <example>.coinflip 100 heads</example>
    [Cmd]
    [Aliases]
    public async Task CoinFlip(long betAmount, string guess)
    {
        var currentBalance = await Service.GetUserBalanceAsync(ctx.User.Id, ctx.Guild.Id);
        if (betAmount > currentBalance || betAmount <= 0)
        {
            await ReplyAsync(Strings.CoinflipInvalidBet(ctx.Guild.Id));
            return;
        }

        var coinFlip = new Random().Next(2) == 0 ? "heads" : "tails";
        if (coinFlip.Equals(guess, StringComparison.OrdinalIgnoreCase))
        {
            await Service.AddUserBalanceAsync(ctx.User.Id, betAmount, ctx.Guild.Id);
            await Service.AddTransactionAsync(ctx.User.Id, betAmount, Strings.CoinflipWonTransaction(ctx.Guild.Id),
                ctx.Guild.Id);
            await ReplyAsync(Strings.CoinflipWon(ctx.Guild.Id, coinFlip, betAmount,
                await Service.GetCurrencyEmote(ctx.Guild.Id)));
        }
        else
        {
            await Service.AddUserBalanceAsync(ctx.User.Id, -betAmount, ctx.Guild.Id);
            await Service.AddTransactionAsync(ctx.User.Id, -betAmount, Strings.CoinflipLostTransaction(ctx.Guild.Id),
                ctx.Guild.Id);
            await ReplyAsync(
                Strings.CoinflipLost(ctx.Guild.Id, coinFlip, betAmount, await Service.GetCurrencyEmote(ctx.Guild.Id)));
        }
    }

    /// <summary>
    ///     Adds money to a users balance
    /// </summary>
    /// <param name="user">The user to add it to.</param>
    /// <param name="amount">The amount to add, dont go too crazy lol. Can be negative</param>
    /// <param name="reason">The reason you are doing this to this, person, thing, whatever</param>
    [Cmd]
    [Aliases]
    [CurrencyPermissions]
    public async Task ModifyBalance(IUser user, long amount, [Remainder] string? reason = null)
    {
        await Service.AddUserBalanceAsync(user.Id, amount, ctx.Guild.Id);
        await Service.AddTransactionAsync(user.Id, amount, reason ??= "", ctx.Guild.Id);
        await ReplyConfirmAsync(Strings.UserBalanceModified(ctx.Guild.Id, user.Mention, amount, reason));
    }

    /// <summary>
    ///     Allows the user to claim their daily reward.
    /// </summary>
    /// <example>.dailyreward</example>
    [Cmd]
    [Aliases]
    public async Task DailyReward()
    {
        var (rewardAmount, cooldownSeconds) = await Service.GetReward(ctx.Guild.Id);
        if (rewardAmount == 0)
        {
            await ctx.Channel.SendErrorAsync(Strings.DailyRewardNotSet(ctx.Guild.Id), Config);
            return;
        }

        var minimumTimeBetweenClaims = TimeSpan.FromSeconds(cooldownSeconds);

        var recentTransactions = (await Service.GetTransactionsAsync(ctx.User.Id, ctx.Guild.Id) ??
                                  [])
            .Where(t => t.Description == Strings.DailyRewardTransaction(ctx.Guild.Id) &&
                        t.DateAdded > DateTime.UtcNow - minimumTimeBetweenClaims);

        if (recentTransactions.Any())
        {
            var nextAllowedClaimTime = recentTransactions.Max(t => t.DateAdded) + minimumTimeBetweenClaims;

            await ctx.Channel.SendErrorAsync(
                Strings.DailyRewardAlreadyClaimed(ctx.Guild.Id, TimestampTag.FromDateTime(nextAllowedClaimTime.Value)),
                Config);
            return;
        }

        await Service.AddUserBalanceAsync(ctx.User.Id, rewardAmount, ctx.Guild.Id);
        await Service.AddTransactionAsync(ctx.User.Id, rewardAmount, Strings.DailyRewardTransaction(ctx.Guild.Id),
            ctx.Guild.Id);
        await ctx.Channel.SendConfirmAsync(
            Strings.DailyRewardClaimed(ctx.Guild.Id, rewardAmount, await Service.GetCurrencyEmote(ctx.Guild.Id)));
    }

    /// <summary>
    ///     Allows the user to guess whether the next number is higher or lower than the current number.
    /// </summary>
    /// <param name="guess">The user's guess ("higher" or "lower").</param>
    /// <example>.highlow higher</example>
    [Cmd]
    [Aliases]
    public async Task HighLow(string guess)
    {
        var currentNumber = new Random().Next(1, 11);
        var nextNumber = new Random().Next(1, 11);

        if (guess.Equals("higher", StringComparison.OrdinalIgnoreCase) && nextNumber > currentNumber
            || guess.Equals("lower", StringComparison.OrdinalIgnoreCase) && nextNumber < currentNumber)
        {
            await Service.AddUserBalanceAsync(ctx.User.Id, 100, ctx.Guild.Id);
            await ReplyAsync(Strings.HighlowWon(ctx.Guild.Id, currentNumber, nextNumber,
                await Service.GetCurrencyEmote(ctx.Guild.Id)));
        }
        else
        {
            await Service.AddUserBalanceAsync(ctx.User.Id, -100, ctx.Guild.Id);
            await ReplyAsync(Strings.HighlowLost(ctx.Guild.Id, currentNumber, nextNumber,
                await Service.GetCurrencyEmote(ctx.Guild.Id)));
        }
    }

    /// <summary>
    ///     Displays the leaderboard of users with the highest balances.
    /// </summary>
    /// <example>.leaderboard</example>
    [Cmd]
    [Aliases]
    public async Task Leaderboard()
    {
        var users = (await Service.GetAllUserBalancesAsync(ctx.Guild.Id))
            .OrderByDescending(u => u.Balance)
            .ToList();

        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex((users.Count - 1) / 10)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await interactive.SendPaginatorAsync(paginator, ctx.Channel, TimeSpan.FromMinutes(60))
            .ConfigureAwait(false);

        async Task<PageBuilder> PageFactory(int index)
        {
            var pageBuilder = new PageBuilder()
                .WithTitle(Strings.LeaderboardTitle(ctx.Guild.Id))
                .WithDescription(Strings.LeaderboardDescription(ctx.Guild.Id, users.Count, ctx.Guild.Name))
                .WithColor(Color.Blue);

            for (var i = index * 10; i < (index + 1) * 10 && i < users.Count; i++)
            {
                var user = await ctx.Guild.GetUserAsync(users[i].UserId) ??
                           (IUser)await ctx.Client.GetUserAsync(users[i].UserId);
                pageBuilder.AddField(Strings.LeaderboardUserEntry(ctx.Guild.Id, i + 1, user.Username),
                    Strings.LeaderboardBalanceEntry(ctx.Guild.Id, users[i].Balance,
                        await Service.GetCurrencyEmote(ctx.Guild.Id)), true);
            }

            return pageBuilder;
        }
    }

    /// <summary>
    ///     Sets the daily reward amount and cooldown time for the guild.
    /// </summary>
    /// <param name="amount">The amount of the daily reward.</param>
    /// <param name="time">The cooldown time for claiming the daily reward.</param>
    /// <example>.setdaily 100 1d</example>
    [Cmd]
    [Aliases]
    [UserPerm(GuildPermission.Administrator)]
    public async Task SetDaily(int amount, StoopidTime time)
    {
        await Service.SetReward(amount, (int)time.Time.TotalSeconds, ctx.Guild.Id);
        await ctx.Channel.SendConfirmAsync(Strings.SetdailySuccess(ctx.Guild.Id, amount,
            await Service.GetCurrencyEmote(ctx.Guild.Id), time.Time.ToReadableDuration()));
    }

    /// <summary>
    ///     Allows the user to spin the wheel for a chance to win or lose credits.
    /// </summary>
    /// <param name="betAmount">The amount of credits the user wants to bet.</param>
    /// <example>.spinwheel 100</example>
    [Cmd]
    [Aliases]
    public async Task SpinWheel(long betAmount = 0)
    {
        var balance = await Service.GetUserBalanceAsync(ctx.User.Id, ctx.Guild.Id);
        if (balance <= 0)
        {
            await ctx.Channel.SendErrorAsync(
                Strings.SpinwheelNoBalance(ctx.Guild.Id, await Service.GetCurrencyEmote(ctx.Guild.Id)), Config);
            return;
        }

        if (betAmount > balance)
        {
            await ctx.Channel.SendErrorAsync(
                Strings.SpinwheelInsufficientBalance(ctx.Guild.Id, await Service.GetCurrencyEmote(ctx.Guild.Id)),
                Config);
            return;
        }

        string[] segments =
        [
            "-$10", "-10%", "+$10", "+30%", "+$30", "-5%"
        ];
        int[] weights =
        [
            2, 2, 1, 1, 1, 2
        ];
        var rand = new Random();
        var winningSegment = GenerateWeightedRandomSegment(segments.Length, weights, rand);

        // Prepare the wheel image
        using var bitmap = new SKBitmap(500, 500);
        using var canvas = new SKCanvas(bitmap);
        DrawWheel(canvas, segments.Length, segments, winningSegment + 2); // Adjust the index as needed

        using var stream = new MemoryStream();
        bitmap.Encode(stream, SKEncodedImageFormat.Png, 100);
        stream.Seek(0, SeekOrigin.Begin);

        var balanceChange = await ComputeBalanceChange(segments[winningSegment], betAmount);
        if (segments[winningSegment].StartsWith("+"))
        {
            balanceChange += betAmount;
        }
        else if (segments[winningSegment].StartsWith("-"))
        {
            balanceChange = betAmount - Math.Abs(balanceChange);
        }

        // Update user balance
        await Service.AddUserBalanceAsync(ctx.User.Id, balanceChange, ctx.Guild.Id);
        await Service.AddTransactionAsync(ctx.User.Id, balanceChange,
            segments[winningSegment].Contains('-')
                ? Strings.SpinwheelLossTransaction(ctx.Guild.Id)
                : Strings.SpinwheelWinTransaction(ctx.Guild.Id), ctx.Guild.Id);

        var eb = new EmbedBuilder()
            .WithTitle(balanceChange > 0
                ? Strings.SpinwheelWinTitle(ctx.Guild.Id)
                : Strings.SpinwheelLossTitle(ctx.Guild.Id))
            .WithDescription(Strings.SpinwheelResult(ctx.Guild.Id, segments[winningSegment], balanceChange,
                await Service.GetCurrencyEmote(ctx.Guild.Id)))
            .WithColor(balanceChange > 0 ? Color.Green : Color.Red)
            .WithImageUrl("attachment://wheelResult.png");

        // Send the image and embed as a message to the channel
        await ctx.Channel.SendFileAsync(stream, "wheelResult.png", embed: eb.Build());

        // Helper method to generate weighted random segment
        int GenerateWeightedRandomSegment(int segmentCount, int[] segmentWeights, Random random)
        {
            var totalWeight = segmentWeights.Sum();
            var randomNumber = random.Next(totalWeight);

            var accumulatedWeight = 0;
            for (var i = 0; i < segmentCount; i++)
            {
                accumulatedWeight += segmentWeights[i];
                if (randomNumber < accumulatedWeight)
                    return i;
            }

            return segmentCount - 1; // Return the last segment as a fallback
        }

        // Helper method to compute balance change
        Task<long> ComputeBalanceChange(string segment, long amount)
        {
            long change;

            if (segment.EndsWith("%"))
            {
                var percent = int.Parse(segment[1..^1]);
                var portion = (long)Math.Ceiling(amount * (percent / 100.0));
                change = segment.StartsWith("-") ? -portion : portion;
            }
            else
            {
                var val = int.Parse(segment.Replace("$", "").Replace("+", "").Replace("-", ""));
                change = segment.StartsWith("-") ? -val : val;
            }

            return Task.FromResult(change);
        }
    }

    /// <summary>
    ///     Starts a new game of Blackjack or joins an existing one.
    /// </summary>
    /// <param name="amount">The bet amount for the player.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Cmd]
    [Aliases]
    public async Task Blackjack(long amount)
    {
        var currentBalance = await Service.GetUserBalanceAsync(ctx.User.Id, ctx.Guild.Id);
        if (amount > currentBalance || amount <= 0)
        {
            await ReplyAsync(Strings.BlackjackInvalidBet(ctx.Guild.Id));
            return;
        }

        try
        {
            var game = BlackjackService.StartOrJoinGame(ctx.User, amount);
            var embed = game.CreateGameEmbed(Strings.BlackjackJoined(ctx.Guild.Id, ctx.User.Username), ctx.Guild.Id,
                Strings);
            await ReplyAsync(embeds: embed);
        }
        catch (InvalidOperationException ex)
        {
            switch (ex.Message)
            {
                case "blackjack_game_full":
                    await ReplyErrorAsync(Strings.BlackjackGameFull(ctx.Guild.Id));
                    break;
                case "blackjack_already_in_game":
                    await ReplyErrorAsync(Strings.AlreadyInGame(ctx.Guild.Id));
                    break;
                case "no_ongoing_game":
                    await ReplyErrorAsync(Strings.NoOngoingGame(ctx.Guild.Id));
                    break;
            }
        }
    }

    /// <summary>
    ///     Hits and draws a new card for the player.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Cmd]
    [Aliases]
    public async Task Hit()
    {
        try
        {
            var game = BlackjackService.GetGame(ctx.User);
            game.HitPlayer(ctx.User);
            var embed = game.CreateGameEmbed(Strings.BlackjackHit(ctx.Guild.Id, ctx.User.Username), ctx.Guild.Id,
                Strings);

            if (BlackjackService.BlackjackGame.CalculateHandTotal(game.PlayerHands[ctx.User]) > 21)
            {
                await EndGame(game, false, Strings.BlackjackBust(ctx.Guild.Id, ctx.User.Username));
            }
            else if (BlackjackService.BlackjackGame.CalculateHandTotal(game.PlayerHands[ctx.User]) == 21)
            {
                await Stand();
            }
            else
            {
                await ReplyAsync(embeds: embed);
            }
        }
        catch (InvalidOperationException ex)
        {
            switch (ex.Message)
            {
                case "no_ongoing_game":
                    await ReplyErrorAsync(Strings.NoOngoingGame(ctx.Guild.Id));
                    break;
                default:
                    await ReplyAsync(Strings.BlackjackError(ctx.Guild.Id, ex.Message));
                    break;
            }
        }
    }

    /// <summary>
    ///     Stands and ends the player's turn, handling the dealer's turn.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Cmd]
    [Aliases]
    public async Task Stand()
    {
        try
        {
            var embed = await blackjackService.HandleStandAsync(ctx.User, ctx.Guild.Id,
                (userId, balanceChange) => Service.AddUserBalanceAsync(userId, balanceChange, ctx.Guild.Id),
                (userId, balanceChange, description) =>
                    Service.AddTransactionAsync(userId, balanceChange, description, ctx.Guild.Id),
                await Service.GetCurrencyEmote(ctx.Guild.Id));
            await ReplyAsync(embeds: embed);
        }
        catch (InvalidOperationException ex)
        {
            switch (ex.Message)
            {
                case "no_ongoing_game":
                    await ReplyErrorAsync(Strings.NoOngoingGame(ctx.Guild.Id));
                    break;
                default:
                    await ReplyAsync(Strings.BlackjackError(ctx.Guild.Id, ex.Message));
                    break;
            }
        }
    }

    /// <summary>
    ///     Ends the game and updates the player's balance and transactions.
    /// </summary>
    /// <param name="game">The current game instance.</param>
    /// <param name="playerWon">Indicates whether the player won or lost.</param>
    /// <param name="message">The message to display in the embed.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task EndGame(BlackjackService.BlackjackGame game, bool playerWon, string message)
    {
        var balanceChange = playerWon ? game.Bets[ctx.User] : -game.Bets[ctx.User];

        await Service.AddUserBalanceAsync(ctx.User.Id, balanceChange, ctx.Guild.Id);
        await Service.AddTransactionAsync(ctx.User.Id, balanceChange,
            playerWon ? Strings.BlackjackWonTransaction(ctx.Guild.Id) : Strings.BlackjackLostTransaction(ctx.Guild.Id),
            ctx.Guild.Id);

        BlackjackService.EndGame(ctx.User);

        var embed = game.CreateGameEmbed(message, ctx.Guild.Id, Strings);
        await ReplyAsync(embeds: embed);
    }

    /// <summary>
    ///     Plays a slot machine game with a specified bet amount.
    /// </summary>
    /// <param name="bet">The amount to bet on the slot machine.</param>
    /// <example>.slot 100</example>
    [Cmd]
    [Aliases]
    public async Task Slot(long bet = 10)
    {
        if (bet < 1)
        {
            await ctx.Channel.SendErrorAsync(
                Strings.SlotMinimumBet(ctx.Guild.Id, await Service.GetCurrencyEmote(ctx.Guild.Id)),
                Config);
            return;
        }

        var userBalance = await Service.GetUserBalanceAsync(ctx.User.Id, ctx.Guild.Id);
        if (bet > userBalance)
        {
            await ctx.Channel.SendErrorAsync(
                Strings.SlotInsufficientFunds(ctx.Guild.Id, await Service.GetCurrencyEmote(ctx.Guild.Id)), Config);
            return;
        }

        string[] symbols = ["🍒", "🍊", "🍋", "🍇", "💎", "7️⃣"];
        var result = new string[3];
        var rng = new Random();

        for (var i = 0; i < 3; i++)
        {
            result[i] = symbols[rng.Next(symbols.Length)];
        }

        var winnings = CalculateWinnings(result, bet);

        var balanceChange = winnings - bet;
        await Service.AddUserBalanceAsync(ctx.User.Id, balanceChange, ctx.Guild.Id);
        await Service.AddTransactionAsync(ctx.User.Id, balanceChange, Strings.SlotTransaction(ctx.Guild.Id),
            ctx.Guild.Id);

        var eb = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.SlotTitle(ctx.Guild.Id))
            .WithDescription(Strings.SlotResult(ctx.Guild.Id, result[0], result[1], result[2]))
            .AddField(Strings.SlotBet(ctx.Guild.Id), $"{bet} {await Service.GetCurrencyEmote(ctx.Guild.Id)}", true)
            .AddField(Strings.SlotWinnings(ctx.Guild.Id), $"{winnings} {await Service.GetCurrencyEmote(ctx.Guild.Id)}",
                true)
            .AddField(Strings.SlotNetProfit(ctx.Guild.Id),
                $"{balanceChange} {await Service.GetCurrencyEmote(ctx.Guild.Id)}",
                true);

        await ctx.Channel.SendMessageAsync(embed: eb.Build());
    }

    private static long CalculateWinnings(string[] result, long bet)
    {
        if (result[0] == result[1] && result[1] == result[2])
        {
            // All three symbols match
            return result[0] switch
            {
                "💎" => bet * 10,
                "7️⃣" => bet * 7,
                "🍇" => bet * 5,
                _ => bet * 3
            };
        }

        if (result[0] == result[1] || result[1] == result[2] || result[0] == result[2])
        {
            // Two symbols match
            return bet * 2;
        }

        // No matches
        return 0;
    }

    /// <summary>
    ///     Play a game of roulette with a specified bet amount and type.
    /// </summary>
    /// <param name="betAmount">The amount to bet.</param>
    /// <param name="betType">The type of bet (e.g., "red", "black", "even", "odd", or a number from 0-36).</param>
    /// <example>.roulette 100 red</example>
    [Cmd]
    [Aliases]
    public async Task Roulette(long betAmount, string betType)
    {
        var balance = await Service.GetUserBalanceAsync(ctx.User.Id, ctx.Guild.Id);
        if (betAmount > balance || betAmount <= 0)
        {
            await ctx.Channel.SendErrorAsync(Strings.RouletteInvalidBet(ctx.Guild.Id), Config);
            return;
        }

        var rng = new Random();
        var result = rng.Next(0, 37);
        var color = result == 0 ? "green" : result % 2 == 0 ? "black" : "red";

        var won = false;
        var multiplier = 0;

        if (int.TryParse(betType, out var numberBet))
        {
            won = result == numberBet;
            multiplier = 35;
        }
        else
        {
            switch (betType.ToLower())
            {
                case "red":
                case "black":
                    won = betType.Equals(color, StringComparison.OrdinalIgnoreCase);
                    break;
                case "even":
                    won = result != 0 && result % 2 == 0;
                    break;
                case "odd":
                    won = result % 2 != 0;
                    break;
                default:
                    await ctx.Channel.SendErrorAsync(Strings.RouletteInvalidBetType(ctx.Guild.Id), Config);
                    return;
            }

            multiplier = 1;
        }

        var winnings = won ? betAmount * (multiplier + 1) : 0;
        var profit = winnings - betAmount;

        await Service.AddUserBalanceAsync(ctx.User.Id, profit, ctx.Guild.Id);
        await Service.AddTransactionAsync(ctx.User.Id, profit, Strings.RouletteTransaction(ctx.Guild.Id), ctx.Guild.Id);

        var eb = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.RouletteTitle(ctx.Guild.Id))
            .WithDescription(Strings.RouletteResult(ctx.Guild.Id, result, color))
            .AddField(Strings.RouletteBet(ctx.Guild.Id),
                Strings.RouletteBetDetails(ctx.Guild.Id, betAmount, await Service.GetCurrencyEmote(ctx.Guild.Id),
                    betType), true)
            .AddField(Strings.RouletteOutcome(ctx.Guild.Id),
                won ? Strings.RouletteWon(ctx.Guild.Id) : Strings.RouletteLost(ctx.Guild.Id), true)
            .AddField(Strings.RouletteProfit(ctx.Guild.Id), $"{profit} {await Service.GetCurrencyEmote(ctx.Guild.Id)}",
                true);

        await ctx.Channel.SendMessageAsync(embed: eb.Build());
    }

    // /// <summary>
    // ///     Roll dice and bet on the outcome.
    // /// </summary>
    // /// <param name="betAmount">The amount to bet.</param>
    // /// <param name="guess">The guessed sum of the dice (2-12).</param>
    // /// <example>.roll 100 7</example>
    // [Cmd]
    // [Aliases]
    // public async Task Roll(long betAmount, int guess)
    // {
    //     var balance = await Service.GetUserBalanceAsync(ctx.User.Id, ctx.Guild.Id);
    //     if (betAmount > balance || betAmount <= 0)
    //     {
    //         await ctx.Channel.SendErrorAsync(Strings.RollInvalidBet(ctx.Guild.Id), Config);
    //         return;
    //     }
    //
    //     if (guess is < 2 or > 12)
    //     {
    //         await ctx.Channel.SendErrorAsync(Strings.RollInvalidGuess(ctx.Guild.Id), Config);
    //         return;
    //     }
    //
    //     var rng = new Random();
    //     var dice1 = rng.Next(1, 7);
    //     var dice2 = rng.Next(1, 7);
    //     var sum = dice1 + dice2;
    //
    //     var won = sum == guess;
    //     const int multiplier = 5; // You can adjust this for balance
    //
    //     var winnings = won ? betAmount * multiplier : 0;
    //     var profit = winnings - betAmount;
    //
    //     await Service.AddUserBalanceAsync(ctx.User.Id, profit, ctx.Guild.Id);
    //     await Service.AddTransactionAsync(ctx.User.Id, profit, Strings.RollTransaction(ctx.Guild.Id), ctx.Guild.Id);
    //
    //     var eb = new EmbedBuilder()
    //         .WithOkColor()
    //         .WithTitle(Strings.RollTitle(ctx.Guild.Id))
    //         .WithDescription(Strings.RollResult(ctx.Guild.Id, dice1, dice2, sum))
    //         .AddField(Strings.RollYourGuess(ctx.Guild.Id), guess, true)
    //         .AddField(Strings.RollOutcome(ctx.Guild.Id), won ? Strings.RollWon(ctx.Guild.Id) : Strings.RollLost(ctx.Guild.Id), true)
    //         .AddField(Strings.RollProfit(ctx.Guild.Id), $"{profit} {await Service.GetCurrencyEmote(ctx.Guild.Id)}", true);
    //
    //     await ctx.Channel.SendMessageAsync(embed: eb.Build());
    // }

    /// <summary>
    ///     Retrieves and displays the transactions for a specified user or the current user.
    /// </summary>
    /// <param name="user">The user whose transactions are to be displayed. Defaults to the current user.</param>
    /// <example>.transactions @user</example>
    [Cmd]
    [Aliases]
    public async Task Transactions(IUser? user = null)
    {
        user ??= ctx.User;

        var transactions = await Service.GetTransactionsAsync(user.Id, ctx.Guild.Id);
        transactions = transactions?.OrderByDescending(x => x.DateAdded);
        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex((transactions.Count() - 1) / 10)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await interactive.SendPaginatorAsync(paginator, ctx.Channel, TimeSpan.FromMinutes(60))
            .ConfigureAwait(false);

        async Task<PageBuilder> PageFactory(int index)
        {
            var pageBuilder = new PageBuilder()
                .WithTitle(Strings.TransactionsTitle(ctx.Guild.Id))
                .WithDescription(Strings.TransactionsDescription(ctx.Guild.Id, user.Username))
                .WithColor(Color.Blue);

            for (var i = index * 10; i < (index + 1) * 10 && i < transactions.Count(); i++)
            {
                pageBuilder.AddField(
                    Strings.TransactionsEntry(ctx.Guild.Id, i + 1, transactions.ElementAt(i).Description),
                    Strings.TransactionsDetails(ctx.Guild.Id, transactions.ElementAt(i).Amount,
                        await Service.GetCurrencyEmote(ctx.Guild.Id),
                        TimestampTag.FromDateTime(transactions.ElementAt(i).DateAdded.Value)));
            }

            return pageBuilder;
        }
    }

    /// <summary>
    ///     Play Rock Paper Scissors Lizard Spock against the bot, with or without betting.
    /// </summary>
    /// <param name="betAmount">The amount to bet (optional).</param>
    /// <param name="choice">Your choice (rock, paper, scissors, lizard, or spock).</param>
    /// <example>.rps rock</example>
    /// <example>.rps 100 rock</example>
    [Cmd]
    [Aliases]
    public async Task Rps(string choice, long betAmount = 0)
    {
        if (betAmount != 0)
        {
            var balance = await Service.GetUserBalanceAsync(ctx.User.Id, ctx.Guild.Id);
            if (betAmount > balance || betAmount <= 0)
            {
                await ctx.Channel.SendErrorAsync(Strings.RpsInvalidBet(ctx.Guild.Id), Config);
                return;
            }
        }

        var validChoices = new[]
        {
            "rock", "paper", "scissors", "lizard", "spock"
        };
        if (!validChoices.Contains(choice.ToLower()))
        {
            await ctx.Channel.SendErrorAsync(Strings.RpsInvalidChoice(ctx.Guild.Id), Config);
            return;
        }

        var rng = new Random();
        var botChoice = validChoices[rng.Next(validChoices.Length)];

        var (result, description) = DetermineWinner(choice.ToLower(), botChoice);

        var eb = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(Strings.RpsEmbedTitle(ctx.Guild.Id))
            .WithDescription(Strings.RpsEmbedDescription(ctx.Guild.Id, choice, botChoice))
            .AddField(Strings.RpsEmbedResult(ctx.Guild.Id), result.ToUpperInvariant(), true)
            .AddField(Strings.RpsEmbedExplanation(ctx.Guild.Id), description, true);

        if (betAmount != 0)
        {
            var profit = result switch
            {
                "win" => betAmount,
                "lose" => -betAmount,
                _ => 0L
            };

            await Service.AddUserBalanceAsync(ctx.User.Id, profit, ctx.Guild.Id);
            await Service.AddTransactionAsync(ctx.User.Id, profit, Strings.RpsTransactionDescription(ctx.Guild.Id),
                ctx.Guild.Id);

            eb.AddField(Strings.RpsEmbedProfit(ctx.Guild.Id),
                $"{profit} {await Service.GetCurrencyEmote(ctx.Guild.Id)}", true);
        }

        await ctx.Channel.SendMessageAsync(embed: eb.Build());
    }

    private (string result, string description) DetermineWinner(string playerChoice, string botChoice)
    {
        if (playerChoice == botChoice) return ("tie", Strings.RpsTie(ctx.Guild.Id));
        return (playerChoice, botChoice) switch
        {
            ("scissors", "paper") => ("win", Strings.RpsScissorsPaper(ctx.Guild.Id)),
            ("paper", "rock") => ("win", Strings.RpsPaperRock(ctx.Guild.Id)),
            ("rock", "lizard") => ("win", Strings.RpsRockLizard(ctx.Guild.Id)),
            ("lizard", "spock") => ("win", Strings.RpsLizardSpock(ctx.Guild.Id)),
            ("spock", "scissors") => ("win", Strings.RpsSpockScissors(ctx.Guild.Id)),
            ("scissors", "lizard") => ("win", Strings.RpsScissorsLizard(ctx.Guild.Id)),
            ("lizard", "paper") => ("win", Strings.RpsLizardPaper(ctx.Guild.Id)),
            ("paper", "spock") => ("win", Strings.RpsPaperSpock(ctx.Guild.Id)),
            ("spock", "rock") => ("win", Strings.RpsSpockRock(ctx.Guild.Id)),
            ("rock", "scissors") => ("win", Strings.RpsRockScissors(ctx.Guild.Id)),
            ("paper", "scissors") => ("lose", Strings.RpsScissorsPaper(ctx.Guild.Id)),
            ("rock", "paper") => ("lose", Strings.RpsPaperRock(ctx.Guild.Id)),
            ("lizard", "rock") => ("lose", Strings.RpsRockLizard(ctx.Guild.Id)),
            ("spock", "lizard") => ("lose", Strings.RpsLizardSpock(ctx.Guild.Id)),
            ("scissors", "spock") => ("lose", Strings.RpsSpockScissors(ctx.Guild.Id)),
            ("lizard", "scissors") => ("lose", Strings.RpsScissorsLizard(ctx.Guild.Id)),
            ("paper", "lizard") => ("lose", Strings.RpsLizardPaper(ctx.Guild.Id)),
            ("spock", "paper") => ("lose", Strings.RpsPaperSpock(ctx.Guild.Id)),
            ("rock", "spock") => ("lose", Strings.RpsSpockRock(ctx.Guild.Id)),
            ("scissors", "rock") => ("lose", Strings.RpsRockScissors(ctx.Guild.Id)),
            _ => throw new ArgumentException("Invalid choice combination")
        };
    }

    /// <summary>
    ///     Draws a wheel with the specified number of segments and their corresponding labels, highlighting the winning
    ///     segment.
    /// </summary>
    /// <param name="canvas">The canvas on which to draw the wheel.</param>
    /// <param name="numSegments">The number of segments in the wheel.</param>
    /// <param name="segments">An array containing the labels for each segment.</param>
    /// <param name="winningSegment">The index of the winning segment (0-based).</param>
    private static void DrawWheel(SKCanvas canvas, int numSegments, string[] segments, int winningSegment)
    {
        var pastelColor = GeneratePastelColor();
        var colors = new[]
        {
            SKColors.White, pastelColor
        };

        var centerX = canvas.LocalClipBounds.MidX;
        var centerY = canvas.LocalClipBounds.MidY;
        var radius = Math.Min(centerX, centerY) - 10;

        var offsetAngle = 360f / numSegments * winningSegment;

        for (var i = 0; i < numSegments; i++)
        {
            using var paint = new SKPaint();
            paint.Style = SKPaintStyle.Fill;
            paint.Color = colors[i % colors.Length];
            paint.IsAntialias = true;

            var startAngle = i * 360 / numSegments - offsetAngle;
            var sweepAngle = 360f / numSegments;

            canvas.DrawArc(new SKRect(centerX - radius, centerY - radius, centerX + radius, centerY + radius),
                startAngle, sweepAngle, true, paint);
        }

        // Create a font for the text
        using var textFont = new SKFont();
        textFont.Size = 20;

        using var textPaint = new SKPaint();
        textPaint.Color = SKColors.Black;
        textPaint.IsAntialias = true;

        for (var i = 0; i < numSegments; i++)
        {
            var startAngle = i * 360 / numSegments - offsetAngle;
            var middleAngle = startAngle + 360 / numSegments / 2;

            // Calculate position for center-aligned text
            var textX = centerX + radius * 0.7f * (float)Math.Cos(DegreesToRadians(middleAngle));
            var textY = centerY + radius * 0.7f * (float)Math.Sin(DegreesToRadians(middleAngle)) + textFont.Size / 2;

            canvas.DrawText(segments[i], textX, textY, SKTextAlign.Center, textFont, textPaint);
        }

        var arrowShaftLength = radius * 0.2f;
        const float arrowHeadLength = 30;
        var arrowShaftEnd = new SKPoint(centerX, centerY - arrowShaftLength);
        var arrowTip = new SKPoint(centerX, arrowShaftEnd.Y - arrowHeadLength);
        var arrowLeftSide = new SKPoint(centerX - 15, arrowShaftEnd.Y);
        var arrowRightSide = new SKPoint(centerX + 15, arrowShaftEnd.Y);

        using var arrowPaint = new SKPaint();
        arrowPaint.Style = SKPaintStyle.StrokeAndFill;
        arrowPaint.Color = SKColors.Black;
        arrowPaint.IsAntialias = true;

        var arrowPath = new SKPath();
        arrowPath.MoveTo(centerX, centerY);
        arrowPath.LineTo(arrowShaftEnd.X, arrowShaftEnd.Y);

        arrowPath.MoveTo(arrowTip.X, arrowTip.Y);
        arrowPath.LineTo(arrowLeftSide.X, arrowLeftSide.Y);
        arrowPath.LineTo(arrowRightSide.X, arrowRightSide.Y);
        arrowPath.LineTo(arrowTip.X, arrowTip.Y);

        canvas.DrawPath(arrowPath, arrowPaint);
    }

    /// <summary>
    ///     Converts degrees to radians.
    /// </summary>
    /// <param name="degrees">The angle in degrees.</param>
    /// <returns>The angle in radians.</returns>
    private static float DegreesToRadians(float degrees)
    {
        return degrees * (float)Math.PI / 180;
    }

    /// <summary>
    ///     Generates a random pastel color.
    /// </summary>
    /// <returns>The generated pastel color.</returns>
    private static SKColor GeneratePastelColor()
    {
        var rand = new Random();
        var hue = (float)rand.Next(0, 361);
        var saturation = 40f + (float)rand.NextDouble() * 20f;
        var lightness = 70f + (float)rand.NextDouble() * 20f;

        return SKColor.FromHsl(hue, saturation, lightness);
    }

    /// <summary>
    ///     Play a game of War against the bot.
    /// </summary>
    /// <param name="betAmount">The amount to bet.</param>
    /// <example>.war 100</example>
    [Cmd]
    [Aliases]
    public async Task War(long betAmount)
    {
        var currentBalance = await Service.GetUserBalanceAsync(ctx.User.Id, ctx.Guild.Id);
        if (betAmount > currentBalance || betAmount <= 0)
        {
            await ReplyAsync(Strings.WarInvalidBet(ctx.Guild.Id));
            return;
        }

        if (currentBalance < betAmount)
        {
            await ReplyAsync(Strings.WarInsufficientFunds(ctx.Guild.Id, await Service.GetCurrencyEmote(ctx.Guild.Id)));
            return;
        }

        var rand = new Random();
        var playerCard = GenerateCard(rand);
        var opponentCard = GenerateCard(rand);

        var eb = new EmbedBuilder()
            .WithTitle(Strings.WarTitle(ctx.Guild.Id))
            .WithColor(Color.Blue)
            .AddField(Strings.WarYourCard(ctx.Guild.Id), $"{playerCard.rank}{playerCard.suit}", true)
            .AddField(Strings.WarOpponentCard(ctx.Guild.Id), $"{opponentCard.rank}{opponentCard.suit}", true);

        if (playerCard.value > opponentCard.value)
        {
            await Service.AddUserBalanceAsync(ctx.User.Id, betAmount, ctx.Guild.Id);
            await Service.AddTransactionAsync(ctx.User.Id, betAmount, Strings.WarTransactionWon(ctx.Guild.Id),
                ctx.Guild.Id);
            eb.WithDescription(Strings.WarResultWin(ctx.Guild.Id, $"{playerCard.rank}{playerCard.suit}",
                    $"{opponentCard.rank}{opponentCard.suit}"))
                .WithColor(Color.Green);
        }
        else if (playerCard.value < opponentCard.value)
        {
            await Service.AddUserBalanceAsync(ctx.User.Id, -betAmount, ctx.Guild.Id);
            await Service.AddTransactionAsync(ctx.User.Id, -betAmount, Strings.WarTransactionLost(ctx.Guild.Id),
                ctx.Guild.Id);
            eb.WithDescription(Strings.WarResultLose(ctx.Guild.Id, $"{playerCard.rank}{playerCard.suit}",
                    $"{opponentCard.rank}{opponentCard.suit}"))
                .WithColor(Color.Red);
        }
        else
        {
            // War battle!
            var warBetAmount = betAmount * 2;
            if (currentBalance < warBetAmount)
            {
                eb.WithDescription(Strings.WarResultTie(ctx.Guild.Id, $"{playerCard.rank}{playerCard.suit}"))
                    .WithColor(Color.Gold);
            }
            else
            {
                eb.WithTitle(Strings.WarBattleStart(ctx.Guild.Id))
                    .WithDescription(Strings.WarBattleDescription(ctx.Guild.Id))
                    .WithColor(Color.Orange);

                var warPlayerCard = GenerateCard(rand);
                var warOpponentCard = GenerateCard(rand);

                eb.AddField("War - Your Card", $"{warPlayerCard.rank}{warPlayerCard.suit}", true)
                    .AddField("War - Opponent's Card", $"{warOpponentCard.rank}{warOpponentCard.suit}", true);

                if (warPlayerCard.value > warOpponentCard.value)
                {
                    await Service.AddUserBalanceAsync(ctx.User.Id, warBetAmount, ctx.Guild.Id);
                    await Service.AddTransactionAsync(ctx.User.Id, warBetAmount,
                        Strings.WarTransactionWon(ctx.Guild.Id), ctx.Guild.Id);
                    eb.AddField("Result",
                            Strings.WarBattleWin(ctx.Guild.Id, warBetAmount,
                                await Service.GetCurrencyEmote(ctx.Guild.Id)))
                        .WithColor(Color.Green);
                }
                else
                {
                    await Service.AddUserBalanceAsync(ctx.User.Id, -warBetAmount, ctx.Guild.Id);
                    await Service.AddTransactionAsync(ctx.User.Id, -warBetAmount,
                        Strings.WarTransactionLost(ctx.Guild.Id), ctx.Guild.Id);
                    eb.AddField("Result",
                            Strings.WarBattleLose(ctx.Guild.Id, warBetAmount,
                                await Service.GetCurrencyEmote(ctx.Guild.Id)))
                        .WithColor(Color.Red);
                }
            }
        }

        await ReplyAsync(embed: eb.Build());
    }

    /// <summary>
    ///     Play a game of Craps.
    /// </summary>
    /// <param name="betAmount">The amount to bet.</param>
    /// <param name="betType">The type of bet (pass, dontpass, field, any).</param>
    /// <example>.craps 100 pass</example>
    [Cmd]
    [Aliases]
    public async Task Craps(long betAmount, string betType = "pass")
    {
        var currentBalance = await Service.GetUserBalanceAsync(ctx.User.Id, ctx.Guild.Id);
        if (betAmount > currentBalance || betAmount <= 0)
        {
            await ReplyAsync(Strings.CrapsInvalidBet(ctx.Guild.Id));
            return;
        }

        if (currentBalance < betAmount)
        {
            await ReplyAsync(Strings.CrapsInsufficientFunds(ctx.Guild.Id,
                await Service.GetCurrencyEmote(ctx.Guild.Id)));
            return;
        }

        var validBetTypes = new[]
        {
            "pass", "dontpass", "field", "any"
        };
        if (!validBetTypes.Contains(betType.ToLower()))
        {
            await ReplyAsync(Strings.CrapsInvalidBetType(ctx.Guild.Id));
            return;
        }

        var rand = new Random();
        var die1 = rand.Next(1, 7);
        var die2 = rand.Next(1, 7);
        var total = die1 + die2;

        var eb = new EmbedBuilder()
            .WithTitle(Strings.CrapsTitle(ctx.Guild.Id))
            .WithColor(Color.Blue)
            .AddField(Strings.CrapsRolled(ctx.Guild.Id, die1, die2, total), "");

        var won = false;
        var multiplier = 1.0;

        switch (betType.ToLower())
        {
            case "pass":
                switch (total)
                {
                    case 7:
                    case 11:
                        won = true;
                        eb.AddField("Result", Strings.CrapsNatural(ctx.Guild.Id, total));
                        break;
                    case 2:
                    case 3:
                    case 12:
                        won = false;
                        eb.AddField("Result", Strings.CrapsCraps(ctx.Guild.Id, total));
                        break;
                    default:
                    {
                        eb.AddField("Point", Strings.CrapsPointEstablished(ctx.Guild.Id, total));
                        var pointRoll = RollForPoint(rand, total);
                        won = pointRoll.won;
                        eb.AddField("Point Roll", pointRoll.description);
                        break;
                    }
                }

                break;

            case "dontpass":
                switch (total)
                {
                    case 2:
                    case 3:
                        won = true;
                        break;
                    case 7:
                    case 11:
                    // Push
                    case 12:
                        won = false;
                        break;
                    default:
                    {
                        var pointRoll = RollForPoint(rand, total);
                        won = !pointRoll.won;
                        eb.AddField("Point Roll", pointRoll.description);
                        break;
                    }
                }

                break;

            case "field":
                won = total is 2 or 3 or 4 or 9 or 10 or 11 or 12;
                if (total is 2 or 12) multiplier = 2.0;
                break;

            case "any":
                won = total == 7;
                if (won) multiplier = 4.0;
                break;
        }

        var profit = won ? (long)(betAmount * multiplier) : -betAmount;
        await Service.AddUserBalanceAsync(ctx.User.Id, profit, ctx.Guild.Id);
        await Service.AddTransactionAsync(ctx.User.Id, profit,
            won ? Strings.CrapsTransactionWon(ctx.Guild.Id) : Strings.CrapsTransactionLost(ctx.Guild.Id),
            ctx.Guild.Id);

        var resultMessage = betType.ToLower() switch
        {
            "pass" when won => Strings.CrapsPassWin(ctx.Guild.Id, Math.Abs(profit),
                await Service.GetCurrencyEmote(ctx.Guild.Id)),
            "pass" when !won => Strings.CrapsPassLose(ctx.Guild.Id, Math.Abs(profit),
                await Service.GetCurrencyEmote(ctx.Guild.Id)),
            "dontpass" when won => Strings.CrapsDontpassWin(ctx.Guild.Id, Math.Abs(profit),
                await Service.GetCurrencyEmote(ctx.Guild.Id)),
            "dontpass" when !won => Strings.CrapsDontpassLose(ctx.Guild.Id, Math.Abs(profit),
                await Service.GetCurrencyEmote(ctx.Guild.Id)),
            "field" when won => Strings.CrapsFieldWin(ctx.Guild.Id, Math.Abs(profit),
                await Service.GetCurrencyEmote(ctx.Guild.Id)),
            "field" when !won => Strings.CrapsFieldLose(ctx.Guild.Id, Math.Abs(profit),
                await Service.GetCurrencyEmote(ctx.Guild.Id)),
            "any" when won => Strings.CrapsAnyWin(ctx.Guild.Id, Math.Abs(profit),
                await Service.GetCurrencyEmote(ctx.Guild.Id)),
            "any" when !won => Strings.CrapsAnyLose(ctx.Guild.Id, Math.Abs(profit),
                await Service.GetCurrencyEmote(ctx.Guild.Id)),
            _ => won ? "You won!" : "You lost!"
        };

        eb.AddField("Outcome", resultMessage)
            .WithColor(won ? Color.Green : Color.Red);

        await ReplyAsync(embed: eb.Build());
    }

    /// <summary>
    ///     Play a scratch card game.
    /// </summary>
    /// <param name="cardType">The type of card to buy (bronze, silver, gold, diamond).</param>
    /// <example>.scratchcard silver</example>
    [Cmd]
    [Aliases]
    public async Task ScratchCard(string cardType = "bronze")
    {
        var cardTypes = new Dictionary<string, (int cost, double winChance, int minWin, int maxWin)>
        {
            {
                "bronze", (10, 0.3, 5, 50)
            },
            {
                "silver", (50, 0.25, 25, 200)
            },
            {
                "gold", (200, 0.2, 100, 1000)
            },
            {
                "diamond", (1000, 0.15, 500, 5000)
            }
        };

        if (!cardTypes.ContainsKey(cardType.ToLower()))
        {
            var availableCards = string.Join(", ", cardTypes.Keys);
            await ReplyAsync(Strings.ScratchCardInvalidBet(ctx.Guild.Id, availableCards));
            return;
        }

        var card = cardTypes[cardType.ToLower()];
        var currentBalance = await Service.GetUserBalanceAsync(ctx.User.Id, ctx.Guild.Id);

        if (currentBalance < card.cost)
        {
            await ReplyAsync(
                Strings.ScratchCardInsufficientFunds(ctx.Guild.Id, await Service.GetCurrencyEmote(ctx.Guild.Id)));
            return;
        }

        await Service.AddUserBalanceAsync(ctx.User.Id, -card.cost, ctx.Guild.Id);
        await Service.AddTransactionAsync(ctx.User.Id, -card.cost, Strings.ScratchCardTransactionBuy(ctx.Guild.Id),
            ctx.Guild.Id);

        var rand = new Random();
        var won = rand.NextDouble() < card.winChance;

        var eb = new EmbedBuilder()
            .WithTitle(Strings.ScratchCardTitle(ctx.Guild.Id))
            .WithColor(Color.Blue)
            .AddField(Strings.ScratchCardType(ctx.Guild.Id, cardType.ToUpper()), "", true)
            .AddField(Strings.ScratchCardCost(ctx.Guild.Id, card.cost, await Service.GetCurrencyEmote(ctx.Guild.Id)),
                "", true);

        if (won)
        {
            var winAmount = rand.Next(card.minWin, card.maxWin + 1);
            await Service.AddUserBalanceAsync(ctx.User.Id, winAmount, ctx.Guild.Id);
            await Service.AddTransactionAsync(ctx.User.Id, winAmount, Strings.ScratchCardTransactionWin(ctx.Guild.Id),
                ctx.Guild.Id);

            eb.WithDescription(Strings.ScratchCardWin(ctx.Guild.Id, winAmount,
                    await Service.GetCurrencyEmote(ctx.Guild.Id)))
                .WithColor(Color.Green);

            var symbols = GenerateScratchSymbols(true);
            eb.AddField(Strings.ScratchCardSymbols(ctx.Guild.Id, symbols), "");
        }
        else
        {
            eb.WithDescription(Strings.ScratchCardLose(ctx.Guild.Id))
                .WithColor(Color.Red);

            var symbols = GenerateScratchSymbols(false);
            eb.AddField(Strings.ScratchCardSymbols(ctx.Guild.Id, symbols), "");
        }

        await ReplyAsync(embed: eb.Build());
    }

    /// <summary>
    ///     Play Russian Roulette.
    /// </summary>
    /// <param name="betAmount">The amount to bet.</param>
    /// <param name="bullets">Number of bullets (1-5).</param>
    /// <example>.russianroulette 100 1</example>
    [Cmd]
    [Aliases]
    public async Task RussianRoulette(long betAmount, int bullets = 1)
    {
        var currentBalance = await Service.GetUserBalanceAsync(ctx.User.Id, ctx.Guild.Id);
        if (betAmount > currentBalance || betAmount <= 0)
        {
            await ReplyAsync(Strings.RussianRouletteInvalidBet(ctx.Guild.Id));
            return;
        }

        if (currentBalance < betAmount)
        {
            await ReplyAsync(Strings.RussianRouletteInsufficientFunds(ctx.Guild.Id,
                await Service.GetCurrencyEmote(ctx.Guild.Id)));
            return;
        }

        if (bullets is < 1 or > 5)
            bullets = 1;

        var rand = new Random();
        var chamber = rand.Next(1, 7);
        var bulletChambers = new HashSet<int>();

        while (bulletChambers.Count < bullets)
        {
            bulletChambers.Add(rand.Next(1, 7));
        }

        var survived = !bulletChambers.Contains(chamber);
        var multiplier = bullets switch
        {
            1 => 1.2,
            2 => 1.5,
            3 => 2.0,
            4 => 3.0,
            5 => 5.0,
            _ => 1.2
        };

        var eb = new EmbedBuilder()
            .WithTitle(Strings.RussianRouletteTitle(ctx.Guild.Id))
            .WithDescription(Strings.RussianRouletteDescription(ctx.Guild.Id))
            .WithColor(Color.Blue)
            .AddField(Strings.RussianRouletteChambers(ctx.Guild.Id, $"{chamber}/6"), "", true)
            .AddField(Strings.RussianRouletteBullets(ctx.Guild.Id, bullets.ToString()), "", true);

        if (survived)
        {
            var winAmount = (long)(betAmount * multiplier);
            await Service.AddUserBalanceAsync(ctx.User.Id, winAmount, ctx.Guild.Id);
            await Service.AddTransactionAsync(ctx.User.Id, winAmount,
                Strings.RussianRouletteTransactionWon(ctx.Guild.Id), ctx.Guild.Id);

            eb.AddField("Result", Strings.RussianRouletteClick(ctx.Guild.Id))
                .AddField("Outcome",
                    Strings.RussianRouletteSurvived(ctx.Guild.Id, winAmount,
                        await Service.GetCurrencyEmote(ctx.Guild.Id)))
                .WithColor(Color.Green);
        }
        else
        {
            await Service.AddUserBalanceAsync(ctx.User.Id, -betAmount, ctx.Guild.Id);
            await Service.AddTransactionAsync(ctx.User.Id, -betAmount,
                Strings.RussianRouletteTransactionLost(ctx.Guild.Id), ctx.Guild.Id);

            eb.AddField("Outcome",
                    Strings.RussianRouletteDied(ctx.Guild.Id, betAmount, await Service.GetCurrencyEmote(ctx.Guild.Id)))
                .WithColor(Color.Red);
        }

        await ReplyAsync(embed: eb.Build());
    }

    /// <summary>
    ///     Play a game of Baccarat.
    /// </summary>
    /// <param name="betAmount">The amount to bet.</param>
    /// <param name="betType">The type of bet (player, banker, tie).</param>
    /// <example>.baccarat 100 player</example>
    [Cmd]
    [Aliases]
    public async Task Baccarat(long betAmount, string betType = "player")
    {
        var currentBalance = await Service.GetUserBalanceAsync(ctx.User.Id, ctx.Guild.Id);
        if (betAmount > currentBalance || betAmount <= 0)
        {
            await ReplyAsync(Strings.BaccaratInvalidBet(ctx.Guild.Id));
            return;
        }

        if (currentBalance < betAmount)
        {
            await ReplyAsync(
                Strings.BaccaratInsufficientFunds(ctx.Guild.Id, await Service.GetCurrencyEmote(ctx.Guild.Id)));
            return;
        }

        var validBetTypes = new[]
        {
            "player", "banker", "tie"
        };
        if (!validBetTypes.Contains(betType.ToLower()))
        {
            await ReplyAsync(Strings.BaccaratInvalidBetType(ctx.Guild.Id));
            return;
        }

        var rand = new Random();
        var playerHand = new List<(string rank, string suit, int value)>
        {
            GenerateCard(rand), GenerateCard(rand)
        };
        var bankerHand = new List<(string rank, string suit, int value)>
        {
            GenerateCard(rand), GenerateCard(rand)
        };

        var playerTotal = CalculateBaccaratTotal(playerHand);
        var bankerTotal = CalculateBaccaratTotal(bankerHand);

        // Baccarat third card rules
        var playerNatural = playerTotal >= 8;
        var bankerNatural = bankerTotal >= 8;

        if (!playerNatural && !bankerNatural)
        {
            // Player draws third card if total is 0-5
            if (playerTotal <= 5)
            {
                playerHand.Add(GenerateCard(rand));
                playerTotal = CalculateBaccaratTotal(playerHand);
            }

            switch (bankerTotal)
            {
                // Banker third card rules are complex, simplified here
                case <= 5 when playerHand.Count == 2:
                    bankerHand.Add(GenerateCard(rand));
                    bankerTotal = CalculateBaccaratTotal(bankerHand);
                    break;
                case <= 2:
                    bankerHand.Add(GenerateCard(rand));
                    bankerTotal = CalculateBaccaratTotal(bankerHand);
                    break;
            }
        }

        var eb = new EmbedBuilder()
            .WithTitle(Strings.BaccaratTitle(ctx.Guild.Id))
            .WithColor(Color.Blue)
            .AddField(Strings.BaccaratPlayerHand(ctx.Guild.Id),
                string.Join(" ", playerHand.Select(c => $"{c.rank}{c.suit}")) + $" (Total: {playerTotal})")
            .AddField(Strings.BaccaratBankerHand(ctx.Guild.Id),
                string.Join(" ", bankerHand.Select(c => $"{c.rank}{c.suit}")) + $" (Total: {bankerTotal})");

        var won = false;
        var multiplier = 1.0;

        if (playerTotal > bankerTotal)
        {
            eb.AddField("Result", Strings.BaccaratPlayerWins(ctx.Guild.Id, playerTotal));
            if (betType.Equals("player", StringComparison.OrdinalIgnoreCase))
            {
                won = true;
                multiplier = 1.95; // 1:1 payout minus 5% commission
            }
        }
        else if (bankerTotal > playerTotal)
        {
            eb.AddField("Result", Strings.BaccaratBankerWins(ctx.Guild.Id, bankerTotal));
            if (betType.Equals("banker", StringComparison.OrdinalIgnoreCase))
            {
                won = true;
                multiplier = 1.95; // 1:1 payout minus 5% commission
            }
        }
        else
        {
            eb.AddField("Result", Strings.BaccaratTie(ctx.Guild.Id, playerTotal));
            if (betType.Equals("tie", StringComparison.OrdinalIgnoreCase))
            {
                won = true;
                multiplier = 8.0; // 8:1 payout for tie
            }
        }

        var profit = won ? (long)(betAmount * multiplier) : -betAmount;
        await Service.AddUserBalanceAsync(ctx.User.Id, profit, ctx.Guild.Id);
        await Service.AddTransactionAsync(ctx.User.Id, profit,
            won ? Strings.BaccaratTransactionWon(ctx.Guild.Id) : Strings.BaccaratTransactionLost(ctx.Guild.Id),
            ctx.Guild.Id);

        var resultMessage = betType.ToLower() switch
        {
            "player" when won => Strings.BaccaratPlayerBetWin(ctx.Guild.Id, Math.Abs(profit),
                await Service.GetCurrencyEmote(ctx.Guild.Id)),
            "banker" when won => Strings.BaccaratBankerBetWin(ctx.Guild.Id, Math.Abs(profit),
                await Service.GetCurrencyEmote(ctx.Guild.Id)),
            "tie" when won => Strings.BaccaratTieBetWin(ctx.Guild.Id, Math.Abs(profit),
                await Service.GetCurrencyEmote(ctx.Guild.Id)),
            _ => Strings.BaccaratBetLose(ctx.Guild.Id, Math.Abs(profit), await Service.GetCurrencyEmote(ctx.Guild.Id))
        };

        if (playerNatural || bankerNatural)
        {
            eb.AddField("Special", Strings.BaccaratNatural(ctx.Guild.Id, Math.Max(playerTotal, bankerTotal)));
        }

        eb.AddField("Outcome", resultMessage)
            .WithColor(won ? Color.Green : Color.Red);

        await ReplyAsync(embed: eb.Build());
    }

    /// <summary>
    ///     Play a simplified Minesweeper game.
    /// </summary>
    /// <param name="betAmount">The amount to bet.</param>
    /// <param name="size">Grid size (small, medium, large).</param>
    /// <example>.minesweeper 100 medium</example>
    [Cmd]
    [Aliases]
    public async Task Minesweeper(long betAmount, string size = "small")
    {
        var currentBalance = await Service.GetUserBalanceAsync(ctx.User.Id, ctx.Guild.Id);
        if (betAmount > currentBalance || betAmount <= 0)
        {
            await ReplyAsync(Strings.MinesweeperInvalidBet(ctx.Guild.Id));
            return;
        }

        if (currentBalance < betAmount)
        {
            await ReplyAsync(
                Strings.MinesweeperInsufficientFunds(ctx.Guild.Id, await Service.GetCurrencyEmote(ctx.Guild.Id)));
            return;
        }

        var gridSizes = new Dictionary<string, (int size, int mines, double safeChance)>
        {
            {
                "small", (3, 2, 0.7)
            },
            {
                "medium", (5, 6, 0.6)
            },
            {
                "large", (7, 12, 0.5)
            }
        };

        if (!gridSizes.ContainsKey(size.ToLower()))
        {
            await ReplyAsync(Strings.MinesweeperInvalidSize(ctx.Guild.Id));
            return;
        }

        var grid = gridSizes[size.ToLower()];
        var rand = new Random();
        var survived = rand.NextDouble() < grid.safeChance;

        var multiplier = size.ToLower() switch
        {
            "small" => 1.5,
            "medium" => 2.5,
            "large" => 4.0,
            _ => 1.5
        };

        var eb = new EmbedBuilder()
            .WithTitle(Strings.MinesweeperTitle(ctx.Guild.Id))
            .WithDescription(Strings.MinesweeperDescription(ctx.Guild.Id))
            .WithColor(Color.Blue)
            .AddField(Strings.MinesweeperGrid(ctx.Guild.Id, grid.size, grid.size), $"{grid.size}x{grid.size}", true)
            .AddField(Strings.MinesweeperMines(ctx.Guild.Id, grid.mines), grid.mines.ToString(), true);

        if (survived)
        {
            var winAmount = (long)(betAmount * multiplier);
            await Service.AddUserBalanceAsync(ctx.User.Id, winAmount, ctx.Guild.Id);
            await Service.AddTransactionAsync(ctx.User.Id, winAmount, Strings.MinesweeperTransactionWon(ctx.Guild.Id),
                ctx.Guild.Id);

            eb.WithDescription(Strings.MinesweeperAllSafe(ctx.Guild.Id, winAmount,
                    await Service.GetCurrencyEmote(ctx.Guild.Id)))
                .WithColor(Color.Green)
                .AddField(Strings.MinesweeperMultiplier(ctx.Guild.Id, multiplier), $"x{multiplier:F1}", true);
        }
        else
        {
            await Service.AddUserBalanceAsync(ctx.User.Id, -betAmount, ctx.Guild.Id);
            await Service.AddTransactionAsync(ctx.User.Id, -betAmount, Strings.MinesweeperTransactionLost(ctx.Guild.Id),
                ctx.Guild.Id);

            eb.WithDescription(Strings.MinesweeperHitMine(ctx.Guild.Id, betAmount,
                    await Service.GetCurrencyEmote(ctx.Guild.Id)))
                .WithColor(Color.Red);
        }

        await ReplyAsync(embed: eb.Build());
    }


    /// <summary>
    ///     Buy lottery tickets.
    /// </summary>
    /// <param name="ticketCount">Number of tickets to buy (1-10).</param>
    /// <example>.lottery 5</example>
    [Cmd]
    [Aliases]
    public async Task Lottery(int ticketCount = 1)
    {
        if (ticketCount is < 1 or > 10)
        {
            await ReplyAsync(Strings.LotteryTicketInvalid(ctx.Guild.Id));
            return;
        }

        const int ticketCost = 50; // Fixed cost per ticket
        var totalCost = ticketCost * ticketCount;
        var currentBalance = await Service.GetUserBalanceAsync(ctx.User.Id, ctx.Guild.Id);

        if (currentBalance < totalCost)
        {
            await ReplyAsync(Strings.LotteryInsufficientFunds(ctx.Guild.Id,
                await Service.GetCurrencyEmote(ctx.Guild.Id), ticketCount));
            return;
        }

        await Service.AddUserBalanceAsync(ctx.User.Id, -totalCost, ctx.Guild.Id);
        await Service.AddTransactionAsync(ctx.User.Id, -totalCost, Strings.LotteryTransactionBuy(ctx.Guild.Id),
            ctx.Guild.Id);

        // Simple lottery implementation - immediate random draw
        var rand = new Random();
        var won = false;
        var winAmount = 0L;

        // Different win chances based on ticket count
        var winChance = Math.Min(0.1 + (ticketCount * 0.05), 0.4); // Max 40% chance

        if (rand.NextDouble() < winChance)
        {
            won = true;
            var multiplier = ticketCount switch
            {
                1 => rand.Next(2, 5),
                2 => rand.Next(3, 7),
                3 => rand.Next(4, 9),
                4 => rand.Next(5, 11),
                5 => rand.Next(6, 13),
                _ => rand.Next(7, 15)
            };
            winAmount = totalCost * multiplier;
        }

        var eb = new EmbedBuilder()
            .WithTitle(Strings.LotteryDrawTitle(ctx.Guild.Id, rand.Next(1000, 9999)))
            .WithColor(won ? Color.Green : Color.Red)
            .AddField(Strings.LotteryTicketCost(ctx.Guild.Id, ticketCost, await Service.GetCurrencyEmote(ctx.Guild.Id)),
                $"{ticketCost} {await Service.GetCurrencyEmote(ctx.Guild.Id)}", true)
            .AddField(Strings.LotteryYourTickets(ctx.Guild.Id, ticketCount.ToString()), "", true);

        if (won)
        {
            await Service.AddUserBalanceAsync(ctx.User.Id, winAmount, ctx.Guild.Id);
            await Service.AddTransactionAsync(ctx.User.Id, winAmount, Strings.LotteryTransactionWin(ctx.Guild.Id),
                ctx.Guild.Id);

            eb.WithDescription(Strings.LotteryWinner(ctx.Guild.Id, ctx.User.Mention, rand.Next(1, 1000), winAmount,
                await Service.GetCurrencyEmote(ctx.Guild.Id)));
        }
        else
        {
            eb.WithDescription(Strings.LotteryNoWinner(ctx.Guild.Id, totalCost + rand.Next(100, 500),
                await Service.GetCurrencyEmote(ctx.Guild.Id)));
        }

        await ReplyAsync(Strings.LotteryTicketsBought(ctx.Guild.Id, ticketCount, totalCost,
            await Service.GetCurrencyEmote(ctx.Guild.Id)));
        await ReplyAsync(embed: eb.Build());
    }

    private static (string rank, string suit, int value) GenerateCard(Random rand)
    {
        var suits = new[]
        {
            "♠️", "♥️", "♦️", "♣️"
        };
        var ranks = new[]
        {
            ("A", 14), ("2", 2), ("3", 3), ("4", 4), ("5", 5), ("6", 6), ("7", 7), ("8", 8), ("9", 9), ("10", 10),
            ("J", 11), ("Q", 12), ("K", 13)
        };

        var suit = suits[rand.Next(suits.Length)];
        var rank = ranks[rand.Next(ranks.Length)];

        return (rank.Item1, suit, rank.Item2);
    }

    private static (bool won, string description) RollForPoint(Random rand, int point)
    {
        var attempts = 0;
        while (attempts < 10) // Prevent infinite loops
        {
            var die1 = rand.Next(1, 7);
            var die2 = rand.Next(1, 7);
            var total = die1 + die2;
            attempts++;

            if (total == point)
                return (true, $"Rolled {die1} + {die2} = {total} (Point made!)");
            if (total == 7)
                return (false, $"Rolled {die1} + {die2} = 7 (Seven out!)");
        }

        return (false, "Seven out!");
    }

    private static string GenerateScratchSymbols(bool winning)
    {
        var symbols = new[]
        {
            "🍒", "🍋", "🍊", "🍇", "⭐", "💎", "🔔", "🍀"
        };
        var rand = new Random();

        if (winning)
        {
            var winSymbol = symbols[rand.Next(symbols.Length)];
            return $"{winSymbol} {winSymbol} {winSymbol}";
        }

        var symbol1 = symbols[rand.Next(symbols.Length)];
        var symbol2 = symbols[rand.Next(symbols.Length)];
        var symbol3 = symbols[rand.Next(symbols.Length)];

        // Make sure they're not all the same
        while (symbol1 == symbol2 && symbol2 == symbol3)
        {
            symbol2 = symbols[rand.Next(symbols.Length)];
            symbol3 = symbols[rand.Next(symbols.Length)];
        }

        return $"{symbol1} {symbol2} {symbol3}";
    }

    private static int CalculateBaccaratTotal(List<(string rank, string suit, int value)> hand)
    {
        var total = 0;
        foreach (var card in hand)
        {
            var cardValue = card.rank switch
            {
                "A" => 1,
                "J" or "Q" or "K" => 0,
                _ => int.Parse(card.rank) > 10 ? 0 : int.Parse(card.rank)
            };
            total += cardValue;
        }

        return total % 10;
    }

    /// <summary>
    ///     Play a Crash game where you bet on a multiplier that can crash at any time.
    /// </summary>
    /// <param name="betAmount">The amount to bet.</param>
    /// <param name="targetMultiplier">The multiplier to cash out at (1.1x - 10.0x).</param>
    /// <example>.crash 100 2.5</example>
    [Cmd]
    [Aliases]
    public async Task Crash(long betAmount, double targetMultiplier = 2.0)
    {
        var currentBalance = await Service.GetUserBalanceAsync(ctx.User.Id, ctx.Guild.Id);
        if (betAmount > currentBalance || betAmount <= 0)
        {
            await ReplyAsync(Strings.CrashInvalidBet(ctx.Guild.Id));
            return;
        }

        if (targetMultiplier is < 1.1 or > 10.0)
        {
            await ReplyAsync(Strings.CrashInvalidMultiplier(ctx.Guild.Id));
            return;
        }

        var rand = new Random();
        var crashPoint = GenerateCrashPoint(rand);
        var won = targetMultiplier <= crashPoint;

        var eb = new EmbedBuilder()
            .WithTitle(Strings.CrashTitle(ctx.Guild.Id))
            .WithColor(won ? Color.Green : Color.Red)
            .AddField(Strings.CrashTargetMultiplier(ctx.Guild.Id, $"{targetMultiplier:F2}x"), "", true)
            .AddField(Strings.CrashActualMultiplier(ctx.Guild.Id, $"{crashPoint:F2}x"), "", true);

        if (won)
        {
            var winAmount = (long)(betAmount * (targetMultiplier - 1));
            await Service.AddUserBalanceAsync(ctx.User.Id, winAmount, ctx.Guild.Id);
            await Service.AddTransactionAsync(ctx.User.Id, winAmount, Strings.CrashTransactionWon(ctx.Guild.Id),
                ctx.Guild.Id);

            eb.WithDescription(Strings.CrashWin(ctx.Guild.Id, targetMultiplier, winAmount,
                await Service.GetCurrencyEmote(ctx.Guild.Id)));
        }
        else
        {
            await Service.AddUserBalanceAsync(ctx.User.Id, -betAmount, ctx.Guild.Id);
            await Service.AddTransactionAsync(ctx.User.Id, -betAmount, Strings.CrashTransactionLost(ctx.Guild.Id),
                ctx.Guild.Id);

            eb.WithDescription(Strings.CrashLose(ctx.Guild.Id, crashPoint, betAmount,
                await Service.GetCurrencyEmote(ctx.Guild.Id)));
        }

        await ReplyAsync(embed: eb.Build());
    }

    /// <summary>
    ///     Play Keno by selecting numbers and seeing how many match the drawn numbers.
    /// </summary>
    /// <param name="betAmount">The amount to bet.</param>
    /// <param name="numbers">Up to 10 numbers between 1-80, separated by spaces.</param>
    /// <example>.keno 100 1 15 23 42 67</example>
    [Cmd]
    [Aliases]
    public async Task Keno(long betAmount, [Remainder] string numbers)
    {
        var currentBalance = await Service.GetUserBalanceAsync(ctx.User.Id, ctx.Guild.Id);
        if (betAmount > currentBalance || betAmount <= 0)
        {
            await ReplyAsync(Strings.KenoInvalidBet(ctx.Guild.Id));
            return;
        }

        var selectedNumbers = ParseKenoNumbers(numbers);
        if (selectedNumbers.Count is 0 or > 10)
        {
            await ReplyAsync(Strings.KenoInvalidNumbers(ctx.Guild.Id));
            return;
        }

        if (selectedNumbers.Any(n => n is < 1 or > 80))
        {
            await ReplyAsync(Strings.KenoNumbersOutOfRange(ctx.Guild.Id));
            return;
        }

        var rand = new Random();
        var drawnNumbers = GenerateKenoNumbers(rand, 20);
        var matches = selectedNumbers.Intersect(drawnNumbers).Count();
        var multiplier = CalculateKenoMultiplier(selectedNumbers.Count, matches);

        var eb = new EmbedBuilder()
            .WithTitle(Strings.KenoTitle(ctx.Guild.Id))
            .WithColor(multiplier > 0 ? Color.Green : Color.Red)
            .AddField(Strings.KenoYourNumbers(ctx.Guild.Id, string.Join(", ", selectedNumbers.OrderBy(x => x))), "")
            .AddField(Strings.KenoDrawnNumbers(ctx.Guild.Id, string.Join(", ", drawnNumbers.OrderBy(x => x))), "")
            .AddField(Strings.KenoMatches(ctx.Guild.Id, matches.ToString()), "", true);

        if (multiplier > 0)
        {
            var winAmount = (long)(betAmount * multiplier);
            await Service.AddUserBalanceAsync(ctx.User.Id, winAmount, ctx.Guild.Id);
            await Service.AddTransactionAsync(ctx.User.Id, winAmount, Strings.KenoTransactionWon(ctx.Guild.Id),
                ctx.Guild.Id);

            eb.WithDescription(Strings.KenoWin(ctx.Guild.Id, matches, winAmount,
                await Service.GetCurrencyEmote(ctx.Guild.Id)));
        }
        else
        {
            await Service.AddUserBalanceAsync(ctx.User.Id, -betAmount, ctx.Guild.Id);
            await Service.AddTransactionAsync(ctx.User.Id, -betAmount, Strings.KenoTransactionLost(ctx.Guild.Id),
                ctx.Guild.Id);

            eb.WithDescription(Strings.KenoLose(ctx.Guild.Id, matches, betAmount,
                await Service.GetCurrencyEmote(ctx.Guild.Id)));
        }

        await ReplyAsync(embed: eb.Build());
    }

    /// <summary>
    ///     Play Plinko by dropping a ball down pegs for random multipliers.
    /// </summary>
    /// <param name="betAmount">The amount to bet.</param>
    /// <param name="rows">Number of peg rows (5-10).</param>
    /// <example>.plinko 100 8</example>
    [Cmd]
    [Aliases]
    public async Task Plinko(long betAmount, int rows = 8)
    {
        var currentBalance = await Service.GetUserBalanceAsync(ctx.User.Id, ctx.Guild.Id);
        if (betAmount > currentBalance || betAmount <= 0)
        {
            await ReplyAsync(Strings.PlinkoInvalidBet(ctx.Guild.Id));
            return;
        }

        if (rows is < 5 or > 10)
        {
            await ReplyAsync(Strings.PlinkoInvalidRows(ctx.Guild.Id));
            return;
        }

        var rand = new Random();
        var path = SimulatePlinkoBall(rand, rows);
        var slot = path.Last();
        var multiplier = GetPlinkoMultiplier(rows, slot);

        using var bitmap = new SKBitmap(400, 500);
        using var canvas = new SKCanvas(bitmap);
        DrawPlinkoBoard(canvas, rows, path);

        using var stream = new MemoryStream();
        bitmap.Encode(stream, SKEncodedImageFormat.Png, 100);
        stream.Seek(0, SeekOrigin.Begin);

        var profit = (long)(betAmount * multiplier) - betAmount;
        await Service.AddUserBalanceAsync(ctx.User.Id, profit, ctx.Guild.Id);
        await Service.AddTransactionAsync(ctx.User.Id, profit,
            profit > 0 ? Strings.PlinkoTransactionWon(ctx.Guild.Id) : Strings.PlinkoTransactionLost(ctx.Guild.Id),
            ctx.Guild.Id);

        var eb = new EmbedBuilder()
            .WithTitle(Strings.PlinkoTitle(ctx.Guild.Id))
            .WithColor(profit > 0 ? Color.Green : Color.Red)
            .WithDescription(profit > 0
                ? Strings.PlinkoWin(ctx.Guild.Id, multiplier, Math.Abs(profit),
                    await Service.GetCurrencyEmote(ctx.Guild.Id))
                : Strings.PlinkoLose(ctx.Guild.Id, multiplier, Math.Abs(profit),
                    await Service.GetCurrencyEmote(ctx.Guild.Id)))
            .AddField(Strings.PlinkoMultiplier(ctx.Guild.Id, $"{multiplier:F2}x"), "", true)
            .AddField(Strings.PlinkoSlot(ctx.Guild.Id, slot.ToString()), "", true)
            .WithImageUrl("attachment://plinko.png");

        await ctx.Channel.SendFileAsync(stream, "plinko.png", embed: eb.Build());
    }

    /// <summary>
    ///     Play an enhanced Wheel of Fortune with customizable segments.
    /// </summary>
    /// <param name="betAmount">The amount to bet.</param>
    /// <param name="wheelType">The type of wheel (classic, risky, balanced).</param>
    /// <example>.wheelfortune 100 risky</example>
    [Cmd]
    [Aliases]
    public async Task WheelFortune(long betAmount, string wheelType = "classic")
    {
        var currentBalance = await Service.GetUserBalanceAsync(ctx.User.Id, ctx.Guild.Id);
        if (betAmount > currentBalance || betAmount <= 0)
        {
            await ReplyAsync(Strings.WheelFortuneInvalidBet(ctx.Guild.Id));
            return;
        }

        var wheelConfigs = GetWheelConfigurations();
        if (!wheelConfigs.ContainsKey(wheelType.ToLower()))
        {
            await ReplyAsync(Strings.WheelFortuneInvalidType(ctx.Guild.Id));
            return;
        }

        var config = wheelConfigs[wheelType.ToLower()];
        var rand = new Random();
        var winningIndex = GenerateWeightedRandomSegment(config.segments.Length, config.weights, rand);
        var segment = config.segments[winningIndex];

        using var bitmap = new SKBitmap(500, 500);
        using var canvas = new SKCanvas(bitmap);
        DrawWheelFortune(canvas, config.segments, winningIndex);

        using var stream = new MemoryStream();
        bitmap.Encode(stream, SKEncodedImageFormat.Png, 100);
        stream.Seek(0, SeekOrigin.Begin);

        var balanceChange = await ComputeWheelBalanceChange(segment, betAmount);
        await Service.AddUserBalanceAsync(ctx.User.Id, balanceChange, ctx.Guild.Id);
        await Service.AddTransactionAsync(ctx.User.Id, balanceChange,
            balanceChange > 0
                ? Strings.WheelFortuneTransactionWon(ctx.Guild.Id)
                : Strings.WheelFortuneTransactionLost(ctx.Guild.Id),
            ctx.Guild.Id);

        var eb = new EmbedBuilder()
            .WithTitle(Strings.WheelFortuneTitle(ctx.Guild.Id))
            .WithColor(balanceChange > 0 ? Color.Green : Color.Red)
            .WithDescription(Strings.WheelFortuneResult(ctx.Guild.Id, segment))
            .AddField(Strings.WheelFortuneType(ctx.Guild.Id, wheelType.ToUpper()), "", true)
            .WithImageUrl("attachment://wheel.png");

        await ctx.Channel.SendFileAsync(stream, "wheel.png", embed: eb.Build());
    }

    /// <summary>
    ///     Start a duck race similar to horse racing.
    /// </summary>
    /// <param name="betAmount">The amount to bet on the duck race.</param>
    /// <example>.duckrace 100</example>
    [Cmd]
    [Aliases]
    public async Task DuckRace(long betAmount)
    {
        var currentBalance = await Service.GetUserBalanceAsync(ctx.User.Id, ctx.Guild.Id);
        if (betAmount > currentBalance || betAmount <= 0)
        {
            await ReplyAsync(Strings.DuckRaceInvalidBet(ctx.Guild.Id));
            return;
        }

        var rand = new Random();
        var ducks = GenerateDucks(5);
        var raceResults = SimulateDuckRace(rand, ducks);

        var userDuck = rand.Next(ducks.Count);
        var userPosition = raceResults.FindIndex(r => r.duckIndex == userDuck) + 1;

        var multiplier = userPosition switch
        {
            1 => 4.0,
            2 => 2.0,
            3 => 1.2,
            _ => 0.0
        };

        var eb = new EmbedBuilder()
            .WithTitle(Strings.DuckRaceTitle(ctx.Guild.Id))
            .WithColor(multiplier > 0 ? Color.Green : Color.Red)
            .AddField(Strings.DuckRaceYourDuck(ctx.Guild.Id, $"{ducks[userDuck]} (#{userDuck + 1})"), "", true)
            .AddField(Strings.DuckRacePosition(ctx.Guild.Id, $"{userPosition}/5"), "", true);

        var resultsText = "";
        for (var i = 0; i < raceResults.Count; i++)
        {
            var duck = ducks[raceResults[i].duckIndex];
            var isUserDuck = raceResults[i].duckIndex == userDuck ? " ⭐" : "";
            resultsText += $"{i + 1}. {duck} Duck #{raceResults[i].duckIndex + 1}{isUserDuck}\n";
        }

        eb.AddField(Strings.DuckRaceResults(ctx.Guild.Id, resultsText), "");

        if (multiplier > 0)
        {
            var winAmount = (long)(betAmount * multiplier);
            await Service.AddUserBalanceAsync(ctx.User.Id, winAmount, ctx.Guild.Id);
            await Service.AddTransactionAsync(ctx.User.Id, winAmount, Strings.DuckRaceTransactionWon(ctx.Guild.Id),
                ctx.Guild.Id);

            eb.WithDescription(Strings.DuckRaceWin(ctx.Guild.Id, winAmount,
                await Service.GetCurrencyEmote(ctx.Guild.Id)));
        }
        else
        {
            await Service.AddUserBalanceAsync(ctx.User.Id, -betAmount, ctx.Guild.Id);
            await Service.AddTransactionAsync(ctx.User.Id, -betAmount, Strings.DuckRaceTransactionLost(ctx.Guild.Id),
                ctx.Guild.Id);

            eb.WithDescription(Strings.DuckRaceLose(ctx.Guild.Id, betAmount,
                await Service.GetCurrencyEmote(ctx.Guild.Id)));
        }

        await ReplyAsync(embed: eb.Build());
    }

    /// <summary>
    ///     Play a Bingo game with number calling.
    /// </summary>
    /// <param name="betAmount">The amount to bet.</param>
    /// <param name="cardType">The type of bingo card (small, large).</param>
    /// <example>.bingo 100 large</example>
    [Cmd]
    [Aliases]
    public async Task Bingo(long betAmount, string cardType = "small")
    {
        var currentBalance = await Service.GetUserBalanceAsync(ctx.User.Id, ctx.Guild.Id);
        if (betAmount > currentBalance || betAmount <= 0)
        {
            await ReplyAsync(Strings.BingoInvalidBet(ctx.Guild.Id));
            return;
        }

        var cardConfigs = new Dictionary<string, (int size, int numbers, double winChance, double multiplier)>
        {
            {
                "small", (3, 9, 0.4, 2.0)
            },
            {
                "large", (5, 25, 0.25, 3.5)
            }
        };

        if (!cardConfigs.ContainsKey(cardType.ToLower()))
        {
            await ReplyAsync(Strings.BingoInvalidCardType(ctx.Guild.Id));
            return;
        }

        var config = cardConfigs[cardType.ToLower()];
        var rand = new Random();
        var playerCard = GenerateBingoCard(rand, config.size);
        var calledNumbers = CallBingoNumbers(rand, config.numbers);
        var won = CheckBingoWin(playerCard, calledNumbers, rand.NextDouble() < config.winChance);

        var eb = new EmbedBuilder()
            .WithTitle(Strings.BingoTitle(ctx.Guild.Id))
            .WithColor(won ? Color.Green : Color.Red)
            .AddField(Strings.BingoCardType(ctx.Guild.Id, cardType.ToUpper()), "", true)
            .AddField(
                Strings.BingoCalledNumbers(ctx.Guild.Id,
                    string.Join(", ", calledNumbers.Take(10)) + (calledNumbers.Count > 10 ? "..." : "")), "");

        var cardDisplay = FormatBingoCard(playerCard, calledNumbers);
        eb.AddField(Strings.BingoYourCard(ctx.Guild.Id, cardDisplay), "");

        if (won)
        {
            var winAmount = (long)(betAmount * config.multiplier);
            await Service.AddUserBalanceAsync(ctx.User.Id, winAmount, ctx.Guild.Id);
            await Service.AddTransactionAsync(ctx.User.Id, winAmount, Strings.BingoTransactionWon(ctx.Guild.Id),
                ctx.Guild.Id);

            eb.WithDescription(Strings.BingoWin(ctx.Guild.Id, winAmount, await Service.GetCurrencyEmote(ctx.Guild.Id)));
        }
        else
        {
            await Service.AddUserBalanceAsync(ctx.User.Id, -betAmount, ctx.Guild.Id);
            await Service.AddTransactionAsync(ctx.User.Id, -betAmount, Strings.BingoTransactionLost(ctx.Guild.Id),
                ctx.Guild.Id);

            eb.WithDescription(Strings.BingoLose(ctx.Guild.Id, betAmount,
                await Service.GetCurrencyEmote(ctx.Guild.Id)));
        }

        await ReplyAsync(embed: eb.Build());
    }

    /// <summary>
    ///     Challenge another player to a dice duel.
    /// </summary>
    /// <param name="opponent">The user to challenge.</param>
    /// <param name="betAmount">The amount each player bets.</param>
    /// <example>.diceduel @user 100</example>
    [Cmd]
    [Aliases]
    public async Task DiceDuel(IUser opponent, long betAmount)
    {
        if (opponent.Id == ctx.User.Id)
        {
            await ReplyAsync(Strings.DiceDuelCannotSelf(ctx.Guild.Id));
            return;
        }

        if (opponent.IsBot)
        {
            await ReplyAsync(Strings.DiceDuelCannotBot(ctx.Guild.Id));
            return;
        }

        var userBalance = await Service.GetUserBalanceAsync(ctx.User.Id, ctx.Guild.Id);
        var opponentBalance = await Service.GetUserBalanceAsync(opponent.Id, ctx.Guild.Id);

        if (betAmount > userBalance || betAmount <= 0)
        {
            await ReplyAsync(Strings.DiceDuelInvalidBet(ctx.Guild.Id));
            return;
        }

        if (betAmount > opponentBalance)
        {
            await ReplyAsync(Strings.DiceDuelOpponentInsufficientFunds(ctx.Guild.Id,
                await Service.GetCurrencyEmote(ctx.Guild.Id)));
            return;
        }

        var challengeEmbed = new EmbedBuilder()
            .WithTitle(Strings.DiceDuelChallengeTitle(ctx.Guild.Id))
            .WithDescription(Strings.DiceDuelChallenge(ctx.Guild.Id, ctx.User.Mention, opponent.Mention, betAmount,
                await Service.GetCurrencyEmote(ctx.Guild.Id)))
            .WithColor(Color.Orange);

        var component = new ComponentBuilder()
            .WithButton(Strings.DiceDuelAccept(ctx.Guild.Id), $"diceduel_accept_{ctx.User.Id}_{betAmount}",
                ButtonStyle.Success)
            .WithButton(Strings.DiceDuelDecline(ctx.Guild.Id), $"diceduel_decline_{ctx.User.Id}", ButtonStyle.Danger)
            .Build();

        await ReplyAsync(embed: challengeEmbed.Build(), components: component);
    }

    /// <summary>
    ///     Play a memory game by remembering sequences.
    /// </summary>
    /// <param name="betAmount">The amount to bet.</param>
    /// <param name="difficulty">The difficulty level (easy, medium, hard).</param>
    /// <example>.memory 100 medium</example>
    [Cmd]
    [Aliases]
    public async Task Memory(long betAmount, string difficulty = "easy")
    {
        var currentBalance = await Service.GetUserBalanceAsync(ctx.User.Id, ctx.Guild.Id);
        if (betAmount > currentBalance || betAmount <= 0)
        {
            await ReplyAsync(Strings.MemoryInvalidBet(ctx.Guild.Id));
            return;
        }

        var difficultyConfigs = new Dictionary<string, (int length, int showTime, double multiplier)>
        {
            {
                "easy", (4, 3000, 1.5)
            },
            {
                "medium", (6, 2500, 2.0)
            },
            {
                "hard", (8, 2000, 3.0)
            }
        };

        if (!difficultyConfigs.ContainsKey(difficulty.ToLower()))
        {
            await ReplyAsync(Strings.MemoryInvalidDifficulty(ctx.Guild.Id));
            return;
        }

        var config = difficultyConfigs[difficulty.ToLower()];
        var rand = new Random();
        var sequence = GenerateMemorySequence(rand, config.length);
        var emojis = new[]
        {
            "🔴", "🟡", "🟢", "🔵", "🟣", "🟠", "⚫", "⚪"
        };

        var sequenceDisplay = string.Join(" ", sequence.Select(i => emojis[i]));

        var showEmbed = new EmbedBuilder()
            .WithTitle(Strings.MemoryTitle(ctx.Guild.Id))
            .WithDescription(Strings.MemorySequenceShow(ctx.Guild.Id, sequenceDisplay))
            .WithColor(Color.Blue)
            .AddField(Strings.MemoryDifficulty(ctx.Guild.Id, difficulty.ToUpper()), "", true)
            .AddField(Strings.MemoryShowTime(ctx.Guild.Id, $"{config.showTime / 1000} seconds"), "", true);

        var message = await ReplyAsync(embed: showEmbed.Build());

        await Task.Delay(config.showTime);

        var hideEmbed = new EmbedBuilder()
            .WithTitle(Strings.MemoryTitle(ctx.Guild.Id))
            .WithDescription(Strings.MemorySequenceHidden(ctx.Guild.Id))
            .WithColor(Color.Orange)
            .AddField(Strings.MemoryInstructions(ctx.Guild.Id), Strings.MemoryInstructionsText(ctx.Guild.Id));

        await message.ModifyAsync(m => m.Embed = hideEmbed.Build());

        var response = await NextMessageAsync(ctx.Channel.Id, ctx.User.Id);

        if (response == null)
        {
            await Service.AddUserBalanceAsync(ctx.User.Id, -betAmount, ctx.Guild.Id);
            await Service.AddTransactionAsync(ctx.User.Id, -betAmount, Strings.MemoryTransactionLost(ctx.Guild.Id),
                ctx.Guild.Id);

            var timeoutEmbed = new EmbedBuilder()
                .WithTitle(Strings.MemoryTitle(ctx.Guild.Id))
                .WithDescription(Strings.MemoryTimeout(ctx.Guild.Id, betAmount,
                    await Service.GetCurrencyEmote(ctx.Guild.Id)))
                .WithColor(Color.Red);

            await ReplyAsync(embed: timeoutEmbed.Build());
            return;
        }

        var userSequence = ParseMemorySequence(response, emojis);
        var isCorrect = userSequence.SequenceEqual(sequence);

        if (isCorrect)
        {
            var winAmount = (long)(betAmount * config.multiplier);
            await Service.AddUserBalanceAsync(ctx.User.Id, winAmount, ctx.Guild.Id);
            await Service.AddTransactionAsync(ctx.User.Id, winAmount, Strings.MemoryTransactionWon(ctx.Guild.Id),
                ctx.Guild.Id);

            var winEmbed = new EmbedBuilder()
                .WithTitle(Strings.MemoryTitle(ctx.Guild.Id))
                .WithDescription(Strings.MemoryCorrect(ctx.Guild.Id, winAmount,
                    await Service.GetCurrencyEmote(ctx.Guild.Id)))
                .WithColor(Color.Green);

            await ReplyAsync(embed: winEmbed.Build());
        }
        else
        {
            await Service.AddUserBalanceAsync(ctx.User.Id, -betAmount, ctx.Guild.Id);
            await Service.AddTransactionAsync(ctx.User.Id, -betAmount, Strings.MemoryTransactionLost(ctx.Guild.Id),
                ctx.Guild.Id);

            var loseEmbed = new EmbedBuilder()
                .WithTitle(Strings.MemoryTitle(ctx.Guild.Id))
                .WithDescription(Strings.MemoryIncorrect(ctx.Guild.Id, sequenceDisplay, betAmount,
                    await Service.GetCurrencyEmote(ctx.Guild.Id)))
                .WithColor(Color.Red);

            await ReplyAsync(embed: loseEmbed.Build());
        }
    }


    private static double GenerateCrashPoint(Random rand)
    {
        var roll = rand.NextDouble();
        return roll switch
        {
            < 0.5 => 1.0 + rand.NextDouble() * 0.5,
            < 0.8 => 1.5 + rand.NextDouble() * 1.5,
            < 0.95 => 3.0 + rand.NextDouble() * 2.0,
            _ => 5.0 + rand.NextDouble() * 5.0
        };
    }

    private static List<int> ParseKenoNumbers(string input)
    {
        var numbers = new HashSet<int>();
        var parts = input.Split([
            ' ', ',', ';'
        ], StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            if (int.TryParse(part.Trim(), out var number))
            {
                numbers.Add(number);
            }
        }

        return numbers.ToList();
    }

    private static HashSet<int> GenerateKenoNumbers(Random rand, int count)
    {
        var numbers = new HashSet<int>();
        while (numbers.Count < count)
        {
            numbers.Add(rand.Next(1, 81));
        }

        return numbers;
    }

    private static double CalculateKenoMultiplier(int picked, int matches)
    {
        return (picked, matches) switch
        {
            (1, 1) => 3.0,
            (2, 2) => 12.0,
            (3, 2) => 1.0,
            (3, 3) => 42.0,
            (4, 2) => 0.5,
            (4, 3) => 5.0,
            (4, 4) => 100.0,
            (5, 3) => 2.0,
            (5, 4) => 20.0,
            (5, 5) => 800.0,
            _ => 0.0
        };
    }

    private static List<int> SimulatePlinkoBall(Random rand, int rows)
    {
        var path = new List<int>
        {
            rows / 2
        };

        for (var i = 0; i < rows; i++)
        {
            var currentPos = path.Last();
            var direction = rand.Next(2) == 0 ? -1 : 1;
            var newPos = Math.Max(0, Math.Min(rows, currentPos + direction));
            path.Add(newPos);
        }

        return path;
    }

    private static double GetPlinkoMultiplier(int rows, int slot)
    {
        var center = rows / 2;
        var distance = Math.Abs(slot - center);

        return distance switch
        {
            0 => 0.5,
            1 => 1.2,
            2 => 2.0,
            3 => 5.0,
            4 => 10.0,
            _ => 0.1
        };
    }

    private static void DrawPlinkoBoard(SKCanvas canvas, int rows, List<int> path)
    {
        canvas.Clear(SKColors.White);

        using var pegPaint = new SKPaint
        {
            Color = SKColors.Gray, IsAntialias = true
        };
        using var pathPaint = new SKPaint
        {
            Color = SKColors.Red, IsAntialias = true, StrokeWidth = 3, Style = SKPaintStyle.Stroke
        };

        var width = canvas.LocalClipBounds.Width;
        var height = canvas.LocalClipBounds.Height;
        var pegSpacing = width / (rows + 1);
        var rowSpacing = height / (rows + 2);

        for (var row = 0; row <= rows; row++)
        {
            for (var col = 0; col <= row; col++)
            {
                var x = width / 2 + (col - row / 2.0f) * pegSpacing;
                var y = rowSpacing * (row + 1);
                canvas.DrawCircle(x, y, 5, pegPaint);
            }
        }

        for (var i = 0; i < path.Count - 1; i++)
        {
            var x1 = width / 2 + (path[i] - rows / 2.0f) * pegSpacing;
            var y1 = rowSpacing * (i + 1);
            var x2 = width / 2 + (path[i + 1] - rows / 2.0f) * pegSpacing;
            var y2 = rowSpacing * (i + 2);

            canvas.DrawLine(x1, y1, x2, y2, pathPaint);
        }
    }

    private static Dictionary<string, (string[] segments, int[] weights)> GetWheelConfigurations()
    {
        return new Dictionary<string, (string[] segments, int[] weights)>
        {
            {
                "classic", ([
                    "x0.5", "x1.2", "x2.0", "x0.8", "x1.5", "x0.1"
                ], [
                    3, 2, 1, 2, 1, 1
                ])
            },
            {
                "risky", ([
                    "x0.1", "x10.0", "x0.2", "x5.0", "x0.1", "x20.0"
                ], [
                    4, 1, 3, 1, 4, 1
                ])
            },
            {
                "balanced", ([
                    "x0.8", "x1.5", "x1.0", "x2.0", "x1.2", "x0.9"
                ], [
                    2, 2, 2, 1, 2, 1
                ])
            }
        };
    }

    private static void DrawWheelFortune(SKCanvas canvas, string[] segments, int winningIndex)
    {
        canvas.Clear(SKColors.White);

        var centerX = canvas.LocalClipBounds.MidX;
        var centerY = canvas.LocalClipBounds.MidY;
        var radius = Math.Min(centerX, centerY) - 10;

        var colors = new[]
        {
            SKColors.Red, SKColors.Blue, SKColors.Green, SKColors.Yellow, SKColors.Purple, SKColors.Orange
        };

        for (var i = 0; i < segments.Length; i++)
        {
            using var paint = new SKPaint
            {
                Style = SKPaintStyle.Fill,
                Color = i == winningIndex ? SKColors.Gold : colors[i % colors.Length],
                IsAntialias = true
            };

            var startAngle = i * 360f / segments.Length;
            var sweepAngle = 360f / segments.Length;

            canvas.DrawArc(new SKRect(centerX - radius, centerY - radius, centerX + radius, centerY + radius),
                startAngle, sweepAngle, true, paint);
        }

        using var textPaint = new SKPaint
        {
            Color = SKColors.Black, IsAntialias = true
        };
        using var font = new SKFont
        {
            Size = 16
        };

        for (var i = 0; i < segments.Length; i++)
        {
            var angle = (i + 0.5f) * 360f / segments.Length;
            var radians = angle * Math.PI / 180;
            var textX = centerX + radius * 0.7f * (float)Math.Cos(radians);
            var textY = centerY + radius * 0.7f * (float)Math.Sin(radians);

            canvas.DrawText(segments[i], textX, textY, SKTextAlign.Center, font, textPaint);
        }
    }

    private static Task<long> ComputeWheelBalanceChange(string segment, long betAmount)
    {
        if (segment.StartsWith("x"))
        {
            var multiplier = double.Parse(segment[1..]);
            return Task.FromResult((long)(betAmount * multiplier) - betAmount);
        }

        return Task.FromResult(-betAmount);
    }

    private static List<string> GenerateDucks()
    {
        return
        [
            "🦆",
            "🦆",
            "🦆",
            "🦆",
            "🦆"
        ];
    }

    private static List<string> GenerateDucks(int count)
    {
        var ducks = new List<string>();
        var duckEmojis = new[]
        {
            "🦆", "🐥", "🐤", "🐣", "🦢"
        };

        for (var i = 0; i < count; i++)
        {
            ducks.Add(duckEmojis[i % duckEmojis.Length]);
        }

        return ducks;
    }

    private static List<(int duckIndex, double time)> SimulateDuckRace(Random rand, List<string> ducks)
    {
        var results = new List<(int duckIndex, double time)>();

        for (var i = 0; i < ducks.Count; i++)
        {
            var time = 10.0 + rand.NextDouble() * 5.0;
            results.Add((i, time));
        }

        return results.OrderBy(r => r.time).ToList();
    }

    private static int[,] GenerateBingoCard(Random rand, int size)
    {
        var card = new int[size, size];
        var used = new HashSet<int>();

        for (var i = 0; i < size; i++)
        {
            for (var j = 0; j < size; j++)
            {
                int number;
                do
                {
                    number = rand.Next(1, 76);
                } while (used.Contains(number));

                used.Add(number);
                card[i, j] = number;
            }
        }

        return card;
    }

    private static List<int> CallBingoNumbers(Random rand, int count)
    {
        var numbers = new HashSet<int>();
        while (numbers.Count < count)
        {
            numbers.Add(rand.Next(1, 76));
        }

        return numbers.ToList();
    }

    private static bool CheckBingoWin(int[,] card, List<int> calledNumbers, bool forceWin)
    {
        if (forceWin) return true;

        var size = card.GetLength(0);
        var calledSet = new HashSet<int>(calledNumbers);

        for (var i = 0; i < size; i++)
        {
            var rowComplete = true;
            var colComplete = true;

            for (var j = 0; j < size; j++)
            {
                if (!calledSet.Contains(card[i, j])) rowComplete = false;
                if (!calledSet.Contains(card[j, i])) colComplete = false;
            }

            if (rowComplete || colComplete) return true;
        }

        var diag1Complete = true;
        var diag2Complete = true;
        for (var i = 0; i < size; i++)
        {
            if (!calledSet.Contains(card[i, i])) diag1Complete = false;
            if (!calledSet.Contains(card[i, size - 1 - i])) diag2Complete = false;
        }

        return diag1Complete || diag2Complete;
    }

    private static string FormatBingoCard(int[,] card, List<int> calledNumbers)
    {
        var size = card.GetLength(0);
        var calledSet = new HashSet<int>(calledNumbers);
        var result = "";

        for (var i = 0; i < size; i++)
        {
            for (var j = 0; j < size; j++)
            {
                var number = card[i, j];
                var marker = calledSet.Contains(number) ? "✅" : "❌";
                result += $"{number:D2}{marker} ";
            }

            result += "\n";
        }

        return result;
    }

    private static List<int> GenerateMemorySequence(Random rand, int length)
    {
        var sequence = new List<int>();
        for (var i = 0; i < length; i++)
        {
            sequence.Add(rand.Next(0, 8));
        }

        return sequence;
    }

    private static List<int> ParseMemorySequence(string input, string[] emojis)
    {
        var sequence = new List<int>();
        var emojiToIndex = emojis.Select((emoji, index) => new
            {
                emoji, index
            })
            .ToDictionary(x => x.emoji, x => x.index);

        foreach (var emoji in emojis)
        {
            var count = input.Count(c => c.ToString() == emoji);
            for (var i = 0; i < count; i++)
            {
                if (emojiToIndex.TryGetValue(emoji, out var index))
                {
                    sequence.Add(index);
                }
            }
        }

        return sequence;
    }

    /// <summary>
    ///     Check your current daily challenge.
    /// </summary>
    /// <example>.dailychallenge</example>
    [Cmd]
    [Aliases]
    public async Task DailyChallenge()
    {
        var challenge = await dailyChallengeService.GetCurrentChallenge(ctx.User.Id, ctx.Guild.Id);

        if (challenge == null)
        {
            await ReplyAsync(Strings.DailyChallengeAlreadyCompleted(ctx.Guild.Id));
            return;
        }

        var eb = new EmbedBuilder()
            .WithTitle(Strings.DailyChallengeTitle(ctx.Guild.Id))
            .WithColor(Color.Blue)
            .WithDescription(challenge.Description)
            .AddField(Strings.DailyChallengeProgress(ctx.Guild.Id, challenge.Progress, challenge.RequiredAmount), "",
                true)
            .AddField(
                Strings.DailyChallengeReward(ctx.Guild.Id, challenge.RewardAmount,
                    await Service.GetCurrencyEmote(ctx.Guild.Id)), "", true);

        if (challenge.Progress >= challenge.RequiredAmount)
        {
            eb.WithColor(Color.Green)
                .AddField(Strings.DailyChallengeStatus(ctx.Guild.Id), Strings.DailyChallengeReadyToClaim(ctx.Guild.Id));
        }

        await ReplyAsync(embed: eb.Build());
    }

    /// <summary>
    ///     Claim your completed daily challenge reward.
    /// </summary>
    /// <example>.claimchallenge</example>
    [Cmd]
    [Aliases]
    public async Task ClaimChallenge()
    {
        var challenge = await dailyChallengeService.GetCurrentChallenge(ctx.User.Id, ctx.Guild.Id);

        if (challenge == null)
        {
            await ReplyAsync(Strings.DailyChallengeNoneAvailable(ctx.Guild.Id));
            return;
        }

        if (challenge.Progress < challenge.RequiredAmount)
        {
            await ReplyAsync(Strings.DailyChallengeNotCompleted(ctx.Guild.Id));
            return;
        }

        var reward = await dailyChallengeService.CompleteChallenge(ctx.User.Id, ctx.Guild.Id, challenge.ChallengeType);

        if (reward > 0)
        {
            await Service.AddUserBalanceAsync(ctx.User.Id, reward, ctx.Guild.Id);
            await Service.AddTransactionAsync(ctx.User.Id, reward,
                Strings.DailyChallengeTransactionReward(ctx.Guild.Id), ctx.Guild.Id);

            await ReplyConfirmAsync(Strings.DailyChallengeClaimed(ctx.Guild.Id, reward,
                await Service.GetCurrencyEmote(ctx.Guild.Id)));
        }
        else
        {
            await ReplyErrorAsync(Strings.DailyChallengeAlreadyClaimed(ctx.Guild.Id));
        }
    }

    /// <summary>
    ///     View the daily challenge leaderboard.
    /// </summary>
    /// <example>.challengeleaderboard</example>
    [Cmd]
    [Aliases]
    public async Task ChallengeLeaderboard()
    {
        var leaderboard = await dailyChallengeService.GetLeaderboard(ctx.Guild.Id);

        if (leaderboard.Count == 0)
        {
            await ReplyAsync(Strings.DailyChallengeLeaderboardEmpty(ctx.Guild.Id));
            return;
        }

        var eb = new EmbedBuilder()
            .WithTitle(Strings.DailyChallengeLeaderboardTitle(ctx.Guild.Id))
            .WithColor(Color.Gold)
            .WithDescription(Strings.DailyChallengeLeaderboardDescription(ctx.Guild.Id));

        for (var i = 0; i < leaderboard.Count; i++)
        {
            var (userId, completionCount) = leaderboard[i];
            var user = await ctx.Guild.GetUserAsync(userId) ?? (IUser)await ctx.Client.GetUserAsync(userId);
            var position = i + 1;
            var medal = position switch
            {
                1 => "🥇",
                2 => "🥈",
                3 => "🥉",
                _ => $"{position}."
            };

            eb.AddField($"{medal} {user?.Username ?? "Unknown User"}",
                Strings.DailyChallengeLeaderboardEntry(ctx.Guild.Id, i + 1, user?.Username ?? "Unknown User",
                    completionCount), true);
        }

        await ReplyAsync(embed: eb.Build());
    }

    /// <summary>
    ///     Generate a weighted random segment index.
    /// </summary>
    /// <param name="segmentCount">Number of segments.</param>
    /// <param name="weights">Weight array for each segment.</param>
    /// <param name="rand">Random generator.</param>
    /// <returns>The selected segment index.</returns>
    private static int GenerateWeightedRandomSegment(int segmentCount, int[] weights, Random rand)
    {
        var totalWeight = weights.Sum();
        var randomValue = rand.Next(totalWeight);

        var currentWeight = 0;
        for (var i = 0; i < segmentCount; i++)
        {
            currentWeight += weights[i];
            if (randomValue < currentWeight)
                return i;
        }

        return segmentCount - 1; // Fallback
    }
}