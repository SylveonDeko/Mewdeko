using Discord.Rest;
using Mewdeko.Common.ModuleBehaviors;
using Mewdeko.Services.Impl;
using Mewdeko.Services.Settings;

namespace Mewdeko.Modules.OwnerOnly.Services;

/// <summary>
///     Detects servers that are littered with bots, both on demand for the owner and automatically when the bot joins a
///     new server. A server is a bot hell when it has at least the configured member count and either its bot count or
///     its bot percentage meets the configured threshold.
/// </summary>
public class BotHellService : INService, IReadyExecutor
{
    private readonly BotConfigService bss;
    private readonly DiscordShardedClient client;
    private readonly BotCredentials creds;
    private readonly EventHandler handler;
    private readonly ILogger<BotHellService> logger;

    /// <summary>
    ///     Initializes a new instance of <see cref="BotHellService" />.
    /// </summary>
    /// <param name="handler">The event handler the join event is subscribed on.</param>
    /// <param name="client">The discord client.</param>
    /// <param name="bss">The bot config service.</param>
    /// <param name="creds">The bot credentials.</param>
    /// <param name="logger">The logger instance for structured logging.</param>
    public BotHellService(EventHandler handler, DiscordShardedClient client, BotConfigService bss,
        BotCredentials creds, ILogger<BotHellService> logger)
    {
        this.handler = handler;
        this.client = client;
        this.bss = bss;
        this.creds = creds;
        this.logger = logger;
    }

    /// <summary>
    ///     The member count a server needs before it is considered at all.
    /// </summary>
    public int MinMembers
    {
        get
        {
            return bss.Data.BotHellMinMembers;
        }
    }

    /// <summary>
    ///     The bot count at or above which a server is flagged, or zero when the count check is off.
    /// </summary>
    public int BotCountLimit
    {
        get
        {
            return bss.Data.BotHellBotCount;
        }
    }

    /// <summary>
    ///     The bot percentage at or above which a server is flagged, or zero when the ratio check is off.
    /// </summary>
    public int BotPercentLimit
    {
        get
        {
            return bss.Data.BotHellBotPercent;
        }
    }

    /// <summary>
    ///     Whether flagged servers are left automatically on join.
    /// </summary>
    public bool AutoLeave
    {
        get
        {
            return bss.Data.BotHellAutoLeave;
        }
    }

    /// <summary>
    ///     The channel configured for reports, or zero when the join/leave channel is used instead.
    /// </summary>
    public ulong ConfiguredChannelId
    {
        get
        {
            return bss.Data.BotHellReportChannel;
        }
    }

    /// <summary>
    ///     The channel join detections get posted to, after the join/leave channel fallback.
    /// </summary>
    public ulong ReportChannelId
    {
        get
        {
            return bss.Data.BotHellReportChannel is not 0
                ? bss.Data.BotHellReportChannel
                : creds.GuildJoinsChannelId;
        }
    }

    /// <inheritdoc />
    public Task OnReadyAsync()
    {
        handler.Subscribe("JoinedGuild", "BotHellService", OnJoinedGuild);
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Evaluates a server against the configured thresholds using the cached member list.
    /// </summary>
    /// <param name="guild">The server to evaluate.</param>
    /// <returns>The counts and whether the server is a bot hell.</returns>
    public BotHellVerdict Evaluate(SocketGuild guild)
    {
        var bots = 0;
        var humans = 0;
        foreach (var user in guild.Users)
        {
            if (user.IsBot)
                bots++;
            else
                humans++;
        }

        var total = Math.Max(guild.MemberCount, bots + humans);
        var percent = total == 0 ? 0 : (int)Math.Round(bots * 100.0 / total);

        var byCount = BotCountLimit > 0 && bots >= BotCountLimit;
        var byPercent = BotPercentLimit > 0 && percent >= BotPercentLimit;
        var flagged = total >= MinMembers && (byCount || byPercent);

        return new BotHellVerdict(guild.Id, guild.Name, total, humans, bots, percent, flagged, byCount, byPercent,
            guild.HasAllMembers);
    }

    /// <summary>
    ///     Evaluates a server after making sure its member list is fully downloaded.
    /// </summary>
    /// <param name="guild">The server to evaluate.</param>
    /// <returns>The counts and whether the server is a bot hell.</returns>
    public async Task<BotHellVerdict> EvaluateFullAsync(SocketGuild guild)
    {
        if (!guild.HasAllMembers)
        {
            try
            {
                await guild.DownloadUsersAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to download members for bot hell check in {GuildId}", guild.Id);
            }
        }

        return Evaluate(guild);
    }

    /// <summary>
    ///     Scans every cached server and returns the ones flagged as bot hells, most bots first.
    /// </summary>
    /// <returns>The flagged servers.</returns>
    public List<BotHellVerdict> Scan()
    {
        return client.Guilds
            .Where(g => g.MemberCount >= MinMembers)
            .Select(Evaluate)
            .Where(v => v.IsBotHell)
            .OrderByDescending(v => v.Bots)
            .ThenByDescending(v => v.Percent)
            .ToList();
    }

    /// <summary>
    ///     Evaluates every cached server, flagged or not, so the dashboard can show the whole picture. Servers below
    ///     the minimum member count are included but can never be flagged.
    /// </summary>
    /// <returns>Every server, flagged ones first, then by bot count.</returns>
    public List<BotHellVerdict> ListAll()
    {
        return client.Guilds
            .Select(Evaluate)
            .OrderByDescending(v => v.IsBotHell)
            .ThenByDescending(v => v.Bots)
            .ThenByDescending(v => v.Percent)
            .ToList();
    }

    /// <summary>
    ///     Leaves the given servers. Servers the bot owns are deleted instead, matching the owner leave command.
    /// </summary>
    /// <param name="guildIds">The servers to leave.</param>
    /// <returns>The ids that were left, and the ids that could not be, with the reason.</returns>
    public async Task<(List<ulong> Left, Dictionary<ulong, string> Failed)> LeaveAsync(IEnumerable<ulong> guildIds)
    {
        var left = new List<ulong>();
        var failed = new Dictionary<ulong, string>();

        foreach (var id in guildIds.Distinct())
        {
            var guild = client.GetGuild(id);
            if (guild is null)
            {
                failed[id] = "The bot is not in that server";
                continue;
            }

            try
            {
                if (guild.OwnerId == client.CurrentUser.Id)
                    await guild.DeleteAsync().ConfigureAwait(false);
                else
                    await guild.LeaveAsync().ConfigureAwait(false);

                logger.LogInformation("Left bot hell {GuildName} [{GuildId}] from the dashboard", guild.Name,
                    guild.Id);
                left.Add(id);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to leave guild {GuildId} from the dashboard", id);
                failed[id] = ex.Message;
            }
        }

        return (left, failed);
    }

    /// <summary>
    ///     Updates the bot wide thresholds and behaviour.
    /// </summary>
    /// <param name="minMembers">Servers smaller than this are never flagged.</param>
    /// <param name="botCount">Bots at or above this flag the server, zero disables.</param>
    /// <param name="botPercent">Bot percentage at or above this flags the server, zero disables.</param>
    /// <param name="autoLeave">Whether flagged servers are left on join.</param>
    /// <param name="reportChannel">Where join detections are posted, zero for the join/leave channel.</param>
    public void UpdateSettings(int minMembers, int botCount, int botPercent, bool autoLeave, ulong reportChannel)
    {
        bss.ModifyConfig(config =>
        {
            config.BotHellMinMembers = Math.Max(0, minMembers);
            config.BotHellBotCount = Math.Max(0, botCount);
            config.BotHellBotPercent = Math.Clamp(botPercent, 0, 100);
            config.BotHellAutoLeave = autoLeave;
            config.BotHellReportChannel = reportChannel;
        });
    }

    /// <summary>
    ///     Resolves a report channel id to something readable for the dashboard.
    /// </summary>
    /// <param name="channelId">The channel id to resolve.</param>
    /// <returns>The channel name, its guild, and whether the bot can post there.</returns>
    public async Task<(string? ChannelName, ulong GuildId, string? GuildName, bool Reachable)> DescribeChannelAsync(
        ulong channelId)
    {
        if (channelId is 0)
            return (null, 0, null, false);

        try
        {
            if (await client.Rest.GetChannelAsync(channelId).ConfigureAwait(false) is not RestTextChannel channel)
                return (null, 0, null, false);

            return (channel.Name, channel.GuildId, client.GetGuild(channel.GuildId)?.Name, true);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to resolve bot hell report channel {ChannelId}", channelId);
            return (null, 0, null, false);
        }
    }

    private Task OnJoinedGuild(SocketGuild guild)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                if (guild.MemberCount < MinMembers)
                    return;

                var verdict = await EvaluateFullAsync(guild).ConfigureAwait(false);
                if (!verdict.IsBotHell)
                    return;

                var left = false;
                if (AutoLeave && guild.OwnerId != client.CurrentUser.Id)
                {
                    await guild.LeaveAsync().ConfigureAwait(false);
                    left = true;
                }

                logger.LogInformation(
                    "Bot hell detected on join: {GuildName} [{GuildId}] {Bots} bots / {Total} members ({Percent}%), left: {Left}",
                    guild.Name, guild.Id, verdict.Bots, verdict.Total, verdict.Percent, left);

                await Report(verdict, left).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Bot hell check failed for guild {GuildId}", guild.Id);
            }
        });
        return Task.CompletedTask;
    }

    private async Task Report(BotHellVerdict verdict, bool left)
    {
        var channelId = ReportChannelId;
        if (channelId is 0)
            return;

        try
        {
            if (await client.Rest.GetChannelAsync(channelId).ConfigureAwait(false) is not RestTextChannel channel)
                return;

            var eb = new EmbedBuilder()
                .WithErrorColor()
                .WithTitle("Bot Hell Detected")
                .AddField("Server", $"{verdict.GuildName} `{verdict.GuildId}`")
                .AddField("Members", verdict.Total, true)
                .AddField("Humans", verdict.Humans, true)
                .AddField("Bots", $"{verdict.Bots} ({verdict.Percent}%)", true)
                .AddField("Trigger", verdict.TriggerDescription, true)
                .AddField("Action", left ? "Left the server" : "Stayed", true);

            await channel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to report bot hell for guild {GuildId}", verdict.GuildId);
        }
    }
}

/// <summary>
///     The result of evaluating a server for bot hell status.
/// </summary>
/// <param name="GuildId">The server id.</param>
/// <param name="GuildName">The server name.</param>
/// <param name="Total">The total member count.</param>
/// <param name="Humans">How many cached members are humans.</param>
/// <param name="Bots">How many cached members are bots.</param>
/// <param name="Percent">The percentage of members that are bots.</param>
/// <param name="IsBotHell">Whether the server met a threshold.</param>
/// <param name="ByCount">Whether the bot count threshold was met.</param>
/// <param name="ByPercent">Whether the bot percentage threshold was met.</param>
/// <param name="Complete">Whether the member list was fully downloaded when counted.</param>
public record BotHellVerdict(
    ulong GuildId,
    string GuildName,
    int Total,
    int Humans,
    int Bots,
    int Percent,
    bool IsBotHell,
    bool ByCount,
    bool ByPercent,
    bool Complete)
{
    /// <summary>
    ///     A short description of which thresholds were met.
    /// </summary>
    public string TriggerDescription
    {
        get
        {
            return (ByCount, ByPercent) switch
            {
                (true, true) => "count and ratio",
                (true, false) => "count",
                (false, true) => "ratio",
                _ => "none"
            };
        }
    }
}
