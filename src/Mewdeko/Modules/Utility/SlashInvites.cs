using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Modules.Utility.Services;

namespace Mewdeko.Modules.Utility;

/// <summary>
///     Provides slash commands for managing and viewing invite-related information.
/// </summary>
[Group("invites", "Invite tracking and leaderboards")]
public class SlashInvites(InteractiveService interactivity) : MewdekoSlashModuleBase<InviteCountService>
{
    /// <summary>
    ///     Displays the number of invites for a user.
    /// </summary>
    /// <param name="user">The user to check invites for. If null, checks for the command user.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("list", "Shows how many invites a user has")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task Invites(IUser? user = null)
    {
        user ??= ctx.User;
        var invites = await Service.GetInviteCount(user.Id, ctx.Guild.Id);
        await ReplyConfirmAsync(Strings.UserInviteCount(ctx.Guild.Id, user, invites));
    }

    /// <summary>
    ///     Displays the current invite settings for the guild.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("settings", "Shows the invite tracking settings")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task InviteSettings()
    {
        var settings = await Service.GetInviteCountSettingsAsync(ctx.Guild.Id);
        await ReplyConfirmAsync(Strings.InviteSettings(ctx.Guild.Id,
            GetEnDis(settings.IsEnabled),
            GetEnDis(settings.RemoveInviteOnLeave),
            settings.MinAccountAge));
    }

    /// <summary>
    ///     Toggles invite tracking for the guild.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("toggle-tracking", "Enables or disables invite tracking")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task ToggleInviteTracking()
    {
        var newState = await Service.SetInviteTrackingEnabledAsync(ctx.Guild.Id,
            !(await Service.GetInviteCountSettingsAsync(ctx.Guild.Id)).IsEnabled);
        await ReplyConfirmAsync(newState
            ? Strings.InviteTrackingEnabled(ctx.Guild.Id)
            : Strings.InviteTrackingDisabled(ctx.Guild.Id));
    }

    /// <summary>
    ///     Toggles whether invites should be removed when a user leaves the guild.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("toggle-remove-on-leave", "Toggles removing invites when the invited user leaves")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task ToggleRemoveInviteOnLeave()
    {
        var newState = await Service.SetRemoveInviteOnLeaveAsync(ctx.Guild.Id,
            !(await Service.GetInviteCountSettingsAsync(ctx.Guild.Id)).RemoveInviteOnLeave);
        await ReplyConfirmAsync(newState
            ? Strings.RemoveInviteOnLeaveEnabled(ctx.Guild.Id)
            : Strings.RemoveInviteOnLeaveDisabled(ctx.Guild.Id));
    }

    /// <summary>
    ///     Sets the minimum account age required for an invite to be counted.
    /// </summary>
    /// <param name="days">The minimum age in days.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("min-account-age", "Sets the minimum account age in days for an invite to count")]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.ManageGuild)]
    public async Task SetMinAccountAge([Summary("days", "Minimum account age in days")] int days)
    {
        var minAge = TimeSpan.FromDays(days);
        await Service.SetMinAccountAgeAsync(ctx.Guild.Id, minAge);
        await ReplyConfirmAsync(Strings.MinAccountAgeSet(ctx.Guild.Id, days));
    }

    /// <summary>
    ///     Displays a leaderboard of users with the most invites.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("leaderboard", "Shows the users with the most invites")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task InviteLeaderboard()
    {
        await DeferAsync();
        var leaderboard = await Service.GetInviteLeaderboardAsync(ctx.Guild);

        if (leaderboard.Count == 0)
        {
            await ReplyErrorAsync(Strings.NoInviteData(ctx.Guild.Id));
            return;
        }

        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(leaderboard.Count / 20)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await interactivity.SendPaginatorAsync(paginator, (ctx.Interaction as SocketInteraction)!,
                TimeSpan.FromMinutes(60), InteractionResponseType.DeferredChannelMessageWithSource)
            .ConfigureAwait(false);
        return;

        async Task<PageBuilder> PageFactory(int page)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            return new PageBuilder().WithOkColor()
                .WithTitle(Strings.InviteLeaderboardTitle(ctx.Guild.Id))
                .WithDescription(string.Join("\n", leaderboard.Skip(page * 20).Take(20)
                    .Select((x, i) => $"{i + 1 + page * 20}. {x.Username} - {x.InviteCount} invites")));
        }
    }

    /// <summary>
    ///     Displays who invited a specific user to the guild.
    /// </summary>
    /// <param name="user">The user to check. If null, checks for the command user.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("who-invited", "Shows who invited a user")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task WhoInvited(IUser? user = null)
    {
        user ??= ctx.User;
        var inviter = await Service.GetInviter(user.Id, ctx.Guild);

        if (inviter == null)
            await ReplyErrorAsync(Strings.NoInviterFound(ctx.Guild.Id, user.Username));
        else
            await ReplyConfirmAsync(Strings.InviterFound(ctx.Guild.Id, user.Username, inviter.Username));
    }

    /// <summary>
    ///     Displays a list of users invited by a specific user.
    /// </summary>
    /// <param name="user">The user whose invites to check. If null, checks for the command user.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [SlashCommand("invited-users", "Lists the users invited by a user")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task InvitedUsers(IUser? user = null)
    {
        await DeferAsync();
        user ??= ctx.User;
        var invitedUsers = await Service.GetInvitedUsers(user.Id, ctx.Guild);

        if (invitedUsers.Count == 0)
        {
            await ReplyErrorAsync(Strings.NoInvitedUsers(ctx.Guild.Id, user.Username));
            return;
        }

        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(invitedUsers.Count / 20)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await interactivity.SendPaginatorAsync(paginator, (ctx.Interaction as SocketInteraction)!,
                TimeSpan.FromMinutes(60), InteractionResponseType.DeferredChannelMessageWithSource)
            .ConfigureAwait(false);
        return;

        async Task<PageBuilder> PageFactory(int page)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            return new PageBuilder().WithOkColor()
                .WithTitle(Strings.InvitedUsersTitle(ctx.Guild.Id, user.Username))
                .WithDescription(string.Join("\n",
                    invitedUsers.Skip(page * 20).Take(20).Select(x => x.ToString())));
        }
    }

    private static string GetEnDis(bool endis)
    {
        return endis ? "Enabled" : "Disabled";
    }
}