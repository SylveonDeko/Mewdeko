using NadekoBot.Extensions;

namespace NadekoBot.Modules.Games.Common.Hangman
{
    public class HangmanObject
    {
        public string Word { get; set; }
        public string ImageUrl { get; set; }

        public string GetWord()
        {
            var term = Word.ToTitleCase();

            return $"[{term}](https://en.wikipedia.org/wiki/{term.Replace(' ', '_')})";
        }
    }
}
