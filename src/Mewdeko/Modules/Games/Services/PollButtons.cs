using Discord.Interactions;
using System.Threading.Tasks;

namespace Mewdeko.Modules.Games.Services;

public class PollButtons : MewdekoSlashCommandModule
{
    private readonly PollService _pollService;

    public PollButtons(PollService pollService)
        => _pollService = pollService;

    [ComponentInteraction("pollbutton:*")]
    public async Task Pollbutton(string num)
    {
        var (allowed, type) = await _pollService.TryVote(ctx.Guild, int.Parse(num) - 1, ctx.User).ConfigureAwait(false);
        switch (type)
        {
            case PollType.PollEnded:
                await ctx.Interaction.SendEphemeralErrorAsync("That poll has already ended!").ConfigureAwait(false);
                break;
            case PollType.SingleAnswer:
                if (!allowed)
                    await ctx.Interaction.SendEphemeralErrorAsync("You can't change your vote!").ConfigureAwait(false);
                else
                    await ctx.Interaction.SendEphemeralConfirmAsync("Voted!").ConfigureAwait(false);
                break;
            case PollType.AllowChange:
                if (!allowed)
                    await ctx.Interaction.SendEphemeralErrorAsync("That's already your vote!").ConfigureAwait(false);
                else
                    await ctx.Interaction.SendEphemeralConfirmAsync("Vote changed.").ConfigureAwait(false);
                break;
            case PollType.MultiAnswer:
                if (!allowed)
                    await ctx.Interaction.SendEphemeralErrorAsync("Removed that vote!").ConfigureAwait(false);
                else
                    await ctx.Interaction.SendEphemeralConfirmAsync("Vote added!").ConfigureAwait(false);
                break;
        }
    }
}