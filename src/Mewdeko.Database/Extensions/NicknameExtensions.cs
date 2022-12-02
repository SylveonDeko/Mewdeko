using Mewdeko.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database.Extensions;

public static class NicknameExtensions
{
    public static IEnumerable<string> GetNicknames(this DbSet<Nicknames> nicknames, ulong userId, ulong guildId)
        => nicknames.Where(x => x.UserId == userId && x.GuildId == guildId).Select(x => x.Nickname);

    public static IEnumerable<string> GetNicknames(this DbSet<Nicknames> nicknames, ulong userId)
        => nicknames.Where(x => x.UserId == userId).Select(x => x.Nickname);
}