using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Humanizer;
using Mewdeko.Common.Modals;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.Votes.Services;
using System.Threading.Tasks;
using Mewdeko.Common.Attributes.InteractionCommands;

namespace Mewdeko.Modules.Votes;
[Group("votes", "Configure vote settings for the bot")]
public class VoteSlashCommands : MewdekoSlashModuleBase<VoteService>
{
    private readonly InteractiveService interactivity;

    public VoteSlashCommands(InteractiveService interactivity) => this.interactivity = interactivity;

    [SlashCommand("channel", "Set the channel"), SlashUserPerm(GuildPermission.ManageGuild), CheckPermissions, RequireContext(ContextType.Guild)]
    public async Task VoteChannel(ITextChannel channel)
    {
        await Service.SetVoteChannel(ctx.Guild.Id, channel.Id);
        await ctx.Interaction.SendConfirmAsync("Sucessfully set the vote channel!");
    }

    [SlashCommand("message", "Set the embed/text message when a vote is recieved"), SlashUserPerm(GuildPermission.ManageGuild), CheckPermissions, RequireContext(ContextType.Guild)]
    public async Task VoteMessage(string message = null)
    {
        await DeferAsync();
        var voteMessage = await Service.GetVoteMessage(ctx.Guild.Id);
        var votes = await Service.GetVotes(ctx.Guild.Id, ctx.User.Id);
        switch (message)
        {
            case null when await PromptUserConfirmAsync("Do you want to preview your embed?", ctx.User.Id):
                {
                    if (string.IsNullOrWhiteSpace(voteMessage))
                    {
                        var eb = new EmbedBuilder()
                                 .WithTitle($"Thanks for voting for {ctx.Guild.Name}")
                                 .WithDescription($"You have votedd a total of {votes.Count} times!")
                                 .WithThumbnailUrl(ctx.User.RealAvatarUrl().AbsoluteUri)
                                 .WithOkColor();

                        await ctx.Interaction.FollowupAsync(ctx.User.Mention, embed: eb.Build());
                    }
                    else
                    {
                        var rep = new ReplacementBuilder()
                                  .WithDefault(ctx.User, null, ctx.Guild as SocketGuild, ctx.Client as DiscordSocketClient)
                                  .WithOverride("%votestotalcount%", () => votes.Count.ToString())
                                  .WithOverride("%votesmonthcount%", () => votes.Count(x => x.DateAdded.Value.Month == DateTime.UtcNow.Month).ToString()).Build();;

                        if (SmartEmbed.TryParse(rep.Replace(voteMessage), ctx.Guild.Id, out var embeds, out var plainText, out var components))
                        {
                            await ctx.Interaction.FollowupAsync(plainText, embeds: embeds, components: components.Build());
                        }
                        else
                        {
                            await ctx.Interaction.FollowupAsync(rep.Replace(voteMessage).SanitizeMentions());
                        }
                    }

                    break;
                }
            case null when string.IsNullOrWhiteSpace(voteMessage):
                await ctx.Interaction.SendConfirmFollowupAsync("Using the default vote message.");
                return;
            case null:
                await ctx.Interaction.SendConfirmFollowupAsync(voteMessage);
                break;
            case "-":
                await ctx.Interaction.SendConfirmFollowupAsync("Switched to using the default embed.");
                await Service.SetVoteMessage(ctx.Guild.Id, "");
                break;
            default:
                await ctx.Interaction.SendConfirmFollowupAsync("Vote message set.");
                await Service.SetVoteMessage(ctx.Guild.Id, message);
                break;
        }
    }

    [SlashCommand("password", "Sets the password using a modal."), SlashUserPerm(GuildPermission.ManageGuild), RequireContext(ContextType.Guild), CheckPermissions]
    public async Task VotePassword()
    {
        await DeferAsync();
        if (await PromptUserConfirmAsync(
                "Please make absolute sure nobody else sees this password, otherwise this will lead to improper data. ***Mewdeko and it's team are not responsible for stolen passwords***. \n\nDo you agree to these terms?",
                ctx.User.Id))
        {
            var component = new ComponentBuilder().WithButton("Press this to set the password. Remember, do not share it to anyone else.", "setvotepassword");
            await ctx.Interaction.FollowupAsync("_ _", components: component.Build());
        }
    }

    [SlashCommand("roleadd", "Add a role as a vote role"), SlashUserPerm(GuildPermission.ManageGuild), RequireContext(ContextType.Guild), CheckPermissions]
    public async Task VoteRoleAdd(IRole role, string time = null)
    {
        var parsedTime = StoopidTime.FromInput("0s");
        if (time is not null)
        {
            try
            {
                parsedTime = StoopidTime.FromInput(time);
            }
            catch
            {
                await ctx.Interaction.SendErrorAsync("Invalid time input. Format is 1d3h2s");
                return;
            }
        }
        if (parsedTime.Time != TimeSpan.Zero)
        {
            var added = await Service.AddVoteRole(ctx.Guild.Id, role.Id, (int)parsedTime.Time.TotalSeconds);
            if (!added.Item1)
            {
                await ctx.Interaction.SendErrorAsync($"Adding vote role failed for the following reason: {added.Item2}");
            }
            else
            {
                await ctx.Interaction.SendConfirmAsync($"{role.Mention} added as a vote role and will last {parsedTime.Time.Humanize()} when a person votes.");
            }
        }
        else
        {
            var added = await Service.AddVoteRole(ctx.Guild.Id, role.Id);
            if (!added.Item1)
            {
                await ctx.Interaction.SendErrorAsync($"Adding vote role failed for the following reason: {added.Item2}");
            }
            else
            {
                await ctx.Interaction.SendConfirmAsync($"{role.Mention} added as a vote role.");
            }
        }
    }

    [SlashCommand("roleedit", "Edits a vote roles expire timer."), SlashUserPerm(GuildPermission.ManageGuild), RequireContext(ContextType.Guild), CheckPermissions]
    public async Task VoteRoleEdit(IRole role, string time)
    {
        StoopidTime parsedTime;
        try
        {
            parsedTime = StoopidTime.FromInput(time);
        }
        catch
        {
            await ctx.Interaction.SendErrorAsync("Invalid time input. Format is 1d3h2s");
            return;
        }
        var update = await Service.UpdateTimer(role.Id, (int)parsedTime.Time.TotalSeconds);
        if (!update.Item1)
        {
            await ctx.Interaction.SendErrorAsync($"Updating vote role time failed due to the following reason: {update.Item2}");
        }
        else
        {
            await ctx.Interaction.SendConfirmAsync($"Successfuly updated the vote role time to {parsedTime.Time.Humanize()}");
        }
    }

    [ComponentInteraction("setvotepassword", true), SlashUserPerm(GuildPermission.ManageGuild), CheckPermissions, RequireContext(ContextType.Guild)]
    public async Task VotePasswordButton()
        => await RespondWithModalAsync<VotePasswordModal>("votepassmodal");

    [ModalInteraction("votepassmodal", true), SlashUserPerm(GuildPermission.ManageGuild), CheckPermissions, RequireContext(ContextType.Guild)]
    public async Task VotePassModal(VotePasswordModal modal)
    {
        await ctx.Interaction.SendEphemeralConfirmAsync("Vote password set.");
        await Service.SetVotePassword(ctx.Guild.Id, modal.Password);
    }

    [SlashCommand("votes", "Shows your total and this months votes"), RequireContext(ContextType.Guild), CheckPermissions]
    public async Task Votes(IUser user = null)
    {
        var curUser = user ?? ctx.User;
        await ctx.Interaction.RespondAsync(embed: (await Service.GetTotalVotes(curUser, ctx.Guild)).Build());
    }

    [SlashCommand("leaderboard", "Shows the current or monthly leaderboard for votes"), RequireContext(ContextType.Guild), CheckPermissions]
    public async Task VotesLeaderboard(bool monthly = false)
    {

        List<Database.Models.Votes> votes;
        if (monthly)
            votes = (await Service.GetVotes(ctx.Guild.Id)).Where(x => x.DateAdded.Value.Month == DateTime.UtcNow.Month).ToList();
        else votes = await Service.GetVotes(ctx.Guild.Id);
        if (votes is null || !votes.Any())
        {
            await ctx.Channel.SendErrorAsync(monthly ? "Not enough monthly votes for a leaderboard." : "Not enough votes for a leaderboard.");
            return;
        }

        var voteList = new List<CustomVoteThingy>();
        foreach (var i in votes)
        {
            var user = (await ctx.Guild.GetUsersAsync()).FirstOrDefault(x => x.Id == i.UserId);
            if (user is null) continue;
            var item = voteList.FirstOrDefault(x => x.User == user);
            if (item is not null)
            {
                voteList.Remove(item);
                item.VoteCount++;
                voteList.Add(item);
            }
            else
            {
                voteList.Add(new CustomVoteThingy{ User = user, VoteCount = 1});
            }

        }
        var paginator = new LazyPaginatorBuilder()
                        .AddUser(ctx.User)
                        .WithPageFactory(PageFactory)
                        .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
                        .WithMaxPageIndex(voteList.Count / 12)
                        .WithDefaultEmotes()
                        .WithActionOnCancellation(ActionOnStop.DeleteMessage)
                        .Build();

        await interactivity.SendPaginatorAsync(paginator, Context.Interaction, TimeSpan.FromMinutes(60)).ConfigureAwait(false);


        async Task<PageBuilder> PageFactory(int page)
        {
            await Task.CompletedTask;
            var eb = new PageBuilder().WithTitle(monthly ? "Votes leaaderboard for this month" : "Votes Leaderboard").WithOkColor();

            for (var i = 0; i < voteList.Count; i++)
            {

                eb.AddField(
                    $"#{i + 1 + (page * 12)} {voteList[i].User.ToString()}",
                    $"{voteList[i].VoteCount}");
            }

            return eb;
        }
    }
}