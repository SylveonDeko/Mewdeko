using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Humanizer;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Common.TypeReaders.Models;
using Mewdeko.Modules.Votes.Services;

namespace Mewdeko.Modules.Votes;

/// <summary>
/// Contains commands related to managing and interacting with voting features in the server.
/// </summary>
public class Vote(InteractiveService interactivity) : MewdekoModuleBase<VoteService>
{
    /// <summary>
    /// Sets the specified text channel as the vote channel where all vote confirmations will be sent.
    /// </summary>
    /// <param name="channel">The text channel to set as the vote channel.</param>
    /// <returns>A task that represents the asynchronous operation of setting the vote channel.</returns>
    [Cmd, Aliases, UserPerm(GuildPermission.ManageGuild), RequireContext(ContextType.Guild)]
    public async Task VoteChannel([Remainder] ITextChannel channel)
    {
        await Service.SetVoteChannel(ctx.Guild.Id, channel.Id);
        await ctx.Channel.SendConfirmAsync("Sucessfully set the vote channel!");
    }

    /// <summary>
    /// Sets a custom message to be sent when a user votes or previews the current vote message.
    /// </summary>
    /// <param name="message">The custom message to set for votes. Use "-" to reset to the default message. If null, previews the current vote message.</param>
    /// <returns>A task that represents the asynchronous operation of setting or previewing the vote message.</returns>
    [Cmd, Aliases, UserPerm(GuildPermission.ManageGuild), RequireContext(ContextType.Guild)]
    public async Task VoteMessage([Remainder] string message = null)
    {
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

                    await ctx.Channel.SendMessageAsync(ctx.User.Mention, embed: eb.Build());
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
                        await ctx.Channel.SendMessageAsync(plainText, embeds: embeds, components: components.Build());
                    }
                    else
                    {
                        await ctx.Channel.SendMessageAsync(rep.Replace(voteMessage).SanitizeMentions());
                    }
                }

                break;
            }
            case null when string.IsNullOrWhiteSpace(voteMessage):
                await ctx.Channel.SendConfirmAsync("Using the default vote message.");
                return;
            case null:
                await ctx.Channel.SendConfirmAsync(voteMessage);
                break;
            case "-":
                await ctx.Channel.SendConfirmAsync("Switched to using the default embed.");
                await Service.SetVoteMessage(ctx.Guild.Id, "");
                break;
            default:
                await ctx.Channel.SendConfirmAsync("Vote message set.");
                await Service.SetVoteMessage(ctx.Guild.Id, message);
                break;
        }
    }

    /// <summary>
    /// Adds a role to be granted to users when they vote, with an optional duration for the role to be automatically removed.
    /// </summary>
    /// <param name="role">The role to add as a vote reward.</param>
    /// <param name="time">The duration for which the role will be granted. Null for indefinite.</param>
    /// <returns>A task that represents the asynchronous operation of adding a vote role.</returns>
    [Cmd, Aliases, UserPerm(GuildPermission.ManageGuild), RequireContext(ContextType.Guild)]
    public async Task VoteRoleAdd(IRole role, StoopidTime time = null)
    {
        if (time is not null)
        {
            var added = await Service.AddVoteRole(ctx.Guild.Id, role.Id, (int)time.Time.TotalSeconds);
            if (!added.Item1)
            {
                await ctx.Channel.SendErrorAsync(
                    $"Adding vote role failed for the following reason:\n{Format.Code(added.Item2)}", Config);
            }
            else
            {
                await ctx.Channel.SendConfirmAsync(
                    $"{role.Mention} added as a vote role and will last {time.Time.Humanize()} when a person votes.");
            }
        }
        else
        {
            var added = await Service.AddVoteRole(ctx.Guild.Id, role.Id);
            if (!added.Item1)
            {
                await ctx.Channel.SendErrorAsync(
                    $"Adding vote role failed for the following reason:\n{Format.Code(added.Item2)}", Config);
            }
            else
            {
                await ctx.Channel.SendConfirmAsync($"{role.Mention} added as a vote role.");
            }
        }
    }

    /// <summary>
    /// Removes a role from the list of roles to be granted to users when they vote.
    /// </summary>
    /// <param name="role">The role to remove from vote rewards.</param>
    /// <returns>A task that represents the asynchronous operation of removing a vote role.</returns>
    [Cmd, Aliases, UserPerm(GuildPermission.ManageGuild), RequireContext(ContextType.Guild)]
    public async Task VoteRoleRemove(IRole role)
    {
        var removed = await Service.RemoveVoteRole(ctx.Guild.Id, role.Id);
        if (removed.Item1)
            await ctx.Channel.SendConfirmAsync("Vote role removed.");
        else
            await ctx.Channel.SendErrorAsync(
                $"Vote role remove failed for the following reason:\n{Format.Code(removed.Item2)}", Config);
    }

    /// <summary>
    /// Lists all roles that are granted to users when they vote.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation of listing all vote roles.</returns>
    [Cmd, Aliases, UserPerm(GuildPermission.ManageGuild), RequireContext(ContextType.Guild)]
    public async Task VoteRolesList()
    {
        var roles = await Service.GetVoteRoles(ctx.Guild.Id);
        if (!roles.Any())
        {
            await ctx.Channel.SendErrorAsync("There are no vote roles set.", Config);
        }
        else
        {
            var eb = new EmbedBuilder()
                .WithTitle($"{roles.Count} Vote Roles")
                .WithOkColor()
                .WithDescription(string.Join("\n",
                    roles.Select(x =>
                        $"<@&{x.RoleId}>: {(x.Timer == 0 ? "No Timer." : $"{TimeSpan.FromSeconds(x.Timer).Humanize()}")}")));
            await ctx.Channel.SendMessageAsync(embed: eb.Build());
        }
    }

    /// <summary>
    /// Clears all roles that are granted to users when they vote.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation of clearing all vote roles.</returns>
    [Cmd, Aliases, UserPerm(GuildPermission.ManageGuild), RequireContext(ContextType.Guild)]
    public async Task VoteRolesClear()
    {
        if (await PromptUserConfirmAsync("Are you sure you want to clear all vote roles, cannot be undone!",
                ctx.User.Id))
        {
            var cleared = await Service.ClearVoteRoles(ctx.Guild.Id);
            if (cleared.Item1)
                await ctx.Channel.SendConfirmAsync("Vote roles cleared!");
            else
                await ctx.Channel.SendErrorAsync(
                    $"Vote roles not cleared for the following reason:\n{Format.Code(cleared.Item2)}", Config);
        }
    }

    /// <summary>
    /// Edits the duration for which a vote role is granted to users.
    /// </summary>
    /// <param name="role">The role to edit.</param>
    /// <param name="time">The new duration for the role to be granted.</param>
    /// <returns>A task that represents the asynchronous operation of editing a vote role's duration.</returns>
    [Cmd, Aliases, UserPerm(GuildPermission.ManageGuild),
     Discord.Interactions.RequireContext(Discord.Interactions.ContextType.Guild)]
    public async Task VoteRoleEdit(IRole role, StoopidTime time)
    {
        var update = await Service.UpdateTimer(role.Id, (int)time.Time.TotalSeconds);
        if (!update.Item1)
            await ctx.Channel.SendErrorAsync(
                $"Updating vote role time failed due to the following reason:\n{Format.Code(update.Item2)}", Config);
        else
            await ctx.Channel.SendConfirmAsync($"Successfuly updated the vote role time to {time.Time.Humanize()}");
    }

    /// <summary>
    /// Initiates a confirmation process for setting a new vote password, cautioning the user about its security.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation of setting a new vote password.</returns>
    [Cmd, Aliases, UserPerm(GuildPermission.ManageGuild), RequireContext(ContextType.Guild)]
    public async Task VotePassword()
    {
        if (await PromptUserConfirmAsync(
                "Please make absolute sure nobody else sees this password, otherwise this will lead to improper data. ***Mewdeko and it's team are not responsible for stolen passwords***. \n\nDo you agree to these terms?",
                ctx.User.Id))
        {
            var component = new ComponentBuilder().WithButton(
                "Press this to set the password. Remember, do not share it to anyone else.", "setvotepassword");
            await ctx.Channel.SendMessageAsync("_ _", components: component.Build());
        }
    }

    /// <summary>
    /// Displays the total number of votes cast by a specific user or the command invoker.
    /// </summary>
    /// <param name="user">The user whose votes to count. If null, counts votes for the command invoker.</param>
    /// <returns>A task that represents the asynchronous operation of displaying vote count.</returns>
    [Cmd, Aliases, RequireContext(ContextType.Guild)]
    public async Task Votes([Remainder] IUser user = null)
    {
        var curUser = user ?? ctx.User;
        await ctx.Channel.SendMessageAsync(embed: (await Service.GetTotalVotes(curUser, ctx.Guild)).Build());
    }

    /// <summary>
    /// Displays a leaderboard of users based on their vote counts, optionally filtering for votes within the current month.
    /// </summary>
    /// <param name="monthly">Whether to filter the leaderboard for the current month's votes.</param>
    /// <returns>A task that represents the asynchronous operation of displaying a vote leaderboard.</returns>
    [Cmd, Aliases, RequireContext(ContextType.Guild)]
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
                : "Not enough votes for a leaderboard.", Config);
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

        voteList = voteList.OrderByDescending(x => x.VoteCount).ToList();
        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(voteList.Count / 12)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await interactivity.SendPaginatorAsync(paginator, Context.Channel, TimeSpan.FromMinutes(60))
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