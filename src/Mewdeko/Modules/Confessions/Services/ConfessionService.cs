using Mewdeko.Common.Configs;

namespace Mewdeko.Modules.Confessions.Services;

/// <summary>
/// Service for managing confessions.
/// </summary>
/// <param name="db"></param>
/// <param name="client"></param>
/// <param name="guildSettings"></param>
public class ConfessionService(
    DbService db,
    DiscordSocketClient client,
    GuildSettingsService guildSettings,
    BotConfig config)
    : INService
{
    /// <summary>
    /// Sends a confession message to the confession channel.
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
        var uow = db.GetDbContext();
        var confessions = uow.Confessions.ForGuild(serverId);
        if (confessions.Count > 0)
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
                            "The confession channel is invalid! Please tell the server staff about this!", config)
                        .ConfigureAwait(false);
                    return;
                }

                await currentChannel.SendErrorAsync(
                        "The confession channel is invalid! Please tell the server staff about this!", config)
                    .ConfigureAwait(false);
                return;
            }

            var eb = new EmbedBuilder().WithOkColor()
                .WithAuthor($"Anonymous confession #{current.ConfessNumber + 1}", guild.IconUrl)
                .WithDescription(confession)
                .WithFooter(
                    $"Do /confess or dm me .confess {guild.Id} yourconfession to send a confession!")
                .WithCurrentTimestamp();
            if (imageUrl != null)
                eb.WithImageUrl(imageUrl);
            var perms = currentUser.GetPermissions(confessionChannel);
            if (!perms.EmbedLinks || !perms.SendMessages)
            {
                if (ctx?.Interaction is not null)
                {
                    await ctx.Interaction.SendEphemeralErrorAsync(
                            "Seems I dont have permission to post in the confession channel! Please tell the server staff.",
                            config)
                        .ConfigureAwait(false);
                    return;
                }

                await currentChannel.SendErrorAsync(
                        "Seems I dont have permission to post in the confession channel! Please tell the server staff.",
                        config)
                    .ConfigureAwait(false);
                return;
            }

            var msg = await confessionChannel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            if (ctx?.Interaction is not null)
            {
                await ctx.Interaction
                    .SendEphemeralConfirmAsync(
                        "Your confession has been sent! Please keep in mind if the server is abusing confessions you can send in a report using `/confessions report`")
                    .ConfigureAwait(false);
            }
            else
            {
                await currentChannel.SendConfirmAsync(
                        "Your confession has been sent! Please keep in mind if the server is abusing confessions you can send in a report using `/confessions report`")
                    .ConfigureAwait(false);
            }

            var toadd = new Database.Models.Confessions
            {
                ChannelId = current.ChannelId,
                Confession = confession,
                ConfessNumber = current.ConfessNumber + 1,
                GuildId = current.GuildId,
                MessageId = msg.Id,
                UserId = user.Id
            };
            uow.Confessions.Add(toadd);
            await uow.SaveChangesAsync().ConfigureAwait(false);
            if (await GetConfessionLogChannel(serverId) != 0)
            {
                var logChannel = guild.GetTextChannel(await GetConfessionLogChannel(serverId));
                if (logChannel is null)
                    return;
                var eb2 = new EmbedBuilder().WithErrorColor()
                    .AddField("User", $"{user} | {user.Id}")
                    .AddField($"Confession {current.ConfessNumber + 1}", confession)
                    .AddField("Message Link", msg.GetJumpUrl()).AddField("***WARNING***",
                        "***Misuse of this function will lead me to finding out, blacklisting this server, and tearing out your reproductive organs.***");
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
                            "The confession channel is invalid! Please tell the server staff about this!", config)
                        .ConfigureAwait(false);
                    return;
                }

                await currentChannel.SendErrorAsync(
                        "The confession channel is invalid! Please tell the server staff about this!", config)
                    .ConfigureAwait(false);
                return;
            }

            var eb = new EmbedBuilder().WithOkColor()
                .WithAuthor("Anonymous confession #1", guild.IconUrl)
                .WithDescription(confession)
                .WithFooter(
                    $"Do /confess or dm me .confess {guild.Id} yourconfession to send a confession!")
                .WithCurrentTimestamp();
            if (imageUrl != null)
                eb.WithImageUrl(imageUrl);
            var perms = currentUser.GetPermissions(confessionChannel);
            if (!perms.EmbedLinks || !perms.SendMessages)
            {
                if (ctx is not null)
                {
                    await ctx.Interaction.SendEphemeralErrorAsync(
                            "Seems I dont have permission to post in the confession channel! Please tell the server staff.",
                            config)
                        .ConfigureAwait(false);
                    return;
                }

                await currentChannel.SendErrorAsync(
                        "Seems I dont have permission to post in the confession channel! Please tell the server staff.",
                        config)
                    .ConfigureAwait(false);
                return;
            }

            var msg = await confessionChannel.SendMessageAsync(embed: eb.Build()).ConfigureAwait(false);
            if (ctx is not null)
            {
                await ctx.Interaction
                    .SendEphemeralConfirmAsync(
                        "Your confession has been sent! Please keep in mind if the server is abusing confessions you can send in a report using `/confessions report`")
                    .ConfigureAwait(false);
            }
            else
            {
                await currentChannel.SendConfirmAsync(
                        "Your confession has been sent! Please keep in mind if the server is abusing confessions you can send in a report using `/confessions report`")
                    .ConfigureAwait(false);
            }

            var toadd = new Database.Models.Confessions
            {
                ChannelId = confessionChannel.Id,
                Confession = confession,
                ConfessNumber = 1,
                GuildId = guild.Id,
                MessageId = msg.Id,
                UserId = user.Id
            };
            uow.Confessions.Add(toadd);
            await uow.SaveChangesAsync().ConfigureAwait(false);
            if (await GetConfessionLogChannel(serverId) != 0)
            {
                var logChannel = guild.GetTextChannel(await GetConfessionLogChannel(serverId));
                if (logChannel is null)
                    return;
                var eb2 = new EmbedBuilder().WithErrorColor()
                    .AddField("User", $"{user} | {user.Id}")
                    .AddField("Confession 1", confession)
                    .AddField("Message Link", msg.GetJumpUrl()).AddField("***WARNING***",
                        "***Misuse of this function will lead me to finding out, blacklisting this server, and tearing out your reproductive organs.***");
                await logChannel.SendMessageAsync(embed: eb2.Build()).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Sets the confession channel for a guild.
    /// </summary>
    /// <param name="guild">The guild to set the confession channel for.</param>
    /// <param name="channelId">The ID of the confession channel.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SetConfessionChannel(IGuild guild, ulong channelId)
    {
        await using var uow = db.GetDbContext();
        var guildConfig = await uow.ForGuildId(guild.Id, set => set);
        guildConfig.ConfessionChannel = channelId;
        await guildSettings.UpdateGuildConfig(guild.Id, guildConfig);
    }

    /// <summary>
    /// Gets the confession channel for a guild.
    /// </summary>
    /// <param name="id">The ID of the guild.</param>
    /// <returns>The ID of the confession channel.</returns>
    private async Task<ulong> GetConfessionChannel(ulong id)
        => (await guildSettings.GetGuildConfig(id)).ConfessionChannel;

    /// <summary>
    /// Toggles the user blacklist asynchronously.
    /// </summary>
    /// <param name="guildId">The ID of the guild.</param>
    /// <param name="roleId">The ID of the role to toggle the blacklist for.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ToggleUserBlacklistAsync(ulong guildId, ulong roleId)
    {
        await using var uow = db.GetDbContext();
        var guildConfig = await uow.ForGuildId(guildId, set => set);
        var blacklists = guildConfig.GetConfessionBlacklists();
        if (!blacklists.Remove(roleId))
            blacklists.Add(roleId);

        guildConfig.SetConfessionBlacklists(blacklists);
        await guildSettings.UpdateGuildConfig(guildId, guildConfig);
    }

    /// <summary>
    /// Sets the confession log channel for a guild.
    /// </summary>
    /// <param name="guild">The guild to set the confession log channel for.</param>
    /// <param name="channelId">The ID of the confession log channel.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SetConfessionLogChannel(IGuild guild, ulong channelId)
    {
        await using var uow = db.GetDbContext();
        var guildConfig = await uow.ForGuildId(guild.Id, set => set);
        guildConfig.ConfessionLogChannel = channelId;
        await guildSettings.UpdateGuildConfig(guild.Id, guildConfig);
    }

    /// <summary>
    /// Gets the confession log channel for a guild.
    /// </summary>
    /// <param name="id">The ID of the guild.</param>
    /// <returns>The ID of the confession log channel.</returns>
    private async Task<ulong> GetConfessionLogChannel(ulong id)
        => (await guildSettings.GetGuildConfig(id)).ConfessionLogChannel;
}

/// <summary>
/// Extension methods for <see cref="GuildConfig"/>, and <see cref="ConfessionService"/> related classes.
/// </summary>
public static class ConfessionExtensions
{
    /// <summary>
    /// Gets the confession blacklists from the guild configuration.
    /// </summary>
    /// <param name="gc">The guild configuration.</param>
    /// <returns>The list of role IDs that are blacklisted for confessions.</returns>
    public static List<ulong> GetConfessionBlacklists(this GuildConfig gc)
        => string.IsNullOrWhiteSpace(gc.ConfessionBlacklist)
            ? new List<ulong>()
            : gc.ConfessionBlacklist.Split(' ').Select(ulong.Parse).ToList();

    /// <summary>
    /// Sets the confession blacklists in the guild configuration.
    /// </summary>
    /// <param name="gc">The guild configuration.</param>
    /// <param name="blacklists">The list of role IDs to set as blacklisted for confessions.</param>
    public static void SetConfessionBlacklists(this GuildConfig gc, IEnumerable<ulong> blacklists) =>
        gc.ConfessionBlacklist = blacklists.JoinWith(' ');
}