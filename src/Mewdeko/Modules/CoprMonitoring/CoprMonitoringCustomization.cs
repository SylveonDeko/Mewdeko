using Discord.Commands;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Modules.CoprMonitoring.Common;
using Mewdeko.Modules.CoprMonitoring.Services;

namespace Mewdeko.Modules.CoprMonitoring;

public partial class CoprMonitoring
{
    /// <summary>
    ///     Customization commands for COPR monitoring notifications.
    /// </summary>
    [Group]
    public class CoprMonitoringCustomization : MewdekoModuleBase<CoprMonitoringService>
    {
        /// <summary>
        ///     Sets the message displayed when a COPR build succeeds.
        /// </summary>
        /// <param name="ownerProject">The COPR project in format owner/project.</param>
        /// <param name="message">The message or embed code. Use "-" to reset to default.</param>
        /// <remarks>
        ///     Allows customization of build success notifications. Providing "-" resets to default.
        ///     Requires Administrator permissions.
        ///     Available placeholders: %copr.owner%, %copr.project%, %copr.package%, %copr.buildid%, %copr.chroot%, %copr.status%,
        ///     %copr.url%, %copr.version%, %copr.user%, %copr.emote%
        ///     Plus all server placeholders: %server.name%, %server.id%, etc.
        /// </remarks>
        /// <example>.coprsucceededmessage linux4switch/l4s -</example>
        /// <example>.coprsuccessmsg linux4switch/l4s {"description": "%copr.emote% Build **%copr.package%** succeeded!"}</example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task CoprSucceededMessage(string ownerProject, [Remainder] string message)
        {
            var parts = ownerProject.Split('/');
            if (parts.Length != 2)
            {
                await ctx.Channel.SendErrorAsync(Strings.CoprInvalidFormat(ctx.Guild.Id), Config);
                return;
            }

            var success =
                await Service.SetStatusMessage(ctx.Guild.Id, parts[0], parts[1], CoprBuildStatus.Succeeded, message);

            if (!success)
            {
                await ctx.Channel.SendErrorAsync(Strings.CoprMonitorNotfound(ctx.Guild.Id, parts[0], parts[1]), Config);
                return;
            }

            if (message == "-")
            {
                await ctx.Channel.SendConfirmAsync(Strings.CoprSucceededMessageDefault(ctx.Guild.Id));
            }
            else
            {
                await ctx.Channel.SendConfirmAsync(Strings.CoprSucceededMessageUpdated(ctx.Guild.Id));
            }
        }

        /// <summary>
        ///     Sets the message displayed when a COPR build fails.
        /// </summary>
        /// <param name="ownerProject">The COPR project in format owner/project.</param>
        /// <param name="message">The message or embed code. Use "-" to reset to default.</param>
        /// <remarks>
        ///     Allows customization of build failure notifications. Providing "-" resets to default.
        ///     Requires Administrator permissions.
        ///     Available placeholders: %copr.owner%, %copr.project%, %copr.package%, %copr.buildid%, %copr.chroot%, %copr.status%,
        ///     %copr.url%, %copr.version%, %copr.user%, %copr.emote%
        ///     Plus all server placeholders: %server.name%, %server.id%, etc.
        /// </remarks>
        /// <example>.coprfailedmessage linux4switch/l4s -</example>
        /// <example>
        ///     .coprfailmsg linux4switch/l4s {"description": "%copr.emote% Build **%copr.package%** failed!\n[View
        ///     Logs](%copr.url%)", "color": "#ff0000"}
        /// </example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task CoprFailedMessage(string ownerProject, [Remainder] string message)
        {
            var parts = ownerProject.Split('/');
            if (parts.Length != 2)
            {
                await ctx.Channel.SendErrorAsync(Strings.CoprInvalidFormat(ctx.Guild.Id), Config);
                return;
            }

            var success =
                await Service.SetStatusMessage(ctx.Guild.Id, parts[0], parts[1], CoprBuildStatus.Failed, message);

            if (!success)
            {
                await ctx.Channel.SendErrorAsync(Strings.CoprMonitorNotfound(ctx.Guild.Id, parts[0], parts[1]), Config);
                return;
            }

            if (message == "-")
            {
                await ctx.Channel.SendConfirmAsync(Strings.CoprFailedMessageDefault(ctx.Guild.Id));
            }
            else
            {
                await ctx.Channel.SendConfirmAsync(Strings.CoprFailedMessageUpdated(ctx.Guild.Id));
            }
        }

        /// <summary>
        ///     Sets the message displayed when a COPR build is canceled.
        /// </summary>
        /// <param name="ownerProject">The COPR project in format owner/project.</param>
        /// <param name="message">The message or embed code. Use "-" to reset to default.</param>
        /// <remarks>
        ///     Allows customization of build cancellation notifications. Providing "-" resets to default.
        ///     Requires Administrator permissions.
        ///     Available placeholders: %copr.owner%, %copr.project%, %copr.package%, %copr.buildid%, %copr.chroot%, %copr.status%,
        ///     %copr.url%, %copr.version%, %copr.user%, %copr.emote%
        ///     Plus all server placeholders: %server.name%, %server.id%, etc.
        /// </remarks>
        /// <example>.coprcanceledmessage linux4switch/l4s -</example>
        /// <example>.coprcancelmsg linux4switch/l4s Build %copr.package% was canceled.</example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task CoprCanceledMessage(string ownerProject, [Remainder] string message)
        {
            var parts = ownerProject.Split('/');
            if (parts.Length != 2)
            {
                await ctx.Channel.SendErrorAsync(Strings.CoprInvalidFormat(ctx.Guild.Id), Config);
                return;
            }

            var success =
                await Service.SetStatusMessage(ctx.Guild.Id, parts[0], parts[1], CoprBuildStatus.Canceled, message);

            if (!success)
            {
                await ctx.Channel.SendErrorAsync(Strings.CoprMonitorNotfound(ctx.Guild.Id, parts[0], parts[1]), Config);
                return;
            }

            if (message == "-")
            {
                await ctx.Channel.SendConfirmAsync(Strings.CoprCanceledMessageDefault(ctx.Guild.Id));
            }
            else
            {
                await ctx.Channel.SendConfirmAsync(Strings.CoprCanceledMessageUpdated(ctx.Guild.Id));
            }
        }

        /// <summary>
        ///     Sets the message displayed when a COPR build is pending.
        /// </summary>
        /// <param name="ownerProject">The COPR project in format owner/project.</param>
        /// <param name="message">The message or embed code. Use "-" to reset to default.</param>
        /// <remarks>
        ///     Allows customization of pending build notifications. Providing "-" resets to default.
        ///     Requires Administrator permissions.
        ///     Available placeholders: %copr.owner%, %copr.project%, %copr.package%, %copr.buildid%, %copr.chroot%, %copr.status%,
        ///     %copr.url%, %copr.version%, %copr.user%, %copr.emote%
        ///     Plus all server placeholders: %server.name%, %server.id%, etc.
        /// </remarks>
        /// <example>.coprpendingmessage linux4switch/l4s -</example>
        /// <example>.coprpendmsg linux4switch/l4s Build queued for %copr.package%</example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task CoprPendingMessage(string ownerProject, [Remainder] string message)
        {
            var parts = ownerProject.Split('/');
            if (parts.Length != 2)
            {
                await ctx.Channel.SendErrorAsync(Strings.CoprInvalidFormat(ctx.Guild.Id), Config);
                return;
            }

            var success =
                await Service.SetStatusMessage(ctx.Guild.Id, parts[0], parts[1], CoprBuildStatus.Pending, message);

            if (!success)
            {
                await ctx.Channel.SendErrorAsync(Strings.CoprMonitorNotfound(ctx.Guild.Id, parts[0], parts[1]), Config);
                return;
            }

            if (message == "-")
            {
                await ctx.Channel.SendConfirmAsync(Strings.CoprPendingMessageDefault(ctx.Guild.Id));
            }
            else
            {
                await ctx.Channel.SendConfirmAsync(Strings.CoprPendingMessageUpdated(ctx.Guild.Id));
            }
        }

        /// <summary>
        ///     Sets the message displayed when a COPR build is running.
        /// </summary>
        /// <param name="ownerProject">The COPR project in format owner/project.</param>
        /// <param name="message">The message or embed code. Use "-" to reset to default.</param>
        /// <remarks>
        ///     Allows customization of running build notifications. Providing "-" resets to default.
        ///     Requires Administrator permissions.
        ///     Available placeholders: %copr.owner%, %copr.project%, %copr.package%, %copr.buildid%, %copr.chroot%, %copr.status%,
        ///     %copr.url%, %copr.version%, %copr.user%, %copr.emote%
        ///     Plus all server placeholders: %server.name%, %server.id%, etc.
        /// </remarks>
        /// <example>.coprrunningmessage linux4switch/l4s -</example>
        /// <example>.coprrunmsg linux4switch/l4s %copr.emote% Building %copr.package%...</example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task CoprRunningMessage(string ownerProject, [Remainder] string message)
        {
            var parts = ownerProject.Split('/');
            if (parts.Length != 2)
            {
                await ctx.Channel.SendErrorAsync(Strings.CoprInvalidFormat(ctx.Guild.Id), Config);
                return;
            }

            var success =
                await Service.SetStatusMessage(ctx.Guild.Id, parts[0], parts[1], CoprBuildStatus.Running, message);

            if (!success)
            {
                await ctx.Channel.SendErrorAsync(Strings.CoprMonitorNotfound(ctx.Guild.Id, parts[0], parts[1]), Config);
                return;
            }

            if (message == "-")
            {
                await ctx.Channel.SendConfirmAsync(Strings.CoprRunningMessageDefault(ctx.Guild.Id));
            }
            else
            {
                await ctx.Channel.SendConfirmAsync(Strings.CoprRunningMessageUpdated(ctx.Guild.Id));
            }
        }

        /// <summary>
        ///     Sets the message displayed when a COPR build is skipped.
        /// </summary>
        /// <param name="ownerProject">The COPR project in format owner/project.</param>
        /// <param name="message">The message or embed code. Use "-" to reset to default.</param>
        /// <remarks>
        ///     Allows customization of skipped build notifications. Providing "-" resets to default.
        ///     Requires Administrator permissions.
        ///     Available placeholders: %copr.owner%, %copr.project%, %copr.package%, %copr.buildid%, %copr.chroot%, %copr.status%,
        ///     %copr.url%, %copr.version%, %copr.user%, %copr.emote%
        ///     Plus all server placeholders: %server.name%, %server.id%, etc.
        /// </remarks>
        /// <example>.coprskippedmessage linux4switch/l4s -</example>
        /// <example>.coprskipmsg linux4switch/l4s Build %copr.package% was skipped.</example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task CoprSkippedMessage(string ownerProject, [Remainder] string message)
        {
            var parts = ownerProject.Split('/');
            if (parts.Length != 2)
            {
                await ctx.Channel.SendErrorAsync(Strings.CoprInvalidFormat(ctx.Guild.Id), Config);
                return;
            }

            var success =
                await Service.SetStatusMessage(ctx.Guild.Id, parts[0], parts[1], CoprBuildStatus.Skipped, message);

            if (!success)
            {
                await ctx.Channel.SendErrorAsync(Strings.CoprMonitorNotfound(ctx.Guild.Id, parts[0], parts[1]), Config);
                return;
            }

            if (message == "-")
            {
                await ctx.Channel.SendConfirmAsync(Strings.CoprSkippedMessageDefault(ctx.Guild.Id));
            }
            else
            {
                await ctx.Channel.SendConfirmAsync(Strings.CoprSkippedMessageUpdated(ctx.Guild.Id));
            }
        }

        /// <summary>
        ///     Sets the default message for all other build statuses.
        /// </summary>
        /// <param name="ownerProject">The COPR project in format owner/project.</param>
        /// <param name="message">The message or embed code. Use "-" to reset to default.</param>
        /// <remarks>
        ///     Sets a fallback message for build statuses that don't have a specific custom message configured.
        ///     Providing "-" resets to default.
        ///     Requires Administrator permissions.
        ///     Available placeholders: %copr.owner%, %copr.project%, %copr.package%, %copr.buildid%, %copr.chroot%, %copr.status%,
        ///     %copr.url%, %copr.version%, %copr.user%, %copr.emote%
        ///     Plus all server placeholders: %server.name%, %server.id%, etc.
        /// </remarks>
        /// <example>.coprdefaultmessage linux4switch/l4s -</example>
        /// <example>.coprdefmsg linux4switch/l4s Build update: %copr.package% - %copr.status%</example>
        [Cmd]
        [Aliases]
        [RequireContext(ContextType.Guild)]
        [UserPerm(GuildPermission.Administrator)]
        public async Task CoprDefaultMessage(string ownerProject, [Remainder] string message)
        {
            var parts = ownerProject.Split('/');
            if (parts.Length != 2)
            {
                await ctx.Channel.SendErrorAsync(Strings.CoprInvalidFormat(ctx.Guild.Id), Config);
                return;
            }

            var success = await Service.SetDefaultMessage(ctx.Guild.Id, parts[0], parts[1], message);

            if (!success)
            {
                await ctx.Channel.SendErrorAsync(Strings.CoprMonitorNotfound(ctx.Guild.Id, parts[0], parts[1]), Config);
                return;
            }

            if (message == "-")
            {
                await ctx.Channel.SendConfirmAsync(Strings.CoprDefaultMessageDefault(ctx.Guild.Id));
            }
            else
            {
                await ctx.Channel.SendConfirmAsync(Strings.CoprDefaultMessageUpdated(ctx.Guild.Id));
            }
        }
    }
}