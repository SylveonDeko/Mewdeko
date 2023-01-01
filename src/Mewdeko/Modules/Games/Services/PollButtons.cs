using System.Threading.Tasks;
using Discord.Interactions;

namespace Mewdeko.Modules.Games.Services;

public class PollButtons : MewdekoSlashCommandModule
{
    private readonly PollService pollService;

    public PollButtons(PollService pollService)
        => this.pollService = pollService;

    [ComponentInteraction("pollbutton:*")]
    public async Task Pollbutton(string num)
    {
        var (allowed, type) = await pollService.TryVote(ctx.Guild, int.Parse(num) - 1, ctx.User);
        switch (type)
        {
            case PollType.PollEnded:
                await ctx.Interaction.SendEphemeralErrorAsync("That poll has already ended!");
                break;
            case PollType.SingleAnswer:
                if (!allowed)
                    await ctx.Interaction.SendEphemeralErrorAsync("You can't change your vote!");
                else
                    await ctx.Interaction.SendEphemeralConfirmAsync("Voted!");
                break;
            case PollType.AllowChange:
                if (!allowed)
                    await ctx.Interaction.SendEphemeralErrorAsync("That's already your vote!");
                else
                    await ctx.Interaction.SendEphemeralConfirmAsync("Vote changed.");
                break;
            case PollType.MultiAnswer:
                if (!allowed)
                    await ctx.Interaction.SendEphemeralErrorAsync("Removed that vote!");
                else
                    await ctx.Interaction.SendEphemeralConfirmAsync("Vote added!");
                break;
        }
    }
}