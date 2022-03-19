using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Mewdeko._Extensions;
using Mewdeko.Database;
using Mewdeko.Database.Extensions;
using System.Collections.Concurrent;

namespace Mewdeko.Modules.Server_Management.Services;

public class ServerManagementService : INService
{
    private static readonly OverwritePermissions _denyOverwrite =
        new(addReactions: PermValue.Deny, sendMessages: PermValue.Deny,
            attachFiles: PermValue.Deny, viewChannel: PermValue.Deny);

    private readonly Mewdeko _bot;
    private readonly DbService _db;
    public DiscordSocketClient Client;

    public CommandContext Ctx;

    public ServerManagementService(DiscordSocketClient client, DbService db, Mewdeko bot)
    {
        Client = client;
        _db = db;
        _bot = bot;
        Ticketchannelids = bot.AllGuildConfigs
            .Where(x => x.TicketCategory != 0)
            .ToDictionary(x => x.GuildId, x => x.TicketCategory)
            .ToConcurrent();

        using var uow = db.GetDbContext();
        var guildIds = client.Guilds.Select(x => x.Id).ToList();
        var configs = uow.GuildConfigs.AsQueryable()
            .Where(x => guildIds.Contains(x.GuildId))
            .ToList();

        GuildMuteRoles = configs
            .Where(c => !string.IsNullOrWhiteSpace(c.MuteRoleName))
            .ToDictionary(c => c.GuildId, c => c.MuteRoleName)
            .ToConcurrent();
    }

    public ConcurrentDictionary<ulong, string> GuildMuteRoles { get; }
    private ConcurrentDictionary<ulong, ulong> Ticketchannelids { get; } = new();

    public ulong GetTicketCategory(ulong? id)
    {
        if (id == null || !Ticketchannelids.TryGetValue(id.Value, out var ticketcat))
            return 0;

        return ticketcat;
    }

    public async Task SetTicketCategoryId(IGuild guild, ICategoryChannel channel)
    {
        await using (var uow = _db.GetDbContext())
        {
            var gc = uow.ForGuildId(guild.Id, set => set);
            gc.TicketCategory = channel.Id;
            await uow.SaveChangesAsync();
        }

        Ticketchannelids.AddOrUpdate(guild.Id, channel.Id, (_, _) => channel.Id);
    }

    public async Task<IRole> GetMuteRole(IGuild guild)
    {
        if (guild == null)
            throw new ArgumentNullException(nameof(guild));

        const string defaultMuteRoleName = "Mewdeko-mute";

        var muteRoleName = GuildMuteRoles.GetOrAdd(guild.Id, defaultMuteRoleName);

        var muteRole = guild.Roles.FirstOrDefault(r => r.Name == muteRoleName);
        if (muteRole == null)
            //if it doesn't exist, create it
            try
            {
                muteRole = await guild.CreateRoleAsync(muteRoleName, isMentionable: false).ConfigureAwait(false);
            }
            catch
            {
                //if creations fails,  maybe the name != correct, find default one, if doesn't work, create default one
                muteRole = guild.Roles.FirstOrDefault(r => r.Name == muteRoleName) ??
                           await guild.CreateRoleAsync(defaultMuteRoleName, isMentionable: false)
                               .ConfigureAwait(false);
            }

        return muteRole;
    }
}