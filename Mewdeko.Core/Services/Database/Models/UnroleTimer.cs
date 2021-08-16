using System;

namespace Mewdeko.Core.Services.Database.Models
{
    public class UnroleTimer : DbEntity
    {
        public ulong UserId { get; set; }
        public ulong RoleId { get; set; }
        public DateTime UnbanAt { get; set; }

        public override int GetHashCode()
        {
            return UserId.GetHashCode() ^ RoleId.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return obj is UnroleTimer ut
                ? ut.UserId == UserId && ut.RoleId == RoleId
                : false;
        }
    }
}