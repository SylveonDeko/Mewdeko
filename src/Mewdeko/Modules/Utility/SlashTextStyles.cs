using Discord.Interactions;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.Utility.Services;

namespace Mewdeko.Modules.Utility;

/// <summary>
///     Holds the top-level text style group.
/// </summary>
public class SlashTextStyles : MewdekoSlashSubmodule
{
    /// <summary>
    ///     Commands that rewrite text in decorative styles.
    /// </summary>
    [Group("style", "Rewrite text in decorative styles")]
    public class TextStyles : MewdekoSlashSubmodule
    {
        /// <summary>
        ///     Converts text to script letters.
        /// </summary>
        /// <param name="input">The text to convert.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("fancy", "Converts text to script letters")]
        [CheckPermissions]
        public Task Fancy([Summary("input", "The text to convert")] string input)
        {
            return Respond(TextStyleServices.Fancy(input));
        }

        /// <summary>
        ///     Converts text to bold script letters.
        /// </summary>
        /// <param name="input">The text to convert.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("bold-fancy", "Converts text to bold script letters")]
        [CheckPermissions]
        public Task BoldFancy([Summary("input", "The text to convert")] string input)
        {
            return Respond(TextStyleServices.BoldFancy(input));
        }

        /// <summary>
        ///     Converts text to fraktur letters.
        /// </summary>
        /// <param name="input">The text to convert.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("fraktur", "Converts text to fraktur letters")]
        [CheckPermissions]
        public Task Fraktur([Summary("input", "The text to convert")] string input)
        {
            return Respond(TextStyleServices.Fraktur(input));
        }

        /// <summary>
        ///     Converts text to bold fraktur letters.
        /// </summary>
        /// <param name="input">The text to convert.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("bold-fraktur", "Converts text to bold fraktur letters")]
        [CheckPermissions]
        public Task BoldFraktur([Summary("input", "The text to convert")] string input)
        {
            return Respond(TextStyleServices.BoldFraktur(input));
        }

        /// <summary>
        ///     Converts text to double-struck letters.
        /// </summary>
        /// <param name="input">The text to convert.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("double-struck", "Converts text to double-struck letters")]
        [CheckPermissions]
        public Task DoubleStruck([Summary("input", "The text to convert")] string input)
        {
            return Respond(TextStyleServices.DoubleStruck(input));
        }

        /// <summary>
        ///     Converts text to small capital letters.
        /// </summary>
        /// <param name="input">The text to convert.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("small-caps", "Converts text to small capital letters")]
        [CheckPermissions]
        public Task SmallCaps([Summary("input", "The text to convert")] string input)
        {
            return Respond(TextStyleServices.SmallCaps(input));
        }

        /// <summary>
        ///     Converts text to full-width letters.
        /// </summary>
        /// <param name="input">The text to convert.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("aesthetic", "Converts text to full-width letters")]
        [CheckPermissions]
        public Task Aesthetic([Summary("input", "The text to convert")] string input)
        {
            return Respond(TextStyleServices.Aesthetic(input));
        }

        /// <summary>
        ///     Puts a clap between each word.
        /// </summary>
        /// <param name="input">The text to convert.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("clap", "Puts a clap between each word")]
        [CheckPermissions]
        public Task Clap([Summary("input", "The text to convert")] string input)
        {
            return Respond(TextStyleServices.Clap(input));
        }

        /// <summary>
        ///     Puts a space between each letter.
        /// </summary>
        /// <param name="input">The text to convert.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("space-out", "Puts a space between each letter")]
        [CheckPermissions]
        public Task SpaceOut([Summary("input", "The text to convert")] string input)
        {
            return Respond(TextStyleServices.SpaceOut(input));
        }

        /// <summary>
        ///     Converts text to regional indicator emoji.
        /// </summary>
        /// <param name="input">The text to convert.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [SlashCommand("emojify", "Converts text to regional indicator emoji")]
        [CheckPermissions]
        public Task Emojify([Summary("input", "The text to convert")] string input)
        {
            return Respond(TextStyleServices.Emojify(input));
        }

        private Task Respond(string text)
        {
            return text.Length > 2000
                ? ReplyErrorAsync(Strings.TextStyleTooLong(ctx.Guild?.Id ?? 0))
                : ctx.Interaction.RespondAsync(text.SanitizeMentions(true));
        }
    }
}
