using System.Threading;
using System.Threading.Tasks;

namespace Mewdeko.Modules.Gambling.Common;

public class RollDuelGame
{
    public enum Reason
    {
        Normal,
        NoFunds,
        Timeout
    }

    public enum State
    {
        Waiting,
        Running,
        Ended
    }

    private readonly ulong botId;

    private readonly ICurrencyService cs;
    private readonly SemaphoreSlim locker = new(1, 1);
    private readonly MewdekoRandom rng = new();

    private readonly Timer timeoutTimer;

    public RollDuelGame(ICurrencyService cs, ulong botId, ulong p1, ulong p2, long amount)
    {
        P1 = p1;
        P2 = p2;
        this.botId = botId;
        Amount = amount;
        this.cs = cs;

        timeoutTimer = new Timer(async delegate
        {
            await locker.WaitAsync().ConfigureAwait(false);
            try
            {
                if (CurrentState != State.Waiting)
                    return;
                CurrentState = State.Ended;
                await OnEnded.Invoke(this, Reason.Timeout).ConfigureAwait(false);
            }
            catch
            {
                // ignored
            }
            finally
            {
                locker.Release();
            }
        }, null, TimeSpan.FromSeconds(15), TimeSpan.FromMilliseconds(-1));
    }

    public ulong P1 { get; }
    public ulong P2 { get; }

    public long Amount { get; }

    public List<(int, int)> Rolls { get; } = new();
    public State CurrentState { get; private set; }
    public ulong Winner { get; private set; }

    public event Func<RollDuelGame, Task> OnGameTick;
    public event Func<RollDuelGame, Reason, Task> OnEnded;

    public async Task StartGame()
    {
        await locker.WaitAsync().ConfigureAwait(false);
        try
        {
            if (CurrentState != State.Waiting)
                return;
            timeoutTimer.Change(Timeout.Infinite, Timeout.Infinite);
            CurrentState = State.Running;
        }
        finally
        {
            locker.Release();
        }

        if (!await cs.RemoveAsync(P1, "Roll Duel", Amount).ConfigureAwait(false))
        {
            await OnEnded.Invoke(this, Reason.NoFunds).ConfigureAwait(false);
            CurrentState = State.Ended;
            return;
        }

        if (!await cs.RemoveAsync(P2, "Roll Duel", Amount).ConfigureAwait(false))
        {
            await cs.AddAsync(P1, "Roll Duel - refund", Amount).ConfigureAwait(false);
            await OnEnded.Invoke(this, Reason.NoFunds).ConfigureAwait(false);
            CurrentState = State.Ended;
            return;
        }

        do
        {
            var n1 = rng.Next(0, 5);
            var n2 = rng.Next(0, 5);
            Rolls.Add((n1, n2));
            if (n1 != n2)
            {
                Winner = n1 > n2 ? P1 : P2;
                var won = (long)(Amount * 2 * 0.98f);
                await cs.AddAsync(Winner, "Roll Duel win", won)
                    .ConfigureAwait(false);

                await cs.AddAsync(botId, "Roll Duel fee", (Amount * 2) - won)
                    .ConfigureAwait(false);
            }

            try
            {
                await OnGameTick(this).ConfigureAwait(false);
            }
            catch
            {
                // ignored
            }

            await Task.Delay(2500).ConfigureAwait(false);
            if (n1 != n2)
                break;
        } while (true);

        CurrentState = State.Ended;
        await OnEnded(this, Reason.Normal).ConfigureAwait(false);
    }
}

public struct RollDuelChallenge
{
    public ulong Player1 { get; set; }
    public ulong Player2 { get; set; }
}