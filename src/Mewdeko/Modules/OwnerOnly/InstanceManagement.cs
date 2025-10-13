using Discord.Commands;
using LinqToDB.Async;
using Mewdeko.Common.Attributes.TextCommands;
using Mewdeko.Controllers.Common.Bot;
using Mewdeko.Modules.OwnerOnly.Services;

namespace Mewdeko.Modules.OwnerOnly;

/// <summary>
///     Commands for managing bot instances that can be controlled via the dashboard.
///     These commands are only available to the bot owner.
/// </summary>
[OwnerOnly]
public class InstanceManagement(IDataConnectionFactory dbFactory) : MewdekoModuleBase<InstanceManagementService>
{
    /// <summary>
    ///     Adds a bot instance to be managed from the dashboard.
    /// </summary>
    /// <param name="instancePort">The port number the instance is running on</param>
    /// <remarks>Only valid port numbers (1024-65535) are accepted</remarks>
    [Cmd]
    [Aliases]
    [Summary("Adds a bot instance running on the specified port")]
    public async Task AddInstance(int instancePort)
    {
        if (instancePort is < 1024 or > 65535)
        {
            await ErrorAsync(Strings.InvalidPort(ctx.Guild.Id));
            return;
        }

        try
        {
            var (success, status, reason) = await Service.AddInstanceAsync(instancePort);
            if (success && status != null)
            {
                var eb = new EmbedBuilder()
                    .WithTitle($"{Strings.InstanceAdded(ctx.Guild.Id)} {status.BotName}")
                    .WithThumbnailUrl(status.BotAvatar)
                    .WithDescription(GetInstanceDescription(status))
                    .WithOkColor();

                await ctx.Channel.SendMessageAsync(embed: eb.Build());
            }
            else
            {
                await ErrorAsync(Strings.InstanceNotAdded(ctx.Guild.Id, reason));
            }
        }
        catch (Exception ex)
        {
            await ErrorAsync(Strings.ErrorAddingInstance(ctx.Guild.Id, ex.Message));
        }
    }

    /// <summary>
    ///     Lists all registered bot instances and their status.
    /// </summary>
    [Cmd]
    [Aliases]
    [Summary("Lists all registered bot instances")]
    public async Task ListInstances()
    {
        await using var db = await dbFactory.CreateConnectionAsync();
        var instances = await db.BotInstances.ToListAsync();

        if (instances.Count == 0)
        {
            await ReplyAsync(Strings.NoInstancesRegistered(ctx.Guild.Id));
            return;
        }

        var eb = new EmbedBuilder()
            .WithTitle(Strings.RegisteredInstances(ctx.Guild.Id))
            .WithOkColor();

        foreach (var instance in instances)
        {
            var status = await Service.GetInstanceStatusAsync(instance.Port);
            var statusEmoji = status != null ? "🟢" : "🔴";

            eb.AddField($"{statusEmoji} Port {instance.Port}",
                status != null
                    ? GetInstanceDescription(status)
                    : Strings.InstanceOffline(ctx.Guild.Id));
        }

        await ctx.Channel.SendMessageAsync(embed: eb.Build());
    }

    /// <summary>
    ///     Removes a bot instance from dashboard management.
    /// </summary>
    /// <param name="instancePort">The port number of the instance to remove</param>
    [Cmd]
    [Aliases]
    [Summary("Removes a bot instance")]
    public async Task RemoveInstance(int instancePort)
    {
        if (instancePort is < 1024 or > 65535)
        {
            await ErrorAsync(Strings.InvalidPort(ctx.Guild.Id));
            return;
        }

        var confirmMessage =
            await PromptUserConfirmAsync(Strings.RemoveInstanceConfirm(ctx.Guild.Id, instancePort), ctx.User.Id);
        if (!confirmMessage)
            return;

        try
        {
            var removed = await Service.RemoveInstanceAsync(instancePort);
            if (removed)
            {
                await ReplyConfirmAsync(Strings.InstanceRemoved(ctx.Guild.Id, instancePort));
            }
            else
            {
                await ErrorAsync(Strings.InstanceNotFound(ctx.Guild.Id, instancePort));
            }
        }
        catch (Exception ex)
        {
            await ErrorAsync(Strings.ErrorAddingInstance(ctx.Guild.Id, ex.Message));
        }
    }

    /// <summary>
    ///     Checks the status of a specific bot instance.
    /// </summary>
    /// <param name="instancePort">The port number of the instance to check</param>
    [Cmd]
    [Aliases]
    [Summary("Checks the status of a specific instance")]
    public async Task InstanceStatus(int instancePort)
    {
        if (instancePort is < 1024 or > 65535)
        {
            await ErrorAsync(Strings.InvalidPort(ctx.Guild.Id));
            return;
        }

        try
        {
            var status = await Service.GetInstanceStatusAsync(instancePort);
            if (status != null)
            {
                var eb = new EmbedBuilder()
                    .WithTitle($"{Strings.InstanceStatus(ctx.Guild.Id)} - Port {instancePort}")
                    .WithThumbnailUrl(status.BotAvatar)
                    .WithDescription(GetInstanceDescription(status))
                    .WithOkColor();

                await ctx.Channel.SendMessageAsync(embed: eb.Build());
            }
            else
            {
                await ErrorAsync(Strings.InstanceOffline(ctx.Guild.Id));
            }
        }
        catch (Exception ex)
        {
            await ErrorAsync(Strings.ErrorAddingInstance(ctx.Guild.Id, ex.Message));
        }
    }

    private string GetInstanceDescription(BotStatusModel status) =>
        $"{Strings.InstanceStatus(ctx.Guild.Id)} {status.BotStatus}\n" +
        $"{Strings.InstanceVersion(ctx.Guild.Id, status.BotVersion)}\n" +
        $"{Strings.InstanceCommandCount(ctx.Guild.Id, status.CommandsCount)}\n" +
        $"{Strings.InstanceModulesCount(ctx.Guild.Id, status.ModulesCount)}\n" +
        $"{Strings.InstanceUserCount(ctx.Guild.Id, status.UserCount)}";

    /// <summary>
    ///     Updates all registered bot instances.
    /// </summary>
    [Cmd]
    [Aliases]
    public async Task UpdateAllInstances()
    {
        var confirmMessage =
            await PromptUserConfirmAsync(Strings.UpdateAllInstancesConfirm(ctx.Guild.Id), ctx.User.Id);
        if (!confirmMessage)
            return;

        try
        {
            await ReplyAsync(Strings.UpdateAllInstancesTriggering(ctx.Guild.Id));

            var results = await Service.UpdateAllInstancesAsync();

            var eb = new EmbedBuilder()
                .WithTitle(Strings.UpdateAllInstancesResultsTitle(ctx.Guild.Id))
                .WithOkColor();

            foreach (var (port, (success, message)) in results)
            {
                var emoji = success ? "✅" : "❌";
                eb.AddField($"{emoji} Port {port}", message);
            }

            await ctx.Channel.SendMessageAsync(embed: eb.Build());
        }
        catch (Exception ex)
        {
            await ErrorAsync(Strings.UpdateAllInstancesFailed(ctx.Guild.Id, ex.Message));
        }
    }

    /// <summary>
    ///     Updates a specific bot instance.
    /// </summary>
    /// <param name="instancePort">The port number of the instance to update</param>
    [Cmd]
    [Aliases]
    public async Task UpdateInstance(int instancePort)
    {
        if (instancePort is < 1024 or > 65535)
        {
            await ErrorAsync(Strings.InvalidPort(ctx.Guild.Id));
            return;
        }

        var confirmMessage =
            await PromptUserConfirmAsync(Strings.UpdateInstanceConfirm(ctx.Guild.Id, instancePort), ctx.User.Id);
        if (!confirmMessage)
            return;

        try
        {
            await ReplyAsync(Strings.UpdateInstanceTriggering(ctx.Guild.Id, instancePort));

            var (success, message) = await Service.UpdateInstanceAsync(instancePort);

            if (success)
            {
                await SuccessAsync(Strings.UpdateInstanceSuccess(ctx.Guild.Id, instancePort, message));
            }
            else
            {
                await ErrorAsync(Strings.UpdateInstanceFailed(ctx.Guild.Id, instancePort, message));
            }
        }
        catch (Exception ex)
        {
            await ErrorAsync(Strings.UpdateInstanceError(ctx.Guild.Id, ex.Message));
        }
    }

    /// <summary>
    ///     Restarts all registered bot instances.
    /// </summary>
    [Cmd]
    [Aliases]
    public async Task RestartAllInstances()
    {
        var confirmMessage =
            await PromptUserConfirmAsync(Strings.RestartAllInstancesConfirm(ctx.Guild.Id), ctx.User.Id);
        if (!confirmMessage)
            return;

        try
        {
            await ReplyAsync(Strings.RestartAllInstancesTriggering(ctx.Guild.Id));

            var results = await Service.RestartAllInstancesAsync();

            var eb = new EmbedBuilder()
                .WithTitle(Strings.RestartAllInstancesResultsTitle(ctx.Guild.Id))
                .WithOkColor();

            foreach (var (port, (success, message)) in results)
            {
                var emoji = success ? "✅" : "❌";
                eb.AddField($"{emoji} Port {port}", message);
            }

            await ctx.Channel.SendMessageAsync(embed: eb.Build());
        }
        catch (Exception ex)
        {
            await ErrorAsync(Strings.RestartAllInstancesFailed(ctx.Guild.Id, ex.Message));
        }
    }

    /// <summary>
    ///     Restarts a specific bot instance.
    /// </summary>
    /// <param name="instancePort">The port number of the instance to restart</param>
    [Cmd]
    [Aliases]
    public async Task RestartInstance(int instancePort)
    {
        if (instancePort is < 1024 or > 65535)
        {
            await ErrorAsync(Strings.InvalidPort(ctx.Guild.Id));
            return;
        }

        var confirmMessage =
            await PromptUserConfirmAsync(Strings.RestartInstanceConfirm(ctx.Guild.Id, instancePort), ctx.User.Id);
        if (!confirmMessage)
            return;

        try
        {
            await ReplyAsync(Strings.RestartInstanceTriggering(ctx.Guild.Id, instancePort));

            var (success, message) = await Service.RestartInstanceAsync(instancePort);

            if (success)
            {
                await SuccessAsync(Strings.RestartInstanceSuccess(ctx.Guild.Id, instancePort, message));
            }
            else
            {
                await ErrorAsync(Strings.RestartInstanceFailed(ctx.Guild.Id, instancePort, message));
            }
        }
        catch (Exception ex)
        {
            await ErrorAsync(Strings.RestartInstanceError(ctx.Guild.Id, ex.Message));
        }
    }
}