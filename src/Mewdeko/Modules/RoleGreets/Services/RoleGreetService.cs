using System.Threading.Tasks;

namespace Mewdeko.Modules.RoleGreets.Services;

public class RoleGreetService : INService
{
    private readonly DbService _db;
    private readonly DiscordSocketClient _client;

    public RoleGreetService(DbService db, DiscordSocketClient client, EventHandler eventHandler)
    {
        _client = client;
        _db = db;
        eventHandler.GuildMemberUpdated += DoRoleGreet;
    }

    public async Task<RoleGreet[]> GetGreets(ulong roleId) => await _db.GetDbContext().RoleGreets.ForRoleId(roleId) ?? Array.Empty<RoleGreet>();

    // ReSharper disable once ReturnTypeCanBeNotNullable
    public RoleGreet[]? GetListGreets(ulong guildId) =>
        _db.GetDbContext().RoleGreets.Where(x => x.GuildId == guildId).ToArray();

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
            var webhooks = greets.Where(x => x.WebhookUrl is not null).Select(x => new DiscordWebhookClient(x.WebhookUrl));
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

    private async Task HandleChannelGreets(IEnumerable<RoleGreet> multiGreets, SocketRole role, SocketGuildUser user)
    {
        var checkGreets = multiGreets.Where(x => x.RoleId == role.Id);
        if (!checkGreets.Any())
            return;
        var replacer = new ReplacementBuilder().WithUser(user).WithClient(_client).WithServer(_client, user.Guild).Build();
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
            if (SmartEmbed.TryParse(content, user.Guild?.Id, out var embedData, out var plainText, out var components))
            {
                if (embedData is not null && plainText is not "")
                {
                    var msg = await channel.SendMessageAsync(plainText, embeds: embedData, components: components?.Build()).ConfigureAwait(false);
                    if (i.DeleteTime > 0)
                        msg.DeleteAfter(i.DeleteTime);
                }

                if (embedData is null && plainText is not null)
                {
                    var msg = await channel.SendMessageAsync(plainText, components:components?.Build()).ConfigureAwait(false);
                    if (i.DeleteTime > 0)
                        msg.DeleteAfter(i.DeleteTime);
                }

                if (embedData is not null && plainText is "")
                {
                    var msg = await channel.SendMessageAsync(embeds: embedData, components:components?.Build()).ConfigureAwait(false);
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
    }
    private async Task HandleWebhookGreets(IEnumerable<RoleGreet> multiGreets, SocketRole role, SocketGuildUser user)
    {
        var checkGreets = multiGreets.Where(x => x.RoleId == role.Id);
        if (!checkGreets.Any())
            return;
        var replacer = new ReplacementBuilder().WithUser(user).WithClient(_client).WithServer(_client, user.Guild).Build();
        foreach (var i in checkGreets)
        {
            if (i.WebhookUrl == null)
                continue;
            if (i.Disabled)
                continue;
            if (!i.GreetBots && user.IsBot)
                continue;

            if (i.WebhookUrl is null) continue;
            var webhook = new DiscordWebhookClient(i.WebhookUrl);
            var channel = user.Guild.GetTextChannel(i.ChannelId);
            if (channel is null)
            {
                await RemoveRoleGreetInternal(i).ConfigureAwait(false);
                continue;
            }
            var content = replacer.Replace(i.Message);
            if (SmartEmbed.TryParse(content, channel.Guild?.Id, out var embedData, out var plainText, out var components))
            {
                if (embedData is not null && plainText is not "")
                {
                    var msg = await webhook.SendMessageAsync(plainText, embeds: embedData, components:components?.Build()).ConfigureAwait(false);
                    if (i.DeleteTime > 0)
                        (await user.Guild.GetTextChannel(i.ChannelId).GetMessageAsync(msg).ConfigureAwait(false)).DeleteAfter(i.DeleteTime);
                }

                if (embedData is null && plainText is not null)
                {
                    var msg = await webhook.SendMessageAsync(plainText, components: components?.Build()).ConfigureAwait(false);
                    if (i.DeleteTime > 0)
                        (await user.Guild.GetTextChannel(i.ChannelId).GetMessageAsync(msg).ConfigureAwait(false)).DeleteAfter(i.DeleteTime);
                }

                if (embedData is not null && plainText is "")
                {
                    var msg = await webhook.SendMessageAsync(embeds: embedData, components:components?.Build()).ConfigureAwait(false);
                    if (i.DeleteTime > 0)
                        (await user.Guild.GetTextChannel(i.ChannelId).GetMessageAsync(msg).ConfigureAwait(false)).DeleteAfter(i.DeleteTime);
                }
            }
            else
            {
                var msg = await webhook.SendMessageAsync(content).ConfigureAwait(false);
                if (i.DeleteTime > 0)
                    (await user.Guild.GetTextChannel(i.ChannelId).GetMessageAsync(msg).ConfigureAwait(false)).DeleteAfter(i.DeleteTime);
            }
        }
    }

    public async Task<bool> AddRoleGreet(ulong guildId, ulong channelId, ulong roleId)
    {
        if ((await GetGreets(guildId)).Length == 10)
            return false;
        var toadd = new RoleGreet { ChannelId = channelId, GuildId = guildId, RoleId = roleId };
        var uow = _db.GetDbContext();
        uow.RoleGreets.Add(toadd);
        await uow.SaveChangesAsync().ConfigureAwait(false);
        return true;
    }

    public async Task ChangeMgMessage(RoleGreet greet, string code)
    {
        var uow = _db.GetDbContext();
        greet.Message = code;
        uow.RoleGreets.Update(greet);
        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task RoleGreetDisable(RoleGreet greet, bool disabled)
    {
        var uow = _db.GetDbContext();
        greet.Disabled = disabled;
        uow.RoleGreets.Update(greet);
        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task ChangeRgDelete(RoleGreet greet, int howlong)
    {
        var uow = _db.GetDbContext();
        greet.DeleteTime = howlong;
        uow.RoleGreets.Update(greet);
        await uow.SaveChangesAsync().ConfigureAwait(false);
    }
    public async Task ChangeMgWebhook(RoleGreet greet, string webhookurl)
    {
        var uow = _db.GetDbContext();
        greet.WebhookUrl = webhookurl;
        uow.Update(greet);
        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task ChangeRgGb(RoleGreet greet, bool enabled)
    {
        var uow = _db.GetDbContext();
        greet.GreetBots = enabled;
        uow.Update(greet);
        await uow.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task RemoveRoleGreetInternal(RoleGreet greet)
    {
        var uow = _db.GetDbContext();
        uow.RoleGreets.Remove(greet);
        await uow.SaveChangesAsync().ConfigureAwait(false);
    }
    public async Task MultiRemoveRoleGreetInternal(RoleGreet[] greet)
    {
        var uow = _db.GetDbContext();
        uow.RoleGreets.RemoveRange(greet);
        await uow.SaveChangesAsync().ConfigureAwait(false);
    }
}