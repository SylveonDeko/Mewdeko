using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using NekosBestApiNet;
using NekosBestApiNet.Models.Images;

namespace Mewdeko.Modules.Searches;

/// <inheritdoc />
public partial class Searches
{
    /// <summary>
    /// Submodule containing action commands.
    /// </summary>
    public class ActionCommands(NekosBestApi nekosBestApi) : MewdekoSubmodule
    {
        /// <summary>
        /// Shoots someone.
        /// </summary>
        /// <param name="toShow">The person to shoot.</param>
        /// <example>
        /// .shoot @user
        /// </example>
        [Cmd, Aliases]
        public async Task Shoot(string toShow) =>
            await SendAction(await nekosBestApi.ActionsApi.Shoot(), "shot", toShow)
                .ConfigureAwait(false);

        /// <summary>
        /// Holds someone's hand.
        /// </summary>
        /// <param name="toShow">The person to hold hands with.</param>
        /// <example>
        /// .handhold @user
        /// </example>
        [Cmd, Aliases]
        public async Task Handhold(string toShow) =>
            await SendAction(await nekosBestApi.ActionsApi.Handhold(), "handholds", toShow)
                .ConfigureAwait(false);

        /// <summary>
        /// Punches someone.
        /// </summary>
        /// <param name="toShow">The person to punch.</param>
        /// <example>
        /// .punch @user
        /// </example>
        [Cmd, Aliases]
        public async Task Punch(string toShow) =>
            await SendAction(await nekosBestApi.ActionsApi.Punch(), "punched", toShow)
                .ConfigureAwait(false);

        /// <summary>
        /// Hugs someone.
        /// </summary>
        /// <param name="toShow">The person to hug.</param>
        /// <example>
        /// .hug @user
        /// </example>
        [Cmd, Aliases]
        public async Task Hug([Remainder] string toShow) =>
            await SendAction(await nekosBestApi.ActionsApi.Hug(), "hugs", toShow)
                .ConfigureAwait(false);

        /// <summary>
        /// Kisses someone.
        /// </summary>
        /// <param name="toShow">The person to kiss.</param>
        /// <example>
        /// .kiss @user
        /// </example>
        [Cmd, Aliases]
        public async Task Kiss([Remainder] string toShow) =>
            await SendAction(await nekosBestApi.ActionsApi.Kiss(), "Kissed", toShow)
                .ConfigureAwait(false);

        /// <summary>
        /// Pats someone.
        /// </summary>
        /// <param name="toShow">The person to pat.</param>
        /// <example>
        /// .pat @user
        /// </example>
        [Cmd, Aliases]
        public async Task Pat([Remainder] string toShow) =>
            await SendAction(await nekosBestApi.ActionsApi.Pat(), "gave pattus to", toShow)
                .ConfigureAwait(false);

        /// <summary>
        /// Tickles someone.
        /// </summary>
        /// <param name="toShow">The person to tickle.</param>
        /// <example>
        /// .tickle @user
        /// </example>
        [Cmd, Aliases]
        public async Task Tickle([Remainder] string toShow) =>
            await SendAction(await nekosBestApi.ActionsApi.Tickle(), "tickles", toShow)
                .ConfigureAwait(false);

        /// <summary>
        /// Slaps someone.
        /// </summary>
        /// <param name="toShow">The person to slap.</param>
        /// <example>
        /// .slap @user
        /// </example>
        [Cmd, Aliases]
        public async Task Slap([Remainder] string toShow) =>
            await SendAction(await nekosBestApi.ActionsApi.Slap(), "slapped", toShow)
                .ConfigureAwait(false);

        /// <summary>
        /// Cuddles with someone.
        /// </summary>
        /// <param name="toShow">The person to cuddle with.</param>
        /// <example>
        /// .cuddle @user
        /// </example>
        [Cmd, Aliases]
        public async Task Cuddle([Remainder] string toShow) =>
            await SendAction(await nekosBestApi.ActionsApi.Cuddle(), "cuddles with", toShow)
                .ConfigureAwait(false);

        /// <summary>
        /// Pokes someone.
        /// </summary>
        /// <param name="toShow">The person to poke.</param>
        /// <example>
        /// .poke @user
        /// </example>
        [Cmd, Aliases]
        public async Task Poke([Remainder] string toShow) =>
            await SendAction(await nekosBestApi.ActionsApi.Poke(), "poked", toShow)
                .ConfigureAwait(false);

        /// <summary>
        /// Feeds someone.
        /// </summary>
        /// <param name="toShow">The person to feed.</param>
        /// <example>
        /// .feed @user
        /// </example>
        [Cmd, Aliases]
        public async Task Feed([Remainder] string toShow) =>
            await SendAction(await nekosBestApi.ActionsApi.Feed(), "is feeding", toShow)
                .ConfigureAwait(false);

        /// <summary>
        /// Calls someone a baka.
        /// </summary>
        /// <param name="toShow">The person to call baka.</param>
        /// <example>
        /// .baka @user
        /// </example>
        [Cmd, Aliases]
        public async Task Baka([Remainder] string toShow) =>
            await SendAction(await nekosBestApi.ActionsApi.Baka(), "bakas", toShow)
                .ConfigureAwait(false);

        /// <summary>
        /// Bites someone.
        /// </summary>
        /// <param name="toShow">The person to bite.</param>
        /// <example>
        /// .bite @user
        /// </example>
        [Cmd, Aliases]
        public async Task Bite([Remainder] string toShow) =>
            await SendAction(await nekosBestApi.ActionsApi.Bite(), "bites", toShow)
                .ConfigureAwait(false);

        /// <summary>
        /// Blushes.
        /// </summary>
        /// <param name="toShow">Additional text (optional).</param>
        /// <example>
        /// .blush
        /// </example>
        [Cmd, Aliases]
        public async Task Blush([Remainder] string toShow = null) =>
            await SendAction(await nekosBestApi.ActionsApi.Blush(), "blushes", toShow)
                .ConfigureAwait(false);

        /// <summary>
        /// Indicates boredom.
        /// </summary>
        /// <param name="toShow">Additional text (optional).</param>
        /// <example>
        /// .bored
        /// </example>
        [Cmd, Aliases]
        public async Task Bored([Remainder] string toShow = null) =>
            await SendAction(await nekosBestApi.ActionsApi.Bored(), "is bored", toShow)
                .ConfigureAwait(false);

        /// <summary>
        /// Cries.
        /// </summary>
        /// <param name="toShow">Additional text (optional).</param>
        /// <example>
        /// .cry
        /// </example>
        [Cmd, Aliases]
        public async Task Cry([Remainder] string toShow = null) =>
            await SendAction(await nekosBestApi.ActionsApi.Cry(), "cries", toShow)
                .ConfigureAwait(false);

        /// <summary>
        /// Dances.
        /// </summary>
        /// <param name="toShow">Additional text (optional).</param>
        /// <example>
        /// .dance
        /// </example>
        [Cmd, Aliases]
        public async Task Dance([Remainder] string toShow = null) =>
            await SendAction(await nekosBestApi.ActionsApi.Dance(), "dances", toShow)
                .ConfigureAwait(false);

        /// <summary>
        /// Facepalms.
        /// </summary>
        /// <param name="toShow">Additional text (optional).</param>
        /// <example>
        /// .facepalm
        /// </example>
        [Cmd, Aliases]
        public async Task Facepalm([Remainder] string toShow = null) =>
            await SendAction(await nekosBestApi.ActionsApi.Facepalm(), "facepalms", toShow)
                .ConfigureAwait(false);

        /// <summary>
        /// Expresses happiness.
        /// </summary>
        /// <param name="toShow">Additional text (optional).</param>
        /// <example>
        /// .happy
        /// </example>
        [Cmd, Aliases]
        public async Task Happy([Remainder] string toShow = null) =>
            await SendAction(await nekosBestApi.ActionsApi.Happy(), "is happy", toShow)
                .ConfigureAwait(false);

        /// <summary>
        /// Gives someone a high-five.
        /// </summary>
        /// <param name="toShow">The person to give a high-five.</param>
        /// <example>
        /// .highfive @user
        /// </example>
        [Cmd, Aliases]
        public async Task HighFive([Remainder] string toShow) =>
            await SendAction(await nekosBestApi.ActionsApi.Highfive(), "high-fives", toShow)
                .ConfigureAwait(false);

        /// <summary>
        /// Laughs.
        /// </summary>
        /// <param name="toShow">Additional text (optional).</param>
        /// <example>
        /// .laugh
        /// </example>
        [Cmd, Aliases]
        public async Task Laugh([Remainder] string toShow = null) =>
            await SendAction(await nekosBestApi.ActionsApi.Laugh(), "laughs", toShow)
                .ConfigureAwait(false);

        /// <summary>
        /// Pouts.
        /// </summary>
        /// <param name="toShow">Additional text (optional).</param>
        /// <example>
        /// .pout
        /// </example>
        [Cmd, Aliases]
        public async Task Pout([Remainder] string toShow = null) =>
            await SendAction(await nekosBestApi.ActionsApi.Pout(), "pouts", toShow)
                .ConfigureAwait(false);

        /// <summary>
        /// Shrugs.
        /// </summary>
        /// <param name="toShow">Additional text (optional).</param>
        /// <example>
        /// .shrug
        /// </example>
        [Cmd, Aliases]
        public async Task Shrug([Remainder] string toShow = null) =>
            await SendAction(await nekosBestApi.ActionsApi.Shrug(), "shrugs", toShow)
                .ConfigureAwait(false);

        /// <summary>
        /// Sleeps.
        /// </summary>
        /// <param name="toShow">Additional text (optional).</param>
        /// <example>
        /// .sleep
        /// </example>
        [Cmd, Aliases]
        public async Task Sleep([Remainder] string toShow = null) =>
            await SendAction(await nekosBestApi.ActionsApi.Sleep(), "sleeps", toShow)
                .ConfigureAwait(false);

        /// <summary>
        /// Smiles.
        /// </summary>
        /// <param name="toShow">Additional text (optional).</param>
        /// <example>
        /// .smile
        /// </example>
        [Cmd, Aliases]
        public async Task Smile([Remainder] string toShow = null) =>
            await SendAction(await nekosBestApi.ActionsApi.Smile(), "smiles", toShow)
                .ConfigureAwait(false);

        /// <summary>
        /// Is smug.
        /// </summary>
        /// <param name="toShow">Additional text (optional).</param>
        /// <example>
        /// .smug
        /// </example>
        [Cmd, Aliases]
        public async Task Smug([Remainder] string toShow = null) =>
            await SendAction(await nekosBestApi.ActionsApi.Smug(), "is smug", toShow)
                .ConfigureAwait(false);

        /// <summary>
        /// Stares.
        /// </summary>
        /// <param name="toShow">Additional text (optional).</param>
        /// <example>
        /// .stare
        /// </example>
        [Cmd, Aliases]
        public async Task Stare([Remainder] string toShow = null) =>
            await SendAction(await nekosBestApi.ActionsApi.Stare(), "stares", toShow)
                .ConfigureAwait(false);

        /// <summary>
        /// Thinks.
        /// </summary>
        /// <param name="toShow">Additional text (optional).</param>
        /// <example>
        /// .think
        /// </example>
        [Cmd, Aliases]
        public async Task Think([Remainder] string toShow = null) =>
            await SendAction(await nekosBestApi.ActionsApi.Think(), "thinks", toShow)
                .ConfigureAwait(false);

        /// <summary>
        /// Gives a thumbs-up.
        /// </summary>
        /// <param name="toShow">Additional text (optional).</param>
        /// <example>
        /// .thumbsup
        /// </example>
        [Cmd, Aliases]
        public async Task ThumbsUp([Remainder] string toShow = null) =>
            await SendAction(await nekosBestApi.ActionsApi.Thumbsup(), "gives a thumbs-up", toShow)
                .ConfigureAwait(false);

        /// <summary>
        /// Waves.
        /// </summary>
        /// <param name="toShow">Additional text (optional).</param>
        /// <example>
        /// .wave
        /// </example>
        [Cmd, Aliases]
        public async Task Wave([Remainder] string toShow = null) =>
            await SendAction(await nekosBestApi.ActionsApi.Wave(), "waves", toShow)
                .ConfigureAwait(false);

        /// <summary>
        /// Winks.
        /// </summary>
        /// <param name="toShow">Additional text (optional).</param>
        /// <example>
        /// .wink
        /// </example>
        [Cmd, Aliases]
        public async Task Wink([Remainder] string toShow = null) =>
            await SendAction(await nekosBestApi.ActionsApi.Wink(), "winks", toShow)
                .ConfigureAwait(false);

        private async Task SendAction(ActionResult result, string action, string toShow = null)
        {
            var em = new EmbedBuilder
            {
                Description =
                    toShow is null
                        ? $"{ctx.User.Mention} is {action}\n{toShow}"
                        : $"{ctx.User.Mention} {action} {toShow}",
                ImageUrl = result.Results.FirstOrDefault().Url,
                Color = Mewdeko.OkColor
            };
            await ctx.Channel.SendMessageAsync(embed: em.Build()).ConfigureAwait(false);
        }
    }
}