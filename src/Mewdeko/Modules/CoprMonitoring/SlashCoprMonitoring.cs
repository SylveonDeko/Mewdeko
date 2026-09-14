using System.Text;
using DataModel;
using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Common.Modals;
using Mewdeko.Modules.CoprMonitoring.Common;
using Mewdeko.Modules.CoprMonitoring.Services;

namespace Mewdeko.Modules.CoprMonitoring;

/// <summary>
///     The notification template kinds a COPR monitor supports: one per build status plus the default fallback.
/// </summary>
public enum CoprTemplateKind
{
    /// <summary>
    ///     Template used when a build fails.
    /// </summary>
    Failed = 0,

    /// <summary>
    ///     Template used when a build succeeds.
    /// </summary>
    Succeeded = 1,

    /// <summary>
    ///     Template used when a build is canceled.
    /// </summary>
    Canceled = 2,

    /// <summary>
    ///     Template used when a build is running.
    /// </summary>
    Running = 3,

    /// <summary>
    ///     Template used when a build is pending.
    /// </summary>
    Pending = 4,

    /// <summary>
    ///     Template used when a build is skipped.
    /// </summary>
    Skipped = 5,

    /// <summary>
    ///     Template used when a build is starting.
    /// </summary>
    Starting = 6,

    /// <summary>
    ///     Template used when a build is importing sources.
    /// </summary>
    Importing = 7,

    /// <summary>
    ///     Template used when a build is forked.
    /// </summary>
    Forked = 8,

    /// <summary>
    ///     Template used when a build is waiting.
    /// </summary>
    Waiting = 9,

    /// <summary>
    ///     Fallback template used for statuses without a specific template.
    /// </summary>
    Default = 100
}

/// <summary>
///     Slash commands for monitoring COPR builds and posting notifications to Discord.
/// </summary>
/// <param name="interactiveService">The interactive service for pagination.</param>
[Group("copr", "Monitor COPR builds and post notifications")]
public class SlashCoprMonitoring(InteractiveService interactiveService)
    : MewdekoSlashModuleBase<CoprMonitoringService>
{
    /// <summary>
    ///     Adds a COPR project monitor to the server.
    /// </summary>
    /// <param name="ownerProject">The COPR project in format owner/project (e.g., linux4switch/l4s).</param>
    /// <param name="channel">Optional channel for notifications. Defaults to current channel.</param>
    /// <param name="packages">Optional comma-separated list of packages to monitor (e.g., mesa,kernel).</param>
    [SlashCommand("add", "Add a COPR project monitor")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    public async Task CoprMonitorAdd(
        [Summary("project", "The COPR project as owner/project")]
        string ownerProject,
        [Summary("channel", "Channel for notifications, defaults to this one")]
        ITextChannel? channel = null,
        [Summary("packages", "Comma separated packages to monitor")]
        string? packages = null)
    {
        var parts = ownerProject.Split('/');
        if (parts.Length != 2)
        {
            await ErrorAsync(Strings.CoprInvalidFormat(ctx.Guild.Id));
            return;
        }

        var targetChannel = channel ?? (ITextChannel)ctx.Channel;
        var monitor = await Service.AddMonitor(ctx.Guild.Id, targetChannel.Id, parts[0], parts[1], packages);

        if (monitor == null)
        {
            await ErrorAsync(Strings.CoprMonitorAlreadyExists(ctx.Guild.Id, parts[0], parts[1]));
            return;
        }

        await ConfirmAsync(Strings.CoprMonitorAdded(ctx.Guild.Id, parts[0], parts[1], targetChannel.Mention));
    }

    /// <summary>
    ///     Removes a COPR project monitor from the server.
    /// </summary>
    /// <param name="ownerProject">The COPR project in format owner/project.</param>
    /// <param name="channel">Optional channel to remove a specific monitor from.</param>
    [SlashCommand("remove", "Remove a COPR project monitor")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    public async Task CoprMonitorRemove(
        [Summary("project", "The COPR project as owner/project")]
        string ownerProject,
        [Summary("channel", "Only remove the monitor for this channel")]
        ITextChannel? channel = null)
    {
        var parts = ownerProject.Split('/');
        if (parts.Length != 2)
        {
            await ErrorAsync(Strings.CoprInvalidFormat(ctx.Guild.Id));
            return;
        }

        var removed = await Service.RemoveMonitor(ctx.Guild.Id, parts[0], parts[1], channel?.Id);

        if (!removed)
        {
            await ErrorAsync(Strings.CoprMonitorNotfound(ctx.Guild.Id, parts[0], parts[1]));
            return;
        }

        await ConfirmAsync(Strings.CoprMonitorRemoved(ctx.Guild.Id, parts[0], parts[1]));
    }

    /// <summary>
    ///     Lists all COPR monitors configured in the server.
    /// </summary>
    [SlashCommand("list", "List the COPR monitors in this server")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    public async Task CoprMonitorList()
    {
        await DeferAsync();
        var monitors = await Service.GetMonitors(ctx.Guild.Id);

        if (monitors.Count == 0)
        {
            await ErrorAsync(Strings.CoprMonitorListEmpty(ctx.Guild.Id));
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

        await interactiveService.SendPaginatorAsync(paginator, (ctx.Interaction as SocketInteraction)!,
            TimeSpan.FromMinutes(5), InteractionResponseType.DeferredChannelMessageWithSource);

        async Task<PageBuilder> PageFactory(int page)
        {
            await Task.CompletedTask;

            var pageMonitors = monitors.Skip(page * 10).Take(10);
            var description = new StringBuilder();

            foreach (var monitor in pageMonitors)
            {
                var statusFlags = new List<string>();
                if (monitor.NotifyOnSucceeded) statusFlags.Add(CoprBuildStatus.Succeeded.ToDisplayString());
                if (monitor.NotifyOnFailed) statusFlags.Add(CoprBuildStatus.Failed.ToDisplayString());
                if (monitor.NotifyOnCanceled) statusFlags.Add(CoprBuildStatus.Canceled.ToDisplayString());
                if (monitor.NotifyOnRunning) statusFlags.Add(CoprBuildStatus.Running.ToDisplayString());

                var packageInfo = string.IsNullOrWhiteSpace(monitor.PackageFilter)
                    ? "all packages"
                    : $"packages: {monitor.PackageFilter}";

                description.AppendLine(
                    $"**{monitor.CoprOwner}/{monitor.CoprProject}**\n" +
                    $"├ Channel: <#{monitor.ChannelId}>\n" +
                    $"├ Filter: {packageInfo}\n" +
                    $"├ Notifications: {string.Join(", ", statusFlags)}\n" +
                    $"└ Enabled: {(monitor.IsEnabled ? "yes" : "no")}\n");
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
    [SlashCommand("toggle", "Enable or disable a COPR monitor")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    public async Task CoprMonitorToggle(
        [Summary("project", "The COPR project as owner/project")]
        string ownerProject)
    {
        var parts = ownerProject.Split('/');
        if (parts.Length != 2)
        {
            await ErrorAsync(Strings.CoprInvalidFormat(ctx.Guild.Id));
            return;
        }

        var newState = await Service.ToggleMonitor(ctx.Guild.Id, parts[0], parts[1]);

        if (newState == null)
        {
            await ErrorAsync(Strings.CoprMonitorNotfound(ctx.Guild.Id, parts[0], parts[1]));
            return;
        }

        await ConfirmAsync(
            Strings.CoprMonitorToggled(ctx.Guild.Id, parts[0], parts[1], newState.Value ? "enabled" : "disabled"));
    }

    /// <summary>
    ///     Configures which build statuses trigger notifications for a monitor.
    /// </summary>
    /// <param name="ownerProject">The COPR project in format owner/project.</param>
    /// <param name="status">The build status to configure.</param>
    /// <param name="enabled">Whether to enable or disable notifications for this status.</param>
    [SlashCommand("notify", "Choose which build statuses send notifications")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    public async Task CoprNotify(
        [Summary("project", "The COPR project as owner/project")]
        string ownerProject,
        [Summary("status", "The build status")]
        CoprBuildStatus status,
        [Summary("enabled", "Whether to notify for this status")]
        bool enabled)
    {
        var parts = ownerProject.Split('/');
        if (parts.Length != 2)
        {
            await ErrorAsync(Strings.CoprInvalidFormat(ctx.Guild.Id));
            return;
        }

        var success = await Service.SetStatusNotification(ctx.Guild.Id, parts[0], parts[1], status, enabled);

        if (!success)
        {
            await ErrorAsync(Strings.CoprMonitorNotfound(ctx.Guild.Id, parts[0], parts[1]));
            return;
        }

        await ConfirmAsync(
            Strings.CoprNotifyToggled(ctx.Guild.Id, parts[0], parts[1], status.ToDisplayString(),
                enabled ? "enabled" : "disabled"));
    }

    /// <summary>
    ///     Sets the package filter for a COPR monitor.
    /// </summary>
    /// <param name="ownerProject">The COPR project in format owner/project.</param>
    /// <param name="packages">Comma-separated list of packages, or "all" to monitor everything.</param>
    [SlashCommand("filter", "Set the package filter for a COPR monitor")]
    [RequireContext(ContextType.Guild)]
    [CheckPermissions]
    [SlashUserPerm(GuildPermission.ManageChannels)]
    public async Task CoprFilter(
        [Summary("project", "The COPR project as owner/project")]
        string ownerProject,
        [Summary("packages", "Comma separated packages, or all")]
        string packages)
    {
        var parts = ownerProject.Split('/');
        if (parts.Length != 2)
        {
            await ErrorAsync(Strings.CoprInvalidFormat(ctx.Guild.Id));
            return;
        }

        var packageFilter = packages.Trim().Equals("all", StringComparison.OrdinalIgnoreCase)
            ? null
            : packages;

        var success = await Service.SetPackageFilter(ctx.Guild.Id, parts[0], parts[1], packageFilter);

        if (!success)
        {
            await ErrorAsync(Strings.CoprMonitorNotfound(ctx.Guild.Id, parts[0], parts[1]));
            return;
        }

        if (packageFilter == null)
        {
            await ConfirmAsync(Strings.CoprFilterCleared(ctx.Guild.Id, parts[0], parts[1]));
        }
        else
        {
            await ConfirmAsync(Strings.CoprFilterSet(ctx.Guild.Id, parts[0], parts[1], packageFilter));
        }
    }

    /// <summary>
    ///     Handles the submitted COPR template modal and stores the template for the given project and kind.
    /// </summary>
    /// <param name="ownerProject">The COPR project in format owner/project.</param>
    /// <param name="kind">The template kind name.</param>
    /// <param name="modal">The submitted modal containing the template.</param>
    [ModalInteraction("copr_template:*:*", true)]
    [RequireContext(ContextType.Guild)]
    [SlashUserPerm(GuildPermission.Administrator)]
    public async Task CoprTemplateSubmitted(string ownerProject, string kind, CoprTemplateModal modal)
    {
        var parts = ownerProject.Split('/');
        if (parts.Length != 2 || !Enum.TryParse<CoprTemplateKind>(kind, true, out var templateKind))
        {
            await ErrorAsync(Strings.CoprInvalidFormat(ctx.Guild.Id));
            return;
        }

        var message = string.IsNullOrWhiteSpace(modal.Template) ? "-" : modal.Template;
        var success = await CoprTemplates.ApplyTemplateAsync(Service, ctx.Guild.Id, parts[0], parts[1],
            templateKind, message);

        if (!success)
        {
            await ErrorAsync(Strings.CoprMonitorNotfound(ctx.Guild.Id, parts[0], parts[1]));
            return;
        }

        await ConfirmAsync(message == "-"
            ? Strings.CoprTemplateReset(ctx.Guild.Id, templateKind.ToString())
            : Strings.CoprTemplateUpdated(ctx.Guild.Id, templateKind.ToString()));
    }

    /// <summary>
    ///     Helpers shared by the template commands and the modal handler.
    /// </summary>
    public static class CoprTemplates
    {
        /// <summary>
        ///     Stores a template for the given kind, routing the default kind to the default message.
        /// </summary>
        /// <param name="service">The COPR monitoring service.</param>
        /// <param name="guildId">The guild id.</param>
        /// <param name="owner">The COPR project owner.</param>
        /// <param name="project">The COPR project name.</param>
        /// <param name="kind">The template kind.</param>
        /// <param name="message">The template text, or "-" to reset.</param>
        /// <returns>True when the monitor exists and was updated.</returns>
        public static Task<bool> ApplyTemplateAsync(CoprMonitoringService service, ulong guildId, string owner,
            string project, CoprTemplateKind kind, string message)
        {
            return kind == CoprTemplateKind.Default
                ? service.SetDefaultMessage(guildId, owner, project, message)
                : service.SetStatusMessage(guildId, owner, project, (CoprBuildStatus)(int)kind, message);
        }

        /// <summary>
        ///     Reads the stored template of the given kind from a monitor.
        /// </summary>
        /// <param name="monitor">The monitor to read from.</param>
        /// <param name="kind">The template kind.</param>
        /// <returns>The stored template or null.</returns>
        public static string? ReadTemplate(CoprMonitor monitor, CoprTemplateKind kind)
        {
            return kind switch
            {
                CoprTemplateKind.Failed => monitor.FailedMessage,
                CoprTemplateKind.Succeeded => monitor.SucceededMessage,
                CoprTemplateKind.Canceled => monitor.CanceledMessage,
                CoprTemplateKind.Running => monitor.RunningMessage,
                CoprTemplateKind.Pending => monitor.PendingMessage,
                CoprTemplateKind.Skipped => monitor.SkippedMessage,
                CoprTemplateKind.Starting => monitor.StartingMessage,
                CoprTemplateKind.Importing => monitor.ImportingMessage,
                CoprTemplateKind.Forked => monitor.ForkedMessage,
                CoprTemplateKind.Waiting => monitor.WaitingMessage,
                _ => monitor.DefaultMessage
            };
        }
    }

    /// <summary>
    ///     Customization commands for COPR monitoring notification templates.
    /// </summary>
    [Group("template", "Customize COPR build notification templates")]
    public class CoprTemplate : MewdekoSlashSubmodule<CoprMonitoringService>
    {
        /// <summary>
        ///     Opens a modal to set the notification template for a build status. Submitting an empty template resets it.
        /// </summary>
        /// <param name="ownerProject">The COPR project in format owner/project.</param>
        /// <param name="kind">The build status the template applies to.</param>
        [SlashCommand("set", "Set the notification template for a build status")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task TemplateSet(
            [Summary("project", "The COPR project as owner/project")]
            string ownerProject,
            [Summary("status", "The build status")]
            CoprTemplateKind kind)
        {
            var parts = ownerProject.Split('/');
            if (parts.Length != 2)
            {
                await ErrorAsync(Strings.CoprInvalidFormat(ctx.Guild.Id));
                return;
            }

            var monitor = await Service.GetMonitor(ctx.Guild.Id, parts[0], parts[1]);
            if (monitor == null)
            {
                await ErrorAsync(Strings.CoprMonitorNotfound(ctx.Guild.Id, parts[0], parts[1]));
                return;
            }

            await RespondWithModalAsync<CoprTemplateModal>($"copr_template:{ownerProject}:{kind}");
        }

        /// <summary>
        ///     Shows the stored notification template for a build status.
        /// </summary>
        /// <param name="ownerProject">The COPR project in format owner/project.</param>
        /// <param name="kind">The build status to show the template for.</param>
        [SlashCommand("show", "Show the notification template for a build status")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task TemplateShow(
            [Summary("project", "The COPR project as owner/project")]
            string ownerProject,
            [Summary("status", "The build status")]
            CoprTemplateKind kind)
        {
            var parts = ownerProject.Split('/');
            if (parts.Length != 2)
            {
                await ErrorAsync(Strings.CoprInvalidFormat(ctx.Guild.Id));
                return;
            }

            var monitor = await Service.GetMonitor(ctx.Guild.Id, parts[0], parts[1]);
            if (monitor == null)
            {
                await ErrorAsync(Strings.CoprMonitorNotfound(ctx.Guild.Id, parts[0], parts[1]));
                return;
            }

            var template = CoprTemplates.ReadTemplate(monitor, kind);
            if (string.IsNullOrWhiteSpace(template) || template == "-")
            {
                await ConfirmAsync(Strings.CoprTemplateNone(ctx.Guild.Id, kind.ToString()));
                return;
            }

            var embed = new EmbedBuilder()
                .WithOkColor()
                .WithTitle(Strings.CoprTemplateShowTitle(ctx.Guild.Id, parts[0], parts[1], kind.ToString()))
                .WithDescription(Format.Code(template.TrimTo(4000), string.Empty));

            await ctx.Interaction.RespondAsync(embed: embed.Build());
        }

        /// <summary>
        ///     Resets the notification template for a build status to the default format.
        /// </summary>
        /// <param name="ownerProject">The COPR project in format owner/project.</param>
        /// <param name="kind">The build status to reset the template for.</param>
        [SlashCommand("reset", "Reset the notification template for a build status")]
        [RequireContext(ContextType.Guild)]
        [CheckPermissions]
        [SlashUserPerm(GuildPermission.Administrator)]
        public async Task TemplateReset(
            [Summary("project", "The COPR project as owner/project")]
            string ownerProject,
            [Summary("status", "The build status")]
            CoprTemplateKind kind)
        {
            var parts = ownerProject.Split('/');
            if (parts.Length != 2)
            {
                await ErrorAsync(Strings.CoprInvalidFormat(ctx.Guild.Id));
                return;
            }

            var success = await CoprTemplates.ApplyTemplateAsync(Service, ctx.Guild.Id, parts[0], parts[1], kind,
                "-");

            if (!success)
            {
                await ErrorAsync(Strings.CoprMonitorNotfound(ctx.Guild.Id, parts[0], parts[1]));
                return;
            }

            await ConfirmAsync(Strings.CoprTemplateReset(ctx.Guild.Id, kind.ToString()));
        }
    }
}