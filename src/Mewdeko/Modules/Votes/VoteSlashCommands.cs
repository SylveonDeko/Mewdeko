using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Humanizer;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.Modals;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.Votes.Services;

namespace Mewdeko.Modules.Votes;

/// <summary>
/// Provides slash commands for configuring and managing vote settings within a Discord server.
/// </summary>
[Group("votes", "Configure vote settings for the bot")]
public class VoteSlashCommands(InteractiveService interactivity) : MewdekoSlashModuleBase<VoteService>
{
    /// <summary>
    /// Sets a designated channel for vote notifications.
    /// </summary>
    /// <param name="channel">The text channel where vote notifications will be sent.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("channel", "Set the channel"), SlashUserPerm(GuildPermission.ManageGuild), CheckPermissions,
     RequireContext(ContextType.Guild)]
    public async Task VoteChannel(ITextChannel channel)
    {
        await Service.SetVoteChannel(ctx.Guild.Id, channel.Id);
        await ctx.Interaction.SendConfirmAsync("Sucessfully set the vote channel!");
    }

    /// <summary>
    /// Configures a custom message to display when a vote is received. If no message is provided, it previews the current vote message.
    /// </summary>
    /// <param name="message">The custom message for votes. Null to preview the current message, "-" to reset to default.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("message", "Set the embed/text message when a vote is recieved"),
     SlashUserPerm(GuildPermission.ManageGuild), CheckPermissions, RequireContext(ContextType.Guild)]
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
                        .WithOverride("%votesmonthcount%",
                            () => votes.Count(x => x.DateAdded.Value.Month == DateTime.UtcNow.Month).ToString())
                        .Build();

                    if (SmartEmbed.TryParse(rep.Replace(voteMessage), ctx.Guild.Id, out var embeds, out var plainText,
                            out var components))
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

    /// <summary>
    /// Initiates a modal interaction for setting a new vote password.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("password", "Sets the password using a modal."), SlashUserPerm(GuildPermission.ManageGuild),
     RequireContext(ContextType.Guild), CheckPermissions]
    public async Task VotePassword()
    {
        await DeferAsync();
        if (await PromptUserConfirmAsync(
                "Please make absolute sure nobody else sees this password, otherwise this will lead to improper data. ***Mewdeko and it's team are not responsible for stolen passwords***. \n\nDo you agree to these terms?",
                ctx.User.Id))
        {
            var component = new ComponentBuilder().WithButton(
                "Press this to set the password. Remember, do not share it to anyone else.", "setvotepassword");
            await ctx.Interaction.FollowupAsync("_ _", components: component.Build());
        }
    }

    /// <summary>
    /// Adds a Discord role as a vote reward, with an optional duration for the reward to last.
    /// </summary>
    /// <param name="role">The role to be added as a vote reward.</param>
    /// <param name="time">The duration in a human-readable format (e.g., "1d2h") for how long the vote reward lasts.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("roleadd", "Add a role as a vote role"), SlashUserPerm(GuildPermission.ManageGuild),
     RequireContext(ContextType.Guild), CheckPermissions]
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
                await ctx.Interaction.SendErrorAsync(
                    $"Adding vote role failed for the following reason: {added.Item2}");
            }
            else
            {
                await ctx.Interaction.SendConfirmAsync(
                    $"{role.Mention} added as a vote role and will last {parsedTime.Time.Humanize()} when a person votes.");
            }
        }
        else
        {
            var added = await Service.AddVoteRole(ctx.Guild.Id, role.Id);
            if (!added.Item1)
            {
                await ctx.Interaction.SendErrorAsync(
                    $"Adding vote role failed for the following reason: {added.Item2}");
            }
            else
            {
                await ctx.Interaction.SendConfirmAsync($"{role.Mention} added as a vote role.");
            }
        }
    }

    /// <summary>
    /// Edits the expiration timer for an existing vote role.
    /// </summary>
    /// <param name="role">The role whose timer is to be edited.</param>
    /// <param name="time">The new duration in a human-readable format (e.g., "1d2h") for the role reward.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("roleedit", "Edits a vote roles expire timer."), SlashUserPerm(GuildPermission.ManageGuild),
     RequireContext(ContextType.Guild), CheckPermissions]
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
            await ctx.Interaction.SendErrorAsync(
                $"Updating vote role time failed due to the following reason: {update.Item2}");
        }
        else
        {
            await ctx.Interaction.SendConfirmAsync(
                $"Successfuly updated the vote role time to {parsedTime.Time.Humanize()}");
        }
    }

    /// <summary>
    /// A component interaction that prompts a modal for setting the vote password securely.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [ComponentInteraction("setvotepassword", true), SlashUserPerm(GuildPermission.ManageGuild), CheckPermissions,
     RequireContext(ContextType.Guild)]
    public Task VotePasswordButton()
        => RespondWithModalAsync<VotePasswordModal>("votepassmodal");

    /// <summary>
    /// Handles the modal interaction for the vote password submission.
    /// </summary>
    /// <param name="modal">The modal containing the vote password.</param>
    /// <returns>A task that represents the asynchronous operation of setting the vote password.</returns>
    [ModalInteraction("votepassmodal", true), SlashUserPerm(GuildPermission.ManageGuild), CheckPermissions,
     RequireContext(ContextType.Guild)]
    public async Task VotePassModal(VotePasswordModal modal)
    {
        await ctx.Interaction.SendEphemeralConfirmAsync("Vote password set.");
        await Service.SetVotePassword(ctx.Guild.Id, modal.Password);
    }

    /// <summary>
    /// Displays the total votes and this month's votes for a specified user or the command invoker.
    /// </summary>
    /// <param name="user">The user whose vote count to display. If null, displays for the command invoker.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("votes", "Shows your total and this months votes"), RequireContext(ContextType.Guild),
     CheckPermissions]
    public async Task Votes(IUser user = null)
    {
        var curUser = user ?? ctx.User;
        await ctx.Interaction.RespondAsync(embed: (await Service.GetTotalVotes(curUser, ctx.Guild)).Build());
    }

    /// <summary>
    /// Shows a leaderboard of votes, optionally filtered by votes cast in the current month.
    /// </summary>
    /// <param name="monthly">Whether to filter the leaderboard to only include votes from the current month.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("leaderboard", "Shows the current or monthly leaderboard for votes"),
     RequireContext(ContextType.Guild), CheckPermissions]
    public async Task VotesLeaderboard(bool monthly = false)
    {
        List<Database.Models.Votes> votes;
        if (monthly)
            votes = (await Service.GetVotes(ctx.Guild.Id)).Where(x => x.DateAdded.Value.Month == DateTime.UtcNow.Month)
                .ToList();
        else votes = await Service.GetVotes(ctx.Guild.Id);
        if (votes is null || !votes.Any())
        {
            await ctx.Channel.SendErrorAsync(monthly
                ? "Not enough monthly votes for a leaderboard."
                : "Not enough votes for a leaderboard.");
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
                voteList.Add(new CustomVoteThingy
                {
                    User = user, VoteCount = 1
                });
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

        await interactivity.SendPaginatorAsync(paginator, Context.Interaction, TimeSpan.FromMinutes(60))
            .ConfigureAwait(false);


        async Task<PageBuilder> PageFactory(int page)
        {
            await Task.CompletedTask;
            var eb = new PageBuilder().WithTitle(monthly ? "Votes leaaderboard for this month" : "Votes Leaderboard")
                .WithOkColor();

            for (var i = 0; i < voteList.Count; i++)
            {
                eb.AddField(
                    $"#{i + 1 + (page * 12)} {voteList[i].User}",
                    $"{voteList[i].VoteCount}");
            }

            return eb;
        }
    }
}