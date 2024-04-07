namespace Mewdeko.Modules.Server_Management.Services;

/// <summary>
/// Provides functionalities for managing server-specific settings and roles, particularly mute roles.
/// </summary>
public class ServerManagementService : INService
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ServerManagementService"/> class.
    /// </summary>
    /// <param name="bot">The bot instance containing configurations for all guilds.</param>
    public ServerManagementService(Mewdeko bot)
    {
        var allgc = bot.AllGuildConfigs;
        GuildMuteRoles = allgc
            .Where(c => !string.IsNullOrWhiteSpace(c.MuteRoleName))
            .ToDictionary(c => c.GuildId, c => c.MuteRoleName)
            .ToConcurrent();
    }


    private ConcurrentDictionary<ulong, string> GuildMuteRoles { get; }

    /// <summary>
    /// Retrieves the mute role for the specified guild, creating one if it does not exist.
    /// </summary>
    /// <param name="guild">The guild for which to retrieve or create the mute role.</param>
    /// <returns>The mute role for the guild.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="guild"/> parameter is null.</exception>
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