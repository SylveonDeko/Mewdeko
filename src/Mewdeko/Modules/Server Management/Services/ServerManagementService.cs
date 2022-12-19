using System.Threading.Tasks;

namespace Mewdeko.Modules.Server_Management.Services;

public class ServerManagementService : INService
{
    public ServerManagementService(DiscordSocketClient client, DbService db)
    {
        using var uow = db.GetDbContext();
        var configs = uow.GuildConfigs.Where(x => client.Guilds.Select(socketGuild => socketGuild.Id).Contains(x.GuildId));

        GuildMuteRoles = configs
            .Where(c => !string.IsNullOrWhiteSpace(c.MuteRoleName))
            .ToDictionary(c => c.GuildId, c => c.MuteRoleName)
            .ToConcurrent();
    }

    public ConcurrentDictionary<ulong, string> GuildMuteRoles { get; }

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