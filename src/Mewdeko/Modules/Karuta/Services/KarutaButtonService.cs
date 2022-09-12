using Discord.Interactions;
using Swan;
using System.Threading.Tasks;

namespace Mewdeko.Modules.Karuta.Services;

public class KarutaButtonService : MewdekoSlashModuleBase<ShibaKarutaService>
{
    private readonly DbService _db;

    public KarutaButtonService(DbService db) => _db = db;

    [ComponentInteraction("karutaeventbutton:*")]
    public async Task HandleVote(int voteNumber)
    {
        await DeferAsync();
        var buttonText = await Service.GetButtonText(ctx.Guild.Id, voteNumber);
        var interaction = ctx.Interaction as IComponentInteraction;
        await using var uow = _db.GetDbContext();
        var potentialEntry = uow.KarutaEventEntry.FirstOrDefault(x => x.MessageId == interaction.Message.Id);
        if (potentialEntry is null)
        {
            await ctx.Interaction.SendEphemeralFollowupErrorAsync("Seems like that entry is no longer valid and can't be voted for.");
            return;
        }
        var potentialVote = uow.KarutaEventVotes.Where(x => x.MessageId == potentialEntry.MessageId && x.UserId == ctx.User.Id);
        if (potentialVote.Any())
        {
            if (potentialVote.Select(x => x.VotedNum).Contains(voteNumber))
            {
                if (await PromptUserConfirmAsync($"You already voted for `{buttonText}`, do you want to remove your vote?", ctx.User.Id, true))
                {
                    uow.KarutaEventVotes.Remove(potentialVote.FirstOrDefault(x => x.VotedNum == voteNumber));
                    await uow.SaveChangesAsync();
                    await ctx.Interaction.SendEphemeralFollowupConfirmAsync($"Vote for `{buttonText}` removed.");
                    switch (voteNumber)
                    {
                        case 1:
                            potentialEntry.Button1Count -= 1;
                            uow.KarutaEventEntry.Update(potentialEntry);
                            await uow.SaveChangesAsync();
                            break;
                        case 2:
                            potentialEntry.Button2Count -= 1;
                            uow.KarutaEventEntry.Update(potentialEntry);
                            await uow.SaveChangesAsync();
                            break;
                        case 3:
                            potentialEntry.Button3Count -= 1;
                            uow.KarutaEventEntry.Update(potentialEntry);
                            await uow.SaveChangesAsync();
                            break;
                        case 4:
                            potentialEntry.Button4Count -= 1;
                            uow.KarutaEventEntry.Update(potentialEntry);
                            await uow.SaveChangesAsync();
                            break;
                        case 5:
                            potentialEntry.Button5Count -= 1;
                            uow.KarutaEventEntry.Update(potentialEntry);
                            await uow.SaveChangesAsync();
                            break;
                        case 6:
                            potentialEntry.Button6Count -= 1;
                            uow.KarutaEventEntry.Update(potentialEntry);
                            await uow.SaveChangesAsync();
                            break;
                    }
                }
            }
            else
            {
                var vote = new KarutaEventVotes
                {
                    GuildId = ctx.Guild.Id,
                    MessageId = interaction.Message.Id,
                    VotedNum = voteNumber,
                    UserId = ctx.User.Id
                };
                uow.KarutaEventVotes.Add(vote);
                await uow.SaveChangesAsync();
                await ctx.Interaction.SendEphemeralFollowupConfirmAsync($"Succesfully voted for `{buttonText}`");
                switch (voteNumber)
                {
                    case 1:
                        potentialEntry.Button1Count += 1;
                        uow.KarutaEventEntry.Update(potentialEntry);
                        await uow.SaveChangesAsync();
                        break;
                    case 2:
                        potentialEntry.Button2Count += 1;
                        uow.KarutaEventEntry.Update(potentialEntry);
                        await uow.SaveChangesAsync();
                        break;
                    case 3:
                        potentialEntry.Button3Count += 1;
                        uow.KarutaEventEntry.Update(potentialEntry);
                        await uow.SaveChangesAsync();
                        break;
                    case 4:
                        potentialEntry.Button4Count += 1;
                        uow.KarutaEventEntry.Update(potentialEntry);
                        await uow.SaveChangesAsync();
                        break;
                    case 5:
                        potentialEntry.Button5Count += 1;
                        uow.KarutaEventEntry.Update(potentialEntry);
                        await uow.SaveChangesAsync();
                        break;
                    case 6:
                        potentialEntry.Button6Count += 1;
                        uow.KarutaEventEntry.Update(potentialEntry);
                        await uow.SaveChangesAsync();
                        break;
                }
            }
        }
        else
        {
            var vote = new KarutaEventVotes
            {
                GuildId = ctx.Guild.Id,
                MessageId = interaction.Message.Id,
                VotedNum = voteNumber,
                UserId = ctx.User.Id
            };
            uow.KarutaEventVotes.Add(vote);
            await uow.SaveChangesAsync();
            await ctx.Interaction.SendEphemeralFollowupConfirmAsync($"Succesfully voted for `{buttonText}`");
            switch (voteNumber)
            {
                case 1:
                    potentialEntry.Button1Count += 1;
                    uow.KarutaEventEntry.Update(potentialEntry);
                    await uow.SaveChangesAsync();
                    break;
                case 2:
                    potentialEntry.Button2Count += 1;
                    uow.KarutaEventEntry.Update(potentialEntry);
                    await uow.SaveChangesAsync();
                    break;
                case 3:
                    potentialEntry.Button3Count += 1;
                    uow.KarutaEventEntry.Update(potentialEntry);
                    await uow.SaveChangesAsync();
                    break;
                case 4:
                    potentialEntry.Button4Count += 1;
                    uow.KarutaEventEntry.Update(potentialEntry);
                    await uow.SaveChangesAsync();
                    break;
                case 5:
                    potentialEntry.Button5Count += 1;
                    uow.KarutaEventEntry.Update(potentialEntry);
                    await uow.SaveChangesAsync();
                    break;
                case 6:
                    potentialEntry.Button6Count += 1;
                    uow.KarutaEventEntry.Update(potentialEntry);
                    await uow.SaveChangesAsync();
                    break;
            }
        }
    }
}