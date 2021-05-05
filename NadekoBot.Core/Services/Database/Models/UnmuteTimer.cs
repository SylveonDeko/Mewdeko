using System;

namespace NadekoBot.Core.Services.Database.Models
{
    public class UnmuteTimer : DbEntity
    {
        public ulong UserId { get; set; }
        public DateTime UnmuteAt { get; set; }

        public override int GetHashCode() =>
            UserId.GetHashCode();

        public override bool Equals(object obj)
        {
            return obj is UnmuteTimer ut
                ? ut.UserId == UserId
                : false;
        }
    }
}
