using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.Utility.Services;

namespace Mewdeko.Modules.Utility;

public partial class Utility
{
    /// <summary>
    ///     Commands that rewrite text in decorative styles.
    /// </summary>
    [Group]
    public class TextStyleCommands : MewdekoSubmodule
    {
        /// <summary>
        ///     Converts text to script letters.
        /// </summary>
        /// <param name="input">The text to convert.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        public Task Fancy([Remainder] string input)
        {
            return SendStyled(TextStyleServices.Fancy(input));
        }

        /// <summary>
        ///     Converts text to bold script letters.
        /// </summary>
        /// <param name="input">The text to convert.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        public Task BoldFancy([Remainder] string input)
        {
            return SendStyled(TextStyleServices.BoldFancy(input));
        }

        /// <summary>
        ///     Converts text to fraktur letters.
        /// </summary>
        /// <param name="input">The text to convert.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        public Task Fraktur([Remainder] string input)
        {
            return SendStyled(TextStyleServices.Fraktur(input));
        }

        /// <summary>
        ///     Converts text to bold fraktur letters.
        /// </summary>
        /// <param name="input">The text to convert.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        public Task BoldFraktur([Remainder] string input)
        {
            return SendStyled(TextStyleServices.BoldFraktur(input));
        }

        /// <summary>
        ///     Converts text to double-struck letters.
        /// </summary>
        /// <param name="input">The text to convert.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        public Task DoubleStruck([Remainder] string input)
        {
            return SendStyled(TextStyleServices.DoubleStruck(input));
        }

        /// <summary>
        ///     Converts text to small capital letters.
        /// </summary>
        /// <param name="input">The text to convert.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        public Task SmallCaps([Remainder] string input)
        {
            return SendStyled(TextStyleServices.SmallCaps(input));
        }

        /// <summary>
        ///     Converts text to full-width letters.
        /// </summary>
        /// <param name="input">The text to convert.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        public Task Aesthetic([Remainder] string input)
        {
            return SendStyled(TextStyleServices.Aesthetic(input));
        }

        /// <summary>
        ///     Puts a clap between each word.
        /// </summary>
        /// <param name="input">The text to convert.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        public Task Clap([Remainder] string input)
        {
            return SendStyled(TextStyleServices.Clap(input));
        }

        /// <summary>
        ///     Puts a space between each letter.
        /// </summary>
        /// <param name="input">The text to convert.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        public Task SpaceOut([Remainder] string input)
        {
            return SendStyled(TextStyleServices.SpaceOut(input));
        }

        /// <summary>
        ///     Converts text to regional indicator emoji.
        /// </summary>
        /// <param name="input">The text to convert.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Cmd]
        [Aliases]
        public Task Emojify([Remainder] string input)
        {
            return SendStyled(TextStyleServices.Emojify(input));
        }

        private Task SendStyled(string text)
        {
            return text.Length > 2000
                ? ReplyErrorAsync(Strings.TextStyleTooLong(ctx.Guild?.Id ?? 0))
                : ctx.Channel.SendMessageAsync(text.SanitizeMentions(true));
        }
    }
}
