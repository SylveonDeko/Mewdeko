using Discord;
using NadekoBot.Modules.Gambling.Common;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NadekoBot.Core.Modules.Gambling.Common.Blackjack
{
    public abstract class Player
    {
        public List<Deck.Card> Cards { get; } = new List<Deck.Card>();

        public int GetHandValue()
        {
            var val = GetRawHandValue();

            // while the hand value is greater than 21, for each ace you have in the deck
            // reduce the value by 10 until it drops below 22
            // (emulating the fact that ace is either a 1 or a 11)
            var i = Cards.Count(x => x.Number == 1);
            while (val > 21 && i-- > 0)
            {
                val -= 10;
            }
            return val;
        }

        public int GetRawHandValue()
        {
            return Cards.Sum(x => x.Number == 1 ? 11 : x.Number >= 10 ? 10 : x.Number);
        }
    }

    public class Dealer : Player
    {

    }

    public class User : Player
    {
        public enum UserState
        {
            Waiting,
            Stand,
            Bust,
            Blackjack,
            Won,
            Lost
        }

        public User(IUser user, long bet)
        {
            if (bet <= 0)
                throw new ArgumentOutOfRangeException(nameof(bet));

            this.Bet = bet;
            this.DiscordUser = user;
        }

        public UserState State { get; set; } = UserState.Waiting;
        public long Bet { get; set; }
        public IUser DiscordUser { get; }
        public bool Done => State != UserState.Waiting;
    }
}
