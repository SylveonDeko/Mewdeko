using Mewdeko.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Mewdeko.Database.Extensions;

public static class UsernameExtensions
{
    public static IEnumerable<string> GetNicknames(this DbSet<Usernames> usernames, ulong userId)
        => usernames.Where(x => x.UserId == userId).Select(x => x.Username);
}