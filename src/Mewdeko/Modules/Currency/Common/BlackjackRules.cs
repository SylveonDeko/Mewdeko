using System.Text.Json;

namespace Mewdeko.Modules.Currency.Common;

/// <summary>
///     Where a blackjack table is in its life.
/// </summary>
public enum BlackjackPhase
{
    /// <summary>
    ///     The table is open and players can still sit down.
    /// </summary>
    Joining = 0,

    /// <summary>
    ///     The dealer shows an Ace and the players answer the insurance offer.
    /// </summary>
    Insurance = 1,

    /// <summary>
    ///     The players take their turns in seat order.
    /// </summary>
    Playing = 2,

    /// <summary>
    ///     The dealer has finished and every seat can be paid out.
    /// </summary>
    Settled = 3
}

/// <summary>
///     A single move a player can make.
/// </summary>
[Flags]
public enum BlackjackActions
{
    /// <summary>
    ///     No move is possible.
    /// </summary>
    None = 0,

    /// <summary>
    ///     Draw another card on the active hand.
    /// </summary>
    Hit = 1,

    /// <summary>
    ///     End the active hand.
    /// </summary>
    Stand = 2,

    /// <summary>
    ///     Double the bet on the active hand and take exactly one card.
    /// </summary>
    Double = 4,

    /// <summary>
    ///     Split a pair into two hands.
    /// </summary>
    Split = 8,

    /// <summary>
    ///     Give up the hand for half the stake.
    /// </summary>
    Surrender = 16,

    /// <summary>
    ///     Take the insurance side bet.
    /// </summary>
    Insurance = 32,

    /// <summary>
    ///     Refuse the insurance side bet.
    /// </summary>
    DeclineInsurance = 64,

    /// <summary>
    ///     Close the joining window and deal. Only the player who opened the table can do this.
    /// </summary>
    Deal = 128
}

/// <summary>
///     How a single hand ended.
/// </summary>
public enum BlackjackOutcome
{
    /// <summary>
    ///     The hand lost, by busting or by being beaten.
    /// </summary>
    Loss,

    /// <summary>
    ///     The stake is returned.
    /// </summary>
    Push,

    /// <summary>
    ///     The hand paid even money.
    /// </summary>
    Win,

    /// <summary>
    ///     A two card 21 on the first deal that paid 2.5 times the stake.
    /// </summary>
    Blackjack,

    /// <summary>
    ///     The player gave up the hand and got half the stake back.
    /// </summary>
    Surrender
}

/// <summary>
///     Why a player could not sit down.
/// </summary>
public enum BlackjackJoinResult
{
    /// <summary>
    ///     The player was seated.
    /// </summary>
    Joined,

    /// <summary>
    ///     The table already has the most players it takes.
    /// </summary>
    Full,

    /// <summary>
    ///     The player already sits at the table.
    /// </summary>
    AlreadySeated,

    /// <summary>
    ///     The joining window is over.
    /// </summary>
    Closed,

    /// <summary>
    ///     The bet is not a positive amount.
    /// </summary>
    InvalidBet
}

/// <summary>
///     A playing card drawn from an endless shoe.
/// </summary>
/// <param name="Rank">The rank from 1 (Ace) to 13 (King).</param>
/// <param name="Suit">The suit index from 0 to 3.</param>
public readonly record struct BlackjackCard(int Rank, int Suit)
{
    private static readonly string[] Suits = ["♠️", "♥️", "♦️", "♣️"];
    private static readonly string[] Ranks = ["A", "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K"];

    /// <summary>
    ///     Gets the hard value of the card, counting an Ace as 11.
    /// </summary>
    public int Value
    {
        get
        {
            return Rank == 1 ? 11 : Math.Min(Rank, 10);
        }
    }

    /// <summary>
    ///     Gets a value indicating whether the card is an Ace.
    /// </summary>
    public bool IsAce
    {
        get
        {
            return Rank == 1;
        }
    }

    /// <summary>
    ///     Gets the rank and suit as shown to the player.
    /// </summary>
    public string Label
    {
        get
        {
            return $"{Ranks[Rank - 1]}{Suits[Suit]}";
        }
    }

    /// <summary>
    ///     Encodes the card as its rank followed by the suit index.
    /// </summary>
    /// <returns>The stored form of the card.</returns>
    public string Encode()
    {
        return $"{Ranks[Rank - 1]}{Suit}";
    }

    /// <summary>
    ///     Reads a card from its stored form.
    /// </summary>
    /// <param name="text">The stored form.</param>
    /// <returns>The card.</returns>
    public static BlackjackCard Decode(string text)
    {
        var suit = text[^1] - '0';
        var rank = Array.IndexOf(Ranks, text[..^1]) + 1;

        if (rank < 1 || suit is < 0 or > 3)
            throw new FormatException($"Unreadable card '{text}'.");

        return new BlackjackCard(rank, suit);
    }
}

/// <summary>
///     One of a player's hands.
/// </summary>
public class BlackjackHand
{
    /// <summary>
    ///     Gets or sets the cards in the hand.
    /// </summary>
    public List<BlackjackCard> Cards { get; set; } = [];

    /// <summary>
    ///     Gets or sets the amount riding on the hand, doubled after a double down.
    /// </summary>
    public long Bet { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the hand takes no more moves.
    /// </summary>
    public bool Done { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the hand was doubled.
    /// </summary>
    public bool Doubled { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the hand came from a split.
    /// </summary>
    public bool FromSplit { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the hand was surrendered.
    /// </summary>
    public bool Surrendered { get; set; }

    /// <summary>
    ///     Gets the best total of the hand.
    /// </summary>
    public int Total
    {
        get
        {
            return BlackjackRules.Total(Cards);
        }
    }

    /// <summary>
    ///     Gets a value indicating whether the hand is a natural, a two card 21 that did not come from a split.
    /// </summary>
    public bool IsNatural
    {
        get
        {
            return !FromSplit && BlackjackRules.IsTwoCard21(Cards);
        }
    }

    /// <summary>
    ///     Gets a value indicating whether the hand went over 21.
    /// </summary>
    public bool IsBust
    {
        get
        {
            return Total > 21;
        }
    }

    internal BlackjackHand Clone()
    {
        return new BlackjackHand
        {
            Cards = [..Cards],
            Bet = Bet,
            Done = Done,
            Doubled = Doubled,
            FromSplit = FromSplit,
            Surrendered = Surrendered
        };
    }
}

/// <summary>
///     One player's place at a table.
/// </summary>
public class BlackjackSeat
{
    /// <summary>
    ///     Gets or sets the player.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    ///     Gets or sets the player's hands. A split makes a second one.
    /// </summary>
    public List<BlackjackHand> Hands { get; set; } = [];

    /// <summary>
    ///     Gets or sets the insurance stake, zero when none was taken.
    /// </summary>
    public long InsuranceStake { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the player took or declined the insurance offer.
    /// </summary>
    public bool InsuranceAnswered { get; set; }

    /// <summary>
    ///     Gets or sets the index of the hand being played.
    /// </summary>
    public int ActiveHand { get; set; }

    /// <summary>
    ///     Gets or sets how many moves the player has made on their hands, not counting insurance.
    /// </summary>
    public int Moves { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the player has split.
    /// </summary>
    public bool HasSplit { get; set; }

    /// <summary>
    ///     Gets the hand being played, or null when every hand is done.
    /// </summary>
    public BlackjackHand? Active
    {
        get
        {
            return ActiveHand >= 0 && ActiveHand < Hands.Count && !Hands[ActiveHand].Done
                ? Hands[ActiveHand]
                : null;
        }
    }

    /// <summary>
    ///     Gets a value indicating whether every hand is done.
    /// </summary>
    public bool Finished
    {
        get
        {
            return Hands.All(h => h.Done);
        }
    }

    internal BlackjackSeat Clone()
    {
        return new BlackjackSeat
        {
            UserId = UserId,
            Hands = Hands.Select(h => h.Clone()).ToList(),
            InsuranceStake = InsuranceStake,
            InsuranceAnswered = InsuranceAnswered,
            ActiveHand = ActiveHand,
            Moves = Moves,
            HasSplit = HasSplit
        };
    }
}

/// <summary>
///     Everything about a table that is stored between steps.
/// </summary>
public class BlackjackTable
{
    /// <summary>
    ///     The most players a table takes.
    /// </summary>
    public const int MaxSeats = 5;

    /// <summary>
    ///     Gets or sets the phase of the table.
    /// </summary>
    public BlackjackPhase Phase { get; set; } = BlackjackPhase.Joining;

    /// <summary>
    ///     Gets or sets the player who opened the table and can deal early.
    /// </summary>
    public ulong OpenerId { get; set; }

    /// <summary>
    ///     Gets the seats in turn order.
    /// </summary>
    public List<BlackjackSeat> Seats { get; set; } = [];

    /// <summary>
    ///     Gets the dealer's cards. The first is shown, the second stays hidden until the table settles.
    /// </summary>
    public List<BlackjackCard> Dealer { get; set; } = [];

    /// <summary>
    ///     Gets or sets the index of the seat whose turn it is while the table is being played.
    /// </summary>
    public int Turn { get; set; }

    /// <summary>
    ///     Gets the seat whose turn it is, or null outside the playing phase.
    /// </summary>
    public BlackjackSeat? CurrentSeat
    {
        get
        {
            return Phase == BlackjackPhase.Playing && Turn >= 0 && Turn < Seats.Count ? Seats[Turn] : null;
        }
    }

    /// <summary>
    ///     Finds the seat of a player.
    /// </summary>
    /// <param name="userId">The player.</param>
    /// <returns>The seat, or null when the player is not at the table.</returns>
    public BlackjackSeat? SeatOf(ulong userId)
    {
        return Seats.FirstOrDefault(s => s.UserId == userId);
    }

    /// <summary>
    ///     Copies the table so a move can be tried without touching the original.
    /// </summary>
    /// <returns>An independent copy.</returns>
    public BlackjackTable Clone()
    {
        return new BlackjackTable
        {
            Phase = Phase,
            OpenerId = OpenerId,
            Seats = Seats.Select(s => s.Clone()).ToList(),
            Dealer = [..Dealer],
            Turn = Turn
        };
    }
}

/// <summary>
///     The outcome of one move.
/// </summary>
/// <param name="Allowed">Whether the move was legal and was applied.</param>
/// <param name="Cost">The extra amount the player has to pay for the move.</param>
public readonly record struct BlackjackStep(bool Allowed, long Cost);

/// <summary>
///     How one hand was paid.
/// </summary>
/// <param name="Outcome">The result of the hand.</param>
/// <param name="Bet">The amount that was riding on the hand.</param>
/// <param name="Payout">The amount returned to the player, stake included.</param>
/// <param name="Total">The hand's final total.</param>
public readonly record struct BlackjackHandResult(BlackjackOutcome Outcome, long Bet, long Payout, int Total);

/// <summary>
///     How a finished seat pays.
/// </summary>
/// <param name="Hands">The result of each hand in play order.</param>
/// <param name="InsuranceStake">The insurance stake that was taken.</param>
/// <param name="InsurancePayout">The amount returned for the insurance, stake included.</param>
/// <param name="DealerTotal">The dealer's final total.</param>
/// <param name="DealerNatural">Whether the dealer had a two card 21.</param>
public record BlackjackSettlement(
    IReadOnlyList<BlackjackHandResult> Hands,
    long InsuranceStake,
    long InsurancePayout,
    int DealerTotal,
    bool DealerNatural)
{
    /// <summary>
    ///     Gets everything the player staked, hands and insurance together.
    /// </summary>
    public long TotalStake
    {
        get
        {
            return BlackjackRules.Clamp(Hands.Aggregate((Int128)0, (sum, h) => sum + h.Bet) + InsuranceStake);
        }
    }

    /// <summary>
    ///     Gets everything returned to the player, hands and insurance together.
    /// </summary>
    public long TotalPayout
    {
        get
        {
            return BlackjackRules.Clamp(Hands.Aggregate((Int128)0, (sum, h) => sum + h.Payout) + InsurancePayout);
        }
    }
}

/// <summary>
///     How one player's seat was paid.
/// </summary>
/// <param name="UserId">The player.</param>
/// <param name="Settlement">The result of the seat's hands and insurance.</param>
public record BlackjackSeatResult(ulong UserId, BlackjackSettlement Settlement);

/// <summary>
///     The rules of blackjack with no Discord or database code in them.
/// </summary>
public static class BlackjackRules
{
    /// <summary>
    ///     How long the table stays open for players to sit down.
    /// </summary>
    public static readonly TimeSpan JoinWindow = TimeSpan.FromSeconds(15);

    /// <summary>
    ///     How long every player has to answer the insurance offer.
    /// </summary>
    public static readonly TimeSpan InsuranceWindow = TimeSpan.FromSeconds(20);

    /// <summary>
    ///     How long a player has for their turn before their hands stand.
    /// </summary>
    public static readonly TimeSpan TurnWindow = TimeSpan.FromSeconds(60);

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    ///     Draws a random card from an endless shoe.
    /// </summary>
    /// <returns>The card.</returns>
    public static BlackjackCard DrawCard()
    {
        return new BlackjackCard(CurrencyRng.Next(1, 14), CurrencyRng.Next(4));
    }

    /// <summary>
    ///     Narrows a wide intermediate amount to a long without overflowing.
    /// </summary>
    /// <param name="amount">The amount.</param>
    /// <returns>The amount, capped at the largest long.</returns>
    public static long Clamp(Int128 amount)
    {
        return amount > long.MaxValue ? long.MaxValue : amount < 0 ? 0 : (long)amount;
    }

    /// <summary>
    ///     Gets the best total of a set of cards, counting Aces as 11 until that would bust.
    /// </summary>
    /// <param name="cards">The cards.</param>
    /// <returns>The total.</returns>
    public static int Total(IEnumerable<BlackjackCard> cards)
    {
        var list = cards as IReadOnlyCollection<BlackjackCard> ?? cards.ToList();
        var total = list.Sum(c => c.Value);
        var aces = list.Count(c => c.IsAce);

        while (total > 21 && aces > 0)
        {
            total -= 10;
            aces--;
        }

        return total;
    }

    /// <summary>
    ///     Checks for exactly two cards totalling 21.
    /// </summary>
    /// <param name="cards">The cards.</param>
    /// <returns><see langword="true" /> for a two card 21.</returns>
    public static bool IsTwoCard21(IReadOnlyCollection<BlackjackCard> cards)
    {
        return cards.Count == 2 && Total(cards) == 21;
    }

    /// <summary>
    ///     Checks whether two cards may be split, which is two of the same rank or any two ten value cards.
    /// </summary>
    /// <param name="first">The first card.</param>
    /// <param name="second">The second card.</param>
    /// <returns><see langword="true" /> when the pair can be split.</returns>
    public static bool CanSplit(BlackjackCard first, BlackjackCard second)
    {
        return first.Rank == second.Rank || (first.Value == 10 && second.Value == 10);
    }

    /// <summary>
    ///     Gets the insurance stake for a bet, half rounded down.
    /// </summary>
    /// <param name="bet">The bet on the hand.</param>
    /// <returns>The insurance stake.</returns>
    public static long InsuranceCost(long bet)
    {
        return bet / 2;
    }

    /// <summary>
    ///     Gets the total returned for a natural, 2.5 times the stake rounded down.
    /// </summary>
    /// <param name="bet">The stake.</param>
    /// <returns>The amount returned.</returns>
    public static long NaturalPayout(long bet)
    {
        return Clamp((Int128)bet * 5 / 2);
    }

    /// <summary>
    ///     Gets the total returned for a win, twice the stake.
    /// </summary>
    /// <param name="bet">The stake.</param>
    /// <returns>The amount returned.</returns>
    public static long WinPayout(long bet)
    {
        return Clamp((Int128)bet * 2);
    }

    /// <summary>
    ///     Gets the amount returned for a surrender, half the stake rounded down.
    /// </summary>
    /// <param name="bet">The stake.</param>
    /// <returns>The amount returned.</returns>
    public static long SurrenderPayout(long bet)
    {
        return bet / 2;
    }

    /// <summary>
    ///     Checks whether the dealer has to draw. The dealer stands on every 17.
    /// </summary>
    /// <param name="cards">The dealer's cards.</param>
    /// <returns><see langword="true" /> below 17.</returns>
    public static bool DealerMustDraw(IReadOnlyCollection<BlackjackCard> cards)
    {
        return Total(cards) < 17;
    }

    /// <summary>
    ///     Gets how long the current phase of a table lasts.
    /// </summary>
    /// <param name="phase">The phase.</param>
    /// <returns>The window for the phase.</returns>
    public static TimeSpan WindowOf(BlackjackPhase phase)
    {
        return phase switch
        {
            BlackjackPhase.Joining => JoinWindow,
            BlackjackPhase.Insurance => InsuranceWindow,
            _ => TurnWindow
        };
    }

    /// <summary>
    ///     Opens a table with its first player seated.
    /// </summary>
    /// <param name="userId">The player opening the table.</param>
    /// <param name="bet">The player's bet.</param>
    /// <returns>The new table.</returns>
    public static BlackjackTable Open(ulong userId, long bet)
    {
        var table = new BlackjackTable
        {
            OpenerId = userId
        };
        table.Seats.Add(NewSeat(userId, bet));

        return table;
    }

    /// <summary>
    ///     Seats a player at a table that is still open.
    /// </summary>
    /// <param name="table">The table, changed in place when the player is seated.</param>
    /// <param name="userId">The player.</param>
    /// <param name="bet">The player's bet.</param>
    /// <returns>Whether the player was seated, and if not why.</returns>
    public static BlackjackJoinResult Join(BlackjackTable table, ulong userId, long bet)
    {
        if (bet <= 0)
            return BlackjackJoinResult.InvalidBet;

        if (table.SeatOf(userId) is not null)
            return BlackjackJoinResult.AlreadySeated;

        if (table.Phase != BlackjackPhase.Joining)
            return BlackjackJoinResult.Closed;

        if (table.Seats.Count >= BlackjackTable.MaxSeats)
            return BlackjackJoinResult.Full;

        table.Seats.Add(NewSeat(userId, bet));
        return BlackjackJoinResult.Joined;
    }

    /// <summary>
    ///     Works out which moves a player may make right now.
    /// </summary>
    /// <param name="table">The table.</param>
    /// <param name="userId">The player.</param>
    /// <returns>The legal moves.</returns>
    public static BlackjackActions LegalActions(BlackjackTable table, ulong userId)
    {
        var seat = table.SeatOf(userId);

        if (seat is null)
            return BlackjackActions.None;

        switch (table.Phase)
        {
            case BlackjackPhase.Joining:
                return userId == table.OpenerId ? BlackjackActions.Deal : BlackjackActions.None;

            case BlackjackPhase.Insurance:
                if (seat.InsuranceAnswered)
                    return BlackjackActions.None;

                return InsuranceCost(seat.Hands[0].Bet) > 0
                    ? BlackjackActions.Insurance | BlackjackActions.DeclineInsurance
                    : BlackjackActions.DeclineInsurance;

            case BlackjackPhase.Playing:
                if (table.CurrentSeat != seat)
                    return BlackjackActions.None;

                break;

            default:
                return BlackjackActions.None;
        }

        var hand = seat.Active;

        if (hand is null)
            return BlackjackActions.None;

        var actions = BlackjackActions.Hit | BlackjackActions.Stand;
        var firstTwo = hand.Cards.Count == 2;

        if (firstTwo && hand.Bet <= long.MaxValue / 2)
            actions |= BlackjackActions.Double;

        if (firstTwo && !seat.HasSplit && seat.Hands.Count == 1 && CanSplit(hand.Cards[0], hand.Cards[1]))
            actions |= BlackjackActions.Split;

        if (firstTwo && seat.Moves == 0 && !seat.HasSplit && seat.Hands.Count == 1)
            actions |= BlackjackActions.Surrender;

        return actions;
    }

    /// <summary>
    ///     Works out which moves the buttons or reactions on the table message should offer: the moves of
    ///     whoever can act, or the insurance answers while any player still owes one.
    /// </summary>
    /// <param name="table">The table.</param>
    /// <returns>The moves to offer.</returns>
    public static BlackjackActions OfferedActions(BlackjackTable table)
    {
        switch (table.Phase)
        {
            case BlackjackPhase.Joining:
                return BlackjackActions.Deal;
            case BlackjackPhase.Insurance:
                return table.Seats.Aggregate(BlackjackActions.None, (all, s) => all | LegalActions(table, s.UserId));
            case BlackjackPhase.Playing:
                return table.CurrentSeat is { } seat ? LegalActions(table, seat.UserId) : BlackjackActions.None;
            default:
                return BlackjackActions.None;
        }
    }

    /// <summary>
    ///     Gets the players who can act right now.
    /// </summary>
    /// <param name="table">The table.</param>
    /// <returns>The players whose move or answer the table is waiting for.</returns>
    public static IReadOnlyList<ulong> WaitingFor(BlackjackTable table)
    {
        return table.Seats.Where(s => LegalActions(table, s.UserId) != BlackjackActions.None)
            .Select(s => s.UserId)
            .ToList();
    }

    /// <summary>
    ///     Applies a move by a player. Dealing, the insurance answers and the hand moves all go through here.
    /// </summary>
    /// <param name="table">The table, changed in place when the move is allowed.</param>
    /// <param name="userId">The player making the move.</param>
    /// <param name="action">The move.</param>
    /// <param name="draw">Supplies the next card.</param>
    /// <returns>Whether the move was allowed and what it costs.</returns>
    public static BlackjackStep Apply(BlackjackTable table, ulong userId, BlackjackActions action,
        Func<BlackjackCard> draw)
    {
        if (action == BlackjackActions.None || (action & (action - 1)) != 0 ||
            (LegalActions(table, userId) & action) == 0)
            return new BlackjackStep(false, 0);

        var seat = table.SeatOf(userId)!;

        switch (action)
        {
            case BlackjackActions.Deal:
                Deal(table, draw);
                return new BlackjackStep(true, 0);

            case BlackjackActions.Insurance:
                var stake = InsuranceCost(seat.Hands[0].Bet);
                seat.InsuranceStake = stake;
                seat.InsuranceAnswered = true;
                PeekWhenAnswered(table, draw);
                return new BlackjackStep(true, stake);

            case BlackjackActions.DeclineInsurance:
                seat.InsuranceAnswered = true;
                PeekWhenAnswered(table, draw);
                return new BlackjackStep(true, 0);
        }

        var hand = seat.Active!;
        seat.Moves++;

        switch (action)
        {
            case BlackjackActions.Hit:
                hand.Cards.Add(draw());
                hand.Done = hand.Total >= 21;
                AdvanceTurn(table, draw);
                return new BlackjackStep(true, 0);

            case BlackjackActions.Stand:
                hand.Done = true;
                AdvanceTurn(table, draw);
                return new BlackjackStep(true, 0);

            case BlackjackActions.Double:
                var extra = hand.Bet;
                hand.Bet += extra;
                hand.Doubled = true;
                hand.Cards.Add(draw());
                hand.Done = true;
                AdvanceTurn(table, draw);
                return new BlackjackStep(true, extra);

            case BlackjackActions.Split:
                var pairBet = hand.Bet;
                var aces = hand.Cards[0].IsAce;
                var second = new BlackjackHand
                {
                    Cards = [hand.Cards[1]],
                    Bet = pairBet,
                    FromSplit = true
                };
                hand.Cards.RemoveAt(1);
                hand.FromSplit = true;
                seat.HasSplit = true;
                seat.Hands.Add(second);
                hand.Cards.Add(draw());
                second.Cards.Add(draw());

                foreach (var h in seat.Hands)
                    h.Done = aces || h.Total >= 21;

                AdvanceTurn(table, draw);
                return new BlackjackStep(true, pairBet);

            case BlackjackActions.Surrender:
                hand.Surrendered = true;
                hand.Done = true;
                AdvanceTurn(table, draw);
                return new BlackjackStep(true, 0);
        }

        return new BlackjackStep(false, 0);
    }

    /// <summary>
    ///     Moves a table on when its current phase ran out of time: the joining window closes and the cards are
    ///     dealt, open insurance offers are declined, and the player whose turn it is stands on every hand.
    /// </summary>
    /// <param name="table">The table, changed in place.</param>
    /// <param name="draw">Supplies the next card.</param>
    /// <returns><see langword="true" /> when something changed.</returns>
    public static bool Expire(BlackjackTable table, Func<BlackjackCard> draw)
    {
        switch (table.Phase)
        {
            case BlackjackPhase.Joining:
                Deal(table, draw);
                return true;

            case BlackjackPhase.Insurance:
                foreach (var seat in table.Seats)
                    seat.InsuranceAnswered = true;

                Peek(table, draw);
                return true;

            case BlackjackPhase.Playing:
                var current = table.CurrentSeat;

                if (current is null)
                    return false;

                foreach (var hand in current.Hands)
                    hand.Done = true;

                AdvanceTurn(table, draw);
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    ///     Works out how a finished table pays each seat.
    /// </summary>
    /// <param name="table">The table.</param>
    /// <returns>The result of each seat in seat order.</returns>
    public static IReadOnlyList<BlackjackSeatResult> Settle(BlackjackTable table)
    {
        var dealerTotal = Total(table.Dealer);
        var dealerNatural = IsTwoCard21(table.Dealer);

        return table.Seats.Select(seat =>
        {
            var results = new List<BlackjackHandResult>();

            foreach (var hand in seat.Hands)
            {
                var total = hand.Total;
                BlackjackOutcome outcome;
                long payout;

                if (hand.Surrendered)
                {
                    outcome = BlackjackOutcome.Surrender;
                    payout = SurrenderPayout(hand.Bet);
                }
                else if (total > 21)
                {
                    outcome = BlackjackOutcome.Loss;
                    payout = 0;
                }
                else if (hand.IsNatural)
                {
                    outcome = dealerNatural ? BlackjackOutcome.Push : BlackjackOutcome.Blackjack;
                    payout = dealerNatural ? hand.Bet : NaturalPayout(hand.Bet);
                }
                else if (dealerNatural)
                {
                    outcome = BlackjackOutcome.Loss;
                    payout = 0;
                }
                else if (dealerTotal > 21 || total > dealerTotal)
                {
                    outcome = BlackjackOutcome.Win;
                    payout = WinPayout(hand.Bet);
                }
                else if (total == dealerTotal)
                {
                    outcome = BlackjackOutcome.Push;
                    payout = hand.Bet;
                }
                else
                {
                    outcome = BlackjackOutcome.Loss;
                    payout = 0;
                }

                results.Add(new BlackjackHandResult(outcome, hand.Bet, payout, total));
            }

            var insurancePayout = dealerNatural && seat.InsuranceStake > 0
                ? Clamp((Int128)seat.InsuranceStake * 3)
                : 0;

            return new BlackjackSeatResult(seat.UserId,
                new BlackjackSettlement(results, seat.InsuranceStake, insurancePayout, dealerTotal, dealerNatural));
        }).ToList();
    }

    /// <summary>
    ///     Writes the seats to the stored form.
    /// </summary>
    /// <param name="seats">The seats.</param>
    /// <returns>The stored form.</returns>
    public static string EncodeSeats(IEnumerable<BlackjackSeat> seats)
    {
        return JsonSerializer.Serialize(seats.Select(s => new StoredSeat
        {
            UserId = s.UserId,
            Hands = s.Hands.Select(h => new StoredHand
            {
                Cards = EncodeCards(h.Cards),
                Bet = h.Bet,
                Done = h.Done,
                Doubled = h.Doubled,
                FromSplit = h.FromSplit,
                Surrendered = h.Surrendered
            }).ToList(),
            InsuranceStake = s.InsuranceStake,
            InsuranceAnswered = s.InsuranceAnswered,
            ActiveHand = s.ActiveHand,
            Moves = s.Moves,
            HasSplit = s.HasSplit
        }), Json);
    }

    /// <summary>
    ///     Reads the seats from the stored form.
    /// </summary>
    /// <param name="text">The stored form.</param>
    /// <returns>The seats.</returns>
    public static List<BlackjackSeat> DecodeSeats(string text)
    {
        var stored = JsonSerializer.Deserialize<List<StoredSeat>>(text, Json) ?? [];

        return stored.Select(s => new BlackjackSeat
        {
            UserId = s.UserId,
            Hands = s.Hands.Select(h => new BlackjackHand
            {
                Cards = DecodeCards(h.Cards),
                Bet = h.Bet,
                Done = h.Done,
                Doubled = h.Doubled,
                FromSplit = h.FromSplit,
                Surrendered = h.Surrendered
            }).ToList(),
            InsuranceStake = s.InsuranceStake,
            InsuranceAnswered = s.InsuranceAnswered,
            ActiveHand = s.ActiveHand,
            Moves = s.Moves,
            HasSplit = s.HasSplit
        }).ToList();
    }

    /// <summary>
    ///     Writes cards to the stored form.
    /// </summary>
    /// <param name="cards">The cards.</param>
    /// <returns>The stored form.</returns>
    public static string EncodeCards(IEnumerable<BlackjackCard> cards)
    {
        return string.Join(' ', cards.Select(c => c.Encode()));
    }

    /// <summary>
    ///     Reads cards from the stored form.
    /// </summary>
    /// <param name="text">The stored form.</param>
    /// <returns>The cards.</returns>
    public static List<BlackjackCard> DecodeCards(string text)
    {
        return text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(BlackjackCard.Decode).ToList();
    }

    private static BlackjackSeat NewSeat(ulong userId, long bet)
    {
        return new BlackjackSeat
        {
            UserId = userId,
            Hands = [new BlackjackHand { Bet = bet }]
        };
    }

    private static void Deal(BlackjackTable table, Func<BlackjackCard> draw)
    {
        foreach (var seat in table.Seats)
        {
            seat.Hands[0].Cards.Add(draw());
            seat.Hands[0].Cards.Add(draw());
        }

        table.Dealer.Add(draw());
        table.Dealer.Add(draw());

        if (table.Dealer[0].IsAce)
        {
            foreach (var seat in table.Seats)
                seat.InsuranceAnswered = InsuranceCost(seat.Hands[0].Bet) <= 0;

            if (table.Seats.Any(s => !s.InsuranceAnswered))
            {
                table.Phase = BlackjackPhase.Insurance;
                return;
            }
        }

        Peek(table, draw);
    }

    private static void PeekWhenAnswered(BlackjackTable table, Func<BlackjackCard> draw)
    {
        if (table.Seats.All(s => s.InsuranceAnswered))
            Peek(table, draw);
    }

    private static void Peek(BlackjackTable table, Func<BlackjackCard> draw)
    {
        if (IsTwoCard21(table.Dealer))
        {
            Conclude(table, draw);
            return;
        }

        foreach (var hand in table.Seats.SelectMany(s => s.Hands).Where(h => h.IsNatural))
            hand.Done = true;

        table.Phase = BlackjackPhase.Playing;
        table.Turn = -1;
        AdvanceTurn(table, draw);
    }

    private static void AdvanceTurn(BlackjackTable table, Func<BlackjackCard> draw)
    {
        if (table.Turn >= 0 && table.Turn < table.Seats.Count)
        {
            var seat = table.Seats[table.Turn];
            var next = seat.Hands.FindIndex(h => !h.Done);

            if (next >= 0)
            {
                seat.ActiveHand = next;
                return;
            }
        }

        for (var i = table.Turn + 1; i < table.Seats.Count; i++)
        {
            var next = table.Seats[i].Hands.FindIndex(h => !h.Done);

            if (next < 0)
                continue;

            table.Turn = i;
            table.Seats[i].ActiveHand = next;
            return;
        }

        Conclude(table, draw);
    }

    private static void Conclude(BlackjackTable table, Func<BlackjackCard> draw)
    {
        table.Phase = BlackjackPhase.Settled;

        var hands = table.Seats.SelectMany(s => s.Hands).ToList();

        foreach (var hand in hands)
            hand.Done = true;

        var live = hands.Any(h => !h.Surrendered && !h.IsBust && !h.IsNatural);

        while (live && DealerMustDraw(table.Dealer))
            table.Dealer.Add(draw());
    }

    private sealed class StoredSeat
    {
        public ulong UserId { get; set; }

        public List<StoredHand> Hands { get; set; } = [];

        public long InsuranceStake { get; set; }

        public bool InsuranceAnswered { get; set; }

        public int ActiveHand { get; set; }

        public int Moves { get; set; }

        public bool HasSplit { get; set; }
    }

    private sealed class StoredHand
    {
        public string Cards { get; set; } = "";

        public long Bet { get; set; }

        public bool Done { get; set; }

        public bool Doubled { get; set; }

        public bool FromSplit { get; set; }

        public bool Surrendered { get; set; }
    }
}
