using Mewdeko.Services.Strings;
using Embed = Discord.Embed;

namespace Mewdeko.Modules.Currency.Services;

/// <summary>
///     Service that handles the logic for a Blackjack game.
/// </summary>
/// <param name="strings">The localization service.</param>
public class BlackjackService(GeneratedBotStrings strings) : INService
{
    private static readonly string[] Suits = ["♠️", "♥️", "♦️", "♣️"];
    private static readonly string[] Ranks = ["A", "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K"];

    private static readonly Dictionary<string, int> RankValues = new()
    {
        {
            "A", 11
        },
        {
            "2", 2
        },
        {
            "3", 3
        },
        {
            "4", 4
        },
        {
            "5", 5
        },
        {
            "6", 6
        },
        {
            "7", 7
        },
        {
            "8", 8
        },
        {
            "9", 9
        },
        {
            "10", 10
        },
        {
            "J", 10
        },
        {
            "Q", 10
        },
        {
            "K", 10
        }
    };

    private static readonly Dictionary<IUser, BlackjackGame> Games = new();

    /// <summary>
    ///     Starts a new Blackjack game or joins an existing one.
    /// </summary>
    /// <param name="user">The player.</param>
    /// <param name="betAmount">The bet amount for the player.</param>
    /// <returns>The ongoing or newly created Blackjack game.</returns>
    public static BlackjackGame StartOrJoinGame(IUser user, long betAmount)
    {
        if (Games.TryGetValue(user, out var game))
        {
            game.AddPlayer(user, betAmount);
            return game;
        }

        game = new BlackjackGame();
        game.AddPlayer(user, betAmount);
        Games[user] = game;
        game.DealInitialCards();

        return game;
    }

    /// <summary>
    ///     Gets an ongoing Blackjack game for the specified user.
    /// </summary>
    /// <param name="user">The user</param>
    /// <returns>The ongoing Blackjack game.</returns>
    public static BlackjackGame GetGame(IUser user)
    {
        if (!Games.TryGetValue(user, out var game))
        {
            throw new InvalidOperationException("no_ongoing_game");
        }

        return game;
    }

    /// <summary>
    ///     Ends an ongoing Blackjack game for the specified user.
    /// </summary>
    /// <param name="user">The player.</param>
    public static void EndGame(IUser user)
    {
        if (!Games.Remove(user))
        {
            throw new InvalidOperationException("no_ongoing_game");
        }
    }

    /// <summary>
    ///     Handles the player's decision to stand and processes the dealer's turn.
    /// </summary>
    /// <param name="user">The player.</param>
    /// <param name="guildId">The guild ID for localization.</param>
    /// <param name="updateBalanceAsync">A function to update the player's balance.</param>
    /// <param name="addTransactionAsync">A function to add a transaction for the player.</param>
    /// <param name="currencyEmote">The currency emote for the game.</param>
    /// <returns>An embed representing the final state of the game.</returns>
    public async Task<Embed[]> HandleStandAsync(IUser user, ulong guildId, Func<ulong, long, Task> updateBalanceAsync,
        Func<ulong, long, string, Task> addTransactionAsync, string currencyEmote)
    {
        var embeds = new List<Embed>();
        var game = GetGame(user);

        while (BlackjackGame.CalculateHandTotal(game.DealerHand) < 17)
        {
            game.HitDealer();
        }

        var dealerTotal = BlackjackGame.CalculateHandTotal(game.DealerHand);
        var dealerBlackjack = BlackjackGame.IsBlackjack(game.DealerHand);

        var endEmbed = new EmbedBuilder()
            .WithTitle(strings.GameOver(user.Id))
            .WithErrorColor()
            .AddField(strings.DealerHand(guildId), string.Join(" ", game.DealerHand.Select(c => $"{c.Rank}{c.Suit}")))
            .AddField(strings.DealerTotal(guildId, dealerTotal), "_ _");

        embeds.Add(endEmbed.Build());

        foreach (var playerId in game.Players)
        {
            var playerHand = game.PlayerHands[playerId];
            var playerTotal = BlackjackGame.CalculateHandTotal(playerHand);
            var playerBlackjack = BlackjackGame.IsBlackjack(playerHand);
            var playerWon = playerBlackjack && !dealerBlackjack ||
                            playerTotal > dealerTotal && playerTotal <= 21 ||
                            dealerTotal > 21;
            var tie = playerTotal == dealerTotal && playerBlackjack == dealerBlackjack;
            var stake = game.Bets[playerId];

            var payout = playerWon
                ? playerBlackjack ? (long)(stake * 2.5) : stake * 2
                : tie
                    ? stake
                    : 0;

            var profit = payout - stake;

            await updateBalanceAsync(playerId.Id, payout);
            await addTransactionAsync(playerId.Id, payout,
                playerWon ? strings.WonBlackjack(guildId, profit) :
                tie ? strings.PushBlackjack(guildId) : strings.LostBlackjack(guildId, stake));

            var handEmbed = new EmbedBuilder()
                .WithOkColor()
                .AddField(strings.PlayerHand(guildId), string.Join(" ", playerHand.Select(c => $"{c.Rank}{c.Suit}")))
                .AddField(strings.PlayerTotal(guildId, playerTotal), "_ _")
                .AddField(strings.PlayerResult(guildId),
                    playerWon ? strings.Won(guildId) : tie ? strings.Push(guildId) : strings.Lost(guildId), true);

            embeds.Add(handEmbed.Build());
        }

        EndGame(user);

        return embeds.ToArray();
    }

    /// <summary>
    ///     Represents a game of Blackjack.
    /// </summary>
    public class BlackjackGame
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="BlackjackGame" /> class.
        /// </summary>
        public BlackjackGame()
        {
        }

        /// <summary>
        ///     Gets the user IDs of the players.
        /// </summary>
        public List<IUser> Players { get; } = [];

        /// <summary>
        ///     Gets the bet amounts for each player.
        /// </summary>
        public Dictionary<IUser, long> Bets { get; } = new();

        /// <summary>
        ///     Gets the hands of cards for each player.
        /// </summary>
        public Dictionary<IUser, List<(string Suit, string Rank)>> PlayerHands { get; } = new();

        /// <summary>
        ///     Gets the dealer's hand of cards.
        /// </summary>
        public List<(string Suit, string Rank)> DealerHand { get; } = [];

        /// <summary>
        ///     Adds a player to the game and places their bet.
        /// </summary>
        /// <param name="user">The player to add</param>
        /// <param name="betAmount">The bet amount for the player.</param>
        public void AddPlayer(IUser user, long betAmount)
        {
            if (Players.Count >= 5)
                throw new InvalidOperationException("blackjack_game_full");

            if (Players.Contains(user))
                throw new InvalidOperationException("blackjack_already_in_game");

            Players.Add(user);
            PlayerHands[user] = [];
            Bets[user] = betAmount;
        }

        /// <summary>
        ///     Checks if a hand is a Blackjack.
        /// </summary>
        /// <param name="hand">The hand to check.</param>
        /// <returns>True if the hand is a Blackjack, otherwise false.</returns>
        public static bool IsBlackjack(IEnumerable<(string Suit, string Rank)> hand)
        {
            return hand.Count() == 2 && CalculateHandTotal(hand) == 21;
        }

        /// <summary>
        ///     Deals the initial cards for the players and the dealer.
        /// </summary>
        public void DealInitialCards()
        {
            foreach (var playerId in Players)
            {
                HitPlayer(playerId);
                HitPlayer(playerId);
            }

            HitDealer();
            HitDealer();
        }

        /// <summary>
        ///     Draws a card for the player.
        /// </summary>
        /// <param name="user">The user</param>
        public void HitPlayer(IUser user)
        {
            PlayerHands[user].Add(DrawCard());
        }

        /// <summary>
        ///     Draws a card for the dealer.
        /// </summary>
        public void HitDealer()
        {
            DealerHand.Add(DrawCard());
        }

        /// <summary>
        ///     Creates an embed representing the current state of the game.
        /// </summary>
        /// <param name="title">The title of the embed.</param>
        /// <param name="guildId">The guild ID for localization.</param>
        /// <param name="strings">The strings service for localization.</param>
        /// <returns>An embed representing the current state of the game.</returns>
        public Embed[] CreateGameEmbed(string title, ulong guildId, GeneratedBotStrings strings)
        {
            var embedList = new List<Embed>();
            var embed = new EmbedBuilder()
                .WithTitle(title)
                .WithColor(Color.Blue)
                .AddField(strings.DealerHand(guildId), string.Join(" ", DealerHand.Select(c => $"{c.Rank}{c.Suit}")))
                .AddField(strings.DealerTotal(guildId, CalculateHandTotal(DealerHand)), "_ _");

            embedList.Add(embed.Build());

            foreach (var playerId in Players)
            {
                var playerHand = new EmbedBuilder()
                    .WithOkColor();

                playerHand.AddField(strings.PlayerHand(guildId),
                    string.Join(" ", PlayerHands[playerId].Select(c => $"{c.Rank}{c.Suit}")));
                playerHand.AddField(strings.PlayerTotal(guildId, CalculateHandTotal(PlayerHands[playerId])), "_ _");

                embedList.Add(playerHand.Build());
            }

            return embedList.ToArray();
        }

        private static (string Suit, string Rank) DrawCard()
        {
            var rand = new Random();
            return (Suits[rand.Next(Suits.Length)], Ranks[rand.Next(Ranks.Length)]);
        }

        /// <summary>
        ///     Calculates the total amount in a hand
        /// </summary>
        /// <param name="hand">The hand to calculate</param>
        /// <returns></returns>
        public static int CalculateHandTotal(IEnumerable<(string Suit, string Rank)> hand)
        {
            var total = hand.Sum(card => RankValues[card.Rank]);
            var aceCount = hand.Count(card => card.Rank == "A");

            while (total > 21 && aceCount > 0)
            {
                total -= 10;
                aceCount--;
            }

            return total;
        }
    }
}