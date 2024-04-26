using Discord.Net;
using Serilog;

namespace Mewdeko.Modules.RoleGreets.Services;

/// <summary>
/// Provides functionalities related to greeting users with specific roles in a guild.
/// </summary>
public class RoleGreetService : INService
{
    private readonly DiscordSocketClient client;
    private readonly DbService db;

    /// <summary>
    /// Initializes a new instance of the <see cref="RoleGreetService"/> class.
    /// </summary>
    /// <param name="db">The database service for accessing role greet configurations.</param>
    /// <param name="client">The Discord socket client to interact with the Discord API.</param>
    /// <param name="eventHandler">The event handler to subscribe to guild member update events.</param>
    public RoleGreetService(DbService db, DiscordSocketClient client, EventHandler eventHandler)
    {
        this.client = client;
        this.db = db;
        eventHandler.GuildMemberUpdated += DoRoleGreet;
    }

    /// <summary>
    /// Retrieves an array of <see cref="RoleGreet"/> configurations for a specific role.
    /// </summary>
    /// <param name="roleId">The unique identifier of the role.</param>
    /// <returns>An array of <see cref="RoleGreet"/> objects.</returns>
    public async Task<RoleGreet[]> GetGreets(ulong roleId) =>
        await db.GetDbContext().RoleGreets.ForRoleId(roleId) ?? Array.Empty<RoleGreet>();

    /// <summary>
    /// Retrieves a list of <see cref="RoleGreet"/> configurations for a specific guild.
    /// </summary>
    /// <param name="guildId">The unique identifier of the guild.</param>
    /// <returns>An array of <see cref="RoleGreet"/> objects if any are found; otherwise, null.</returns>
    public RoleGreet[]? GetListGreets(ulong guildId) =>
        db.GetDbContext().RoleGreets.Where(x => x.GuildId == guildId).ToArray();

    /// <summary>
    /// Handles the role greet functionality when a guild member's roles are updated.
    /// </summary>
    /// <param name="cacheable">A cacheable representation of the updated guild member.</param>
    /// <param name="socketGuildUser">The updated guild member.</param>
    private async Task DoRoleGreet(Cacheable<SocketGuildUser, ulong> cacheable, SocketGuildUser socketGuildUser)
    {
        var user = await cacheable.GetOrDownloadAsync().ConfigureAwait(false);
        if (user.Roles.SequenceEqual(socketGuildUser.Roles))
        {
            if (user.Roles.Count > socketGuildUser.Roles.Count)
                return;
        }

        var diffRoles = socketGuildUser.Roles.Where(r => !user.Roles.Contains(r)).ToArray();
        foreach (var i in diffRoles)
        {
            var greets = await GetGreets(i.Id);
            if (greets.Length == 0) return;
            var webhooks = greets.Where(x => x.WebhookUrl is not null)
                .Select(x => new DiscordWebhookClient(x.WebhookUrl));
            if (greets.Length > 0)
            {
                async void Exec(SocketRole x) => await HandleChannelGreets(greets, x, user).ConfigureAwait(false);

                diffRoles.ForEach(Exec);
            }

            if (!webhooks.Any()) continue;
            {
                async void Exec(SocketRole x) => await HandleWebhookGreets(greets, x, user).ConfigureAwait(false);

                diffRoles.ForEach(Exec);
            }
        }
    }

    /// <summary>
    /// Handles the sending of greet messages through channel for roles added to a user.
    /// </summary>
    /// <param name="multiGreets">An enumerable of <see cref="RoleGreet"/> configurations.</param>
    /// <param name="role">The role that was added to the user.</param>
    /// <param name="user">The user who received the role.</param>
    private async Task HandleChannelGreets(IEnumerable<RoleGreet> multiGreets, SocketRole role, SocketGuildUser user)
    {
        var checkGreets = multiGreets.Where(x => x.RoleId == role.Id);
        if (!checkGreets.Any())
            return;
        var replacer = new ReplacementBuilder().WithUser(user).WithClient(client).WithServer(client, user.Guild)
            .Build();
        foreach (var i in checkGreets)
        {
            if (i.Disabled)
                continue;
            if (!i.GreetBots && user.IsBot)
                continue;
            if (i.WebhookUrl != null)
                continue;
            var channel = user.Guild.GetTextChannel(i.ChannelId);
            if (channel is null)
            {
                await RemoveRoleGreetInternal(i).ConfigureAwait(false);
                continue;
            }

            var content = replacer.Replace(i.Message);
            try
            {
                if (SmartEmbed.TryParse(content, user.Guild?.Id, out var embedData, out var plainText,
                        out var components))
                {
                    if (embedData is not null && plainText is not "")
                    {
                        var msg = await channel
                            .SendMessageAsync(plainText, embeds: embedData, components: components?.Build())
                            .ConfigureAwait(false);
                        if (i.DeleteTime > 0)
                            msg.DeleteAfter(i.DeleteTime);
                    }

                    if (embedData is null && plainText is not null)
                    {
                        var msg = await channel.SendMessageAsync(plainText, components: components?.Build())
                            .ConfigureAwait(false);
                        if (i.DeleteTime > 0)
                            msg.DeleteAfter(i.DeleteTime);
                    }

                    if (embedData is not null && plainText is "")
                    {
                        var msg = await channel.SendMessageAsync(embeds: embedData, components: components?.Build())
                            .ConfigureAwait(false);
                        if (i.DeleteTime > 0)
                            msg.DeleteAfter(i.DeleteTime);
                    }
                }
                else
                {
                    var msg = await channel.SendMessageAsync(content).ConfigureAwait(false);
                    if (i.DeleteTime > 0)
                        msg.DeleteAfter(i.DeleteTime);
                }
            }
            catch (HttpException ex)
            {
                if (ex.DiscordCode == DiscordErrorCode.MissingPermissions)
                {
                    await RoleGreetDisable(i, true);
                    Log.Information($"RoleGreet disabled in {user.Guild} due to missing permissions.");
                }
            }
        }
    }

    /// <summary>
    /// Handles the sending of greet messages through webhooks for roles added to a user.
    /// </summary>
    /// <param name="multiGreets">An enumerable of <see cref="RoleGreet"/> configurations.</param>
    /// <param name="role">The role that was added to the user.</param>
    /// <param name="user">The user who received the role.</param>
    private async Task HandleWebhookGreets(IEnumerable<RoleGreet> multiGreets, SocketRole role, SocketGuildUser user)
    {
        var checkGreets = multiGreets.Where(x => x.RoleId == role.Id);
        if (!checkGreets.Any())
            return;
        var replacer = new ReplacementBuilder().WithUser(user).WithClient(client).WithServer(client, user.Guild)
            .Build();
        foreach (var i in checkGreets)
        {
            if (i.WebhookUrl == null)
                continue;
            if (i.Disabled)
                continue;
            if (!i.GreetBots && user.IsBot)
                continue;

            if (string.IsNullOrEmpty(i.WebhookUrl)) continue;
            var webhook = new DiscordWebhookClient(i.WebhookUrl);
            var channel = user.Guild.GetTextChannel(i.ChannelId);
            if (channel is null)
            {
                await RemoveRoleGreetInternal(i).ConfigureAwait(false);
                continue;
            }

            var content = replacer.Replace(i.Message);
            try
            {
                if (SmartEmbed.TryParse(content, channel.Guild?.Id, out var embedData, out var plainText,
                        out var components))
                {
                    if (embedData is not null && plainText is not "")
                    {
                        var msg = await webhook
                            .SendMessageAsync(plainText, embeds: embedData, components: components?.Build())
                            .ConfigureAwait(false);
                        if (i.DeleteTime > 0)
                            (await user.Guild.GetTextChannel(i.ChannelId).GetMessageAsync(msg).ConfigureAwait(false))
                                .DeleteAfter(i.DeleteTime);
                    }

                    if (embedData is null && plainText is not null)
                    {
                        var msg = await webhook.SendMessageAsync(plainText, components: components?.Build())
                            .ConfigureAwait(false);
                        if (i.DeleteTime > 0)
                            (await user.Guild.GetTextChannel(i.ChannelId).GetMessageAsync(msg).ConfigureAwait(false))
                                .DeleteAfter(i.DeleteTime);
                    }

                    if (embedData is not null && plainText is "")
                    {
                        var msg = await webhook.SendMessageAsync(embeds: embedData, components: components?.Build())
                            .ConfigureAwait(false);
                        if (i.DeleteTime > 0)
                            (await user.Guild.GetTextChannel(i.ChannelId).GetMessageAsync(msg).ConfigureAwait(false))
                                .DeleteAfter(i.DeleteTime);
                    }
                }
                else
                {
                    var msg = await webhook.SendMessageAsync(content).ConfigureAwait(false);
                    if (i.DeleteTime > 0)
                        (await user.Guild.GetTextChannel(i.ChannelId).GetMessageAsync(msg).ConfigureAwait(false))
                            .DeleteAfter(i.DeleteTime);
                }
            }
            catch (HttpException ex)
            {
                if (ex.DiscordCode == DiscordErrorCode.MissingPermissions)
                {
                    await RoleGreetDisable(i, true);
                    Log.Information($"RoleGreet disabled in {user.Guild} due to missing permissions.");
                }
            }
        }
    }

    /// <summary>
    /// Adds a new role greet configuration.
    /// </summary>
    /// <param name="guildId">The unique identifier of the guild.</param>
    /// <param name="channelId">The unique identifier of the channel.</param>
    /// <param name="roleId">The unique identifier of the role.</param>
    /// <returns>True if the configuration was added successfully; otherwise, false.</returns>
    public async Task<bool> AddRoleGreet(ulong guildId, ulong channelId, ulong roleId)
    {
        if ((await GetGreets(guildId)).Length == 10)
            return false;
        var toadd = new RoleGreet
        {
            ChannelId = channelId, GuildId = guildId, RoleId = roleId
        };
        var uow = db.GetDbContext();
        uow.RoleGreets.Add(toadd);
        await uow.SaveChangesAsync().ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// Updates the message content of a role greet configuration.
    /// </summary>
    /// <param name="greet">The role greet configuration to update.</param>
    /// <param name="code">The new message content.</param>
    public async Task ChangeMgMessage(RoleGreet greet, string code)
    {
        var uow = db.GetDbContext();
        greet.Message = code;
        uow.RoleGreets.Update(greet);
        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Enables or disables a role greet configuration.
    /// </summary>
    /// <param name="greet">The role greet configuration to update.</param>
    /// <param name="disabled">Specifies whether the greet should be disabled.</param>
    public async Task RoleGreetDisable(RoleGreet greet, bool disabled)
    {
        var uow = db.GetDbContext();
        greet.Disabled = disabled;
        uow.RoleGreets.Update(greet);
        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Updates the deletion time for messages sent by a role greet configuration.
    /// </summary>
    /// <param name="greet">The role greet configuration to update.</param>
    /// <param name="howlong">The time in seconds after which the greet message should be deleted.</param>
    public async Task ChangeRgDelete(RoleGreet greet, int howlong)
    {
        var uow = db.GetDbContext();
        greet.DeleteTime = howlong;
        uow.RoleGreets.Update(greet);
        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Updates the webhook URL of a role greet configuration.
    /// </summary>
    /// <param name="greet">The role greet configuration to update.</param>
    /// <param name="webhookurl">The new webhook URL.</param>
    public async Task ChangeMgWebhook(RoleGreet greet, string webhookurl)
    {
        var uow = db.GetDbContext();
        greet.WebhookUrl = webhookurl;
        uow.Update(greet);
        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Enables or disables greeting bots for a role greet configuration.
    /// </summary>
    /// <param name="greet">The role greet configuration to update.</param>
    /// <param name="enabled">Specifies whether bots should be greeted.</param>
    public async Task ChangeRgGb(RoleGreet greet, bool enabled)
    {
        var uow = db.GetDbContext();
        greet.GreetBots = enabled;
        uow.Update(greet);
        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Removes a specific role greet configuration.
    /// </summary>
    /// <param name="greet">The role greet configuration to remove.</param>
    public async Task RemoveRoleGreetInternal(RoleGreet greet)
    {
        var uow = db.GetDbContext();
        uow.RoleGreets.Remove(greet);
        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Removes multiple role greet configurations.
    /// </summary>
    /// <param name="greet">An array of role greet configurations to remove.</param>
    public async Task MultiRemoveRoleGreetInternal(RoleGreet[] greet)
    {
        var uow = db.GetDbContext();
        uow.RoleGreets.RemoveRange(greet);
        await uow.SaveChangesAsync().ConfigureAwait(false);
    }
}