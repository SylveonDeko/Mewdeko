using Discord.Interactions;
using Mewdeko.Common.Configs;

namespace Mewdeko.Modules.Games.Services
{
    /// <summary>
    /// Handles interaction with poll buttons for voting.
    /// </summary>
    public class PollButtons(PollService pollService, BotConfig config) : MewdekoSlashCommandModule
    {
        /// <summary>
        /// Handles interaction with poll buttons for voting.
        /// </summary>
        /// <param name="num">The number representing the option selected.</param>
        [ComponentInteraction("pollbutton:*")]
        public async Task Pollbutton(string num)
        {
            var (allowed, type) = await pollService.TryVote(ctx.Guild, int.Parse(num) - 1, ctx.User);
            switch (type)
            {
                case PollType.PollEnded:
                    await ctx.Interaction.SendEphemeralErrorAsync("That poll has already ended!", config);
                    break;
                case PollType.SingleAnswer:
                    if (!allowed)
                        await ctx.Interaction.SendEphemeralErrorAsync("You can't change your vote!", config);
                    else
                        await ctx.Interaction.SendEphemeralConfirmAsync("Voted!");
                    break;
                case PollType.AllowChange:
                    if (!allowed)
                        await ctx.Interaction.SendEphemeralErrorAsync("That's already your vote!", config);
                    else
                        await ctx.Interaction.SendEphemeralConfirmAsync("Vote changed.");
                    break;
                case PollType.MultiAnswer:
                    if (!allowed)
                        await ctx.Interaction.SendEphemeralErrorAsync("Removed that vote!", config);
                    else
                        await ctx.Interaction.SendEphemeralConfirmAsync("Vote added!");
                    break;
            }
        }
    }
}