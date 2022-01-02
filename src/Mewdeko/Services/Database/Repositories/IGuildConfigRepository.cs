using System.Collections.Generic;
using Mewdeko.Services.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Services.Database.Repositories;

public interface IGuildConfigRepository : IRepository<GuildConfig>
{
    GuildConfig ForId(ulong guildId, Func<DbSet<GuildConfig>, IQueryable<GuildConfig>> includes = null);
    GuildConfig LogSettingsFor(ulong guildId);
    IEnumerable<GuildConfig> GetAllGuildConfigs(List<ulong> availableGuilds);
    IEnumerable<FollowedStream> GetFollowedStreams(List<ulong> included);
    IEnumerable<FollowedStream> GetFollowedStreams();
    ulong GetCleverbotChannel(ulong guildid);
    IEnumerable<GuildConfig> Permissionsv2ForAll(List<ulong> include);
    GuildConfig GcWithPermissionsv2For(ulong guildId);
    XpSettings XpSettingsFor(ulong guildId);
    IEnumerable<GeneratingChannel> GetGeneratingChannels();
}

public class GeneratingChannel
{
    public ulong GuildId { get; set; }
    public ulong ChannelId { get; set; }
}