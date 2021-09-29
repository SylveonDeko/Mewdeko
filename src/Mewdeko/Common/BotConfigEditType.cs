namespace Mewdeko.Common
{
    public enum BotConfigEditType
    {
        /// <summary>
        ///     The amount of currency awarded to the winner of the trivia game.
        ///     Default is 0.
        /// </summary>
        TriviaCurrencyReward,

        /// <summary>
        ///     Users can't start trivia games which have smaller win requirement than specified by this setting.
        ///     Default is 0.
        /// </summary>
        MinimumTriviaWinReq,

        /// <summary>
        ///     The amount of XP the user receives when they send a message (which is not too short).
        ///     Default is 3.
        /// </summary>
        XpPerMessage,

        /// <summary>
        ///     This value represents how often the user can receive XP from sending messages.
        ///     Default is 5.
        /// </summary>
        XpMinutesTimeout
    }
}