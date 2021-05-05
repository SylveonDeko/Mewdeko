namespace NadekoBot.Core.Services.Database.Models
{
    /// <summary>
    /// Used to set stakes for gambling games which don't reward right away -
    /// like blackjack. If the bot is restarted mid game, users will get their funds back
    /// when the bot is back up.
    /// </summary>
    public class Stake : DbEntity
    {
        public ulong UserId { get; set; }
        public long Amount { get; set; }
        public string Source { get; set; }
    }
}
