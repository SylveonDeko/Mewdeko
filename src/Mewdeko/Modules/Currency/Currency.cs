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
/// <param name="interactive"></param>
public partial class Currency(InteractiveService interactive, BlackjackService blackjackService)
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
            .WithDescription(GetText("cash_balance", await Service.GetUserBalanceAsync(ctx.User.Id, ctx.Guild.Id),
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
            await ReplyAsync(GetText("coinflip_invalid_bet"));
            return;
        }

        var coinFlip = new Random().Next(2) == 0 ? "heads" : "tails";
        if (coinFlip.Equals(guess, StringComparison.OrdinalIgnoreCase))
        {
            await Service.AddUserBalanceAsync(ctx.User.Id, betAmount, ctx.Guild.Id);
            await Service.AddTransactionAsync(ctx.User.Id, betAmount, GetText("coinflip_won_transaction"),
                ctx.Guild.Id);
            await ReplyAsync(GetText("coinflip_won", coinFlip, betAmount,
                await Service.GetCurrencyEmote(ctx.Guild.Id)));
        }
        else
        {
            await Service.AddUserBalanceAsync(ctx.User.Id, -betAmount, ctx.Guild.Id);
            await Service.AddTransactionAsync(ctx.User.Id, -betAmount, GetText("coinflip_lost_transaction"),
                ctx.Guild.Id);
            await ReplyAsync(
                GetText("coinflip_lost", coinFlip, betAmount, await Service.GetCurrencyEmote(ctx.Guild.Id)));
        }
    }

    /// <summary>
    ///     Adds money to a users balance
    /// </summary>
    /// <param name="amount">The amount to add, dont go too crazy lol. Can be negative</param>
    /// <param name="reason">The reason you are doing this to this, person, thing, whatever</param>
    [Cmd]
    [Aliases]
    [CurrencyPermissions]
    public async Task ModifyBalance(IUser user, long amount, [Remainder] string? reason = null)
    {
        await Service.AddUserBalanceAsync(user.Id, amount, ctx.Guild.Id);
        await Service.AddTransactionAsync(user.Id, amount, reason, ctx.Guild.Id);
        await ReplyConfirmLocalizedAsync("user_balance_modified", user.Mention, amount, reason);
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
            await ctx.Channel.SendErrorAsync(GetText("daily_reward_not_set"), Config);
            return;
        }

        var minimumTimeBetweenClaims = TimeSpan.FromSeconds(cooldownSeconds);

        var recentTransactions = (await Service.GetTransactionsAsync(ctx.User.Id, ctx.Guild.Id))
            .Where(t => t.Description == GetText("daily_reward_transaction") &&
                        t.DateAdded > DateTime.UtcNow - minimumTimeBetweenClaims);

        if (recentTransactions.Any())
        {
            var nextAllowedClaimTime = recentTransactions.Max(t => t.DateAdded) + minimumTimeBetweenClaims;

            await ctx.Channel.SendErrorAsync(
                GetText("daily_reward_already_claimed", TimestampTag.FromDateTime(nextAllowedClaimTime.Value)),
                Config);
            return;
        }

        await Service.AddUserBalanceAsync(ctx.User.Id, rewardAmount, ctx.Guild.Id);
        await Service.AddTransactionAsync(ctx.User.Id, rewardAmount, GetText("daily_reward_transaction"), ctx.Guild.Id);
        await ctx.Channel.SendConfirmAsync(
            GetText("daily_reward_claimed", rewardAmount, await Service.GetCurrencyEmote(ctx.Guild.Id)));
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
            await ReplyAsync(GetText("highlow_won", currentNumber, nextNumber,
                await Service.GetCurrencyEmote(ctx.Guild.Id)));
        }
        else
        {
            await Service.AddUserBalanceAsync(ctx.User.Id, -100, ctx.Guild.Id);
            await ReplyAsync(GetText("highlow_lost", currentNumber, nextNumber,
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
                .WithTitle(GetText("leaderboard_title"))
                .WithDescription(GetText("leaderboard_description", users.Count, ctx.Guild.Name))
                .WithColor(Color.Blue);

            for (var i = index * 10; i < (index + 1) * 10 && i < users.Count; i++)
            {
                var user = await ctx.Guild.GetUserAsync(users[i].UserId) ??
                           (IUser)await ctx.Client.GetUserAsync(users[i].UserId);
                pageBuilder.AddField(GetText("leaderboard_user_entry", i + 1, user.Username),
                    GetText("leaderboard_balance_entry", users[i].Balance,
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
        await Service.SetReward(amount, time.Time.Seconds, ctx.Guild.Id);
        await ctx.Channel.SendConfirmAsync(GetText("setdaily_success", amount,
            await Service.GetCurrencyEmote(ctx.Guild.Id), time.Time.Seconds));
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
                GetText("spinwheel_no_balance", await Service.GetCurrencyEmote(ctx.Guild.Id)), Config);
            return;
        }

        if (betAmount > balance)
        {
            await ctx.Channel.SendErrorAsync(
                GetText("spinwheel_insufficient_balance", await Service.GetCurrencyEmote(ctx.Guild.Id)), Config);
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
            GetText(segments[winningSegment].Contains('-')
                ? "spinwheel_loss_transaction"
                : "spinwheel_win_transaction"), ctx.Guild.Id);

        var eb = new EmbedBuilder()
            .WithTitle(balanceChange > 0 ? GetText("spinwheel_win_title") : GetText("spinwheel_loss_title"))
            .WithDescription(GetText("spinwheel_result", segments[winningSegment], balanceChange,
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
        Task<long> ComputeBalanceChange(string segment, long betAmount)
        {
            long balanceChange = 0;

            if (segment.EndsWith("%"))
            {
                var percent = int.Parse(segment[1..^1]);
                var portion = (long)Math.Ceiling(betAmount * (percent / 100.0));
                balanceChange = segment.StartsWith("-") ? -portion : portion;
            }
            else
            {
                var val = int.Parse(segment.Replace("$", "").Replace("+", "").Replace("-", ""));
                balanceChange = segment.StartsWith("-") ? -val : val;
            }

            return Task.FromResult(balanceChange);
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
            await ReplyAsync(GetText("blackjack_invalid_bet"));
            return;
        }

        try
        {
            var game = BlackjackService.StartOrJoinGame(ctx.User, amount);
            var embed = game.CreateGameEmbed(GetText("blackjack_joined", ctx.User.Username));
            await ReplyAsync(embeds: embed);
        }
        catch (InvalidOperationException ex)
        {
            switch (ex.Message)
            {
                case "full":
                    await ReplyErrorLocalizedAsync("blackjack_game_full");
                    break;
                case "ingame":
                    await ReplyErrorLocalizedAsync("already_in_game");
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
            var embed = game.CreateGameEmbed(GetText("blackjack_hit", ctx.User.Username));

            if (BlackjackService.BlackjackGame.CalculateHandTotal(game.PlayerHands[ctx.User]) > 21)
            {
                await EndGame(game, false, GetText("blackjack_bust", ctx.User.Username));
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
            await ReplyAsync(GetText("blackjack_error", ex.Message));
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
            var embed = await blackjackService.HandleStandAsync(ctx.User,
                (userId, balanceChange) => Service.AddUserBalanceAsync(userId, balanceChange, ctx.Guild.Id),
                (userId, balanceChange, description) =>
                    Service.AddTransactionAsync(userId, balanceChange, description, ctx.Guild.Id),
                await Service.GetCurrencyEmote(ctx.Guild.Id));
            await ReplyAsync(embeds: embed);
        }
        catch (InvalidOperationException ex)
        {
            await ReplyAsync(GetText("blackjack_error", ex.Message));
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
            GetText(playerWon ? "blackjack_won_transaction" : "blackjack_lost_transaction"), ctx.Guild.Id);

        BlackjackService.EndGame(ctx.User);

        var embed = game.CreateGameEmbed(message);
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
            await ctx.Channel.SendErrorAsync(GetText("slot_minimum_bet", await Service.GetCurrencyEmote(ctx.Guild.Id)),
                Config);
            return;
        }

        var userBalance = await Service.GetUserBalanceAsync(ctx.User.Id, ctx.Guild.Id);
        if (bet > userBalance)
        {
            await ctx.Channel.SendErrorAsync(
                GetText("slot_insufficient_funds", await Service.GetCurrencyEmote(ctx.Guild.Id)), Config);
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
        await Service.AddTransactionAsync(ctx.User.Id, balanceChange, GetText("slot_transaction"), ctx.Guild.Id);

        var eb = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(GetText("slot_title"))
            .WithDescription(GetText("slot_result", result[0], result[1], result[2]))
            .AddField(GetText("slot_bet"), $"{bet} {await Service.GetCurrencyEmote(ctx.Guild.Id)}", true)
            .AddField(GetText("slot_winnings"), $"{winnings} {await Service.GetCurrencyEmote(ctx.Guild.Id)}", true)
            .AddField(GetText("slot_net_profit"), $"{balanceChange} {await Service.GetCurrencyEmote(ctx.Guild.Id)}",
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
            await ctx.Channel.SendErrorAsync(GetText("roulette_invalid_bet"), Config);
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
                    won = betType.ToLower() == color;
                    break;
                case "even":
                    won = result != 0 && result % 2 == 0;
                    break;
                case "odd":
                    won = result % 2 != 0;
                    break;
                default:
                    await ctx.Channel.SendErrorAsync(GetText("roulette_invalid_bet_type"), Config);
                    return;
            }

            multiplier = 1;
        }

        var winnings = won ? betAmount * (multiplier + 1) : 0;
        var profit = winnings - betAmount;

        await Service.AddUserBalanceAsync(ctx.User.Id, profit, ctx.Guild.Id);
        await Service.AddTransactionAsync(ctx.User.Id, profit, GetText("roulette_transaction"), ctx.Guild.Id);

        var eb = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(GetText("roulette_title"))
            .WithDescription(GetText("roulette_result", result, color))
            .AddField(GetText("roulette_bet"),
                GetText("roulette_bet_details", betAmount, await Service.GetCurrencyEmote(ctx.Guild.Id), betType), true)
            .AddField(GetText("roulette_outcome"), won ? GetText("roulette_won") : GetText("roulette_lost"), true)
            .AddField(GetText("roulette_profit"), $"{profit} {await Service.GetCurrencyEmote(ctx.Guild.Id)}", true);

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
    //         await ctx.Channel.SendErrorAsync(GetText("roll_invalid_bet"), Config);
    //         return;
    //     }
    //
    //     if (guess is < 2 or > 12)
    //     {
    //         await ctx.Channel.SendErrorAsync(GetText("roll_invalid_guess"), Config);
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
    //     await Service.AddTransactionAsync(ctx.User.Id, profit, GetText("roll_transaction"), ctx.Guild.Id);
    //
    //     var eb = new EmbedBuilder()
    //         .WithOkColor()
    //         .WithTitle(GetText("roll_title"))
    //         .WithDescription(GetText("roll_result", dice1, dice2, sum))
    //         .AddField(GetText("roll_your_guess"), guess, true)
    //         .AddField(GetText("roll_outcome"), won ? GetText("roll_won") : GetText("roll_lost"), true)
    //         .AddField(GetText("roll_profit"), $"{profit} {await Service.GetCurrencyEmote(ctx.Guild.Id)}", true);
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
    public async Task Transactions(IUser user = null)
    {
        user ??= ctx.User;

        var transactions = await Service.GetTransactionsAsync(user.Id, ctx.Guild.Id);
        transactions = transactions.OrderByDescending(x => x.DateAdded);
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
                .WithTitle(GetText("transactions_title"))
                .WithDescription(GetText("transactions_description", user.Username))
                .WithColor(Color.Blue);

            for (var i = index * 10; i < (index + 1) * 10 && i < transactions.Count(); i++)
            {
                pageBuilder.AddField(GetText("transactions_entry", i + 1, transactions.ElementAt(i).Description),
                    GetText("transactions_details", transactions.ElementAt(i).Amount,
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
                await ctx.Channel.SendErrorAsync(GetText("rps_invalid_bet"), Config);
                return;
            }
        }

        var validChoices = new[]
        {
            "rock", "paper", "scissors", "lizard", "spock"
        };
        if (!validChoices.Contains(choice.ToLower()))
        {
            await ctx.Channel.SendErrorAsync(GetText("rps_invalid_choice"), Config);
            return;
        }

        var rng = new Random();
        var botChoice = validChoices[rng.Next(validChoices.Length)];

        var (result, description) = DetermineWinner(choice.ToLower(), botChoice);

        var eb = new EmbedBuilder()
            .WithOkColor()
            .WithTitle(GetText("rps_embed_title"))
            .WithDescription(GetText("rps_embed_description", choice, botChoice))
            .AddField(GetText("rps_embed_result"), result.ToUpperInvariant(), true)
            .AddField(GetText("rps_embed_explanation"), description, true);

        if (betAmount != 0)
        {
            var profit = result switch
            {
                "win" => betAmount,
                "lose" => -betAmount,
                _ => 0L
            };

            await Service.AddUserBalanceAsync(ctx.User.Id, profit, ctx.Guild.Id);
            await Service.AddTransactionAsync(ctx.User.Id, profit, GetText("rps_transaction_description"),
                ctx.Guild.Id);

            eb.AddField(GetText("rps_embed_profit"), $"{profit} {await Service.GetCurrencyEmote(ctx.Guild.Id)}", true);
        }

        await ctx.Channel.SendMessageAsync(embed: eb.Build());
    }

    private (string result, string description) DetermineWinner(string playerChoice, string botChoice)
    {
        if (playerChoice == botChoice) return ("tie", GetText("rps_tie"));
        return (playerChoice, botChoice) switch
        {
            ("scissors", "paper") => ("win", GetText("rps_scissors_paper")),
            ("paper", "rock") => ("win", GetText("rps_paper_rock")),
            ("rock", "lizard") => ("win", GetText("rps_rock_lizard")),
            ("lizard", "spock") => ("win", GetText("rps_lizard_spock")),
            ("spock", "scissors") => ("win", GetText("rps_spock_scissors")),
            ("scissors", "lizard") => ("win", GetText("rps_scissors_lizard")),
            ("lizard", "paper") => ("win", GetText("rps_lizard_paper")),
            ("paper", "spock") => ("win", GetText("rps_paper_spock")),
            ("spock", "rock") => ("win", GetText("rps_spock_rock")),
            ("rock", "scissors") => ("win", GetText("rps_rock_scissors")),
            ("paper", "scissors") => ("lose", GetText("rps_scissors_paper")),
            ("rock", "paper") => ("lose", GetText("rps_paper_rock")),
            ("lizard", "rock") => ("lose", GetText("rps_rock_lizard")),
            ("spock", "lizard") => ("lose", GetText("rps_lizard_spock")),
            ("scissors", "spock") => ("lose", GetText("rps_spock_scissors")),
            ("lizard", "scissors") => ("lose", GetText("rps_scissors_lizard")),
            ("paper", "lizard") => ("lose", GetText("rps_lizard_paper")),
            ("spock", "paper") => ("lose", GetText("rps_paper_spock")),
            ("rock", "spock") => ("lose", GetText("rps_spock_   rock")),
            ("scissors", "rock") => ("lose", GetText("rps_rock_scissors")),
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

        using var textPaint = new SKPaint();
        textPaint.Color = SKColors.Black;
        textPaint.TextSize = 20;
        textPaint.IsAntialias = true;
        textPaint.TextAlign = SKTextAlign.Center;

        for (var i = 0; i < numSegments; i++)
        {
            var startAngle = i * 360 / numSegments - offsetAngle;
            var middleAngle = startAngle + 360 / numSegments / 2;
            var textPosition = new SKPoint(
                centerX + radius * 0.7f * (float)Math.Cos(DegreesToRadians(middleAngle)),
                centerY + radius * 0.7f * (float)Math.Sin(DegreesToRadians(middleAngle)) +
                textPaint.TextSize / 2);

            canvas.DrawText(segments[i], textPosition.X, textPosition.Y, textPaint);
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
}