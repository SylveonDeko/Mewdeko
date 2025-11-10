using System.Text;
using Discord.Commands;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.CoprMonitoring.Common;
using Mewdeko.Modules.CoprMonitoring.Services;

namespace Mewdeko.Modules.CoprMonitoring;

/// <summary>
///     Commands for monitoring COPR builds and posting notifications to Discord.
/// </summary>
/// <param name="interactiveService">The interactive service for pagination.</param>
public partial class CoprMonitoring(InteractiveService interactiveService) : MewdekoModuleBase<CoprMonitoringService>
{
    /// <summary>
    ///     Adds a COPR project monitor to the server.
    /// </summary>
    /// <param name="ownerProject">The COPR project in format owner/project (e.g., linux4switch/l4s).</param>
    /// <param name="channel">Optional channel for notifications. Defaults to current channel.</param>
    /// <param name="packages">Optional comma-separated list of packages to monitor (e.g., mesa,kernel).</param>
    /// <remarks>
    ///     Configures the bot to monitor a COPR project and post build notifications to the specified channel.
    ///     Requires Manage Channels permission.
    ///     By default, only succeeded and failed builds are notified.
    ///     Use coprnotify command to configure which statuses trigger notifications.
    /// </remarks>
    /// <example>.coprmonitoradd linux4switch/l4s</example>
    /// <example>.copradd @copr/copr #builds</example>
    /// <example>.coprmonadd linux4switch/l4s #kernel mesa,kernel</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageChannels)]
    public async Task CoprMonitorAdd(string ownerProject, ITextChannel? channel = null,
        [Remainder] string? packages = null)
    {
        var parts = ownerProject.Split('/');
        if (parts.Length != 2)
        {
            await ctx.Channel.SendErrorAsync(Strings.CoprInvalidFormat(ctx.Guild.Id), Config);
            return;
        }

        var targetChannel = channel ?? (ITextChannel)ctx.Channel;
        var monitor = await Service.AddMonitor(ctx.Guild.Id, targetChannel.Id, parts[0], parts[1], packages);

        if (monitor == null)
        {
            await ctx.Channel.SendErrorAsync(Strings.CoprMonitorAlreadyExists(ctx.Guild.Id, parts[0], parts[1]),
                Config);
            return;
        }

        await ctx.Channel.SendConfirmAsync(Strings.CoprMonitorAdded(ctx.Guild.Id, parts[0], parts[1],
            targetChannel.Mention));
    }

    /// <summary>
    ///     Removes a COPR project monitor from the server.
    /// </summary>
    /// <param name="ownerProject">The COPR project in format owner/project.</param>
    /// <param name="channel">Optional channel to remove a specific monitor from.</param>
    /// <remarks>
    ///     Stops monitoring the specified COPR project. If a channel is specified, only removes the monitor for that channel.
    ///     Requires Manage Channels permission.
    /// </remarks>
    /// <example>.coprmonitorremove linux4switch/l4s</example>
    /// <example>.coprremove @copr/copr #builds</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageChannels)]
    public async Task CoprMonitorRemove(string ownerProject, ITextChannel? channel = null)
    {
        var parts = ownerProject.Split('/');
        if (parts.Length != 2)
        {
            await ctx.Channel.SendErrorAsync(Strings.CoprInvalidFormat(ctx.Guild.Id), Config);
            return;
        }

        var removed = await Service.RemoveMonitor(ctx.Guild.Id, parts[0], parts[1], channel?.Id);

        if (!removed)
        {
            await ctx.Channel.SendErrorAsync(Strings.CoprMonitorNotfound(ctx.Guild.Id, parts[0], parts[1]), Config);
            return;
        }

        await ctx.Channel.SendConfirmAsync(Strings.CoprMonitorRemoved(ctx.Guild.Id, parts[0], parts[1]));
    }

    /// <summary>
    ///     Lists all COPR monitors configured in the server.
    /// </summary>
    /// <remarks>
    ///     Displays all COPR projects being monitored, their target channels, and notification settings.
    /// </remarks>
    /// <example>.coprmonitorlist</example>
    /// <example>.coprlist</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    public async Task CoprMonitorList()
    {
        var monitors = await Service.GetMonitors(ctx.Guild.Id);

        if (monitors.Count == 0)
        {
            await ctx.Channel.SendErrorAsync(Strings.CoprMonitorListEmpty(ctx.Guild.Id), Config);
            return;
        }

        var paginator = new LazyPaginatorBuilder()
            .AddUser(ctx.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber | PaginatorFooter.Users)
            .WithMaxPageIndex(monitors.Count / 10)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .Build();

        await interactiveService.SendPaginatorAsync(paginator, ctx.Channel, TimeSpan.FromMinutes(5));

        async Task<PageBuilder> PageFactory(int page)
        {
            await Task.CompletedTask;

            var pageMonitors = monitors.Skip(page * 10).Take(10);
            var description = new StringBuilder();

            foreach (var monitor in pageMonitors)
            {
                var statusFlags = new List<string>();
                if (monitor.NotifyOnSucceeded) statusFlags.Add("✅");
                if (monitor.NotifyOnFailed) statusFlags.Add("❌");
                if (monitor.NotifyOnCanceled) statusFlags.Add("⚠️");
                if (monitor.NotifyOnRunning) statusFlags.Add("🔄");

                var packageInfo = string.IsNullOrWhiteSpace(monitor.PackageFilter)
                    ? "all packages"
                    : $"packages: {monitor.PackageFilter}";

                description.AppendLine(
                    $"**{monitor.CoprOwner}/{monitor.CoprProject}**\n" +
                    $"├ Channel: <#{monitor.ChannelId}>\n" +
                    $"├ Filter: {packageInfo}\n" +
                    $"├ Notifications: {string.Join(" ", statusFlags)}\n" +
                    $"└ Enabled: {(monitor.IsEnabled ? "✅" : "❌")}\n");
            }

            return new PageBuilder()
                .WithOkColor()
                .WithTitle(Strings.CoprMonitorListTitle(ctx.Guild.Id))
                .WithDescription(description.ToString());
        }
    }

    /// <summary>
    ///     Toggles a COPR monitor on or off.
    /// </summary>
    /// <param name="ownerProject">The COPR project in format owner/project.</param>
    /// <remarks>
    ///     Enables or disables notifications for the specified COPR project.
    ///     Requires Manage Channels permission.
    /// </remarks>
    /// <example>.coprmonitortoggle linux4switch/l4s</example>
    /// <example>.coprtoggle @copr/copr</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageChannels)]
    public async Task CoprMonitorToggle(string ownerProject)
    {
        var parts = ownerProject.Split('/');
        if (parts.Length != 2)
        {
            await ctx.Channel.SendErrorAsync(Strings.CoprInvalidFormat(ctx.Guild.Id), Config);
            return;
        }

        var newState = await Service.ToggleMonitor(ctx.Guild.Id, parts[0], parts[1]);

        if (newState == null)
        {
            await ctx.Channel.SendErrorAsync(Strings.CoprMonitorNotfound(ctx.Guild.Id, parts[0], parts[1]), Config);
            return;
        }

        await ctx.Channel.SendConfirmAsync(
            Strings.CoprMonitorToggled(ctx.Guild.Id, parts[0], parts[1], newState.Value ? "enabled" : "disabled"));
    }

    /// <summary>
    ///     Configures which build statuses trigger notifications for a monitor.
    /// </summary>
    /// <param name="ownerProject">The COPR project in format owner/project.</param>
    /// <param name="status">The build status to configure.</param>
    /// <param name="enabled">Whether to enable or disable notifications for this status.</param>
    /// <remarks>
    ///     Allows fine-grained control over which build states trigger Discord notifications.
    ///     Requires Manage Channels permission.
    /// </remarks>
    /// <example>.coprnotify linux4switch/l4s succeeded true</example>
    /// <example>.coprn linux4switch/l4s running false</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageChannels)]
    public async Task CoprNotify(string ownerProject, CoprBuildStatus status, bool enabled)
    {
        var parts = ownerProject.Split('/');
        if (parts.Length != 2)
        {
            await ctx.Channel.SendErrorAsync(Strings.CoprInvalidFormat(ctx.Guild.Id), Config);
            return;
        }

        var success = await Service.SetStatusNotification(ctx.Guild.Id, parts[0], parts[1], status, enabled);

        if (!success)
        {
            await ctx.Channel.SendErrorAsync(Strings.CoprMonitorNotfound(ctx.Guild.Id, parts[0], parts[1]), Config);
            return;
        }

        await ctx.Channel.SendConfirmAsync(
            Strings.CoprNotifyToggled(ctx.Guild.Id, parts[0], parts[1], status.ToDisplayString(),
                enabled ? "enabled" : "disabled"));
    }

    /// <summary>
    ///     Sets the package filter for a COPR monitor.
    /// </summary>
    /// <param name="ownerProject">The COPR project in format owner/project.</param>
    /// <param name="packages">Comma-separated list of packages, or "all" to monitor everything.</param>
    /// <remarks>
    ///     Filters notifications to only specific packages within a COPR project.
    ///     Use "all" to reset and monitor all packages.
    ///     Requires Manage Channels permission.
    /// </remarks>
    /// <example>.coprfilter linux4switch/l4s mesa,kernel</example>
    /// <example>.coprf linux4switch/l4s all</example>
    [Cmd]
    [Aliases]
    [RequireContext(ContextType.Guild)]
    [UserPerm(GuildPermission.ManageChannels)]
    public async Task CoprFilter(string ownerProject, [Remainder] string packages)
    {
        var parts = ownerProject.Split('/');
        if (parts.Length != 2)
        {
            await ctx.Channel.SendErrorAsync(Strings.CoprInvalidFormat(ctx.Guild.Id), Config);
            return;
        }

        var packageFilter = packages.Trim().Equals("all", StringComparison.OrdinalIgnoreCase)
            ? null
            : packages;

        var success = await Service.SetPackageFilter(ctx.Guild.Id, parts[0], parts[1], packageFilter);

        if (!success)
        {
            await ctx.Channel.SendErrorAsync(Strings.CoprMonitorNotfound(ctx.Guild.Id, parts[0], parts[1]), Config);
            return;
        }

        if (packageFilter == null)
        {
            await ctx.Channel.SendConfirmAsync(Strings.CoprFilterCleared(ctx.Guild.Id, parts[0], parts[1]));
        }
        else
        {
            await ctx.Channel.SendConfirmAsync(Strings.CoprFilterSet(ctx.Guild.Id, parts[0], parts[1], packageFilter));
        }
    }
}