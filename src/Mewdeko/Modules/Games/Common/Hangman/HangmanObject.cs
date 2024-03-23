namespace Mewdeko.Modules.Games.Common.Hangman
{
    /// <summary>
    /// Represents a hangman word.
    /// </summary>
    public class HangmanObject
    {
        /// <summary>
        /// Gets or sets the word for the hangman game.
        /// </summary>
        public string Word { get; set; }

        /// <summary>
        /// Gets or sets the URL of an image associated with the word.
        /// </summary>
        public string ImageUrl { get; set; }

        /// <summary>
        /// Gets a formatted version of the word with hyperlinks to Wikipedia.
        /// </summary>
        /// <returns>A formatted string representing the word with hyperlinks to Wikipedia.</returns>
        public string GetWord()
        {
            var term = Word.ToTitleCase();
            return $"[{term}](https://en.wikipedia.org/wiki/{term.Replace(' ', '_')})";
        }
    }
}