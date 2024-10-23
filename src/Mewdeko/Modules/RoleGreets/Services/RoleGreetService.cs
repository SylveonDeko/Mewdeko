using Discord.Net;
using LinqToDB;
using Mewdeko.Database.DbContextStuff;
using Mewdeko.Modules.Utility.Services;
using Serilog;

namespace Mewdeko.Modules.RoleGreets.Services;

/// <summary>
/// Provides functionalities related to greeting users with specific roles in a guild.
/// </summary>
public class RoleGreetService : INService
{
    private readonly DiscordShardedClient client;
    private readonly DbContextProvider dbProvider;
    private readonly InviteCountService inviteCountService;

    /// <summary>
    /// Initializes a new instance of the <see cref="RoleGreetService"/> class.
    /// </summary>
    /// <param name="dbProvider">The database context provider.</param>
    /// <param name="client">The Discord client.</param>
    /// <param name="eventHandler">The event handler for guild member update events.</param>
    /// <param name="inviteCountService">The invite count service</param>
    public RoleGreetService(DbContextProvider dbProvider, DiscordShardedClient client, EventHandler eventHandler, InviteCountService inviteCountService)
    {
        this.client = client;
        this.inviteCountService = inviteCountService;
        this.dbProvider = dbProvider;
        eventHandler.GuildMemberUpdated += DoRoleGreet;
    }

    /// <summary>
    /// Retrieves an array of <see cref="RoleGreet"/> configurations for a specific role.
    /// </summary>
    /// <param name="roleId">The unique identifier of the role.</param>
    /// <returns>An array of <see cref="RoleGreet"/> objects.</returns>
    public async Task<RoleGreet[]> GetGreets(ulong roleId) =>
        await WithMewdekoContext(db => db.RoleGreets.ForRoleId(roleId));

    /// <summary>
    /// Retrieves a list of <see cref="RoleGreet"/> configurations for a specific guild.
    /// </summary>
    /// <param name="guildId">The unique identifier of the guild.</param>
    /// <returns>An array of <see cref="RoleGreet"/> objects if any are found; otherwise, an empty array.</returns>
    public async Task<RoleGreet[]> GetListGreets(ulong guildId) =>
        await WithMewdekoContext(db => db.RoleGreets.Where(x => x.GuildId == guildId).ToArrayAsync());

    /// <summary>
    /// Adds a new role greet configuration.
    /// </summary>
    /// <param name="guildId">The unique identifier of the guild.</param>
    /// <param name="channelId">The unique identifier of the channel.</param>
    /// <param name="roleId">The unique identifier of the role.</param>
    /// <returns>True if the configuration was added successfully; otherwise, false.</returns>
    public async Task<bool> AddRoleGreet(ulong guildId, ulong channelId, ulong roleId)
    {
        if ((await GetGreets(roleId)).Length == 10)
            return false;

        return await WithMewdekoContextNoReturn(db =>
        {
            db.RoleGreets.Add(new RoleGreet
            {
                ChannelId = channelId,
                GuildId = guildId,
                RoleId = roleId
            });
            return db.SaveChangesAsync();
        });
    }

    /// <summary>
    /// Updates the message content of a role greet configuration.
    /// </summary>
    /// <param name="greet">The role greet configuration to update.</param>
    /// <param name="code">The new message content.</param>
    public async Task ChangeMgMessage(RoleGreet greet, string code) =>
        await UpdateRoleGreet(greet, rg => rg.Message = code);

    /// <summary>
    /// Enables or disables a role greet configuration.
    /// </summary>
    /// <param name="greet">The role greet configuration to update.</param>
    /// <param name="disabled">Specifies whether the greet should be disabled.</param>
    public async Task RoleGreetDisable(RoleGreet greet, bool disabled) =>
        await UpdateRoleGreet(greet, rg => rg.Disabled = disabled);

    /// <summary>
    /// Updates the deletion time for messages sent by a role greet configuration.
    /// </summary>
    /// <param name="greet">The role greet configuration to update.</param>
    /// <param name="howlong">The time in seconds after which the greet message should be deleted.</param>
    public async Task ChangeRgDelete(RoleGreet greet, int howlong) =>
        await UpdateRoleGreet(greet, rg => rg.DeleteTime = howlong);

    /// <summary>
    /// Updates the webhook URL of a role greet configuration.
    /// </summary>
    /// <param name="greet">The role greet configuration to update.</param>
    /// <param name="webhookurl">The new webhook URL.</param>
    public async Task ChangeMgWebhook(RoleGreet greet, string webhookurl) =>
        await UpdateRoleGreet(greet, rg => rg.WebhookUrl = webhookurl);

    /// <summary>
    /// Enables or disables greeting bots for a role greet configuration.
    /// </summary>
    /// <param name="greet">The role greet configuration to update.</param>
    /// <param name="enabled">Specifies whether bots should be greeted.</param>
    public async Task ChangeRgGb(RoleGreet greet, bool enabled) =>
        await UpdateRoleGreet(greet, rg => rg.GreetBots = enabled);

    /// <summary>
    /// Removes a specific role greet configuration.
    /// </summary>
    /// <param name="greet">The role greet configuration to remove.</param>
    public async Task RemoveRoleGreetInternal(RoleGreet greet) =>
        await WithMewdekoContextNoReturn(db =>
        {
            db.RoleGreets.Remove(greet);
            return db.SaveChangesAsync();
        });

    /// <summary>
    /// Removes multiple role greet configurations.
    /// </summary>
    /// <param name="greets">An array of role greet configurations to remove.</param>
    public async Task MultiRemoveRoleGreetInternal(RoleGreet[] greets) =>
        await WithMewdekoContextNoReturn(db =>
        {
            db.RoleGreets.RemoveRange(greets);
            return db.SaveChangesAsync();
        });

    private async Task DoRoleGreet(Cacheable<SocketGuildUser, ulong> cacheable, SocketGuildUser socketGuildUser)
    {
        var user = await cacheable.GetOrDownloadAsync().ConfigureAwait(false);
        if (user.Roles.SequenceEqual(socketGuildUser.Roles))
        {
            if (user.Roles.Count > socketGuildUser.Roles.Count)
                return;
        }

        var diffRoles = socketGuildUser.Roles.Where(r => !user.Roles.Contains(r)).ToArray();
        foreach (var role in diffRoles)
        {
            var greets = await GetGreets(role.Id);
            if (greets.Length == 0) continue;

            var replacer = new ReplacementBuilder().WithUser(socketGuildUser).WithClient(client)
                .WithServer(client, socketGuildUser.Guild);

            foreach (var greet in greets)
            {
                await HandleGreet(greet, socketGuildUser, replacer);
            }
        }
    }

    private async Task HandleGreet(RoleGreet greet, SocketGuildUser user, ReplacementBuilder replacer)
    {
        if (user.IsBot && !greet.GreetBots)
            return;

        if (greet.Disabled)
            return;

        var inviteSettings = await inviteCountService.GetInviteCountSettingsAsync(user.Guild.Id);
        if (inviteSettings.IsEnabled)
        {
            await Task.Delay(500);
            var inviter = await inviteCountService.GetInviter(user.Id, user.Guild);
            if (inviter == null)
            {
                replacer.WithOverride("%inviter.username%", () => "Unknown");
                replacer.WithOverride("%inviter.avatar%", () => "Unknown");
                replacer.WithOverride("%inviter.id%", () => "Unknown");
                replacer.WithOverride("%inviter.mention%", () => "Unknown");
            }
            else
            {
                var invCount = await inviteCountService.GetInviteCount(inviter.Id, user.Guild.Id);
                replacer.WithOverride("%inviter.username%", () => inviter.Username);
                replacer.WithOverride("%inviter.avatar%", () => inviter.GetAvatarUrl());
                replacer.WithOverride("%inviter.id%", () => user.Id.ToString());
                replacer.WithOverride("%inviter.mention%", () => user.Mention);
                replacer.WithOverride("%inviter.count%", () => invCount.ToString());
            }
        }

        var content = replacer.Build().Replace(greet.Message);
        var channel = user.Guild.GetTextChannel(greet.ChannelId);

        if (channel == null)
        {
            await RemoveRoleGreetInternal(greet);
            return;
        }

        try
        {
            if (greet.WebhookUrl != null)
            {
                var webhook = new DiscordWebhookClient(greet.WebhookUrl);
                var msgId = await SendSmartEmbedWebhookMessage(webhook, content, user.Guild.Id);
                if (greet.DeleteTime > 0)
                {
                    var msg = await channel.GetMessageAsync(msgId);
                    msg?.DeleteAfter(greet.DeleteTime);
                }
            }
            else
            {
                await SendSmartEmbedMessage(channel, content, user.Guild.Id, greet.DeleteTime);
            }
        }
        catch (HttpException ex)
        {
            if (ex.DiscordCode is DiscordErrorCode.UnknownWebhook or DiscordErrorCode.InvalidWebhookToken or DiscordErrorCode.MissingPermissions)
            {
                await RoleGreetDisable(greet, true);
                Log.Information($"RoleGreet disabled in {user.Guild} due to {ex.DiscordCode}.");
            }
        }
    }

    private static async Task SendSmartEmbedMessage(IMessageChannel channel, string content, ulong guildId, int deleteTime = 0)
    {
        if (SmartEmbed.TryParse(content, guildId, out var embedData, out var plainText, out var components))
        {
            var msg = await channel.SendMessageAsync(plainText, embeds: embedData, components: components?.Build());
            if (deleteTime > 0)
                msg.DeleteAfter(deleteTime);
        }
        else
        {
            var msg = await channel.SendMessageAsync(content);
            if (deleteTime > 0)
                msg.DeleteAfter(deleteTime);
        }
    }

    private async Task<ulong> SendSmartEmbedWebhookMessage(DiscordWebhookClient webhook, string content, ulong guildId)
    {
        if (SmartEmbed.TryParse(content, guildId, out var embedData, out var plainText, out var components))
        {
            return await webhook.SendMessageAsync(plainText, embeds: embedData, components: components?.Build());
        }
        else
        {
            return await webhook.SendMessageAsync(content);
        }
    }

    private async Task<T> WithMewdekoContext<T>(Func<MewdekoContext, Task<T>> action)
    {
        await using var mewdekoContext = await dbProvider.GetContextAsync();
        return await action(mewdekoContext);
    }

    private async Task<bool> WithMewdekoContextNoReturn(Func<MewdekoContext, Task> action)
    {
        await using var mewdekoContext = await dbProvider.GetContextAsync();
        await action(mewdekoContext);
        return true;
    }

    private async Task UpdateRoleGreet(RoleGreet greet, Action<RoleGreet> updateAction) =>
        await WithMewdekoContextNoReturn(db =>
        {
            updateAction(greet);
            db.RoleGreets.Update(greet);
            return db.SaveChangesAsync();
        });
}