using Mewdeko.Modules.Currency.Common;

namespace Mewdeko.Tests;

/// <summary>
///     Tests for the blackjack rules: seating, dealing, turn order, moves, payouts and the timeouts.
/// </summary>
[TestFixture]
public class BlackjackRulesTests
{
    private const ulong P1 = 1;
    private const ulong P2 = 2;
    private const ulong P3 = 3;

    private static Func<BlackjackCard> Shoe(params string[] cards)
    {
        var queue = new Queue<BlackjackCard>(cards.Select(BlackjackCard.Decode));
        return queue.Dequeue;
    }

    private static BlackjackTable Dealt(long bet, params string[] cards)
    {
        var table = BlackjackRules.Open(P1, bet);
        BlackjackRules.Apply(table, P1, BlackjackActions.Deal, Shoe(cards));
        return table;
    }

    private static BlackjackStep Do(BlackjackTable table, BlackjackActions action, params string[] draws)
    {
        return BlackjackRules.Apply(table, P1, action, Shoe(draws));
    }

    private static BlackjackStep Do(BlackjackTable table, ulong user, BlackjackActions action, params string[] draws)
    {
        return BlackjackRules.Apply(table, user, action, Shoe(draws));
    }

    private static BlackjackActions Legal(BlackjackTable table)
    {
        return BlackjackRules.LegalActions(table, P1);
    }

    private static BlackjackSettlement Solo(BlackjackTable table)
    {
        return BlackjackRules.Settle(table)[0].Settlement;
    }

    private static BlackjackTable Seated(params (ulong User, long Bet)[] players)
    {
        var table = BlackjackRules.Open(players[0].User, players[0].Bet);

        foreach (var (user, bet) in players.Skip(1))
            BlackjackRules.Join(table, user, bet);

        return table;
    }

    /// <summary>
    ///     A first-deal 21 ends the game and pays two and a half times the stake.
    /// </summary>
    [Test]
    public void NaturalEndsAtOnceAndPaysTwoAndAHalf()
    {
        var state = Dealt(100, "A0", "K0", "50", "90");
        var settlement = Solo(state);

        Assert.That(state.Phase, Is.EqualTo(BlackjackPhase.Settled));
        Assert.That(settlement.Hands[0].Outcome, Is.EqualTo(BlackjackOutcome.Blackjack));
        Assert.That(settlement.Hands[0].Payout, Is.EqualTo(250));
    }

    /// <summary>
    ///     The natural payout rounds down on an odd stake.
    /// </summary>
    [Test]
    public void NaturalRoundsDownOnOddStake()
    {
        Assert.That(Solo(Dealt(25, "A0", "K0", "50", "90")).Hands[0].Payout, Is.EqualTo(62));
    }

    /// <summary>
    ///     A natural against a dealer natural is a push.
    /// </summary>
    [Test]
    public void NaturalAgainstDealerNaturalPushes()
    {
        var state = Dealt(100, "A0", "K0", "K1", "A1");
        var settlement = Solo(state);

        Assert.That(state.Phase, Is.EqualTo(BlackjackPhase.Settled));
        Assert.That(settlement.Hands[0].Outcome, Is.EqualTo(BlackjackOutcome.Push));
        Assert.That(settlement.Hands[0].Payout, Is.EqualTo(100));
    }

    /// <summary>
    ///     A dealer natural with a ten showing ends the game at once.
    /// </summary>
    [Test]
    public void DealerNaturalWithTenShowingEndsAtOnce()
    {
        var state = Dealt(100, "100", "90", "K1", "A1");
        var settlement = Solo(state);

        Assert.That(state.Phase, Is.EqualTo(BlackjackPhase.Settled));
        Assert.That(settlement.DealerNatural, Is.True);
        Assert.That(settlement.Hands[0].Outcome, Is.EqualTo(BlackjackOutcome.Loss));
        Assert.That(settlement.Hands[0].Payout, Is.EqualTo(0));
    }

    /// <summary>
    ///     A win pays twice the stake.
    /// </summary>
    [Test]
    public void WinPaysTwiceTheStake()
    {
        var state = Dealt(100, "100", "90", "K1", "71");
        var step = Do(state, BlackjackActions.Stand);
        var settlement = Solo(state);

        Assert.That(step.Allowed, Is.True);
        Assert.That(state.Phase, Is.EqualTo(BlackjackPhase.Settled));
        Assert.That(settlement.Hands[0].Outcome, Is.EqualTo(BlackjackOutcome.Win));
        Assert.That(settlement.Hands[0].Payout, Is.EqualTo(200));
    }

    /// <summary>
    ///     A lower total loses the stake.
    /// </summary>
    [Test]
    public void LossPaysNothing()
    {
        var state = Dealt(100, "100", "70", "K1", "91");
        Do(state, BlackjackActions.Stand);
        var settlement = Solo(state);

        Assert.That(settlement.Hands[0].Outcome, Is.EqualTo(BlackjackOutcome.Loss));
        Assert.That(settlement.Hands[0].Payout, Is.EqualTo(0));
    }

    /// <summary>
    ///     An equal total returns the stake.
    /// </summary>
    [Test]
    public void PushReturnsTheStake()
    {
        var state = Dealt(100, "100", "80", "K1", "81");
        Do(state, BlackjackActions.Stand);
        var settlement = Solo(state);

        Assert.That(settlement.Hands[0].Outcome, Is.EqualTo(BlackjackOutcome.Push));
        Assert.That(settlement.Hands[0].Payout, Is.EqualTo(100));
    }

    /// <summary>
    ///     A bust loses at once and the dealer does not draw.
    /// </summary>
    [Test]
    public void BustLosesAndDealerDoesNotDraw()
    {
        var state = Dealt(100, "100", "60", "K1", "51");
        Do(state, BlackjackActions.Hit, "K2");
        var settlement = Solo(state);

        Assert.That(state.Phase, Is.EqualTo(BlackjackPhase.Settled));
        Assert.That(state.Dealer, Has.Count.EqualTo(2));
        Assert.That(settlement.Hands[0].Payout, Is.EqualTo(0));
    }

    /// <summary>
    ///     The dealer stands on a soft 17 and draws below 17.
    /// </summary>
    [Test]
    public void DealerStandsOnAllSeventeensAndDrawsBelow()
    {
        var soft = Dealt(100, "100", "80", "61", "A1");
        Do(soft, BlackjackActions.Stand, "20");

        Assert.That(soft.Dealer, Has.Count.EqualTo(2));
        Assert.That(Solo(soft).Hands[0].Outcome, Is.EqualTo(BlackjackOutcome.Win));

        var low = Dealt(100, "100", "80", "K1", "61");
        Do(low, BlackjackActions.Stand, "50");

        Assert.That(low.Dealer, Has.Count.EqualTo(3));
        Assert.That(Solo(low).DealerTotal, Is.EqualTo(21));
    }

    /// <summary>
    ///     A double takes one card, doubles the stake and pays on the doubled amount.
    /// </summary>
    [Test]
    public void DoubleTakesOneCardAndPaysOnDoubleStake()
    {
        var state = Dealt(100, "50", "60", "K1", "71");
        var step = Do(state, BlackjackActions.Double, "K2");
        var settlement = Solo(state);

        Assert.That(step, Is.EqualTo(new BlackjackStep(true, 100)));
        Assert.That(state.Seats[0].Hands[0].Cards, Has.Count.EqualTo(3));
        Assert.That(settlement.Hands[0].Bet, Is.EqualTo(200));
        Assert.That(settlement.Hands[0].Payout, Is.EqualTo(400));
    }

    /// <summary>
    ///     Double is only offered on the first two cards.
    /// </summary>
    [Test]
    public void DoubleIsNotOfferedAfterAHit()
    {
        var state = Dealt(100, "20", "30", "K1", "71");
        Assert.That(Legal(state).HasFlag(BlackjackActions.Double), Is.True);

        Do(state, BlackjackActions.Hit, "20");

        Assert.That(Legal(state), Is.EqualTo(BlackjackActions.Hit | BlackjackActions.Stand));
        Assert.That(Do(state, BlackjackActions.Double, "20").Allowed, Is.False);
    }

    /// <summary>
    ///     A split plays the hands in order and pays each on its own.
    /// </summary>
    [Test]
    public void SplitPlaysHandsInOrderWithMixedOutcomes()
    {
        var state = Dealt(100, "80", "81", "K1", "91");
        var step = Do(state, BlackjackActions.Split, "30", "K2");
        var seat = state.Seats[0];

        Assert.That(step, Is.EqualTo(new BlackjackStep(true, 100)));
        Assert.That(seat.Hands, Has.Count.EqualTo(2));
        Assert.That(seat.ActiveHand, Is.EqualTo(0));

        Do(state, BlackjackActions.Hit, "K3");

        Assert.That(seat.Hands[0].Total, Is.EqualTo(21));
        Assert.That(seat.ActiveHand, Is.EqualTo(1));

        Do(state, BlackjackActions.Stand);
        var settlement = Solo(state);

        Assert.That(settlement.Hands[0].Payout, Is.EqualTo(200));
        Assert.That(settlement.Hands[1].Outcome, Is.EqualTo(BlackjackOutcome.Loss));
        Assert.That(settlement.TotalStake, Is.EqualTo(200));
        Assert.That(settlement.TotalPayout, Is.EqualTo(200));
    }

    /// <summary>
    ///     A split can happen once and doubling is still offered on a split hand.
    /// </summary>
    [Test]
    public void SplitHappensOnceAndDoubleStaysOffered()
    {
        var state = Dealt(100, "80", "81", "K1", "91");
        Do(state, BlackjackActions.Split, "80", "30");

        var legal = Legal(state);

        Assert.That(legal.HasFlag(BlackjackActions.Split), Is.False);
        Assert.That(legal.HasFlag(BlackjackActions.Surrender), Is.False);
        Assert.That(legal.HasFlag(BlackjackActions.Double), Is.True);
    }

    /// <summary>
    ///     Split aces get one card each and a 21 on them pays as a normal win.
    /// </summary>
    [Test]
    public void SplitAcesGetOneCardAndTwentyOnePaysAsWin()
    {
        var state = Dealt(100, "A0", "A1", "K1", "71");
        Do(state, BlackjackActions.Split, "K2", "90");
        var settlement = Solo(state);

        Assert.That(state.Phase, Is.EqualTo(BlackjackPhase.Settled));
        Assert.That(state.Seats[0].Hands.All(h => h.Cards.Count == 2), Is.True);
        Assert.That(settlement.Hands[0].Total, Is.EqualTo(21));
        Assert.That(settlement.Hands[0].Outcome, Is.EqualTo(BlackjackOutcome.Win));
        Assert.That(settlement.Hands[0].Payout, Is.EqualTo(200));
        Assert.That(settlement.Hands[1].Payout, Is.EqualTo(200));
    }

    /// <summary>
    ///     A split needs two cards of the same rank, or any two ten value cards.
    /// </summary>
    [Test]
    public void SplitNeedsTheSameRankOrTwoTenValueCards()
    {
        var state = Dealt(100, "90", "80", "K1", "71");

        Assert.That(Legal(state).HasFlag(BlackjackActions.Split), Is.False);
        Assert.That(Do(state, BlackjackActions.Split, "20", "30").Allowed, Is.False);
        Assert.That(Legal(Dealt(100, "90", "100", "K1", "71")).HasFlag(BlackjackActions.Split), Is.False);
        Assert.That(Legal(Dealt(100, "A0", "K0", "K1", "71")).HasFlag(BlackjackActions.Split), Is.False);
    }

    /// <summary>
    ///     Any two ten value cards split, and each half plays on without a second split.
    /// </summary>
    [TestCase("K0", "Q0")]
    [TestCase("100", "J0")]
    [TestCase("J0", "K0")]
    [TestCase("100", "100")]
    public void TenValueCardsSplit(string first, string second)
    {
        var state = Dealt(100, first, second, "K1", "71");

        Assert.That(Legal(state).HasFlag(BlackjackActions.Split), Is.True);
        Assert.That(Do(state, BlackjackActions.Split, "30", "40"), Is.EqualTo(new BlackjackStep(true, 100)));
        Assert.That(state.Seats[0].Hands, Has.Count.EqualTo(2));
        Assert.That(Legal(state).HasFlag(BlackjackActions.Split), Is.False);
        Assert.That(Legal(state).HasFlag(BlackjackActions.Double), Is.True);
    }

    /// <summary>
    ///     A 21 made on a split ten value hand pays as a normal win, not as a natural.
    /// </summary>
    [Test]
    public void TwentyOneAfterSplittingTensIsNotANatural()
    {
        var state = Dealt(100, "K0", "Q0", "K1", "71");
        Do(state, BlackjackActions.Split, "A1", "50");

        Assert.That(state.Seats[0].Hands[0].IsNatural, Is.False);
        Assert.That(state.Seats[0].Hands[0].Total, Is.EqualTo(21));
    }

    /// <summary>
    ///     The bets are open for fifteen seconds.
    /// </summary>
    [Test]
    public void JoinWindowIsFifteenSeconds()
    {
        Assert.That(BlackjackRules.JoinWindow, Is.EqualTo(TimeSpan.FromSeconds(15)));
        Assert.That(BlackjackRules.WindowOf(BlackjackPhase.Joining), Is.EqualTo(TimeSpan.FromSeconds(15)));
        Assert.That(BlackjackRules.WindowOf(BlackjackPhase.Insurance), Is.EqualTo(TimeSpan.FromSeconds(20)));
        Assert.That(BlackjackRules.WindowOf(BlackjackPhase.Playing), Is.EqualTo(TimeSpan.FromSeconds(60)));
    }

    /// <summary>
    ///     Surrender is not offered during the insurance step, only after the dealer has peeked.
    /// </summary>
    [Test]
    public void SurrenderIsRefusedDuringInsuranceAndAllowedAfterThePeek()
    {
        var state = Dealt(100, "100", "60", "A1", "51");

        Assert.That(state.Phase, Is.EqualTo(BlackjackPhase.Insurance));
        Assert.That(Do(state, BlackjackActions.Surrender).Allowed, Is.False);

        Do(state, BlackjackActions.DeclineInsurance);

        Assert.That(state.Phase, Is.EqualTo(BlackjackPhase.Playing));
        Assert.That(Legal(state).HasFlag(BlackjackActions.Surrender), Is.True);
    }

    /// <summary>
    ///     Surrender is not possible once the dealer has blackjack, with a ten or an Ace showing.
    /// </summary>
    [Test]
    public void SurrenderIsRefusedAfterDealerBlackjack()
    {
        var tenUp = Dealt(100, "100", "60", "K1", "A1");

        Assert.That(tenUp.Phase, Is.EqualTo(BlackjackPhase.Settled));
        Assert.That(Do(tenUp, BlackjackActions.Surrender).Allowed, Is.False);

        var aceUp = Dealt(100, "100", "60", "A1", "K1");
        Do(aceUp, BlackjackActions.DeclineInsurance);

        Assert.That(aceUp.Phase, Is.EqualTo(BlackjackPhase.Settled));
        Assert.That(Legal(aceUp), Is.EqualTo(BlackjackActions.None));
        Assert.That(Do(aceUp, BlackjackActions.Surrender).Allowed, Is.False);
    }

    /// <summary>
    ///     A seat holding a natural is offered insurance like every other seat.
    /// </summary>
    [Test]
    public void InsuranceIsOfferedToASeatHoldingANatural()
    {
        var table = Seated((P1, 100), (P2, 100));
        Do(table, P1, BlackjackActions.Deal, "A0", "K0", "100", "60", "A1", "51");

        Assert.That(table.Phase, Is.EqualTo(BlackjackPhase.Insurance));
        Assert.That(BlackjackRules.LegalActions(table, P1),
            Is.EqualTo(BlackjackActions.Insurance | BlackjackActions.DeclineInsurance));
        Assert.That(BlackjackRules.LegalActions(table, P2),
            Is.EqualTo(BlackjackActions.Insurance | BlackjackActions.DeclineInsurance));
        Assert.That(Do(table, P1, BlackjackActions.Insurance), Is.EqualTo(new BlackjackStep(true, 50)));
    }

    /// <summary>
    ///     Surrender returns half the stake rounded down and ends the game without the dealer drawing.
    /// </summary>
    [TestCase(100, 50)]
    [TestCase(25, 12)]
    [TestCase(1, 0)]
    public void SurrenderReturnsHalfRoundedDown(long bet, long expected)
    {
        var state = Dealt(bet, "100", "60", "K1", "51");
        var step = Do(state, BlackjackActions.Surrender);
        var settlement = Solo(state);

        Assert.That(step.Allowed, Is.True);
        Assert.That(state.Phase, Is.EqualTo(BlackjackPhase.Settled));
        Assert.That(state.Dealer, Has.Count.EqualTo(2));
        Assert.That(settlement.Hands[0].Outcome, Is.EqualTo(BlackjackOutcome.Surrender));
        Assert.That(settlement.Hands[0].Payout, Is.EqualTo(expected));
    }

    /// <summary>
    ///     Surrender is gone after the first move.
    /// </summary>
    [Test]
    public void SurrenderOnlyAsTheFirstAction()
    {
        var state = Dealt(100, "20", "30", "K1", "71");
        Assert.That(Legal(state), Is.EqualTo(
            BlackjackActions.Hit | BlackjackActions.Stand | BlackjackActions.Double | BlackjackActions.Surrender));

        Do(state, BlackjackActions.Hit, "20");

        Assert.That(Do(state, BlackjackActions.Surrender).Allowed, Is.False);
    }

    /// <summary>
    ///     A pair adds split to the first-move set.
    /// </summary>
    [Test]
    public void PairOffersEveryFirstMove()
    {
        var state = Dealt(100, "80", "81", "K1", "71");

        Assert.That(Legal(state), Is.EqualTo(BlackjackActions.Hit | BlackjackActions.Stand |
                                              BlackjackActions.Double | BlackjackActions.Split |
                                              BlackjackActions.Surrender));
    }

    /// <summary>
    ///     An Ace showing offers insurance before anything else.
    /// </summary>
    [Test]
    public void AceShowingOffersInsuranceFirst()
    {
        var state = Dealt(100, "100", "90", "A1", "51");

        Assert.That(state.Phase, Is.EqualTo(BlackjackPhase.Insurance));
        Assert.That(Legal(state), Is.EqualTo(BlackjackActions.Insurance | BlackjackActions.DeclineInsurance));
        Assert.That(Do(state, BlackjackActions.Hit, "20").Allowed, Is.False);
    }

    /// <summary>
    ///     Insurance on a stake of one costs nothing, so it is not offered.
    /// </summary>
    [Test]
    public void InsuranceIsSkippedWhenItWouldCostNothing()
    {
        Assert.That(Dealt(1, "100", "90", "A1", "51").Phase, Is.EqualTo(BlackjackPhase.Playing));
    }

    /// <summary>
    ///     Insurance pays two to one when the dealer has blackjack, and the main bet loses.
    /// </summary>
    [Test]
    public void InsuranceWinsWhenDealerHasBlackjack()
    {
        var state = Dealt(100, "100", "90", "A1", "K1");
        var step = Do(state, BlackjackActions.Insurance);
        var settlement = Solo(state);

        Assert.That(step, Is.EqualTo(new BlackjackStep(true, 50)));
        Assert.That(state.Phase, Is.EqualTo(BlackjackPhase.Settled));
        Assert.That(settlement.Hands[0].Payout, Is.EqualTo(0));
        Assert.That(settlement.InsurancePayout, Is.EqualTo(150));
        Assert.That(settlement.TotalStake, Is.EqualTo(150));
        Assert.That(settlement.TotalPayout, Is.EqualTo(150));
    }

    /// <summary>
    ///     Insurance is lost and play continues when the dealer has no blackjack.
    /// </summary>
    [Test]
    public void InsuranceLosesWhenDealerHasNoBlackjack()
    {
        var state = Dealt(100, "100", "90", "A1", "51");
        Do(state, BlackjackActions.Insurance);

        Assert.That(state.Phase, Is.EqualTo(BlackjackPhase.Playing));

        Do(state, BlackjackActions.Stand, "K2", "50");
        var settlement = Solo(state);

        Assert.That(settlement.InsuranceStake, Is.EqualTo(50));
        Assert.That(settlement.InsurancePayout, Is.EqualTo(0));
        Assert.That(settlement.Hands[0].Outcome, Is.EqualTo(BlackjackOutcome.Loss));
    }

    /// <summary>
    ///     Insurance with a player blackjack against a dealer blackjack pushes the hand and pays the insurance.
    /// </summary>
    [Test]
    public void InsuranceWithPlayerBlackjackPushesTheHand()
    {
        var state = Dealt(100, "A0", "K0", "A1", "K1");
        Do(state, BlackjackActions.Insurance);
        var settlement = Solo(state);

        Assert.That(state.Phase, Is.EqualTo(BlackjackPhase.Settled));
        Assert.That(settlement.Hands[0].Outcome, Is.EqualTo(BlackjackOutcome.Push));
        Assert.That(settlement.Hands[0].Payout, Is.EqualTo(100));
        Assert.That(settlement.InsurancePayout, Is.EqualTo(150));
        Assert.That(settlement.TotalPayout, Is.EqualTo(250));
    }

    /// <summary>
    ///     Declining insurance lets the dealer's blackjack take the hand.
    /// </summary>
    [Test]
    public void DecliningInsuranceLosesToDealerBlackjack()
    {
        var state = Dealt(100, "100", "90", "A1", "K1");
        Do(state, BlackjackActions.DeclineInsurance);
        var settlement = Solo(state);

        Assert.That(state.Phase, Is.EqualTo(BlackjackPhase.Settled));
        Assert.That(settlement.InsuranceStake, Is.EqualTo(0));
        Assert.That(settlement.Hands[0].Payout, Is.EqualTo(0));
    }

    /// <summary>
    ///     A quiet solo game declines open insurance, stands every hand and settles.
    /// </summary>
    [Test]
    public void ExpiryDeclinesInsuranceAndStandsEveryHand()
    {
        var offered = Dealt(100, "100", "90", "A1", "51");
        BlackjackRules.Expire(offered, Shoe());

        Assert.That(offered.Phase, Is.EqualTo(BlackjackPhase.Playing));

        BlackjackRules.Expire(offered, Shoe("K2", "50"));

        Assert.That(offered.Phase, Is.EqualTo(BlackjackPhase.Settled));
        Assert.That(Solo(offered).InsuranceStake, Is.EqualTo(0));

        var split = Dealt(100, "80", "81", "K1", "71");
        Do(split, BlackjackActions.Split, "30", "40");
        BlackjackRules.Expire(split, Shoe("20"));

        Assert.That(split.Phase, Is.EqualTo(BlackjackPhase.Settled));
        Assert.That(split.Seats[0].Hands.All(h => h.Done), Is.True);
    }

    /// <summary>
    ///     Payout maths caps at the largest long instead of wrapping.
    /// </summary>
    [Test]
    public void LargeStakesDoNotOverflow()
    {
        Assert.That(BlackjackRules.NaturalPayout(long.MaxValue), Is.EqualTo(long.MaxValue));
        Assert.That(BlackjackRules.WinPayout(long.MaxValue), Is.EqualTo(long.MaxValue));
        Assert.That(Solo(Dealt(long.MaxValue, "A0", "K0", "50", "90")).TotalPayout, Is.EqualTo(long.MaxValue));
        Assert.That(Legal(Dealt(long.MaxValue, "20", "30", "K1", "71")).HasFlag(BlackjackActions.Double), Is.False);
    }

    /// <summary>
    ///     Aces count as 11 until that would bust.
    /// </summary>
    [Test]
    public void TotalsCountAcesSoftThenHard()
    {
        Assert.That(BlackjackRules.Total(BlackjackRules.DecodeCards("A0 K1")), Is.EqualTo(21));
        Assert.That(BlackjackRules.Total(BlackjackRules.DecodeCards("A0 A1 90")), Is.EqualTo(21));
        Assert.That(BlackjackRules.Total(BlackjackRules.DecodeCards("A0 A1 K2")), Is.EqualTo(12));
    }

    /// <summary>
    ///     Cards and seats survive being stored and read back.
    /// </summary>
    [Test]
    public void StoredFormRoundTrips()
    {
        var state = Dealt(100, "80", "81", "K1", "71");
        Do(state, BlackjackActions.Split, "100", "A3");

        var restored = BlackjackRules.DecodeSeats(BlackjackRules.EncodeSeats(state.Seats));

        Assert.That(restored, Has.Count.EqualTo(1));
        Assert.That(restored[0].Hands[1].Cards, Is.EqualTo(state.Seats[0].Hands[1].Cards));
        Assert.That(restored[0].Hands[1].FromSplit, Is.True);
        Assert.That(restored[0].HasSplit, Is.True);
        Assert.That(restored[0].Hands[0].Bet, Is.EqualTo(100));
        Assert.That(BlackjackCard.Decode("103").Label, Is.EqualTo("10♣️"));
    }

    /// <summary>
    ///     Two players play in seat order and are paid against the same dealer hand.
    /// </summary>
    [Test]
    public void TwoPlayersPlayInSeatOrderAndSettleTogether()
    {
        var table = Seated((P1, 100), (P2, 200));
        Do(table, P1, BlackjackActions.Deal, "100", "90", "100", "80", "K1", "71");

        Assert.That(table.Phase, Is.EqualTo(BlackjackPhase.Playing));
        Assert.That(table.CurrentSeat!.UserId, Is.EqualTo(P1));
        Assert.That(BlackjackRules.WaitingFor(table), Is.EqualTo(new[] { P1 }));

        Do(table, P1, BlackjackActions.Stand);

        Assert.That(table.CurrentSeat!.UserId, Is.EqualTo(P2));

        Do(table, P2, BlackjackActions.Stand);
        var results = BlackjackRules.Settle(table);

        Assert.That(table.Phase, Is.EqualTo(BlackjackPhase.Settled));
        Assert.That(results[0].Settlement.Hands[0].Payout, Is.EqualTo(200));
        Assert.That(results[1].Settlement.Hands[0].Payout, Is.EqualTo(400));
        Assert.That(results[0].Settlement.DealerTotal, Is.EqualTo(results[1].Settlement.DealerTotal));
    }

    /// <summary>
    ///     Seats can end differently against the same dealer hand.
    /// </summary>
    [Test]
    public void SeatsCanEndWithDifferentOutcomes()
    {
        var table = Seated((P1, 100), (P2, 100), (P3, 100));
        Do(table, P1, BlackjackActions.Deal, "100", "90", "100", "70", "100", "80", "K1", "81");
        Do(table, P1, BlackjackActions.Stand);
        Do(table, P2, BlackjackActions.Stand);
        Do(table, P3, BlackjackActions.Stand);
        var results = BlackjackRules.Settle(table);

        Assert.That(results[0].Settlement.Hands[0].Outcome, Is.EqualTo(BlackjackOutcome.Win));
        Assert.That(results[1].Settlement.Hands[0].Outcome, Is.EqualTo(BlackjackOutcome.Loss));
        Assert.That(results[2].Settlement.Hands[0].Outcome, Is.EqualTo(BlackjackOutcome.Push));
    }

    /// <summary>
    ///     Only the player whose turn it is can act.
    /// </summary>
    [Test]
    public void OutOfTurnActionIsRefused()
    {
        var table = Seated((P1, 100), (P2, 100));
        Do(table, P1, BlackjackActions.Deal, "100", "90", "100", "80", "K1", "71");

        Assert.That(BlackjackRules.LegalActions(table, P2), Is.EqualTo(BlackjackActions.None));
        Assert.That(Do(table, P2, BlackjackActions.Hit, "20").Allowed, Is.False);
        Assert.That(Do(table, 99, BlackjackActions.Hit, "20").Allowed, Is.False);
        Assert.That(table.SeatOf(P2)!.Hands[0].Cards, Has.Count.EqualTo(2));
    }

    /// <summary>
    ///     A table takes five players, refuses a sixth and refuses the same player twice.
    /// </summary>
    [Test]
    public void SeatingRefusesASixthAndARepeat()
    {
        var table = BlackjackRules.Open(1, 100);

        Assert.That(BlackjackRules.Join(table, 1, 100), Is.EqualTo(BlackjackJoinResult.AlreadySeated));

        for (ulong user = 2; user <= 5; user++)
            Assert.That(BlackjackRules.Join(table, user, 100), Is.EqualTo(BlackjackJoinResult.Joined));

        Assert.That(BlackjackRules.Join(table, 6, 100), Is.EqualTo(BlackjackJoinResult.Full));
        Assert.That(BlackjackRules.Join(table, 7, 0), Is.EqualTo(BlackjackJoinResult.InvalidBet));
        Assert.That(table.Seats, Has.Count.EqualTo(5));
    }

    /// <summary>
    ///     Nobody can join once the cards are dealt, and only the opener can deal early.
    /// </summary>
    [Test]
    public void JoinAfterDealingIsRefusedAndOnlyOpenerDeals()
    {
        var table = Seated((P1, 100), (P2, 100));

        Assert.That(Do(table, P2, BlackjackActions.Deal, "20").Allowed, Is.False);
        Assert.That(table.Phase, Is.EqualTo(BlackjackPhase.Joining));

        Do(table, P1, BlackjackActions.Deal, "100", "90", "100", "80", "K1", "71");

        Assert.That(BlackjackRules.Join(table, P3, 100), Is.EqualTo(BlackjackJoinResult.Closed));
    }

    /// <summary>
    ///     When the joining window runs out the table deals, with a lone player playing alone.
    /// </summary>
    [Test]
    public void JoiningWindowExpiryDeals()
    {
        var table = BlackjackRules.Open(P1, 100);

        BlackjackRules.Expire(table, Shoe("100", "90", "K1", "71"));

        Assert.That(table.Phase, Is.EqualTo(BlackjackPhase.Playing));
        Assert.That(table.Seats[0].Hands[0].Cards, Has.Count.EqualTo(2));
    }

    /// <summary>
    ///     A turn that runs out stands that player's hands and moves to the next seat.
    /// </summary>
    [Test]
    public void TurnTimeoutAdvancesToTheNextSeat()
    {
        var table = Seated((P1, 100), (P2, 100));
        Do(table, P1, BlackjackActions.Deal, "100", "90", "100", "80", "K1", "71");

        BlackjackRules.Expire(table, Shoe());

        Assert.That(table.SeatOf(P1)!.Finished, Is.True);
        Assert.That(table.CurrentSeat!.UserId, Is.EqualTo(P2));

        BlackjackRules.Expire(table, Shoe());

        Assert.That(table.Phase, Is.EqualTo(BlackjackPhase.Settled));
    }

    /// <summary>
    ///     Every player answers the insurance offer separately, and unanswered offers count as declined.
    /// </summary>
    [Test]
    public void InsuranceWindowTakesMixedAnswers()
    {
        var table = Seated((P1, 100), (P2, 100), (P3, 100));
        Do(table, P1, BlackjackActions.Deal, "100", "90", "100", "80", "100", "70", "A1", "51");

        Assert.That(table.Phase, Is.EqualTo(BlackjackPhase.Insurance));
        Assert.That(BlackjackRules.WaitingFor(table), Is.EqualTo(new[] { P1, P2, P3 }));

        Assert.That(Do(table, P3, BlackjackActions.Insurance), Is.EqualTo(new BlackjackStep(true, 50)));
        Do(table, P1, BlackjackActions.DeclineInsurance);

        Assert.That(table.Phase, Is.EqualTo(BlackjackPhase.Insurance));
        Assert.That(Do(table, P1, BlackjackActions.Insurance).Allowed, Is.False);

        BlackjackRules.Expire(table, Shoe());

        Assert.That(table.Phase, Is.EqualTo(BlackjackPhase.Playing));
        Assert.That(table.SeatOf(P3)!.InsuranceStake, Is.EqualTo(50));
        Assert.That(table.SeatOf(P2)!.InsuranceStake, Is.EqualTo(0));
    }

    /// <summary>
    ///     A dealer blackjack settles every seat at once, paying insurance and pushing a player blackjack.
    /// </summary>
    [Test]
    public void DealerBlackjackSettlesEverySeat()
    {
        var table = Seated((P1, 100), (P2, 100), (P3, 100));
        Do(table, P1, BlackjackActions.Deal, "100", "90", "A0", "K0", "100", "80", "A1", "K1");
        Do(table, P1, BlackjackActions.Insurance);
        Do(table, P2, BlackjackActions.DeclineInsurance);
        Do(table, P3, BlackjackActions.DeclineInsurance);
        var results = BlackjackRules.Settle(table);

        Assert.That(table.Phase, Is.EqualTo(BlackjackPhase.Settled));
        Assert.That(results[0].Settlement.TotalPayout, Is.EqualTo(150));
        Assert.That(results[1].Settlement.Hands[0].Outcome, Is.EqualTo(BlackjackOutcome.Push));
        Assert.That(results[2].Settlement.Hands[0].Outcome, Is.EqualTo(BlackjackOutcome.Loss));
    }

    /// <summary>
    ///     A natural is skipped in turn order and paid when the table settles.
    /// </summary>
    [Test]
    public void NaturalIsSkippedInTurnOrder()
    {
        var table = Seated((P1, 100), (P2, 100));
        Do(table, P1, BlackjackActions.Deal, "A0", "K0", "100", "90", "K1", "71");

        Assert.That(table.CurrentSeat!.UserId, Is.EqualTo(P2));

        Do(table, P2, BlackjackActions.Stand);
        var results = BlackjackRules.Settle(table);

        Assert.That(results[0].Settlement.Hands[0].Outcome, Is.EqualTo(BlackjackOutcome.Blackjack));
        Assert.That(results[0].Settlement.Hands[0].Payout, Is.EqualTo(250));
        Assert.That(results[1].Settlement.Hands[0].Payout, Is.EqualTo(200));
    }

    /// <summary>
    ///     A split and a double each play inside a multi-player round without disturbing the other seat.
    /// </summary>
    [Test]
    public void SplitAndDoubleInsideAMultiPlayerRound()
    {
        var table = Seated((P1, 100), (P2, 100));
        Do(table, P1, BlackjackActions.Deal, "80", "81", "50", "60", "K1", "91");

        Assert.That(Do(table, P1, BlackjackActions.Split, "30", "K2"), Is.EqualTo(new BlackjackStep(true, 100)));
        Do(table, P1, BlackjackActions.Stand);

        Assert.That(table.CurrentSeat!.UserId, Is.EqualTo(P1));

        Do(table, P1, BlackjackActions.Stand);

        Assert.That(table.CurrentSeat!.UserId, Is.EqualTo(P2));
        Assert.That(Do(table, P2, BlackjackActions.Double, "K3"), Is.EqualTo(new BlackjackStep(true, 100)));

        var results = BlackjackRules.Settle(table);

        Assert.That(table.Phase, Is.EqualTo(BlackjackPhase.Settled));
        Assert.That(results[0].Settlement.Hands, Has.Count.EqualTo(2));
        Assert.That(results[0].Settlement.Hands[0].Outcome, Is.EqualTo(BlackjackOutcome.Loss));
        Assert.That(results[1].Settlement.Hands[0].Bet, Is.EqualTo(200));
        Assert.That(results[1].Settlement.Hands[0].Payout, Is.EqualTo(400));
    }

    /// <summary>
    ///     A table stored mid-turn comes back exactly as it was and play carries on.
    /// </summary>
    [Test]
    public void TableRecoversMidTurnAfterARestart()
    {
        var table = Seated((P1, 100), (P2, 100));
        Do(table, P1, BlackjackActions.Deal, "100", "50", "100", "80", "K1", "71");
        Do(table, P1, BlackjackActions.Hit, "20");

        var restored = new BlackjackTable
        {
            Phase = table.Phase,
            OpenerId = table.OpenerId,
            Turn = table.Turn,
            Dealer = BlackjackRules.DecodeCards(BlackjackRules.EncodeCards(table.Dealer)),
            Seats = BlackjackRules.DecodeSeats(BlackjackRules.EncodeSeats(table.Seats))
        };

        Assert.That(restored.CurrentSeat!.UserId, Is.EqualTo(P1));
        Assert.That(restored.CurrentSeat.Hands[0].Cards, Has.Count.EqualTo(3));
        Assert.That(BlackjackRules.LegalActions(restored, P1), Is.EqualTo(BlackjackActions.Hit | BlackjackActions.Stand));

        BlackjackRules.Expire(restored, Shoe());

        Assert.That(restored.CurrentSeat!.UserId, Is.EqualTo(P2));
    }
}
