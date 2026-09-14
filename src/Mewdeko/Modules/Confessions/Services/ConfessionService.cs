using DataModel;
using LinqToDB;
using LinqToDB.Async;
using Mewdeko.Common.Configs;
using Mewdeko.Services.Analytics;
using Mewdeko.Services.Strings;

namespace Mewdeko.Modules.Confessions.Services;

/// <summary>
///     Service for managing confessions.
/// </summary>
/// <param name="dbFactory">The database connection factory.</param>
/// <param name="client">The Discord client instance.</param>
/// <param name="guildSettings">The guild settings service.</param>
/// <param name="config">The bot configuration.</param>
/// <param name="strings">The localization service.</param>
/// <param name="logger">The logger instance for structured logging.</param>
/// <param name="collector">The analytics collector.</param>
public class ConfessionService(
    IDataConnectionFactory dbFactory,
    DiscordShardedClient client,
    GuildSettingsService guildSettings,
    BotConfig config,
    GeneratedBotStrings strings,
    ILogger<ConfessionService> logger,
    IAnalyticsCollector collector)
    : INService
{
    /// <summary>
    ///     Sends a confession message to the confession channel.
    /// </summary>
    /// <param name="serverId">The ID of the server where the confession is sent.</param>
    /// <param name="user">The user who confessed.</param>
    /// <param name="confession">The confession message.</param>
    /// <param name="currentChannel">The current message channel.</param>
    /// <param name="ctx">The interaction context, if available.</param>
    /// <param name="imageUrl">The URL of the image, if any.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SendConfession(
        ulong serverId,
        IUser user,
        string confession,
        IMessageChannel currentChannel, IInteractionContext? ctx = null, string? imageUrl = null)
    {
        try
        {
            await using var dbContext = await dbFactory.CreateConnectionAsync();
            var confessions = await dbContext.Confessions.Where(x => x.GuildId == serverId).ToListAsync();
            if (confessions.Any())
            {
                var guild = client.GetGuild(serverId);
                var current = confessions.LastOrDefault();
                var currentUser = guild.GetUser(client.CurrentUser.Id);
                var confessionChannel =
                    guild.GetTextChannel((await guildSettings.GetGuildConfig(ctx.Guild.Id)).ConfessionChannel);
                if (confessionChannel is null)
                {
                    if (ctx?.Interaction is not null)
                    {
                        await ctx.Interaction.SendEphemeralErrorAsync(
                                strings.ConfessionInvalid(serverId), config)
                            .ConfigureAwait(false);
                        return;
                    }

                    await currentChannel.SendErrorAsync(
                            strings.ConfessionInvalid(serverId), config)
                        .ConfigureAwait(false);
                    return;
                }

                var eb = new EmbedBuilder().WithOkColor()
                    .WithAuthor(strings.AnonymousConfession(guild.Id, current.ConfessNumber + 1), guild.IconUrl)
                    .WithDescription(confession)
                    .WithFooter(
                        strings.ConfessionFooter(guild.Id, guild.Id))
                    .WithCurrentTimestamp();
                if (imageUrl != null)
                    eb.WithImageUrl(imageUrl);
                var perms = currentUser.GetPermissions(confessionChannel);
                if (!perms.EmbedLinks || !perms.SendMessages)
                {
                    if (ctx?.Interaction is not null)
                    {
                        await ctx.Interaction.SendEphemeralErrorAsync(
                                strings.ConfessionNoPermission(guild.Id),
                                config)
                            .ConfigureAwait(false);
                        return;
                    }

                    await currentChannel.SendErrorAsync(
                            strings.ConfessionNoPermission(guild.Id),
                            config)
                        .ConfigureAwait(false);
                    return;
                }

                var msg = await confessionChannel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
                if (ctx?.Interaction is not null)
                {
                    await ctx.Interaction
                        .SendEphemeralConfirmAsync(
                            strings.ConfessionSent(serverId))
                        .ConfigureAwait(false);
                }
                else
                {
                    await currentChannel.SendConfirmAsync(
                            strings.ConfessionSent(serverId))
                        .ConfigureAwait(false);
                }

                var toadd = new Confession
                {
                    ChannelId = current.ChannelId,
                    Confession1 = confession,
                    ConfessNumber = current.ConfessNumber + 1,
                    GuildId = current.GuildId,
                    MessageId = msg.Id,
                    UserId = user.Id
                };
                await dbContext.InsertAsync(toadd);
                collector.Feature("confession", serverId);
                if (await GetConfessionLogChannel(serverId) != 0)
                {
                    var logChannel = guild.GetTextChannel(await GetConfessionLogChannel(serverId));
                    if (logChannel is null)
                        return;
                    var eb2 = new EmbedBuilder().WithErrorColor()
                        .AddField(strings.ConfessionLogUser(serverId), $"{user} | {user.Id}")
                        .AddField(strings.ConfessionLogConfession(serverId, current.ConfessNumber + 1), confession)
                        .AddField(strings.ConfessionLogMessageLink(serverId), msg.GetJumpUrl()).AddField(
                            strings.ConfessionLogWarning(serverId),
                            strings.ConfessionLogWarningText(serverId));
                    await logChannel.SendMessageAsync(embed: eb2.Build()).ConfigureAwait(false);
                }
            }
            else
            {
                var guild = client.GetGuild(serverId);
                var currentUser = guild.GetUser(client.CurrentUser.Id);
                var confessionChannel = guild.GetTextChannel(await GetConfessionChannel(guild.Id));
                if (confessionChannel is null)
                {
                    if (ctx is not null)
                    {
                        await ctx.Interaction.SendEphemeralErrorAsync(
                                strings.ConfessionInvalid(serverId), config)
                            .ConfigureAwait(false);
                        return;
                    }

                    await currentChannel.SendErrorAsync(
                            strings.ConfessionInvalid(serverId), config)
                        .ConfigureAwait(false);
                    return;
                }

                var eb = new EmbedBuilder().WithOkColor()
                    .WithAuthor(strings.AnonymousConfession(guild.Id, 1), guild.IconUrl)
                    .WithDescription(confession)
                    .WithFooter(
                        strings.ConfessionFooter(guild.Id, guild.Id))
                    .WithCurrentTimestamp();
                if (imageUrl != null)
                    eb.WithImageUrl(imageUrl);
                var perms = currentUser.GetPermissions(confessionChannel);
                if (!perms.EmbedLinks || !perms.SendMessages)
                {
                    if (ctx is not null)
                    {
                        await ctx.Interaction.SendEphemeralErrorAsync(
                                strings.ConfessionNoPermission(guild.Id),
                                config)
                            .ConfigureAwait(false);
                        return;
                    }

                    await currentChannel.SendErrorAsync(
                            strings.ConfessionNoPermission(guild.Id),
                            config)
                        .ConfigureAwait(false);
                    return;
                }

                var msg = await confessionChannel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
                if (ctx is not null)
                {
                    await ctx.Interaction
                        .SendEphemeralConfirmAsync(
                            strings.ConfessionSent(serverId))
                        .ConfigureAwait(false);
                }
                else
                {
                    await currentChannel.SendConfirmAsync(
                            strings.ConfessionSent(serverId))
                        .ConfigureAwait(false);
                }

                var toadd = new Confession
                {
                    ChannelId = confessionChannel.Id,
                    Confession1 = confession,
                    ConfessNumber = 1,
                    GuildId = guild.Id,
                    MessageId = msg.Id,
                    UserId = user.Id
                };
                await dbContext.InsertAsync(toadd);
                collector.Feature("confession", serverId);
                if (await GetConfessionLogChannel(serverId) != 0)
                {
                    var logChannel = guild.GetTextChannel(await GetConfessionLogChannel(serverId));
                    if (logChannel is null)
                        return;
                    var eb2 = new EmbedBuilder().WithErrorColor()
                        .AddField(strings.ConfessionLogUser(serverId), $"{user} | {user.Id}")
                        .AddField(strings.ConfessionLogConfession(serverId, 1), confession)
                        .AddField(strings.ConfessionLogMessageLink(serverId), msg.GetJumpUrl()).AddField(
                            strings.ConfessionLogWarning(serverId),
                            strings.ConfessionLogWarningText(serverId));
                    await logChannel.SendMessageAsync(embed: eb2.Build()).ConfigureAwait(false);
                }
            }
        }
        catch (Exception e)
        {
            collector.Feature("confession", serverId, false, e.GetType().Name);
            logger.LogInformation($"{e}");
        }
    }

    /// <summary>
    ///     Sets the confession channel for a guild.
    /// </summary>
    /// <param name="guild">The guild to set the confession channel for.</param>
    /// <param name="channelId">The ID of the confession channel.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SetConfessionChannel(IGuild guild, ulong channelId)
    {
        var guildConfig = await guildSettings.GetGuildConfig(guild.Id);
        guildConfig.ConfessionChannel = channelId;
        await guildSettings.UpdateGuildConfig(guild.Id, guildConfig);
    }

    /// <summary>
    ///     Gets the confession channel for a guild.
    /// </summary>
    /// <param name="id">The ID of the guild.</param>
    /// <returns>The ID of the confession channel.</returns>
    private async Task<ulong> GetConfessionChannel(ulong id)
    {
        return (await guildSettings.GetGuildConfig(id)).ConfessionChannel;
    }

    /// <summary>
    ///     Toggles the user blacklist asynchronously.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="roleId">The ID of the role to toggle the blacklist for.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ToggleUserBlacklistAsync(ulong guildId, ulong roleId)
    {
        var guildConfig = await guildSettings.GetGuildConfig(guildId);
        var blacklists = guildConfig.GetConfessionBlacklists();
        if (!blacklists.Remove(roleId))
            blacklists.Add(roleId);

        guildConfig.SetConfessionBlacklists(blacklists);
        await guildSettings.UpdateGuildConfig(guildId, guildConfig);
    }

    /// <summary>
    ///     Sets the confession log channel for a guild.
    /// </summary>
    /// <param name="guild">The guild to set the confession log channel for.</param>
    /// <param name="channelId">The ID of the confession log channel.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SetConfessionLogChannel(IGuild guild, ulong channelId)
    {
        var guildConfig = await guildSettings.GetGuildConfig(guild.Id);
        guildConfig.ConfessionLogChannel = channelId;
        await guildSettings.UpdateGuildConfig(guild.Id, guildConfig);
    }

    /// <summary>
    ///     Gets the confession log channel for a guild.
    /// </summary>
    /// <param name="id">The ID of the guild.</param>
    /// <returns>The ID of the confession log channel.</returns>
    private async Task<ulong> GetConfessionLogChannel(ulong id)
    {
        return (await guildSettings.GetGuildConfig(id)).ConfessionLogChannel;
    }
}

/// <summary>
///     Extension methods for <see cref="GuildConfig" />, and <see cref="ConfessionService" /> related classes.
/// </summary>
public static class ConfessionExtensions
{
    /// <summary>
    ///     Gets the confession blacklists from the guild configuration.
    /// </summary>
    /// <param name="gc">The guild configuration.</param>
    /// <returns>The list of role IDs that are blacklisted for confessions.</returns>
    public static List<ulong> GetConfessionBlacklists(this GuildConfig gc)
    {
        return string.IsNullOrWhiteSpace(gc.ConfessionBlacklist)
            ? []
            : gc.ConfessionBlacklist.Split(' ').Select(ulong.Parse).ToList();
    }

    /// <summary>
    ///     Sets the confession blacklists in the guild configuration.
    /// </summary>
    /// <param name="gc">The guild configuration.</param>
    /// <param name="blacklists">The list of role IDs to set as blacklisted for confessions.</param>
    public static void SetConfessionBlacklists(this GuildConfig gc, IEnumerable<ulong> blacklists)
    {
        gc.ConfessionBlacklist = blacklists.JoinWith(' ');
    }
}