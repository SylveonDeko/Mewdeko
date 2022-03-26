using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Mewdeko._Extensions;
using Mewdeko.Database;
using System.Collections.Concurrent;

namespace Mewdeko.Modules.Server_Management.Services;

public class ServerManagementService : INService
{

    public CommandContext Ctx;

    public ServerManagementService(DiscordSocketClient client, DbService db, Mewdeko bot)
    {

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
    

    public async Task<IRole> GetMuteRole(IGuild guild)
    {
        if (guild == null)
            throw new ArgumentNullException(nameof(guild));

        const string defaultMuteRoleName = "Mewdeko-mute";

        var muteRoleName = GuildMuteRoles.GetOrAdd(guild.Id, defaultMuteRoleName);

        var muteRole = guild.Roles.FirstOrDefault(r => r.Name == muteRoleName);
        if (muteRole != null) return muteRole;
        {
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
        }

        return muteRole;
    }
}