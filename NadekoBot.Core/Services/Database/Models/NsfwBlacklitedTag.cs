using System;

namespace NadekoBot.Core.Services.Database.Models
{
    public class NsfwBlacklitedTag : DbEntity
    {
        public string Tag { get; set; }

        public override int GetHashCode()
        {
            return Tag.GetHashCode(StringComparison.InvariantCulture);
        }

        public override bool Equals(object obj)
        {
            return obj is NsfwBlacklitedTag x
                ? x.Tag == Tag
                : false;
        }
    }
}
