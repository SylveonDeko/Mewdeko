using System;

namespace Mewdeko.Services.Database.Models;

public class UnbanTimer : DbEntity
{
    public ulong UserId { get; set; }
    public DateTime UnbanAt { get; set; }

    public override int GetHashCode()
    {
        return UserId.GetHashCode();
    }

    public override bool Equals(object obj)
    {
        return obj is UnbanTimer ut
            ? ut.UserId == UserId
            : false;
    }
}