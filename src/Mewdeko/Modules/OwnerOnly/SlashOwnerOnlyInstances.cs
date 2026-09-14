using System.IO;
using Discord.Interactions;
using LibGit2Sharp;
using LinqToDB.Async;
using Mewdeko.Common.Attributes.InteractionCommands;
using Mewdeko.Controllers.Common.Bot;
using Mewdeko.Modules.OwnerOnly.Services;
using Mewdeko.Services.Impl;
using Mewdeko.Services.Settings;
using Swan.Formatters;

namespace Mewdeko.Modules.OwnerOnly;

public partial class SlashOwnerOnly
{
    /// <summary>
    ///     Updates the bot to the latest version available on the repository for the chosen branch.
    /// </summary>
    /// <param name="branch">The branch to update to, stable or nightly.</param>
    [SlashCommand("update", "Updates the bot to the latest version on the repository")]
    [SlashOwnerOnly]
    [RequireContext(ContextType.Guild)]
    public async Task Update(
        [Summary("branch", "The branch to update to")] [Choice("Stable", "main")] [Choice("Nightly", "psqldeko")]
        string branch)
    {
        await DeferAsync().ConfigureAwait(false);

        var updatingEmbed = new EmbedBuilder()
            .WithColor(Color.Blue)
            .WithDescription(Strings.UpdateInProgress(ctx.Guild.Id, branch))
            .Build();
        await ctx.Interaction.FollowupAsync(embed: updatingEmbed).ConfigureAwait(false);

        try
        {
            var repoPath = Directory.GetCurrentDirectory();
            var discovered = Repository.Discover(repoPath);

            if (string.IsNullOrWhiteSpace(discovered))
            {
                throw new Exception(Strings.InvalidRepoPath(ctx.Guild.Id));
            }

            using var repo = new Repository(discovered);

            var remote = repo.Network.Remotes["origin"];
            var refSpecs = remote.FetchRefSpecs.Select(x => x.Specification);
            Commands.Fetch(repo, remote.Name, refSpecs, null, "");

            var currentBranch = repo.Head.FriendlyName;
            if (currentBranch != branch)
            {
                Commands.Checkout(repo, branch);
            }

            var options = new PullOptions
            {
                FetchOptions = new FetchOptions(),
                MergeOptions = new MergeOptions
                {
                    FailOnConflict = true
                }
            };

            var signature = new Signature(new Identity("Mewdeko", "mewdeko@mewdeko.tech"), DateTimeOffset.Now);
            var result = Commands.Pull(repo, signature, options);

            var successEmbed = new EmbedBuilder()
                .WithColor(Color.Green)
                .WithDescription(Strings.UpdateComplete(ctx.Guild.Id))
                .AddField("Branch", branch, true)
                .AddField("Status", result.Status, true)
                .Build();
            await ctx.Interaction.ModifyOriginalResponseAsync(x => x.Embed = successEmbed).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            var errorEmbed = new EmbedBuilder()
                .WithColor(Color.Red)
                .WithTitle(Strings.UpdateFailed(ctx.Guild.Id))
                .WithDescription(Strings.UpdateError(ctx.Guild.Id, ex.Message))
                .Build();
            await ctx.Interaction.ModifyOriginalResponseAsync(x => x.Embed = errorEmbed).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Updates the list of top guilds based on member count, excluding certain guilds by name or ID,
    ///     and stores the updated information in the cache for further use.
    /// </summary>
    [SlashCommand("top-servers-update", "Updates the cached list of top servers")]
    [SlashOwnerOnly]
    [RequireContext(ContextType.Guild)]
    public async Task TopServersUpdate()
    {
        if (!creds.IsMasterInstance)
        {
            await ErrorAsync(Strings.NotMasterInstance(ctx.Guild.Id));
            return;
        }

        await DeferAsync().ConfigureAwait(false);

        try
        {
            var guilds = (await ctx.Client.GetGuildsAsync().ConfigureAwait(false))
                .Cast<SocketGuild>();

            var excludedTerms = new[]
            {
                "botlist", "bots", "xhamster", "nsfw", "18+"
            };
            const ulong excludedId = 374071874222686211;

            var servers = guilds
                .Where(x => x.Id != excludedId &&
                            !excludedTerms.Any(term => x.Name.Contains(term, StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(x => x.MemberCount)
                .Take(11)
                .Select(x => new StatsService.MewdekoPartialGuild
                {
                    IconUrl = x.IconId.StartsWith("a_") ? x.IconUrl.Replace(".jpg", ".gif") : x.IconUrl,
                    MemberCount = x.MemberCount,
                    Name = x.Name
                })
                .ToList();

            var serialied = Json.Serialize(servers);
            await cache.Redis.GetDatabase().StringSetAsync($"{client.CurrentUser.Id}_topguilds", serialied)
                .ConfigureAwait(false);
            await ConfirmAsync(Strings.TopGuildsUpdated(ctx.Guild.Id)).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            await ErrorAsync(Strings.ErrorUpdatingTopGuilds(ctx.Guild.Id, e.Message)).ConfigureAwait(false);
            logger.LogError(e, "Error updating top guilds");
        }
    }

    /// <summary>
    ///     Commands for managing bot instances that can be controlled via the dashboard.
    /// </summary>
    /// <param name="dbFactory">Service for database operations and access.</param>
    /// <param name="bss">Bot config service used for status emotes.</param>
    [SlashOwnerOnly]
    [Group("instance", "Manage bot instances controlled from the dashboard")]
    public class InstanceCommands(IDataConnectionFactory dbFactory, BotConfigService bss)
        : MewdekoSlashSubmodule<InstanceManagementService>
    {
        /// <summary>
        ///     Adds a bot instance to be managed from the dashboard. Only valid port numbers (1024-65535) are accepted.
        /// </summary>
        /// <param name="instancePort">The port number the instance is running on</param>
        [SlashCommand("add", "Adds a bot instance running on the specified port")]
        [SlashOwnerOnly]
        [RequireContext(ContextType.Guild)]
        public async Task AddInstance([Summary("port", "The port the instance is running on")] int instancePort)
        {
            if (instancePort is < 1024 or > 65535)
            {
                await ErrorAsync(Strings.InvalidPort(ctx.Guild.Id));
                return;
            }

            await DeferAsync().ConfigureAwait(false);

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

                    await ctx.Interaction.FollowupAsync(embed: eb.Build());
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
        [SlashCommand("list", "Lists all registered bot instances")]
        [SlashOwnerOnly]
        [RequireContext(ContextType.Guild)]
        public async Task ListInstances()
        {
            await DeferAsync().ConfigureAwait(false);

            await using var db = await dbFactory.CreateConnectionAsync();
            var instances = await db.BotInstances.ToListAsync();

            if (instances.Count == 0)
            {
                await ConfirmAsync(Strings.NoInstancesRegistered(ctx.Guild.Id));
                return;
            }

            var eb = new EmbedBuilder()
                .WithTitle(Strings.RegisteredInstances(ctx.Guild.Id))
                .WithOkColor();

            foreach (var instance in instances)
            {
                var status = await Service.GetInstanceStatusAsync(instance.Port);
                var statusEmote = status != null ? bss.Data.SuccessEmote : bss.Data.ErrorEmote;

                eb.AddField($"{statusEmote} Port {instance.Port}",
                    status != null
                        ? GetInstanceDescription(status)
                        : Strings.InstanceOffline(ctx.Guild.Id));
            }

            await ctx.Interaction.FollowupAsync(embed: eb.Build());
        }

        /// <summary>
        ///     Removes a bot instance from dashboard management.
        /// </summary>
        /// <param name="instancePort">The port number of the instance to remove</param>
        [SlashCommand("remove", "Removes a bot instance")]
        [SlashOwnerOnly]
        [RequireContext(ContextType.Guild)]
        public async Task RemoveInstance([Summary("port", "The port of the instance to remove")] int instancePort)
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
        [SlashCommand("status", "Checks the status of a specific instance")]
        [SlashOwnerOnly]
        [RequireContext(ContextType.Guild)]
        public async Task InstanceStatus([Summary("port", "The port of the instance to check")] int instancePort)
        {
            if (instancePort is < 1024 or > 65535)
            {
                await ErrorAsync(Strings.InvalidPort(ctx.Guild.Id));
                return;
            }

            await DeferAsync().ConfigureAwait(false);

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

                    await ctx.Interaction.FollowupAsync(embed: eb.Build());
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

        /// <summary>
        ///     Updates all registered bot instances.
        /// </summary>
        [SlashCommand("update-all", "Updates all registered bot instances")]
        [SlashOwnerOnly]
        [RequireContext(ContextType.Guild)]
        public async Task UpdateAllInstances()
        {
            var confirmMessage =
                await PromptUserConfirmAsync(Strings.UpdateAllInstancesConfirm(ctx.Guild.Id), ctx.User.Id);
            if (!confirmMessage)
                return;

            try
            {
                await ConfirmAsync(Strings.UpdateAllInstancesTriggering(ctx.Guild.Id));

                var results = await Service.UpdateAllInstancesAsync();

                var eb = new EmbedBuilder()
                    .WithTitle(Strings.UpdateAllInstancesResultsTitle(ctx.Guild.Id))
                    .WithOkColor();

                foreach (var (port, (success, message)) in results)
                {
                    var emote = success ? bss.Data.SuccessEmote : bss.Data.ErrorEmote;
                    eb.AddField($"{emote} Port {port}", message);
                }

                await ctx.Interaction.FollowupAsync(embed: eb.Build());
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
        [SlashCommand("update", "Updates a specific bot instance")]
        [SlashOwnerOnly]
        [RequireContext(ContextType.Guild)]
        public async Task UpdateInstance([Summary("port", "The port of the instance to update")] int instancePort)
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
                await ConfirmAsync(Strings.UpdateInstanceTriggering(ctx.Guild.Id, instancePort));

                var (success, message) = await Service.UpdateInstanceAsync(instancePort);

                if (success)
                {
                    await ConfirmAsync(Strings.UpdateInstanceSuccess(ctx.Guild.Id, instancePort, message));
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
        [SlashCommand("restart-all", "Restarts all registered bot instances")]
        [SlashOwnerOnly]
        [RequireContext(ContextType.Guild)]
        public async Task RestartAllInstances()
        {
            var confirmMessage =
                await PromptUserConfirmAsync(Strings.RestartAllInstancesConfirm(ctx.Guild.Id), ctx.User.Id);
            if (!confirmMessage)
                return;

            try
            {
                await ConfirmAsync(Strings.RestartAllInstancesTriggering(ctx.Guild.Id));

                var results = await Service.RestartAllInstancesAsync();

                var eb = new EmbedBuilder()
                    .WithTitle(Strings.RestartAllInstancesResultsTitle(ctx.Guild.Id))
                    .WithOkColor();

                foreach (var (port, (success, message)) in results)
                {
                    var emote = success ? bss.Data.SuccessEmote : bss.Data.ErrorEmote;
                    eb.AddField($"{emote} Port {port}", message);
                }

                await ctx.Interaction.FollowupAsync(embed: eb.Build());
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
        [SlashCommand("restart", "Restarts a specific bot instance")]
        [SlashOwnerOnly]
        [RequireContext(ContextType.Guild)]
        public async Task RestartInstance([Summary("port", "The port of the instance to restart")] int instancePort)
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
                await ConfirmAsync(Strings.RestartInstanceTriggering(ctx.Guild.Id, instancePort));

                var (success, message) = await Service.RestartInstanceAsync(instancePort);

                if (success)
                {
                    await ConfirmAsync(Strings.RestartInstanceSuccess(ctx.Guild.Id, instancePort, message));
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

        private string GetInstanceDescription(BotStatusModel status)
        {
            return $"{Strings.InstanceStatus(ctx.Guild.Id)} {status.BotStatus}\n" +
                   $"{Strings.InstanceVersion(ctx.Guild.Id, status.BotVersion)}\n" +
                   $"{Strings.InstanceCommandCount(ctx.Guild.Id, status.CommandsCount)}\n" +
                   $"{Strings.InstanceModulesCount(ctx.Guild.Id, status.ModulesCount)}\n" +
                   $"{Strings.InstanceUserCount(ctx.Guild.Id, status.UserCount)}";
        }
    }
}