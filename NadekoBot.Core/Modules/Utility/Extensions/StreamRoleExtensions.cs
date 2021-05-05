using Microsoft.EntityFrameworkCore;
using NadekoBot.Core.Services.Database.Models;
using NadekoBot.Core.Services.Database.Repositories;

namespace NadekoBot.Modules.Utility.Extensions
{
    public static class StreamRoleExtensions
    {
        /// <summary>
        /// Gets full stream role settings for the guild with the specified id.
        /// </summary>
        /// <param name="gc"></param>
        /// <param name="guildId">Id of the guild to get stream role settings for.</param>
        /// <returns></returns>
        public static StreamRoleSettings GetStreamRoleSettings(this IGuildConfigRepository gc, ulong guildId)
        {
            var conf = gc.ForId(guildId, set => set.Include(y => y.StreamRole)
                .Include(y => y.StreamRole.Whitelist)
                .Include(y => y.StreamRole.Blacklist));

            if (conf.StreamRole == null)
                conf.StreamRole = new StreamRoleSettings();

            return conf.StreamRole;
        }
    }
}
